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
    [SerializeField]
    AnimationCurve curve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -4f), // starts at full value, steep negative out-tangent for a fast initial burst
        new Keyframe(1f, 0f, -1f, 0f)  // decays to 0, gentle in-tangent so it trails off rather than stopping abruptly
    );
    [SerializeField] float baseDuration = 0.25f;
    [SerializeField] float baseForwardBurst = 2.5f;
    [SerializeField] float cooldownDuration = 0.3f;

    // Contact detection range — how close a defender needs to be, in front of the player,
    // to count as "connected with." Deliberately generous (arcade forgiveness), similar
    // reasoning to DefenderDetector's trigger volume, but checked via OverlapSphere here
    // rather than a persistent collider, since this only needs to matter during the move's
    // brief active window.
    [SerializeField] float contactRange = 1.5f;
    [SerializeField] string defenderTag = "Defender";

    // Outcome odds — base 30% full shed, 70% push back, per design call. Scaled by
    // powerMult: a maxed-power player (1.3x) pushes the shed chance up meaningfully,
    // a low-power player (0.7x) pushes it down. Defenders have no resistance stat yet —
    // known simplification, revisit once defenders get their own attributes.
    [SerializeField] float baseShedChance = 0.3f;
    [SerializeField] float pushBackDistance = 0.8f;
    [SerializeField] float shedDuration = 1f;

    float timer, lastSample;
    PlayerAttributes attr;
    bool hasResolvedContact; // ensures only ONE outcome roll per activation, even if OverlapSphere keeps detecting the same defender across multiple Tick frames

    float Duration => baseDuration;
    float ForwardBurst => baseForwardBurst * attr.Speed();
    float ShedChance => Mathf.Clamp01(baseShedChance * attr.RunPower());

    public bool CanTrigger(PlayerContext ctx, PlayerState currentState)
        => currentState == PlayerState.Idle || currentState == PlayerState.Walk || currentState == PlayerState.Run;

    public void Enter(PlayerContext ctx)
    {
        attr = ctx.attributes;
        timer = 0f;
        lastSample = 0f;
        hasResolvedContact = false;
    }

    public void Tick(PlayerContext ctx, float deltaTime)
    {
        timer += deltaTime;
        float t = Mathf.Clamp01(timer / Duration);
        float sample = curve.Evaluate(t);
        float delta = sample - lastSample;
        lastSample = sample;

        ctx.transform.position += ctx.transform.forward * ForwardBurst * delta;

        if (!hasResolvedContact)
            CheckContact(ctx);
    }

    void CheckContact(PlayerContext ctx)
    {
        Vector3 checkCenter = ctx.transform.position + ctx.transform.forward * (contactRange * 0.5f);
        Collider[] hits = Physics.OverlapSphere(checkCenter, contactRange);

        foreach (var hit in hits)
        {
            if (!hit.CompareTag(defenderTag)) continue;

            hasResolvedContact = true;

            var defenderAI = hit.GetComponent<DefenderAI>();
            if (defenderAI == null) break;

            // Net roll: attacker power pushes shed chance up, defender resistance pushes it back down.
            float netShedChance = Mathf.Clamp01(baseShedChance * attr.RunPower() / defenderAI.ResistMult);
            float netPushDistance = pushBackDistance * attr.RunPower() / defenderAI.ResistMult;
            bool shed = Random.value < netShedChance;

            if (shed)
            {
                defenderAI.ApplyShed(shedDuration);
            }
            else
            {
                Vector3 pushDir = (hit.transform.position - ctx.transform.position).normalized;
                defenderAI.ApplyPushBack(pushDir, netPushDistance);
            }
            break;
        }
    }

    public bool IsComplete => timer >= Duration;

    public void Exit(PlayerContext ctx) => ctx.setCooldown(cooldownDuration);
}