using UnityEngine;

// Ground-plane indicator, procedurally jagged rather than a clean circle (reads like a
// spray-stencil mark on asphalt, not a MOBA reticle). Two tracking modes share this one
// component:
//
//   BallCarrier    — follows whoever currently has the ball (BallController.Carrier).
//   UserControlled — follows whoever the user is actually piloting right now
//                    (PossessionController.ActivelyControlled).
//
// On offense today these usually coincide, since control follows the ball — but they are
// NOT the same concept and must not be conflated. The instant the ball leaves the user's
// hands (a completed pass or pitch), BallCarrier jumps to the new (possibly CPU) holder
// while UserControlled correctly stays on the passer, who the user is still piloting.
// PossessionController is the single, already-correct source of truth for "who's the
// user" — it's the component that LITERALLY makes that decision every frame for real
// gameplay reasons. This deliberately does not maintain its own notion of "the user's
// guy" (an earlier version of this file did, via a separate UserControl singleton
// defaulting to a hardcoded tag check — that was wrong and has been removed).
//
// Both targets resolve live every frame, never cached — same rule as every other
// BallController.Carrier read in the project (DefenderAI/DefenderCoordinator/
// CameraFollow/TouchdownZone).
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CarrierHighlight : MonoBehaviour
{
    public enum TrackingMode { BallCarrier, UserControlled }

    [SerializeField] TrackingMode trackingMode = TrackingMode.BallCarrier;
    [SerializeField] float baseRadius = 0.9f;
    [SerializeField] float ringThickness = 0.18f;
    [SerializeField] float jaggedness = 0.12f; // noise amplitude on inner/outer radius, world units
    [SerializeField] int segments = 40;
    [SerializeField] float groundOffset = 0.03f; // just above the plane, avoids z-fighting
    [SerializeField] float spinSpeed = 12f; // deg/sec — purely cosmetic, helps it read as "live" rather than a decal

    // Future settings-menu hook: assign these directly (CarrierHighlight.UserColor = x)
    // without this script needing to know a settings system exists at all.
    public static Color UserColor = new(0.85f, 0.1f, 0.1f);
    public static Color OtherColor = new(0.55f, 0.55f, 0.55f);

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
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // visible regardless of winding order
            meshRenderer.sharedMaterial = mat;
        }
    }

    void LateUpdate()
    {
        Transform target = trackingMode == TrackingMode.BallCarrier
            ? (BallController.Instance != null ? BallController.Instance.Carrier : null)
            : PossessionController.ActivelyControlled;

        if (target == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 pos = target.position;
        pos.y = groundOffset;
        transform.position = pos;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // Same comparison regardless of mode: is THIS target the one the user is piloting?
        // In UserControlled mode it's trivially always true; in BallCarrier mode it's the
        // real "is it your ball or theirs" check.
        bool isUsers = target == PossessionController.ActivelyControlled;
        ApplyColor(isUsers ? UserColor : OtherColor);
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
        float seed = Random.Range(0f, 1000f); // unique per instance so multiple rings don't look identical

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