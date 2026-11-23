using UnityEngine;

// Applies small rig-space corrections after Humanoid evaluation. Shared by the
// live presenter and the editor preview, with no dependency on gameplay state.
public static class FootballActionPose
{
    public static void ApplyCarry(Animator animator, float pace, Vector3 gripOffset,
        BodyArchetype build = BodyArchetype.Athletic, float weight = 1f)
    {
        if (weight <= 0f || animator == null || !animator.isHuman) return;
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        if (hips == null || rightShoulder == null || rightElbow == null || rightHand == null || leftShoulder == null)
            return;

        Vector3 shoulders = (rightShoulder.position + leftShoulder.position) * 0.5f;
        Vector3 up = (shoulders - hips.position).normalized;
        Vector3 right = Vector3.ProjectOnPlane(rightShoulder.position - leftShoulder.position, up).normalized;
        Vector3 forward = Vector3.Cross(right, up);
        float armLength = Vector3.Distance(rightShoulder.position, rightElbow.position) +
            Vector3.Distance(rightElbow.position, rightHand.position);
        float scale = armLength / 0.66f;

        // On the move, secure the football along the inside of the forearm
        // against the upper ribs. The larger tops need more front clearance.
        float chestDepth = build switch
        {
            BodyArchetype.Slim => 0.20f,
            BodyArchetype.Stocky or BodyArchetype.Muscular => 0.24f,
            BodyArchetype.Power => 0.26f,
            BodyArchetype.Massive => 0.28f,
            _ => 0.22f
        };
        Vector3 center = shoulders + (right * Mathf.Lerp(0.06f, 0.17f, pace) -
            up * Mathf.Lerp(0.19f, 0.22f, pace) + forward * (chestDepth + Mathf.Lerp(0.07f, 0.01f, pace))) * scale;
        Vector3 palm = Vector3.Slerp(up - forward * 0.4f, -forward - right * 0.38f + up * 0.12f, pace);
        Vector3 longAxis = Vector3.Slerp(-right + up * 0.17f, up * 0.66f - right * 0.65f + forward * 0.25f, pace);
        Quaternion desiredGrip = GripRotation(palm, longAxis);
        if (!PlayerAnimationPresenter.TrySampleFootballGrip(animator, gripOffset, out var grip)) return;
        Quaternion handTurn = desiredGrip * Quaternion.Inverse(grip.rotation);
        rightHand.rotation = Quaternion.Slerp(Quaternion.identity, handTurn, weight) * rightHand.rotation;
        PlayerAnimationPresenter.TrySampleFootballGrip(animator, gripOffset, out grip);
        SolveLimb(rightShoulder, rightElbow, rightHand,
            rightHand.position + (center - grip.position) * weight, right * 0.60f - up - forward * 0.18f, true);

        // The non-carrying arm supports the far tip only in the ready stance.
        // Fade it back into the free arm swing as soon as the player moves.
        float support = weight * (1f - pace);
        if (support <= 0.01f) return;
        Transform leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (leftElbow == null || leftHand == null) return;
        Vector3 supportWrist = center - right * 0.185f - up * 0.065f - forward * 0.04f;
        SolveLimb(leftShoulder, leftElbow, leftHand, Vector3.Lerp(leftHand.position, supportWrist, support),
            -right * 0.65f - up, true);
    }

    public static Vector3 GetHandoffContact(Animator giver, Animator receiver, Transform giverRoot, Transform receiverRoot)
    {
        Vector3 center = (giverRoot.position + receiverRoot.position) * 0.5f;
        if (giver == null || receiver == null || !giver.isHuman || !receiver.isHuman) return center;

        Transform giverShoulder = giver.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform receiverShoulder = receiver.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (giverShoulder == null || receiverShoulder == null) return center;

        // Weight the exchange toward the shorter reach and place it below the
        // shoulders. Blend toward the shoulder midpoint to account for facing.
        float giverScale = giver.transform.lossyScale.x;
        float receiverScale = receiver.transform.lossyScale.x;
        float bias = giverScale / Mathf.Max(0.01f, giverScale + receiverScale);
        Vector3 shoulders = Vector3.Lerp(giverShoulder.position, receiverShoulder.position, bias);
        center = Vector3.Lerp(Vector3.Lerp(giverRoot.position, receiverRoot.position, bias), shoulders, 0.55f);
        center.y = shoulders.y - 0.27f * (giverScale + receiverScale) * 0.5f;
        return center;
    }

