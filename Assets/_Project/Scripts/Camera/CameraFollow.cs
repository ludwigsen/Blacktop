using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target; // fallback if no ball exists (e.g. testing without BallController in scene)
    [SerializeField] Vector3 offset = new Vector3(0f, 9f, -7f);
    [SerializeField] float followSpeed = 8f;

    // Offset is authored for a team attacking +Z (camera behind the offense). When
    // possession flips the attack direction, the camera swings around to stay behind the
    // new offense. Orbiting a yaw angle (instead of just lerping the position) matters:
    // a straight lerp would pass directly over the target and LookAt would go vertical.
    [Tooltip("Degrees per second the camera orbits after a possession change. 180 = a one-second swing.")]
    [SerializeField] float orbitSpeed = 180f;

    float currentYaw;
    bool yawInitialized;

    void LateUpdate()
    {
        // Follow the ball carrier directly while possessed — avoids tracking
        // BallController's derived (and rotation-swinging) position when we can just
        // follow the source transform instead. Only fall back to the ball's own
        // transform when it's loose (fumbled, no carrier) — there's no player to
        // follow at that point, the ball IS the subject.
        Transform followTarget = target;
        if (BallController.Instance != null)
        {
            followTarget = BallController.Instance.IsHeld
                ? BallController.Instance.Carrier
                : BallController.Instance.transform;
        }

        if (followTarget == null) return;

        float targetYaw = PlayState.Instance != null ? PlayState.Instance.AttackDirection.Yaw : 0f;
        if (!yawInitialized)
        {
            currentYaw = targetYaw; // first frame: start already behind the offense, no opening swing
            yawInitialized = true;
        }
        else
        {
            currentYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, orbitSpeed * Time.deltaTime);
        }

        Vector3 orbitedOffset = Quaternion.Euler(0f, currentYaw, 0f) * offset;
        Vector3 desiredPosition = followTarget.position + orbitedOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(followTarget.position + Vector3.up * 1f);
    }
}