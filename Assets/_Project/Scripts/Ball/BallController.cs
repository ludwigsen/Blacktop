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

    [SerializeField] Transform carrier;
    [SerializeField] Vector3 carryOffset = new Vector3(0.4f, 1f, 0.3f);

    // How close a player/defender needs to get to a loose ball to scoop it up.
    // Generous on purpose — same arcade-forgiveness reasoning as every other
    // OverlapSphere check in the project (TackleContact, StiffArmMove's contact range).
    [SerializeField] float recoveryRadius = 1f;
    [SerializeField] string playerTag = "Player";
    [SerializeField] string defenderTag = "Defender";

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
        if (carrier == null)
        {
            CheckRecovery(); // loose ball — poll every frame for anyone close enough to scoop it up
            return;
        }

        Vector3 forwardUp = carrier.TransformDirection(new Vector3(0f, carryOffset.y, carryOffset.z));
        transform.position = carrier.position + forwardUp + carrier.right * carryOffset.x;
        transform.rotation = carrier.rotation;
    }

    // Polled via OverlapSphere, same pattern as TackleContact/StiffArmMove — no
    // Rigidbody/trigger-callback reliance anywhere in this project. First Player or
    // Defender tag found within range recovers the ball; whichever happens to be
    // first in the hits array wins on a tie (no tie-breaking logic — extremely rare
    // in practice given frame-rate granularity, revisit only if it's ever visibly bad).
    void CheckRecovery()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, recoveryRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag(playerTag) || hit.CompareTag(defenderTag))
            {
                AttachTo(hit.transform);
                return;
            }
        }
    }

    public void AttachTo(Transform newCarrier) => carrier = newCarrier;

    // Detaches and leaves the ball at its current world position — the drop spot for
    // a fumble. Recovery logic (nearest player/defender picks it up via OverlapSphere,
    // same pattern as TackleContact) is next pass, not this one.
    public void Drop() => carrier = null;

    void OnDrawGizmosSelected()
    {
        if (carrier == null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, recoveryRadius);
        }
    }
}