    public static void ApplyHandoff(Animator animator, Transform actor, Vector3 contactPoint, float weight, bool giving,
        Vector3 gripOffset, float turnLimit = 65f)
    {
        if (weight <= 0f || animator == null || !animator.isHuman) return;
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (chest == null || shoulder == null || elbow == null || hand == null) return;

        // Rotate the torso toward the live mesh point, then use the actual
        // upper/lower arm lengths to fit each player's hand to the same ball.
        Vector3 aim = Vector3.ProjectOnPlane(contactPoint - actor.position, Vector3.up);
        if (aim.sqrMagnitude > 0.001f)
        {
            aim.Normalize();
            Vector3 authoredDirection = giving ? actor.right : actor.forward;
            float turn = Vector3.SignedAngle(authoredDirection, aim, Vector3.up);
            Quaternion yaw = Quaternion.AngleAxis(Mathf.Clamp(turn * 0.75f, -turnLimit, turnLimit) * weight, Vector3.up);
            Quaternion lean = Quaternion.AngleAxis(13f * weight, Vector3.Cross(Vector3.up, aim));
            chest.rotation = lean * yaw * chest.rotation;
        }

        if (!PlayerAnimationPresenter.TrySampleFootballGrip(animator, gripOffset, out var grip)) return;
        // The QB reaches under the ball. The runner presents the opposite
        // side of the pocket. Orient their wrists along the exchange so the
        // hands can make contact with the ball before either reaches lockout.
        if (aim.sqrMagnitude > 0.001f)
        {
            Vector3 palm = aim * (giving ? 0.78f : 0.85f) + Vector3.up * (giving ? 0.65f : 0.44f);
            Quaternion facing = GripRotation(palm, aim);
            Quaternion correction = facing * Quaternion.Inverse(grip.rotation);
            hand.rotation = Quaternion.Slerp(Quaternion.identity, correction, weight * 0.72f) * hand.rotation;
            PlayerAnimationPresenter.TrySampleFootballGrip(animator, gripOffset, out grip);
        }
        Vector3 target = hand.position + contactPoint - grip.position;
        SolveLimb(shoulder, elbow, hand, Vector3.Lerp(hand.position, target, weight), actor.right - Vector3.up);

        if (!giving)
        {
            // Raise the opposite forearm under the exchange, leaving room for
            // the ball between the two hands and for the authored tuck afterward.
            Transform leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (leftShoulder == null || leftElbow == null || leftHand == null) return;
            float scale = animator.transform.lossyScale.x;
            Vector3 pocket = contactPoint - (actor.right * 0.13f + Vector3.up * 0.09f) * scale;
            SolveLimb(leftShoulder, leftElbow, leftHand, Vector3.Lerp(leftHand.position, pocket, weight * 0.78f),
                -actor.right - Vector3.up);
        }
    }

    public static void ApplyThrowStance(Animator animator, Transform actor, float windup, float followThrough, float weight = 1f)
    {
        if (weight <= 0f || animator == null || !animator.isHuman || (windup < 0f && followThrough < 0f)) return;
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform upperLeft = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform lowerLeft = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform upperRight = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform lowerRight = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (hips == null || upperLeft == null || lowerLeft == null || leftFoot == null ||
            upperRight == null || lowerRight == null || rightFoot == null) return;

        // Load over the right leg first, then rotate the pelvis and move weight
        // onto the left leg as the throwing arm accelerates. Keep the two ankles
        // at their sampled base positions while the hips articulate above them.
        Vector3 leftPosition = leftFoot.position;
        Vector3 rightPosition = rightFoot.position;
        Quaternion leftRotation = leftFoot.rotation;
        Quaternion rightRotation = rightFoot.rotation;
        float load = windup >= 0f ? Mathf.SmoothStep(0f, 1f, windup / 0.60f) : 1f;
        float transfer = windup >= 0f ? Mathf.SmoothStep(0f, 1f, (windup - 0.60f) / 0.40f) : 1f;
        float recovery = followThrough >= 0f ? 1f - Mathf.SmoothStep(0f, 1f, followThrough) : 1f;
        float scale = animator.transform.lossyScale.x * weight * recovery;
        hips.position += (actor.right * ((0.075f * load) - (0.145f * transfer)) +
            actor.forward * (0.095f * transfer) - Vector3.up * (0.045f * load)) * scale;
        hips.rotation = Quaternion.AngleAxis((17f * load - 19f * transfer) * weight * recovery,
            Vector3.up) * hips.rotation;
        SolveLimb(upperLeft, lowerLeft, leftFoot, leftPosition, actor.forward);
        SolveLimb(upperRight, lowerRight, rightFoot, rightPosition, actor.forward);
        leftFoot.rotation = leftRotation;
        rightFoot.rotation = rightRotation;
    }

