using UnityEngine;

// Vertical arc, now fires unconditionally on press — same triggering philosophy as
// Juke/StiffArm ("press it, commit to the animation"). Proximity to a defender no longer
// gates WHETHER Hurdle fires; it only gates whether Hurdle gets a shot at negating an
// incoming tackle for the move's duration. Duration stays FIXED regardless of attributes
// (unlike Juke) — height/reach scale with hurdleMult instead, keeping timing stable for
// eventual animation-clip syncing.
[System.Serializable]
public class HurdleMove : IPlayerMove
{
    [SerializeField] AnimationCurve heightCurve; // author as 0→1→0 arc shape in Inspector — quick rise, hang, faster drop reads best
    [SerializeField] float baseDuration = 0.5f;
    [SerializeField] float basePeakHeight = 2.0f;
    [SerializeField] float baseForwardDistance = 4.5f;
    [SerializeField] float cooldownDuration = 0.2f;

    // Tackle-negation roll settings — checked once at Enter, not every Tick. A hurdle
    // either "wins" the negation for its whole duration or it doesn't; rerolling per-frame
    // would make longer hurdles disproportionately safer, which isn't the intent.
    [SerializeField] float negateCheckRadius = 2f;
    [SerializeField] float baseNegateChance = 0.2f; // flat 20% base, scaled by hurdleMult
    [SerializeField] string defenderTag = "Defender";

    float timer, lastHeightSample, lastForwardSample;
    PlayerAttributes attr;

    float PeakHeight => basePeakHeight * attr.Agility();
    float Duration => baseDuration;

    public bool CanTrigger(PlayerContext ctx, PlayerState currentState)
        => currentState == PlayerState.Idle || currentState == PlayerState.Walk || currentState == PlayerState.Run;

    public void Enter(PlayerContext ctx)
    {
        attr = ctx.attributes;
        timer = 0f;
        lastHeightSample = 0f;
        lastForwardSample = 0f;

        // Check for a nearby defender ONCE, at trigger time — this is what the negation
        // roll is conditioned on, per design: "20% + multiplier chance IF within 2f of
        // the tackling defender." Not a gate on whether Hurdle fires at all.
        bool defenderClose = false;
        Collider[] nearby = Physics.OverlapSphere(ctx.transform.position, negateCheckRadius);
        foreach (var c in nearby)
        {
            if (c.CompareTag(defenderTag)) { defenderClose = true; break; }
        }

        if (defenderClose)
        {
            float netChance = Mathf.Clamp01(baseNegateChance * attr.Agility());
            bool negates = Random.value < netChance;
            ctx.setTackleImmune(negates);
        }
        else
        {
            ctx.setTackleImmune(false); // no defender in range — nothing to negate, immunity stays off
        }
    }

    public void Tick(PlayerContext ctx, float deltaTime)
    {
        timer += deltaTime;
        float t = Mathf.Clamp01(timer / Duration);

        float heightSample = heightCurve.Evaluate(t);
        float forwardSample = t; // linear forward carry — swap for an ease curve later if linear feels robotic

        float heightDelta = (heightSample - lastHeightSample) * PeakHeight;
        float forwardDelta = (forwardSample - lastForwardSample) * baseForwardDistance;
        lastHeightSample = heightSample;
        lastForwardSample = forwardSample;

        ctx.transform.position += ctx.transform.forward * forwardDelta + Vector3.up * heightDelta;
    }

    public bool IsComplete => timer >= Duration;

    public void Exit(PlayerContext ctx)
    {
        ctx.setTackleImmune(false); // immunity only lasts the move's duration — never lingers into normal Run state
        ctx.setCooldown(cooldownDuration);
    }
}