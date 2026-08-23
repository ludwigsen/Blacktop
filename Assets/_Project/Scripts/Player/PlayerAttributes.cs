using UnityEngine;

// Per-character identity: 12 ratings (0-20, 10 = neutral), plus a reference to the
// shared AttributeCurves asset. Characters differ ONLY in ratings — curve shapes and hard
// caps live once in AttributeCurves, not duplicated per character. There is no archetype
// concept: a "Balanced" character is just every rating set to 10, same as any other.
[CreateAssetMenu(menuName = "Blacktop/Player Attributes")]
public class PlayerAttributes : ScriptableObject
{
    public AttributeCurves curves;

    [Range(0, 20)] public int passing = 10;
    [Range(0, 20)] public int speed = 10;
    [Range(0, 20)] public int blocking = 10;
    [Range(0, 20)] public int agility = 10;
    [Range(0, 20)] public int catching = 10;
    [Range(0, 20)] public int runPower = 10;
    [Range(0, 20)] public int carrying = 10;
    [Range(0, 20)] public int tackling = 10;
    [Range(0, 20)] public int coverage = 10;
    [Range(0, 20)] public int dMoves = 10;
    [Range(0, 20)] public int swagger = 10;
    [Range(0, 20)] public int routeRunning = 10;

    // Stack order: curve output (identity) + field additive% (environment, small &
    // bounded by design) -> combined, THEN x gamebreaker (temporary, multiplicative,
    // cleared on PlayState.OnPlayEnded per the Gamebreaker design note) -> hard clamp.
    // The clamp is the backstop against ANY combination running away, independent of
    // how field/gamebreaker individually stack.
    public float Effective(AttributeStat stat, int rating, FieldModifiers field = null, float gamebreakerMult = 1f)
    {
        float curveMult = curves != null ? curves.Evaluate(stat, rating) : 1f;
        float fieldAdd = field != null ? field.Get(stat) : 0f;
        float combined = (curveMult + fieldAdd) * gamebreakerMult;

        var (min, max) = curves != null ? curves.HardCap(stat) : (0.5f, 2f);
        return Mathf.Clamp(combined, min, max);
    }

    // Per-stat convenience accessors — field/gamebreaker default to "inactive" so
    // existing call sites (nothing selects a field yet — that's Tier 3) don't need to
    // pass anything until a field-selection system exists.
    public float Passing(FieldModifiers field = null, float gb = 1f) => Effective(AttributeStat.Passing, passing, field, gb);
    public float Speed(FieldModifiers field = null, float gb = 1f) => Effective(AttributeStat.Speed, speed, field, gb);
    public float Blocking(FieldModifiers field = null, float gb = 1f) => Effective(AttributeStat.Blocking, blocking, field, gb);
    public float Agility(FieldModifiers field = null, float gb = 1f) => Effective(AttributeStat.Agility, agility, field, gb);
    public float Catching(FieldModifiers field = null, float gb = 1f) => Effective(AttributeStat.Catching, catching, field, gb);
    public float RunPower(FieldModifiers field = null, float gb = 1f) => Effective(AttributeStat.RunPower, runPower, field, gb);
    public float Carrying(FieldModifiers field = null, float gb = 1f) => Effective(AttributeStat.Carrying, carrying, field, gb);
    public float Tackling(FieldModifiers field = null, float gb = 1f) => Effective(AttributeStat.Tackling, tackling, field, gb);
    public float Coverage(FieldModifiers field = null, float gb = 1f) => Effective(AttributeStat.Coverage, coverage, field, gb);
    public float DMoves(FieldModifiers field = null, float gb = 1f) => Effective(AttributeStat.DMoves, dMoves, field, gb);
    public float Swagger(FieldModifiers field = null, float gb = 1f) => Effective(AttributeStat.Swagger, swagger, field, gb);
    public float RouteRunning(FieldModifiers field = null, float gb = 1f) => Effective(AttributeStat.RouteRunning, routeRunning, field, gb);
}