    public static void ApplyOverhandArm(Animator animator, Transform actor, float windup, float followThrough,
        Vector3 gripOffset, float weight = 1f)
    {
        if (weight <= 0f || animator == null || !animator.isHuman || (windup < 0f && followThrough < 0f)) return;
        Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (shoulder == null || elbow == null || hand == null ||
            !PlayerAnimationPresenter.TrySampleFootballGrip(animator, gripOffset, out var grip)) return;

        // Guide the short muscle-curve arc through a true overhand pocket.
        // A loaded throw is beside/behind the shoulder before it comes through
        // high and in front. Retarget using the live arm lengths at each height.
        float t = windup >= 0f ? windup : 1f;
        float acceleration = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.59f, 1f, t));
        float influence = weight * (windup >= 0f
            ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.58f, t))
            : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.34f, followThrough)));
        if (influence <= 0.001f) return;

        Vector3 right = actor.right;
        Vector3 forward = actor.forward;
        Vector3 up = Vector3.up;
        float armLength = Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, hand.position);
        Vector3 load = right * 0.18f + up * 0.34f - forward * 0.14f;
        Vector3 release = right * 0.04f + up * 0.45f + forward * 0.35f;
        Vector3 target = shoulder.position + Vector3.Lerp(load, release, acceleration) * (armLength / 0.66f);
        Vector3 palm = Vector3.Slerp(forward * 0.75f + up * 0.5f + right * 0.24f,
            up * 0.63f + forward * 0.72f - right * 0.22f, acceleration);
        Quaternion gripFacing = GripRotation(palm, forward);
        Quaternion handTurn = gripFacing * Quaternion.Inverse(grip.rotation);
        hand.rotation = Quaternion.Slerp(Quaternion.identity, handTurn, influence * 0.82f) * hand.rotation;
        SolveLimb(shoulder, elbow, hand, Vector3.Lerp(hand.position, target, influence),
            right * 0.65f - up * 0.65f - forward * 0.30f, true);
    }

    // FootballVisual points along local Z, with its negative Y against the palm.
    static Quaternion GripRotation(Vector3 palm, Vector3 longAxis) =>
        Quaternion.LookRotation(Vector3.ProjectOnPlane(longAxis, palm), -palm);

    static void SolveLimb(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 preferredBend,
        bool preferBend = false)
    {
        Vector3 origin = upper.position;
        Vector3 toTarget = target - origin;
        float upperLength = Vector3.Distance(origin, lower.position);
        float lowerLength = Vector3.Distance(lower.position, end.position);
        if (toTarget.sqrMagnitude < 0.000001f || upperLength < 0.001f || lowerLength < 0.001f) return;

        // Respect both bone lengths and the current bend plane. Keep a degree
        // of flexion at full reach so the elbow/knee cannot flip at lockout.
        Vector3 axis = toTarget.normalized;
        float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f,
            (upperLength + lowerLength) * 0.985f);
        Vector3 bend = Vector3.ProjectOnPlane(lower.position - origin, axis);
        Vector3 authoredBend = Vector3.ProjectOnPlane(preferredBend, axis);
        if (preferBend || bend.sqrMagnitude < 0.0001f) bend = authoredBend;
        if (bend.sqrMagnitude < 0.000001f) return;
        bend.Normalize();
        float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
        float across = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        Vector3 desired = origin + axis * along + bend * across;
        Quaternion rotation = end.rotation;
        upper.rotation = Quaternion.FromToRotation(lower.position - origin, desired - origin) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position, origin + axis * distance - lower.position) *
            lower.rotation;
        end.rotation = rotation;
    }
}
