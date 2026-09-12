using UnityEngine;

// Polled touchdown/safety check against both standard end zones. Deliberately checks the
// BALL rather than the carrier: passes, fumbles, and interceptions all use the same rule.
// FieldBounds performs the test in FieldRoot-local coordinates, so moving or rotating the
// complete field does not require any scoring-code changes.
//
// The FAR end zone (the one the offense is driving toward) scores a Touchdown. The NEAR
// end zone (behind the offense's own line) is a Safety — this used to be unconditional
// (both end zones triggered Touchdown), so carrying the ball back into your own end zone
// falsely scored a touchdown FOR the offense instead of correctly killing the play as a
// safety. FieldBounds.GetEndZone() already distinguished Near/Far, it just wasn't read.
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

        Vector3 ballPos = BallController.Instance.transform.position;

        if (fieldBounds != null)
        {
            FieldEndZone zone = fieldBounds.GetEndZone(ballPos);
            if (zone == FieldEndZone.Far)
                PlayState.Instance.EndPlay(PlayState.PlayEndReason.Touchdown);
            else if (zone == FieldEndZone.Near)
                PlayState.Instance.EndPlay(PlayState.PlayEndReason.Safety);
            return;
        }

        // Compatibility for scenes not yet migrated to a FieldRoot. New fields should
        // always assign FieldBounds above so this world-axis fallback is never used.
        float z = ballPos.z;
        if (z >= FieldConstants.FarGoalLineZ)
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.Touchdown);
        else if (z <= FieldConstants.NearGoalLineZ)
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.Safety);
    }
}