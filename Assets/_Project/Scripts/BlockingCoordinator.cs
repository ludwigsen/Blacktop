using System.Collections.Generic;
using UnityEngine;

// Mirrors DefenderCoordinator on the offensive side. Each frame, assigns every ELIGIBLE
// blocker to the nearest unassigned member of the OPPOSING team relative to the BALL —
// cascading outward from the ball rather than each blocker independently picking its own
// closest opponent, which is what produces "block the guy near the play" instead of
// every blocker grabbing whoever's standing next to THEM.
//
// Eligibility is play-type-aware (see IsEligibleBlocker):
//   - OL always blocks, every play, from the snap. No LOS gate, no play-type gate.
//   - Skill positions (WR/RB) only block on designed RUN plays, and never the player
//     who's about to receive the handoff — that player autopaths to the QB instead
//     (see HandoffCoordinator), it doesn't block.
// On a pass play, skill positions run their assigned routes via ReceiverAI instead;
// AllyBlocker's own RouteComplete gate keeps them out of blocking until their route
// (if any) finishes.
public class BlockingCoordinator : MonoBehaviour
{
    public static BlockingCoordinator Instance { get; private set; }

    [SerializeField] float blockEngageRadius = 6f; // max distance from an opponent for a blocker to be assignable to them at all
    [SerializeField] float switchThreshold = 1.5f; // pre-existing field — currently unused by the assignment logic below, left as-is, not this pass's problem to fix

    List<AllyBlocker> blockers = new List<AllyBlocker>();

    // Opponent -> blocker currently assigned to them. Rebuilt fresh every frame from
    // scratch based on each blocker's CURRENT target (kept or dropped), not recomputed
    // from zero — this is what makes "already engaged, move to next target" cascade
    // correctly instead of every blocker re-picking independently.
    Dictionary<Transform, AllyBlocker> defenderAssignments = new();

    void Awake() => Instance = this;

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        bool ballHeld = BallController.Instance != null
            && BallController.Instance.Carrier != null
            && BallController.Instance.State == BallController.BallState.Held;

        if (!ballHeld)
        {
            if (defenderAssignments.Count > 0 || AnyBlockerHasTarget())
            {
                foreach (var blocker in blockers)
                    blocker.SetBlockingTarget(null);
                defenderAssignments.Clear();
            }
            return;
        }

