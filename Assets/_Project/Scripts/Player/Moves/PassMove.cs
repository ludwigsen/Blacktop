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

    // Accuracy scatter — the throw lands somewhere around the intended lead point, not
    // exactly on it. Radius shrinks as Passing rating rises. At NEUTRAL Passing worst-
    // case scatter is baseMaxLeadError; at max Passing it's minLeadError, never zero —
    // nobody's a laser in this game. Flat radius regardless of throw distance for now;
    // a real QB misses bombs by more than screens, so scaling this by distance is a
    // reasonable follow-up if flat scatter feels wrong on deep balls specifically.
    [SerializeField] float baseMaxLeadError = 3f;
    [SerializeField] float minLeadError = 0.2f;

    float timer;
    Transform target;

    public bool CanTrigger(PlayerContext ctx, PlayerState currentState)
    {
        bool isCorrectState = currentState == PlayerState.Idle || currentState == PlayerState.Walk || currentState == PlayerState.Run;
        bool hasBC = BallController.Instance != null;
        bool isCarrier = hasBC && BallController.Instance.Carrier == ctx.transform;

        // Only the QB can throw — on both run and pass plays. A run play can hand the
        // ball to the RB; that doesn't make the RB a passer.
        bool isQB = ctx.transform.TryGetComponent<TeamMember>(out var member) && member.slot == TeamMember.RosterSlot.QB;

        // Real forward-pass rule: once the passer has crossed the LOS at ANY point this
        // play, forward passing is dead for the rest of the play — a scramble back behind
        // it does NOT re-legalize the throw. This reads a one-way latch on PlayState
        // (HasPasserCrossedLOS), not the passer's live position, on purpose.
        bool eligibleToPass = PlayState.Instance == null || !PlayState.Instance.HasPasserCrossedLOS;

        Debug.Log($"[PassMove.CanTrigger] State: {currentState} (ok: {isCorrectState}), BC: {hasBC}, IsCarrier: {isCarrier}, IsQB: {isQB}, EligibleToPass: {eligibleToPass}");

        return isCorrectState && hasBC && isCarrier && isQB && eligibleToPass;
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
        if (target == null || BallController.Instance == null) return;

        // Lead the throw — see ReceiverAI.PredictedPosition: estimate flight time off
        // current distance, predict where the receiver will actually be by then, aim
        // there instead of at their position-at-release (old behavior always threw
        // "behind" a moving receiver).
        float initialDistance = Vector3.Distance(ctx.transform.position, target.position);
        float estimateThrowSpeed = baseThrowSpeed * ctx.attributes.Passing();
        float estimatedDuration = Mathf.Max(initialDistance / estimateThrowSpeed, minFlightDuration);

        Vector3 leadPoint = target.TryGetComponent<ReceiverAI>(out var receiverAI)
            ? receiverAI.PredictedPosition(estimatedDuration)
            : target.position; // no ReceiverAI on an eligible target shouldn't happen — fall back to old behavior

        // Passing-attribute accuracy: leadPoint above is the QB's INTENT. The actual
        // release scatters around it, radius shrinking as Passing rises. This is the
        // half of the equation that makes a bad QB actually feel bad — the receiver-side
        // half (ReceiverAI adjusting toward the ball, scaled by Route Running) is what
        // decides whether a miss here is still catchable or a clean incompletion.
        float skill = InverseLerpPassing(ctx.attributes.Passing());
        float errorRadius = Mathf.Lerp(baseMaxLeadError, minLeadError, skill);
        Vector2 scatter = Random.insideUnitCircle * errorRadius;
        Vector3 actualTarget = leadPoint + new Vector3(scatter.x, 0f, scatter.y);

        float distance = Vector3.Distance(ctx.transform.position, actualTarget);
        float throwSpeed = baseThrowSpeed * ctx.attributes.Passing();
        float duration = Mathf.Max(distance / throwSpeed, minFlightDuration);

        // Arc grows with distance (more air time needed to cover ground), but a
        // higher Passing rating flattens it back down — an elite arm can drive a
        // 30-yard throw tighter than an average one.
        float distanceArc = baseArcHeight + distance * arcHeightPerDistance;
        float arc = distanceArc / Mathf.Lerp(1.3f, 0.8f, skill);

        BallController.Instance.Throw(actualTarget, target, isPitch: false, arcHeight: arc, duration: duration);
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

        // Auto-select fallback — shared with CPUCarrierAI's QB brain via PassTargeting, so
        // tuning "openness" once improves both a human's auto-throw and every CPU pass.
        var best = PassTargeting.FindBestReceiver(ctx.transform, PlayState.Instance?.OffensivePlayers, maxReceiverSearchRadius, receiverSearchConeAngle);
        Debug.Log($"[PassMove] Auto-selected: {(best != null ? best.name : "NULL")}");
        return best;
    }

    bool IsWithinRange(PlayerContext ctx, Transform receiver)
    {
        float dist = Vector3.Distance(receiver.position, ctx.transform.position);
        return dist <= maxReceiverSearchRadius;
    }


}