using UnityEngine;

// Blocking AI for offensive teammates. No IPlayerMove, no state machine — just a passive
// component that subscribes to target assignments from BlockingCoordinator and moves toward
// them. Contact resolution (power vs resist) happens here.
//
// Only engages when:
// - Ball is possessed (BallController.Instance.Carrier != null)
// - Ball is not in flight (BallController.Instance.State == BallState.Held)
// - BlockingCoordinator has assigned this blocker a target
//
// If current target breaks free (leaves blocking range or becomes null), BlockingCoordinator
// will reassign next frame. Hysteresis (in BlockingCoordinator) prevents flickering when
// targets are close in distance.
public class AllyBlocker : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float stopDistance = 0.5f;
    [SerializeField] float contactDetectRadius = 1.2f; // OverlapSphere radius for contact resolution
    [SerializeField] float pushBackDistance = 0.8f;
    [SerializeField] float pushBackDuration = 0.15f; // lerp duration for pushback effect
    [SerializeField] string defenderTag = "Defender";

    // Attributes — assign the same PlayerAttributes asset as the ball carrier for now,
    // or create ally-specific variants once blocking feel needs differentiation from
    // the passer's stat line.
    [SerializeField] PlayerAttributes attributes;

    Transform currentTarget;

    // Pushback state — mirrors DefenderAI's pushBackTarget/pushBackTimer pattern so
    // both sides of a block resolve with the same lerp-based feedback.
    Vector3 pushBackTarget;
    float pushBackTimer;

    // Present only on RB/WR/TE slots (the ones that also carry ReceiverAI). Null on OL
    // — GetComponent returning null there is expected and harmless.
    ReceiverAI receiverAI;
    
    // True once this transform is the live ball carrier. BlockingCoordinator uses this
    // to skip us entirely during assignment — a carrier can't also be its own blocker,
    // and without this, catching a pass mid-route left AllyBlocker still trying to walk
    // toward a defender while PossessionController handed real control to the same
    // transform at the same time.
    public bool IsCarrier => BallController.Instance != null && BallController.Instance.Carrier == transform;

    void Awake()
    {
        receiverAI = GetComponent<ReceiverAI>();
    }

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        if (IsCarrier)
        {
            currentTarget = null;
            return;
        }

        // Defer to an in-progress route — only relevant on RB/WR/TE slots. OL have no
        // ReceiverAI component, so receiverAI is null and this never blocks them.
        if (receiverAI != null && !receiverAI.RouteComplete) return;

        // Only engage if ball is possessed and held (not in flight) — mirrors the
        // BlockingCoordinator's own gate, checked again here since a blocker could
        // still hold a stale target reference for one frame during the transition.
        if (BallController.Instance == null || BallController.Instance.Carrier == null ||
            BallController.Instance.State != BallController.BallState.Held)
        {
            currentTarget = null;
            return;
        }

        // Pushback in progress takes priority over movement/contact — same pattern as
        // DefenderAI's pushBackTimer gate.
        if (pushBackTimer > 0f)
        {
            transform.position = Vector3.Lerp(transform.position, pushBackTarget, Time.deltaTime / pushBackTimer);
            pushBackTimer -= Time.deltaTime;
            return;
        }

        if (currentTarget == null) return;

        MoveTowardTarget();
        CheckBlockContact();
    }

    void MoveTowardTarget()
    {
        Vector3 toTarget = currentTarget.position - transform.position;
        float distance = toTarget.magnitude;
        if (distance <= stopDistance) return;

        Vector3 direction = toTarget.normalized;
        transform.rotation = Quaternion.LookRotation(direction);
        transform.position += direction * moveSpeed * Time.deltaTime;
    }

    void CheckBlockContact()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, contactDetectRadius);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag(defenderTag)) continue;
            if (hit.transform != currentTarget) continue; // only resolve contact against OUR assigned target

            var defenderAI = hit.GetComponent<DefenderAI>();
            if (defenderAI != null && attributes != null)
                ResolveBlockContact(hit.transform, defenderAI);

            break; // one resolution per frame
        }
    }

    // Power vs resist ratio decides who gets pushed back. Same shape as StiffArmMove's
    // shed-chance calc — attacker stat over the sum of both, not a flat multiplier —
    // so neither side can ever hit 0% or 100% regardless of how lopsided the attributes get.
    void ResolveBlockContact(Transform defender, DefenderAI defenderAI)
    {
        float blockerPower = attributes.RunPower();
        float defenderResist = defenderAI.ResistMult;
        float blockWinChance = blockerPower / (blockerPower + defenderResist);

        if (Random.value < blockWinChance)
        {
            // Blocker wins — push the defender back and off their pursuit line for a beat.
            Vector3 pushDir = (defender.position - transform.position).normalized;
            defenderAI.ApplyPushBack(pushDir, pushBackDistance);
        }
        else
        {
            // Defender holds — blocker gets nudged back instead, same visual grammar
            // as StiffArm's pushback so a "lost" block reads the same way as a shed attempt.
            Vector3 knockDir = (transform.position - defender.position).normalized;
            pushBackTarget = transform.position + knockDir * (pushBackDistance * 0.5f);
            pushBackTimer = pushBackDuration;
        }
    }

    // Called by BlockingCoordinator — this component never decides its own target,
    // same "assigned externally" pattern as DefenderAI.SetRole.
    public void SetBlockingTarget(Transform target) => currentTarget = target;
    public Transform GetCurrentTarget() => currentTarget;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, contactDetectRadius);

        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}