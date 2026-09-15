using UnityEngine;
using UnityEngine.Rendering;

// Procedural, code-only field markings: line of scrimmage, first-down marker, and both
// goal lines. Built as flat quads (not LineRenderer) — full-width colored bars don't need
// LineRenderer's cornering/width-curve machinery, and a quad is one draw call with zero
// per-frame allocation once built.
//
// FieldBounds is OPTIONAL, not required. If one exists in the scene, lines are parented
// to it and positions run through WorldToFieldLocal so this stays correct even if a
// field's dressing root is offset/rotated. If none exists (true today — no FieldBounds
// GameObject in the scene yet), this falls back to FieldConstants and builds the lines as
// root-level objects in raw world space, same fallback TouchdownZone already uses. Either
// way, drop this component on any single GameObject and it self-configures — no scene
// wiring required.
//
// LOS and first-down lines get their Z updated every frame straight from
// PlayState.CurrentLineOfScrimmageZ. That's just repositioning two existing transforms —
// cheap, no mesh rebuild, no dependency on exactly which PlayState event fires in what
// order. Goal lines never move, so those are built once and left alone.
//
// First down is a flat +firstDownYards ahead of the CURRENT line of scrimmage. There's no
// downs/yardage tracking yet (see BLACKTOP_STATUS.md), so this is a placeholder "+10 from
// wherever LOS currently sits," not "10 yards to a fresh set of downs." Swap the source in
// Update() for a real down tracker later — nothing else here needs to change.
[DisallowMultipleComponent]
public class FieldLines : MonoBehaviour
{
    [Header("Field Reference (optional — falls back to FieldConstants if unset)")]
    [SerializeField] FieldBounds fieldBounds;

    [Header("Line Geometry")]
    [SerializeField] float lineThickness = 0.3f;      // LOS / first-down depth along Z
    [SerializeField] float goalLineThickness = 0.4f;
    [SerializeField] float heightOffset = 0.02f;      // lifts lines above the plane to dodge z-fighting
    [SerializeField] float firstDownYards = 10f;      // placeholder distance — see class note above

    [Header("Colors (alpha = transparency)")]
    [SerializeField] Color losColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] Color firstDownColor = new Color(1f, 0.85f, 0f, 0.6f);
    [SerializeField] Color goalLineColor = new Color(0.92f, 0.92f, 0.85f, 0.5f);

    Transform losLine;
    Transform firstDownLine;

    // Null when no FieldBounds exists — lines are then built at scene root, so their
    // "local" position IS world position and no conversion is needed anywhere below.
    Transform Anchor => fieldBounds != null ? fieldBounds.transform : null;

    float FieldWidth => fieldBounds != null ? fieldBounds.PlayableWidth : FieldConstants.HalfWidth * 2f;
    float NearGoalZ => fieldBounds != null ? fieldBounds.NearGoalLineLocalZ : FieldConstants.NearGoalLineZ;
    float FarGoalZ => fieldBounds != null ? fieldBounds.FarGoalLineLocalZ : FieldConstants.FarGoalLineZ;

    void Awake()
    {
        if (fieldBounds == null) fieldBounds = GetComponentInParent<FieldBounds>();
        if (fieldBounds == null) fieldBounds = FindAnyObjectByType<FieldBounds>();

        var losMat = BuildTransparentUnlitMaterial(losColor, "LOS");
        var firstDownMat = BuildTransparentUnlitMaterial(firstDownColor, "FirstDown");
        var goalLineMat = BuildTransparentUnlitMaterial(goalLineColor, "GoalLine");

        // Static — built once, never repositioned.
        BuildLine("Near Goal Line", goalLineMat, goalLineThickness, NearGoalZ);
        BuildLine("Far Goal Line", goalLineMat, goalLineThickness, FarGoalZ);

        // Dynamic — Z gets driven every frame in Update().
        losLine = BuildLine("LOS Line", losMat, lineThickness, 0f);
        firstDownLine = BuildLine("First Down Line", firstDownMat, lineThickness, 0f);
    }

    void Update()
    {
        if (PlayState.Instance == null) return;

        // PlayState works in raw world Z (see PlayState.BreakHuddle). Convert through
        // FieldBounds only if one exists — otherwise this IS already the right space.
        float losWorldZ = PlayState.Instance.CurrentLineOfScrimmageZ;
        float losZ = fieldBounds != null
            ? fieldBounds.WorldToFieldLocal(new Vector3(0f, 0f, losWorldZ)).z
            : losWorldZ;

        SetLocalZ(losLine, losZ);
        SetLocalZ(firstDownLine, losZ + firstDownYards);
    }

    static void SetLocalZ(Transform t, float z)
    {
        Vector3 p = t.localPosition;
        p.z = z;
        t.localPosition = p;
    }

    Transform BuildLine(string name, Material mat, float thickness, float z)
    {
        var go = new GameObject(name);
        go.transform.SetParent(Anchor, false);
        go.transform.localPosition = new Vector3(0f, heightOffset, z);

        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        meshFilter.sharedMesh = BuildQuadMesh(FieldWidth, thickness);
        meshRenderer.sharedMaterial = mat;

        return go.transform;
    }

    // Flat quad on the local XZ plane, normal up. Both triangle windings are included so
    // it renders correctly regardless of Unity's front-face convention — irrelevant for
    // an unlit top-down overlay, and cheaper than debugging winding by eye in-Editor.
    static Mesh BuildQuadMesh(float width, float depth)
    {
        float hw = width * 0.5f;
        float hd = depth * 0.5f;

        var mesh = new Mesh { name = "FieldLineQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-hw, 0f, -hd),
            new Vector3(hw, 0f, -hd),
            new Vector3(hw, 0f, hd),
            new Vector3(-hw, 0f, hd)
        };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        return mesh;
    }

    // URP Unlit configured for standard alpha transparency via script — same properties
    // the Inspector's Surface Type -> Transparent toggle sets, since these materials are
    // generated at runtime and never exist as editable assets.
    static Material BuildTransparentUnlitMaterial(Color color, string debugName)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(shader) { name = "FieldLineMat_" + debugName };

        mat.SetFloat("_Surface", 1f); // 0 = Opaque, 1 = Transparent
        mat.SetFloat("_Blend", 0f);   // 0 = Alpha blend
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetColor("_BaseColor", color);

        return mat;
    }
}