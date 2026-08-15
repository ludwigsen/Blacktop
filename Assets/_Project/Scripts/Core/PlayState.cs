using System.Collections.Generic;
using UnityEngine;

public class PlayState : MonoBehaviour
{
    public enum PlayEndReason { Tackled, Touchdown }

    public static PlayState Instance { get; private set; }

    [SerializeField] Transform player;
    [SerializeField] float initialPlayerZ = -5f;
    [SerializeField] float kickoffResetZ = -5f; // where the next play starts after a score — separate from initialPlayerZ in case you want them to differ later (e.g. touchback rules)
    [SerializeField] List<Transform> defenders = new List<Transform>();
    [SerializeField] List<Vector3> defenderStartOffsetsFromLOS = new List<Vector3>();

    public bool IsLive { get; private set; } = true;

    // Now passes the reason — subscribers (UI, scoring, future systems) can react
    // differently to a tackle vs. a touchdown instead of treating every stoppage the same.
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

        for (int i = 0; i < defenders.Count && i < defenderStartOffsetsFromLOS.Count; i++)
        {
            if (defenders[i] == null) continue;
            defenders[i].position = new Vector3(0f, 1f, resetZ) + defenderStartOffsetsFromLOS[i];
        }

        if (lastEndReason == PlayEndReason.Touchdown)
            nextLineOfScrimmageZ = kickoffResetZ; // keep this in sync so a subsequent tackle-based reset (if reset is somehow called twice) still has a sane fallback

        IsLive = true;
        OnPlayReset?.Invoke();
    }
}