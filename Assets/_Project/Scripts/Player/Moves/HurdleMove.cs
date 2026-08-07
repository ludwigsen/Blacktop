using UnityEngine;

[System.Serializable]
public class HurdleMove : IPlayerMove
{
    [SerializeField] AnimationCurve heightCurve;
    [SerializeField] float baseDuration = 0.5f;
    [SerializeField] float basePeakHeight = 2.0f; // bumped from 1.4 — needs to read clearly as a jump
    [SerializeField] float baseForwardDistance = 4.5f; // bumped from 3
    [SerializeField] float cooldownDuration = 0.2f;

    float timer, lastHeightSample, lastForwardSample;
    PlayerAttributes attr;

    float PeakHeight => basePeakHeight * attr.hurdleMult;
    float Duration => baseDuration;

    // Hurdle now requires a defender actually in front of you — this is what turns it from
    // "a hop button" into "a reactive move." Standing-still hurdles are still allowed (per
    // earlier requirement) as long as something's in range to hurdle over/past.
    public bool CanTrigger(PlayerContext ctx, PlayerState currentState)
        => (currentState == PlayerState.Idle || currentState == PlayerState.Walk || currentState == PlayerState.Run)
        && ctx.isDefenderInRange();

    public void Enter(PlayerContext ctx)
    {
        attr = ctx.attributes;
        timer = 0f;
        lastHeightSample = 0f;
        lastForwardSample = 0f;
    }

    public void Tick(PlayerContext ctx, float deltaTime)
    {
        timer += deltaTime;
        float t = Mathf.Clamp01(timer / Duration);

        float heightSample = heightCurve.Evaluate(t);
        float forwardSample = t;

        float heightDelta = (heightSample - lastHeightSample) * PeakHeight;
        float forwardDelta = (forwardSample - lastForwardSample) * baseForwardDistance;
        lastHeightSample = heightSample;
        lastForwardSample = forwardSample;

        ctx.transform.position += ctx.transform.forward * forwardDelta + Vector3.up * heightDelta;
    }

    public bool IsComplete => timer >= Duration;

    public void Exit(PlayerContext ctx) => ctx.setCooldown(cooldownDuration);
}