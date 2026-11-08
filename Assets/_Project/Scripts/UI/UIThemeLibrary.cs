using TMPro;
using UnityEngine;

// One Resources entry makes presentation available from either scene, including direct gameplay entry.
[CreateAssetMenu(menuName="Blacktop/UI/Theme Library")]
public class UIThemeLibrary : ScriptableObject
{
    public BlacktopTheme streetClassic,afterDark;
    public TMP_FontAsset brand,regular,semiBold,extraBold,boldItalic;
    [Header("Street sprite kit")]
    public Sprite panelSurface, chalkFrame, brushPlate, crown, wordmark, inkScrim;
    [Header("Grayscale texture layers")]
    public Sprite asphaltGrain,dryBrush,sprayScatter,edgeWear;
    public FormationData diagramOffense,diagramDefense; // Presentation fallback matches the installed default formations.
    public TMP_FontAsset FontFor(UITypeRole role) => role switch
    {
        UITypeRole.TeamName=>brand, UITypeRole.Body=>regular,
        UITypeRole.Heading or UITypeRole.Numeral=>extraBold,
        UITypeRole.Announcement=>boldItalic, _=>semiBold
    };
}
