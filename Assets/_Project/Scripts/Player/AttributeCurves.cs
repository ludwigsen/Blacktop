using System.Collections.Generic;
using UnityEngine;

// Shared, single-instance asset — every PlayerAttributes character references THIS,
// never its own copy. Curve shape defines the attribute's identity curve (soft cap,
// aggressive, explosive, per the design doc); hardCap is a separate backstop that clamps
// the FINAL combined value after field modifiers + Gamebreaker stack on top, so no
// combination of buffs can produce a broken result regardless of curve shape.
// List<Entry> (not 12 explicit fields) follows the same pattern as FormationData's
// List<DefenderSlot> — scales to 12 stats without field bloat, still Inspector-editable.
[CreateAssetMenu(menuName = "Blacktop/Attribute Curves")]
public class AttributeCurves : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public AttributeStat stat;
        public AnimationCurve curve; // X: rating 0-20, Y: multiplier. Rating 10 should evaluate to 1.0 (neutral).
        public float hardCapMin;
        public float hardCapMax;
    }

    public List<Entry> entries = new List<Entry>();

    // Built lazily, not serialized — Dictionary doesn't survive Unity serialization,
    // so this is a runtime-only lookup cache over the authored List<Entry>.
    Dictionary<AttributeStat, Entry> lookup;

    // Invalidates the cache whenever the asset is edited in the Inspector — otherwise
    // tweaking a curve mid-playtest wouldn't take effect until a domain reload.
    void OnValidate() => lookup = null;

    void EnsureLookup()
    {
        if (lookup != null) return;
        lookup = new Dictionary<AttributeStat, Entry>();
        foreach (var e in entries) lookup[e.stat] = e;
    }

    public float Evaluate(AttributeStat stat, int rating)
    {
        EnsureLookup();
        return lookup.TryGetValue(stat, out var e) ? e.curve.Evaluate(rating) : 1f;
    }

    public (float min, float max) HardCap(AttributeStat stat)
    {
        EnsureLookup();
        return lookup.TryGetValue(stat, out var e) ? (e.hardCapMin, e.hardCapMax) : (0.5f, 2f);
    }
}