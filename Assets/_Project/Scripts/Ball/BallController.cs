using UnityEngine;

// Ball as its own object, transform-based (no Rigidbody — consistent with the rest of
// the project). Deliberately NOT parented to the carrier: a fumble, incomplete pass, or
// pitch just needs to stop following and let the ball sit/fly on its own, which is much
// simpler if it was never in the carrier's hierarchy to begin with.
//
// Four flight flavors now share one arc pipeline (FlightKind):
//   Pass       — thrown at a target, interceptable, resolves via a Catching-stat roll.
//   Pitch      — Street convention: never intercepted, tracks the receiver, always lands.
//   Fumble     — no receiver. Pops loose in a scattered direction on a tackle, lands
//                Loose and live (scoopable) — the play is NEVER ended here.
//   Deflection — a dropped pass's cosmetic bounce off the receiver's hands. Purely
//                visual; the play ends Incomplete the instant it lands. Never a real
//                recoverable loose ball — an incomplete pass is dead on the spot.
public class BallController : MonoBehaviour
{
    public enum BallState { Held, InFlight, Loose }
    enum FlightKind { Pass, Pitch, Fumble, Deflection }

    public static BallController Instance { get; private set; }

    [SerializeField] Transform carrier;
    [SerializeField] Vector3 carryOffset = new(0.4f, 1f, 0.3f);

    // How close a player needs to get to a loose ball to scoop it up. Generous on
    // purpose — same arcade-forgiveness reasoning as every other OverlapSphere check in
    // the project (TackleContact, StiffArmMove's contact range). Also doubles as "close
    // enough to complete a catch" for pass/pitch arrival.
    [SerializeField] float recoveryRadius = 1f;

    public BallState State { get; private set; } = BallState.Held;
    public Transform Carrier => carrier;
    public bool IsHeld => State == BallState.Held;
    public Vector3 FlightTarget => targetPoint; // where the current pass/pitch is headed — used to auto-switch defenders
    public Transform IntendedReceiver => intendedReceiver; // who the ball is thrown to, while InFlight — ReceiverAI uses this to know a pass is coming for THEM specifically, not just that some ball is in the air
 
    // Fired ONLY on a clean pass/pitch catch (see ResolvePassArrival/Pitch case) — never
    // on fumble recovery, never on interception. Single hook OffenseControlManager uses
    // to decide "should the player now be piloting this body."
    public event System.Action<Transform> OnControlEligibleCatch;

    // --- Flight state (shared by pass/pitch/fumble/deflection) ---
    Vector3 launchPoint, targetPoint;
    Transform intendedReceiver;
    float flightTimer, flightDuration, flightArcHeight;
    FlightKind flightKind;
    bool interceptionResolved; // single-roll-per-throw guard, same pattern as StiffArmMove's hasResolvedContact

    // Captured from the thrower's TeamMember at Throw() time, before carrier is nulled.
    // Only a member of the OTHER team can intercept a given throw — without this, a
    // thrower's own teammate standing near the flight path could "intercept" their own pass.
    int? throwingTeamId;

    // Author as a 0→1→0 arc shape in Inspector — same idiom as HurdleMove's heightCurve.
    // Shared by every flight kind; only flightArcHeight/flightDuration differ per kind.
    [SerializeField] AnimationCurve flightArcCurve = AnimationCurve.EaseInOut(0, 0, 1, 0);
    [SerializeField] float interceptCheckRadius = 1.2f;

    // Flat placeholder — same spirit as TackleContact's flat 50% fumble-chance testing
    // value. Scale by a defender Coverage-style multiplier once that stat is wired in.
    [SerializeField] float baseInterceptChance = 0.15f;

    [Header("Catching")]
    [Tooltip("Catch chance at neutral (rating 10) Catching, before the receiver's Catching multiplier is applied and the result is clamped 0-1. Only rolled on a ball that actually reaches the receiver — a ball thrown out of recoveryRadius is always an incomplete, no roll.")]
    [SerializeField, Range(0f, 1f)] float baseCatchChance = 0.95f;

