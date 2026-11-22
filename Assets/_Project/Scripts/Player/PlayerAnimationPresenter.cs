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

    [Header("Football presentation")]
    [SerializeField, Min(0.01f)] float carryTuckSpeed = 3f;
    [SerializeField, Min(0.01f)] float footballBlendSeconds = 0.07f;
    [SerializeField, Min(0.01f)] float followThroughDuration = 0.31f;
    [SerializeField, Min(0.01f)] float handoffPoseDuration = 0.40f;
    [SerializeField, Range(0f, 90f)] float handoffTurnLimit = 65f;
    [SerializeField] Vector3 footballGripOffset = new(0.085f, 0.005f, 0.075f);

    static readonly int Locomotion = Animator.StringToHash("Locomotion");
    static readonly int Hurdling = Animator.StringToHash("Hurdling");
    static readonly int HurdleRate = Animator.StringToHash("HurdleRate");
    static readonly int LocomotionRate = Animator.StringToHash("LocomotionRate");
    static readonly int CarryPace = Animator.StringToHash("CarryPace");
    static readonly int WindupTime = Animator.StringToHash("ThrowWindupTime");
    static readonly int ActionTime = Animator.StringToHash("FootballActionTime");
    static readonly int Carry = Animator.StringToHash("Football.Carry");
    static readonly int ThrowWindup = Animator.StringToHash("Football.Throw Windup");
    static readonly int ThrowFollowThrough = Animator.StringToHash("Football.Throw Follow Through");
    static readonly int HandoffGive = Animator.StringToHash("Football.Handoff Give");
    static readonly int HandoffTake = Animator.StringToHash("Football.Handoff Take");

    PlayerStateMachine stateMachine;
    Vector3 previousPosition;
    Transform recoilPivot;
    Quaternion uprightRotation;

    int footballLayer = -1;
    int footballState;
    int footballCue;
    float actionElapsed;
    Vector3 exchangePoint;
    Transform gripHand;
    Transform gripMiddle;
    Transform gripIndex;
    Transform gripLittle;
    Transform throwingArm;
    Transform upperChest;

    public Animator Animator => animator;
    public float PlanarSpeed { get; private set; }
    public bool MovingBackward { get; private set; }

    void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        BindVisual(animator);
    }

    public void BindVisual(Animator visual)
    {
        // Refresh hand, chest and recoil references whenever the fitted Avatar changes.
        animator = visual;
        if (animator != null)
        {
            footballState = 0;
            animator.applyRootMotion = false;
            footballLayer = animator.GetLayerIndex("Football");
            if (animator.isHuman)
            {
                gripHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                gripMiddle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                gripIndex = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                gripLittle = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                throwingArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            }
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

    void Start()
    {
        // All scene Awakes have completed, so PlayState is available here.
        if (PlayState.Instance == null) return;
        PlayState.Instance.OnPlayReset += ClearFootballAction;
        PlayState.Instance.OnPlayEnded += OnFootballWhistle;
    }

    void OnDestroy()
    {
        if (PlayState.Instance == null) return;
        PlayState.Instance.OnPlayReset -= ClearFootballAction;
        PlayState.Instance.OnPlayEnded -= OnFootballWhistle;
    }

    void ClearFootballAction() => footballCue = 0;
    void OnFootballWhistle(PlayState.PlayEndReason _) => ClearFootballAction();
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

        // The football mask supplies the upper body over the existing locomotion pose.
        UpdateFootball(deltaTime, stumbling || hurdling, teleported);

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

    internal void ShowPassRelease()
    {
        footballCue = ThrowFollowThrough;
        actionElapsed = 0f;
    }

    internal void ShowHandoff(bool giving, Vector3 contactPoint)
    {
        footballCue = giving ? HandoffGive : HandoffTake;
        actionElapsed = 0f;
        exchangePoint = contactPoint;
    }

    internal bool TryGetBallGrip(out Pose pose)
    {
        pose = default;
        if (!isActiveAndEnabled || footballLayer < 0 || gripHand == null || gripMiddle == null ||
            gripIndex == null || gripLittle == null) return false;

        // Derive a palm frame from the metacarpals so it follows wrist roll and
        // avatar scale. Local Y is the ball's long axis, laid across the hand.
        Vector3 fingers = (gripMiddle.position - gripHand.position).normalized;
        Vector3 thumbSide = (gripIndex.position - gripLittle.position).normalized;
        Vector3 palm = Vector3.Cross(fingers, thumbSide).normalized;
        float handScale = gripHand.lossyScale.x;
        Quaternion rotation = Quaternion.LookRotation(palm, Vector3.Cross(palm, fingers));
        Vector3 offset = (fingers * footballGripOffset.x + thumbSide * footballGripOffset.y +
            palm * footballGripOffset.z) * handScale;
        pose = new Pose(gripHand.position + offset, rotation);
        return true;
    }

    void UpdateFootball(float deltaTime, bool interrupted, bool teleported)
    {
        if (footballLayer < 0) return;
        var play = PlayState.Instance;
        var ball = BallController.Instance;
        bool heldHere = ball != null && ball.IsHeld && ball.Carrier == transform;
        bool live = play == null || play.IsLive;

        // Whistle, a formation reset and a higher-priority move clear one-shots.
        // Losing the ball also cancels a receiving pose or a pending windup.
        if (footballCue != 0 && (!live || teleported || interrupted || (footballCue == HandoffTake) != heldHere))
            ClearFootballAction();

        int next = 0;
        float progress = 0f;
        if (footballCue != 0)
        {
            float duration = footballCue == ThrowFollowThrough ? followThroughDuration : handoffPoseDuration;
            progress = Mathf.Clamp01(actionElapsed / duration);
            if (progress < 1f)
            {
                next = footballCue;
                actionElapsed += Mathf.Max(0f, deltaTime);
            }
            else footballCue = 0;
        }

        if (next == 0 && heldHere && !interrupted)
        {
            float windup = live ? stateMachine.PassWindup : -1f;
            next = windup >= 0f ? ThrowWindup : Carry;
            progress = Mathf.Max(0f, windup);
            // A new snap keeps the QB's ready grip until their feet move.
            animator.SetFloat(CarryPace, live ? Mathf.Clamp01(PlanarSpeed / carryTuckSpeed) : 0f,
                blendDamping, deltaTime);
        }

        float weight = animator.GetLayerWeight(footballLayer);
        if (next != 0 && next != footballState)
        {
            if (weight <= 0.01f) animator.Play(next, footballLayer, 0f);
            else animator.CrossFadeInFixedTime(next, 0.045f, footballLayer);
        }

        // Keep the outgoing release or handoff pose sampled during its crossfade.
        if (next == ThrowWindup) animator.SetFloat(WindupTime, progress);
        else if (next != 0 && next != Carry) animator.SetFloat(ActionTime, progress);
        animator.SetLayerWeight(footballLayer, Mathf.MoveTowards(weight, next == 0 ? 0f : 1f,
            Mathf.Max(0f, deltaTime) / footballBlendSeconds));
        // Preserve the last sample during the no-ball fade.
        if (next != 0 || animator.GetLayerWeight(footballLayer) == 0f) footballState = next;
    }

    void LateUpdate()
    {
        if (footballCue != HandoffGive && footballCue != HandoffTake) return;
        if (gripHand == null || throwingArm == null) return;

        // Shotgun and Pistol put the runner on different sides. Turn the
        // animated reach toward the real mesh point without steering the actor.
        Vector3 aim = Vector3.ProjectOnPlane(exchangePoint - transform.position, Vector3.up);
        if (aim.sqrMagnitude < 0.001f) return;
        float progress = animator.GetFloat(ActionTime);
        float reach = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.26f, 0.72f, progress));
        Vector3 authoredDirection = footballCue == HandoffGive ? transform.right : transform.forward;
        float turn = Vector3.SignedAngle(authoredDirection, aim, Vector3.up);
        if (upperChest != null)
            upperChest.rotation = Quaternion.AngleAxis(Mathf.Clamp(turn * 0.6f, -handoffTurnLimit, handoffTurnLimit) * reach,
                Vector3.up) * upperChest.rotation;

        Vector3 target = exchangePoint;
        target.y = gripHand.position.y;
        Quaternion correction = Quaternion.FromToRotation(gripHand.position - throwingArm.position, target - throwingArm.position);
        throwingArm.rotation = Quaternion.RotateTowards(Quaternion.identity, correction, handoffTurnLimit * reach) *
            throwingArm.rotation;
    }

    void ResetParameters()
    {
        if (recoilPivot != null) recoilPivot.localRotation = uprightRotation;
        PlanarSpeed = 0f;
        MovingBackward = false;
        footballState = footballCue = 0;
        actionElapsed = 0f;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        if (footballLayer >= 0)
        {
            animator.SetLayerWeight(footballLayer, 0f);
            animator.SetFloat(CarryPace, 0f);
            animator.SetFloat(ActionTime, 0f);
            animator.SetFloat(WindupTime, 0f);
        }
        animator.SetFloat(Locomotion, 0f);
        animator.SetBool(Hurdling, false);
        animator.SetFloat(HurdleRate, 1f);
        animator.SetFloat(LocomotionRate, 1f);
    }
}
