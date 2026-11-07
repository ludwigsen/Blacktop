using System.Collections.Generic;
using UnityEngine;

// Ball as its own object, transform-based (no Rigidbody — consistent with the rest of
// the project). Deliberately NOT parented to the carrier: a fumble, incomplete pass, or
// pitch just needs to stop following and let the ball sit/fly on its own, which is much
// simpler if it was never in the carrier's hierarchy to begin with.
//
// Forward passes use constant-gravity ballistics; pitches/deflections retain their
// authored cosmetic arcs. A fumble is Loose immediately, even while airborne, so
// the existing scrum/control owners can respond throughout its free motion.
public class BallController : MonoBehaviour
{
    public enum BallState { Held, InFlight, Loose }
    enum FlightKind { Pass, Pitch, Deflection }

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
    public bool IsForwardPass => State == BallState.InFlight && flightKind == FlightKind.Pass;
    public bool IsLiveLoose => State == BallState.Loose && (PlayState.Instance == null || PlayState.Instance.IsLive);
    public Vector3 Velocity => State == BallState.Loose ? looseMotion.Velocity : flightVelocity;

    // Rendering follows in LateUpdate, but rulings can happen during Update. Resolve
    // the held spot live so goal-line/contact order never reads last frame's ball.
    public Vector3 RulingPosition => IsHeld && carrier != null
        ? carrier.position + carrier.TransformDirection(carryOffset)
        : transform.position;
    public Vector3 FlightTarget => targetPoint; // where the current pass/pitch is headed — used to auto-switch defenders
    public Transform IntendedReceiver => intendedReceiver; // who the ball is thrown to, while InFlight — ReceiverAI uses this to know a pass is coming for THEM specifically, not just that some ball is in the air
 
    // Fired ONLY on a clean pass/pitch catch (see ResolvePassArrival/Pitch case) — never
    // on fumble recovery, never on interception. Single hook OffenseControlManager uses
    // to decide "should the player now be piloting this body."
    public event System.Action<Transform> OnControlEligibleCatch;

    [Header("Ballistic Pass")]
    [SerializeField, Min(0.1f)] float passGravity = 9.81f;
    [SerializeField] float spiralDegreesPerSecond = 1440f;
    [Header("Live Loose Ball")]
    [SerializeField] BallLooseMotion looseMotion = new();
    readonly HashSet<TeamMember> releaseRecoveryContacts = new();
    readonly HashSet<TeamMember> currentRecoveryContacts = new();
    Vector3 flightVelocity;
    float spiralAngle;

    // --- Flight state (pass/pitch/dead deflection) ---
    Vector3 launchPoint, targetPoint;
    Transform intendedReceiver;
    float flightTimer, flightDuration, flightArcHeight;
    FlightKind flightKind;
    bool interceptionResolved; // single-roll-per-throw guard, same pattern as StiffArmMove's hasResolvedContact

    // Captured from the thrower's TeamMember at Throw() time, before carrier is nulled.
    // Only a member of the OTHER team can intercept a given throw — without this, a
    // thrower's own teammate standing near the flight path could "intercept" their own pass.
    int? throwingTeamId;

    // Cosmetic pitch/deflection shape only; forward passes use constant gravity.
    [SerializeField] AnimationCurve flightArcCurve = AnimationCurve.EaseInOut(0, 0, 1, 0);
    [SerializeField] float interceptCheckRadius = 1.2f;

    // Flat placeholder — same spirit as TackleContact's flat 50% fumble-chance testing
    // value. Scale by a defender Coverage-style multiplier once that stat is wired in.
    [SerializeField] float baseInterceptChance = 0.15f;

    [Header("Catching")]
    [Tooltip("Catch chance at neutral (rating 10) Catching, before the receiver's Catching multiplier is applied and the result is clamped 0-1. Only rolled on a ball that actually reaches the receiver — a ball thrown out of recoveryRadius is always an incomplete, no roll.")]
    [SerializeField, Range(0f, 1f)] float baseCatchChance = 0.95f;

