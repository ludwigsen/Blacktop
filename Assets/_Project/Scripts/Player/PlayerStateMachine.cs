using System.Collections.Generic;
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
    [SerializeField] StumbleMove stumbleMove = new();

    PlayerMovement movement;
    InputBuffer inputBuffer;
    PlayerContext ctx;
    PlayerState currentState = PlayerState.Idle;
    IPlayerMove activeMove;
    float cooldownTimer;

    CapsuleCollider body;
    FieldBounds fieldBounds;
    readonly List<FieldBoundaryWall> latchedWalls = new();
    int stumbleFrame = -1;

    public bool IsStumbling => currentState == PlayerState.Stumble;
    public float StumbleProgress => IsStumbling ? stumbleMove.Progress : 0f;
    public Vector3 StumbleNormal { get; private set; }
    bool BlocksLocomotion => IsStumbling || Time.frameCount == stumbleFrame;

    // Set by HurdleMove via ctx.setTackleImmune during a successful negation roll.
    // TackleContact reads this directly (via GetComponent<PlayerStateMachine>()) before
    // ending the play on contact — a hurdle that "won" its roll passes through a tackle
    // attempt untouched for the remainder of the move.
    public bool IsTackleImmune { get; private set; }

    // Exposed for visual feedback (and any other read-only observer) — currentState
    // itself stays private so only this class can set it.
    public PlayerState CurrentState => currentState;
    // Read-only timing for the shared animation presenter, including move cancellation.
    public float HurdleDuration => hurdleMove.Duration;
    public void SetAttributes(PlayerAttributes newAttributes) => attributes = newAttributes;
    // Exposed so BallController can read a receiver's Catching (and any future stat)
    // without every consumer needing its own duplicate serialized attributes reference.
    public PlayerAttributes Attributes => attributes;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        body = GetComponent<CapsuleCollider>();
        fieldBounds = FindAnyObjectByType<FieldBounds>();
        inputBuffer = GetComponent<InputBuffer>();  // back to local, matches PossessionController's enable/disable target
        var defenderDetector = GetComponent<DefenderDetector>();

        ctx = new PlayerContext
        {
            transform = transform,
            attributes = attributes,
            inputBuffer = inputBuffer,
            moveBy = delta => MoveTo(transform.position + delta),
            moveStumble = MoveStumble,
            getMoveInput = () => movement.CurrentMoveInput,
            isDefenderInRange = () => defenderDetector != null && defenderDetector.DefenderInRange,
            setTackleImmune = v => IsTackleImmune = v,
            setCooldown = t => cooldownTimer = t,
            addOffensePoints = pts => PlayState.Instance?.AddTeamPoints(GetComponent<TeamMember>().teamId, pts * (attributes != null ? attributes.Swagger() : 1f)),
        };
    }

    void Start()
    {
        // Start runs after PlayState's Awake. AI and human players both keep
        // this machine enabled, even as possession toggles their input scripts.
        if (PlayState.Instance == null) return;
        PlayState.Instance.OnPlayEnded += HandlePlayEnded;
        PlayState.Instance.OnPlayReset += ResetMoves;
    }

    void HandlePlayEnded(PlayState.PlayEndReason reason) => ResetMoves();
    void OnDisable() => ResetMoves();

    void ResetMoves()
    {
        CancelActiveMove();
        latchedWalls.Clear();
        stumbleFrame = -1;
    }

    // Possession still cancels input moves immediately, but a wall impact stays
    // with this actor even if it no longer carries the ball.
    internal void CancelCarrierMove()
    {
        if (!IsStumbling && (activeMove != null || currentState != PlayerState.Idle)) CancelActiveMove();
    }

    void CancelActiveMove()
    {
        // Exit is successful completion (Pass/Pitch release there). An interruption
        // only cleans up temporary effects and must never launch a post-whistle ball.
        activeMove?.Cancel(ctx);
        activeMove = null;
        IsTackleImmune = false;
        cooldownTimer = 0f;
        currentState = PlayerState.Idle;
    }

    void OnDestroy()
    {
        if (PlayState.Instance == null) return;
        PlayState.Instance.OnPlayEnded -= HandlePlayEnded;
        PlayState.Instance.OnPlayReset -= ResetMoves;
    }

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        // An impact belongs to the actor, even if control or ball ownership changes
        // mid-move. Do not tick on its entry frame or resume locomotion on its exit frame.
        if (IsStumbling)
        {
            if (Time.frameCount != stumbleFrame)
            {
                TickMove(Time.deltaTime);
                if (!IsStumbling) stumbleFrame = Time.frameCount;
            }
            return;
        }

        // A recovery may change carriers before PossessionController toggles this
        // component. Cancel stale moves before they can tick or release another ball.
        if (BallController.Instance == null || BallController.Instance.Carrier != transform ||
            PossessionController.ActivelyControlled != transform)
        {
            CancelCarrierMove();
            return;
        }

        if (stumbleFrame == Time.frameCount) return;
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (activeMove != null)
        {
            TickMove(Time.deltaTime);
            return; // input locked out entirely while a move is active — no steering during committed moves
        }

        HandleLocomotion();
        CheckMoveTriggers();
    }

    void TickMove(float deltaTime)
    {
        IPlayerMove move = activeMove;
        move.Tick(ctx, deltaTime);
        // A sweep inside a Juke/Hurdle/StiffArm tick may already have replaced it.
        if (activeMove != move || !move.IsComplete) return;
        move.Exit(ctx);
        activeMove = null;
        currentState = PlayerState.Run;
    }

    // Existing movement owners still choose destinations. This path only arbitrates
    // wall contact and recoil, and leaves actors without a gameplay capsule alone.
    internal static bool IsMovementLocked(Transform actor) =>
        actor.TryGetComponent(out PlayerStateMachine machine) && machine.BlocksLocomotion;

    internal static bool MoveActor(Transform actor, Vector3 destination)
    {
        if (actor.TryGetComponent(out PlayerStateMachine machine)) return machine.MoveTo(destination);
        actor.position = destination;
        return true;
    }

    internal bool MoveTo(Vector3 destination)
    {
        if (BlocksLocomotion) return false;
        if (fieldBounds == null && body != null) fieldBounds = FindAnyObjectByType<FieldBounds>();
        if (!FieldWallContact.Sweep(fieldBounds, body, destination, out Vector3 stop, out var wall, out Vector3 normal))
        {
            transform.position = destination;
            ReleaseSeparatedWalls();
            return true;
        }

        // A terminal ball position takes precedence over pulling the holder away
        // from a wall. The existing sideline/score pollers still own the whistle.
        var play = PlayState.Instance;
        bool live = play == null || play.IsLive;
        if (live && HasPendingRuling(play)) return false;

        transform.position = stop;
        if (!live || !isActiveAndEnabled || HasPendingRuling(play)) return false;
        ReleaseSeparatedWalls();

        if (latchedWalls.Contains(wall))
        {
            // Continued diagonal input may slide along the wall, but still
            // sweeps against the next face at a corner or at the end of a segment.
            Vector3 slide = Vector3.ProjectOnPlane(destination - stop, normal);
            slide.y = 0f;
            bool blocked = FieldWallContact.Sweep(fieldBounds, body, stop + slide, out stop, out wall, out normal);
            transform.position = stop;
            ReleaseSeparatedWalls();
            // A clear slide preserves locomotion momentum along the wall. A
            // straight push into it or a blocked corner still stops the mover.
            if (!blocked) return slide.sqrMagnitude > 0.000001f;
            if (latchedWalls.Contains(wall) || HasPendingRuling(play)) return false;
        }

        if (!stumbleMove.CanTrigger(ctx, currentState)) return false;
        CancelActiveMove();
        latchedWalls.Add(wall);
        StumbleNormal = normal;
        stumbleFrame = Time.frameCount;
        stumbleMove.SetContactNormal(normal);
        activeMove = stumbleMove;
        activeMove.Enter(ctx);
        currentState = PlayerState.Stumble;
        return false;
    }

    void ReleaseSeparatedWalls()
    {
        // Only voluntary movement can rearm contact. Keep a small clearance
        // beyond the recoil endpoint so holding into the wall cannot retrigger.
        float clearance = stumbleMove.Distance + FieldWallContact.Skin * 2f;
        for (int i = latchedWalls.Count - 1; i >= 0; i--)
        {
            FieldBoundaryWall wall = latchedWalls[i];
            if (wall == null || !FieldWallContact.IsNear(fieldBounds, body, wall, clearance)) latchedWalls.RemoveAt(i);
        }
    }

    bool HasPendingRuling(PlayState play) => play != null && BallController.Instance != null &&
        BallController.Instance.Carrier == transform &&
        (play.IsBallOutOfBounds(fieldBounds) || play.TryGetTouchdownTeam(out _, fieldBounds));

    void MoveStumble(Vector3 delta)
    {
        if (HasPendingRuling(PlayState.Instance)) return;
        FieldWallContact.Sweep(fieldBounds, body, transform.position + delta, out Vector3 stop, out _, out _);
        transform.position = stop;
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
