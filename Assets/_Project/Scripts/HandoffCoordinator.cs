using UnityEngine;

// Automatic QB-to-back exchange on designed run plays. Not input-driven — the play call
// itself decides whether a handoff happens (PlayCallData.PlayType == Run) and who
// receives it (HandoffReceiverSlot). On a pass play this never finds a run flag to act
// on, so it's a pure no-op.
//
// Also owns the receiver's autopath to the QB — a real mesh point requires the back to
// close the gap, not the QB hunting the back down. (The QB reverse-pivoting to meet the
// back partway is a real technique too, but the QB is human-controlled here — out of
// scope to script that side; the back walking the full distance alone is enough to
// reliably trigger the exchange.)
//
// Sits on GameManager alongside DefenderCoordinator/BlockingCoordinator — same
// per-frame-coordinator pattern, same live TeamMember resolution instead of tags.
public class HandoffCoordinator : MonoBehaviour
{
    // Generous mesh-point radius — this is the QB/RB exchange point, not a skill check.
    // 2f, not 1.5f: Shotgun_Base spawns the RB exactly 1.5 units from the QB, which put
    // the old default right on the edge of triggering (or not) on frame one purely from
    // float precision.
    [SerializeField] float handoffRadius = 2f;
    [SerializeField] float autoPathSpeed = 6f; // matches ReceiverAI's default moveSpeed — same footspeed whether running a route or running the mesh point
    [SerializeField, Min(0.01f)] float presentationApproachDistance = 0.9f;

    // Latches once resolved so a fumble recovered back to the QB mid-play can't
    // re-trigger a second "handoff" just because the receiver happens to still be nearby.
    bool handoffResolvedThisPlay;

    void OnEnable()
    {
        if (PlayState.Instance != null) PlayState.Instance.OnPlayReset += HandlePlayReset;
    }

    void OnDisable()
    {
        if (PlayState.Instance != null) PlayState.Instance.OnPlayReset -= HandlePlayReset;
    }

    void HandlePlayReset() => handoffResolvedThisPlay = false;

    void Update()
    {
        var play = PlayState.Instance;
        if (play == null || (!play.IsLive && !play.IsSetAtLOS)) return;
        if (play.IsLive && handoffResolvedThisPlay) return;

        var playCall = play.CurrentPlayCall;
        if (playCall == null || playCall.PlayType != PlayType.Run) return;

        if (BallController.Instance == null || !BallController.Instance.IsHeld) return;
        Transform carrier = BallController.Instance.Carrier;
        if (carrier == null) return;

        // Only relevant while the QB still has it — once the back receives it,
        // carrier.slot is no longer QB, so this naturally stops without a second guard.
        if (!carrier.TryGetComponent<TeamMember>(out var carrierTeam)) return;
        if (carrierTeam.teamId != play.PossessionTeamId) return; // handoff logic only ever applies to the team actually running this play call
        if (carrierTeam.slot != TeamMember.RosterSlot.QB) return;

        // A handoff is a behind-the-line exchange. Once the QB has crossed the LOS it's
        // off for the rest of the play — otherwise the back keeps chasing the QB downfield
        // and "hands off" past the line. Latching also stops the autopath, so the back
        // goes back to normal behavior (blocking) instead of running into the QB.
        if (play.IsLive && play.IsPastLineOfScrimmage(carrier.position.z))
        {
            handoffResolvedThisPlay = true;
            return;
        }

        Transform receiver = FindReceiver(carrierTeam.teamId, playCall.HandoffReceiverSlot);
        if (receiver == null || PlayerStateMachine.IsMovementLocked(receiver)) return;

        if (!play.IsLive)
        {
            // The real QB already has the football in Blacktop's formation.
            // Cue a small receiving pocket during Set while gameplay stays frozen.
            PrepareExchange(carrier, receiver, 0.42f);
            return;
        }

        // Autopath: walk the receiver toward the QB's current position every frame
        // until the mesh point closes the gap and the exchange resolves.
        Vector3 toQB = carrier.position - receiver.position;
        toQB.y = 0f;
        if (toQB.sqrMagnitude > 0.01f)
            receiver.rotation = Quaternion.LookRotation(toQB.normalized);

        // The designed exchange uses the receiver's same effective speed as routes/blocking.
        var attributes = receiver.GetComponent<PlayerStateMachine>()?.Attributes;
        Vector3 destination = Vector3.MoveTowards(receiver.position, carrier.position,
            autoPathSpeed * (attributes != null ? attributes.Speed() : 1f) * Time.deltaTime);
        if (!PlayerStateMachine.MoveActor(receiver, destination)) return;

        float distance = Vector3.Distance(carrier.position, receiver.position);
        if (distance <= handoffRadius)
        {
            // Commit ownership at the same mesh point. The paired action is visual.
            BallController.Instance.HandoffTo(receiver);
            handoffResolvedThisPlay = true;
        }
        else PrepareExchange(carrier, receiver, 1f);
    }

    void PrepareExchange(Transform carrier, Transform receiver, float progressLimit)
    {
        float distance = Vector3.Distance(carrier.position, receiver.position);
        float progress = Mathf.InverseLerp(handoffRadius + presentationApproachDistance, handoffRadius, distance);
        if (progress <= 0f || !carrier.TryGetComponent<PlayerAnimationPresenter>(out var giver) ||
            !receiver.TryGetComponent<PlayerAnimationPresenter>(out var taker)) return;

        Vector3 contact = FootballActionPose.GetHandoffContact(giver.Animator, taker.Animator, carrier, receiver);
        giver.ShowHandoffApproach(true, contact, progress * progressLimit);
        taker.ShowHandoffApproach(false, contact, progress * progressLimit);
    }

    internal static Transform FindReceiver(int teamId, TeamMember.RosterSlot slot)
    {
        foreach (var member in FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (member.teamId == teamId && member.slot == slot)
                return member.transform;
        }
        return null;
    }
}
