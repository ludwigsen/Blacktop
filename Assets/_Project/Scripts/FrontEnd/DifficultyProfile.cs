using UnityEngine;

// Inspector-authored CPU execution/decision tuning; these values never rewrite roster ratings.
[CreateAssetMenu(menuName = "Blacktop/Front End/Difficulty Profile")]
public sealed class DifficultyProfile : ScriptableObject
{
    public MatchDifficulty difficulty = MatchDifficulty.Normal;
    [Min(0f)] public float pursuitReactionSeconds;
    [Min(.001f)] public float decisionInterval = .01f;
    [Range(0f, .3f)] public float pursuitLeadSeconds;
    [Min(.1f)] public float passReactionScale = 1f;
    [Min(.1f)] public float coverageCushionScale = 1f;
    [Min(.1f)] public float zoneAwarenessScale = 1f;
    [Min(.1f)] public float qbReadTimeScale = 1f;
    [Min(.1f)] public float avoidanceScale = 1f;
    [Min(1)] public int threatsConsidered = 2;
    [Range(0f, 2f)] public float situationalWeight = 1f;
    [Range(0f, 1f)] public float standardRunBias;
    [Min(0f)] public float gamebreakerDelay;
    [Range(0f, 1f)] public float draftQuality = .65f;
}

// Detached values protect active matches/rematches from Inspector edits to profiles.
public sealed class DifficultyTuning
{
    public static readonly DifficultyTuning Normal = new DifficultyTuning();
    public float PursuitReaction { get; }
    public float DecisionInterval { get; } = .01f;
    public float PursuitLead { get; }
    public float PassReactionScale { get; } = 1f;
    public float CoverageCushionScale { get; } = 1f;
    public float ZoneAwarenessScale { get; } = 1f;
    public float QbReadTimeScale { get; } = 1f;
    public float AvoidanceScale { get; } = 1f;
    public int ThreatsConsidered { get; } = 2;
    public float SituationalWeight { get; } = 1f;
    public float StandardRunBias { get; }
    public float GamebreakerDelay { get; }
    public float DraftQuality { get; } = .65f;
    DifficultyTuning() { }
    public DifficultyTuning(DifficultyProfile source)
    {
        PursuitReaction = source.pursuitReactionSeconds; DecisionInterval = source.decisionInterval;
        PursuitLead = source.pursuitLeadSeconds;
        PassReactionScale = source.passReactionScale; CoverageCushionScale = source.coverageCushionScale;
        ZoneAwarenessScale = source.zoneAwarenessScale; QbReadTimeScale = source.qbReadTimeScale;
        AvoidanceScale = source.avoidanceScale; ThreatsConsidered = source.threatsConsidered;
        SituationalWeight = source.situationalWeight; StandardRunBias = source.standardRunBias;
        GamebreakerDelay = source.gamebreakerDelay; DraftQuality = source.draftQuality;
    }
    public float RunChance(float standard, float situational) => Mathf.Clamp01(standard + StandardRunBias + (situational - standard) * SituationalWeight);
    public static DifficultyTuning ForTeam(int teamId) => FrontEndRuntime.Match != null && teamId != FrontEndRuntime.Match.HumanSide
        ? FrontEndRuntime.Match.Configuration.Cpu : Normal;
}
