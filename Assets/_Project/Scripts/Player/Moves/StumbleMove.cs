using UnityEngine;

[System.Serializable]
public sealed class StumbleMove : IPlayerMove
{
    [SerializeField, Min(0.01f)] float duration = 0.32f;
    [SerializeField, Min(0f)] float knockbackDistance = 0.85f;

    Vector3 direction;
    float elapsed;

    public float Distance => Mathf.Max(0f, knockbackDistance);
    public float Progress => Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
    public bool IsComplete => Progress >= 1f;

    // The state machine supplies the polled contact. There is no input binding or
    // tackle immunity for this move.
    public bool CanTrigger(PlayerContext ctx, PlayerState state) => state != PlayerState.Stumble;
    public void SetContactNormal(Vector3 normal) => direction = normal;
    public void Enter(PlayerContext ctx) => elapsed = 0f;

    public void Tick(PlayerContext ctx, float deltaTime)
    {
        // Integrate an ease-out by subtracting samples so total recoil distance
        // is independent of the number or sizes of frames.
        float before = 1f - Progress;
        elapsed += deltaTime;
        float after = 1f - Progress;
        ctx.moveStumble(direction * ((before * before - after * after) * Distance));
    }

    public void Exit(PlayerContext ctx) { }
    public void Cancel(PlayerContext ctx) { }
}
