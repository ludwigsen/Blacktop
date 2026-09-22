using UnityEngine;

// Lateral/backward pitch — the Street "keep the play alive" mechanic. Deliberately cheap:
// reuses BallController.Throw() with a near-zero arc and a very short flight, no manual
// targeting, no interception risk (Street convention: pitches aren't picked off). Exists
// mainly to validate the carrier-handoff pipeline before it's wired into any style/
// Gamebreaker scoring later.
[System.Serializable]
public class PitchMove : IPlayerMove
{
    [SerializeField] float windupDuration = 0.05f; // near-instant — a pitch should read as a reflex, not a decision
    [SerializeField] float pitchRadius = 6f;
    [SerializeField] float arcHeight = 0.5f; // low flat toss, not a lob
    [SerializeField] float flightDuration = 0.15f;

    float timer;
    Transform target;

    public bool CanTrigger(PlayerContext ctx, PlayerState currentState)
        => (currentState == PlayerState.Idle || currentState == PlayerState.Walk || currentState == PlayerState.Run)
           && BallController.Instance != null
           && BallController.Instance.Carrier == ctx.transform;

    public void Enter(PlayerContext ctx)
    {
        timer = 0f;
        target = FindNearestTeammateBehind(ctx);
    }

    public void Tick(PlayerContext ctx, float deltaTime) => timer += deltaTime;

    public bool IsComplete => timer >= windupDuration;

    public void Exit(PlayerContext ctx)
    {
        if (target != null && BallController.Instance != null)
        {
            BallController.Instance.Throw(target.position, target, isPitch: true, arcHeight: arcHeight, duration: flightDuration);
        }
        // Whiffed pitch (nobody in range) intentionally does nothing — no drop/fumble
        // penalty for an errant pitch yet. Revisit once this gets punished properly.
    }

    // Teammates are resolved through TeamMember (same team as the carrier), not tags —
    // the tag scheme was retired when teams were separated, and the scene now has T2
    // players carrying a "Teammate" tag, so a tag search could hand the ball to the
    // other team. "Backward" is measured against the direction the possession team is
    // attacking rather than the carrier's facing, so a pitch stays legal/illegal
    // regardless of which way the carrier is turned mid-juke.
    Transform FindNearestTeammateBehind(PlayerContext ctx)
    {
        if (!ctx.transform.TryGetComponent<TeamMember>(out var self)) return null;

        // self's own team direction — self IS the carrier here (CanTrigger already guarantees
        // it), so this is correct even the instant after an interception, before the whistle
        // updates PossessionTeamId/AttackDirection.
        Vector3 attackForward = PlayState.Instance != null
            ? PlayState.Instance.DirectionFor(self.teamId).Forward
            : ctx.transform.forward;

        Transform best = null;
        float bestDist = pitchRadius;

        foreach (var member in Object.FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (member.teamId != self.teamId || member.transform == ctx.transform) continue;

            Vector3 toTeammate = member.transform.position - ctx.transform.position;
            float dist = toTeammate.magnitude;
            if (dist > bestDist) continue;

            // Real (and arcade) football rule: pitches go backward/lateral, never
            // forward. This is what makes Pitch distinct from Pass rather than a
            // shorter-range duplicate of it.
            if (Vector3.Dot(attackForward, toTeammate.normalized) > 0.2f) continue;

            bestDist = dist;
            best = member.transform;
        }

        if (best == null)
            Debug.Log($"[PitchMove] No legal target: nobody on the carrier's team within {pitchRadius} units and behind/level with the carrier.");

        return best;
    }
}