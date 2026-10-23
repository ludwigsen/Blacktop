using System;
using UnityEngine;

[Serializable]
public struct BodyWeightEntry
{
    public BodyArchetype build;
    [Min(1f)] public float poundsAt183cm;
    [Range(1f, 4f)] public float heightExponent;
    public Vector2 poundLimits;
}

// Appearance-only estimates, never inputs to PlayerAttributes or collision.
[CreateAssetMenu(menuName = "Blacktop/Front End/Body Weight Profile")]
public class BodyWeightProfile : ScriptableObject
{
    public BodyWeightEntry[] entries = {
        Entry(BodyArchetype.Lean,165), Entry(BodyArchetype.Athletic,190), Entry(BodyArchetype.Thick,220),
        Entry(BodyArchetype.Heavy,250), Entry(BodyArchetype.Massive,275) };
    static BodyWeightEntry Entry(BodyArchetype build, float pounds) => new BodyWeightEntry {
        build = build, poundsAt183cm = pounds, heightExponent = 2.5f, poundLimits = new Vector2(120f,390f) };
    public int Pounds(PlayerHeight height, BodyArchetype build)
    {
        foreach (var entry in entries)
            if (entry.build == build) return Mathf.RoundToInt(Mathf.Clamp(entry.poundsAt183cm *
                Mathf.Pow((int)height / 183f, entry.heightExponent), entry.poundLimits.x, entry.poundLimits.y));
        return 0;
    }
}
