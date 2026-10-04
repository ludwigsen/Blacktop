using UnityEngine;

// Polled touchdown check against the end zone the ball is being driven toward. Deliberately
// checks the BALL rather than the carrier: passes, fumbles, and interceptions all use the
// same rule. FieldBounds performs the test in FieldRoot-local coordinates, so moving or
// rotating the complete field does not require any scoring-code changes.
//
// This class NEVER ends a play as a Safety. A safety is a result of how a play ends (tackled
// or out of bounds with the offense's carrier inside its own end zone), so PlayState decides
// it in EndPlay. The ball merely touching your own end zone — QB dropping back, a pass flying
// through, an incomplete landing there — is a live play, not a safety.
//
// "Toward" comes from PlayState.ViewDirection (whoever is actually carrying right now), so
// this stays correct during a live interception or fumble return.
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

        // Resolve live, not from PossessionTeamId: during a live turnover the carrier is on
        // the other team and PossessionTeamId hasn't flipped yet (that happens in EndPlay).
        FieldDirection dir = PlayState.Instance.ViewDirection;

        if (fieldBounds != null)
        {
            if (fieldBounds.GetEndZone(ballPos) == dir.TargetEndZone)
                PlayState.Instance.EndPlay(PlayState.PlayEndReason.Touchdown);
            return;
        }

        // Compatibility for scenes not yet migrated to a FieldRoot (SampleScene has none
        // today). New fields should always assign FieldBounds so this fallback is unused.
        if (dir.IsInTargetEndZone(ballPos.z))
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.Touchdown);
    }
}
