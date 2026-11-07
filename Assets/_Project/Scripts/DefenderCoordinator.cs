using System.Collections.Generic;
using UnityEngine;

// Turns the called DefensivePlayData into roles, once, at the snap. Deliberately never looks
// at the offense's play call — defense is blind, same as a human defender. It reacts to what
// it can actually see: who has the ball and where.
//
// Snap:   Rush -> Engage, Spy -> Contain, Zone -> Zone (+ anchor), Man -> matched to the
//         nearest receiver (cascade-by-distance, locked for the whole play so crossing routes
//         can't cause mid-route swaps). Leftover men with nobody to cover fall back to Spy.
// Live:   QB still has it behind the LOS -> assignments stand.
//         Carrier past the LOS (scramble, breakaway) -> everyone Engages.
//         Carrier is NOT the QB (handoff, screen catch) -> run-fit: closest defender Engages,
//         rest Contain; Rush-job defenders stay Engage.
// No defensive play set at all -> the original closest-Engage/rest-Contain scheme, so a
// scene with no defensive plays authored behaves exactly as it did before.
public class DefenderCoordinator : MonoBehaviour
{
    Transform Carrier => BallController.Instance != null ? BallController.Instance.Carrier : null;

    // Start(), not OnEnable(): PlayState.Instance is only guaranteed to exist after every Awake().
    void Start()
    {
        if (PlayState.Instance != null) PlayState.Instance.OnPlayReset += AssignSnapRoles;
    }

    void OnDestroy()
    {
        if (PlayState.Instance != null) PlayState.Instance.OnPlayReset -= AssignSnapRoles;
    }

    void AssignSnapRoles()
    {
        var ps = PlayState.Instance;
        var defense = ps.DefensePlayers; // index N = formation slot N = DefensivePlayData assignment N

        foreach (var t in defense)
            if (t != null && t.TryGetComponent<DefenderAI>(out var ai)) ai.ClearCoverTarget();

        var play = ps.CurrentDefensivePlay;
        if (play == null) return; // fallback scheme runs per-frame in Update()

        var manTargets = GetManTargets(ps, play);

        for (int i = 0; i < defense.Count; i++)
        {
            if (defense[i] == null || !defense[i].TryGetComponent<DefenderAI>(out var ai)) continue;

            // Give the selected player a job too. Human locomotion still owns
            // that actor until the player switches away.
            var assignment = play.GetAssignment(i);
            switch (assignment.job)
            {
                case DefenderJob.Rush:
                    ai.SetRole(DefenderAI.Role.Engage);
                    break;
                case DefenderJob.Spy:
                    ai.SetRole(DefenderAI.Role.Contain);
                    break;
                case DefenderJob.Zone:
                    Vector3 anchor = ZoneAnchor(ps, assignment);
                    anchor.y = ai.transform.position.y;
                    ai.SetZoneAnchor(anchor);
                    ai.SetRole(DefenderAI.Role.Zone);
                    break;
                case DefenderJob.Man:
                    if (manTargets.TryGetValue(ai, out var receiver))
                    {
                        ai.SetRole(DefenderAI.Role.Cover);
                        ai.SetCoverTarget(receiver);
                    }
                    else ai.SetRole(DefenderAI.Role.Contain);
                    break;
            }
        }
    }

    internal static Vector3 ZoneAnchor(PlayState play, DefensivePlayData.SlotAssignment assignment)
        => new Vector3(0f, 0f, play.CurrentLineOfScrimmageZ) +
            play.AttackDirection.ToWorldVector(assignment.zoneOffsetFromLOS);

    // The preview reads the same distance cascade used at the snap without
    // installing roles or exposing any of the offense's play data.
    internal static Dictionary<DefenderAI, Transform> GetManTargets(PlayState ps, DefensivePlayData play)
    {
        var targets = new Dictionary<DefenderAI, Transform>();
        var available = new List<DefenderAI>();
        for (int i = 0; i < ps.DefensePlayers.Count; i++)
        {
            var actor = ps.DefensePlayers[i];
            if (play.GetAssignment(i).job == DefenderJob.Man && actor != null &&
                actor.gameObject.activeInHierarchy && actor.TryGetComponent<DefenderAI>(out var ai))
                available.Add(ai);
        }
        if (available.Count == 0) return targets;

        var receivers = ReceiverTargeting.GetEligibleReceivers(ps.OffensivePlayers);
        receivers.RemoveAll(receiver => !receiver.gameObject.activeInHierarchy);
        var qb = ps.Passer;

        // Most dangerous (closest to the ball) receiver picks first — same cascade idea BlockingCoordinator uses.
        if (qb != null)
            receivers.Sort((a, b) => Vector3.Distance(qb.position, a.position).CompareTo(Vector3.Distance(qb.position, b.position)));

        foreach (var receiver in receivers)
        {
            DefenderAI best = null;
            float bestDist = float.MaxValue;
            foreach (var ai in available)
            {
                float dist = Vector3.Distance(ai.transform.position, receiver.position);
                if (dist < bestDist) { bestDist = dist; best = ai; }
            }
            if (best == null) break;

            targets.Add(best, receiver);
            available.Remove(best);
        }
        return targets;
    }

    void Update()
    {
        var ps = PlayState.Instance;
        if (ps == null || !ps.IsLive) return;

        var carrier = Carrier;
        if (carrier == null) return; // loose or in flight — roles stand as they were at release
        if (!carrier.TryGetComponent<TeamMember>(out var carrierTeam)) return;

        var defenders = LiveDefenders(carrierTeam.teamId);
        if (defenders.Count == 0) return;

        // Breakaway / scramble: coverage stops mattering, everybody converges.
        if (ps.IsCarrierPastLineOfScrimmage(carrier.position.z))
        {
            foreach (var ai in defenders) ai.SetRole(DefenderAI.Role.Engage);
            return;
        }

        var play = ps.CurrentDefensivePlay;
        bool qbStillHasIt = carrierTeam.slot == TeamMember.RosterSlot.QB;
        if (play != null && qbStillHasIt) return; // called assignments stand

        // Run-fit (or no defensive play at all): closest Engages, the rest Contain.
        // Rush-job defenders are always Engage.
        var rushers = play != null ? RushersFor(ps, play) : null;

        DefenderAI closest = null;
        float closestDist = float.MaxValue;
        foreach (var ai in defenders)
        {
            if (rushers != null && rushers.Contains(ai)) continue;
            float dist = Vector3.Distance(ai.transform.position, carrier.position);
            if (dist < closestDist) { closestDist = dist; closest = ai; }
        }

        foreach (var ai in defenders)
        {
            bool engage = ai == closest || (rushers != null && rushers.Contains(ai));
            ai.SetRole(engage ? DefenderAI.Role.Engage : DefenderAI.Role.Contain);
        }
    }

    static HashSet<DefenderAI> RushersFor(PlayState ps, DefensivePlayData play)
    {
        var set = new HashSet<DefenderAI>();
        var defense = ps.DefensePlayers;
        for (int i = 0; i < defense.Count; i++)
        {
            if (defense[i] == null || play.GetAssignment(i).job != DefenderJob.Rush) continue;
            if (defense[i].TryGetComponent<DefenderAI>(out var ai)) set.Add(ai);
        }
        return set;
    }

    static List<DefenderAI> LiveDefenders(int carrierTeamId)
    {
        var defenders = new List<DefenderAI>();
        foreach (var member in FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (member.teamId == carrierTeamId) continue;
            if (member.TryGetComponent<DefenderAI>(out var ai) && !ai.IsUserControlled) defenders.Add(ai);
        }
        return defenders;
    }
}
