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
    [SerializeField] float maxReceiverSearchRadius = 45f;
    [SerializeField] float receiverSearchConeAngle = 70f;
    [SerializeField] string teammateTag = "Teammate";
    [SerializeField] string defenderTag = "Defender";

    // Arc now scales with distance instead of a flat value — short screens should read
    // as bullets, deep balls need real air under them. arcHeightPerDistance is the ratio
    // (roughly: for every unit of throw distance, add this much peak height). Passing
    // attribute flattens the arc at the top end (a gunslinger drives it in tighter) —
    // inverse of the speed relationship, since velocity and touch are the tradeoff here.
    [SerializeField] float baseArcHeight = 0.5f;         // floor — even the shortest pass has SOME loft
    [SerializeField] float arcHeightPerDistance = 0.12f; // tune this to taste

    // Velocity-based flight time instead of a flat duration. A fixed 0.6s regardless of
    // distance meant bombs implicitly traveled faster than short hitches (same time,
    // more ground covered) — backwards feel. baseThrowSpeed is units/sec at Passing
    // rating 10 (neutral, attr.Passing() == 1.0). Scale up/down from there.
    [SerializeField] float baseThrowSpeed = 35f;
    [SerializeField] float minFlightDuration = 0.15f; // floor so a 2-yard hitch doesn't arrive in one frame

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
            float distance = Vector3.Distance(ctx.transform.position, target.position);
            float throwSpeed = baseThrowSpeed * ctx.attributes.Passing();
            float duration = Mathf.Max(distance / throwSpeed, minFlightDuration);

            // Arc grows with distance (more air time needed to cover ground), but a
            // higher Passing rating flattens it back down — an elite arm can drive a
            // 30-yard throw tighter than an average one. Inverse curve vs. throw speed:
            // Passing 20 = flatter/faster, Passing 0 = looping and slow, both compounding
            // in the same direction (bad passer = lob city, good passer = frozen rope).
            float distanceArc = baseArcHeight + distance * arcHeightPerDistance;
            float arc = distanceArc / Mathf.Lerp(1.3f, 0.8f, InverseLerpPassing(ctx.attributes.Passing()));

            BallController.Instance.Throw(target.position, target, isPitch: false, arcHeight: arc, duration: duration);
        }
    }

    // Passing() typically ranges ~0.6–1.3 per AttributeCurves. Remap to 0-1 for the Lerp
    // above rather than hardcoding rating-based math here — keeps this decoupled from
    // whatever the curve asset's exact bounds are.
    float InverseLerpPassing(float passingMult) => Mathf.InverseLerp(0.6f, 1.3f, passingMult);

    Transform FindBestReceiver(PlayerContext ctx)
    {
        var receivers = ReceiverTargeting.GetEligibleReceivers(PlayState.Instance?.OffensivePlayers);
        Debug.Log($"[PassMove] Found {receivers.Count} eligible pass targets");

        if (ReceiverSelectionUI.Instance != null)
        {
            int selectedIdx = ReceiverSelectionUI.Instance.GetSelectedReceiverIndex();

            if (selectedIdx >= 0 && selectedIdx < receivers.Count)
            {
                var selected = receivers[selectedIdx];

                // Human picked this target explicitly — only the physical distance cap
                // applies. No cone/angle gate here: screens, swings, and check-downs
                // behind the LOS are legal, common throws once a player makes the call.
                // The cone only matters for the auto-select fallback below, where there's
                // no human judgment to defer to (or eventually, a CPU-controlled offense
                // making its own read).
                if (IsWithinRange(ctx, selected))
                {
                    Debug.Log($"[PassMove] Throwing to selected receiver {selectedIdx + 1}");
                    return selected;
                }

                Debug.LogWarning($"[PassMove] Selected receiver {selectedIdx + 1} out of range, falling back to auto-select");
            }
        }

        // Auto-select fallback (no explicit selection, or selection out of range) —
        // cone angle applies here since a heuristic is making the read, not a person.
        Transform best = null;
        float bestScore = float.MinValue;

        foreach (var r in receivers)
        {
            if (!IsValidAutoTarget(ctx, r)) continue;

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

    bool IsWithinRange(PlayerContext ctx, Transform receiver)
    {
        float dist = Vector3.Distance(receiver.position, ctx.transform.position);
        return dist <= maxReceiverSearchRadius;
    }

    bool IsValidAutoTarget(PlayerContext ctx, Transform receiver)
    {
        if (!IsWithinRange(ctx, receiver)) return false;

        Vector3 toReceiver = receiver.position - ctx.transform.position;
        float angle = Vector3.Angle(ctx.transform.forward, toReceiver);
        return angle <= receiverSearchConeAngle;
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
