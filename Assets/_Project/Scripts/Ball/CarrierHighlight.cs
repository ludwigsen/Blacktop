using UnityEngine;

// Ground-plane indicator under the live ball carrier — red for the user-controlled
// player, gray for anyone else (defender interception return, teammate after a
// pitch/handoff, etc). Resolved live off BallController.Instance.Carrier every frame,
// never cached — same "resolve live, don't cache" rule as DefenderAI/DefenderCoordinator/
// CameraFollow/TouchdownZone.
//
// Shape is a procedurally generated JAGGED ring, not a clean circle — inner and outer
// radius are both perturbed by per-instance noise, so it reads like a rough spray-painted
// stencil mark on asphalt rather than a MOBA-style selection reticle. Fits the street-court
// aesthetic instead of looking like a UI widget bolted onto the field.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CarrierHighlight : MonoBehaviour
{
    [SerializeField] string playerTag = "Player";
    [SerializeField] float baseRadius = 0.9f;
    [SerializeField] float ringThickness = 0.18f;
    [SerializeField] float jaggedness = 0.12f; // noise amplitude on inner/outer radius, world units
    [SerializeField] int segments = 40;
    [SerializeField] float groundOffset = 0.03f; // just above the plane, avoids z-fighting
    [SerializeField] float spinSpeed = 12f; // deg/sec — purely cosmetic, helps it read as "live" rather than a decal

    // Future settings-menu hook: a settings screen can assign these directly
    // (CarrierHighlight.UserColor = pickedColor) without this script needing to know a
    // settings system exists at all. Applied live every frame, so a change takes effect
    // on the next carrier regardless of when it's set.
    public static Color UserColor = new Color(0.85f, 0.1f, 0.1f);
    public static Color OtherColor = new Color(0.55f, 0.55f, 0.55f);

    MeshRenderer meshRenderer;
    MaterialPropertyBlock mpb;

    void Awake()
    {
        BuildMesh();
        meshRenderer = GetComponent<MeshRenderer>();
        mpb = new MaterialPropertyBlock();

        if (meshRenderer.sharedMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // visible regardless of winding order, no need to fight triangle order for an unlit flat ring
            meshRenderer.sharedMaterial = mat;
        }
    }

    void LateUpdate()
    {
        var carrier = BallController.Instance != null ? BallController.Instance.Carrier : null;

        if (carrier == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        Vector3 pos = carrier.position;
        pos.y = groundOffset;
        transform.position = pos;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        ApplyColor(carrier.CompareTag(playerTag) ? UserColor : OtherColor);
    }

    void SetVisible(bool visible)
    {
        if (meshRenderer.enabled != visible) meshRenderer.enabled = visible;
    }

    void ApplyColor(Color c)
    {
        meshRenderer.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", c); // URP Lit/Unlit
        mpb.SetColor("_Color", c);     // Unlit/Color fallback
        meshRenderer.SetPropertyBlock(mpb);
    }

    // Builds a jagged annulus — inner AND outer radius independently perturbed by Perlin
    // noise per segment, so the silhouette reads as hand-sprayed rather than a machined
    // circle. Built once at Awake; only position/color change at runtime after that.
    void BuildMesh()
    {
        var mesh = new Mesh { name = "CarrierHighlightRing" };

        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];
        float seed = Random.Range(0f, 1000f); // unique per instance so multiple rings in a scene don't look identical

        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / segments;
            float angle = t * Mathf.PI * 2f;

            float outerNoise = (Mathf.PerlinNoise(seed, t * 6f) - 0.5f) * 2f * jaggedness;
            float innerNoise = (Mathf.PerlinNoise(seed + 50f, t * 6f) - 0.5f) * 2f * jaggedness;

            float outerR = baseRadius + outerNoise;
            float innerR = baseRadius - ringThickness + innerNoise;

            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);

            vertices[i * 2] = new Vector3(x * outerR, 0f, z * outerR);
            vertices[i * 2 + 1] = new Vector3(x * innerR, 0f, z * innerR);
        }

        int tri = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int outerCurrent = i * 2, innerCurrent = i * 2 + 1;
            int outerNext = next * 2, innerNext = next * 2 + 1;

            triangles[tri++] = outerCurrent;
            triangles[tri++] = outerNext;
            triangles[tri++] = innerCurrent;

            triangles[tri++] = innerCurrent;
            triangles[tri++] = outerNext;
            triangles[tri++] = innerNext;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }
}