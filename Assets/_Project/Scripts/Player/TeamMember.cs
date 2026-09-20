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

    // The one "is that guy on the other team?" check. Replaces every "Defender"/"Teammate"
    // tag comparison — tags encoded a permanent offense/defense split that stopped being
    // true once either team can have the ball. Works on colliders too (players keep their
    // TeamMember on the same GameObject as the collider). Anything without a TeamMember —
    // ball, scenery, props — is never an opponent.
    public static bool AreOpponents(Component self, Component other)
    {
        return self.TryGetComponent<TeamMember>(out var a)
            && other.TryGetComponent<TeamMember>(out var b)
            && a.teamId != b.teamId;
    }

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