    [Header("Fumble Pop")]
    [Tooltip("How far the ball scatters when it pops loose.")]
    [SerializeField] float fumbleMinDistance = 1f;
    [SerializeField] float fumbleMaxDistance = 3f;
    [SerializeField] float fumblePopHeight = 0.9f;
    [SerializeField] float fumbleFlightDuration = 0.35f;
    [Tooltip("Random cone (degrees, either side) around the 'straight away from the tackler' direction. 0 = always dead-away, no variance.")]
    [SerializeField] float fumbleScatterAngle = 70f;

    [Header("Dropped Pass Deflection")]
    [Tooltip("Small cosmetic bounce off the receiver's hands before the play ends Incomplete.")]
    [SerializeField] float deflectionMinDistance = 0.5f;
    [SerializeField] float deflectionMaxDistance = 1.5f;
    [SerializeField] float deflectionHeight = 0.5f;
    [SerializeField] float deflectionDuration = 0.25f;

    void Awake()
    {
        Instance = this;
        // Initial carrier is resolved in Start() from PlayState's possession team —
        // PlayState.Instance isn't guaranteed to exist yet during Awake.
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

            // PlayState owns possession, so it also decides who starts with the ball. The
            // serialized carrier above is now only a fallback for scenes with no PlayState.
            AttachToPossessionQuarterback();
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
    // or interception) puts the ball in the possession team's QB's hands. Who that is
    // comes from PlayState.PossessionTeamId — which flips on turnovers — not a fixed team.
    void HandlePlayReset() => AttachToPossessionQuarterback();

    // Ball has no business sitting at a fumble/incomplete/interception spot once the
    // team starts huddling back up — snap it straight to the QB. No visual handoff;
    // the huddle is about to be covered by playcalling UI anyway, so there's nothing
    // to sell here. PlayState resolves possession BEFORE firing OnHuddleStarted, so after
    // a turnover this is already the new offense's QB.
    void HandleHuddleStarted() => AttachToPossessionQuarterback();

    void AttachToPossessionQuarterback()
    {
        if (PlayState.Instance == null) return;
        var qb = PlayState.Instance.Passer;
        if (qb != null) AttachTo(qb);
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
                // incomplete pass calls EndPlay before ever reaching Loose, so this
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
        flightKind = isPitch ? FlightKind.Pitch : FlightKind.Pass;
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

    // Called by TackleContact on a successful fumble roll — BEFORE it calls
    // NotifyFumble()/AddDefensePoints, order doesn't matter between those and this.
    // The ball pops loose in a semi-random direction, scattered around "straight away
    // from whoever hit you" so it never looks like it teleports into the tackler's
    // chest, but also never pops the exact same way twice. Lands Loose and live —
    // EndPlay is NEVER called here. TackleContact/PlayState already treat a fumble as
    // a play that keeps going until the whistle.
    public void Fumble(Vector3 fromPosition, Vector3 tacklerPosition)
    {
        Vector3 away = fromPosition - tacklerPosition;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere;

        float scatter = Random.Range(-fumbleScatterAngle, fumbleScatterAngle);
        Vector3 direction = Quaternion.Euler(0f, scatter, 0f) * away;
        float distance = Random.Range(fumbleMinDistance, fumbleMaxDistance);

        launchPoint = fromPosition;
        targetPoint = fromPosition + direction * distance;
        intendedReceiver = null;
        flightKind = FlightKind.Fumble;
        flightArcHeight = fumblePopHeight;
        flightDuration = fumbleFlightDuration;
        flightTimer = 0f;
        interceptionResolved = false;
        throwingTeamId = null; // no "thrower" here — n/a, this flight never checks it

        carrier = null;
        State = BallState.InFlight;
    }

    // Cosmetic-only bounce off a receiver's hands on a failed catch roll. Never sets
    // State to a genuinely-live Loose — the moment this lands, ResolveArrival ends the
    // play as Incomplete, same frame, before any recovery poll can ever see it.
    void DeflectOffHands(Vector3 fromPosition)
    {
        Vector3 direction = Random.insideUnitSphere;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;

        float distance = Random.Range(deflectionMinDistance, deflectionMaxDistance);

        launchPoint = fromPosition;
        targetPoint = fromPosition + direction * distance;
        intendedReceiver = null;
        flightKind = FlightKind.Deflection;
        flightArcHeight = deflectionHeight;
        flightDuration = deflectionDuration;
        flightTimer = 0f;
        interceptionResolved = false;

        State = BallState.InFlight;
    }

    void UpdateFlight()
    {
        flightTimer += Time.deltaTime;
        float t = Mathf.Clamp01(flightTimer / flightDuration);

        // A pitch is a 0.15s reflex toss — the receiver keeps running while it's in the
        // air, and aiming at where they WERE at release could put the catch just outside
        // recoveryRadius and end the play as Incomplete. Track them instead, so a pitch
        // always arrives. (Passes deliberately keep the fixed target: leading the
        // receiver is the skill there. Fumble/Deflection have no receiver to track.)
        if (flightKind == FlightKind.Pitch && intendedReceiver != null)
            targetPoint = intendedReceiver.position;

        Vector3 flatPos = Vector3.Lerp(launchPoint, targetPoint, t);
        float height = flightArcCurve.Evaluate(t) * flightArcHeight;
        transform.position = flatPos + Vector3.up * height;

        // Interception check — polled OverlapSphere along the flight path, same
        // no-Rigidbody/no-trigger-callback pattern as every other contact check in this
        // project. Only real forward passes are interceptable: pitches by Street
        // convention, fumbles (nobody "intercepts" a bouncing loose ball mid-air — v1
        // just lets it land), and hands-deflections (already a dead ball) all skip this.
        if (flightKind == FlightKind.Pass && !interceptionResolved)
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

            float defenderCoverage = hit.TryGetComponent<DefenderAI>(out var defAI) ? defAI.CoverageMult : 1f;
            float chance = Mathf.Clamp01(baseInterceptChance * defenderCoverage);

            if (guaranteed || Random.value < chance)
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
        switch (flightKind)
        {
            case FlightKind.Fumble:
                // Lands live and loose — the play is already live (fumbles never end
                // the play here), CheckRecovery picks it up from the very next LateUpdate.
                Drop();
                return;

            case FlightKind.Deflection:
                // Cosmetic bounce is over — this is where the drop actually becomes
                // an incomplete pass.
                Drop();
                if (PlayState.Instance != null)
                    PlayState.Instance.EndPlay(PlayState.PlayEndReason.Incomplete);
                return;

            case FlightKind.Pitch:
                // Pitches always complete once the flight math places them at the
                // receiver — Street convention, no catch roll, no interception.
                if (intendedReceiver != null) AttachTo(intendedReceiver);
                else Drop(); // shouldn't happen (PitchMove already validated a target) — don't strand it mid-air
                return;

            case FlightKind.Pass:
                ResolvePassArrival();
                return;
        }
    }

    void ResolvePassArrival()
    {
        // Ball never got close enough to the receiver — a clean incomplete, no catch
        // roll needed (there was nothing catchable to drop).
        if (intendedReceiver == null || Vector3.Distance(transform.position, intendedReceiver.position) > recoveryRadius)
        {
            Drop();
            if (PlayState.Instance != null)
                PlayState.Instance.EndPlay(PlayState.PlayEndReason.Incomplete);
            return;
        }

        if (Random.value < CatchChance(intendedReceiver))
        {
            AttachTo(intendedReceiver);
        }
        else
        {
            // Catchable ball, failed the roll — tipped off the hands rather than a
            // clean drop dead on arrival.
            DeflectOffHands(intendedReceiver.position);
        }
    }

    // Catching multiplier already centers on 1.0 at neutral (rating 10) via
    // AttributeCurves — same "the curve does the work" pattern as every other stat
    // read in this project. No contested-catch modifier yet (nearby defender pressure
    // making a catch harder) — flagged for later, not v1.
    float CatchChance(Transform receiver)
    {
        var attrs = GetAttributes(receiver);
        float catchingMult = attrs != null ? attrs.Catching() : 1f;
        return Mathf.Clamp01(baseCatchChance * catchingMult);
    }

    // PlayerAttributes lives on several different components across this project
    // (PlayerMovement, PlayerStateMachine, AllyBlocker, GamebreakerController all
    // serialize their own reference) — PlayerStateMachine is the one every eligible
    // receiver slot carries, so that's the lookup used here.
    static PlayerAttributes GetAttributes(Transform t)
    {
        return t.TryGetComponent<PlayerStateMachine>(out var psm) ? psm.Attributes : null;
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