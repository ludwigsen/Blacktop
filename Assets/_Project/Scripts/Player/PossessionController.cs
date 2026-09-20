using UnityEngine;
[RequireComponent(typeof(PlayerMovement))]

// Decides, every frame, whether THIS player is the human-controlled ball carrier or an
// AI-driven teammate. Control follows the ball rather than living on one fixed "the
// player" object — sits on every T1/T2 roster player, offense or defense, so either
// team's carrier can be the human-controlled one.
//
// This is also the fix for TackleContact/PlayerStateMachine going stale after a
// completed pass or pitch: those components check THEIR OWN transform, so as long as
// only the live carrier's copy is enabled, "their own transform" is automatically
// correct without those scripts needing to know anything about possession themselves.
//
// Not every roster slot has a ReceiverAI (a QB/OL doesn't run a route) — every
// reference below is null-guarded so this works on an object missing either half of
// the stack.
[RequireComponent(typeof(InputBuffer))]
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(TackleContact))]
[RequireComponent(typeof(TeamMember))]
public class PossessionController : MonoBehaviour
{
    PlayerMovement playerMovement;
    InputBuffer inputBuffer;
    PlayerStateMachine stateMachine;
    TackleContact tackleContact;
    ReceiverAI receiverAI;
    TeamMember teamMember;

    // AICarrier: holds the ball but isn't the human's team (user is defending). Nothing
    // drives it yet — it just has to stay tackleable, which is the ONLY thing the
    // carrier-side stack does for it.
    enum Mode { Controlled, AICarrier, AI, Frozen }
    Mode currentMode;

    // Single source of truth for "which Transform is the human actually piloting right
    // now" — set/cleared at the exact point this decision already gets made for real
    // gameplay reasons (ApplyMode), rather than duplicated as a separate guess elsewhere.
    // Safe as a plain static: control follows the ball, so at most one PossessionController
    // is ever in Controlled mode at a time. Null whenever nobody is controlled (ball loose).
    // Anything that needs to answer "is this the user's guy?" — highlight rings, future
    // UI, whatever — should read this, not re-derive its own notion of it.
    public static Transform ActivelyControlled { get; private set; }

    // Which team's player is currently controlled, if any — null when the ball is loose.
    // Exists for future control-switching UI ("cycle to another guy on MY team") so that
    // feature doesn't need to re-derive team membership from scratch.
    public static int? ActivelyControlledTeamId { get; private set; }

    // Defender control (DefenderControl) doesn't go through ApplyMode — a defender has no
    // PossessionController — but it still has to answer "which guy is the user piloting?"
    // the same way, so highlight rings and anything else reading ActivelyControlled just work.
    public static void ClaimControl(Transform t, int teamId)
    {
        ActivelyControlled = t;
        ActivelyControlledTeamId = teamId;
    }

    public static void ReleaseControl(Transform t)
    {
        if (ActivelyControlled != t) return;
        ActivelyControlled = null;
        ActivelyControlledTeamId = null;
    }

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        inputBuffer = GetComponent<InputBuffer>();
        stateMachine = GetComponent<PlayerStateMachine>();
        tackleContact = GetComponent<TackleContact>();
        teamMember = GetComponent<TeamMember>();
        receiverAI = GetComponent<ReceiverAI>(); // do not RequireComponent - OL/QB does not need this
    }

    void Start()
    {
        // Forces an explicit first application rather than trusting whatever enabled
        // state each component happened to be authored with in the Inspector.
        currentMode = DetermineMode();
        ApplyMode(currentMode);
    }

    void Update()
    {
        Mode desired = DetermineMode();
        if (desired == currentMode) return; // only touch .enabled on an actual change, not every frame

        ApplyMode(desired);
        currentMode = desired;
    }

    void OnDestroy()
    {
        // Safety net for the edge case where this object is destroyed mid-play while it
        // happened to be the controlled one — avoids ActivelyControlled pointing at a
        // destroyed Transform until the next possession change naturally clears it.
        if (ActivelyControlled == transform)
        {
            ActivelyControlled = null;
            ActivelyControlledTeamId = null;
        }
    }

    Mode DetermineMode()
    {
        if (BallController.Instance == null) return Mode.Frozen;

        // Fumble — nobody is human-controlled until someone recovers it.
        // Same convention DefenderAI already uses for a loose ball (hold position,
        // don't invent pursuit behavior that doesn't exist yet).
        if (BallController.Instance.State == BallController.BallState.Loose)
            return Mode.Frozen;

        if (BallController.Instance.Carrier != transform) return Mode.AI;

        // The human only pilots carriers on THEIR team. If the other team has the ball the
        // user is playing defense, so this carrier stays idle (but tackleable).
        bool isUsers = PlayState.Instance == null || teamMember == null || PlayState.Instance.IsUserTeam(teamMember.teamId);
        return isUsers ? Mode.Controlled : Mode.AICarrier;
    }

    void ApplyMode(Mode mode)
    {
        bool controlled = mode == Mode.Controlled;
        bool ai = mode == Mode.AI;
        bool carrying = controlled || mode == Mode.AICarrier;

        if (playerMovement != null) playerMovement.enabled = controlled;
        if (inputBuffer != null) inputBuffer.enabled = controlled;
        if (stateMachine != null) stateMachine.enabled = controlled;
        if (tackleContact != null) tackleContact.enabled = carrying; // only the live carrier needs to check for being tackled — human-piloted or not
        if (receiverAI != null) receiverAI.enabled = ai;

        if (controlled)
        {
            ActivelyControlled = transform;
            ActivelyControlledTeamId = teamMember != null ? teamMember.teamId : (int?)null;
        }
        else if (ActivelyControlled == transform)
        {
            // don't clear a DIFFERENT object's claim if this instance never held it
            ActivelyControlled = null;
            ActivelyControlledTeamId = null;
        }

        // Frozen: everything off. Object just sits wherever it is until this flips back
        // to Controlled or AI once someone recovers the ball.
    }
}