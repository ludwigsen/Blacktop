using UnityEngine;

// Tight-range contact check, separate from DefenderDetector's forward zone (which is
// deliberately generous/forgiving and forward-only, for Hurdle's OLD gating behavior).
// Tackle contact is omnidirectional and precise — a tackle from behind or the side counts
// just as much as one from the front. Polled via Physics.OverlapSphere rather than
// OnTriggerEnter/Exit, since trigger callbacks require a Rigidbody on at least one
// colliding object and this project has none (transform-based movement throughout).
public class TackleContact : MonoBehaviour
{
    [SerializeField] string defenderTag = "Defender";
    [SerializeField] float contactRadius = 0.8f;

    PlayerStateMachine stateMachine;

    void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
    }

    void Update()
    {
        if (PlayState.Instance == null || !PlayState.Instance.IsLive) return;

        // Hurdle can grant temporary tackle immunity via a successful negation roll —
        // check this BEFORE the overlap check, so an immune player passes through a
        // defender's contact entirely, no play-ending call at all.
        if (stateMachine != null && stateMachine.IsTackleImmune) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, contactRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag(defenderTag))
            {
                PlayState.Instance.EndPlay(PlayState.PlayEndReason.Tackled);
                return;
            }
        }
    }

    // Visualize the contact radius in Scene view even without a real collider component —
    // makes tuning contactRadius visually verifiable instead of guessing blind.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, contactRadius);
    }
}