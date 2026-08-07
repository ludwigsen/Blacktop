using UnityEngine;
using UnityEngine.InputSystem;

// Transform-based arcade locomotion — deliberately NOT Rigidbody/physics-driven.
// Gives direct control over velocity curves for that snappy "arcade" feel rather
// than letting a physics solver interpret input. Rigidbody-based knockback/ragdoll
// is a separate system layered on top later, not the base movement.
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] PlayerAttributes attributes; // per-player stat multipliers (speed/agility/etc)

    // Base values are tuned during solo playtesting with a "balanced" player (all multipliers = 1.0).
    // Actual per-player speed is base * attribute multiplier — never hardcode absolute
    // values per archetype, or tuning work gets thrown away when stats are added later.
    [SerializeField] float baseMaxSpeed = 8f;
    [SerializeField] float baseAcceleration = 40f;
    [SerializeField] float deceleration = 60f; // intentionally NOT attribute-scaled — stopping feel stays consistent across players
    [SerializeField] float rotationSpeed = 720f; // deg/sec — test diagonal-to-diagonal flicks specifically when tuning this

    InputSystem_Actions controls; // auto-generated wrapper from the project-wide Input Actions asset
    Vector2 moveInput;
    Vector3 currentVelocity;

    // Multipliers applied here rather than baked into serialized fields, so PlayerAttributes
    // can be swapped at runtime/per-prefab without touching these base tuning values.
    float MaxSpeed => baseMaxSpeed * attributes.speedMult;
    float Acceleration => baseAcceleration * attributes.accelMult;

    // Exposed for PlayerStateMachine to read — drives Idle/Walk/Run thresholds without
    // the state machine needing its own input polling.
    public float CurrentInputMagnitude => moveInput.magnitude;

    // Exposed so moves (via PlayerContext.getMoveInput) can read raw direction — e.g. Juke
    // needs to know which way the stick was tilted at trigger time. Keeps all input reading
    // routed through PlayerMovement rather than moves polling Input.* directly (legacy or new).
    public Vector2 CurrentMoveInput => moveInput;

    void Awake()
    {
        controls = new InputSystem_Actions();
    }

    void OnEnable()
    {
        controls.Player.Enable();
        // performed/canceled callbacks over polling — avoids missing fast taps between Update() calls
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;
    }

    void OnDisable() => controls.Player.Disable();

    void Update()
    {
        Vector3 moveDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;
        Vector3 targetVelocity = moveDir * MaxSpeed;

        // Asymmetric accel/decel is the core of "arcade feel" — stopping should read as
        // slightly snappier than starting, or the whole thing feels like ice skating.
        float rate = moveDir.magnitude > 0.1f ? Acceleration : deceleration;
        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * Time.deltaTime);

        transform.position += currentVelocity * Time.deltaTime;

        // Only rotate when there's meaningful input — prevents jittery snapping to a
        // "forward" direction when input magnitude is near zero (dead zone noise).
        if (moveDir.magnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }
}