using System.Collections.Generic;
using UnityEngine;

// Stage-level buff/debuff set (e.g. "Slippery", "Beach", "Parking Lot"). Deliberately
// ADDITIVE percentages only, never multiplicative — this is what keeps field effects from
// compounding into something broken when they stack with Gamebreaker (which IS
// multiplicative, per PlayerAttributes.Effective). Keep individual values small by design;
// the hard cap on AttributeCurves is the final backstop regardless.
[CreateAssetMenu(menuName = "Blacktop/Field Modifiers")]
public class FieldModifiers : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public AttributeStat stat;
        [Tooltip("Additive percentage. 0.1 = +10%, -0.08 = -8%.")]
        public float additivePercent;
    }

    public string fieldName; // Inspector readability only ("Slippery") — not read by code
    public List<Entry> entries = new List<Entry>();

    Dictionary<AttributeStat, float> lookup;

    void OnValidate() => lookup = null;

    public float Get(AttributeStat stat)
    {
        if (lookup == null)
        {
            lookup = new Dictionary<AttributeStat, float>();
            foreach (var e in entries) lookup[e.stat] = e.additivePercent;
        }
        return lookup.TryGetValue(stat, out var v) ? v : 0f;
    }
}