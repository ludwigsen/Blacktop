using System.Collections.Generic;
using UnityEngine;

public class PlayState : MonoBehaviour
{
    public enum PlayEndReason { Tackled, Touchdown }

    public static PlayState Instance { get; private set; }

    [SerializeField] Transform player;
    [SerializeField] float initialPlayerZ = -5f;
    [SerializeField] float kickoffResetZ = -5f;

    // Defenders list stays as-is — these are live scene object references, unavoidable
    // per-scene setup. What changes is where their reset OFFSETS come from: authored
    // once in a FormationData asset instead of duplicated per-PlayState-instance data entry.
    [SerializeField] List<Transform> defenders = new List<Transform>();
    [SerializeField] FormationData formation;

    public bool IsLive { get; private set; } = true;
    public event System.Action<PlayEndReason> OnPlayEnded;
    public event System.Action OnPlayReset;

    InputSystem_Actions controls;
    float nextLineOfScrimmageZ;
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

        if (reason == PlayEndReason.Tackled && player != null)
        {
            nextLineOfScrimmageZ = player.position.z; // next play starts where the tackle happened
        }
        // Touchdown doesn't touch nextLineOfScrimmageZ here — handled in ResetPlay via kickoffResetZ instead

        OnPlayEnded?.Invoke(reason);
    }

    public void ResetPlay()
    {
        if (IsLive) return;

        float resetZ = lastEndReason == PlayEndReason.Touchdown ? kickoffResetZ : nextLineOfScrimmageZ;

        if (player != null)
        {
            Vector3 pos = player.position;
            pos.x = 0f;
            pos.z = resetZ;
            player.position = pos;
        }

        // Matched by INDEX — defenders[0] gets formation.defenderSlots[0]'s offset, etc.
        // Same sync-by-index contract as before, but now count mismatches are visible in
        // ONE place (the formation asset) rather than two duplicated lists drifting apart.
        if (formation != null)
        {
            for (int i = 0; i < defenders.Count && i < formation.defenderSlots.Count; i++)
            {
                if (defenders[i] == null) continue;
                defenders[i].position = new Vector3(0f, 1f, resetZ) + formation.defenderSlots[i].offsetFromLOS;
            }
        }

        if (lastEndReason == PlayEndReason.Touchdown)
            nextLineOfScrimmageZ = kickoffResetZ; // keep this in sync so a subsequent tackle-based reset (if reset is somehow called twice) still has a sane fallback

        IsLive = true;
        OnPlayReset?.Invoke();
    }
}