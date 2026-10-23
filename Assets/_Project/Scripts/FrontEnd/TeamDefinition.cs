using System;
using UnityEngine;

// Stable source-team identity and explicit both-ways assignments; roster order is never a position.
[CreateAssetMenu(menuName = "Blacktop/Front End/Team Definition")]
public class TeamDefinition : ScriptableObject
{
    public string id;
    public TeamIdentity identity;
    public TeamRoster roster;
    public LineupAssignment[] activeLineup = Array.Empty<LineupAssignment>();
}

[Serializable]
public class LineupAssignment
{
    public string playerId;
    public TeamMember.RosterSlot offense;
    public int defense; // 0..6: EDGE-L, EDGE-R, LB-L, LB-R, DB-L, DB-R, S.
}
