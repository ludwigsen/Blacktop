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
    [SerializeField] Transform target; // the player — assign in Inspector
    [SerializeField] float moveSpeed = 5f; // intentionally slower than player baseMaxSpeed (8) — defender shouldn't just walk it down instantly
    [SerializeField] float stopDistance = 1f; // how close before defender halts — prevents jittering/overlapping at zero distance

    // Shed = temporarily can't chase at all. Simple timer-based lockout rather than a
    // full state machine — defender doesn't need Juke/Hurdle-style complexity, just
    // "disabled for a bit."
    float shedTimer;
    bool IsShed => shedTimer > 0f;

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        if (shedTimer > 0f)
        {
            shedTimer -= Time.deltaTime;
            return; // frozen while shed — no movement, no chase
        }
        
        if (target == null) return;

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance <= stopDistance) return; // close enough — stand ground rather than pushing into/through the player

        Vector3 direction = (target.position - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        // Face the player while closing — cosmetic for now, but matters once defender has
        // any directional animation/reaction later.
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    // Called by StiffArmMove on a "push back" result — knocks the defender away
    // instantly (teleport-style displacement, matching the project's preference for
    // curve/direct-position moves over physics forces) but they keep chasing after.
    public void ApplyPushBack(Vector3 direction, float distance)
    {
        transform.position += direction * distance;
    }

    // Called by StiffArmMove on a "full shed" result — defender stops entirely for
    // a duration, giving the player a real window to separate before the chase resumes.
    public void ApplyShed(float duration)
    {
        shedTimer = duration;
    }
}