        UpdateAssignments();
    }

    bool AnyBlockerHasTarget()
    {
        foreach (var b in blockers)
            if (b.GetCurrentTarget() != null) return true;
        return false;
    }

    void UpdateAssignments()
    {
        defenderAssignments.Clear();

        Transform carrier = BallController.Instance.Carrier;
        if (!carrier.TryGetComponent<TeamMember>(out var carrierTeam))
        {
            foreach (var blocker in blockers) blocker.SetBlockingTarget(null);
            return;
        }

        var playCall = PlayState.Instance != null ? PlayState.Instance.CurrentPlayCall : null;
        Vector3 ballPos = BallController.Instance.transform.position;

        // Pass-play rule: skill players block once a ball carrier is past the LOS (QB scramble
        // or a catch-and-run). Resolved live from the actual carrier, same as ViewDirection.
        bool carrierPastLOS = PlayState.Instance != null
            && PlayState.Instance.IsCarrierPastLineOfScrimmage(carrier.position.z);

        var defendersByBallDistance = new List<Transform>();
        foreach (var member in FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (member.teamId != carrierTeam.teamId)
                defendersByBallDistance.Add(member.transform);
        }

        if (defendersByBallDistance.Count == 0)
        {
            foreach (var blocker in blockers) blocker.SetBlockingTarget(null);
            return;
        }

        defendersByBallDistance.Sort((a, b) =>
            Vector3.Distance(ballPos, a.position).CompareTo(Vector3.Distance(ballPos, b.position)));

        // Pass 1 — honor existing assignments where the target is still valid AND the
        // blocker is still eligible (e.g. a run play's WR1 was blocking, the play reset
        // into a pass play — they lose eligibility and drop their target right here).
        foreach (var blocker in blockers)
        {
            if (blocker.IsCarrier) continue; // the carrier is never a blocker, full stop
            // After a turnover the old offense is the carrier's OPPONENT — it must not "block"
            // its own teammates. Only players on the carrier's team can be blockers.
            if (!IsOnTeam(blocker, carrierTeam.teamId) || !IsEligibleBlocker(blocker, playCall, carrierPastLOS))
            {
                blocker.SetBlockingTarget(null);
                continue;
            }

            Transform current = blocker.GetCurrentTarget();
            if (current == null) continue;

            bool stillExists = defendersByBallDistance.Contains(current);
            bool stillInRange = stillExists && Vector3.Distance(blocker.transform.position, current.position) <= blockEngageRadius;

            if (stillExists && stillInRange)
            {
                defenderAssignments[current] = blocker;
            }
            else
            {
                blocker.SetBlockingTarget(null);
            }
        }

        // Pass 2 — any eligible (non-carrier) blocker without a valid target gets
        // assigned to the nearest ball-priority opposing player that isn't already claimed.
        foreach (var blocker in blockers)
        {
            if (blocker.IsCarrier) continue;
            if (!IsOnTeam(blocker, carrierTeam.teamId) || !IsEligibleBlocker(blocker, playCall, carrierPastLOS)) continue; // already cleared in Pass 1
            if (blocker.GetCurrentTarget() != null) continue;

            Transform best = FindBestAvailableDefender(blocker, defendersByBallDistance);
            if (best != null)
            {
                blocker.SetBlockingTarget(best);
                defenderAssignments[best] = blocker;
            }
        }
    }

    static bool IsOnTeam(AllyBlocker blocker, int teamId)
        => blocker.TryGetComponent<TeamMember>(out var m) && m.teamId == teamId;

    // OL blocks on every play, unconditionally, from the snap. Skill positions (WR/RB):
    //   Run  — block from the snap, except the handoff receiver (he's taking the ball).
    //   Pass — run the route (ReceiverAI) and only block once a ball carrier is past the LOS.
    //          ReceiverAI ends the route on that same condition, and AllyBlocker waits on
    //          RouteComplete, so the two never drive the transform at once.
    //   Trick — falls under the Pass rules until trick plays get their own pass.
    static bool IsEligibleBlocker(AllyBlocker blocker, PlayCallData playCall, bool carrierPastLOS)
    {
        if (!blocker.TryGetComponent<TeamMember>(out var member)) return false;

        if (member.slot == TeamMember.RosterSlot.OL1 || member.slot == TeamMember.RosterSlot.OL2)
            return true;

        bool isRunPlay = playCall != null && playCall.PlayType == PlayType.Run;
        if (isRunPlay) return playCall.HandoffReceiverSlot != member.slot;

        return carrierPastLOS;
    }

    Transform FindBestAvailableDefender(AllyBlocker blocker, List<Transform> defendersByBallDistance)
    {
        Vector3 blockerPos = blocker.transform.position;

        foreach (var defender in defendersByBallDistance)
        {
            if (defenderAssignments.ContainsKey(defender)) continue; // already engaged — cascade to next
            if (Vector3.Distance(blockerPos, defender.position) > blockEngageRadius) continue;

            return defender; // list is already ball-distance-sorted, so first available wins
        }

        return null;
    }

    // Called by PlayState.ResetPlay() after offensive players are repositioned each snap.
    public void RegisterBlockers(List<Transform> offensivePlayers)
    {
        blockers.Clear();
        defenderAssignments.Clear();

        foreach (var t in offensivePlayers)
        {
            if (t == null) continue;
            if (t.TryGetComponent<AllyBlocker>(out var blocker)) blockers.Add(blocker);
        }
    }
}