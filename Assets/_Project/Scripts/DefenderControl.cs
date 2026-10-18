using UnityEngine;
using UnityEngine.InputSystem;

// Human locomotion for a defender. Deliberately tiny: stick -> velocity, nothing else.
// Tackling stays proximity-based (the carrier's TackleContact already treats any
// other-team player in range as a tackle), so "controlling a defender" is just getting him
// to the ball. Moves (dive, hit-stick) are a later layer, same IPlayerMove idea as offense.
//
// Added at runtime by DefenderControlManager the first time a defender is selected, and
// enabled ONLY while that defender is the human's — so the other six carry a disabled
// component and cost nothing. Tuning lives on the manager (it's the one in the scene, so
// it stays editable and live-tunable in Play mode); this component only reads it.
//
// Mirrors PlayerMovement's velocity model on purpose (asymmetric accel/decel) so a
// defender feels like the same kind of athlete as the ball carrier.
[RequireComponent(typeof(DefenderAI))]
public class DefenderControl : MonoBehaviour
{
    DefenderAI ai;
    InputSystem_Actions controls;
    Vector2 moveInput;
    Vector3 velocity;

    void Awake()
    {
        ai = GetComponent<DefenderAI>();
        controls = new InputSystem_Actions();
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += _ => moveInput = Vector2.zero;
    }

    void OnEnable() => controls.Player.Enable();

    void OnDisable()
    {
        controls.Player.Disable();
        moveInput = Vector2.zero;
        velocity = Vector3.zero;
    }

    void OnDestroy() => controls?.Dispose();

    // Single entry point for "the human is (no longer) driving this guy." Keeps the four
    // things that must change together — AI hands-off, input on/off, and the shared
    // "who is the user piloting" authority — in one place so they can't drift apart.
    public void SetControlled(bool controlled)
    {
        // Releasing an already-released defender must not erase a newer carrier claim
        // on the same player (recovery and manager updates can happen in either order).
        bool wasControlled = ai.IsUserControlled;
        ai.SetUserControlled(controlled);
        enabled = controlled;

        if (controlled && TryGetComponent<TeamMember>(out var member))
            PossessionController.ClaimControl(transform, member.teamId);
        else if (wasControlled)
            PossessionController.ReleaseControl(transform);
    }

    void Update()
    {
        var play = PlayState.Instance;

        // PlayState's canned presentation owns actor transforms after the whistle.
        if (play != null && !play.IsLive) { velocity = Vector3.zero; return; }

        // A live recovery can occur before the manager's next Update. Stop the old
        // defender locomotion immediately, even if its component is still enabled.
        if (play != null && !play.IsUserDefending) { velocity = Vector3.zero; return; }
        if (PossessionController.ActivelyControlled != transform) { velocity = Vector3.zero; return; }

        // Stiff-armed / shed: DefenderAI is animating the shove, so input is locked out.
        if (ai.IsStunned) { velocity = Vector3.zero; return; }

        var tuning = DefenderControlManager.Instance;
        float maxSpeed = (tuning != null ? tuning.MaxSpeed : 7f) * ai.SpeedMult;
        float accel = tuning != null ? tuning.Acceleration : 40f;
        float decel = tuning != null ? tuning.Deceleration : 60f;
        float turnRate = tuning != null ? tuning.RotationSpeed : 720f;

        // Stick is screen-relative, same as the carrier: up = up on screen, however the
        // camera is currently oriented.
        Vector3 moveDir = CameraFollow.ScreenToWorld(new Vector3(moveInput.x, 0f, moveInput.y).normalized);

        Vector3 targetVelocity = moveDir * maxSpeed;
        float rate = moveDir.magnitude > 0.1f ? accel : decel;
        velocity = Vector3.MoveTowards(velocity, targetVelocity, rate * Time.deltaTime);
        transform.position += velocity * Time.deltaTime;

        if (moveDir.magnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnRate * Time.deltaTime);
        }
    }
}
