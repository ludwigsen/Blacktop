using UnityEngine;

// Chase-and-contain-and-cover logic, transform-based. Behavior branches on a Role set
// externally by DefenderCoordinator — Engage/Contain/Cover — DefenderAI never decides its
// own assignment, same standing rule as before Cover existed.
//
// Movement now branches on BallController.State, not just "is there a carrier":
//   - Held:     original Engage/Contain, plus new Cover (shadow an assigned receiver).
//   - InFlight: the ONE thing that was completely missing. A defender assigned to Cover
//     the actual intended receiver breaks toward BallController.FlightTarget instead of
//     freezing — this is what makes an interception a real, winnable footrace instead of
//     a static dice roll nobody could ever be in position to benefit from.
//   - Loose:    unchanged — hold position, pursuit-of-loose-ball is still a deferred system.
[RequireComponent(typeof(TeamMember))]
public class DefenderAI : MonoBehaviour
{
    public enum Role { Engage, Contain, Cover }

    [SerializeField] DefenderAttributes attributes;
    [SerializeField] float baseMoveSpeed = 5f;
    [SerializeField] float stopDistance = 1f;
    [SerializeField] float separationRadius = 1.2f;
    [SerializeField] float separationStrength = 3f;

    [SerializeField] float containBreakRadius = 4f;
    [SerializeField] float containLeadDistance = 3f;

    [Header("Coverage (Cover role — man coverage on an assigned receiver)")]
    [Tooltip("Distance defender sits toward their own goal line from the receiver — reads as playing off-man rather than standing on top of them.")]
    [SerializeField] float coverCushion = 2.5f;
    [Tooltip("Cover role closes on its shadow point faster than a normal chase — this is what keeps a defender glued to a receiver instead of trailing.")]
    [SerializeField] float coverCatchUpSpeedMult = 1.15f;
    [Tooltip("Speed boost once a Cover defender breaks on a released throw — 'jumping the route' should look faster than normal pursuit.")]
    [SerializeField] float breakOnBallSpeedMult = 1.35f;
    [Tooltip("Seconds after release before a Cover defender reacts to the throw — arcade reaction-time fudge so this never reads as psychic.")]
    [SerializeField] float breakReactionDelay = 0.15f;

    public Role CurrentRole { get; private set; } = Role.Engage; // default Engage so a scene without a coordinator behaves sanely

    TeamMember teamMember;
    Transform coverTarget;
    float ballInFlightTimer;

    float MoveSpeed => baseMoveSpeed * SpeedMult;
    public float SpeedMult => attributes != null ? attributes.speedMult : 1f;
    public float ResistMult => attributes != null ? attributes.resistMult : 1f;
    public float CoverageMult => attributes != null ? attributes.coverageMult : 1f;

    Vector3 pushBackTarget;
    float pushBackTimer;
    const float pushBackDuration = 0.15f;
    float shedTimer;

    public bool IsUserControlled { get; private set; }
    public bool IsStunned => pushBackTimer > 0f || shedTimer > 0f;
    public void SetUserControlled(bool controlled) => IsUserControlled = controlled;

    void Awake() => teamMember = GetComponent<TeamMember>();

    // Called by DefenderCoordinator — external assignment, same reasoning as before.
    public void SetRole(Role role) => CurrentRole = role;
    public void SetCoverTarget(Transform receiver) => coverTarget = receiver;
    public void ClearCoverTarget() => coverTarget = null;

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
                // Hold position — pursuit-of-loose-ball is a separate, still-deferred system.
                break;
        }
    }

    void UpdateHeldState(Transform carrier)
    {
        if (carrier == null) return;

        Vector3 roleMove = CurrentRole switch
        {
            Role.Cover => CalculateCoverMove(),
            Role.Contain => CalculateContainMove(carrier),
            _ => CalculateEngageMove(carrier)
        };

        transform.position += (roleMove + CalculateSeparation()) * Time.deltaTime;
    }

    // The one genuinely new piece of gameplay: a Cover defender guarding the ACTUAL
    // intended receiver breaks toward the landing spot instead of standing still while
    // the ball sails past. Everyone else near the landing spot still gets a shot via
    // BallController's own OverlapSphere check once it arrives — they just don't get
    // the head start, which is what keeps this from reading as every defender on the
    // field psychically converging on a live throw.
    void UpdateInFlightState(BallController ball)
    {
        bool isTargetedDefender = coverTarget != null && ball.IntendedReceiver == coverTarget;
        if (!isTargetedDefender || ballInFlightTimer < breakReactionDelay) return;

        Vector3 landing = ball.FlightTarget;
        float distance = Vector3.Distance(transform.position, landing);
        if (distance <= stopDistance) return;

        Vector3 direction = (landing - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(direction);
        transform.position += direction * (MoveSpeed * breakOnBallSpeedMult) * Time.deltaTime;
    }

    Vector3 CalculateCoverMove()
    {
        if (coverTarget == null) return Vector3.zero; // assignment lost mid-play — coordinator reassigns next snap

        FieldDirection receiverAttack = PlayState.Instance != null && coverTarget.TryGetComponent<TeamMember>(out var wrTeam)
            ? PlayState.Instance.DirectionFor(wrTeam.teamId)
            : FieldDirection.TowardPositiveZ;

        Vector3 shadowPoint = coverTarget.position - receiverAttack.Forward * coverCushion;
        float distance = Vector3.Distance(transform.position, shadowPoint);
        if (distance <= stopDistance) return Vector3.zero;

        Vector3 direction = (shadowPoint - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation((coverTarget.position - transform.position).normalized);
        return direction * (MoveSpeed * coverCatchUpSpeedMult);
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

        if (distanceToCarrier <= containBreakRadius)
            return CalculateEngageMove(target);

        float losZ = PlayState.Instance != null ? PlayState.Instance.CurrentLineOfScrimmageZ : target.position.z;
        float holdZ = PlayState.Instance != null
            ? PlayState.Instance.ViewDirection.Advance(losZ, containLeadDistance)
            : losZ + containLeadDistance;
        Vector3 holdPosition = new(transform.position.x, transform.position.y, holdZ);
        float distanceToHold = Vector3.Distance(transform.position, holdPosition);

        if (distanceToHold <= stopDistance) return Vector3.zero;

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