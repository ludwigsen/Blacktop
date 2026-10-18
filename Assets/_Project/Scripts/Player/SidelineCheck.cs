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
        var play = PlayState.Instance;
        if (play == null || !play.IsLive) return;

        // PlayState also applies this check before tackle/fumble resolution, so the
        // same live ball position yields one ruling regardless of polling order.
        if (play.IsBallOutOfBounds(fieldBounds))
            play.EndPlay(PlayState.PlayEndReason.OutOfBounds);
    }
}
