using TMPro;
using UnityEngine;

// These roles describe UI meaning; team identities and rating tiers never enter this palette.
public enum UIThemeId { StreetClassic, AfterDark }
public enum UIColorRole
{
    BackgroundPrimary, BackgroundSecondary, SurfacePrimary, SurfaceRaised,
    TextPrimary, TextSecondary, TextOnBright, SelectionPrimary, SelectionHighlight,
    ActionConfirm, ActionCancel, StateSuccess, StateWarning, AccentSpecial, BorderSubtle, ShadowPrimary
}
public enum UITypeRole { Body, Navigation, Heading, Numeral, Announcement, TeamName }

[CreateAssetMenu(menuName="Blacktop/UI/Theme")]
public class BlacktopTheme : ScriptableObject
{
    public UIThemeId id;
    public string displayName;
    [SerializeField] Color[] colors;
    [Range(0,1)] public float textureStrength=.12f;
    public Color ColorFor(UIColorRole role) => colors[(int)role];

    // The same defaults initialize authored assets and keep unconfigured test fixtures usable.
    public void SetDefaults(UIThemeId value)
    {
        id=value;displayName=value==UIThemeId.AfterDark?"After Dark":"Street Classic";
        string[] hex=value==UIThemeId.AfterDark
            ?new[]{"0B1020","0A1428","1B2540","253452","F2F4FA","92A4C5","0A1428","344D91","74D9ED","FFB454","FF6B69","63C8A5","FFB454","FFB454","435879","050A14"}
            :new[]{"15171C","10151F","23272F","303744","F3EFE6","B6BFCC","10151F","2D7FF9","59D7FF","FF7B00","E63946","46B88B","FFD23F","FFD23F","495362","080B10"};
        colors=new Color[hex.Length];
        for(int i=0;i<hex.Length;i++){ColorUtility.TryParseHtmlString("#"+hex[i],out colors[i]);}
        textureStrength=value==UIThemeId.AfterDark?.045f:.12f;
    }
}
