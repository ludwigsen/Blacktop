using UnityEngine;

// Chase / contain / cover / zone logic, transform-based. Role is assigned externally by
// DefenderCoordinator — DefenderAI never decides its own assignment.
//
//   Engage  — direct pursuit of the ball carrier (also = Rush job)
//   Contain — hold a lane near the LOS (also = Spy job)
//   Cover   — man: shadow one assigned receiver
//   Zone    — drop to an anchor; shadow any receiver who walks into it; break on nearby throws
//
// Movement branches on BallController.State, not just "is there a carrier": Held runs the
// role logic, InFlight lets a man defender on the target (or a zone defender near the
// landing spot) break on the ball, Loose still holds position (loose-ball pursuit is a
// separate, still-deferred system).
[RequireComponent(typeof(TeamMember))]
public class DefenderAI : MonoBehaviour
{
    public enum Role { Engage, Contain, Cover, Zone }

    [SerializeField] PlayerAttributes attributes;
    [SerializeField] float baseMoveSpeed = 5f;
    [SerializeField] float stopDistance = 1f;
    [SerializeField] float separationRadius = 1.2f;
    [SerializeField] float separationStrength = 3f;

    [SerializeField] float containBreakRadius = 4f;
    [SerializeField] float containLeadDistance = 3f;

    [Header("Coverage")]
    [Tooltip("Distance defender sits toward their own goal line from the receiver — reads as playing off rather than standing on top of them.")]
    [SerializeField] float coverCushion = 2.5f;
    [Tooltip("Man/zone shadowing closes faster than a normal chase — keeps a defender glued instead of trailing.")]
    [SerializeField] float coverCatchUpSpeedMult = 1.15f;
    [Tooltip("Speed boost once a defender breaks on a released throw.")]
    [SerializeField] float breakOnBallSpeedMult = 1.35f;
    [Tooltip("Seconds after release before anyone reacts to the throw — arcade reaction fudge so it never reads as psychic.")]
    [SerializeField] float breakReactionDelay = 0.15f;

    [Header("Zone")]
    [Tooltip("A receiver inside this radius of the zone anchor gets shadowed.")]
    [SerializeField] float zoneReactRadius = 4f;
    [Tooltip("A zone defender within this distance of a throw's landing spot breaks on it.")]
    [SerializeField] float zoneBreakRadius = 8f;

    public Role CurrentRole { get; private set; } = Role.Engage; // default Engage so a scene without a coordinator behaves sanely

    TeamMember teamMember;
    Transform coverTarget;
    Vector3 zoneAnchor;
    float ballInFlightTimer;

    float MoveSpeed => baseMoveSpeed * SpeedMult;
    public float SpeedMult => attributes != null ? attributes.Speed() : 1f;
    public float ResistMult => attributes != null ? attributes.Tackling() : 1f;
    public float CoverageMult => attributes != null ? attributes.Coverage() : 1f;

    public void SetAttributes(PlayerAttributes newAttributes) => attributes = newAttributes;

    Transform Target => BallController.Instance != null ? BallController.Instance.Carrier : null;

    Vector3 pushBackTarget;
    float pushBackTimer;
    const float pushBackDuration = 0.15f;
    float shedTimer;

    public bool IsUserControlled { get; private set; }
    public bool IsStunned => pushBackTimer > 0f || shedTimer > 0f;
    public void SetUserControlled(bool controlled) => IsUserControlled = controlled;

    void Awake() => teamMember = GetComponent<TeamMember>();

    public void SetRole(Role role) => CurrentRole = role;
    public void SetCoverTarget(Transform receiver) => coverTarget = receiver;
    public void ClearCoverTarget() => coverTarget = null;
    public void SetZoneAnchor(Vector3 anchor) => zoneAnchor = anchor;

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        if (pushBackTimer > 0f)
        {
            transform.position = Vector3.Lerp(transform.position, pushBackTarget, Time.deltaTime / pushBackTimer);
            pushBackTimer -= Time.deltaTime;
            return;
        }

        if (shedTimer > 0f)
        {
            shedTimer -= Time.deltaTime;
            return;
        }

        if (IsUserControlled) return;

        var ball = BallController.Instance;
        if (ball == null) return;

        // Both-ways roster: every player carries DefenderAI, but only the team that does NOT
        // have the ball may run it. Without this gate the offense's copy (default role Engage)
        // walked every teammate toward their own ball carrier while ReceiverAI/AllyBlocker
        // moved the same transform — the route-vs-block "conflict".
        if (!IsDefendingBall(ball)) return;

