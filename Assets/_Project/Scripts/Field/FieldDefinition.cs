using UnityEngine;

// Venue identity and art only. Conditions/severity are independent match settings.
[CreateAssetMenu(menuName = "Blacktop/Field Definition")]
public sealed class FieldDefinition : ScriptableObject
{
    public string venueId;
    public string displayName;
    public FieldSurface surfacePrefab;
    public Material baseMaterial;
    public bool useTint;
    public Color tint = Color.white;
    public Vector2 textureTiling = Vector2.one;
    [Tooltip("Optional collider-free chalk, wear or repair geometry.")]
    public GameObject detailPrefab;
    [Tooltip("Reserved for later boundary dressing; Phase 1 does not spawn this.")]
    public GameObject boundaryPrefab;
    [Tooltip("Reserved for later scenery; Phase 1 does not spawn this.")]
    public GameObject environmentRootPrefab;
}
