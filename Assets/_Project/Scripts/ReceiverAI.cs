using UnityEngine;

// Runs a receiver's assigned route on a PASS play. Route ends when (a) the receiver reaches
// the end of it, or (b) a teammate ball carrier is past the line of scrimmage (QB scramble or
// a catch-and-run) — at which point AllyBlocker/BlockingCoordinator take over and the receiver
// blocks. On a RUN play PlayState assigns RoutePattern.None, which counts as "route complete"
// immediately, so run-play skill players block from the snap.
//
// Hand-off between the two components is RouteComplete: ReceiverAI only moves the transform
// while it is false (or while a ball is inbound to this receiver); AllyBlocker only moves it
// once it is true. Never both.
public class ReceiverAI : MonoBehaviour
{
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float routeDepth = 12f;
    [SerializeField] float slantDistance = 4f; // lateral distance for Slant
    [SerializeField] float hitchDepth = 4f;   // distance upfield for Hitch before settling

    // Route Running's ball-tracking half: how far off the CHARTED route (targetPosition,
    // not wherever the receiver physically happens to be) this receiver can drift to
    // adjust for a misthrown pass, scaled by their RouteRunning multiplier. A great
    // route runner bails out a mediocre throw; a bad one just watches it sail by.
    // Deliberately capped relative to the route's planned endpoint, not to current
    // position — this is "how far can I deviate from my assignment," not "how far can
    // I run in general" (that's moveSpeed's job, and it still gates how much of this
    // radius is actually reachable before the ball lands — a long-developing bomb
    // gives more real time to recover from a bad lead than a quick slant does).
    [SerializeField] float baseBallAdjustRadius = 2.5f;
    PlayerAttributes attributes;

    // CharacterApplier owns construction of the runtime PlayerAttributes instance.
    // ReceiverAI consumes that same instance rather than keeping a stale prefab reference.
    public void SetAttributes(PlayerAttributes newAttributes) => attributes = newAttributes;

    TeamMember teamMember;
    Vector3 snapPosition;
    Vector3 targetPosition;
    RoutePattern assignedRoute = RoutePattern.None;

    // Defaults to true: a receiver nobody has assigned a route to is idle, not running to
    // Vector3.zero. PlayState.AssignRoutes -> SetRoute is the only thing that starts a route.
    bool routeComplete = true;

    public bool RouteComplete => routeComplete;

    void Awake() => teamMember = GetComponent<TeamMember>();

    // Called by PlayState once everyone is standing at the line, every snap. This is the ONLY
    // place a route starts. (The old OnEnable/OnPlayReset resets are gone: PossessionController
    // toggles this component's enabled flag whenever the ball changes hands, and each toggle
    // used to wipe the route mid-play. Fields now simply persist across a disable/enable.)
    public void SetRoute(RoutePattern route)
    {
        snapPosition = transform.position;
        assignedRoute = route;
        CalculateTargetPosition();
    }

    void CalculateTargetPosition()
    {
        // Default to forward streak
        targetPosition = snapPosition + transform.forward * routeDepth;

        switch (assignedRoute)
        {
            case RoutePattern.Go:
                targetPosition = snapPosition + transform.forward * routeDepth;
                break;
            case RoutePattern.Slant:
                targetPosition = snapPosition + (transform.forward * routeDepth * 0.7f) + (transform.right * slantDistance);
                break;
            case RoutePattern.Hitch:
                targetPosition = snapPosition + transform.forward * hitchDepth;
                break;
            case RoutePattern.Wheel:
                // Curved path — for now, just go out to sideline then forward
                targetPosition = snapPosition + (transform.right * 5f) + (transform.forward * routeDepth * 0.5f);
                break;
            case RoutePattern.Comeback:
                // Go deep then come back toward snap spot
                targetPosition = snapPosition + transform.forward * routeDepth + (-transform.forward * routeDepth * 0.4f);
                break;
            case RoutePattern.None:
                targetPosition = snapPosition; // stay at snap point
                break;
        }

        // None = "no route, go block." Done from frame one so AllyBlocker's RouteComplete
        // gate opens at the snap instead of after a phantom route.
        routeComplete = assignedRoute == RoutePattern.None;
    }

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        // Pass-play rule: the route is over the moment a teammate carrying the ball is past
        // the LOS. From here AllyBlocker owns this transform (BlockingCoordinator makes skill
        // players eligible on the same condition).
        if (!routeComplete && TeammateCarrierPastLOS())
            routeComplete = true;

        bool ballInbound = IsBallInboundForMe(out Vector3 ballTarget);
        Vector3 moveTarget = targetPosition;

        if (ballInbound)
        {
            float maxAdjust = baseBallAdjustRadius * (attributes != null ? attributes.RouteRunning() : 1f);
            moveTarget = targetPosition + Vector3.ClampMagnitude(ballTarget - targetPosition, maxAdjust);
        }
        else if (routeComplete)
        {
            return; // route's done and there's no ball to chase — nothing to do
        }

        transform.position = Vector3.MoveTowards(transform.position, moveTarget, moveSpeed * Time.deltaTime);

        // Only the ORIGINAL route can "complete" — while a ball's inbound we keep
        // evaluating every frame (the ball might still be adjusting, or we might not
        // reach it in time), so completion is only meaningful for normal route running.
        if (!ballInbound && Vector3.Distance(transform.position, moveTarget) < 0.5f)
            routeComplete = true;
    }

    // True when somebody on MY team is holding the ball past the line of scrimmage.
    // Live reads only (project rule): carrier from BallController, direction from the carrier.
    bool TeammateCarrierPastLOS()
    {
        var ball = BallController.Instance;
        var ps = PlayState.Instance;
        if (ball == null || ps == null || teamMember == null) return false;
        if (!ball.IsHeld || ball.Carrier == null || ball.Carrier == transform) return false;
        if (!ball.Carrier.TryGetComponent<TeamMember>(out var holder) || holder.teamId != teamMember.teamId) return false;

        return ps.IsCarrierPastLineOfScrimmage(ball.Carrier.position.z);
    }

    // Predicts where this receiver will be after `time` more seconds of route movement,
    // using the exact same Vector3.MoveTowards call Update() actually runs — so a
    // prediction can never run a receiver past where their own route stops (a Hitch
    // clamps here the same way it clamps in real movement). Returns the current position
    // untouched once the route's already finished — nothing left to lead into.
    public Vector3 PredictedPosition(float time)
    {
        if (routeComplete) return transform.position;
        return Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * time);
    }

    bool IsBallInboundForMe(out Vector3 target)
    {
        target = default;
        var ball = BallController.Instance;
        if (ball == null || ball.State != BallController.BallState.InFlight) return false;
        if (ball.IntendedReceiver != transform) return false;

        target = ball.FlightTarget;
        return true;
    }

    // Called by AllyBlocker when the ball goes loose mid-route. Without this, ReceiverAI's
    // own Update() keeps MoveTowards-ing the receiver toward their route target in the same
    // frame AllyBlocker tries to move them toward the ball — two components fighting over
    // one transform. Marking the route complete hands full control to AllyBlocker cleanly.
    public void AbortRoute() => routeComplete = true;
}
