using UnityEngine;

// Polled touchdown/safety check against both standard end zones. Deliberately checks the
// BALL rather than the carrier: passes, fumbles, and interceptions all use the same rule.
// FieldBounds performs the test in FieldRoot-local coordinates, so moving or rotating the
// complete field does not require any scoring-code changes.
//
// The end zone the offense is driving toward scores a Touchdown; the one behind its own
// line is a Safety. "Toward" comes from PlayState.AttackDirection (possession-relative),
// so this stays correct when the other team has the ball and attacks the opposite way.
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
        if (fieldBounds == null) fieldBounds = FindAnyObjectByType<FieldBounds>();
    }

    void Update()
    {
        if (PlayState.Instance == null || !PlayState.Instance.IsLive) return;
        if (BallController.Instance == null) return;

        Vector3 ballPos = BallController.Instance.transform.position;

        // Which end zone is a Touchdown vs. a Safety depends on who's actually carrying it
        // RIGHT NOW, not who last had possession at the whistle — during a live interception
        // or fumble return the carrier is on the opposing team, driving the opposite way, and
        // PossessionTeamId hasn't flipped yet (that only happens at EndPlay). ViewDirection
        // already solves this exact staleness for the camera; reuse it here.
        FieldDirection dir = PlayState.Instance.ViewDirection;

        if (fieldBounds != null)
        {
            FieldEndZone zone = fieldBounds.GetEndZone(ballPos);
            if (zone == FieldEndZone.None) return;

            PlayState.Instance.EndPlay(zone == dir.TargetEndZone
                ? PlayState.PlayEndReason.Touchdown
                : PlayState.PlayEndReason.Safety);
            return;
        }

        // Compatibility for scenes not yet migrated to a FieldRoot. New fields should
        // always assign FieldBounds above so this world-axis fallback is never used.
        float z = ballPos.z;
        if (dir.IsInTargetEndZone(z))
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.Touchdown);
        else if (dir.IsInOwnEndZone(z))
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.Safety);
    }
}