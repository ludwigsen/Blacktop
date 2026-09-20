using UnityEngine;

// Which way the team with the ball is driving, expressed once so nothing else in the
// project has to hardcode "offense moves toward +Z". PlayState hands out the current one
// (PlayState.AttackDirection) — it's derived from whichever team has possession, so it
// flips automatically when possession does.
//
// Two ideas to keep straight:
//   - "world Z"  : raw scene Z, same space PlayState.CurrentLineOfScrimmageZ lives in.
//   - "axis Z"   : possession-relative Z. Positive = toward the end zone the offense is
//                  attacking, negative = toward its own. All the authored numbers in the
//                  project (formation offsets, initialPlayerZ, kickoffResetZ, the
//                  FieldConstants goal lines) are written in axis space, from the
//                  perspective of a team attacking +Z. ToWorldVector / FromAxis convert.
//
// A default(FieldDirection) attacks +Z — the project's original assumption — so
// uninitialized values behave like the pre-possession code did.
//
// Field is assumed centered on world Z = 0 with an unrotated FieldRoot (already true:
// PlayState's LOS is raw world Z, and FieldConstants goal lines are symmetric about 0).
public readonly struct FieldDirection
{
    readonly bool attacksNegativeZ;

    FieldDirection(bool attacksNegativeZ) { this.attacksNegativeZ = attacksNegativeZ; }

    public static FieldDirection TowardPositiveZ => new(false);
    public static FieldDirection TowardNegativeZ => new(true);
    public static FieldDirection FromAttacksPositiveZ(bool attacksPositiveZ) => new(!attacksPositiveZ);

    public float Sign => attacksNegativeZ ? -1f : 1f;
    public FieldDirection Opposite => new(!attacksNegativeZ);

    public Vector3 Forward => new(0f, 0f, Sign);
    public Quaternion Rotation => attacksNegativeZ ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
    public float Yaw => attacksNegativeZ ? 180f : 0f; // handy for camera orbit

    // --- Z conversion ---
    public float ToAxis(float worldZ) => worldZ * Sign;
    public float FromAxis(float axisZ) => axisZ * Sign;
    public float Advance(float worldZ, float yards) => worldZ + yards * Sign; // negative yards = toward own goal

    // --- Line of scrimmage ---
    public float YardsPastLOS(float worldZ, float losZ) => (worldZ - losZ) * Sign;
    public bool IsPastLOS(float worldZ, float losZ) => YardsPastLOS(worldZ, losZ) > 0f;

    // Converts a vector authored for a +Z attacker (formation offsets, raw stick input)
    // into world space. A 180-degree yaw flips BOTH x and z — flipping only z would
    // mirror the formation (left tackle becomes right tackle) instead of rotating it.
    public Vector3 ToWorldVector(Vector3 attackSpace) => new(attackSpace.x * Sign, attackSpace.y, attackSpace.z * Sign);

    // --- Goal lines / end zones (world space) ---
    public float TargetGoalLineZ => attacksNegativeZ ? FieldConstants.NearGoalLineZ : FieldConstants.FarGoalLineZ;
    public float OwnGoalLineZ => attacksNegativeZ ? FieldConstants.FarGoalLineZ : FieldConstants.NearGoalLineZ;

    // FieldBounds.GetEndZone() reports Near/Far in field-local terms; these say which
    // of those two is "the one we score in" / "the one we defend" for this direction.
    public FieldEndZone TargetEndZone => attacksNegativeZ ? FieldEndZone.Near : FieldEndZone.Far;
    public FieldEndZone OwnEndZone => attacksNegativeZ ? FieldEndZone.Far : FieldEndZone.Near;

    // Axis-space comparison against the same goal-line constants the rest of the field
    // code already uses — the symmetric-field assumption is what makes this one line.
    public bool IsInTargetEndZone(float worldZ) => ToAxis(worldZ) >= FieldConstants.FarGoalLineZ;
    public bool IsInOwnEndZone(float worldZ) => ToAxis(worldZ) <= FieldConstants.NearGoalLineZ;
}