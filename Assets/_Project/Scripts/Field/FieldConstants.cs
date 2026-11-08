// Fixed field dimensions, shared by every field variant. Per the design call: all 11
// fields share the same playable rectangle. Venue art and obstacles can vary around it.
// This is why there's no FieldLayout ScriptableObject; there's nothing to vary per field,
// so a static class is the right call (promote to a tunable asset only if that ever changes).
public static class FieldConstants
{
    public const float HalfWidth = 15f;          // sideline to sideline / 2 (30u total width)
    public const float PlayLength = 60f;         // end line to end line (60u total length)
    public const float EndZoneDepth = 10f;       // two 10u end-zone overlays inside the 60u playable length

    // The existing safety band outside the legal scoring rectangle. A venue can
    // place walls/fences here while leaving other stretches open for OOB rulings.
    public const float OutOfBoundsMargin = 2f;

    // Derived, not authored — keeps "goal line Z" defined in exactly one place.
    public static float NearGoalLineZ => -PlayLength / 2f + EndZoneDepth;
    public static float FarGoalLineZ => PlayLength / 2f - EndZoneDepth;
}
