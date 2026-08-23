// Standalone top-level enum (not nested in AttributeCurves/PlayerAttributes) — same
// reasoning as PlayerState: external readers should never need a qualified path like
// AttributeCurves.AttributeStat, and nesting has already bitten this project once (CS0426).
public enum AttributeStat
{
    Passing, Speed, Blocking, Agility, Catching, RunPower,
    Carrying, Tackling, Coverage, DMoves, Swagger, RouteRunning
}