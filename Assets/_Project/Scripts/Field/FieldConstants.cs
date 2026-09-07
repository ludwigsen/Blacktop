// Fixed field dimensions, shared by every field variant. Per the design call: all 10
// fields are the same size, no per-field narrowing or shortening — visual dressing only.
// This is why there's no FieldLayout ScriptableObject; there's nothing to vary per field,
// so a static class is the right call (promote to a tunable asset only if that ever changes).
public static class FieldConstants
{
    public const float HalfWidth = 15f;          // sideline to sideline / 2 (30u total width)
    public const float PlayLength = 60f;         // end line to end line (60u total length)
    public const float EndZoneDepth = 10f;       // two 10u end-zone overlays inside the 60u playable length

    // How far past the sideline the ball has to travel before it's ruled dead — accounts
    // for walls/fences sitting flush at the boundary rather than exactly on it, so a
    // stumble and an out-of-bounds ruling land in the same frame instead of one preceding
    // the other by a few units.
    public const float OutOfBoundsMargin = 2f;

    // Derived, not authored — keeps "goal line Z" defined in exactly one place.
    public static float NearGoalLineZ => -PlayLength / 2f + EndZoneDepth;
    public static float FarGoalLineZ => PlayLength / 2f - EndZoneDepth;
}
