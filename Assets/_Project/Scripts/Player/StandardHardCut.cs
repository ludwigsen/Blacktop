using System;
using UnityEngine;

// A pose overlay on the existing Humanoid gait. Movement and gameplay contact stay on the actor root.
[Serializable]
public sealed class StandardHardCut
{
    [SerializeField, Min(0.1f)] float minimumApproachSpeed = 3.4f;
    [SerializeField, Range(25f, 100f)] float minimumTurnAngle = 52f;
    [SerializeField, Min(0.12f)] float steeringCutDuration = 0.32f;
    [SerializeField, Min(0f)] float repeatDelay = 0.14f;
    [SerializeField, Range(0.05f, 0.32f)] float pelvisDrop = 0.19f;
    [SerializeField, Range(0.1f, 0.4f)] float plantWidth = 0.23f;

    // First settle onto the outside foot, then release its contact into the exit stride.
    public const float PlantStart = 0.22f;
    public const float PlantEnd = 0.38f;
    const float MaximumTurnAngle = 145f;

    Transform actor;
    Transform hips;
    Transform chest;
    Transform leftArm;
    Transform rightArm;
    PlayerMovement movement;
    float localGroundY;
    Leg left;
    Leg right;
    Leg plant;
    Leg swing;
    Quaternion contactRotation;
    Vector3 approach;
    Vector3 departure;
    Vector3 outside;
    Vector3 headingVelocity;
    float progress;
    float cooldown;
    float turn;
    bool jukeActive;

    public Transform PlantFoot => plant?.Foot;
    public Vector3 PlantWorldPosition { get; private set; }
    public bool IsActive => plant != null;

    public void Bind(Animator animator, Transform playerRoot)
    {
        Cancel();
        actor = playerRoot;
        hips = null;
        if (actor == null || animator == null || !animator.isHuman) return;

        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
        leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        movement = actor.GetComponent<PlayerMovement>();
        var rig = animator.GetComponentInParent<PlayerVisualRig>();
        // Height profiles scale around this same visual pivot. Save it in gameplay-root space.
        localGroundY = rig != null
            ? actor.InverseTransformPoint(rig.transform.parent != null
                ? rig.transform.parent.TransformPoint(rig.footPivot)
                : actor.TransformPoint(rig.footPivot)).y
            : -1f;
        left = new Leg(animator, actor, localGroundY, true);
        right = new Leg(animator, actor, localGroundY, false);
    }

    internal void Observe(Vector3 displacement, float deltaTime, bool interrupted, PlayerStateMachine machine)
    {
        if (interrupted)
        {
            Cancel();
            return;
        }
        if (hips == null || deltaTime <= 0f) return;

        Vector3 velocity = displacement / deltaTime;
        if (machine != null && machine.TryGetJukePresentation(out Vector3 cutDirection, out float moveProgress))
        {
            if (!jukeActive)
            {
                // A committed juke chooses its side relative to the actor's facing at entry.
                CapturePlant(cutDirection, actor.forward, headingVelocity.magnitude);
            }
            jukeActive = true;
            // The committed juke travels quickly. Load and release the foot early in its existing curve.
            progress = moveProgress <= 0.30f
                ? Mathf.Lerp(0f, 0.50f, moveProgress / 0.30f)
                : Mathf.Lerp(0.50f, 1f, Mathf.InverseLerp(0.30f, 0.96f, moveProgress));
            if (progress >= 1f) StopPose();
        }
        else
        {
            if (jukeActive)
            {
                // An interrupted move drops the contact immediately. Normal completion has already recovered.
                StopPose();
                cooldown = repeatDelay;
                jukeActive = false;
            }

            cooldown = Mathf.Max(0f, cooldown - deltaTime);
            if (IsActive)
            {
                progress += deltaTime / steeringCutDuration;
                if (progress >= 1f)
                {
                    StopPose();
                    cooldown = repeatDelay;
                }
            }
            else if (cooldown <= 0f && headingVelocity.magnitude >= minimumApproachSpeed)
            {
                Vector3 desired = velocity;
                if (movement != null && movement.isActiveAndEnabled)
                {
                    // Read the same screen-relative intent as PlayerMovement before velocity finishes changing.
                    Vector2 input = movement.CurrentMoveInput;
                    if (input.sqrMagnitude > 0.12f) desired = CameraFollow.ScreenToWorld(new Vector3(input.x, 0f, input.y));
                }
                float angle = Mathf.Abs(Vector3.SignedAngle(headingVelocity, desired, Vector3.up));
                if (velocity.magnitude > minimumApproachSpeed * 0.65f && angle >= minimumTurnAngle && angle <= MaximumTurnAngle)
                    CapturePlant(desired, headingVelocity, headingVelocity.magnitude);
            }
        }

        // A short heading average detects sharp AI turns and avoids retriggering on individual stride samples.
        headingVelocity = Vector3.Lerp(headingVelocity, velocity, 1f - Mathf.Exp(-deltaTime / 0.085f));
    }

