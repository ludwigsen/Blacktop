using System.Collections.Generic;
using UnityEngine;

// Authored once, reusable across scenes/resets — replaces PlayState's own hardcoded
// offset list. A formation is just a named set of positions relative to the line of
// scrimmage anchor point. Swapping formations later (goal-line vs. spread, etc.) becomes
// "assign a different asset" instead of re-entering every offset by hand.
[CreateAssetMenu(menuName = "Blacktop/Formation Data")]
public class FormationData : ScriptableObject
{
    [System.Serializable]
    public struct DefenderSlot
    {
        public string label; // purely for readability in the Inspector — "CB1", "MLB", etc. Not used in logic yet, but pays off once roles/AI behavior differ per slot.
        public Vector3 offsetFromLOS;
    }

    public List<DefenderSlot> defenderSlots = new List<DefenderSlot>();
}