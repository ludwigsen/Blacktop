using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayState : MonoBehaviour
{
    public enum PlayEndReason { Tackled, Touchdown, Interception, Incomplete, OutOfBounds, Safety }

    public static PlayState Instance { get; private set; }

    // --- Possession ---
    // Teams are identified by TeamMember.teamId (0 = Team 1, 1 = Team 2) and are static per
    // player. Everything role-related — who's on offense, who snaps, which way we're
    // driving, who lines up where — is derived from PossessionTeamId, never authored per
    // player. This replaces the old hand-wired player / offensivePlayers / defenders lists
    // (T1 = offense, T2 = defense forever) and BallController.defaultOffenseTeamId.
    [Header("Possession")]
    [Tooltip("TeamMember.teamId of the team that snaps the ball on the very first play.")]
    [SerializeField] int startingPossessionTeamId = 0;
    [Tooltip("Team 0 drives toward +Z when checked; Team 1 always drives the opposite way. Uncheck for a halftime-style swap.")]
    [SerializeField] bool team0AttacksPositiveZ = true;

    // Rosters are resolved from TeamMember.slot. These orders are what make slot N of a
    // FormationData asset (and receiverIndex N in a PlayCallData) land on a specific
    // player. Defaults reproduce the exact assignment the old hand-wired lists had.
    [Header("Roster Ordering (by TeamMember.slot)")]
    [Tooltip("Non-QB offense, in the order FormationData.offensiveSlots (and PlayCallData receiverIndex) are authored. QB is handled separately via qbOffsetFromLOS.")]
    [SerializeField] TeamMember.RosterSlot[] offenseSlotOrder =
    {
        TeamMember.RosterSlot.RB, TeamMember.RosterSlot.OL1, TeamMember.RosterSlot.OL2,
        TeamMember.RosterSlot.WR1, TeamMember.RosterSlot.WR2, TeamMember.RosterSlot.WR3
    };
    [Tooltip("A team's players in the order FormationData.defenderSlots are authored (EDGE-L, EDGE-R, LB-L, LB-R, DB-L, DB-R, S). Both-ways roster: any player can fill any defensive slot.")]
    [SerializeField] TeamMember.RosterSlot[] defenseSlotOrder =
    {
        TeamMember.RosterSlot.OL1, TeamMember.RosterSlot.OL2, TeamMember.RosterSlot.QB, TeamMember.RosterSlot.RB,
        TeamMember.RosterSlot.WR1, TeamMember.RosterSlot.WR2, TeamMember.RosterSlot.WR3
    };

    [Header("Field Position (authored in attack-axis space: negative = own side of midfield)")]
    [SerializeField] float initialPlayerZ = -5f;
    [SerializeField] float kickoffResetZ = -5f;

    [Header("Formations (side-based defaults — apply to whichever team is on that side of the ball)")]
    [SerializeField] FormationData defaultDefensiveFormation;
    [SerializeField] FormationData defaultOffensiveFormation;

    FormationData defensiveFormationOverride;
    public void SetDefensiveFormation(FormationData f) => defensiveFormationOverride = f;

    [Header("Gamebreaker")]
    [SerializeField] GamebreakerBuffs gamebreakerBuffs;
    [SerializeField] int gamebreakerPossessionLimit = 3;

    [SerializeField] PlayCallData playCall;

    [Header("Snap Safety")]
    [Tooltip("Brief window after the snap where a tackle can't register.")]
    [SerializeField] float postSnapTackleGrace = 0.25f;

    float snapTimestamp;
    public bool IsPostSnapGraceActive => IsLive && (Time.time - snapTimestamp < postSnapTackleGrace);

    // --- Huddle / pre-snap flow ---
    // GatheringToHuddle: automatic, starts the instant the whistle blows, no input needed.
    // InHuddle: PlayCallSelector cycles freely here; nothing moves until Confirm.
    // BreakingToFormation: animated walk to the selected play's spots (offense AND defense).
    // SetAtLOS: arrived and set — this is the ONLY phase ResetPlay() (the snap) will act on.
    enum HuddlePhase { GatheringToHuddle, InHuddle, BreakingToFormation, SetAtLOS }
    HuddlePhase phase;

    [Header("Huddle / Formation")]
    [SerializeField] float huddleRadius = 1.5f;
    [SerializeField] float huddleDistanceBehindLOS = 2f;
    [SerializeField] float formationMoveSpeed = 6f;
    [SerializeField] float formationArrivalTolerance = 0.15f;
    [Tooltip("Delay-of-game clock. Runs continuously from the whistle; force-completes whatever step is in progress and snaps if it hits zero.")]
    [SerializeField] float playClockDuration = 40f;

    float playClockTimer;
    public bool IsInHuddle => !IsLive && phase == HuddlePhase.InHuddle;
    public bool IsSetAtLOS => !IsLive && phase == HuddlePhase.SetAtLOS;
    public float PlayClockRemaining => playClockTimer;

    bool passerCrossedLOS;
    public bool HasPasserCrossedLOS => passerCrossedLOS;

    public bool IsLive { get; private set; } = false;
    public event System.Action<PlayEndReason> OnPlayEnded;
    public event System.Action OnPlayReset;
    public event System.Action OnHuddleStarted; // fired the instant the huddle begins forming — before any walk-in animation

    InputSystem_Actions controls;
    float nextLineOfScrimmageZ;
    public float CurrentLineOfScrimmageZ => nextLineOfScrimmageZ;
    PlayEndReason lastEndReason;

    // ------------------------------------------------------------------------------
    // Possession API
    // ------------------------------------------------------------------------------

    // The team snapping the ball this play. Only PlayState changes it (TrySetPossession),
    // and only between plays — so within a play, everything that reads it sees one answer.
    public int PossessionTeamId { get; private set; }
    public int DefendingTeamId => OpponentOf(PossessionTeamId);

    // The two-team assumption lives here and nowhere else.
    public static int OpponentOf(int teamId) => 1 - teamId;

    // Fired AFTER rosters are refreshed, so subscribers can read Passer/OffensivePlayers
    // straight away. Fires between plays only (during EndPlay, before OnPlayEnded).
    public event System.Action<int> OnPossessionChanged;

    // Each team has a fixed attack direction; possession decides which one is "the" direction.
    public FieldDirection DirectionFor(int teamId)
    {
        bool attacksPositiveZ = teamId == 0 ? team0AttacksPositiveZ : !team0AttacksPositiveZ;
        return FieldDirection.FromAttacksPositiveZ(attacksPositiveZ);
    }

    public FieldDirection AttackDirection => DirectionFor(PossessionTeamId);

    // LOS helpers in the current attack direction — use these instead of comparing raw Z.
    public bool IsPastLineOfScrimmage(float worldZ) => AttackDirection.IsPastLOS(worldZ, nextLineOfScrimmageZ);
    public float YardsPastLineOfScrimmage(float worldZ) => AttackDirection.YardsPastLOS(worldZ, nextLineOfScrimmageZ);

    // Rosters for the current possession, rebuilt by RefreshRosters(). Lists keep a null
    // entry for a missing slot on purpose — index N must stay aligned with formation slot
    // N and PlayCallData.receiverIndex N. Every consumer already null-checks.
    Transform quarterback;
    readonly List<Transform> offensePlayers = new(); // possession team, non-QB, in offenseSlotOrder
    readonly List<Transform> defensePlayers = new(); // opposing team, in defenseSlotOrder

    public Transform Passer => quarterback; // possession team's QB
    public List<Transform> OffensivePlayers => offensePlayers;

    // Re-resolves rosters from TeamMember. Runs at Awake and on every possession change;
    // call it yourself if players are ever spawned/despawned at runtime.
    public void RefreshRosters()
    {
        var members = FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        quarterback = ResolveSlot(members, PossessionTeamId, TeamMember.RosterSlot.QB);

        offensePlayers.Clear();
        foreach (var slot in offenseSlotOrder)
            offensePlayers.Add(ResolveSlot(members, PossessionTeamId, slot));

        defensePlayers.Clear();
        foreach (var slot in defenseSlotOrder)
            defensePlayers.Add(ResolveSlot(members, DefendingTeamId, slot));
    }

    static Transform ResolveSlot(TeamMember[] members, int teamId, TeamMember.RosterSlot slot)
    {
        foreach (var m in members)
        {
            if (m.teamId == teamId && m.slot == slot) return m.transform;
        }

        Debug.LogWarning($"[PlayState] Team {teamId} has no {slot} TeamMember — that formation slot will stay empty.");
        return null;
    }

    // A team can take possession only if it's genuinely both-ways: a QB, plus the full
    // offensive control stack (PossessionController) on every player, AND the other team
    // has pursuit AI (DefenderAI) on every player. Until the T1/T2 prefabs are unified
    // this is what stops a turnover from handing the ball to a team that can't run a play.
    public bool CanTakePossession(int teamId)
    {
        bool hasQB = false;

        foreach (var m in FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (m.teamId == teamId)
            {
                if (!m.TryGetComponent<PossessionController>(out _)) return false;
                if (m.slot == TeamMember.RosterSlot.QB) hasQB = true;
            }
            else if (!m.TryGetComponent<DefenderAI>(out _))
            {
                return false;
            }
        }

        return hasQB;
    }

    public bool TrySetPossession(int teamId)
    {
        if (teamId == PossessionTeamId) return true;

        if (IsLive)
        {
            Debug.LogWarning("[PlayState] Possession can only change between plays.");
            return false;
        }

        if (!CanTakePossession(teamId))
        {
            Debug.LogWarning($"[PlayState] Team {teamId} can't take possession yet — needs a QB with PossessionController on every player, and DefenderAI on every player of the other team.");
            return false;
        }

        ApplyPossession(teamId);
        return true;
    }

    void ApplyPossession(int teamId)
    {
        PossessionTeamId = teamId;
        RefreshRosters();
        OnPossessionChanged?.Invoke(teamId);
    }

    // Whoever is holding the ball at the whistle snaps next: tackle, out of bounds,
    // interception, and a fumble recovered by the other team all resolve through this one
    // rule. Loose/incomplete balls (no carrier) leave possession alone. Silent no-op when
    // the other team can't take over yet, so today's T1-only game behaves exactly as before.
    void ResolvePossessionAtWhistle()
    {
        var carrier = BallController.Instance != null ? BallController.Instance.Carrier : null;
        if (carrier == null || !carrier.TryGetComponent<TeamMember>(out var holder)) return;
        if (holder.teamId == PossessionTeamId) return;

        if (CanTakePossession(holder.teamId)) ApplyPossession(holder.teamId);
    }

    [ContextMenu("Debug: Flip Possession")]
    void DebugFlipPossession() => TrySetPossession(DefendingTeamId);

    // ------------------------------------------------------------------------------

    FormationData ActiveOffensiveFormation =>
        (playCall != null && playCall.OffensiveFormation != null) ? playCall.OffensiveFormation : defaultOffensiveFormation;

    FormationData ActiveDefensiveFormation =>
        defensiveFormationOverride != null ? defensiveFormationOverride : defaultDefensiveFormation;

    public void SetPlayCall(PlayCallData call) => playCall = call;
    public PlayCallData CurrentPlayCall => playCall;

    // --- Gamebreaker state ---
    float offenseMeter;
    float defenseMeter;
    int offensePossessionsRemaining;
    bool defenseGuaranteedTurnoverPending;

    public float OffenseMeter => offenseMeter;
    public float DefenseMeter => defenseMeter;
    public bool IsOffenseGamebreakerActive { get; private set; }

    public event System.Action<float> OnOffenseMeterChanged;
    public event System.Action<float> OnDefenseMeterChanged;
    public event System.Action OnOffenseGamebreakerActivated;
    public event System.Action OnOffenseGamebreakerEnded;

    void Awake()
    {
        Instance = this;
        controls = new InputSystem_Actions();

        PossessionTeamId = startingPossessionTeamId;
        RefreshRosters();

        // initialPlayerZ is authored in attack-axis space (negative = own side), so it
        // lands on the correct half of the field for whichever team starts with the ball.
        nextLineOfScrimmageZ = AttackDirection.FromAxis(initialPlayerZ);

        // No special-cased first play logically — but visually, there's nothing to
        // gather FROM before the first snap, so skip straight to InHuddle instead of
        // animating a walk from wherever the scene happened to place everyone.
        phase = HuddlePhase.InHuddle;
        playClockTimer = playClockDuration;
    }

    void OnEnable()
    {
        controls.Player.ResetPlay.performed += HandleResetPlay;
        controls.Player.Enable();
    }

    void OnDisable()
    {
        controls.Player.ResetPlay.performed -= HandleResetPlay;
        controls.Player.Disable();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        controls?.Dispose();
    }

    void HandleResetPlay(InputAction.CallbackContext _) => ResetPlay();

    void Update()
    {
        if (IsLive)
        {
            if (!passerCrossedLOS && quarterback != null && IsPastLineOfScrimmage(quarterback.position.z))
                passerCrossedLOS = true;
            return;
        }

        if (playClockTimer > 0f)
        {
            playClockTimer -= Time.deltaTime;
            if (playClockTimer <= 0f) ForceThroughPlayClock();
        }
    }

    public void EndPlay(PlayEndReason reason)
    {
        if (!IsLive) return;
        IsLive = false;
        lastEndReason = reason;

        // Possession first — the kickoff-reset LOS below is authored in axis space and
        // needs to be converted using the direction of whoever snaps NEXT.
        ResolvePossessionAtWhistle();

        if (reason == PlayEndReason.Tackled || reason == PlayEndReason.Interception || reason == PlayEndReason.OutOfBounds)
        {
            if (BallController.Instance != null)
            {
                nextLineOfScrimmageZ = BallController.Instance.transform.position.z;
            }
        }
        else if (reason == PlayEndReason.Touchdown || reason == PlayEndReason.Safety)
        {
            nextLineOfScrimmageZ = AttackDirection.FromAxis(kickoffResetZ);
        }

        if (reason == PlayEndReason.Touchdown)
        {
            AddOffensePoints(10f);
            EndOffenseGamebreaker();
        }
        else if (reason == PlayEndReason.Interception || reason == PlayEndReason.Safety)
        {
            EndOffenseGamebreaker();
        }
        
        OnPlayEnded?.Invoke(reason);

        StopAllCoroutines();
        StartCoroutine(RegularPlayEndDelay());
    }

    public void NotifyFumble() => EndOffenseGamebreaker();

    // Entry point for PlayCallSelector's Confirm. Animated, non-blocking — returns
    // immediately, formation coroutine runs in the background. No-op while still
    // walking INTO the huddle (nothing to break yet) or while already live.
    public void BreakHuddle()
    {
        if (IsLive) return;
        if (phase == HuddlePhase.GatheringToHuddle) return;

        StopAllCoroutines();
        StartCoroutine(BreakToFormationRoutine());
    }

    // The actual snap. Only does something once you're actually SET at the line —
    // no more implicit "also break the huddle for you" fallback. Real football
    // doesn't let you snap out of the huddle either.
    public void ResetPlay()
    {
        if (IsLive) return;
        if (phase != HuddlePhase.SetAtLOS) return;
        DoSnap();
    }

    void DoSnap()
    {
        if (IsOffenseGamebreakerActive)
        {
            offensePossessionsRemaining--;
            if (offensePossessionsRemaining <= 0) EndOffenseGamebreaker();
        }

        IsLive = true;
        snapTimestamp = Time.time;
        OnPlayReset?.Invoke();
    }

    // Delay-of-game. Forces whatever step is in progress to complete INSTANTLY
    // (no point animating a panic snap) and goes live immediately after.
    // BreakToFormationRoutine(instant: true) has no yield in its instant path, so it
    // runs fully to completion synchronously before StartCoroutine even returns —
    // phase is already SetAtLOS by the time DoSnap() is called below, regardless of
    // which phase we were stuck in when the clock hit zero.
    void ForceThroughPlayClock()
    {
        StopAllCoroutines();
        StartCoroutine(BreakToFormationRoutine(instant: true));
        DoSnap();
    }

    // Possession team's full offensive unit: QB + everyone in offenseSlotOrder.
    List<Transform> GetOffenseRoster()
    {
        var roster = new List<Transform>();
        if (quarterback != null) roster.Add(quarterback);
        foreach (var t in offensePlayers)
            if (t != null) roster.Add(t);
        return roster;
    }

    // delays the next huddle until after the current play's end animation finishes
    private IEnumerator RegularPlayEndDelay()
    {
        yield return new WaitForSeconds(3f);

        phase = HuddlePhase.GatheringToHuddle;
        playClockTimer = playClockDuration;

        StartCoroutine(GatherToHuddleRoutine());
    }

    IEnumerator GatherToHuddleRoutine()
    {
        phase = HuddlePhase.GatheringToHuddle;
        OnHuddleStarted?.Invoke();

        var members = GetOffenseRoster();
        if (members.Count == 0) { phase = HuddlePhase.InHuddle; yield break; }

        // "Behind the LOS" means behind it from the possession team's point of view.
        Vector3 huddleCenter = new(0f, 1f, AttackDirection.Advance(nextLineOfScrimmageZ, -huddleDistanceBehindLOS));
        var targets = CircleTargets(members.Count, huddleCenter, huddleRadius);

        yield return MoveGroupTo(members, targets, formationMoveSpeed, formationArrivalTolerance, instant: false);

        for (int i = 0; i < members.Count; i++)
        {
            Vector3 dir = huddleCenter - members[i].position;
            if (dir.sqrMagnitude > 0.01f) members[i].rotation = Quaternion.LookRotation(dir.normalized);
        }

        phase = HuddlePhase.InHuddle;
    }

    IEnumerator BreakToFormationRoutine(bool instant = false)
    {
        phase = HuddlePhase.BreakingToFormation;

        // New play cycle — clear the previous play's LOS-crossing latch here, same
        // reasoning as before: re-confirming a different play before the snap
        // shouldn't leave a stale latch from a moment ago.
        passerCrossedLOS = false;

        // Formation offsets are authored for a +Z attacker; ToWorldVector rotates them
        // 180 degrees when the possession team drives the other way.
        FieldDirection dir = AttackDirection;
        Vector3 losOrigin = new(0f, 1f, nextLineOfScrimmageZ);
        var offense = ActiveOffensiveFormation;
        var defense = ActiveDefensiveFormation;

        var members = new List<Transform>();
        var targets = new List<Vector3>();

        if (quarterback != null)
        {
            members.Add(quarterback);
            targets.Add(offense != null
                ? losOrigin + dir.ToWorldVector(offense.qbOffsetFromLOS)
                : new Vector3(0f, quarterback.position.y, nextLineOfScrimmageZ));
        }

        for (int i = 0; i < offensePlayers.Count; i++)
        {
            if (offensePlayers[i] == null) continue;
            members.Add(offensePlayers[i]);
            targets.Add(offense != null && i < offense.offensiveSlots.Count
                ? losOrigin + dir.ToWorldVector(offense.offensiveSlots[i].offsetFromLOS)
                : offensePlayers[i].position); // no slot defined -> hold, don't yank to origin
        }

        for (int i = 0; i < defensePlayers.Count; i++)
        {
            if (defensePlayers[i] == null) continue;
            members.Add(defensePlayers[i]);
            targets.Add(defense != null && i < defense.defenderSlots.Count
                ? losOrigin + dir.ToWorldVector(defense.defenderSlots[i].offsetFromLOS)
                : defensePlayers[i].position);
        }

        yield return MoveGroupTo(members, targets, formationMoveSpeed, formationArrivalTolerance, instant);

        // Offense faces the way it's driving; defense faces the offense. Set for both
        // sides so a team that was just on the other side of the ball doesn't come out
        // of a possession change still facing backward.
        if (quarterback != null) quarterback.rotation = dir.Rotation;
        foreach (var t in offensePlayers)
            if (t != null) t.rotation = dir.Rotation;
        foreach (var t in defensePlayers)
            if (t != null) t.rotation = dir.Opposite.Rotation;

        // Fires only once everyone's actually standing in their final spot —
        // ReceiverAI.SetRoute() calculates route depth from current position, so
        // calling this any earlier (e.g. at the start of the walk) would point every
        // route at the wrong depth.
        AssignRoutes();

        if (BlockingCoordinator.Instance != null)
            BlockingCoordinator.Instance.RegisterBlockers(offensePlayers);

        var selectionUI = FindAnyObjectByType<ReceiverSelectionUI>();
        if (selectionUI != null)
            selectionUI.ResetSelection();

        phase = HuddlePhase.SetAtLOS;
    }

    // Shared walk helper. instant=true assigns final positions directly with zero
    // yields, so a caller can StartCoroutine() it and rely on it being fully complete
    // by the time StartCoroutine() returns (see ForceThroughPlayClock).
    IEnumerator MoveGroupTo(List<Transform> members, List<Vector3> targets, float speed, float tolerance, bool instant)
    {
        if (instant)
        {
            for (int i = 0; i < members.Count && i < targets.Count; i++)
                if (members[i] != null) members[i].position = targets[i];
            yield break;
        }

        bool anyMoving = true;
        while (anyMoving)
        {
            anyMoving = false;
            for (int i = 0; i < members.Count && i < targets.Count; i++)
            {
                if (members[i] == null) continue;
                Vector3 pos = members[i].position;
                if (Vector3.Distance(pos, targets[i]) > tolerance)
                {
                    members[i].position = Vector3.MoveTowards(pos, targets[i], speed * Time.deltaTime);
                    anyMoving = true;
                }
            }
            yield return null;
        }
    }

    static List<Vector3> CircleTargets(int count, Vector3 center, float radius)
    {
        var result = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i * Mathf.Deg2Rad;
            result.Add(center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius);
        }
        return result;
    }

    void AssignRoutes()
    {
        for (int i = 0; i < offensePlayers.Count; i++)
        {
            Transform playerTransform = offensePlayers[i];
            if (!ReceiverTargeting.IsEligible(playerTransform)) continue;

            ReceiverAI receiver = playerTransform.GetComponent<ReceiverAI>();
            if (receiver == null) continue;

            RoutePattern route = playCall != null
                ? playCall.GetRouteForReceiver(i)
                : RoutePattern.Go;
            receiver.SetRoute(route); // None now means "stay and block," not "silently became Go"
        }
    }

    // --- Gamebreaker API (unchanged) ---

    public void AddOffensePoints(float pts)
    {
        offenseMeter = Mathf.Clamp(offenseMeter + pts, 0f, 100f);
        OnOffenseMeterChanged?.Invoke(offenseMeter);
    }

    public void AddDefensePoints(float pts)
    {
        defenseMeter = Mathf.Clamp(defenseMeter + pts, 0f, 100f);
        OnDefenseMeterChanged?.Invoke(defenseMeter);
    }

    public bool TryActivateOffenseGamebreaker()
    {
        if (IsOffenseGamebreakerActive || offenseMeter < 100f) return false;

        IsOffenseGamebreakerActive = true;
        offensePossessionsRemaining = gamebreakerPossessionLimit;
        offenseMeter = 0f;
        OnOffenseMeterChanged?.Invoke(offenseMeter);
        OnOffenseGamebreakerActivated?.Invoke();
        return true;
    }

    void EndOffenseGamebreaker()
    {
        if (!IsOffenseGamebreakerActive) return;
        IsOffenseGamebreakerActive = false;
        OnOffenseGamebreakerEnded?.Invoke();
    }

    void CheckDefenseAutoActivate()
    {
        if (!defenseGuaranteedTurnoverPending && defenseMeter >= 100f)
        {
            defenseGuaranteedTurnoverPending = true;
            defenseMeter = 0f;
            OnDefenseMeterChanged?.Invoke(defenseMeter);
        }
    }

    public bool ConsumeGuaranteedTurnover()
    {
        CheckDefenseAutoActivate();
        if (!defenseGuaranteedTurnoverPending) return false;
        defenseGuaranteedTurnoverPending = false;
        return true;
    }

    public float GetGamebreakerMult(AttributeStat stat)
    {
        if (!IsOffenseGamebreakerActive || gamebreakerBuffs == null) return 1f;
        return gamebreakerBuffs.Get(stat);
    }
}
