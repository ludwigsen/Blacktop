using UnityEngine;

// Identifies which team and roster slot this player occupies. Team and role are
// deliberately decoupled — team is static per player (T1Player3 is always Team 1),
// but offense/defense is NOT baked in here. It's resolved live from ball possession
// (IsOnOffense) so either team can be on offense on a given play.
public class TeamMember : MonoBehaviour
{
    public enum RosterSlot { QB, RB, WR1, WR2, WR3, OL1, OL2 }

    [Tooltip("0 = Team 1, 1 = Team 2.")]
    public int teamId;

    public RosterSlot slot;

    // True when this player's team currently has the ball. Resolved live every call —
    // never cached — same reasoning as every other BallController.Carrier read in this
    // project (DefenderAI/DefenderCoordinator/CameraFollow/TouchdownZone).
    public bool IsOnOffense
    {
        get
        {
            if (BallController.Instance == null || BallController.Instance.Carrier == null) return false;
            var carrierTeam = BallController.Instance.Carrier.GetComponent<TeamMember>();
            return carrierTeam != null && carrierTeam.teamId == teamId;
        }
    }
}