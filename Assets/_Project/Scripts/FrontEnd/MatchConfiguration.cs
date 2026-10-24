using System;
using System.Linq;
using UnityEngine;

public enum ExhibitionMode { HeadToHead, Pickup }
public enum MatchCondition { Random, Clear, Cold, Slick, Loose, Raw }
public enum MatchDifficulty { Random, Easy, Normal, Hard, StreetLegend }

// Local randomness is injectable; menu setup never changes Unity's gameplay random stream.
public interface IMatchRandom { int Next(int count); }
public sealed class SeededMatchRandom : IMatchRandom
{
    readonly System.Random random;
    public SeededMatchRandom(int seed) { random = new System.Random(seed); }
    public int Next(int count) => random.Next(count);
}

// Each mode owns its own editable choices. Null field ID means Random.
public sealed class MatchOptions
{
    public string fieldId;
    public MatchCondition condition = MatchCondition.Clear;
    public ConditionSeverity severity = ConditionSeverity.Regular;
    public MatchDifficulty difficulty = MatchDifficulty.Normal;
}

// Fractional additive effects are detached from the authoring asset exactly once per match.
public sealed class MatchModifiers
{
    public static readonly MatchModifiers Clear = new MatchModifiers(new float[12], 0f);
    readonly float[] additions;
    public float Acceleration { get; }
    MatchModifiers(float[] values, float acceleration) { additions = values; Acceleration = acceleration; }
    public float Get(AttributeStat stat) => additions[(int)stat];
    public static MatchModifiers From(ConditionData source, ConditionSeverity severity, bool clear = false)
    {
        if (clear) return Clear; // Clear remains zero even if a Clear asset was authored incorrectly.
        var values = new float[12]; float acceleration = 0f;
        foreach (var entry in source.GetEffectiveModifiers(severity))
        {
            float fraction = entry.regularPercent * .01f;
            switch (entry.stat)
            {
                case ConditionStat.Speed: values[(int)AttributeStat.Speed] += fraction; break;
                case ConditionStat.Agility: values[(int)AttributeStat.Agility] += fraction; break;
                case ConditionStat.Power: values[(int)AttributeStat.RunPower] += fraction; break;
                case ConditionStat.Carrying: values[(int)AttributeStat.Carrying] += fraction; break;
                case ConditionStat.Accel: acceleration += fraction; break;
            }
        }
        // Bound aggregate environmental nudges before Gamebreaker and the existing final caps.
        for (int i = 0; i < values.Length; i++) values[i] = Mathf.Clamp(values[i], -.3f, .3f);
        return new MatchModifiers(values, Mathf.Clamp(acceleration, -.3f, .3f));
    }
}

// All Random requests resolve once at snapshot creation; this object is reused on rematch.
public sealed class MatchConfiguration
{
    public string RequestedFieldId { get; }
    public MatchCondition RequestedCondition { get; }
    public MatchDifficulty RequestedDifficulty { get; }
    public FieldDefinition Field { get; }
    public MatchCondition Condition { get; }
    public ConditionSeverity Severity { get; }
    public MatchDifficulty Difficulty { get; }
    public DifficultyTuning Cpu { get; }
    public MatchModifiers Modifiers { get; }
    MatchConfiguration(MatchOptions request, FieldDefinition field, MatchCondition condition, DifficultyProfile profile, ConditionData data)
    {
        RequestedFieldId = request.fieldId; RequestedCondition = request.condition; RequestedDifficulty = request.difficulty;
        Field = field; Condition = condition; Severity = request.severity; Difficulty = profile.difficulty;
        Cpu = new DifficultyTuning(profile); Modifiers = MatchModifiers.From(data, Severity, condition == MatchCondition.Clear);
    }
    public static FieldDefinition[] PlayableFields(FrontEndSettings settings) => settings.fields
        .Where(FieldLoader.IsPlayable).GroupBy(f => f.venueId).Select(g => g.First()).ToArray();
    public static bool TryResolve(FrontEndSettings settings, MatchOptions request, IMatchRandom random,
        out MatchConfiguration result, out string error)
    {
        result = null; error = null;
        if (settings == null || request == null || random == null) { error = "Match settings are missing."; return false; }
        var fields = PlayableFields(settings);
        if (fields.Length == 0) { error = "No playable fields are installed."; return false; }
        var field = string.IsNullOrEmpty(request.fieldId) ? fields[random.Next(fields.Length)] : fields.FirstOrDefault(f => f.venueId == request.fieldId);
        var conditions = Enum.GetValues(typeof(MatchCondition)).Cast<MatchCondition>().Where(c => c != MatchCondition.Random && settings.Condition(c) != null).ToArray();
        var profiles = settings.difficulties.Where(p => p != null && p.difficulty != MatchDifficulty.Random && Enum.IsDefined(typeof(MatchDifficulty), p.difficulty))
            .GroupBy(p => p.difficulty).Select(g => g.First()).ToArray();
        if (field == null) error = "Choose an available playable field.";
        else if (!Enum.IsDefined(typeof(ConditionSeverity), request.severity)) error = "Choose a valid severity.";
        else if (conditions.Length != 5 || profiles.Length != 4) error = "Install all five conditions and all four difficulty profiles in FrontEndSettings.";
        if (error != null) return false;
        var condition = request.condition == MatchCondition.Random ? conditions[random.Next(conditions.Length)] : request.condition;
        var profile = request.difficulty == MatchDifficulty.Random ? profiles[random.Next(profiles.Length)] : profiles.FirstOrDefault(p => p.difficulty == request.difficulty);
        var data = settings.Condition(condition);
        if (data == null || profile == null) { error = "Choose an installed condition and difficulty."; return false; }
        result = new MatchConfiguration(request, field, condition, profile, data); return true;
    }
}
