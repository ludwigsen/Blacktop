using UnityEngine;

// Dead-ball check against the shared FieldBounds definition. The ball is the ruling point:
// player feet are irrelevant once the ball leaves the field. FieldBounds is authoritative
// for both sideline and end-line geometry.
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
        if (PlayState.Instance == null || !PlayState.Instance.IsLive) return;
        if (BallController.Instance == null) return;

        Vector3 ballPos = BallController.Instance.transform.position;

        if (fieldBounds != null)
        {
            if (fieldBounds.GetState(ballPos) == FieldBoundsState.OutOfBounds)
                PlayState.Instance.EndPlay(PlayState.PlayEndReason.OutOfBounds);
            return;
        }

        // Legacy fallback for scenes that have not yet added FieldBounds.
        if (Mathf.Abs(ballPos.x) > FieldConstants.HalfWidth + FieldConstants.OutOfBoundsMargin)
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.OutOfBounds);
    }
}