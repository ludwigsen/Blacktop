using UnityEngine;

// Polled touchdown check against both standard end zones. It deliberately checks the
// BALL rather than the carrier: passes, fumbles, and future interceptions all use the
// same rule. FieldBounds performs the test in FieldRoot-local coordinates, so moving or
// rotating the complete field does not require any scoring-code changes.
public class TouchdownZone : MonoBehaviour
{
    [SerializeField] FieldBounds fieldBounds;

    void Reset()
    {
        fieldBounds = GetComponentInParent<FieldBounds>();
    }

    void Awake()
    {
        if (fieldBounds == null) fieldBounds = GetComponentInParent<FieldBounds>();
        if (fieldBounds == null) fieldBounds = FindFirstObjectByType<FieldBounds>();
    }

    void Update()
    {
        if (PlayState.Instance == null || !PlayState.Instance.IsLive) return;
        if (BallController.Instance == null) return;

        if (fieldBounds != null)
        {
            if (fieldBounds.IsInEndZone(BallController.Instance.transform.position))
                PlayState.Instance.EndPlay(PlayState.PlayEndReason.Touchdown);
            return;
        }

        // Compatibility for scenes not yet migrated to a FieldRoot. New fields should
        // always assign FieldBounds above so this world-axis fallback is never used.
        float z = BallController.Instance.transform.position.z;
        if (z >= FieldConstants.FarGoalLineZ || z <= FieldConstants.NearGoalLineZ)
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.Touchdown);
    }
}
