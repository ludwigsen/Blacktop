using UnityEngine;

// Ball as its own object, transform-based (no Rigidbody — consistent with the rest of
// the project). Deliberately NOT parented to the carrier: a fumble, incomplete pass, or
// pitch just needs to stop following and let the ball sit/fly on its own, which is much
// simpler if it was never in the carrier's hierarchy to begin with.
//
// Three states now instead of just held/loose: Held (follows carrier, original
// behavior), InFlight (new — mid-pass or mid-pitch, follows a parabolic arc), Loose
// (original behavior — sits at its drop/incomplete/interception spot, polled recovery).
public class BallController : MonoBehaviour
{
    public enum BallState { Held, InFlight, Loose }

    public static BallController Instance { get; private set; }

    [SerializeField] Transform carrier;
    [SerializeField] Vector3 carryOffset = new Vector3(0.4f, 1f, 0.3f);

    // How close a player/defender/teammate needs to get to a loose ball to scoop it up.
    // Generous on purpose — same arcade-forgiveness reasoning as every other
    // OverlapSphere check in the project (TackleContact, StiffArmMove's contact range).
    // Also doubles as "close enough to complete a catch" for pass/pitch arrival.
    [SerializeField] float recoveryRadius = 1f;
    [SerializeField] string playerTag = "Player";
    [SerializeField] string defenderTag = "Defender";
    [SerializeField] string teammateTag = "Teammate"; // offensive AI receivers — add this tag in TagManager

    public BallState State { get; private set; } = BallState.Held;
    public Transform Carrier => carrier;
    public bool IsHeld => State == BallState.Held;

    // --- Pass/pitch flight state ---
    Vector3 launchPoint, targetPoint;
    Transform intendedReceiver;
    float flightTimer, flightDuration, flightArcHeight;
    bool isPitchInFlight;
    bool interceptionResolved; // single-roll-per-throw guard, same pattern as StiffArmMove's hasResolvedContact

    // Author as a 0→1→0 arc shape in Inspector — same idiom as HurdleMove's heightCurve.
    [SerializeField] AnimationCurve flightArcCurve = AnimationCurve.EaseInOut(0, 0, 1, 0);
    [SerializeField] float interceptCheckRadius = 1.2f;

    // Flat placeholder — same spirit as TackleContact's flat 50% fumble-chance testing
    // value. Scale by a defender Coverage-style multiplier once that stat exists.
    [SerializeField] float baseInterceptChance = 0.15f;

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

    // Every new play (previous one ended via tackle, touchdown, fumble, incomplete pass,
    // or interception) re-establishes possession with the player. No offense-vs-defense
    // possession flip exists yet, so "new play" always means "ball goes back to the
    // offense's ball carrier" — currently always UserPlayer.
    void HandlePlayReset()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) AttachTo(player.transform);
    }

    void LateUpdate()
    {
        switch (State)
        {
            case BallState.Held:
                FollowCarrier();
                break;
            case BallState.InFlight:
                UpdateFlight();
                break;
            case BallState.Loose:
                CheckRecovery(); // loose ball — poll every frame for anyone close enough to scoop it up
                break;
        }
    }

    void FollowCarrier()
    {
        if (carrier == null) return; // shouldn't happen while Held, but cheap to guard

        Vector3 forwardUp = carrier.TransformDirection(new Vector3(0f, carryOffset.y, carryOffset.z));
        transform.position = carrier.position + forwardUp + carrier.right * carryOffset.x;
        transform.rotation = carrier.rotation;
    }

    // Entry point called by PassMove/PitchMove on Exit. arcHeight/duration are supplied
    // by the caller so Pass (high, slow) and Pitch (flat, near-instant) can share this
    // one flight pipeline instead of needing separate code paths.
    public void Throw(Vector3 target, Transform receiver, bool isPitch, float arcHeight, float duration)
    {
        launchPoint = transform.position;
        targetPoint = target;
        intendedReceiver = receiver;
        isPitchInFlight = isPitch;
        flightArcHeight = arcHeight;
        flightDuration = duration;
        flightTimer = 0f;
        interceptionResolved = false;
        carrier = null;
        State = BallState.InFlight;
    }

    void UpdateFlight()
    {
        flightTimer += Time.deltaTime;
        float t = Mathf.Clamp01(flightTimer / flightDuration);

        Vector3 flatPos = Vector3.Lerp(launchPoint, targetPoint, t);
        float height = flightArcCurve.Evaluate(t) * flightArcHeight;
        transform.position = flatPos + Vector3.up * height;

        // Interception check — polled OverlapSphere along the flight path, same
        // no-Rigidbody/no-trigger-callback pattern as every other contact check in this
        // project. Skipped for pitches (Street convention: pitches aren't picked off).
        if (!isPitchInFlight && !interceptionResolved)
            TryIntercept();

        if (t >= 1f && State == BallState.InFlight) // still InFlight — TryIntercept may have already resolved this
            ResolveArrival();
    }

    void TryIntercept()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, interceptCheckRadius);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag(defenderTag)) continue;

            interceptionResolved = true;
            if (Random.value < baseInterceptChance)
            {
                AttachTo(hit.transform);
                if (PlayState.Instance != null)
                    PlayState.Instance.EndPlay(PlayState.PlayEndReason.Interception);
            }
            break;
        }
    }

    void ResolveArrival()
    {
        // No defender-presence check here yet — this is purely "did the ball reach the
        // spot the receiver is standing." Pass breakups (defender contests at the catch
        // point) are a deliberate follow-on, not covered by this first pass.
        if (intendedReceiver != null && Vector3.Distance(transform.position, intendedReceiver.position) <= recoveryRadius)
        {
            AttachTo(intendedReceiver);
        }
        else
        {
            // Incomplete pass drops straight into CheckRecovery below — no separate
            // "incomplete" concept needed, it's just a loose ball at the miss spot.
            Drop();
        }
    }

    // Polled via OverlapSphere, same pattern as TackleContact/StiffArmMove — no
    // Rigidbody/trigger-callback reliance anywhere in this project. First Player,
    // Defender, or Teammate tag found within range recovers the ball; whichever happens
    // to be first in the hits array wins on a tie (no tie-breaking logic — extremely
    // rare in practice given frame-rate granularity, revisit only if it's ever visibly bad).
    void CheckRecovery()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, recoveryRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag(playerTag) || hit.CompareTag(defenderTag) || hit.CompareTag(teammateTag))
            {
                AttachTo(hit.transform);
                return;
            }
        }
    }

    public void AttachTo(Transform newCarrier)
    {
        carrier = newCarrier;
        State = BallState.Held;
    }

    // Detaches and leaves the ball at its current world position — the drop spot for a
    // fumble or incomplete pass. Recovery is handled by CheckRecovery above.
    public void Drop()
    {
        carrier = null;
        State = BallState.Loose;
    }

    void OnDrawGizmosSelected()
    {
        if (State == BallState.Loose)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, recoveryRadius);
        }
    }
}