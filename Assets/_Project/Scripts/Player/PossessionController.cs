using UnityEngine;

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
public class PossessionController : MonoBehaviour
{
    PlayerMovement playerMovement;
    InputBuffer inputBuffer;
    PlayerStateMachine stateMachine;
    TackleContact tackleContact;
    ReceiverAI receiverAI;

    enum Mode { Controlled, AI, Frozen }
    Mode currentMode;

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        inputBuffer = GetComponent<InputBuffer>();
        stateMachine = GetComponent<PlayerStateMachine>();
        tackleContact = GetComponent<TackleContact>();
        receiverAI = GetComponent<ReceiverAI>();
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

        // Frozen: everything off. Object just sits wherever it is until this flips back
        // to Controlled or AI once someone recovers the ball.
    }
}