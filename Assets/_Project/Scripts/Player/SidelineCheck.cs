using UnityEngine;

// Polled dead-ball check, same "no Rigidbody/trigger-callback" pattern as TouchdownZone
// and TackleContact. Checks the BALL's position (not the player's) — once passing exists,
// a receiver's feet at the catch point are what should matter, not where the QB is
// standing. Margin comes from FieldConstants so wall placement and OOB ruling agree on
// where "out" actually is.
public class SidelineCheck : MonoBehaviour
{
    void Update()
    {
        if (PlayState.Instance == null || !PlayState.Instance.IsLive) return;
        if (BallController.Instance == null) return;

        float x = BallController.Instance.transform.position.x;
        if (Mathf.Abs(x) > FieldConstants.HalfWidth + FieldConstants.OutOfBoundsMargin)
        {
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.OutOfBounds);
        }
    }
}