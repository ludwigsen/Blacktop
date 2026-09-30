// Standalone top-level enum, same convention as PlayType/PlayerState/AttributeStat.
public enum DefenderJob
{
    Man,   // shadow one receiver, matched to the nearest available at the snap
    Zone,  // drop to an anchor point, pick up whoever walks into it, break on balls thrown nearby
    Rush,  // attack the ball carrier from the snap (pass rush / run blitz)
    Spy    // hold a lane near the LOS (existing Contain behavior)
}