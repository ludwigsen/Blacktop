using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The CPU picks its side's play the instant the huddle forms — simultaneous with, and blind
// to, the human's pick. Nothing waits on it. It never breaks the huddle (the human's Confirm
// does that; the play clock is the backstop). The one thing it does drive is the SNAP when
// the CPU has the ball, so a human defender doesn't stare at a set offense for 40 seconds.
//
//   CPU on offense -> SetPlayCall from down & distance (run/pass split), then auto-snap.
//   CPU on defense -> SetDefensivePlay, weighted by situation (blitz short, zone long).
//
// Deliberately dumb: no formation read, no tendencies. Weighted randomness is enough to keep
// it from being predictable; smarter comes later if playtesting says it's needed.
//
// SETUP: drop on GameManager; assign the scene's PlayCallSelector (shared playbook).
public class CPUPlayCaller : MonoBehaviour
{
    [SerializeField] PlayCallSelector playCallSelector;

    [Header("Snap (CPU offense only)")]
    [SerializeField] float snapDelayAfterSet = 1.5f;

    [Header("Offense: chance of RUN by situation")]
    [SerializeField, Range(0f, 1f)] float runChanceStandard = 0.55f;
    [SerializeField, Range(0f, 1f)] float runChanceShortYardage = 0.7f;
    [SerializeField, Range(0f, 1f)] float runChanceLongYardage = 0.25f;
    [SerializeField, Range(0f, 1f)] float runChanceGoalToGo = 0.65f;

    [Header("Defense: situational weighting")]
    [Tooltip("Extra weight per rusher on a play when the offense is in short yardage or goal-to-go.")]
    [SerializeField] float blitzBonusShortYardage = 1.5f;
    [Tooltip("Extra weight per zone defender on a play when the offense needs a lot of yards.")]
    [SerializeField] float zoneBonusLongYardage = 1.0f;

    [Header("Thresholds")]
    [SerializeField] float shortYardageThreshold = 3f;
    [SerializeField] float longYardageThreshold = 7f;

    // Start(), not OnEnable(): PlayState.Instance only guaranteed after every Awake().
    void Start()
    {
        var ps = PlayState.Instance;
        if (ps == null) return;

        ps.OnHuddleStarted += PickForCpuSide;
        ps.OnSetAtLOS += HandleSetAtLOS;

        // First play of a session skips GatherToHuddleRoutine, so OnHuddleStarted never fires for it.
        if (ps.IsInHuddle) PickForCpuSide();
    }

    void OnDestroy()
    {
        var ps = PlayState.Instance;
        if (ps == null) return;
        ps.OnHuddleStarted -= PickForCpuSide;
        ps.OnSetAtLOS -= HandleSetAtLOS;
    }

    bool CpuHasBall => PlayState.Instance != null && !PlayState.Instance.IsUserTeam(PlayState.Instance.PossessionTeamId);

    void PickForCpuSide()
    {
        var ps = PlayState.Instance;
        if (ps == null || playCallSelector == null) return;

        if (CpuHasBall)
        {
            var play = ChooseOffense();
            if (play != null) ps.SetPlayCall(play);
        }
        else
        {
            var play = ChooseDefense();
            if (play != null) ps.SetDefensivePlay(play);
        }
    }

    void HandleSetAtLOS()
    {
        if (!CpuHasBall) return; // human on offense snaps their own ball
        StopAllCoroutines();
        StartCoroutine(SnapAfterDelay());
    }

    IEnumerator SnapAfterDelay()
    {
        yield return new WaitForSeconds(snapDelayAfterSet);
        var ps = PlayState.Instance;
        if (ps != null && ps.IsSetAtLOS) ps.ResetPlay(); // no-op if the human already snapped
    }

    // ---- Offense ------------------------------------------------------------

    PlayCallData ChooseOffense()
    {
        var all = playCallSelector.OffensivePlays;
        if (all == null || all.Count == 0) return null;

        bool wantRun = Random.value < RunChanceForSituation();
        var primary = FilterByType(all, wantRun ? PlayType.Run : PlayType.Pass);
        var fallback = FilterByType(all, wantRun ? PlayType.Pass : PlayType.Run);

        if (primary.Count > 0) return primary[Random.Range(0, primary.Count)];
        if (fallback.Count > 0) return fallback[Random.Range(0, fallback.Count)];
        return all[Random.Range(0, all.Count)];
    }

    float RunChanceForSituation()
    {
        var downs = DownsTracker.Instance;
        if (downs == null) return runChanceStandard;

        if (downs.IsGoalToGo) return runChanceGoalToGo;
        if (downs.YardsToGo <= shortYardageThreshold) return runChanceShortYardage;
        if (downs.CurrentDown >= 3 && downs.YardsToGo >= longYardageThreshold) return runChanceLongYardage;
        return runChanceStandard;
    }

    static List<PlayCallData> FilterByType(IReadOnlyList<PlayCallData> source, PlayType type)
    {
        var result = new List<PlayCallData>();
        foreach (var p in source)
            if (p != null && p.PlayType == type) result.Add(p);
        return result;
    }

    // ---- Defense ------------------------------------------------------------

    DefensivePlayData ChooseDefense()
    {
        var all = playCallSelector.DefensivePlays;
        if (all == null || all.Count == 0) return null;

        var downs = DownsTracker.Instance;
        bool shortYardage = downs != null && (downs.IsGoalToGo || downs.YardsToGo <= shortYardageThreshold);
        bool longYardage = downs != null && downs.CurrentDown >= 2 && downs.YardsToGo >= longYardageThreshold;

        // Weighted random: every play is always possible, situation just tilts the odds.
        var weights = new float[all.Count];
        float total = 0f;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] == null) continue;
            float w = 1f;
            if (shortYardage) w += all[i].RushCount * blitzBonusShortYardage;
            if (longYardage) w += all[i].ZoneCount * zoneBonusLongYardage;
            weights[i] = w;
            total += w;
        }
        if (total <= 0f) return null;

        float roll = Random.value * total;
        for (int i = 0; i < all.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f && all[i] != null) return all[i];
        }
        return all[all.Count - 1];
    }
}