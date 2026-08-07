using UnityEngine;

// Lateral cut — the "planted foot" move. Direction locks in at trigger time based on
// held input, NOT re-read during the move, so players can't re-aim mid-juke.
//
// Movement is PURE lateral (along the player's local right/left axis) — no forward
// component mixed in. This keeps the move reading as a clean east-west cut relative to
// wherever the player is currently facing, rather than a diagonal drift. Forward progress
// during a juke comes from momentum carrying over naturally once the move ends and normal
// locomotion resumes — Juke itself doesn't need to simulate forward motion.
[System.Serializable]
public class JukeMove : IPlayerMove
{
    [SerializeField] AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] float baseDuration = 0.3f;
    [SerializeField] float baseLateralDistance = 3.5f;
    [SerializeField] float cooldownDuration = 0.2f;

    float timer, lastSample;
    Vector3 direction;
    PlayerAttributes attr;

    float Duration => baseDuration / attr.agilityMult;
    float LateralDistance => baseLateralDistance * attr.agilityMult;

    public bool CanTrigger(PlayerContext ctx, PlayerState currentState)
        => currentState == PlayerState.Idle || currentState == PlayerState.Walk || currentState == PlayerState.Run;

    public void Enter(PlayerContext ctx)
    {
        attr = ctx.attributes;
        timer = 0f;
        lastSample = 0f;

        // transform.right is LOCAL to the player's current facing — this is what makes the
        // juke "east-west relative to the asset's orientation," not relative to world/plane
        // axes. If the player is facing diagonally, the juke still cuts cleanly perpendicular
        // to that facing, not perpendicular to the field.
        float inputX = ctx.getMoveInput().x;
        direction = ctx.transform.right * Mathf.Sign(inputX == 0 ? 1 : inputX);
    }

    public void Tick(PlayerContext ctx, float deltaTime)
    {
        timer += deltaTime;
        float t = Mathf.Clamp01(timer / Duration);
        float sample = curve.Evaluate(t);
        float delta = sample - lastSample;
        lastSample = sample;

        // Lateral-only displacement — no transform.forward blended in here (that was the
        // source of the diagonal drift). Pure east-west cut relative to player facing.
        ctx.transform.position += direction * LateralDistance * delta;
    }

    public bool IsComplete => timer >= Duration;

    public void Exit(PlayerContext ctx) => ctx.setCooldown(cooldownDuration);
}