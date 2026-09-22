using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target; // fallback if no ball exists (e.g. testing without BallController in scene)
    [SerializeField] Vector3 offset = new Vector3(0f, 9f, -7f);
    [SerializeField] float followSpeed = 8f;

    // Offset is authored for a team attacking +Z (camera behind the ball holder). When the
    // ball changes hands and the holder attacks the other way, the camera orbits around to
    // stay behind them. Orbiting a yaw angle (instead of just lerping the position) matters:
    // a straight lerp would pass directly over the target and LookAt would go vertical.
    [Header("Turnover Swing")]
    [Tooltip("Seconds for a full 180-degree swing. A shorter swing (e.g. a reversal partway through) scales down proportionally.")]
    [SerializeField] float orbitDuration = 0.9f;

    [Tooltip("Cubic-bezier easing as (x1, y1, x2, y2) — same control points CSS uses. Default is a symmetric ease-in-out: slow start, fast middle, slow settle.")]
    [SerializeField] Vector4 easeBezier = new Vector4(0.65f, 0f, 0.35f, 1f);

    // The camera's CURRENT yaw, published so stick input can be made relative to what is
    // actually on screen (including mid-swing) instead of guessing from game state. Movement
    // scripts go through ScreenToWorld; with no CameraFollow in the scene it's 0, i.e. plain
    // world axes — the original behavior.
    public static float ControlYaw { get; private set; }
    public static Vector3 ScreenToWorld(Vector3 screenSpace) => Quaternion.Euler(0f, ControlYaw, 0f) * screenSpace;

    float currentYaw;
    bool yawInitialized;

    // Swing tween state. Time-based (not speed-based) so the ease curve shapes the whole
    // move and it always lands exactly on the target yaw.
    float swingFrom, swingTo, swingElapsed, swingDuration;
    bool swinging;

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
        // starts the swing the moment the ball changes teams.
        float targetYaw = PlayState.Instance != null ? PlayState.Instance.ViewDirection.Yaw : 0f;
        UpdateYaw(targetYaw);
        ControlYaw = currentYaw;

        Vector3 orbitedOffset = Quaternion.Euler(0f, currentYaw, 0f) * offset;
        Vector3 desiredPosition = followTarget.position + orbitedOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(followTarget.position + Vector3.up * 1f);
    }

    void UpdateYaw(float targetYaw)
    {
        if (!yawInitialized)
        {
            currentYaw = targetYaw; // first frame: start already behind the ball holder, no opening swing
            swingTo = targetYaw;
            yawInitialized = true;
            return;
        }

        // Target changed (turnover, or a reversal mid-swing): start a new swing from wherever
        // the camera is right now, by the shortest way around. Duration scales with the angle
        // left to cover so a mid-swing reversal doesn't crawl.
        if (Mathf.Abs(Mathf.DeltaAngle(swingTo, targetYaw)) > 0.01f)
        {
            float delta = Mathf.DeltaAngle(currentYaw, targetYaw);
            swingFrom = currentYaw;
            swingTo = currentYaw + delta;
            swingElapsed = 0f;
            swingDuration = orbitDuration * Mathf.Abs(delta) / 180f;
            swinging = swingDuration > 0.0001f;
            if (!swinging) currentYaw = targetYaw;
        }

        if (!swinging) return;

        swingElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(swingElapsed / swingDuration);
        currentYaw = swingFrom + (swingTo - swingFrom) * CubicBezierEase(t, easeBezier.x, easeBezier.y, easeBezier.z, easeBezier.w);

        if (t >= 1f)
        {
            currentYaw = swingTo;
            swinging = false;
        }
    }

    // Standard CSS-style cubic-bezier easing: curve from (0,0) to (1,1) with control points
    // (x1,y1) and (x2,y2). The curve is parametric, so given progress x (= time) we first
    // solve for the curve parameter s where Bx(s) = x, then return By(s).
    static float CubicBezierEase(float x, float x1, float y1, float x2, float y2)
    {
        if (x <= 0f) return 0f;
        if (x >= 1f) return 1f;

        // Newton's method converges in a few steps for well-behaved curves...
        float s = x;
        for (int i = 0; i < 6; i++)
        {
            float err = BezierAxis(s, x1, x2) - x;
            if (Mathf.Abs(err) < 0.0001f) return BezierAxis(s, y1, y2);
            float slope = BezierAxisSlope(s, x1, x2);
            if (Mathf.Abs(slope) < 0.0001f) break; // flat spot — Newton would blow up, bisect instead
            s -= err / slope;
        }

        // ...bisection as the safety net (control x values outside 0-1 can make Newton wander).
        float lo = 0f, hi = 1f;
        s = x;
        for (int i = 0; i < 20; i++)
        {
            float bx = BezierAxis(s, x1, x2);
            if (Mathf.Abs(bx - x) < 0.0001f) break;
            if (bx < x) lo = s; else hi = s;
            s = (lo + hi) * 0.5f;
        }
        return BezierAxis(s, y1, y2);
    }

    // One axis of a cubic bezier with endpoints 0 and 1: 3(1-s)^2 s*a + 3(1-s) s^2*b + s^3
    static float BezierAxis(float s, float a, float b)
    {
        float inv = 1f - s;
        return 3f * inv * inv * s * a + 3f * inv * s * s * b + s * s * s;
    }

    static float BezierAxisSlope(float s, float a, float b)
    {
        float inv = 1f - s;
        return 3f * inv * inv * a + 6f * inv * s * (b - a) + 3f * s * s * (1f - b);
    }
}