using UnityEngine;

// Same polling approach as DefenderDetector — no Rigidbody dependency. Omnidirectional
// (small sphere centered on player) since a tackle from behind or the side is just as
// valid as one from the front, unlike Hurdle's forward-only detection.
public class TackleContact : MonoBehaviour
{
    [SerializeField] string defenderTag = "Defender";
    [SerializeField] float contactRadius = 0.8f;

    void Update()
    {
        if (PlayState.Instance == null || !PlayState.Instance.IsLive) return;

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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, contactRadius);
    }
}