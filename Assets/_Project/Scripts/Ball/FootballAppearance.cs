using UnityEngine;

// Reusable visual-only selection point for a future pre-game football picker.
// BallController remains the sole owner of possession, flight, recovery and rulings.
[DisallowMultipleComponent]
public class FootballAppearance : MonoBehaviour
{
    [SerializeField] Renderer targetRenderer;
    [SerializeField] FootballStyle selectedStyle;
    [SerializeField] FootballStyle[] availableStyles;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
    MaterialPropertyBlock propertyBlock;

    public FootballStyle SelectedStyle => selectedStyle;
    public int StyleCount => availableStyles != null ? availableStyles.Length : 0;
    public FootballStyle StyleAt(int index) => index >= 0 && index < StyleCount ? availableStyles[index] : null;

    void Awake() => ApplyStyle(selectedStyle);

    public bool SelectStyle(int index)
    {
        var style = StyleAt(index);
        if (style == null) return false;
        ApplyStyle(style);
        return true;
    }

    public void ApplyStyle(FootballStyle style)
    {
        if (style == null || targetRenderer == null) return;
        selectedStyle = style;
        propertyBlock ??= new MaterialPropertyBlock();
        int slots = Mathf.Min(6, targetRenderer.sharedMaterials.Length);
        for (int slot = 0; slot < slots; slot++)
        {
            // Per-slot overrides keep shared URP materials intact and avoid .material clones.
            propertyBlock.Clear();
            targetRenderer.GetPropertyBlock(propertyBlock, slot);
            propertyBlock.SetColor(BaseColor, style.ColorForSlot(slot));
            if (slot < 3) propertyBlock.SetFloat(Smoothness, style.leatherSmoothness);
            targetRenderer.SetPropertyBlock(propertyBlock, slot);
        }
    }

#if UNITY_EDITOR
    void OnValidate() => ApplyStyle(selectedStyle); // Preview a changed selection in the Inspector.
#endif
}
