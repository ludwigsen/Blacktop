using System.Collections.Generic;
using UnityEngine;

// Gamebreaker's stat buff table — deliberately separate from FieldModifiers even though
// the shape looks similar, because the math is different on purpose: field effects are
// ADDITIVE percentages (per design doc §18, keeps them from compounding badly), Gamebreaker
// is MULTIPLICATIVE on top of everything else. This is what makes Gamebreaker feel like an
// exaggerated version of the player's own identity rather than a flat universal boost —
// a high-Speed player gets more out of the same Speed multiplier than a low-Speed one,
// same shape as the design doc's worked example (Speed x1.08, Agility x1.15, D-Moves x1.25).
[CreateAssetMenu(menuName = "Blacktop/Gamebreaker Buffs")]
public class GamebreakerBuffs : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public AttributeStat stat;
        [Tooltip("Multiplicative while Gamebreaker is active. 1.15 = +15%. NOT additive like FieldModifiers.")]
        public float multiplier;
    }

    public List<Entry> entries = new();

    Dictionary<AttributeStat, float> lookup;
    void OnValidate() => lookup = null;

    public float Get(AttributeStat stat)
    {
        if (lookup == null)
        {
            lookup = new Dictionary<AttributeStat, float>();
            foreach (var e in entries) lookup[e.stat] = e.multiplier;
        }
        return lookup.TryGetValue(stat, out var v) ? v : 1f;
    }
}