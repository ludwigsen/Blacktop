using UnityEngine;

/// <summary>
/// Dead-ball check against the shared FieldBounds definition (or the FieldConstants
/// rectangle when the scene has no FieldBounds yet).
///
/// FieldBounds determines the legal field.
/// FieldBoundaryWall determines whether a boundary crossing is physically
/// enclosed by a wall.
///
/// The ball remains the ruling point for OOB — player position is irrelevant
/// once the ball itself has left the playable area.
///
/// A ball that is IN FLIGHT is never ruled out of bounds mid-air: a pass arcing over the
/// sideline can still be caught in bounds, and one that isn't resolves as Incomplete when
/// it lands (BallController.ResolveArrival). Held and Loose balls are ruled live.
///
/// PlayState adds one of these at Awake if the scene doesn't have one.
/// </summary>
public class SidelineCheck : MonoBehaviour
{
    [SerializeField] FieldBounds fieldBounds;

    void Awake()
    {
        if (fieldBounds == null)
            fieldBounds = FindAnyObjectByType<FieldBounds>();
    }

    void Update()
    {
        if (PlayState.Instance == null || !PlayState.Instance.IsLive)
            return;

        var ball = BallController.Instance;
        if (ball == null)
            return;

        if (ball.State == BallController.BallState.InFlight)
            return;

        Vector3 ballPos = ball.transform.position;

        if (fieldBounds != null)
        {
            EvaluateFieldBounds(ballPos);
            return;
        }

        EvaluateLegacyBounds(ballPos);
    }

    void EvaluateFieldBounds(Vector3 ballPos)
    {
        FieldBoundsState state = fieldBounds.GetState(ballPos);

        if (state != FieldBoundsState.OutOfBounds)
            return;

        // The ball crossed a legal boundary, but that boundary may be enclosed
        // by a physical wall. If so, let physics continue the play.
        if (fieldBounds.IsWallContained(ballPos))
            return;

        PlayState.Instance.EndPlay(
            PlayState.PlayEndReason.OutOfBounds);
    }

    // No FieldBounds in the scene: rule at the actual painted lines. The old version added
    // FieldConstants.OutOfBoundsMargin (2u) here, but that margin exists for walls sitting
    // flush at the boundary — with no walls it just let the ball run 2u past the line.
    // Wall-enclosed fields should use FieldBounds + FieldBoundaryWall instead.
    void EvaluateLegacyBounds(Vector3 ballPos)
    {
        float xLimit = FieldConstants.HalfWidth;
        float zLimit = FieldConstants.PlayLength * 0.5f;

        if (Mathf.Abs(ballPos.x) > xLimit ||
            Mathf.Abs(ballPos.z) > zLimit)
        {
            PlayState.Instance.EndPlay(
                PlayState.PlayEndReason.OutOfBounds);
        }
    }
}
