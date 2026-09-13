using UnityEngine;
[RequireComponent(typeof(PlayerMovement))]

// Decides, every frame, whether THIS offensive player is the human-controlled ball
// carrier or an AI-driven teammate. Control follows the ball rather than living on one
// fixed "the player" object — sits on UserPlayer and on every ReceiverAI teammate.
//
// This is also the fix for TackleContact/PlayerStateMachine going stale after a
// completed pass or pitch: those components check THEIR OWN transform, so as long as
// only the live carrier's copy is enabled, "their own transform" is automatically
// correct without those scripts needing to know anything about possession themselves.
//
// UserPlayer legitimately has no ReceiverAI (a passer doesn't run a route after
// throwing) — every reference below is null-guarded so this works on an object missing
// either half of the stack.
[RequireComponent(typeof(InputBuffer))]
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(TackleContact))]
public class PossessionController : MonoBehaviour
{
    PlayerMovement playerMovement;
    InputBuffer inputBuffer;
    PlayerStateMachine stateMachine;
    TackleContact tackleContact;
    ReceiverAI receiverAI;

    enum Mode { Controlled, AI, Frozen }
    Mode currentMode;

    // Single source of truth for "which Transform is the human actually piloting right
    // now" — set/cleared at the exact point this decision already gets made for real
    // gameplay reasons (ApplyMode), rather than duplicated as a separate guess elsewhere.
    // Safe as a plain static: control follows the ball, so at most one PossessionController
    // is ever in Controlled mode at a time. Null whenever nobody on offense is controlled
    // (ball loose). Anything that needs to answer "is this the user's guy?" — highlight
    // rings, future UI, whatever — should read this, not re-derive its own notion of it.
    public static Transform ActivelyControlled { get; private set; }

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        inputBuffer = GetComponent<InputBuffer>();
        stateMachine = GetComponent<PlayerStateMachine>();
        tackleContact = GetComponent<TackleContact>();
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
        if (ActivelyControlled == transform) ActivelyControlled = null;
    }

    Mode DetermineMode()
    {
        if (BallController.Instance == null) return Mode.Frozen;

        // Fumble — nobody on offense is human-controlled until someone recovers it.
        // Same convention DefenderAI already uses for a loose ball (hold position,
        // don't invent pursuit behavior that doesn't exist yet).
        if (BallController.Instance.State == BallController.BallState.Loose)
            return Mode.Frozen;

        return BallController.Instance.Carrier == transform ? Mode.Controlled : Mode.AI;
    }

    void ApplyMode(Mode mode)
    {
        bool controlled = mode == Mode.Controlled;
        bool ai = mode == Mode.AI;

        if (playerMovement != null) playerMovement.enabled = controlled;
        if (inputBuffer != null) inputBuffer.enabled = controlled;
        if (stateMachine != null) stateMachine.enabled = controlled;
        if (tackleContact != null) tackleContact.enabled = controlled; // only the live carrier needs to check for being tackled
        if (receiverAI != null) receiverAI.enabled = ai;

        if (controlled) ActivelyControlled = transform;
        else if (ActivelyControlled == transform) ActivelyControlled = null; // don't clear a DIFFERENT object's claim if this instance never held it

        // Frozen: everything off. Object just sits wherever it is until this flips back
        // to Controlled or AI once someone recovers the ball.
    }
}