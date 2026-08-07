using UnityEngine;

// Tight-range contact check, separate from DefenderDetector's forward zone (which is
// deliberately generous/forgiving for Hurdle gating). Tackle contact should be a much
// smaller, more precise trigger — "the defender is actually touching you," not "roughly
// in front of you." Sits on the PLAYER, checks for the Defender tag, same pattern as
// DefenderDetector but different collider size/purpose.
[RequireComponent(typeof(SphereCollider))]
public class TackleContact : MonoBehaviour
{
    [SerializeField] string defenderTag = "Defender";

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(defenderTag)) return;
        if (PlayState.Instance == null || !PlayState.Instance.IsLive) return; // already-dead plays shouldn't re-trigger
        PlayState.Instance.EndPlay();
    }
}