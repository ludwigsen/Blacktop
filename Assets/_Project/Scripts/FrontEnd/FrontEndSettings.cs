using System;
using UnityEngine;

// Explicit scene dependencies keep packaged builds independent from AssetDatabase/Resources scans.
[CreateAssetMenu(menuName = "Blacktop/Front End/Settings")]
public class FrontEndSettings : ScriptableObject
{
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
