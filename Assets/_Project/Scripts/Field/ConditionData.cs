using System.Collections.Generic;
using UnityEngine;

// Which base stat a condition's modifier targets. Deliberately small and hand-picked
// rather than tied to the full 12-stat system (see nfl_street_player_attribute_abstraction.md)
// — only Speed/Agility/Accel/Power exist as real multipliers today (PlayerAttributes.cs).
// Carrying is a placeholder for the future fumble-resistance stat; nothing consumes it yet.
// Extend this enum as real stats land instead of wiring conditions to the full future set now.
public enum ConditionStat
{
    Speed,
    Agility,
    Accel,
    Power,
    Carrying
}

// Clean/Regular/Hard severity — scales how strong a rolled condition's effect is.
// See ConditionData.GetEffectiveModifiers() for how this scales authored values.
public enum ConditionSeverity
{
    Clean,   // ~half strength
    Regular, // authored baseline (±10%)
    Hard     // ~1.8x strength
}

// Single stat nudge. Sign carries the meaning (negative = debuff, positive = buff), but
// ConditionData keeps debuffs/buffs in separate lists anyway so the "every condition needs
// at least one of each" rule is enforceable/visible in the Inspector rather than implicit
// in a sign check.
[System.Serializable]
public struct StatModifier
{
    public ConditionStat stat;

    // Percent value authored AT REGULAR SEVERITY (e.g. -10 for a 10% debuff, +8 for an
    // 8% buff). Clean/Hard are NOT authored separately — they scale off this one number
    // via ConditionData.TierScale, so balancing a condition means touching one value,
    // not three. Additive percentage per project convention (see nfl_street doc §18),
    // NOT multiplicative — stacks predictably with attribute multipliers/Gamebreaker
    // instead of compounding.
    public float regularPercent;
}

// One field condition (Clear/Cold/Slick/Loose/Raw), authored as a ScriptableObject asset
// per condition. Every non-Clear condition must define at least one buff AND one debuff
// per design — see BLACKTOP_STATUS.md "Field & Condition System" for the buff/debuff
// rationale behind each. Clear is the explicit zero-modifier baseline and should ship
// with both lists empty rather than being special-cased in code.
[CreateAssetMenu(menuName = "Blacktop/Condition Data")]
public class ConditionData : ScriptableObject
{
    [Tooltip("Display name shown to the player — e.g. \"Slick\", \"Cold\".")]
    public string conditionName;

    [Tooltip("Flavor text for UI/announcer lines. Optional.")]
    [TextArea] public string description;

    [Tooltip("Stats this condition PENALIZES. Author percent as negative, at Regular severity.")]
    public List<StatModifier> debuffs = new List<StatModifier>();

    [Tooltip("Stats this condition IMPROVES. Author percent as positive, at Regular severity.")]
    public List<StatModifier> buffs = new List<StatModifier>();

    // Design-locked scale: Clean ±5%, Regular ±10%, Hard ±18% — expressed here as a
    // multiplier on the Regular-authored value rather than three separate hand-entered
    // percentages, so the ratio between tiers can't drift out of sync per-condition.
    static float TierScale(ConditionSeverity tier) => tier switch
    {
        ConditionSeverity.Clean => 0.5f,
        ConditionSeverity.Regular => 1f,
        ConditionSeverity.Hard => 1.8f,
        _ => 1f
    };

    // Returns every modifier (debuffs + buffs combined) scaled for the given severity.
    // Gameplay/stat systems should call this rather than reading debuffs/buffs directly —
    // keeps tier scaling centralized in one place instead of reimplemented at every
    // call site (field selection UI, in-play stat application, etc. once those exist).
    public List<StatModifier> GetEffectiveModifiers(ConditionSeverity tier)
    {
        float scale = TierScale(tier);
        var result = new List<StatModifier>(debuffs.Count + buffs.Count);

        foreach (var mod in debuffs)
            result.Add(new StatModifier { stat = mod.stat, regularPercent = mod.regularPercent * scale });

        foreach (var mod in buffs)
            result.Add(new StatModifier { stat = mod.stat, regularPercent = mod.regularPercent * scale });

        return result;
    }
}
