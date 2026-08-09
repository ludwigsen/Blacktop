using UnityEngine;

// First real "game state" concept in the project — everything before this (movement,
// moves, defender) operated with no notion of a play being active or over. This is
// intentionally minimal: two states, one event. Down/distance, scoring, play-calling
// all build on top of this later, but none of that belongs here yet.
public class PlayState : MonoBehaviour
{
    public static PlayState Instance { get; private set; }

    [SerializeField] Transform player;
    [SerializeField] Transform defender;
    [SerializeField] Vector3 playerStartPos = new Vector3(0f, 1f, -5f);
    [SerializeField] Vector3 defenderStartPos = new Vector3(0f, 1f, 5f);

    public bool IsLive { get; private set; } = true;

    // Other systems (player, defender, UI) subscribe to this rather than polling IsLive
    // every frame — cheaper and keeps the "what happens on tackle" logic decoupled from
    // this class knowing about players/UI/etc.
    public event System.Action OnPlayEnded;
    public event System.Action OnPlayReset; // new — lets subscribers re-arm themselves (state machine re-enable, etc.)

    InputSystem_Actions controls;

    void Awake()
    {
        Instance = this;
        controls = new InputSystem_Actions();
    }

    void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.ResetPlay.performed += ctx => ResetPlay();
    }

    void OnDisable() => controls.Player.Disable();

    public void EndPlay()
    {
        if (!IsLive) return; // guard against double-firing if multiple colliders trigger contact in the same frame
        IsLive = false;
        OnPlayEnded?.Invoke();
    }

    // Called later by a "reset/next play" flow — not wired to anything yet, but the
    // state needs a documented way back to Live or it's a dead end after one tackle.
    public void ResetPlay()
    {
        if (IsLive) return; // only makes sense to reset a dead play — no-op if called mid-live-play by accident

        // Snap positions back — deliberately teleport, not lerp/animate. This is a debug/test
        // convenience, not a "next play" presentation moment; that's a much later polish pass.
        if (player != null) player.position = playerStartPos;
        if (defender != null) defender.position = defenderStartPos;

        IsLive = true;
        OnPlayReset?.Invoke();
    }
}