using UnityEngine;

// First real "game state" concept in the project — everything before this (movement,
// moves, defender) operated with no notion of a play being active or over. This is
// intentionally minimal: two states, one event. Down/distance, scoring, play-calling
// all build on top of this later, but none of that belongs here yet.
public class PlayState : MonoBehaviour
{
    public static PlayState Instance { get; private set; } // simple singleton — fine at this scale, revisit if the project grows multiple concurrent "plays" (unlikely for this game type)

    public bool IsLive { get; private set; } = true;

    // Other systems (player, defender, UI) subscribe to this rather than polling IsLive
    // every frame — cheaper and keeps the "what happens on tackle" logic decoupled from
    // this class knowing about players/UI/etc.
    public event System.Action OnPlayEnded;

    void Awake()
    {
        Instance = this;
    }

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
        IsLive = true;
    }
}