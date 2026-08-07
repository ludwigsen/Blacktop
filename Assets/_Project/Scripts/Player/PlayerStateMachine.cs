using System.Linq;
using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    [SerializeField] JukeMove jukeMove;
    [SerializeField] HurdleMove hurdleMove;
    [SerializeField] StiffArmMove stiffArmMove;
    [SerializeField] PlayerAttributes attributes;

    PlayerMovement movement;
    InputBuffer inputBuffer;
    PlayerContext ctx;
    PlayerState currentState = PlayerState.Idle;
    IPlayerMove activeMove;
    float cooldownTimer;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        inputBuffer = GetComponent<InputBuffer>();
        var defenderDetector = GetComponent<DefenderDetector>();

        ctx = new PlayerContext
        {
            transform = transform,
            attributes = attributes,
            inputBuffer = inputBuffer,
            getMoveInput = () => movement.CurrentMoveInput,
            isDefenderInRange = () => defenderDetector.DefenderInRange,
            setCooldown = t => cooldownTimer = t
        };

        // On tackle, the state machine stops processing entirely — no locomotion, no move
        // triggers. Deliberately NOT disabling the whole component, just gating Update(),
        // so re-enabling on ResetPlay() later is a one-line flip, not a re-Awake.
        if (PlayState.Instance != null)
            PlayState.Instance.OnPlayEnded += HandlePlayEnded;
    }

    void HandlePlayEnded()
    {
        activeMove = null; // cut any in-progress move short — you don't finish a juke after being tackled
        currentState = PlayerState.Idle;
    }

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return; // play's dead — no input processed

        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (activeMove != null)
        {
            activeMove.Tick(ctx, Time.deltaTime);
            if (activeMove.IsComplete)
            {
                activeMove.Exit(ctx);
                activeMove = null;
                currentState = PlayerState.Run;
            }
            return;
        }

        HandleLocomotion();
        CheckMoveTriggers();
    }

    void HandleLocomotion()
    {
        float mag = movement.CurrentInputMagnitude;
        currentState = mag < 0.05f ? PlayerState.Idle
                      : mag < 0.3f ? PlayerState.Walk
                      : PlayerState.Run;
    }

    void CheckMoveTriggers()
    {
        if (cooldownTimer > 0f) return;

        var candidates = new (string action, IPlayerMove move)[]
        {
            ("Juke", jukeMove),
            ("Hurdle", hurdleMove),
            ("StiffArm", stiffArmMove)
        };

        string action = inputBuffer.PeekEarliestValid(candidates.Select(c => c.action).ToArray());
        if (action == null) return;

        var move = candidates.First(c => c.action == action).move;
        if (!move.CanTrigger(ctx, currentState)) return;

        inputBuffer.TryConsume(action);
        activeMove = move;
        activeMove.Enter(ctx);
        currentState = action switch
        {
            "Juke" => PlayerState.Juke,
            "Hurdle" => PlayerState.Hurdle,
            "StiffArm" => PlayerState.StiffArm,
            _ => currentState
        };
    }
}