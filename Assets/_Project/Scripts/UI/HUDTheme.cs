using TMPro;
using UnityEngine;

// Shared HUD typography. Team names/scores use the display face; supporting copy uses Outfit.
[CreateAssetMenu(menuName = "Blacktop/HUD Theme")]
public class HUDTheme : ScriptableObject
{
    [Tooltip("Old London — team names and score digits.")]
    public TMP_FontAsset displayFont;

    [Tooltip("Outfit — Gamebreaker, downs, field position, clock, and other HUD copy.")]
    public TMP_FontAsset legibleFont;

    // SDF outline settings are copied to a panel-owned material, never the shared font asset.
    public Color scorebugTextColor = new Color(0.94f, 0.93f, 0.89f, 1f);
    [Range(0f, 0.4f)] public float scorebugOutlineWidth = 0.22f;

    [Header("Team panel layout")]
    [Min(1f)] public float logoScale = 1.25f;
    [Range(0f, 45f)] public float logoOutwardTilt = 20f;
    [Tooltip("Move names outward by this fraction of their original text slot width.")]
    [Range(0f, 0.25f)] public float nameOutwardShift = 0.10f;
    [Tooltip("Pull scores toward names by this fraction of the team panel width.")]
    [Range(0f, 0.08f)] public float nameScoreGapReduction = 0.04f;
}
