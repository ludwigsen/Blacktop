using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayState : MonoBehaviour
{
    public enum PlayEndReason { Tackled, Touchdown, Interception, Incomplete, OutOfBounds, Safety }

    public static PlayState Instance { get; private set; }

    [SerializeField] Transform player;
    [SerializeField] float initialPlayerZ = -5f;
    [SerializeField] float kickoffResetZ = -5f;

    [SerializeField] List<Transform> defenders = new();
    [SerializeField] FormationData defaultDefensiveFormation;

    [SerializeField] List<Transform> offensivePlayers = new();
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

    InputSystem_Actions controls;
    float nextLineOfScrimmageZ;
    public float CurrentLineOfScrimmageZ => nextLineOfScrimmageZ;
    PlayEndReason lastEndReason;

    public Transform Passer => player;
    public List<Transform> OffensivePlayers => offensivePlayers;

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
        nextLineOfScrimmageZ = initialPlayerZ;

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
            if (!passerCrossedLOS && player != null && player.position.z > nextLineOfScrimmageZ)
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

        if (reason == PlayEndReason.Tackled || reason == PlayEndReason.Interception || reason == PlayEndReason.OutOfBounds)
        {
            if (BallController.Instance != null)
            {
                nextLineOfScrimmageZ = BallController.Instance.transform.position.z;
            }
        }
        else if (reason == PlayEndReason.Touchdown || reason == PlayEndReason.Safety)
        {
            nextLineOfScrimmageZ = kickoffResetZ;
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

    List<Transform> GetOffenseRoster()
    {
        var roster = new List<Transform>();
        if (player != null) roster.Add(player);
        foreach (var t in offensivePlayers)
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

        var members = GetOffenseRoster();
        if (members.Count == 0) { phase = HuddlePhase.InHuddle; yield break; }

        Vector3 huddleCenter = new(0f, 1f, nextLineOfScrimmageZ - huddleDistanceBehindLOS);
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

        Vector3 losOrigin = new(0f, 1f, nextLineOfScrimmageZ);
        var offense = ActiveOffensiveFormation;
        var defense = ActiveDefensiveFormation;

        var members = new List<Transform>();
        var targets = new List<Vector3>();

        if (player != null)
        {
            members.Add(player);
            targets.Add(offense != null ? losOrigin + offense.qbOffsetFromLOS : new Vector3(0f, player.position.y, nextLineOfScrimmageZ));
        }

        for (int i = 0; i < offensivePlayers.Count; i++)
        {
            if (offensivePlayers[i] == null) continue;
            members.Add(offensivePlayers[i]);
            targets.Add(offense != null && i < offense.offensiveSlots.Count
                ? losOrigin + offense.offensiveSlots[i].offsetFromLOS
                : offensivePlayers[i].position); // no slot defined -> hold, don't yank to origin
        }

        for (int i = 0; i < defenders.Count; i++)
        {
            if (defenders[i] == null) continue;
            members.Add(defenders[i]);
            targets.Add(defense != null && i < defense.defenderSlots.Count
                ? losOrigin + defense.defenderSlots[i].offsetFromLOS
                : defenders[i].position);
        }

        yield return MoveGroupTo(members, targets, formationMoveSpeed, formationArrivalTolerance, instant);

        if (player != null) player.rotation = Quaternion.identity;
        foreach (var t in offensivePlayers)
            if (t != null) t.rotation = Quaternion.identity;

        // Fires only once everyone's actually standing in their final spot —
        // ReceiverAI.SetRoute() calculates route depth from current position, so
        // calling this any earlier (e.g. at the start of the walk) would point every
        // route at the wrong depth.
        AssignRoutes();

        if (BlockingCoordinator.Instance != null)
            BlockingCoordinator.Instance.RegisterBlockers(offensivePlayers);

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
        for (int i = 0; i < offensivePlayers.Count; i++)
        {
            Transform playerTransform = offensivePlayers[i];
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