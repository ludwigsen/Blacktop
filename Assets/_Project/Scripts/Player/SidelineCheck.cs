using UnityEngine;

/// <summary>
/// Dead-ball check against the shared FieldBounds definition.
///
/// FieldBounds determines the legal field.
/// FieldBoundaryWall determines whether a boundary crossing is physically
/// enclosed by a wall.
///
/// The ball remains the ruling point for OOB — player position is irrelevant
/// once the ball itself has left the playable area.
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

        if (BallController.Instance == null)
            return;

        Vector3 ballPos = BallController.Instance.transform.position;

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

    void EvaluateLegacyBounds(Vector3 ballPos)
    {
        float xLimit =
            FieldConstants.HalfWidth +
            FieldConstants.OutOfBoundsMargin;

        float zLimit =
            FieldConstants.PlayLength * 0.5f +
            FieldConstants.OutOfBoundsMargin;

        if (Mathf.Abs(ballPos.x) > xLimit ||
            Mathf.Abs(ballPos.z) > zLimit)
        {
            PlayState.Instance.EndPlay(
                PlayState.PlayEndReason.OutOfBounds);
        }
    }
}