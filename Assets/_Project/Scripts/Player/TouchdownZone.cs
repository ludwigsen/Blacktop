using UnityEngine;

// Polled touchdown check against the end zone the ball is being driven toward. Deliberately
// checks a HELD BALL rather than the carrier's feet: possession is required before
// crossing the goal line can score. FieldBounds performs the test in FieldRoot-local coordinates, so moving or
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
        // PlayState validates held possession AND legal field position for every caller.
        // This poll only requests the ruling; it cannot score a pass or a loose fumble.
        if (PlayState.Instance.TryGetTouchdownTeam(out _, fieldBounds))
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.Touchdown);
    }
}
