using UnityEngine;

// Mirrors PlayerAttributes' multiplier pattern for the defensive side. Same rule applies:
// multipliers off tuned base values, never absolute overrides, so base tuning work
// (moveSpeed, pushBackDistance, etc.) stays valid regardless of archetype.
[CreateAssetMenu(menuName = "Blacktop/Defender Attributes")]
public class DefenderAttributes : ScriptableObject
{
    [Range(0.7f, 1.3f)] public float speedMult = 1f;   // chase speed
    [Range(0.7f, 1.3f)] public float resistMult = 1f;  // resists StiffArm — higher = harder to push/shed
}