using UnityEngine;

// Polled Z-threshold check, same "no Rigidbody/trigger-callback dependency" pattern as
// TackleContact/DefenderDetector. Checks the BALL's position, not this component's own
// transform — now that a real ball object exists, scoring should track wherever the ball
// actually is, not assume the player is always the carrier (a fumble recovered near the
// goal line, or eventually a defender/teammate carrying, would score off the wrong
// position otherwise).
public class TouchdownZone : MonoBehaviour
{
    [SerializeField] float endZoneZ = 25f;

    void Update()
    {
        if (PlayState.Instance == null || !PlayState.Instance.IsLive) return;
        if (BallController.Instance == null) return;

        if (BallController.Instance.transform.position.z >= endZoneZ)
        {
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.Touchdown);
        }
    }
}