using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayState : MonoBehaviour
{
    public enum PlayEndReason { Tackled, Touchdown, Interception, Incomplete, OutOfBounds }

    public static PlayState Instance { get; private set; }

    [SerializeField] Transform player;
    [SerializeField] float initialPlayerZ = -5f;
    [SerializeField] float kickoffResetZ = -5f;

    // Defenders list stays as-is — these are live scene object references, unavoidable
    // per-scene setup. What changes is where their reset OFFSETS come from: authored
    // once in a FormationData asset instead of duplicated per-PlayState-instance data entry.
    [SerializeField] List<Transform> defenders = new();
    [SerializeField] FormationData formation;

    // Same by-index convention as defenders — offensivePlayers[i] gets whichever
    // OffensiveFormationData is ACTIVE for the current play (see ActiveFormation below),
    // slot i's offset. The passer (UserPlayer) is NOT in this list; it's repositioned
    // separately via passerOffsetFromLOS, since it isn't interchangeable with the
    // receiver slots.
    [SerializeField] List<Transform> offensivePlayers = new();

    // Fallback formation used when the current PlayCallData doesn't specify its own —
    // NOT the only formation in play anymore. See ActiveFormation.
    [SerializeField] OffensiveFormationData offensiveFormation;

    [Header("Gamebreaker")]
    [SerializeField] GamebreakerBuffs gamebreakerBuffs;
    [SerializeField] int gamebreakerPossessionLimit = 3;

    [SerializeField] PlayCallData playCall;

    // Starts dead. There is no special-cased "opening play" anymore — the first snap
    // of a session goes through ResetPlay() exactly like every other one, which is what
    // makes the play-selection HUD, route assignment, AND blocker registration all fire
    // correctly before ANY play, instead of only from play #2 onward.
    public bool IsLive { get; private set; } = false;
    public event System.Action<PlayEndReason> OnPlayEnded;
    public event System.Action OnPlayReset;

    InputSystem_Actions controls;
    float nextLineOfScrimmageZ;
    // Exposed so DefenderCoordinator (and potentially other systems later) can compare
    // the ball carrier's live position against the current line of scrimmage without
    // PlayState needing to know anything about defender logic itself.
    public float CurrentLineOfScrimmageZ => nextLineOfScrimmageZ;
    PlayEndReason lastEndReason;

    // Exposed so TackleContact can identify "the passer" for sack scoring — only the
    // designated passer (UserPlayer) getting tackled behind the LOS counts as a sack,
    // not any teammate who happens to be carrying after a pitch/pass downfield.
    public Transform Passer => player;

    // Public read-only access to offensive players for UI/route assignment
    public List<Transform> OffensivePlayers => offensivePlayers;

    // Whichever formation actually governs the CURRENT play — the play call's own
    // formation if it specifies one, otherwise PlayState's fallback. This is what
    // fixes formation-per-play: previously ResetPlay() read the fallback field
    // unconditionally and playCall.OffensiveFormation was dead data nobody consumed.
    OffensiveFormationData ActiveFormation =>
        (playCall != null && playCall.OffensiveFormation != null) ? playCall.OffensiveFormation : offensiveFormation;

    // Assigned by PlayCallSelector (or any future playbook UI) before the next snap.
    // Takes effect the next time ResetPlay() runs.
    public void SetPlayCall(PlayCallData call) => playCall = call;

    // --- Gamebreaker state ---
    // Offense meter fills via GamebreakerController (Styling, continuous) and point-award
    // hooks scattered through the move scripts (Juke/Hurdle/StiffArm) plus Touchdown
    // (handled inline below, since PlayState already owns EndPlay). Defense meter fills
    // via TackleContact (sack) and BallController (interception/forced fumble). Both are
    // 0-100; activation consumes the respective meter to zero.
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
    }

    // No Start() override anymore. The first play is no longer special-cased into
    // calling AssignRoutes() directly while IsLive sits true from frame one — it now
    // waits for the player's first Reset Play (R) press, same as every subsequent down.
    // This is also what was silently skipping BlockingCoordinator.RegisterBlockers()
    // on the opening play — that call lives in ResetPlay() below, so it's now guaranteed
    // to run before ANY play, not just play #2 onward.

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

    public void EndPlay(PlayEndReason reason)
    {
        if (!IsLive) return;
        IsLive = false;
        lastEndReason = reason;

        // Reads the ACTUAL ball carrier's position, not a hardcoded reference to
        // UserPlayer. Same "resolve live, don't cache" rule already applied to
        // DefenderAI/DefenderCoordinator/CameraFollow/TouchdownZone.
        if (reason == PlayEndReason.Tackled || reason == PlayEndReason.Interception || reason == PlayEndReason.OutOfBounds)
        {
            // Out of bounds spots the ball where it crossed, not where the player is standing —
            // matters once a receiver can go OOB independent of the ball carrier's position.
            // Tackled/Interception keep the existing player-position behavior untouched.
            nextLineOfScrimmageZ = (reason == PlayEndReason.OutOfBounds && BallController.Instance != null)
                ? BallController.Instance.transform.position.z
                : player.position.z;
        }
        // Touchdown doesn't touch nextLineOfScrimmageZ here — handled in ResetPlay via kickoffResetZ instead

        // Touchdown and Interception both end offensive Gamebreaker outright — a score or
        // a turnover closes the window regardless of possessions remaining. A fumble ends
        // it too, but that's signaled separately via NotifyFumble() since a fumble doesn't
        // change PlayEndReason (still resolves as Tackled per TackleContact's design).
        if (reason == PlayEndReason.Touchdown)
        {
            AddOffensePoints(10f); // flat TD bonus, not Swagger-scaled — a score is a score regardless of style
            EndOffenseGamebreaker();
        }
        else if (reason == PlayEndReason.Interception)
        {
            EndOffenseGamebreaker();
        }

        OnPlayEnded?.Invoke(reason);
    }

    // Called by TackleContact the instant a fumble roll succeeds — turnover-by-fumble ends
    // offensive Gamebreaker immediately, same as an interception, even though the play
    // itself still resolves as PlayEndReason.Tackled.
    public void NotifyFumble() => EndOffenseGamebreaker();

    public void ResetPlay()
    {
        if (IsLive) return;

        float resetZ = lastEndReason == PlayEndReason.Touchdown ? kickoffResetZ : nextLineOfScrimmageZ;
        Vector3 losOrigin = new(0f, 1f, resetZ);

        var activeFormation = ActiveFormation;

        if (player != null)
        {
            player.position = activeFormation != null
                ? losOrigin + activeFormation.passerOffsetFromLOS
                : new Vector3(0f, player.position.y, resetZ);
        }

        if (formation != null)
        {
            for (int i = 0; i < defenders.Count && i < formation.defenderSlots.Count; i++)
            {
                if (defenders[i] == null) continue;
                defenders[i].position = losOrigin + formation.defenderSlots[i].offsetFromLOS;
            }
        }

        if (activeFormation != null)
        {
            for (int i = 0; i < offensivePlayers.Count && i < activeFormation.receiverSlots.Count; i++)
            {
                if (offensivePlayers[i] == null) continue;
                offensivePlayers[i].position = losOrigin + activeFormation.receiverSlots[i].offsetFromLOS;
            }
        }

        if (lastEndReason == PlayEndReason.Touchdown)
            nextLineOfScrimmageZ = kickoffResetZ; // keep this in sync so a subsequent tackle-based reset (if reset is somehow called twice) still has a sane fallback

        if (IsOffenseGamebreakerActive)
        {
            offensePossessionsRemaining--;
            if (offensePossessionsRemaining <= 0) EndOffenseGamebreaker();
        }

        IsLive = true;
        OnPlayReset?.Invoke();
        AssignRoutes();

        if (BlockingCoordinator.Instance != null)
        {
            BlockingCoordinator.Instance.RegisterBlockers(offensivePlayers);
        }

        // Reset receiver selection UI
        var selectionUI = FindAnyObjectByType<ReceiverSelectionUI>();
        if (selectionUI != null)
            selectionUI.ResetSelection();
    }

    void AssignRoutes()
    {
        for (int i = 0; i < offensivePlayers.Count; i++)
        {
            Transform playerTransform = offensivePlayers[i];
            if (!ReceiverTargeting.IsEligible(playerTransform)) continue;

            ReceiverAI receiver = playerTransform.GetComponent<ReceiverAI>();
            if (receiver == null) continue;

            // A play call can replace the prototype go route. Until one is assigned,
            // every eligible target runs a straight-upfield route for pass testing.
            RoutePattern route = playCall != null
                ? playCall.GetRouteForReceiver(i)
                : RoutePattern.Go;
            receiver.SetRoute(route == RoutePattern.None ? RoutePattern.Go : route);
        }
    }

    // --- Gamebreaker API ---

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

    // Defense has no human-controlled activation path yet — there's no player-controllable
    // defender in the project. This auto-arms the guaranteed-turnover flag the instant the
    // meter caps, consumed by whichever fumble/interception roll happens next. Replace with
    // a real input-driven TryActivateDefenseGamebreaker() once defense is human-playable;
    // the flag/consumption plumbing below already supports it as-is.
    void CheckDefenseAutoActivate()
    {
        if (!defenseGuaranteedTurnoverPending && defenseMeter >= 100f)
        {
            defenseGuaranteedTurnoverPending = true;
            defenseMeter = 0f;
            OnDefenseMeterChanged?.Invoke(defenseMeter);
        }
    }

    // Consumed by TackleContact's fumble roll and BallController's interception roll —
    // whichever physical contest happens next after the meter caps wins automatically.
    // One-shot: calling this clears the flag, so only that single next contest is guaranteed.
    public bool ConsumeGuaranteedTurnover()
    {
        CheckDefenseAutoActivate();
        if (!defenseGuaranteedTurnoverPending) return false;
        defenseGuaranteedTurnoverPending = false;
        return true;
    }

    // Multiplicative stat buff while offensive Gamebreaker is active — returns 1f (no-op)
    // otherwise. Callers pass this straight into PlayerAttributes.Speed(field, gb) etc.,
    // same pattern as FieldModifiers, just multiplicative instead of additive per design.
    public float GetGamebreakerMult(AttributeStat stat)
    {
        if (!IsOffenseGamebreakerActive || gamebreakerBuffs == null) return 1f;
        return gamebreakerBuffs.Get(stat);
    }
}