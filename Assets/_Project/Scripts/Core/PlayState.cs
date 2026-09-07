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

    // Same by-index convention as defenders — offensivePlayers[i] gets
    // offensiveFormation.receiverSlots[i]'s offset. The passer (UserPlayer) is NOT in
    // this list; it's repositioned separately via offensiveFormation.passerOffsetFromLOS,
    // since it isn't interchangeable with the receiver slots.
    [SerializeField] List<Transform> offensivePlayers = new();
    [SerializeField] OffensiveFormationData offensiveFormation;

    [Header("Gamebreaker")]
    [SerializeField] GamebreakerBuffs gamebreakerBuffs;
    [SerializeField] int gamebreakerPossessionLimit = 3;

    public bool IsLive { get; private set; } = true;
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

        if (player != null)
        {
            player.position = offensiveFormation != null
                ? losOrigin + offensiveFormation.passerOffsetFromLOS
                : new Vector3(0f, player.position.y, resetZ); // fallback if no formation asset assigned yet
        }

        // Matched by INDEX — defenders[0] gets formation.defenderSlots[0]'s offset, etc.
        // Same sync-by-index contract as before, but now count mismatches are visible in
        // ONE place (the formation asset) rather than two duplicated lists drifting apart.
        if (formation != null)
        {
            for (int i = 0; i < defenders.Count && i < formation.defenderSlots.Count; i++)
            {
                if (defenders[i] == null) continue;
                defenders[i].position = losOrigin + formation.defenderSlots[i].offsetFromLOS;
            }
        }

        // Same by-index convention, offense side. Positioning lives here instead of inside
        // ReceiverAI — one authority for "where does everyone line up," matching exactly
        // how defenders already work.
        if (offensiveFormation != null)
        {
            for (int i = 0; i < offensivePlayers.Count && i < offensiveFormation.receiverSlots.Count; i++)
            {
                if (offensivePlayers[i] == null) continue;
                offensivePlayers[i].position = losOrigin + offensiveFormation.receiverSlots[i].offsetFromLOS;
            }
        }

        if (lastEndReason == PlayEndReason.Touchdown)
            nextLineOfScrimmageZ = kickoffResetZ; // keep this in sync so a subsequent tackle-based reset (if reset is somehow called twice) still has a sane fallback

        // "Lasts 3 possessions" — this project has no multi-play drive concept (every stop
        // is effectively a turnover-on-downs), so a possession is approximated as one play
        // (one ResetPlay call). Decremented here, AFTER the play that just ended, so an
        // activation made mid-play still gets the full count starting from the next snap.
        if (IsOffenseGamebreakerActive)
        {
            offensePossessionsRemaining--;
            if (offensePossessionsRemaining <= 0) EndOffenseGamebreaker();
        }

        IsLive = true;
        OnPlayReset?.Invoke(); // fires AFTER positions are set — ReceiverAI's route-reset logic depends on this ordering

        // Register offensive players with BlockingCoordinator so they get fresh
        // target assignments starting this play.
        if (BlockingCoordinator.Instance != null)
        {
            BlockingCoordinator.Instance.RegisterBlockers(offensivePlayers);
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
