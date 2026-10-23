using System;
using UnityEngine;

// Explicit scene dependencies keep packaged builds independent from AssetDatabase/Resources scans.
[CreateAssetMenu(menuName = "Blacktop/Front End/Settings")]
public class FrontEndSettings : ScriptableObject
{
    public TeamDefinition[] teams = Array.Empty<TeamDefinition>();
    public FieldDefinition[] fields = Array.Empty<FieldDefinition>();
    public PlayerAppearanceCatalog appearances;
    public PlayerAppearanceDefinition defaultAppearance;
    public GameObject visualPrefab;
    public BodyWeightProfile weights;
    public HUDTheme theme;
    public Sprite paint;
    public string gameplayScene = "SampleScene";
    public string menuScene = "MainMenu";

    public TeamDefinition Team(string id) => Array.Find(teams, team => team != null && team.id == id);
}
