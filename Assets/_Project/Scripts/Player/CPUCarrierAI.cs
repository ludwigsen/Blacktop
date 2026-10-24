using System.Collections.Generic;
using UnityEngine;

// Drives whoever's currently carrying the ball, ONLY when that carrier belongs to a team
// the human isn't controlling. PossessionController already puts a CPU carrier into
// "AICarrier" mode — it just doesn't move him. This fills that gap.
//
// Two jobs share one component because they share a gate and a Transform, not because
// they share behavior:
//   - QB pre-throw: dropback/read/scramble decision, pass plays only. Run plays need
//     NOTHING here — HandoffCoordinator already autopaths the RB to the mesh point and
//     reassigns the carrier regardless of who's driving the QB.
//   - Runner (RB/WR/scrambling QB): crude broken-field steering — run at the end zone,
//     lean away from the nearest defenders. No committed Juke/Hurdle/StiffArm yet — v1.
//
// SETUP: add to every offensive-capable player — Ally.prefab, plus T1Player01 (the one
// bespoke non-prefab player in the scene).
public class CPUCarrierAI : MonoBehaviour
{
    [Header("Quarterback")]
    [SerializeField] float dropbackSpeed = 2.5f;
    [SerializeField] float maxReadTime = 1.4f;
    [SerializeField] float scrambleTriggerDistance = 3f;
    [SerializeField] float maxReceiverSearchRadius = 45f;
    [SerializeField] float receiverSearchConeAngle = 90f; // wider than PassMove's human cone — no human read to defer to

    [Header("Throw Physics (effective Passing scales flight speed)")]
    [SerializeField] float baseThrowSpeed = 30f;
    [SerializeField] float minFlightDuration = 0.15f;
    [SerializeField] float baseArcHeight = 0.5f;
    [SerializeField] float arcHeightPerDistance = 0.12f;

    [Header("Runner")]
    [SerializeField] float runSpeed = 7f;
    [SerializeField] float rotationSpeed = 540f;
    [SerializeField] float avoidanceRadius = 4f;
    [SerializeField] float avoidanceWeight = 1.2f;
    [SerializeField] int threatsConsidered = 2;

    PlayerAttributes attributes;
    public void SetAttributes(PlayerAttributes value) => attributes = value;
    // Attribute multipliers already contain the team boost; never multiply Gamebreaker again.
    float SpeedBoost => attributes != null ? attributes.Speed() : 1f;
    float PassingBoost => attributes != null ? attributes.Passing() : 1f;
    DifficultyTuning Difficulty => DifficultyTuning.ForTeam(teamMember.teamId);
    TeamMember teamMember;
    float readTimer;

    void Awake() => teamMember = GetComponent<TeamMember>();

    bool ShouldDrive =>
        PlayState.Instance != null &&
        BallController.Instance != null &&
        BallController.Instance.State == BallController.BallState.Held &&
        BallController.Instance.Carrier == transform &&
        teamMember != null &&
        !PlayState.Instance.IsUserTeam(teamMember.teamId);

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        if (!ShouldDrive) { readTimer = 0f; return; }

        // A QB who recovers the other team's fumble is a returner, not the passer
        // for the old play call. Never let him throw to the opposing offense's roster.
        if (teamMember.slot == TeamMember.RosterSlot.QB &&
            teamMember.teamId == PlayState.Instance.PossessionTeamId)
            UpdateQuarterback();
        else
            UpdateRunner();
    }

    void UpdateQuarterback()
    {
        var playCall = PlayState.Instance.CurrentPlayCall;
        if (playCall == null || playCall.PlayType != PlayType.Pass)
        {
            readTimer = 0f;
            return; // run/no-call: stand for the mesh point, HandoffCoordinator does the rest
        }

        readTimer += Time.deltaTime;
        bool flushed = NearestOpponentDistance() < scrambleTriggerDistance;

        float readTime = maxReadTime * Difficulty.QbReadTimeScale;
        if (flushed || readTimer >= readTime)
        {
            var target = PassTargeting.FindBestReceiver(transform, PlayState.Instance.OffensivePlayers, maxReceiverSearchRadius, receiverSearchConeAngle);
            if (target != null) { ThrowTo(target); return; }
            if (flushed || readTimer >= readTime * 1.5f) { UpdateRunner(); return; } // nobody's open — tuck it and go
        }

        transform.position += -PlayState.Instance.AttackDirection.Forward * (dropbackSpeed * SpeedBoost * Time.deltaTime);
    }

    void ThrowTo(Transform target)
    {
        float distance = Vector3.Distance(transform.position, target.position);
        float duration = Mathf.Max(distance / (baseThrowSpeed * PassingBoost), minFlightDuration);
        float arc = baseArcHeight + distance * arcHeightPerDistance;
        BallController.Instance.Throw(target.position, target, isPitch: false, arcHeight: arc, duration: duration);
        readTimer = 0f;
    }

    void UpdateRunner()
    {
        // PossessionTeamId changes at the whistle; return direction changes immediately.
        Vector3 desired = PlayState.Instance.DirectionFor(teamMember.teamId).Forward;

        foreach (var threatPos in NearestThreatPositions(Mathf.Max(1, threatsConsidered + Difficulty.ThreatsConsidered - 2)))
        {
            Vector3 away = transform.position - threatPos;
            away.y = 0f;
            float dist = away.magnitude;
            if (dist < 0.01f || dist > avoidanceRadius) continue;
            desired += away.normalized * ((avoidanceRadius - dist) / avoidanceRadius) * avoidanceWeight * Difficulty.AvoidanceScale;
        }

        desired.y = 0f;
        if (desired.sqrMagnitude < 0.0001f) return;
        desired.Normalize();

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(desired), rotationSpeed * Time.deltaTime);
        transform.position += desired * (runSpeed * SpeedBoost * Time.deltaTime);
    }

    float NearestOpponentDistance()
    {
        float closest = float.MaxValue;
        foreach (var member in FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!TeamMember.AreOpponents(this, member)) continue;
            float dist = Vector3.Distance(transform.position, member.transform.position);
            if (dist < closest) closest = dist;
        }
        return closest;
    }

    List<Vector3> NearestThreatPositions(int count)
    {
        var all = new List<(Transform t, float dist)>();
        foreach (var member in FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!TeamMember.AreOpponents(this, member)) continue;
            all.Add((member.transform, Vector3.Distance(transform.position, member.transform.position)));
        }
        all.Sort((a, b) => a.dist.CompareTo(b.dist));

        var result = new List<Vector3>();
        for (int i = 0; i < Mathf.Min(count, all.Count); i++) result.Add(all[i].t.position);
        return result;
    }
}
