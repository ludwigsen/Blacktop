using UnityEngine;

// Chase-and-contain logic, transform-based (no Rigidbody — consistent with the rest of
// the project). Behavior branches on a Role set externally by DefenderCoordinator:
// Engage = direct pursuit. Contain = hold a lane position between the ball and the end
// zone, only escalating to direct chase if the carrier gets close enough to this specific
// defender to be a real threat.
//
// Target is resolved live from BallController every frame rather than cached once —
// this is what makes defenders track the ball itself (and whoever's currently carrying
// it) instead of a hardcoded reference to the player. Once fumbles/interceptions change
// possession mid-play, a cached reference would go stale immediately; this doesn't.
public class DefenderAI : MonoBehaviour
{
    public enum Role { Engage, Contain }

    [SerializeField] DefenderAttributes attributes;
    [SerializeField] float baseMoveSpeed = 5f;
    [SerializeField] float stopDistance = 1f;
    [SerializeField] float separationRadius = 1.2f;
    [SerializeField] float separationStrength = 3f;
    [SerializeField] string defenderTag = "Defender";

    // How close the ball carrier needs to get to THIS defender before a Contain defender
    // drops the "hold position" behavior and chases directly, same as Engage would.
    [SerializeField] float containBreakRadius = 4f;

    // How far downfield (toward the end zone) a Contain defender holds relative to the
    // carrier's current Z — keeps them positioned as a real obstacle ahead of the
    // runner rather than standing still wherever they started.
    [SerializeField] float containLeadDistance = 3f;

    public Role CurrentRole { get; private set; } = Role.Engage; // default Engage so a scene without a coordinator behaves sanely

    float MoveSpeed => baseMoveSpeed * (attributes != null ? attributes.speedMult : 1f);
    public float ResistMult => attributes != null ? attributes.resistMult : 1f;

    // Resolved live each frame — null when the ball is loose (fumbled, not yet
    // recovered). No fallback to a hardcoded Player reference; a loose ball means
    // defenders have nothing to chase yet (recovery/pursuit-of-loose-ball is a
    // future system, not this one).
    Transform Target => BallController.Instance != null ? BallController.Instance.Carrier : null;

    Vector3 pushBackTarget;
    float pushBackTimer;
    const float pushBackDuration = 0.15f;
    float shedTimer;

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

        var target = Target; // resolve once per frame — avoids repeated property/null-check calls below
        if (target == null) return; // loose ball — hold current position rather than chasing nothing

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

        // Hold position is anchored to the actual line of scrimmage, NOT the carrier's
        // live position — otherwise a player moving backward drags the whole contain
        // formation backward with them. LOS is the fixed anchor; only the break-radius
        // check above should react to where the carrier currently is.
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