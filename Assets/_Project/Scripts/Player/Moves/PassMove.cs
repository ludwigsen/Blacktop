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
    {
        bool isCorrectState = currentState == PlayerState.Idle || currentState == PlayerState.Walk || currentState == PlayerState.Run;
        bool hasBC = BallController.Instance != null;
        bool isCarrier = hasBC && BallController.Instance.Carrier == ctx.transform;

        Debug.Log($"[PassMove.CanTrigger] State: {currentState} (ok: {isCorrectState}), BC: {hasBC}, IsCarrier: {isCarrier}");

        return isCorrectState && hasBC && isCarrier;
    }

    public void Enter(PlayerContext ctx)
    {
        Debug.Log("[PassMove] Enter() called");
        timer = 0f;
        target = FindBestReceiver(ctx);
        Debug.Log($"[PassMove] Target found: {(target != null ? target.name : "NULL")}");
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
        // UI button indices must map to exactly the same transforms as this pass.
        // Do not infer eligibility from ReceiverAI: the Ally prefab carries that
        // component, while only WR1-3 and RB are legal pass targets.
        var receivers = ReceiverTargeting.GetEligibleReceivers(PlayState.Instance?.OffensivePlayers);
        Debug.Log($"[PassMove] Found {receivers.Count} eligible pass targets");

        // Check if there's an explicit selection
        if (ReceiverSelectionUI.Instance != null)
        {
            int selectedIdx = ReceiverSelectionUI.Instance.GetSelectedReceiverIndex();
            Debug.Log($"[PassMove] Selected receiver index: {selectedIdx}");

            if (selectedIdx >= 0 && selectedIdx < receivers.Count)
            {
                var selected = receivers[selectedIdx];
                if (IsValidTarget(ctx, selected))
                {
                    Debug.Log($"[PassMove] Throwing to selected receiver {selectedIdx + 1}");
                    return selected;
                }
            }
        }

        // Fall back to auto-select from filtered receivers
        Transform best = null;
        float bestScore = float.MinValue;

        foreach (var r in receivers)
        {
            if (!IsValidTarget(ctx, r)) continue;

            float openness = NearestDefenderDistance(r.position);
            float dist = Vector3.Distance(r.position, ctx.transform.position);
            float score = openness * 2f - dist * 0.1f;

            if (score > bestScore)
            {
                bestScore = score;
                best = r;
            }
        }

        Debug.Log($"[PassMove] Auto-selected: {(best != null ? best.name : "NULL")}");
        return best;
    }

    bool IsValidTarget(PlayerContext ctx, Transform receiver)
    {
        Vector3 toReceiver = receiver.position - ctx.transform.position;
        float dist = toReceiver.magnitude;
        if (dist > maxReceiverSearchRadius) return false;

        float angle = Vector3.Angle(ctx.transform.forward, toReceiver);
        if (angle > receiverSearchConeAngle) return false;

        return true;
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
