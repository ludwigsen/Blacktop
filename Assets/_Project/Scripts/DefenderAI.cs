using UnityEngine;

// Minimal defender behavior: closes distance on the player at a fixed speed. No
// pathfinding, no avoidance, no reaction to player moves yet — this exists purely to
// give DefenderDetector/Hurdle something dynamic to react to, replacing the static
// placeholder capsule. Same "arcade not sim" philosophy as PlayerMovement: transform-based,
// no Rigidbody/NavMesh, direct MoveTowards.
//
// Deliberately dumb for now. Smarter behavior (juke reaction, pursuit angles, containment)
// is a later pass once basic contact/tackle rules exist — no point tuning defender
// intelligence against a player move-set that's still being tuned itself.
public class DefenderAI : MonoBehaviour
{
    [SerializeField] Transform target; // the player
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float stopDistance = 1f;

    // Manual separation — since movement is transform-based (no Rigidbody), colliders
    // don't physically resolve overlaps between defenders on their own. This nudges
    // defenders apart when too close to each other, checked/applied every frame alongside
    // the chase movement. Cheap O(n) check against other Defenders — fine at this scale
    // (a handful of defenders), would need spatial partitioning if this ever scaled to
    // dozens of agents, which it won't for 7v7.
    [SerializeField] float separationRadius = 1.2f;
    [SerializeField] float separationStrength = 3f;
    [SerializeField] string defenderTag = "Defender";
    [SerializeField] DefenderAttributes attributes;
    [SerializeField] float baseMoveSpeed = 5f;
    Vector3 pushBackTarget;
    float pushBackTimer;
    const float pushBackDuration = 0.15f;

    float MoveSpeed => baseMoveSpeed * (attributes != null ? attributes.speedMult : 1f);
    // ...use MoveSpeed instead of moveSpeed in the chase calc

    // Exposed so StiffArmMove can read resistance when rolling/applying its outcome.
    public float ResistMult => attributes != null ? attributes.resistMult : 1f;

    float shedTimer;

    void Awake()
    {
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        if (shedTimer > 0f)
        {
            shedTimer -= Time.deltaTime;
            return;
        }

        // In Update(), before chase logic:
        if (pushBackTimer > 0f)
        {
            transform.position = Vector3.Lerp(transform.position, pushBackTarget, Time.deltaTime / pushBackTimer);
            pushBackTimer -= Time.deltaTime;
            return; // skip chase/separation this frame while being pushed
        }

        Vector3 chaseMove = Vector3.zero;
        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > stopDistance)
            {
                Vector3 direction = (target.position - transform.position).normalized;
                chaseMove = direction * moveSpeed;
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        Vector3 separationMove = CalculateSeparation();

        transform.position += (chaseMove + separationMove) * Time.deltaTime;
    }

    Vector3 CalculateSeparation()
    {
        Vector3 push = Vector3.zero;
        GameObject[] defenders = GameObject.FindGameObjectsWithTag(defenderTag);

        foreach (var other in defenders)
        {
            if (other.transform == transform) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist < separationRadius && dist > 0.001f)
            {
                Vector3 away = (transform.position - other.transform.position).normalized;
                push += away * (separationRadius - dist); // closer = stronger push
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