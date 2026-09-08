using System.Linq;
using UnityEngine;
[RequireComponent(typeof(PlayerMovement))]

// Orchestrator only — holds current state and delegates to the active IPlayerMove.
// Deliberately does NOT contain move implementation details; that's what IPlayerMove
// abstracts away. This class should stay thin even as more moves get added.
[RequireComponent(typeof(DefenderDetector))]
[RequireComponent(typeof(InputBuffer))]
public class PlayerStateMachine : MonoBehaviour
{
    [SerializeField] JukeMove jukeMove;
    [SerializeField] HurdleMove hurdleMove;
    [SerializeField] StiffArmMove stiffArmMove;
    [SerializeField] PitchMove pitchMove;
    [SerializeField] PassMove passMove;
    [SerializeField] PlayerAttributes attributes;

    PlayerMovement movement;
    InputBuffer inputBuffer;
    PlayerContext ctx;
    PlayerState currentState = PlayerState.Idle;
    IPlayerMove activeMove;
    float cooldownTimer;

    // Set by HurdleMove via ctx.setTackleImmune during a successful negation roll.
    // TackleContact reads this directly (via GetComponent<PlayerStateMachine>()) before
    // ending the play on contact — a hurdle that "won" its roll passes through a tackle
    // attempt untouched for the remainder of the move.
    public bool IsTackleImmune { get; private set; }

    // Exposed for visual feedback (and any other read-only observer) — currentState
    // itself stays private so only this class can set it.
    public PlayerState CurrentState => currentState;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        inputBuffer = GetComponent<InputBuffer>();  // back to local, matches PossessionController's enable/disable target
        var defenderDetector = GetComponent<DefenderDetector>();

        ctx = new PlayerContext
        {
            transform = transform,
            attributes = attributes,
            inputBuffer = inputBuffer,
            getMoveInput = () => movement.CurrentMoveInput,
            isDefenderInRange = () => defenderDetector != null && defenderDetector.DefenderInRange,
            setTackleImmune = v => IsTackleImmune = v,
            setCooldown = t => cooldownTimer = t,
            addOffensePoints = pts => PlayState.Instance?.AddOffensePoints(pts),
        };

        if (PlayState.Instance != null)
            PlayState.Instance.OnPlayEnded += HandlePlayEnded;
    }

    // Signature matches Action<PlayEndReason> — reason isn't used yet, but having it
    // available means future logic (different freeze behavior for touchdown vs tackle,
    // celebration state, etc.) doesn't require another signature change later.
    void HandlePlayEnded(PlayState.PlayEndReason reason)
    {
        activeMove = null;
        currentState = PlayerState.Idle;
    }

    void OnDestroy()
    {
        if (PlayState.Instance != null)
            PlayState.Instance.OnPlayEnded -= HandlePlayEnded;
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
                currentState = PlayerState.Run; // moves always return to Run — locomotion re-evaluates next frame anyway
            }
            return; // input locked out entirely while a move is active — no steering during committed moves
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
        ("StiffArm", stiffArmMove),
        ("Pitch", pitchMove),
        ("Pass", passMove)
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
            "Pitch" => PlayerState.Pitch,
            "Pass" => PlayerState.Pass,
            _ => currentState
        };
    }
}
