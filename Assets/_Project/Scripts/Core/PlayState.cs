using System.Collections.Generic;
using UnityEngine;

public class PlayState : MonoBehaviour
{
    public enum PlayEndReason { Tackled, Touchdown, Interception, Incomplete }

    public static PlayState Instance { get; private set; }

    [SerializeField] Transform player;
    [SerializeField] float initialPlayerZ = -5f;
    [SerializeField] float kickoffResetZ = -5f;

    // Defenders list stays as-is — these are live scene object references, unavoidable
    // per-scene setup. What changes is where their reset OFFSETS come from: authored
    // once in a FormationData asset instead of duplicated per-PlayState-instance data entry.
    [SerializeField] List<Transform> defenders = new List<Transform>();
    [SerializeField] FormationData formation;

    // Same by-index convention as defenders — offensivePlayers[i] gets
    // offensiveFormation.receiverSlots[i]'s offset. The passer (UserPlayer) is NOT in
    // this list; it's repositioned separately via offensiveFormation.passerOffsetFromLOS,
    // since it isn't interchangeable with the receiver slots.
    [SerializeField] List<Transform> offensivePlayers = new List<Transform>();
    [SerializeField] OffensiveFormationData offensiveFormation;

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

    void Awake()
    {
        Instance = this;
        controls = new InputSystem_Actions();
        nextLineOfScrimmageZ = initialPlayerZ;
    }

    void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.ResetPlay.performed += ctx => ResetPlay();
    }

    void OnDisable() => controls.Player.Disable();

    public void EndPlay(PlayEndReason reason)
    {
        if (!IsLive) return;
        IsLive = false;
        lastEndReason = reason;

        // Reads the ACTUAL ball carrier's position, not a hardcoded reference to
        // UserPlayer. This was the root cause of the whole team resetting to the wrong
        // line of scrimmage: once a pass or pitch moves the ball to a receiver,
        // player.position no longer has anything to do with where the ball ended up —
        // it's just wherever the passer happens to be standing. Same "resolve live,
        // don't cache" fix already applied to DefenderAI/DefenderCoordinator/
        // CameraFollow/TouchdownZone; this was the one place it hadn't landed yet.
        if ((reason == PlayEndReason.Tackled || reason == PlayEndReason.Interception)
            && BallController.Instance != null && BallController.Instance.Carrier != null)
        {
            nextLineOfScrimmageZ = BallController.Instance.Carrier.position.z;
        }
        // Touchdown doesn't touch nextLineOfScrimmageZ here — handled in ResetPlay via kickoffResetZ instead

        OnPlayEnded?.Invoke(reason);
    }

    public void ResetPlay()
    {
        if (IsLive) return;

        float resetZ = lastEndReason == PlayEndReason.Touchdown ? kickoffResetZ : nextLineOfScrimmageZ;
        Vector3 losOrigin = new Vector3(0f, 1f, resetZ);

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

        // Same by-index convention, offense side. Positioning now lives here instead of
        // inside ReceiverAI — one authority for "where does everyone line up," matching
        // exactly how defenders already work.
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

        IsLive = true;
        OnPlayReset?.Invoke(); // fires AFTER positions are set — ReceiverAI's route-reset logic depends on this ordering
    }
}