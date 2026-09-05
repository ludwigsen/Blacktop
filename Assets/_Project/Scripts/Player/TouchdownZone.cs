using UnityEngine;

// Polled Z-threshold check against BOTH goal lines now that fields are fixed-size and
// symmetric. Checks the BALL's position, not this component's own transform — same
// reasoning as before (fumble recovery, eventual interceptions/passing mean the carrier
// isn't guaranteed to be the player). The far goal line is currently dead code (one
// offense always drives toward +Z), but building it symmetric now means a future
// possession-flip system needs zero changes here.
public class TouchdownZone : MonoBehaviour
{
    void Update()
    {
        if (PlayState.Instance == null || !PlayState.Instance.IsLive) return;
        if (BallController.Instance == null) return;

        float z = BallController.Instance.transform.position.z;
        if (z >= FieldConstants.FarGoalLineZ || z <= FieldConstants.NearGoalLineZ)
        {
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.Touchdown);
        }
    }
}