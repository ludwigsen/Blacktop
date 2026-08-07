using UnityEngine;

// Short forward shove — reads as "breaking through" rather than repositioning (that's what
// Juke is for). Front-loaded curve: most displacement happens immediately, then trails off,
// mimicking a push-then-recover motion rather than a smooth glide.
//
// No opponent/tackle collision system exists yet — this currently just moves the player.
// Once tackling exists, Tick() is where a "stiff arm active" flag should get exposed so
// nearby tackle attempts can check it and get shrugged off / knocked back instead of
// landing normally. Left as a placeholder rather than guessing at that API now.
[System.Serializable]
public class StiffArmMove : IPlayerMove
{
    [SerializeField] AnimationCurve curve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -4f), // starts at full value, steep negative out-tangent for a fast initial burst
        new Keyframe(1f, 0f, -1f, 0f)  // decays to 0, gentle in-tangent so it trails off rather than stopping abruptly
    );
    [SerializeField] float baseDuration = 0.25f;
    [SerializeField] float baseForwardBurst = 2.5f;
    [SerializeField] float cooldownDuration = 0.3f;

    float timer, lastSample;
    PlayerAttributes attr;

    float Duration => baseDuration;
    float ForwardBurst => baseForwardBurst * attr.speedMult;

    public bool CanTrigger(PlayerContext ctx, PlayerState currentState)
        => currentState == PlayerState.Idle || currentState == PlayerState.Walk || currentState == PlayerState.Run;

    public void Enter(PlayerContext ctx)
    {
        attr = ctx.attributes;
        timer = 0f;
        lastSample = 0f;
    }

    public void Tick(PlayerContext ctx, float deltaTime)
    {
        timer += deltaTime;
        float t = Mathf.Clamp01(timer / Duration);
        float sample = curve.Evaluate(t);
        float delta = sample - lastSample;
        lastSample = sample;

        ctx.transform.position += ctx.transform.forward * ForwardBurst * delta;
    }

    public bool IsComplete => timer >= Duration;

    public void Exit(PlayerContext ctx) => ctx.setCooldown(cooldownDuration);
}