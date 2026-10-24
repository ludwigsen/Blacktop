using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Explicit scene/data authoring. Existing roster GUIDs, model imports and field roots are preserved.
public static class FrontEndSetup
{
    public const string Data="Assets/_Project/Data/FrontEnd";
    public const string SettingsPath=Data+"/FrontEndSettings.asset";
    [MenuItem("Blacktop/Front End/Generate and Wire Front End")]
    public static void Generate()
    {
        Directory.CreateDirectory(Data);AssetDatabase.Refresh();
        var legacy=AssetDatabase.LoadAssetAtPath<PlayerAppearanceCatalog>(CharacterAppearanceSetup.Data+"/PrototypeCatalog.asset");
        var catalog=Create<PlayerAppearanceCatalog>("FrontEndCatalog",asset=>{
            asset.parts=FrontEndTemporaryArt.Build(legacy);asset.heightProfiles=legacy.heightProfiles;
            asset.skinTones=legacy.skinTones;asset.hairColors=legacy.hairColors;});
        var standard=Create<PlayerAppearanceDefinition>("FrontEndDefault",asset=>{
            var source=AssetDatabase.LoadAssetAtPath<PlayerAppearanceDefinition>(CharacterAppearanceSetup.Data+"/Appearance_Standard.asset");
            EditorUtility.CopySerialized(source,asset);asset.id="appearance.frontend.default";asset.bodyId="body.athletic.temporary";
            asset.clothingIds=new[]{"gear.top.athletic.temporary","gear.shorts.athletic.temporary","gear.footwear.athletic.temporary"};});
        var weight=Create<BodyWeightProfile>("BodyWeights",_=>{});
        var kings=AssetDatabase.LoadAssetAtPath<TeamRoster>("Assets/_Project/Data/Teams/Kings/Roster_Kings.asset");
        const string vipersPath="Assets/_Project/Data/Teams/Vipers/Roster_Vipers.asset";
        var vipers=AssetDatabase.LoadAssetAtPath<TeamRoster>(vipersPath);
        if(vipers==null)
        {
            // This is a documented source roster for the seven pre-existing balanced Vipers actors, not runtime autofill.
            vipers=ScriptableObject.CreateInstance<TeamRoster>();vipers.teamName="Vipers";vipers.curves=kings.curves;
            foreach(TeamMember.RosterSlot slot in Enum.GetValues(typeof(TeamMember.RosterSlot)))
            {
                var record=new TeamRoster.PlayerRecord {id=Guid.NewGuid().ToString(),displayName="Vipers "+slot,jerseyNumber=(int)slot,accentColor=Color.white};
                for(int stat=0;stat<12;stat++)RosterRatings.Set(ref record,stat,10);vipers.players.Add(record);
            }
            AssetDatabase.CreateAsset(vipers,vipersPath);
        }
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var actors=UnityEngine.Object.FindObjectsByType<TeamMember>();
        // The legacy QB had an added applier alongside the inherited one. Keep one GUID owner.
        foreach(var actor in actors)
        {
            var appliers=actor.GetComponents<CharacterApplier>();
            if(appliers.Length<2) continue;
            var binding=appliers.Select(a=>new SerializedObject(a)).FirstOrDefault(a=>!string.IsNullOrEmpty(a.FindProperty("playerId").stringValue));
            if(binding==null)continue;
            string guid=binding.FindProperty("playerId").stringValue;var rosterRef=binding.FindProperty("roster").objectReferenceValue;
            var canonical=new SerializedObject(appliers[0]);canonical.FindProperty("playerId").stringValue=guid;
            canonical.FindProperty("roster").objectReferenceValue=rosterRef;canonical.ApplyModifiedPropertiesWithoutUndo();
            for(int i=1;i<appliers.Length;i++)UnityEngine.Object.DestroyImmediate(appliers[i]);
        }
        var play=UnityEngine.Object.FindAnyObjectByType<PlayState>();
        var playData=new SerializedObject(play);var order=playData.FindProperty("defenseSlotOrder");
        var defense=Enumerable.Range(0,order.arraySize).Select(i=>(TeamMember.RosterSlot)order.GetArrayElementAtIndex(i).enumValueIndex).ToArray();
        var teams=new TeamDefinition[2];
        for(int side=0;side<2;side++)
        {
            int teamSide=side;string teamName=side==0?"Kings":"Vipers";var roster=side==0?kings:vipers;
            teams[side]=Create<TeamDefinition>(teamName,asset=>{
                asset.id=teamName.ToLowerInvariant();asset.identity=AssetDatabase.LoadAssetAtPath<TeamIdentity>($"Assets/_Project/Data/Teams/{teamName}/Identity_{teamName}.asset");
                asset.roster=roster;
                asset.activeLineup=actors.Where(a=>a.teamId==teamSide).OrderBy(a=>a.slot).Select(actor=>{
                    string guid=teamSide==0?new SerializedObject(actor.GetComponent<CharacterApplier>()).FindProperty("playerId").stringValue:roster.players[(int)actor.slot].id;
                    return new LineupAssignment {playerId=guid,offense=actor.slot,defense=Array.IndexOf(defense,actor.slot)};}).ToArray();});
        }
        var settings=Create<FrontEndSettings>("FrontEndSettings",asset=>{
            asset.teams=teams;asset.fields=new[]{AssetDatabase.LoadAssetAtPath<FieldDefinition>("Assets/_Project/Data/Fields/TheLot.asset"),AssetDatabase.LoadAssetAtPath<FieldDefinition>("Assets/_Project/Data/Fields/Downtown.asset")};
            asset.appearances=catalog;asset.defaultAppearance=standard;asset.weights=weight;
            asset.visualPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PlayerModel.prefab");
            asset.theme=AssetDatabase.LoadAssetAtPath<HUDTheme>("Assets/_Project/Scripts/UI/BlacktopHUDTheme.asset");
            asset.paint=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/scorebug_base.png");});
        // Register committed exhibition catalogs without regenerating rosters or venue assets.
        if(settings.conditions.Length==0)settings.conditions=new[]{"Clear","Cold","Slick","Loose","Raw"}.Select(name=>AssetDatabase.LoadAssetAtPath<ConditionData>($"{Data}/Conditions/{name}.asset")).ToArray();
        if(settings.difficulties.Length==0)settings.difficulties=new[]{"Easy","Normal","Hard","StreetLegend"}.Select(name=>AssetDatabase.LoadAssetAtPath<DifficultyProfile>($"{Data}/Difficulty/{name}.asset")).ToArray();
        if(settings.pickupHomeStyle==null)settings.pickupHomeStyle=teams[0].identity;
        if(settings.pickupAwayStyle==null)settings.pickupAwayStyle=teams[1].identity;
        EditorUtility.SetDirty(settings);
        var bootstrap=play.GetComponent<MatchBootstrap>();if(bootstrap==null)bootstrap=play.gameObject.AddComponent<MatchBootstrap>();
        bootstrap.settings=settings;bootstrap.players=actors;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        var main=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        // Scene switching can unload newly created asset wrappers; resolve persistent references again.
        AssetDatabase.Refresh();
        settings=AssetDatabase.LoadAssetAtPath<FrontEndSettings>(SettingsPath);
        settings.teams=new[]{AssetDatabase.LoadAssetAtPath<TeamDefinition>(Data+"/Kings.asset"),AssetDatabase.LoadAssetAtPath<TeamDefinition>(Data+"/Vipers.asset")};
        EditorUtility.SetDirty(settings);
        var menu=new GameObject("Blacktop Front End").AddComponent<FrontEndController>();menu.settings=settings;
        var camera=new GameObject("Menu camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=FrontEndUI.Ink;camera.cullingMask=0;camera.depth=-100;
        RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.3f,.3f,.3f);
        EditorSceneManager.SaveScene(main,"Assets/Scenes/MainMenu.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity",true),new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity",true)};
        AssetDatabase.SaveAssets();
        var service=new RosterService(settings,new RosterSaveStore(Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json")));
        string error=service.Validate(service.Snapshot);if(error!=null)throw new InvalidOperationException(error);
        Debug.Log("FRONT_END_SETUP_COMPLETE. Kings GUIDs preserved; Vipers starter source roster authored; menus and existing match scene wired.");
    }
    static T Create<T>(string name,Action<T> configure) where T:ScriptableObject
    {
        string path=$"{Data}/{name}.asset";var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing!=null)return existing;
        var asset=ScriptableObject.CreateInstance<T>();configure(asset);AssetDatabase.CreateAsset(asset,path);return asset;
    }
    public static void SetupAndExit(){Generate();EditorApplication.Exit(0);}
}
