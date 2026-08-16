using UnityEngine;

// Chase-and-contain logic, transform-based (no Rigidbody — consistent with the rest of
// the project). Behavior now branches on a Role set externally by DefenderCoordinator:
// Engage = direct pursuit (today's original behavior). Contain = hold a lane position
// between the ball carrier and the end zone, only escalating to direct chase if the
// ball carrier gets close enough to this specific defender to be a real threat.
public class DefenderAI : MonoBehaviour
{
    public enum Role { Engage, Contain }

    [SerializeField] Transform target;
    [SerializeField] DefenderAttributes attributes;
    [SerializeField] float baseMoveSpeed = 5f;
    [SerializeField] float stopDistance = 1f;
    [SerializeField] float separationRadius = 1.2f;
    [SerializeField] float separationStrength = 3f;
    [SerializeField] string defenderTag = "Defender";

    // How close the ball carrier needs to get to THIS defender before a Contain defender
    // drops the "hold position" behavior and chases directly, same as Engage would.
    // This is what makes a broken-past defender still a threat rather than a permanent
    // statue once someone else is marked Engage.
    [SerializeField] float containBreakRadius = 4f;

    // How far downfield (toward the end zone) a Contain defender holds relative to the
    // ball carrier's current Z — keeps them positioned as a real obstacle ahead of the
    // runner rather than standing still wherever they started.
    [SerializeField] float containLeadDistance = 3f;

    public Role CurrentRole { get; private set; } = Role.Engage; // default Engage so a single-defender scene (no coordinator) behaves exactly as before

    float MoveSpeed => baseMoveSpeed * (attributes != null ? attributes.speedMult : 1f);
    public float ResistMult => attributes != null ? attributes.resistMult : 1f;

    Vector3 pushBackTarget;
    float pushBackTimer;
    const float pushBackDuration = 0.15f;
    float shedTimer;

    void Awake()
    {
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    // Called by DefenderCoordinator once per frame — external assignment rather than
    // this script deciding its own role, since "who's closest" requires comparing
    // across ALL defenders, information a single DefenderAI instance doesn't have.
    public void SetRole(Role role) => CurrentRole = role;

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        // Push-back and shed states take priority over any role behavior — being
        // stiff-armed interrupts whatever the defender was doing.
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

        if (target == null) return;

        Vector3 roleMove = CurrentRole == Role.Engage ? CalculateEngageMove() : CalculateContainMove();
        Vector3 separationMove = CalculateSeparation();

        transform.position += (roleMove + separationMove) * Time.deltaTime;
    }

    Vector3 CalculateEngageMove()
    {
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance <= stopDistance) return Vector3.zero;

        Vector3 direction = (target.position - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(direction);
        return direction * MoveSpeed;
    }

    Vector3 CalculateContainMove()
    {
        float distanceToCarrier = Vector3.Distance(transform.position, target.position);

        if (distanceToCarrier <= containBreakRadius)
        {
            return CalculateEngageMove();
        }

        // Hold position is anchored to the actual line of scrimmage, NOT the ball carrier's
        // live position — otherwise a player moving backward drags the whole contain formation
        // backward with them, which reads as defenders "predicting" a sack/negative play rather
        // than actually holding ground. LOS is the fixed anchor; only the break-radius check
        // above should react to where the ball carrier currently is.
        float losZ = PlayState.Instance != null ? PlayState.Instance.CurrentLineOfScrimmageZ : target.position.z;
        Vector3 holdPosition = new Vector3(transform.position.x, transform.position.y, losZ + containLeadDistance);
        float distanceToHold = Vector3.Distance(transform.position, holdPosition);

        if (distanceToHold <= stopDistance) return Vector3.zero;

        Vector3 direction = (holdPosition - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation((target.position - transform.position).normalized);
        return direction * MoveSpeed;
    }

    Vector3 CalculateSeparation()
    {
        Vector3 push = Vector3.zero;
        GameObject[] allDefenders = GameObject.FindGameObjectsWithTag(defenderTag);

        foreach (var other in allDefenders)
        {
            if (other.transform == transform) continue;

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