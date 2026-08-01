using UnityEngine;

// Per-player stat multipliers. Design as multipliers off tuned base values (in
// PlayerMovement/JukeMove/etc), never as absolute overrides — keeps solo-playtested
// base feel intact regardless of which archetype is active.
[CreateAssetMenu(menuName = "Blacktop/Player Attributes")]
public class PlayerAttributes : ScriptableObject
{
    [Range(0.7f, 1.3f)] public float speedMult = 1f;
    [Range(0.7f, 1.3f)] public float agilityMult = 1f; // affects Juke distance + duration
    [Range(0.7f, 1.3f)] public float accelMult = 1f;
    [Range(0.7f, 1.3f)] public float hurdleMult = 1f;  // affects Hurdle peak height/reach
}