        switch (ball.State)
        {
            case BallController.BallState.Held:
                ballInFlightTimer = 0f;
                UpdateHeldState(ball.Carrier);
                break;
            case BallController.BallState.InFlight:
                ballInFlightTimer += Time.deltaTime;
                UpdateInFlightState(ball);
                break;
            case BallController.BallState.Loose:
                ballInFlightTimer = 0f;
                break; // hold position — loose-ball pursuit still deferred
        }
    }

    // Resolved live from the ball, never cached (project rule). Held: anyone not on the
    // carrier's team defends. InFlight: the offense is the intended receiver's team (falls
    // back to PossessionTeamId for a pop/fumble flight with no receiver). Loose: nobody acts
    // anyway, so return true and let the Loose case hold position.
    bool IsDefendingBall(BallController ball)
    {
        if (teamMember == null) return true;

        switch (ball.State)
        {
            case BallController.BallState.Held:
                return ball.Carrier == null
                    || !ball.Carrier.TryGetComponent<TeamMember>(out var holder)
                    || holder.teamId != teamMember.teamId;

            case BallController.BallState.InFlight:
                int offenseTeam = ball.IntendedReceiver != null && ball.IntendedReceiver.TryGetComponent<TeamMember>(out var receiver)
                    ? receiver.teamId
                    : (PlayState.Instance != null ? PlayState.Instance.PossessionTeamId : -1);
                return teamMember.teamId != offenseTeam;

            default:
                return true;
        }
    }

    void UpdateHeldState(Transform carrier)
    {
        if (carrier == null) return;

        Vector3 roleMove = CurrentRole switch
        {
            Role.Cover => CalculateCoverMove(),
            Role.Zone => CalculateZoneMove(carrier),
            Role.Contain => CalculateContainMove(carrier),
            _ => CalculateEngageMove(carrier)
        };

        transform.position += (roleMove + CalculateSeparation()) * Time.deltaTime;
    }

    // A man defender on the ACTUAL intended receiver breaks on the ball, and so does a zone
    // defender near the landing spot. Everyone else still gets a shot via BallController's
    // OverlapSphere check on arrival, just without the head start — which is what keeps this
    // from reading as the whole defense psychically converging on a live throw.
    void UpdateInFlightState(BallController ball)
    {
        Vector3 landing = ball.FlightTarget;

        bool manBreak = coverTarget != null && ball.IntendedReceiver == coverTarget;
        bool zoneBreak = CurrentRole == Role.Zone && Vector3.Distance(transform.position, landing) <= zoneBreakRadius;
        if (!(manBreak || zoneBreak) || ballInFlightTimer < breakReactionDelay) return;

        if (Vector3.Distance(transform.position, landing) <= stopDistance) return;

        Vector3 direction = (landing - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(direction);
        transform.position += direction * (MoveSpeed * breakOnBallSpeedMult) * Time.deltaTime;
    }

    Vector3 CalculateCoverMove() => coverTarget == null ? Vector3.zero : ShadowMove(coverTarget);

    Vector3 CalculateZoneMove(Transform carrier)
    {
        Transform threat = FindReceiverInZone();
        if (threat != null) return ShadowMove(threat);

        // Nobody in the zone: drop to the anchor, facing the ball (reads as a backpedal).
        Vector3 toCarrier = carrier.position - transform.position;
        toCarrier.y = 0f;
        if (toCarrier.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toCarrier.normalized);

        float distance = Vector3.Distance(transform.position, zoneAnchor);
        if (distance <= stopDistance) return Vector3.zero;
        return (zoneAnchor - transform.position).normalized * MoveSpeed;
    }

    // Nearest eligible opposing receiver inside the zone. Anchor-relative, so the defender
    // is naturally leashed to his zone instead of chasing a receiver across the field.
    Transform FindReceiverInZone()
    {
        Transform best = null;
        float bestDist = zoneReactRadius;

        foreach (var member in FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!TeamMember.AreOpponents(this, member)) continue;
            if (!ReceiverTargeting.IsEligible(member.transform)) continue;

            float dist = Vector3.Distance(zoneAnchor, member.transform.position);
            if (dist < bestDist) { bestDist = dist; best = member.transform; }
        }

        return best;
    }

    // Shared by man and zone: sit a cushion toward our own goal from the target, facing them.
    Vector3 ShadowMove(Transform target)
    {
        FieldDirection targetAttack = PlayState.Instance != null && target.TryGetComponent<TeamMember>(out var targetTeam)
            ? PlayState.Instance.DirectionFor(targetTeam.teamId)
            : FieldDirection.TowardPositiveZ;

        Vector3 shadowPoint = target.position - targetAttack.Forward * coverCushion;
        transform.rotation = Quaternion.LookRotation((target.position - transform.position).normalized);

        if (Vector3.Distance(transform.position, shadowPoint) <= stopDistance) return Vector3.zero;
        return (shadowPoint - transform.position).normalized * (MoveSpeed * coverCatchUpSpeedMult);
    }

    Vector3 CalculateEngageMove(Transform target)
    {
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance <= stopDistance) return Vector3.zero;

        Vector3 direction = (target.position - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(direction);
        return direction * MoveSpeed;
    }

    Vector3 CalculateContainMove(Transform target)
    {
        float distanceToCarrier = Vector3.Distance(transform.position, target.position);
        if (distanceToCarrier <= containBreakRadius) return CalculateEngageMove(target);

        float losZ = PlayState.Instance != null ? PlayState.Instance.CurrentLineOfScrimmageZ : target.position.z;
        float holdZ = PlayState.Instance != null
            ? PlayState.Instance.ViewDirection.Advance(losZ, containLeadDistance)
            : losZ + containLeadDistance;
        Vector3 holdPosition = new(transform.position.x, transform.position.y, holdZ);

        if (Vector3.Distance(transform.position, holdPosition) <= stopDistance) return Vector3.zero;

        Vector3 direction = (holdPosition - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation((target.position - transform.position).normalized);
        return direction * MoveSpeed;
    }

    Vector3 CalculateSeparation()
    {
        Vector3 push = Vector3.zero;
        var allMembers = FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var other in allMembers)
        {
            if (other.transform == transform) continue;
            if (other.teamId != teamMember.teamId) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist < separationRadius && dist > 0.001f)
            {
                Vector3 away = (transform.position - other.transform.position).normalized;
                push += away * (separationRadius - dist);
            }
        }

        return push * separationStrength;
    }

    public void ApplyPushBack(Vector3 direction, float distance)
    {
        pushBackTarget = transform.position + direction * distance;
        pushBackTimer = pushBackDuration;
 
    }

    public void ApplyShed(float duration) => shedTimer = duration;
}