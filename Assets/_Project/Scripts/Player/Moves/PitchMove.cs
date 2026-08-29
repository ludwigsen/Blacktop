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
    [SerializeField] string teammateTag = "Teammate";
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

    Transform FindNearestTeammateBehind(PlayerContext ctx)
    {
        var candidates = GameObject.FindGameObjectsWithTag(teammateTag);
        Transform best = null;
        float bestDist = pitchRadius;

        foreach (var c in candidates)
        {
            Vector3 toTeammate = c.transform.position - ctx.transform.position;
            float dist = toTeammate.magnitude;
            if (dist > bestDist) continue;

            // Real (and arcade) football rule: pitches go backward/lateral, never
            // forward. This is what makes Pitch distinct from Pass rather than a
            // shorter-range duplicate of it.
            float forwardDot = Vector3.Dot(ctx.transform.forward, toTeammate.normalized);
            if (forwardDot > 0.2f) continue;

            bestDist = dist;
            best = c.transform;
        }
        return best;
    }
}