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
    [SerializeField, Min(0.1f)] float walkSpeed = 2f;
    [SerializeField, Min(0.1f)] float jogSpeed = 4f;
    [SerializeField, Min(0.1f)] float standardRunSpeed = 6f;
    [SerializeField, Min(0.1f)] float fastRunSpeed = 8f;
    [SerializeField, Min(0.1f)] float sprintSpeed = 10f;
    [SerializeField, Min(0.1f)] float strafeSpeed = 4.5f;
    [SerializeField, Min(0.1f)] float backwardRunSpeed = 4.5f;
    [SerializeField, Min(0f)] float idleSpeed = 0.08f;
    [SerializeField, Range(-1f, 0f)] float backwardDotThreshold = -0.25f;
    [SerializeField, Min(0f)] float blendDamping = 0.08f;
    [SerializeField, Min(0.1f)] float teleportDistance = 3f;

    [Header("Wall recoil")]
    [SerializeField, Range(0f, 45f)] float stumbleLeanAngle = 12f;

    [Header("Football presentation")]
    [SerializeField, Min(0.01f)] float carryTuckSpeed = 3f;
    [SerializeField, Min(0.01f)] float footballBlendSeconds = 0.07f;
    [SerializeField, Min(0.01f)] float followThroughDuration = 0.31f;
    [SerializeField, Min(0.01f)] float handoffPoseDuration = 0.40f;
    [SerializeField, Range(0f, 90f)] float handoffTurnLimit = 65f;
    [SerializeField, Min(0.01f)] float throwStanceSpeed = 1.7f;
    [SerializeField] Vector3 footballGripOffset = DefaultFootballGripOffset;
    [SerializeField] StandardHardCut hardCut = new();

    public static readonly Vector3 DefaultFootballGripOffset = new(0.070f, 0.005f, 0.085f);
    public const float HandoffContactProgress = 0.30f;

    static readonly int Locomotion = Animator.StringToHash("Locomotion");
    static readonly int Lateral = Animator.StringToHash("Lateral");
    static readonly int LocomotionVariant = Animator.StringToHash("LocomotionVariant");
    static readonly int Stumbling = Animator.StringToHash("Stumbling");
    static readonly int StumbleProgress = Animator.StringToHash("StumbleProgress");
    static readonly int Hurdling = Animator.StringToHash("Hurdling");
    static readonly int HurdleRate = Animator.StringToHash("HurdleRate");
    static readonly int LocomotionRate = Animator.StringToHash("LocomotionRate");
    static readonly int CarryPace = Animator.StringToHash("CarryPace");
    static readonly int CarryTime = Animator.StringToHash("CarryTime");
    static readonly int WindupTime = Animator.StringToHash("ThrowWindupTime");
    static readonly int ActionTime = Animator.StringToHash("FootballActionTime");
    static readonly int Carry = Animator.StringToHash("Football.Carry");
    static readonly int ThrowWindup = Animator.StringToHash("Football.Throw Windup");
    static readonly int ThrowFollowThrough = Animator.StringToHash("Football.Throw Follow Through");
    static readonly int HandoffGive = Animator.StringToHash("Football.Handoff Give");
    static readonly int HandoffTake = Animator.StringToHash("Football.Handoff Take");

    PlayerStateMachine stateMachine;
    TeamMember teamMember;
    CPUCarrierAI cpuCarrier;
    PlayerVisualAssembler visualAssembler;
    Vector3 previousPosition;
    Transform recoilPivot;
    Quaternion uprightRotation;
    int footballLayer = -1;
    int footballState;
    int footballCue;
    float actionElapsed;
    Vector3 exchangePoint;
    int handoffApproachFrame = -1;
    int handoffApproachState;
    float handoffApproachProgress;
    Vector3 handoffApproachPoint;
    float poseProgress;
    float throwStanceWeight;

    public Animator Animator => animator;
    public float PlanarSpeed { get; private set; }
    public bool MovingBackward { get; private set; }

    void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
        teamMember = GetComponent<TeamMember>();
        cpuCarrier = GetComponent<CPUCarrierAI>();
        visualAssembler = GetComponent<PlayerVisualAssembler>();
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
        hardCut.Bind(animator, transform);
        ResetParameters(); // A fitted body swap also installs a fresh controller instance.
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

    void ClearFootballAction()
    {
        footballCue = 0;
        handoffApproachFrame = -1;
    }
    void OnFootballWhistle(PlayState.PlayEndReason _)
    {
        ClearFootballAction();
        hardCut.Cancel();
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
        // The root may keep facing upfield during a dropback or coverage shuffle.
        Vector3 direction = PlanarSpeed > idleSpeed ? transform.InverseTransformDirection(displacement.normalized) : Vector3.zero;
        MovingBackward = direction.z < backwardDotThreshold;
        bool stumbling = stateMachine != null && stateMachine.IsStumbling;
        if (recoilPivot != null)
        {
            Vector3 lean = stumbling ? transform.InverseTransformDirection(stateMachine.StumbleNormal) : Vector3.forward;
            float angle = stumbling ? stumbleLeanAngle * (1f - stateMachine.StumbleProgress) : 0f;
            recoilPivot.localRotation = Quaternion.AngleAxis(angle, Vector3.Cross(Vector3.up, lean)) * uprightRotation;
        }
        if (animator == null || animator.runtimeAnimatorController == null) return;

        // Sample the one-shot at the existing move's progress. On exit, keep its
        // last sample during the crossfade so it cannot snap to its opening pose.
        animator.SetBool(Stumbling, stumbling);
        if (stumbling) animator.SetFloat(StumbleProgress, stateMachine.StumbleProgress);

        bool hurdling = stateMachine != null && stateMachine.CurrentState == PlayerState.Hurdle;
        animator.SetBool(Hurdling, hurdling);
        if (hurdling)
        {
            // Fit the source clip to the existing move duration, without changing move tuning.
            float length = hurdleClip != null ? hurdleClip.length : 1f;
            animator.SetFloat(HurdleRate, length / Mathf.Max(0.01f, stateMachine.HurdleDuration));
        }

        UpdateFootball(deltaTime, stumbling || hurdling, teleported);
        var play = PlayState.Instance;
        bool footballAction = footballState != 0 && footballState != Carry;
        bool otherMove = stateMachine.CurrentState is PlayerState.StiffArm or PlayerState.Pitch;
        hardCut.Observe(displacement, deltaTime, teleported || stumbling || hurdling ||
            (play != null && !play.IsLive) || footballAction || otherMove, stateMachine);

        // Formation teleports and the wall's recoil are not locomotion strides.
        if (stumbling || teleported || deltaTime <= 0f)
        {
            animator.SetFloat(Locomotion, 0f);
            animator.SetFloat(Lateral, 0f);
            animator.SetFloat(LocomotionRate, 1f);
            return;
        }

        float forwardBlend = MovingBackward
            ? -Mathf.Clamp01(-direction.z * PlanarSpeed / backwardRunSpeed)
            : ForwardBlend(Mathf.Max(0f, direction.z) * PlanarSpeed);
        animator.SetFloat(Locomotion, forwardBlend, blendDamping, deltaTime);
        animator.SetFloat(Lateral, Mathf.Clamp(direction.x * PlanarSpeed / strafeSpeed, -1f, 1f), blendDamping, deltaTime);
        float referenceSpeed = Mathf.Lerp(MovingBackward ? backwardRunSpeed : sprintSpeed, strafeSpeed, Mathf.Abs(direction.x));
        animator.SetFloat(LocomotionRate, Mathf.Clamp(PlanarSpeed / referenceSpeed, 1f, 1.5f));
    }

    float ForwardBlend(float speed)
    {
        // Keep the appearance previews' signed idle/run/fast-run input values.
        return Mathf.InverseLerp(idleSpeed, walkSpeed, speed) / 3f +
            Mathf.InverseLerp(walkSpeed, jogSpeed, speed) / 3f +
            Mathf.InverseLerp(jogSpeed, standardRunSpeed, speed) / 3f +
            Mathf.InverseLerp(standardRunSpeed, fastRunSpeed, speed) +
            Mathf.InverseLerp(fastRunSpeed, sprintSpeed, speed);
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
        handoffApproachFrame = -1;
    }

    internal void ShowHandoffApproach(bool giving, Vector3 contactPoint, float progress)
    {
        // A coordinator that stops reporting the approach also stops the cue.
        handoffApproachFrame = Time.frameCount;
        handoffApproachState = giving ? HandoffGive : HandoffTake;
        handoffApproachPoint = contactPoint;
        handoffApproachProgress = Mathf.Clamp01(progress);
    }

    internal bool TryGetBallGrip(out Pose pose)
    {
        pose = default;
        return isActiveAndEnabled && footballLayer >= 0 && TrySampleFootballGrip(animator, footballGripOffset, out pose);
    }

    public static bool TrySampleFootballGrip(Animator animator, Vector3 gripOffset, out Pose pose)
    {
        pose = default;
        if (animator == null || !animator.isHuman) return false;
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        if (hand == null || middle == null || index == null || little == null) return false;

        // Use the anatomical palm frame. The FootballVisual mesh is long on
        // local Z, which is also the forward/spiral axis during gameplay flight.
        // Only the contact location on the hand scales with the avatar. The
        // inflated ball keeps its real palm-to-center distance at every height.
        Vector3 metacarpal = middle.position - hand.position;
        Vector3 fingers = metacarpal.normalized;
        Vector3 thumbSide = Vector3.ProjectOnPlane(index.position - little.position, fingers).normalized;
        Vector3 palm = Vector3.Cross(fingers, thumbSide).normalized;
        if (palm.sqrMagnitude < 0.001f) return false;
        Quaternion rotation = Quaternion.LookRotation(Vector3.Cross(palm, fingers), -palm);
        float handRatio = metacarpal.magnitude / 0.09f;
        Vector3 offset = (fingers * gripOffset.x + thumbSide * gripOffset.y) * handRatio + palm * gripOffset.z;
        pose = new Pose(hand.position + offset, rotation);
        return true;
    }

    void UpdateFootball(float deltaTime, bool interrupted, bool teleported)
    {
        if (footballLayer < 0) return;
        var play = PlayState.Instance;
        var ball = BallController.Instance;
        bool heldHere = ball != null && ball.IsHeld && ball.Carrier == transform;
        bool live = play == null || play.IsLive;
        if (heldHere)
        {
            // Match the free arm swing to the same gait clock as the legs,
            // including the runner's first strides out of the handoff.
            animator.SetFloat(CarryPace, live ? Mathf.Clamp01(PlanarSpeed / carryTuckSpeed) : 0f,
                blendDamping, deltaTime);
            animator.SetFloat(CarryTime, Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f));
        }

        // Whistle, a formation reset and a higher-priority move clear one-shots.
        // Losing the ball also cancels a receiving pose or a pending windup.
        if (footballCue != 0 && (!live || teleported || interrupted || (footballCue == HandoffTake) != heldHere))
            ClearFootballAction();

        int next = 0;
        float progress = 0f;
        bool commitAtContact = false;
        if (footballCue != 0)
        {
            float duration = footballCue == ThrowFollowThrough ? followThroughDuration : handoffPoseDuration;
            progress = Mathf.Clamp01(actionElapsed / duration);
            if (progress < 1f)
            {
                next = footballCue;
                commitAtContact = actionElapsed <= 0f && (next != ThrowFollowThrough || footballState == ThrowWindup);
                if (next != ThrowFollowThrough) progress = Mathf.Lerp(HandoffContactProgress, 1f, progress);
                actionElapsed += Mathf.Max(0f, deltaTime);
            }
            else footballCue = 0;
        }

        float windup = live && heldHere ? stateMachine.PassWindup : -1f;
        if (live && heldHere && windup < 0f && cpuCarrier != null && cpuCarrier.isActiveAndEnabled)
            windup = cpuCarrier.VisualWindup;
        if (next == 0 && windup < 0f && handoffApproachFrame == Time.frameCount && !interrupted && !teleported &&
            (handoffApproachState == HandoffGive) == heldHere)
        {
            next = handoffApproachState;
            progress = HandoffContactProgress * handoffApproachProgress;
            exchangePoint = handoffApproachPoint;
        }

        if (next == 0 && heldHere && !interrupted)
        {
            next = windup >= 0f ? ThrowWindup : Carry;
            progress = Mathf.Max(0f, windup);
        }

        if (next == ThrowWindup && footballState != ThrowWindup)
            throwStanceWeight = 1f - Mathf.Clamp01(PlanarSpeed / throwStanceSpeed);
        float weight = animator.GetLayerWeight(footballLayer);
        if (commitAtContact)
        {
            // Evaluate the contact/release key this frame. The final windup
            // and first follow-through share the same throwing arm pose.
            animator.Play(next, footballLayer, 0f);
            weight = 1f;
        }
        else if (next != 0 && next != footballState)
        {
            if (weight <= 0.01f) animator.Play(next, footballLayer, 0f);
            else animator.CrossFadeInFixedTime(next, 0.045f, footballLayer);
        }

        // Keep the outgoing release or handoff pose sampled during its crossfade.
        if (next == ThrowWindup) animator.SetFloat(WindupTime, progress);
        else if (next != 0 && next != Carry) animator.SetFloat(ActionTime, progress);
        poseProgress = progress;
        animator.SetLayerWeight(footballLayer, Mathf.MoveTowards(weight, next == 0 ? 0f : 1f,
            Mathf.Max(0f, deltaTime) / footballBlendSeconds));
        // Preserve the last sample during the no-ball fade.
        if (next != 0 || animator.GetLayerWeight(footballLayer) == 0f) footballState = next;
    }

    void LateUpdate()
    {
        bool carrying = BallController.Instance is { IsHeld: true } ball && ball.Carrier == transform;
        hardCut.Apply(carrying);
        if (footballLayer < 0) return;

        float weight = animator.GetLayerWeight(footballLayer);
        float carryWeight = footballState == Carry ? weight : 0f;
        if (footballState == ThrowWindup)
            carryWeight = weight * (1f - Mathf.SmoothStep(0f, 1f, poseProgress / 0.28f));
        if (footballCue == HandoffTake)
            carryWeight = weight * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 1f, poseProgress));
        if (footballState == HandoffGive && footballCue == 0 && handoffApproachFrame == Time.frameCount)
            carryWeight = weight * (1f - Mathf.SmoothStep(0f, 1f, handoffApproachProgress));
        if (carrying && carryWeight > 0.001f)
            FootballActionPose.ApplyCarry(animator, animator.GetFloat(CarryPace), footballGripOffset,
                visualAssembler?.AppliedAppearance?.bodyArchetype ?? BodyArchetype.Athletic, carryWeight);
        if (footballState == ThrowWindup || footballCue == ThrowFollowThrough)
        {
            float windup = footballState == ThrowWindup ? poseProgress : -1f;
            float followThrough = footballCue == ThrowFollowThrough ? poseProgress : -1f;
            FootballActionPose.ApplyThrowStance(animator, transform, windup, followThrough, throwStanceWeight * weight);
            FootballActionPose.ApplyOverhandArm(animator, transform, windup, followThrough, footballGripOffset, weight);
        }
        if (footballState != HandoffGive && footballState != HandoffTake) return;
        if (footballCue == 0 && handoffApproachFrame != Time.frameCount) return;

        // The reach peaks at the same frame on both players. Visual corrections
        // then decay into the already-playing recovery and carry poses.
        float reach = footballCue == 0
            ? 0.72f * Mathf.SmoothStep(0f, 1f, handoffApproachProgress)
            : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HandoffContactProgress, 0.73f, poseProgress));
        FootballActionPose.ApplyHandoff(animator, transform, exchangePoint, reach * weight,
            footballState == HandoffGive, footballGripOffset, handoffTurnLimit);
    }

    void ResetParameters()
    {
        if (recoilPivot != null) recoilPivot.localRotation = uprightRotation;
        PlanarSpeed = 0f;
        MovingBackward = false;
        hardCut.Cancel();
        ClearFootballAction();
        footballState = 0;
        actionElapsed = 0f;
        throwStanceWeight = 0f;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        if (footballLayer >= 0)
        {
            animator.SetLayerWeight(footballLayer, 0f);
            animator.SetFloat(CarryPace, 0f);
            animator.SetFloat(CarryTime, 0f);
            animator.SetFloat(ActionTime, 0f);
            animator.SetFloat(WindupTime, 0f);
        }
        animator.SetFloat(Locomotion, 0f);
        animator.SetFloat(Lateral, 0f);
        // Pick a stable idle and walk pair at spawn so the stance does not change mid-stride.
        animator.SetFloat(LocomotionVariant, teamMember != null && teamMember.teamId == 1 ? 1f : 0f);
        animator.SetBool(Stumbling, false);
        animator.SetFloat(StumbleProgress, 0f);
        animator.SetBool(Hurdling, false);
        animator.SetFloat(HurdleRate, 1f);
        animator.SetFloat(LocomotionRate, 1f);
    }
}