    [Header("Fumble Pop")]
    [Tooltip("Horizontal scatter to first ground contact, before bounce/roll.")]
    [SerializeField] float fumbleMinDistance = 1f;
    [SerializeField] float fumbleMaxDistance = 3f;
    [SerializeField] float fumblePopHeight = 0.9f;
    [Tooltip("Travel from the release point before initially overlapping players may scoop without first separating. Spatial clearance, not a timer.")]
    [SerializeField, Min(0f)] float releaseClearance = 0.5f;
    Vector3 releaseOrigin;
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
        // Existing scenes receive marker presentation without a scene/GUID migration.
        if (!TryGetComponent<BallGroundMarkers>(out _)) gameObject.AddComponent<BallGroundMarkers>();
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
            PlayState.Instance.OnPlayEnded += HandlePlayEnded;

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
            PlayState.Instance.OnPlayEnded -= HandlePlayEnded;
        }
    }

    void HandlePlayEnded(PlayState.PlayEndReason reason)
    {
        // A whistle freezes free motion immediately, including a dead pass in flight.
        looseMotion.Stop();
        flightVelocity = Vector3.zero;
    }

    // New plays resolve their QB from current possession, never a fixed team/actor.
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

    void LateUpdate() => Simulate(Time.deltaTime);

    void Simulate(float deltaTime)
    {
        // Auto-sync is disabled in this project. Read the current Update-driven
        // player/wall transforms once before polling contacts in LateUpdate.
        if (State != BallState.Held && (PlayState.Instance == null || PlayState.Instance.IsLive))
            Physics.SyncTransforms();
        switch (State)
        {
            case BallState.Held:
                FollowCarrier();
                break;
            case BallState.InFlight:
                UpdateFlight(deltaTime);
                break;
            case BallState.Loose:
                // Only scramble for it while the play's actually still live — a fumble
                // keeps IsLive true (that's the whole point of a scramble), but an
                // incomplete pass calls EndPlay before ever reaching Loose, so this
                // correctly skips recovery for a dead ball instead of letting whoever's
                // nearest silently pick it up after the whistle.
                if (IsLiveLoose)
                    looseMotion.Simulate(transform, deltaTime, TryRecoverAlongSegment);
                break;
        }
    }

    void FollowCarrier()
    {
        if (carrier == null) return; // shouldn't happen while Held, but cheap to guard

        transform.SetPositionAndRotation(RulingPosition, carrier.rotation);
    }

    // Callers retain target/time ownership. Forward-pass loft is derived from gravity;
    // arcHeight remains the authored cosmetic height for the special Street pitch.
    public void Throw(Vector3 target, Transform receiver, bool isPitch, float arcHeight, float duration)
    {
        launchPoint = RulingPosition;
        transform.position = launchPoint;
        looseMotion.Stop();
        releaseRecoveryContacts.Clear();
        targetPoint = target;
        intendedReceiver = receiver;
        flightKind = isPitch ? FlightKind.Pitch : FlightKind.Pass;
        flightArcHeight = arcHeight;
        flightDuration = Mathf.Max(0.001f, duration);
        flightTimer = 0f;
        interceptionResolved = false;
        spiralAngle = 0f;
        // Target and arrival time stay owned by the existing Passing/lead calculation.
        // Constant gravity derives the vertical velocity and loft from that same time.
        flightVelocity = (targetPoint - launchPoint) / flightDuration + Vector3.up * (0.5f * passGravity * flightDuration);

        // Capture BEFORE clearing carrier below — this is the only point where we still
        // know who threw it.
        throwingTeamId = (carrier != null && carrier.TryGetComponent<TeamMember>(out var throwerTeam))
            ? throwerTeam.teamId
            : (int?)null;

        carrier = null;
        State = BallState.InFlight;
    }

    // Start at the live held position, not last frame's rendered position. Initial
    // overlap candidates first separate or let the ball travel its release clearance;
    // other players can recover immediately, including before ground contact.
    public void Fumble(Vector3 fromPosition, Vector3 tacklerPosition)
    {
        Vector3 release = IsHeld && carrier != null ? RulingPosition : fromPosition;
        Vector3 away = Vector3.ProjectOnPlane(fromPosition - tacklerPosition, Vector3.up);
        away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
        Vector3 direction = Quaternion.Euler(0f, Random.Range(-fumbleScatterAngle, fumbleScatterAngle), 0f) * away;
        float upSpeed = Mathf.Sqrt(2f * looseMotion.Gravity * Mathf.Max(0f, fumblePopHeight));
        // Derive airtime from gravity/height so the existing scatter distances remain
        // distances, rather than accidentally becoming several times larger as speeds.
        float duration = Mathf.Max(0.01f, (upSpeed + Mathf.Sqrt(upSpeed * upSpeed +
            2f * looseMotion.Gravity * Mathf.Max(0f, release.y))) / looseMotion.Gravity);
        Vector3 velocity = direction * (Random.Range(fumbleMinDistance, fumbleMaxDistance) / duration) + Vector3.up * upSpeed;
        Drop();
        transform.position = release;
        releaseOrigin = release;
        Physics.SyncTransforms(); // capture actual release overlaps after transform movement
        releaseRecoveryContacts.Clear();
        foreach (var hit in Physics.OverlapSphere(release, recoveryRadius))
        {
            var member = hit.GetComponentInParent<TeamMember>();
            if (member != null) releaseRecoveryContacts.Add(member);
        }
        looseMotion.Launch(transform, velocity);
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

    void UpdateFlight(float deltaTime)
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;
        if (flightDuration <= 0f) return; // an uninitialized/editor-forced state has no trajectory
        Vector3 previous = transform.position;
        flightTimer = Mathf.Min(flightTimer + deltaTime, flightDuration);
        float t = Mathf.Clamp01(flightTimer / flightDuration);
        if (flightKind == FlightKind.Pass)
        {
            Vector3 initialVelocity = (targetPoint - launchPoint) / flightDuration +
                Vector3.up * (0.5f * passGravity * flightDuration);
            transform.position = launchPoint + initialVelocity * flightTimer -
                Vector3.up * (0.5f * passGravity * flightTimer * flightTimer);
            flightVelocity = initialVelocity - Vector3.up * (passGravity * flightTimer);
            spiralAngle = (spiralAngle + spiralDegreesPerSecond * deltaTime) % 360f;
            if (flightVelocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(flightVelocity) * Quaternion.AngleAxis(spiralAngle, Vector3.forward);
            if (!interceptionResolved) TryInterceptAlongSegment(previous, transform.position);
        }
        else
        {
            // Street pitches keep following their runner and always complete; failed
            // catch deflections stay cosmetic and can never enter recovery polling.
            if (flightKind == FlightKind.Pitch && intendedReceiver != null)
                targetPoint = intendedReceiver.position;
            transform.position = Vector3.Lerp(launchPoint, targetPoint, t) +
                Vector3.up * (flightArcCurve.Evaluate(t) * flightArcHeight);
        }
        if (t >= 1f && State == BallState.InFlight) ResolveArrival();
    }

    void TryInterceptAlongSegment(Vector3 from, Vector3 to)
    {
        // Sweeping the existing interception radius preserves its roll and attribute
        // owner while removing frame-rate-dependent gaps along fast forward passes.
        ResolveInterception(Physics.OverlapCapsule(from, to, interceptCheckRadius));
    }

    void ResolveInterception(Collider[] hits)
    {
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<TeamMember>(out var member)) continue;
            // Same team as the thrower — that's just a teammate near the flight path,
            // not a defensive play on the ball. Keep looking.
            if (throwingTeamId.HasValue && member.teamId == throwingTeamId.Value) continue;

            interceptionResolved = true;

            float defenderCoverage = hit.TryGetComponent<DefenderAI>(out var defAI) ? defAI.CoverageMult : 1f;
            float chance = Mathf.Clamp01(baseInterceptChance * defenderCoverage);

            if (Random.value < chance)
            {
                AttachTo(hit.transform);
                if (PlayState.Instance != null)
                {
                    // Swagger controls meter earnings; the interceptor owns this award.
                    var attr = member.GetComponent<PlayerStateMachine>()?.Attributes;
                    PlayState.Instance.AddTeamPoints(member.teamId, 8f * (attr != null ? attr.Swagger() : 1f));
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

    // Polled along the swept volume, with no
    // Rigidbody/trigger-callback reliance anywhere in this project. Recovery is
    // deterministic: nearest eligible player wins, with entity ID breaking exact ties.
    // This prevents the physics engine's collider ordering from deciding possession.
    bool TryRecoverAlongSegment(Vector3 from, Vector3 to)
    {
        if (!IsLiveLoose) return true;
        transform.position = to;
        // Preserve boundary precedence: an airborne fumble is live/Loose, so it is
        // ruled at its crossing rather than returning from outside the field.
        if (PlayState.Instance != null && PlayState.Instance.IsBallOutOfBounds())
        {
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.OutOfBounds);
            return true;
        }
        Collider[] hits = Physics.OverlapCapsule(from, to, recoveryRadius);
        currentRecoveryContacts.Clear();
        foreach (var hit in hits)
        {
            var member = hit.GetComponentInParent<TeamMember>();
            if (member != null) currentRecoveryContacts.Add(member);
        }
        bool clearedRelease = (to - releaseOrigin).sqrMagnitude >= releaseClearance * releaseClearance;
        releaseRecoveryContacts.RemoveWhere(member => clearedRelease || member == null || !currentRecoveryContacts.Contains(member));
        Transform best = null;
        float bestDistanceSqr = float.PositiveInfinity;
        EntityId bestEntityId = default;
        foreach (var member in currentRecoveryContacts)
        {
            if (!member.isActiveAndEnabled || releaseRecoveryContacts.Contains(member)) continue;
            Transform candidate = member.transform;
            Vector3 segment = to - from;
            float fraction = segment.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(candidate.position - from, segment) / segment.sqrMagnitude) : 0f;
            Vector3 nearest = from + segment * fraction;
            if (RecoveryBlocked(nearest, candidate.position)) continue;
            float distanceSqr = (candidate.position - nearest).sqrMagnitude;
            EntityId entityId = candidate.GetEntityId();
            if (distanceSqr < bestDistanceSqr ||
                (Mathf.Approximately(distanceSqr, bestDistanceSqr) && entityId.CompareTo(bestEntityId) < 0))
            {
                best = candidate;
                bestDistanceSqr = distanceSqr;
                bestEntityId = entityId;
            }
        }
        if (best == null) return false;
        AttachTo(best);
        return true; // one authoritative attachment stops all remaining free motion
    }

    bool RecoveryBlocked(Vector3 from, Vector3 to)
    {
        // The generous scoop radius must not reach through a solid fence/wall.
        // Player bodies remain recovery candidates rather than occluding teammates.
        Vector3 travel = to - from;
        if (travel.sqrMagnitude < 0.000001f) return false;
        foreach (var hit in Physics.RaycastAll(from, travel.normalized, travel.magnitude,
            Physics.AllLayers, QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(transform) &&
                hit.collider.GetComponentInParent<TeamMember>() == null) return true;
        return false;
    }

    public void AttachTo(Transform newCarrier)
    {
        looseMotion.Stop();
        releaseRecoveryContacts.Clear();
        flightVelocity = Vector3.zero;
        carrier = newCarrier;
        intendedReceiver = null;
        State = BallState.Held;
    }

    // Detaches and leaves the ball at its current world position — the drop spot for a
    // fumble or incomplete pass. Recovery is handled by the live loose-ball path.
    public void Drop()
    {
        looseMotion.Stop();
        releaseRecoveryContacts.Clear();
        flightVelocity = Vector3.zero;
        carrier = null;
        intendedReceiver = null; // a dead/loose ball has no live receiving team for control
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
