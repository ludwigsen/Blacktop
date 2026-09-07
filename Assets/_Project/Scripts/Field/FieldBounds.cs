using UnityEngine;

/// <summary>
/// Identifies the edge crossed when a position is outside the playable rectangle.
/// Values are expressed relative to the FieldBounds transform's local axes.
/// </summary>
public enum FieldBoundarySide
{
    None,
    LeftSideline,
    RightSideline,
    NearEndLine,
    FarEndLine
}

/// <summary>
/// Classifies a position with respect to a field. The safety band is not legal play.
/// </summary>
public enum FieldBoundsState
{
    InPlay,
    InSafetyBand,
    OutOfBounds
}

/// <summary>Identifies a standard end zone, expressed relative to local field Z.</summary>
public enum FieldEndZone
{
    None,
    Near,
    Far
}

/// <summary>
/// Shared 30 x 60 playable field definition. Attach this to each FieldRoot.
/// All checks use this transform's local X/Z plane, allowing a complete field to be
/// translated or rotated without changing gameplay code.
/// </summary>
[DisallowMultipleComponent]
public sealed class FieldBounds : MonoBehaviour
{
    [Header("Shared Field Dimensions")]
    [Tooltip("Legal-play width on local X. Blacktop fields should use 30.")]
    [SerializeField, Min(0.01f)] float playableWidth = 30f;
    [Tooltip("Legal-play length on local Z. Blacktop fields should use 60.")]
    [SerializeField, Min(0.01f)] float playableLength = 60f;
    [Tooltip("Non-playable space outside each line. Blacktop fields should use 2.")]
    [SerializeField, Min(0f)] float safetyBandWidth = 2f;
    [Tooltip("Playable depth overlaid at each end of the 60-unit field. Blacktop fields should use 10.")]
    [SerializeField, Min(0f)] float endZoneDepth = 10f;

    [Header("Gizmos")]
    [SerializeField] bool drawGizmos = true;
    [SerializeField, Min(0.01f)] float gizmoHeight = 0.05f;

    public float PlayableWidth => playableWidth;
    public float PlayableLength => playableLength;
    public float SafetyBandWidth => safetyBandWidth;
    public float EndZoneDepth => endZoneDepth;
    public float HalfPlayableWidth => playableWidth * 0.5f;
    public float HalfPlayableLength => playableLength * 0.5f;
    public float HalfSafetyWidth => HalfPlayableWidth + safetyBandWidth;
    public float HalfSafetyLength => HalfPlayableLength + safetyBandWidth;
    public float NearGoalLineLocalZ => -HalfPlayableLength + endZoneDepth;
    public float FarGoalLineLocalZ => HalfPlayableLength - endZoneDepth;

    /// <summary>Converts a world position to this field's local coordinate space.</summary>
    public Vector3 WorldToFieldLocal(Vector3 worldPosition) => transform.InverseTransformPoint(worldPosition);

    /// <summary>Converts a local field position to world space.</summary>
    public Vector3 FieldLocalToWorld(Vector3 localFieldPosition) => transform.TransformPoint(localFieldPosition);

    public bool IsInsidePlayableBounds(Vector3 worldPosition)
    {
        Vector3 localPosition = WorldToFieldLocal(worldPosition);
        return IsInsidePlayableLocal(localPosition);
    }

    public bool IsInsideSafetyBounds(Vector3 worldPosition)
    {
        Vector3 localPosition = WorldToFieldLocal(worldPosition);
        return IsInsideSafetyLocal(localPosition);
    }

    /// <summary>
    /// Returns the most significantly crossed playable edge, or None while in play.
    /// At a perfectly equal diagonal crossing, the sideline is returned deterministically.
    /// </summary>
    public FieldBoundarySide GetOutOfBoundsDirection(Vector3 worldPosition)
    {
        return GetOutOfBoundsDirectionLocal(WorldToFieldLocal(worldPosition));
    }

    /// <summary>
    /// Clamps X and Z to legal play and preserves the input's local Y value.
    /// </summary>
    public Vector3 GetClosestPlayablePoint(Vector3 worldPosition)
    {
        Vector3 localPosition = WorldToFieldLocal(worldPosition);
        localPosition.x = Mathf.Clamp(localPosition.x, -HalfPlayableWidth, HalfPlayableWidth);
        localPosition.z = Mathf.Clamp(localPosition.z, -HalfPlayableLength, HalfPlayableLength);
        return FieldLocalToWorld(localPosition);
    }

    public FieldBoundsState GetState(Vector3 worldPosition)
    {
        Vector3 localPosition = WorldToFieldLocal(worldPosition);
        if (IsInsidePlayableLocal(localPosition)) return FieldBoundsState.InPlay;
        return IsInsideSafetyLocal(localPosition)
            ? FieldBoundsState.InSafetyBand
            : FieldBoundsState.OutOfBounds;
    }

