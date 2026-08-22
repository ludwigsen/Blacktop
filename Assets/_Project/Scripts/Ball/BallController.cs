using UnityEngine;

// Ball as its own object, transform-based (no Rigidbody — consistent with the rest of
// the project). Deliberately NOT parented to the carrier: a future fumble just needs to
// stop following and let the ball sit at its drop position, which is much simpler if
// it was never in the carrier's hierarchy to begin with.
//
// Follows in LateUpdate — runs after PlayerMovement's Update() moves the carrier, so the
// ball never lags a frame behind. No special PlayState.OnPlayReset handling needed: since
// position is fully re-derived from the carrier every frame, a reset that snaps the
// carrier's position (see PlayState.ResetPlay) is picked up automatically next LateUpdate.
public class BallController : MonoBehaviour
{
    public static BallController Instance { get; private set; }

    [SerializeField] Transform carrier; // assign the initial ball carrier (UserPlayer) in Inspector
    [SerializeField] Vector3 carryOffset = new Vector3(0.4f, 1f, 0.3f); // tucked at carrier's side, in carrier-local space

    public Transform Carrier => carrier;
    public bool IsHeld => carrier != null;

    void Awake()
    {
        Instance = this;

        if (carrier == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) carrier = player.transform;
        }
    }

    // Subscribed in Start(), not OnEnable() — Unity guarantees all Awake() calls run
    // before any Start() calls in the same frame, so this ensures PlayState.Instance
    // is guaranteed to exist by the time we try to subscribe (Awake-order between
    // separate GameObjects isn't guaranteed, Start-order relative to all Awakes is).
    void Start()
    {
        if (PlayState.Instance != null)
            PlayState.Instance.OnPlayReset += HandlePlayReset;
    }

    void OnDestroy()
    {
        if (PlayState.Instance != null)
            PlayState.Instance.OnPlayReset -= HandlePlayReset;
    }

    // Every new play (whether the previous one ended via a clean tackle or a fumble
    // that dropped the ball loose) re-establishes possession with the player. No
    // pass/reception/recovery system exists yet, so "new play" always means "ball
    // goes back to the offense's ball carrier" — currently always UserPlayer.
    void HandlePlayReset()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) AttachTo(player.transform);
    }

    void LateUpdate()
    {
        if (carrier == null) return;

        // Only forward/up components rotate with facing — the lateral (x) component
        // stays in world space. A fully carrier-relative offset was swinging visibly
        // during quick turns (the x offset sweeping through the turn arc), which read
        // as camera lag once CameraFollow started tracking the ball instead of the
        // player directly. This keeps the "tucked" look without the swing.
        Vector3 forwardUp = carrier.TransformDirection(new Vector3(0f, carryOffset.y, carryOffset.z));
        transform.position = carrier.position + forwardUp + carrier.right * carryOffset.x;
        transform.rotation = carrier.rotation;
    }

    // Called on possession change — reception, fumble recovery, etc. Kept generic
    // (not "GiveToPlayer") since defenders will eventually be able to hold it too
    // (interceptions).
    public void AttachTo(Transform newCarrier) => carrier = newCarrier;

    // Detaches and leaves the ball at its current world position — the drop spot for
    // a fumble. Recovery logic (nearest player/defender picks it up via OverlapSphere,
    // same pattern as TackleContact) is next pass, not this one.
    public void Drop() => carrier = null;
}