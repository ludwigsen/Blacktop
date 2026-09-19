// Standalone top-level enum (not nested in PlayCallData) — same reasoning as
// PlayerState/AttributeStat: external readers should never need a qualified path
// like PlayCallData.PlayType, and nesting has already bitten this project once (CS0426).
public enum PlayType
{
    Pass,
    Run, 
    Trick
}