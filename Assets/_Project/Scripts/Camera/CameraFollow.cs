using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target; // fallback if no ball exists (e.g. testing without BallController in scene)
    [SerializeField] Vector3 offset = new Vector3(0f, 9f, -7f);
    [SerializeField] float followSpeed = 8f;

    // Offset is authored for a team attacking +Z (camera behind the ball holder). When the
    // ball changes hands and the holder attacks the other way, the camera swings around to
    // stay behind them. Orbiting a yaw angle (instead of just lerping the position) matters:
    // a straight lerp would pass directly over the target and LookAt would go vertical.
    [Tooltip("Degrees per second the camera orbits after the ball changes hands. 180 = a one-second swing.")]
    [SerializeField] float orbitSpeed = 180f;

    // The camera's CURRENT yaw, published so stick input can be made relative to what is
    // actually on screen (including mid-swing) instead of guessing from game state. Movement
    // scripts go through ScreenToWorld; with no CameraFollow in the scene it's 0, i.e. plain
    // world axes — the original behavior.
    public static float ControlYaw { get; private set; }
    public static Vector3 ScreenToWorld(Vector3 screenSpace) => Quaternion.Euler(0f, ControlYaw, 0f) * screenSpace;

    float currentYaw;
    bool yawInitialized;

    void OnDestroy() => ControlYaw = 0f;

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

        // Behind whoever holds the ball (PlayState.ViewDirection), live — so a turnover
        // swings the camera around the moment the ball changes teams.
        float targetYaw = PlayState.Instance != null ? PlayState.Instance.ViewDirection.Yaw : 0f;
        if (!yawInitialized)
        {
            currentYaw = targetYaw; // first frame: start already behind the ball holder, no opening swing
            yawInitialized = true;
        }
        else
        {
            currentYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, orbitSpeed * Time.deltaTime);
        }
        ControlYaw = currentYaw;

        Vector3 orbitedOffset = Quaternion.Euler(0f, currentYaw, 0f) * offset;
        Vector3 desiredPosition = followTarget.position + orbitedOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(followTarget.position + Vector3.up * 1f);
    }
}
