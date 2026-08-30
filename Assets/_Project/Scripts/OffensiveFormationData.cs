using System.Collections.Generic;
using UnityEngine;

// Mirrors FormationData's role for the defense, but deliberately a separate type rather
// than reusing FormationData directly — offense and defense slots read as structurally
// identical today (label + offset), but they're going to diverge (receiver slots will
// eventually carry a route assignment; defender slots will eventually carry a coverage
// assignment). Kept apart now instead of forcing a shared generic type that gets awkward
// the moment one side needs a field the other doesn't.
//
// The passer gets its own dedicated field rather than being "just another slot" — it's
// not interchangeable with the receiver list, it's specifically wherever UserPlayer
// lines up every single snap. Modeling it as a flagged entry in a list you have to search
// invites exactly the kind of silent-mismatch bug the by-index defender/receiver sync
// already carries; this way the passer's position can't accidentally end up unset or
// duplicated.
[CreateAssetMenu(menuName = "Blacktop/Offensive Formation Data")]
public class OffensiveFormationData : ScriptableObject
{
    [System.Serializable]
    public struct ReceiverSlot
    {
        public string label; // Inspector readability only — "Slot Left", "Split Right", "Wing" — not read by code
        public Vector3 offsetFromLOS;
    }

    [Tooltip("Where UserPlayer lines up at the snap. Every play starts here regardless of who ends up carrying the ball by the end of the previous play.")]
    public Vector3 passerOffsetFromLOS = Vector3.zero;

    public List<ReceiverSlot> receiverSlots = new List<ReceiverSlot>();
}