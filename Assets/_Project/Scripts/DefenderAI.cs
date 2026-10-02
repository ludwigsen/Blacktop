using UnityEngine;

[RequireComponent(typeof(TeamMember))]
public class DefenderAI : MonoBehaviour
{
    public enum Role { Engage, Contain }

    [SerializeField] PlayerAttributes attributes;
    [SerializeField] float baseMoveSpeed = 5f;
    [SerializeField] float stopDistance = 1f;
    [SerializeField] float separationRadius = 1.2f;
    [SerializeField] float separationStrength = 3f;
    [SerializeField] float containBreakRadius = 4f;
    [SerializeField] float containLeadDistance = 3f;

    public Role CurrentRole { get; private set; } = Role.Engage;

    TeamMember teamMember;

    float MoveSpeed => baseMoveSpeed * SpeedMult;
    public float SpeedMult => attributes != null ? attributes.Speed() : 1f;
    public float ResistMult => attributes != null ? attributes.Tackling() : 1f;

    public void SetAttributes(PlayerAttributes newAttributes) => attributes = newAttributes;

    Transform Target => BallController.Instance != null ? BallController.Instance.Carrier : null;

    Vector3 pushBackTarget;
    float pushBackTimer;
    const float pushBackDuration = 0.15f;
    float shedTimer;

    public bool IsUserControlled { get; private set; }
    public bool IsStunned => pushBackTimer > 0f || shedTimer > 0f;
    public void SetUserControlled(bool controlled) => IsUserControlled = controlled;

    void Awake()
    {
        teamMember = GetComponent<TeamMember>();
    }

    public void SetRole(Role role) => CurrentRole = role;

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

        var target = Target;
        if (target == null) return;

        Vector3 roleMove = CurrentRole == Role.Engage ? CalculateEngageMove(target) : CalculateContainMove(target);
        Vector3 separationMove = CalculateSeparation();

        transform.position += (roleMove + separationMove) * Time.deltaTime;
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
        {
            return CalculateEngageMove(target);
        }

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

    public void ApplyShed(float duration)
    {
        shedTimer = duration;
    }
}