using UnityEngine;

// Read-only presentation for every roster player, regardless of the movement owner.
// Sample after the normal movement Updates; Animator never drives the gameplay root.
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerAnimationPresenter : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] AnimationClip hurdleClip;
    [Header("Animation reference speeds (world units per second)")]
    [SerializeField, Min(0.1f)] float standardRunSpeed = 6f;
    [SerializeField, Min(0.1f)] float fastRunSpeed = 10f;
    [SerializeField, Min(0.1f)] float backwardRunSpeed = 4.5f;
    [SerializeField, Min(0f)] float idleSpeed = 0.08f;
    [SerializeField, Range(-1f, 0f)] float backwardDotThreshold = -0.25f;
    [SerializeField, Min(0f)] float blendDamping = 0.08f;
    [SerializeField, Min(0.1f)] float teleportDistance = 3f;

    [Header("Wall recoil")]
    [SerializeField, Range(0f, 45f)] float stumbleLeanAngle = 24f;

    static readonly int Locomotion = Animator.StringToHash("Locomotion");
    static readonly int Hurdling = Animator.StringToHash("Hurdling");
    static readonly int HurdleRate = Animator.StringToHash("HurdleRate");
    static readonly int LocomotionRate = Animator.StringToHash("LocomotionRate");
    PlayerStateMachine stateMachine;
    Vector3 previousPosition;
    Transform recoilPivot;
    Quaternion uprightRotation;

    public Animator Animator => animator;
    public float PlanarSpeed { get; private set; }
    public bool MovingBackward { get; private set; }

    void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            // Use the wrapper above the Animator so neither the gameplay root nor
            // the Animator's own local pose is overwritten.
            Transform pivot = animator.transform;
            while (pivot.parent != null && pivot.parent != transform) pivot = pivot.parent;
            if (pivot.parent == transform && pivot != animator.transform)
            {
                recoilPivot = pivot;
                uprightRotation = pivot.localRotation;
            }
            // Offscreen AI must retain the same animation state as controlled players.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    void OnEnable()
    {
        previousPosition = transform.position;
        ResetParameters();
    }

    void OnDisable() => ResetParameters();
    void Update() => UpdatePresentation(Time.deltaTime);

    void UpdatePresentation(float deltaTime)
    {
        Vector3 displacement = transform.position - previousPosition;
        previousPosition = transform.position;
        displacement.y = 0f; // The hurdle's vertical arc is gameplay, not running speed.
        bool teleported = displacement.magnitude > teleportDistance;
        PlanarSpeed = deltaTime > 0f && !teleported ? displacement.magnitude / deltaTime : 0f;
        MovingBackward = PlanarSpeed > idleSpeed &&
            Vector3.Dot(displacement.normalized, transform.forward) < backwardDotThreshold;
        bool stumbling = stateMachine != null && stateMachine.IsStumbling;
        if (recoilPivot != null)
        {
            Vector3 lean = stumbling ? transform.InverseTransformDirection(stateMachine.StumbleNormal) : Vector3.forward;
            float angle = stumbling ? stumbleLeanAngle * (1f - stateMachine.StumbleProgress) : 0f;
            recoilPivot.localRotation = Quaternion.AngleAxis(angle, Vector3.Cross(Vector3.up, lean)) * uprightRotation;
        }
        if (animator == null || animator.runtimeAnimatorController == null) return;

        bool hurdling = stateMachine != null && stateMachine.CurrentState == PlayerState.Hurdle;
        animator.SetBool(Hurdling, hurdling);
        if (hurdling)
        {
            // Fit the source clip to the existing move duration, without changing move tuning.
            float length = hurdleClip != null ? hurdleClip.length : 1f;
            animator.SetFloat(HurdleRate, length / Mathf.Max(0.01f, stateMachine.HurdleDuration));
        }

        float blend = 0f;
        if (PlanarSpeed > idleSpeed)
        {
            if (MovingBackward) blend = -Mathf.Clamp01(PlanarSpeed / backwardRunSpeed);
            else if (PlanarSpeed <= standardRunSpeed) blend = PlanarSpeed / standardRunSpeed;
            else blend = 1f + Mathf.InverseLerp(standardRunSpeed, Mathf.Max(standardRunSpeed + 0.1f, fastRunSpeed), PlanarSpeed);
        }
        // Formation/reset teleports must not produce a one-frame sprint or reverse stride.
        if (stumbling || teleported || deltaTime <= 0f) animator.SetFloat(Locomotion, 0f);
        else animator.SetFloat(Locomotion, blend, blendDamping, deltaTime);
        float referenceSpeed = MovingBackward ? backwardRunSpeed : fastRunSpeed;
        animator.SetFloat(LocomotionRate, Mathf.Clamp(PlanarSpeed / referenceSpeed, 1f, 1.5f));
    }

    void ResetParameters()
    {
        if (recoilPivot != null) recoilPivot.localRotation = uprightRotation;
        PlanarSpeed = 0f;
        MovingBackward = false;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetFloat(Locomotion, 0f);
        animator.SetBool(Hurdling, false);
        animator.SetFloat(HurdleRate, 1f);
        animator.SetFloat(LocomotionRate, 1f);
    }
}
