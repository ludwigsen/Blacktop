using UnityEngine;

// Forward detection zone — a trigger collider (not a raycast) sitting in front of the
// player, checking for anything tagged "Defender" within range. Trigger over raycast
// because it's more forgiving/arcade-appropriate: a raycast can miss a defender standing
// slightly off-center, a volume trigger reads "roughly in front of me" more generously,
// matching how NFL Street-style games are forgiving rather than precise.
//
// This same detection also seeds the future tackle-contact system — "defender in my
// hurdle-trigger zone" and "defender close enough to tackle me" are the same underlying
// question at different ranges, so keeping this generic (not hardcoded to Hurdle) matters.
[RequireComponent(typeof(BoxCollider))]
public class DefenderDetector : MonoBehaviour
{
    [SerializeField] string defenderTag = "Defender";

    // Exposed as a simple bool rather than a list — for now we only care THAT something's
    // in range, not which defender or how many. Revisit if defender-specific logic
    // (e.g. targeting a specific tackle animation) is needed later.
    public bool DefenderInRange { get; private set; }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(defenderTag)) DefenderInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(defenderTag)) DefenderInRange = false;
    }
}