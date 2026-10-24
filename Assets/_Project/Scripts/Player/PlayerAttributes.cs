using UnityEngine;

// Per-character identity: 12 ratings (0-20, 10 = neutral), plus a reference to the
// shared AttributeCurves asset. Characters differ ONLY in ratings — curve shapes and hard
// caps live once in AttributeCurves, not duplicated per character. There is no archetype
// concept: a "Balanced" character is just every rating set to 10, same as any other.
[CreateAssetMenu(menuName = "Blacktop/Player Attributes")]
public class PlayerAttributes : ScriptableObject
{
    public AttributeCurves curves;

    // Bound only on a private runtime copy. Shared assets must never acquire a team's match state.
    [System.NonSerialized] public int ownerTeamId = -1;
    MatchModifiers matchModifiers;
    public void BindMatchModifiers(MatchModifiers modifiers) => matchModifiers = modifiers;

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

    // Runtime factory — builds a working PlayerAttributes instance from a data-only
    // TeamRoster.PlayerRecord plus the one shared AttributeCurves asset. This is how
    // stats reach a capsule WITHOUT a hand-authored PlayerAttributes .asset per player.
    public static PlayerAttributes CreateFromRecord(TeamRoster.PlayerRecord record, AttributeCurves curves)
    {
        var attr = CreateInstance<PlayerAttributes>();
        attr.curves = curves;
        attr.passing = record.passing;
        attr.speed = record.speed;
        attr.blocking = record.blocking;
        attr.agility = record.agility;
        attr.catching = record.catching;
        attr.runPower = record.runPower;
        attr.carrying = record.carrying;
        attr.tackling = record.tackling;
        attr.coverage = record.coverage;
        attr.dMoves = record.dMoves;
        attr.swagger = record.swagger;
        attr.routeRunning = record.routeRunning;
        return attr;
    }

    // Stack order: curve output (identity) + field additive% (environment, small &
    // bounded by design) -> combined, THEN x gamebreaker (temporary, multiplicative,
    // cleared when the possession ends) -> hard clamp.
    // The clamp is the backstop against ANY combination running away, independent of
    // how field/gamebreaker individually stack.
    public float Effective(AttributeStat stat, int rating, FieldModifiers field = null, float gamebreakerMult = 1f)
    {
        // A bound match owns the field addition; a legacy optional argument cannot stack it twice.
        float fieldAdd = matchModifiers != null ? matchModifiers.Get(stat) : field != null ? field.Get(stat) : 0f;
        return Combined(stat, rating, fieldAdd, gamebreakerMult);
    }

    float Combined(AttributeStat stat, int rating, float fieldAdd, float gamebreakerMult)
    {
        float curveMult = curves != null ? curves.Evaluate(stat, rating) : 1f;
        // Resolve the boost live for this attribute owner's team, including defenders and AI teammates.
        float teamBoost = PlayState.Instance != null
            ? PlayState.Instance.GetGamebreakerMult(ownerTeamId, stat) : 1f;
        float combined = (curveMult + fieldAdd) * gamebreakerMult * teamBoost;

        var (min, max) = curves != null ? curves.HardCap(stat) : (0.5f, 2f);
        return Mathf.Clamp(combined, min, max);
    }

    // Acceleration is owned by Agility's base curve, but takes only the condition's Accel addition.
    // Slick's Agility penalty still affects moves; it cannot also be charged to acceleration.
    public float Acceleration() => Combined(AttributeStat.Agility, agility, matchModifiers?.Acceleration ?? 0f, 1f);

    // Existing convenience accessors now automatically consume the bound match snapshot.
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
