using System.Collections.Generic;
using UnityEngine;

// Mirrors DefenderCoordinator on the offensive side. Each frame, assigns every blocker to
// the nearest unassigned defender relative to the BALL (not the blocker itself) — per
// design, "all offensive parties engage with the defender nearest to the ball; if that
// defender is already engaged, move to the next target." Assignment cascades outward from
// the ball rather than each blocker independently picking its own closest defender, which
// is what actually produces "block the guy near the play" instead of every blocker just
// grabbing whoever happens to be standing next to THEM.
//
// Reassignment only happens when a blocker's current target breaks free (leaves blocking
// range or stops existing) — per design, contact-based commitment, not per-frame closest-
// wins. switchThreshold adds hysteresis on TOP of that for the reassignment moment itself,
// so a fresh assignment doesn't flicker between two nearly-equidistant defenders frame to
// frame while the blocker is still closing the gap.
public class BlockingCoordinator : MonoBehaviour
{
    public static BlockingCoordinator Instance { get; private set; }

    [SerializeField] float blockEngageRadius = 6f; // max distance from a defender for a blocker to be assignable to them at all
    [SerializeField] float switchThreshold = 1.5f; // new target must be this much closer than the old one to steal an assignment
    [SerializeField] string defenderTag = "Defender";

    List<AllyBlocker> blockers = new List<AllyBlocker>();

    // Defender -> blocker currently assigned to them. Rebuilt fresh every frame from
    // scratch based on each blocker's CURRENT target (kept or dropped), not recomputed
    // from zero — this is what makes "already engaged, move to next target" cascade
    // correctly instead of every blocker re-picking independently and potentially
    // colliding on the same defender.
    Dictionary<Transform, AllyBlocker> defenderAssignments = new Dictionary<Transform, AllyBlocker>();

    void Awake() => Instance = this;

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        // Ball not possessed, mid-pass/pitch, or hasn't crossed the LOS yet — no blocking
        // assignments exist. Design call: blocking only matters once the carrier has
        // crossed the line of scrimmage AND the ball is possessed; before that, WR/TE/RB
        // are still running routes via ReceiverAI and shouldn't be fighting AllyBlocker
        // for control of their own transform.
        bool ballHeldAndPossessed = BallController.Instance != null
            && BallController.Instance.Carrier != null
            && BallController.Instance.State == BallController.BallState.Held;

        bool pastLOS = ballHeldAndPossessed && PlayState.Instance != null
            && BallController.Instance.Carrier.position.z > PlayState.Instance.CurrentLineOfScrimmageZ;

        if (!ballHeldAndPossessed || !pastLOS)
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

        Vector3 ballPos = BallController.Instance.transform.position;
        GameObject[] defenderObjects = GameObject.FindGameObjectsWithTag(defenderTag);
        if (defenderObjects.Length == 0)
        {
            foreach (var blocker in blockers) blocker.SetBlockingTarget(null);
            return;
        }

        // Sort candidate defenders by distance to the BALL — this defines the priority
        // order blockers cascade through ("nearest to the ball first, then next nearest
        // if already taken").
        var defendersByBallDistance = new List<Transform>();
        foreach (var obj in defenderObjects) defendersByBallDistance.Add(obj.transform);
        defendersByBallDistance.Sort((a, b) =>
            Vector3.Distance(ballPos, a.position).CompareTo(Vector3.Distance(ballPos, b.position)));

        // Pass 1 — honor existing assignments where the target is still valid (in range,
        // still exists). This is the "only reassign if the target breaks free" rule.
        foreach (var blocker in blockers)
        {
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
                blocker.SetBlockingTarget(null); // target broke free — eligible for reassignment below
            }
        }

        // Pass 2 — any blocker without a valid target gets assigned to the nearest
        // ball-priority defender that isn't already claimed. switchThreshold only matters
        // here if we're comparing against a target the blocker is ALREADY holding from
        // pass 1 — since those are skipped above, this pass is a clean "find the best
        // available slot" rather than a fight over an existing assignment.
        foreach (var blocker in blockers)
        {
            if (blocker.GetCurrentTarget() != null) continue; // handled in pass 1

            Transform best = FindBestAvailableDefender(blocker, defendersByBallDistance);
            if (best != null)
            {
                blocker.SetBlockingTarget(best);
                defenderAssignments[best] = blocker;
            }
        }
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
            var blocker = t.GetComponent<AllyBlocker>();
            if (blocker != null) blockers.Add(blocker);
        }
    }
}