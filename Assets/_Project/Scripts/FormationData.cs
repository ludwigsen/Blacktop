using System.Collections.Generic;
using UnityEngine;

// Authored once, reusable across scenes/resets — replaces PlayState's own hardcoded
// offset list. A formation is just a named set of positions relative to the line of
// scrimmage anchor point. Swapping formations later (goal-line vs. spread, etc.) becomes
// "assign a different asset" instead of re-entering every offset by hand.
//
// One asset can describe EITHER side's alignment, or both — offense and defense pick
// formations independently (Pistol/Shotgun vs. 2-2-2-1 base), so a given asset is free
// to leave whichever list it doesn't care about empty. 7v7_Base, for example, only
// fills defenderSlots; Pistol_Base/Shotgun_Base only fill qbOffsetFromLOS + offensiveSlots.
[CreateAssetMenu(menuName = "Blacktop/Formation Data")]
public class FormationData : ScriptableObject
{
    [System.Serializable]
    public struct DefenderSlot
    {
        public string label; // purely for readability in the Inspector — "CB1", "MLB", etc. Not used in logic yet, but pays off once roles/AI behavior differ per slot.
        public Vector3 offsetFromLOS;
    }

    [System.Serializable]
    public struct OffensiveSlot
    {
        public string label; // "RB", "OL-Left", "WR-X", etc. Same readability-only role as DefenderSlot.label.
        public Vector3 offsetFromLOS;
    }

    public List<DefenderSlot> defenderSlots = new List<DefenderSlot>();

    // QB gets its own dedicated field rather than living in offensiveSlots — it's the
    // one offensive position PlayState already has a direct transform reference to
    // (the ball carrier at snap), and it's the entire reason Pistol vs. Shotgun exists
    // as a distinct choice: depth relative to LOS is the only thing separating them
    // with no hike/exchange in this game. Everything else (RB mesh point, OL spacing,
    // WR/TE splits) is downstream of where the QB starts.
    public Vector3 qbOffsetFromLOS = new Vector3(0f, 0f, -5f);
    public List<OffensiveSlot> offensiveSlots = new List<OffensiveSlot>();
}