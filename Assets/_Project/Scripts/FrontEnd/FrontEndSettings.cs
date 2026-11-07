using System;
using UnityEngine;

// Explicit scene dependencies keep packaged builds independent from AssetDatabase/Resources scans.
[CreateAssetMenu(menuName = "Blacktop/Front End/Settings")]
public class FrontEndSettings : ScriptableObject
{
    [Header("Menu presentation")]
    [Tooltip("Reference-resolution pixels above the secondary-page wordmark, including ascender clearance.")]
    [Range(32f,100f)] public float submenuLogoTopPadding=54f;
    public TMPro.TMP_FontAsset menuTitleFont;
    public TMPro.TMP_FontAsset menuFont;
    public Texture2D[] menuFields = Array.Empty<Texture2D>();
    public Shader menuBlurShader;
    public Shader menuBlurFilterShader; // Explicit reference retains the offline filter in standalone builds.
    [Min(1f)] public float fieldHoldSeconds = 6f;
    [Min(.1f)] public float fieldBlendSeconds = 2.4f;
    [Range(0f,40f)] public float submenuBlurPixels = 18f;
    [Range(.1f,.6f)] public float menuTransitionSeconds = .28f;
    [Tooltip("Full player authoring is available only in the Editor and development builds.")]
    public bool allowDevelopmentEditing = true;
    public bool CanAuthorPlayers => allowDevelopmentEditing && (Application.isEditor || Debug.isDebugBuild);

    [Min(0)] public int rosterRevision;
    public TeamDefinition[] teams = Array.Empty<TeamDefinition>();
    public FieldDefinition[] fields = Array.Empty<FieldDefinition>();
    public ConditionData[] conditions = Array.Empty<ConditionData>();
    public DifficultyProfile[] difficulties = Array.Empty<DifficultyProfile>();
    public TeamIdentity pickupHomeStyle;
    public TeamIdentity pickupAwayStyle;
    public PlayerAppearanceCatalog appearances;
    public PlayerAppearanceDefinition defaultAppearance;
    public GameObject visualPrefab;
    public BodyWeightProfile weights;
    public HUDTheme theme;
    public Sprite paint;
    public string gameplayScene = "SampleScene";
    public string menuScene = "MainMenu";

    public TeamDefinition Team(string id) => Array.Find(teams, team => team != null && team.id == id);
    public ConditionData Condition(MatchCondition choice) => Array.Find(conditions,
        value => value != null && string.Equals(value.conditionName, choice.ToString(), StringComparison.OrdinalIgnoreCase));
}