    public bool CapturePlant(Vector3 exitDirection, Vector3 entryDirection, float approachSpeed = 5f)
    {
        if (hips == null || !left.IsValid || !right.IsValid) return false;

        Vector3 entry = Vector3.ProjectOnPlane(entryDirection, Vector3.up).normalized;
        Vector3 exit = Vector3.ProjectOnPlane(exitDirection, Vector3.up).normalized;
        float angle = Vector3.SignedAngle(entry, exit, Vector3.up);
        if (entry == Vector3.zero || exit == Vector3.zero || Mathf.Abs(angle) < 20f || Mathf.Abs(angle) > MaximumTurnAngle)
            return false;

        approach = entry;
        departure = exit;
        turn = angle;
        bool cuttingRight = turn > 0f;
        plant = cuttingRight ? left : right;
        swing = cuttingRight ? right : left;
        outside = Vector3.Cross(approach, Vector3.up) * (cuttingRight ? 1f : -1f);

        float legLength = plant.Length;
        float advance = legLength * (0.16f + Mathf.Min(0.25f, approachSpeed * 0.045f));
        float stepWidth = legLength * plantWidth;
        PlantWorldPosition = hips.position + approach * advance + outside * stepWidth;
        PlantWorldPosition = new Vector3(PlantWorldPosition.x, GroundY + plant.AnkleHeight, PlantWorldPosition.z);
        // A small yaw into the departure keeps the shoe and knee aligned through load.
        Quaternion toeHeading = Quaternion.AngleAxis(turn * 0.19f, Vector3.up) * Quaternion.LookRotation(approach);
        contactRotation = toeHeading * plant.NeutralRotation;
        progress = 0f;
        return true;
    }

    internal void Apply(bool carrying)
    {
        if (IsActive) ApplyPose(progress, carrying);
    }

    // Preview and match both apply to a fresh Animator evaluation, allowing the same pose to blend into any gait.
    public void ApplyPose(float normalizedTime, bool carrying)
    {
        if (!IsActive) return;
        float t = Mathf.Clamp01(normalizedTime);
        float plantWeight = Ramp(0f, PlantStart, t) * (1f - Ramp(PlantEnd, 0.66f, t));
        float load = Ramp(0f, 0.26f, t) * (1f - Ramp(0.39f, 0.76f, t));
        float push = Ramp(0.31f, 0.52f, t) * (1f - Ramp(0.61f, 1f, t));
        float influence = Ramp(0f, 0.18f, t) * (1f - Ramp(0.58f, 1f, t));
        if (influence <= 0f) return;

        Vector3 plantAnkle = plant.Foot.position;
        Vector3 swingAnkle = swing.Foot.position;
        Quaternion plantRotation = plant.Foot.rotation;
        Quaternion swingRotation = swing.Foot.rotation;
        float legLength = plant.Length;
        float yaw = Vector3.SignedAngle(actor.forward, departure, Vector3.up);
        float counterYaw = Mathf.Clamp(turn, -90f, 90f) * 0.14f;
        float hipYaw = Mathf.Clamp(yaw, -38f, 38f) * influence * Ramp(0.10f, 0.53f, t);

        // Load over the outside hip, then drive the pelvis toward the open lane.
        hips.position += legLength * (outside * (0.10f * load) + departure * (0.12f * push) - Vector3.up * (pelvisDrop * load));
        hips.rotation = Quaternion.AngleAxis(hipYaw, Vector3.up) * hips.rotation;
        if (chest != null)
        {
            Vector3 driveRight = Vector3.Cross(Vector3.up, departure);
            Vector3 leanAxis = Vector3.Cross(Vector3.up, -outside);
            chest.rotation = Quaternion.AngleAxis(-counterYaw * load, Vector3.up) *
                Quaternion.AngleAxis(11f * load + 5f * push, driveRight) *
                Quaternion.AngleAxis(10f * push, leanAxis) * chest.rotation;
        }

        // The free leg unloads during contact, then takes a quick exit step. The same lengths and
        // knee plane solve both sides, including avatars scaled by the appearance height setting.
        Vector3 swingTarget = hips.position + (departure * (0.12f + 0.31f * push) - outside * 0.18f) * legLength;
        float swingHeight = GroundY + swing.AnkleHeight + legLength * (0.18f * load + 0.09f * push);
        swingTarget = new Vector3(swingTarget.x, swingHeight, swingTarget.z);
        Vector3 kneeHeading = Vector3.Slerp(approach, departure, 0.14f + 0.54f * push);
        plant.Place(Vector3.Lerp(plantAnkle, PlantWorldPosition, plantWeight),
            Quaternion.Slerp(plantRotation, contactRotation, plantWeight), kneeHeading, plantWeight);
        float swingWeight = Mathf.Max(load * 0.68f, push * 0.94f);
        swing.Place(Vector3.Lerp(swingAnkle, swingTarget, swingWeight), swingRotation, kneeHeading, swingWeight);

        // Keep the carry grip from the animator layer. The free arm counters the redirected hips.
        if (leftArm != null)
            leftArm.rotation = Quaternion.AngleAxis(Mathf.Sign(turn) * (15f * load - 22f * push),
                Vector3.Cross(Vector3.up, approach)) * leftArm.rotation;
        if (!carrying && rightArm != null)
            rightArm.rotation = Quaternion.AngleAxis(Mathf.Sign(turn) * (-15f * load + 22f * push),
                Vector3.Cross(Vector3.up, approach)) * rightArm.rotation;
    }

