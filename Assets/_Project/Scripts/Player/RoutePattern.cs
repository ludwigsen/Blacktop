// Route type enumeration. Start simple — expand as needed.
// Each pattern defines how ReceiverAI moves during a play.
public enum RoutePattern
{
    Go,        // Straight upfield (default streak)
    Slant,     // Diagonal forward-right
    Hitch,     // Short route, stop and settle
    Wheel,     // Curved out to the sideline
    Comeback,  // Upfield then break back toward QB
    None       // No route assigned (used for blocking-only roles)
}