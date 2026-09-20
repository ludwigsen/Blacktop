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
    [SerializeField] Vector3 carryOffset = new(0.4f, 1f, 0.3f);

    // No "starting offense" concept exists yet anywhere in the project — every new
    // play resets possession to this team's QB. Once alternating possession/kickoff
    // logic exists, this is the one place that needs to change.
    [SerializeField] int defaultOffenseTeamId = 0;

    // How close a player needs to get to a loose ball to scoop it up. Generous on
    // purpose — same arcade-forgiveness reasoning as every other OverlapSphere check in
    // the project (TackleContact, StiffArmMove's contact range). Also doubles as "close
    // enough to complete a catch" for pass/pitch arrival.
    [SerializeField] float recoveryRadius = 1f;

    public BallState State { get; private set; } = BallState.Held;
    public Transform Carrier => carrier;
    public bool IsHeld => State == BallState.Held;

    // Fired ONLY on a clean pass/pitch catch (see ResolveArrival) — never on fumble
    // recovery, never on interception. This is the single hook OffenseControlManager
    // uses to decide "should the player now be piloting this body." Keeping it this
    // narrow is deliberate: a defender recovering a fumble should obviously never grant
    // control, and even a teammate scooping a loose ball mid-scramble shouldn't yank
    // control away mid-chaos — only a deliberate completed throw should.
    public event System.Action<Transform> OnControlEligibleCatch;

    // --- Pass/pitch flight state ---
    Vector3 launchPoint, targetPoint;
    Transform intendedReceiver;
    float flightTimer, flightDuration, flightArcHeight;
    bool isPitchInFlight;
    bool interceptionResolved; // single-roll-per-throw guard, same pattern as StiffArmMove's hasResolvedContact

    // Captured from the thrower's TeamMember at Throw() time, before carrier is nulled.
    // Only a member of the OTHER team can intercept a given throw — without this, a
    // thrower's own teammate standing near the flight path could "intercept" their own pass.
    int? throwingTeamId;

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
            var qb = FindQuarterback(defaultOffenseTeamId);
            if (qb != null) carrier = qb;
        }
    }

    // Subscribed in Start(), not OnEnable() — Unity guarantees all Awake() calls run
    // before any Start() calls in the same frame, so this ensures PlayState.Instance
    // is guaranteed to exist by the time we try to subscribe (Awake-order between
    // separate GameObjects isn't guaranteed, Start-order relative to all Awakes is).
    void Start()
    {
        if (PlayState.Instance != null)
        {
            PlayState.Instance.OnPlayReset += HandlePlayReset;
            PlayState.Instance.OnHuddleStarted += HandleHuddleStarted;
        }
    }

    void OnDestroy()
    {
        if (PlayState.Instance != null)
        {
            PlayState.Instance.OnPlayReset -= HandlePlayReset;
            PlayState.Instance.OnHuddleStarted -= HandleHuddleStarted;
        }
    }

    // Every new play (previous one ended via tackle, touchdown, fumble, incomplete pass,
    // or interception) re-establishes possession with defaultOffenseTeamId's QB. No
    // alternating-possession logic exists yet — see the field comment above.
    void HandlePlayReset()
    {
        var qb = FindQuarterback(defaultOffenseTeamId);
        if (qb != null) AttachTo(qb);
    }

    // Ball has no business sitting at a fumble/incomplete/interception spot once the
    // team starts huddling back up — snap it straight to the QB. No visual handoff;
    // the huddle is about to be covered by playcalling UI anyway, so there's nothing
    // to sell here. Same target-finding as HandlePlayReset, just fired earlier in the
    // pre-snap sequence.
    void HandleHuddleStarted()
    {
        var qb = FindQuarterback(defaultOffenseTeamId);
        if (qb != null) AttachTo(qb);
    }

    static Transform FindQuarterback(int teamId)
    {
        foreach (var member in FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (member.teamId == teamId && member.slot == TeamMember.RosterSlot.QB)
                return member.transform;
        }
        return null;
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
                // Only scramble for it while the play's actually still live — a fumble
                // keeps IsLive true (that's the whole point of a scramble), but an
                // incomplete pass calls EndPlay before dropping into Loose, so this
                // correctly skips recovery for a dead ball instead of letting whoever's
                // nearest silently pick it up after the whistle.
                if (PlayState.Instance == null || PlayState.Instance.IsLive)
                    CheckRecovery();
                break;
        }
    }

    void FollowCarrier()
    {
        if (carrier == null) return; // shouldn't happen while Held, but cheap to guard

        Vector3 forwardUp = carrier.TransformDirection(new Vector3(0f, carryOffset.y, carryOffset.z));
        transform.SetPositionAndRotation(carrier.position + forwardUp + carrier.right * carryOffset.x, carrier.rotation);
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

        // Capture BEFORE clearing carrier below — this is the only point where we still
        // know who threw it.
        throwingTeamId = (carrier != null && carrier.TryGetComponent<TeamMember>(out var throwerTeam))
            ? throwerTeam.teamId
            : (int?)null;

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
            if (!hit.TryGetComponent<TeamMember>(out var member)) continue;
            // Same team as the thrower — that's just a teammate near the flight path,
            // not a defensive play on the ball. Keep looking.
            if (throwingTeamId.HasValue && member.teamId == throwingTeamId.Value) continue;

            interceptionResolved = true;
            bool guaranteed = PlayState.Instance != null && PlayState.Instance.ConsumeGuaranteedTurnover();

            if (guaranteed || Random.value < baseInterceptChance)
            {
                AttachTo(hit.transform);
                if (PlayState.Instance != null)
                {
                    PlayState.Instance.AddDefensePoints(8f);
                    PlayState.Instance.EndPlay(PlayState.PlayEndReason.Interception);
                }
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
            // Incomplete pass is a DEAD ball, not a fumble — ends the play immediately
            // rather than sitting there as a loose ball waiting for someone to scramble
            // for it. Drop() still lets it visually fall at the miss spot; EndPlay is
            // what actually stops the down (and is what makes ResetPlay's IsLive guard
            // pass again).
            Drop();
            if (PlayState.Instance != null)
                PlayState.Instance.EndPlay(PlayState.PlayEndReason.Incomplete);
        }
    }

    // Polled via OverlapSphere, same pattern as TackleContact/StiffArmMove — no
    // Rigidbody/trigger-callback reliance anywhere in this project. First player found
    // within range recovers, regardless of team — whichever happens to be first in the
    // hits array wins on a tie (no tie-breaking logic, flagged separately in project
    // status notes).
    void CheckRecovery()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, recoveryRadius);
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<TeamMember>(out _)) continue;
            AttachTo(hit.transform);
            return;
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