    public void Cancel()
    {
        headingVelocity = Vector3.zero;
        jukeActive = false;
        cooldown = 0f;
        StopPose();
    }

    float GroundY => actor.TransformPoint(0f, localGroundY, 0f).y;

    void StopPose()
    {
        plant = null;
        swing = null;
        progress = 0f;
    }

    static float Ramp(float start, float end, float time) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));

    sealed class Leg
    {
        readonly Transform thigh;
        readonly Transform knee;
        readonly float ankleHeightRatio;
        public Transform Foot { get; }
        public Quaternion NeutralRotation { get; }
        public bool IsValid => thigh != null && knee != null && Foot != null;
        public float Length => Vector3.Distance(thigh.position, knee.position) + Vector3.Distance(knee.position, Foot.position);
        public float AnkleHeight => ankleHeightRatio * Length;

        public Leg(Animator animator, Transform actor, float localGroundY, bool isLeft)
        {
            thigh = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            knee = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            Foot = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            if (!IsValid || Length < 0.01f) return;

            NeutralRotation = Quaternion.Inverse(actor.rotation) * Foot.rotation;
            float ground = actor.TransformPoint(0f, localGroundY, 0f).y;
            ankleHeightRatio = Mathf.Clamp((Foot.position.y - ground) / Length, 0.055f, 0.16f);
        }

        public void Place(Vector3 target, Quaternion footRotation, Vector3 bendForward, float bendWeight)
        {
            Vector3 hipPosition = thigh.position;
            float upper = Vector3.Distance(hipPosition, knee.position);
            float lower = Vector3.Distance(knee.position, Foot.position);
            Vector3 axis = (target - hipPosition).normalized;
            if (axis == Vector3.zero || upper < 0.01f || lower < 0.01f) return;

            // Solve the triangle with a small reach reserve to avoid a straight and unstable knee.
            float reachFraction = Mathf.Lerp(0.999f, 0.988f, bendWeight);
            float distance = Mathf.Clamp(Vector3.Distance(hipPosition, target), Mathf.Abs(upper - lower) + 0.01f, (upper + lower) * reachFraction);
            float along = (upper * upper + distance * distance - lower * lower) / (2f * distance);
            float forward = Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            Vector3 fromGait = Vector3.ProjectOnPlane(knee.position - hipPosition, axis).normalized;
            Vector3 towardCut = Vector3.ProjectOnPlane(bendForward, axis).normalized;
            Vector3 bend = Vector3.Slerp(fromGait, towardCut, bendWeight).normalized;
            if (bend == Vector3.zero) return;

            Vector3 kneeTarget = hipPosition + axis * along + bend * forward;
            Vector3 footTarget = hipPosition + axis * distance;
            thigh.rotation = Quaternion.FromToRotation(knee.position - hipPosition, kneeTarget - hipPosition) * thigh.rotation;
            knee.rotation = Quaternion.FromToRotation(Foot.position - knee.position, footTarget - knee.position) * knee.rotation;
            Foot.rotation = footRotation;
        }
    }
}
