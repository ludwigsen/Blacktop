using TMPro;
using UnityEngine;

// Single source of truth for which TYPEFACE the HUD uses — not size, not color (that's
// still GameHUD's/TeamIdentity's job). Swap Horsemen/Outfit in here once you have real
// TMP Font Assets built from them (Window > TextMeshPro > Font Asset Creator — use
// Dynamic rendering mode so a missing glyph later doesn't mean re-baking an atlas) and
// every HUD script picks it up with no code changes.
//
// Both fields are allowed to be null — TMP falls back to its own default font asset
// automatically, so this scaffolding works today with zero fonts imported yet.
[CreateAssetMenu(menuName = "Blacktop/HUD Theme")]
public class HUDTheme : ScriptableObject
{
    [Tooltip("Bold/brand font — team names, score digits, the GAMEBREAKER label.")]
    public TMP_FontAsset displayFont;

    [Tooltip("Legible font — down & distance, play clock, and (once built) player nameplates and menu labels.")]
    public TMP_FontAsset legibleFont;
}