using UnityEngine;

// Venue identity, art and optional physical boundaries. Conditions are independent match settings.
[CreateAssetMenu(menuName = "Blacktop/Field Definition")]
public sealed class FieldDefinition : ScriptableObject
{
    public string venueId;
    public string displayName;
    [Tooltip("Only completed, playable venues belong in match selection or Random.")]
    public bool playable = true;
    public FieldSurface surfacePrefab;
    public Material baseMaterial;
    public bool useTint;
    public Color tint = Color.white;
    public Vector2 textureTiling = Vector2.one;
    [Tooltip("Optional collider-free chalk, wear or repair geometry.")]
    public GameObject detailPrefab;
    [Tooltip("Optional boundary visuals and marked solid wall/fence colliders under FieldBounds.")]
    public GameObject boundaryPrefab;
    [Tooltip("Reserved for later scenery; Phase 1 does not spawn this.")]
    public GameObject environmentRootPrefab;
}
