using UnityEngine;

// The prefab owns the mesh; venue changes only override the renderer's shared material.
[DisallowMultipleComponent]
public sealed class FieldSurface : MonoBehaviour
{
    [SerializeField] MeshRenderer surfaceRenderer;
    MaterialPropertyBlock properties;

    public MeshRenderer SurfaceRenderer => surfaceRenderer;

    public void Apply(FieldDefinition definition)
    {
        // Restore the prefab default too when an optional venue override is cleared.
        surfaceRenderer.sharedMaterial = definition.baseMaterial != null ? definition.baseMaterial
            : definition.surfacePrefab.SurfaceRenderer.sharedMaterial;

        // Property blocks avoid material instances and never modify source assets.
        Material material = surfaceRenderer.sharedMaterial;
        if (material == null) return;
        properties ??= new MaterialPropertyBlock();
        properties.Clear();
        Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
        properties.SetColor("_BaseColor", baseColor * (definition.useTint ? definition.tint : Color.white));
        Vector2 scale = material.mainTextureScale * definition.textureTiling;
        Vector2 offset = material.mainTextureOffset;
        properties.SetVector("_BaseMap_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
        surfaceRenderer.SetPropertyBlock(properties);
    }
}
