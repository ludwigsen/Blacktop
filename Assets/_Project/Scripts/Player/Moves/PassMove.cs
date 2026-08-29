using UnityEngine;

// Forward pass. Short windup locks the player in place — same "commit to the animation"
// philosophy as Juke/Hurdle/StiffArm. No manual targeting reticle for v1: on trigger,
// auto-selects the "best" eligible teammate by a cheap openness score (distance from
// nearest defender), which is enough to validate the throw/catch/interception pipeline
// without building a real targeting UI first.
[System.Serializable]
public class PassMove : IPlayerMove
{
    [SerializeField] float windupDuration = 0.15f;
    [SerializeField] float maxReceiverSearchRadius = 20f;
    [SerializeField] float receiverSearchConeAngle = 70f; // degrees off forward — keeps this a FORWARD pass, distinct from PitchMove
    [SerializeField] string teammateTag = "Teammate";
    [SerializeField] string defenderTag = "Defender";
    [SerializeField] float arcHeight = 3f;
    [SerializeField] float flightDuration = 0.6f;

    float timer;
    Transform target;

    public bool CanTrigger(PlayerContext ctx, PlayerState currentState)
        => (currentState == PlayerState.Idle || currentState == PlayerState.Walk || currentState == PlayerState.Run)
           && BallController.Instance != null
           && BallController.Instance.Carrier == ctx.transform; // only the ball carrier can throw

    public void Enter(PlayerContext ctx)
    {
        timer = 0f;
        target = FindBestReceiver(ctx);
    }

    public void Tick(PlayerContext ctx, float deltaTime)
    {
        timer += deltaTime;
        // no position movement during windup — player just locks, same as the others
    }

    public bool IsComplete => timer >= windupDuration;

    public void Exit(PlayerContext ctx)
    {
        if (target != null && BallController.Instance != null)
        {
            BallController.Instance.Throw(target.position, target, isPitch: false, arcHeight: arcHeight, duration: flightDuration);
        }
        // No receiver found -> pass fizzles, ball stays with carrier. No dedicated
        // "throwaway"/spike behavior yet — revisit if that matters once playtested.
    }

    Transform FindBestReceiver(PlayerContext ctx)
    {
        var candidates = GameObject.FindGameObjectsWithTag(teammateTag);
        Transform best = null;
        float bestScore = float.MinValue;

        foreach (var c in candidates)
        {
            Vector3 toReceiver = c.transform.position - ctx.transform.position;
            float dist = toReceiver.magnitude;
            if (dist > maxReceiverSearchRadius) continue;

            float angle = Vector3.Angle(ctx.transform.forward, toReceiver);
            if (angle > receiverSearchConeAngle) continue;

            float openness = NearestDefenderDistance(c.transform.position);

            // Weight openness heavily over raw distance — a wide-open receiver 15 units
            // out should usually beat a covered one 3 units out. Pure placeholder ratio,
            // needs real playtesting once there's more than one teammate to choose from.
            float score = openness * 2f - dist * 0.1f;
            if (score > bestScore)
            {
                bestScore = score;
                best = c.transform;
            }
        }
        return best;
    }

    float NearestDefenderDistance(Vector3 pos)
    {
        var defenders = GameObject.FindGameObjectsWithTag(defenderTag);
        float closest = float.MaxValue;
        foreach (var d in defenders)
        {
            float dist = Vector3.Distance(pos, d.transform.position);
            if (dist < closest) closest = dist;
        }
        return closest == float.MaxValue ? 999f : closest;
    }
}