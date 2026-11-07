using System.Collections.Generic;
using UnityEngine;

// Read-only ground glyphs. Meshes stay flat and retain a useful stroke at gameplay
// distance; neither glyph predicts a second target or participates in physics.
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class BallGroundMarkers : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float looseRadius = 0.65f;
    [SerializeField, Min(0.1f)] float targetRadius = 0.8f;
    [SerializeField, Min(0.01f)] float lineWidth = 0.12f;
    [SerializeField, Min(0.001f)] float groundOffset = 0.035f;
    [SerializeField] Color looseColor = new(1f, 0.83f, 0.15f);
    [SerializeField] Color targetColor = new(0.4f, 0.95f, 1f);
    BallController ball;
    MeshRenderer looseOutline, looseGlyph, targetOutline, targetGlyph;
    Material material;
    readonly List<Mesh> ownedMeshes = new();

    public bool LooseVisible => looseGlyph != null && looseGlyph.enabled;
    public bool TargetVisible => targetGlyph != null && targetGlyph.enabled;
    public Vector3 LoosePosition { get; private set; }
    public Vector3 TargetPosition { get; private set; }

    void Awake()
    {
        ball = GetComponent<BallController>();
        // This shader is already retained in GraphicsSettings for FieldLines.
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetFloat("_Cull", 0f);
        looseOutline = CreateGlyph("Loose ball outline", false, looseRadius, lineWidth * 2.3f, Color.black);
        looseGlyph = CreateGlyph("Loose ball ring", false, looseRadius, lineWidth, looseColor);
        targetOutline = CreateGlyph("Pass target outline", true, targetRadius, lineWidth * 2.3f, Color.black);
        targetGlyph = CreateGlyph("Pass target diamond", true, targetRadius, lineWidth, targetColor);
    }

    MeshRenderer CreateGlyph(string label, bool diamond, float radius, float width, Color color)
    {
        var child = new GameObject(label);
        child.transform.SetParent(transform, false);
        var mesh = BuildMesh(diamond, radius, width);
        ownedMeshes.Add(mesh);
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        // Per-renderer tint preserves the single owned material and shared assets.
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(properties);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;
        return renderer;
    }

    static Mesh BuildMesh(bool diamond, float radius, float width)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        int sides = diamond ? 4 : 32;
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            float b = (i + 1) * Mathf.PI * 2f / sides;
            Segment(vertices, triangles, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius,
                new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * radius, width);
        }
        if (diamond)
        {
            Segment(vertices, triangles, Vector3.left * radius * 0.65f, Vector3.right * radius * 0.65f, width);
            Segment(vertices, triangles, Vector3.back * radius * 0.65f, Vector3.forward * radius * 0.65f, width);
        }
        var mesh = new Mesh { name = diamond ? "Pass target diamond/cross" : "Loose ball ring" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        return mesh;
    }

    static void Segment(List<Vector3> vertices, List<int> triangles, Vector3 from, Vector3 to, float width)
    {
        Vector3 side = Vector3.Cross((to - from).normalized, Vector3.up) * (width * 0.5f);
        int start = vertices.Count;
        vertices.Add(from - side); vertices.Add(from + side);
        vertices.Add(to + side); vertices.Add(to - side);
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    void LateUpdate()
    {
        bool live = ball != null && ball.isActiveAndEnabled && (PlayState.Instance == null || PlayState.Instance.IsLive);
        bool loose = live && ball.IsLiveLoose;
        bool pass = live && ball.IsForwardPass;
        Show(looseOutline, looseGlyph, loose);
        Show(targetOutline, targetGlyph, pass);
        if (loose)
        {
            LoosePosition = Ground(ball.transform.position);
            Place(looseOutline, looseGlyph, LoosePosition);
        }
        if (pass)
        {
            TargetPosition = Ground(ball.FlightTarget);
            Place(targetOutline, targetGlyph, TargetPosition);
        }
    }

    Vector3 Ground(Vector3 position) => new(position.x, groundOffset, position.z);

    static void Place(MeshRenderer outline, MeshRenderer glyph, Vector3 position)
    {
        // Explicit world rotation prevents the ball's spiral/tumble tilting its marker.
        outline.transform.SetPositionAndRotation(position, Quaternion.identity);
        glyph.transform.SetPositionAndRotation(position + Vector3.up * 0.004f, Quaternion.identity);
    }

    static void Show(MeshRenderer outline, MeshRenderer glyph, bool visible)
    {
        if (outline != null) outline.enabled = visible;
        if (glyph != null) glyph.enabled = visible;
    }

    void OnDisable()
    {
        Show(looseOutline, looseGlyph, false);
        Show(targetOutline, targetGlyph, false);
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        foreach (var mesh in ownedMeshes) Destroy(mesh); // release all generated native resources
    }
}