    /// <summary>
    /// Returns the end zone containing a world position. Positions outside the playable
    /// rectangle return None, so an out-of-bounds ball cannot score as a touchdown.
    /// </summary>
    public FieldEndZone GetEndZone(Vector3 worldPosition)
    {
        Vector3 localPosition = WorldToFieldLocal(worldPosition);
        if (!IsInsidePlayableLocal(localPosition) || endZoneDepth <= 0f) return FieldEndZone.None;
        if (localPosition.z <= NearGoalLineLocalZ) return FieldEndZone.Near;
        if (localPosition.z >= FarGoalLineLocalZ) return FieldEndZone.Far;
        return FieldEndZone.None;
    }

    public bool IsInEndZone(Vector3 worldPosition) => GetEndZone(worldPosition) != FieldEndZone.None;

    bool IsInsidePlayableLocal(Vector3 localPosition)
    {
        return Mathf.Abs(localPosition.x) <= HalfPlayableWidth
            && Mathf.Abs(localPosition.z) <= HalfPlayableLength;
    }

    bool IsInsideSafetyLocal(Vector3 localPosition)
    {
        return Mathf.Abs(localPosition.x) <= HalfSafetyWidth
            && Mathf.Abs(localPosition.z) <= HalfSafetyLength;
    }

    FieldBoundarySide GetOutOfBoundsDirectionLocal(Vector3 localPosition)
    {
        float xOverrun = Mathf.Abs(localPosition.x) - HalfPlayableWidth;
        float zOverrun = Mathf.Abs(localPosition.z) - HalfPlayableLength;
        if (xOverrun <= 0f && zOverrun <= 0f) return FieldBoundarySide.None;

        if (xOverrun >= zOverrun)
            return localPosition.x < 0f ? FieldBoundarySide.LeftSideline : FieldBoundarySide.RightSideline;

        return localPosition.z < 0f ? FieldBoundarySide.NearEndLine : FieldBoundarySide.FarEndLine;
    }

    void OnValidate()
    {
        playableWidth = Mathf.Max(0.01f, playableWidth);
        playableLength = Mathf.Max(0.01f, playableLength);
        safetyBandWidth = Mathf.Max(0f, safetyBandWidth);
        endZoneDepth = Mathf.Clamp(endZoneDepth, 0f, playableLength * 0.5f);
        gizmoHeight = Mathf.Max(0.01f, gizmoHeight);
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;

        // Green is legal play; amber is the non-playable safety space; red marks the OOB edge.
        DrawFlatRect(HalfSafetyWidth, HalfSafetyLength, new Color(1f, 0.67f, 0f, 0.13f));
        Gizmos.color = new Color(1f, 0.3f, 0.12f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(HalfSafetyWidth * 2f, gizmoHeight, HalfSafetyLength * 2f));
        DrawFlatRect(HalfPlayableWidth, HalfPlayableLength, new Color(0.1f, 0.85f, 0.28f, 0.2f));
        Gizmos.color = new Color(0.15f, 1f, 0.35f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(playableWidth, gizmoHeight, playableLength));

        // Blue overlays identify the two end zones. White is the centre line, cyan the goal lines.
        DrawFlatRect(HalfPlayableWidth, endZoneDepth * 0.5f, new Vector3(0f, 0f, -HalfPlayableLength + endZoneDepth * 0.5f), new Color(0.1f, 0.5f, 1f, 0.2f));
        DrawFlatRect(HalfPlayableWidth, endZoneDepth * 0.5f, new Vector3(0f, 0f, HalfPlayableLength - endZoneDepth * 0.5f), new Color(0.1f, 0.5f, 1f, 0.2f));
        DrawLine(new Vector3(-HalfPlayableWidth, 0f, 0f), new Vector3(HalfPlayableWidth, 0f, 0f), Color.white);
        DrawLine(new Vector3(-HalfPlayableWidth, 0f, NearGoalLineLocalZ), new Vector3(HalfPlayableWidth, 0f, NearGoalLineLocalZ), Color.cyan);
        DrawLine(new Vector3(-HalfPlayableWidth, 0f, FarGoalLineLocalZ), new Vector3(HalfPlayableWidth, 0f, FarGoalLineLocalZ), Color.cyan);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    void DrawFlatRect(float halfWidth, float halfLength, Color color)
    {
        DrawFlatRect(halfWidth, halfLength, Vector3.zero, color);
    }

    void DrawFlatRect(float halfWidth, float halfLength, Vector3 center, Color color)
    {
        if (halfLength <= 0f) return;
        Gizmos.color = color;
        Gizmos.DrawCube(center, new Vector3(halfWidth * 2f, gizmoHeight, halfLength * 2f));
    }

    static void DrawLine(Vector3 from, Vector3 to, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawLine(from, to);
    }
}
