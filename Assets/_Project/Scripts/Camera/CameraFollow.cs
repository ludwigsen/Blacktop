using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target; // fallback if no ball exists (e.g. testing without BallController in scene)
    [SerializeField] Vector3 offset = new Vector3(0f, 9f, -7f);
    [SerializeField] float followSpeed = 8f;

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

        Vector3 desiredPosition = followTarget.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(followTarget.position + Vector3.up * 1f);
    }
}