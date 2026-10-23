using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Creates stable data slots around existing art; it never manufactures substitute character meshes.
public static class CharacterAppearanceSetup
{
    public const string Data = "Assets/_Project/Data/Characters";
    const string Player = "Assets/_Project/Prefabs/Player.prefab";
    const string Model = "Assets/_Project/Prefabs/PlayerModel.prefab";

    [MenuItem("Blacktop/Characters/Generate Missing Foundation Assets")]
    public static void GenerateAssets()
    {
        Directory.CreateDirectory(Data);
        AssetDatabase.Refresh();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PlayerBody.fbx");
        var skin = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.sharedMesh.vertexCount).First();
        var skeleton = Create<CharacterSkeletonDefinition>("CanonicalSkeleton", value =>
        { value.referenceModel = model; value.referenceSkin = skin; });
        var parts = new System.Collections.Generic.List<CharacterPartDefinition>();
        foreach (BodyArchetype archetype in Enum.GetValues(typeof(BodyArchetype)))
        {
            parts.Add(Create<CharacterPartDefinition>("Body_" + archetype, value =>
            {
                value.id = "body." + archetype.ToString().ToLowerInvariant() + ".prototype";
                value.slot = CharacterPartSlot.Body;
                value.bodyArchetype = archetype;
                value.skeleton = skeleton;
                value.useExistingBody = true;
                value.integratedHead = true;
                value.placeholder = archetype != BodyArchetype.Athletic;
                value.authoringNote = archetype == BodyArchetype.Athletic ? "Existing monolithic PlayerBody stand-in; not a final authored Athletic body." :
                    "No authored body mesh. Displays the Athletic prototype without pretending to change physique.";
            }));
        }
        foreach (string suffix in new[] { "a", "b", "c" })
            parts.Add(Create<CharacterPartDefinition>("Face_" + suffix, value =>
            {
                value.id = "face.prototype." + suffix;
                value.slot = CharacterPartSlot.Face;
                value.placeholder = true;
                value.scalpProfileId = "prototype.integrated";
                value.authoringNote = "Asset slot only. Uses the SAME integrated prototype face; distinct face art is absent.";
            }));
        Option("Hair_Shaved", "hair.shaved", CharacterPartSlot.Hair, true);
        Option("Hair_Cropped", "hair.cropped", CharacterPartSlot.Hair, false);
        Option("Facial_Clean", "facial.clean", CharacterPartSlot.FacialHair, true);
        Option("Facial_Beard", "facial.beard", CharacterPartSlot.FacialHair, false);
        var catalog = Create<PlayerAppearanceCatalog>("PrototypeCatalog", value =>
        {
            value.parts = parts.ToArray();
            value.skinTones = new[] { ColorEntry("skin.light", new Color(0.76f, 0.52f, 0.37f)),
                ColorEntry("skin.medium", new Color(0.48f, 0.28f, 0.17f)), ColorEntry("skin.dark", new Color(0.2f, 0.1f, 0.065f)) };
            value.hairColors = new[] { ColorEntry("hair.black", new Color(0.025f, 0.018f, 0.015f)),
                ColorEntry("hair.brown", new Color(0.16f, 0.07f, 0.025f)) };
        });
        foreach (PlayerHeight height in Enum.GetValues(typeof(PlayerHeight)))
            Create<PlayerHeightProfile>("Height_" + (int)height, value =>
            {
                value.id = "height." + (int)height;
                value.height = height;
                // Measurement slots await artist calibration; scaling uses explicit standing height only.
                float ratio = (int)height / 183f;
                value.nameplateOffset = Vector3.up * (2f * ratio - 2f);
                value.cameraOffset = Vector3.up * (ratio - 1f);
            });
        var shortAppearance = Appearance("Short", 170, "a", "hair.shaved", "facial.clean");
        var standard = Appearance("Standard", 183, "b", "hair.cropped", "facial.clean");
        var giant = Appearance("Giant", 201, "c", "hair.shaved", "facial.beard");
        if (catalog.heightProfiles.Length == 0) catalog.heightProfiles = Enum.GetValues(typeof(PlayerHeight)).Cast<PlayerHeight>()
            .Select(height => AssetDatabase.LoadAssetAtPath<PlayerHeightProfile>($"{Data}/Height_{(int)height}.asset")).ToArray();
        if (catalog.sampleAppearances.Length == 0) catalog.sampleAppearances = new[] { shortAppearance, standard, giant };
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Character foundation assets available. Catalog validation errors: {catalog.ValidateCatalog().Count}. Placeholders are explicitly labelled.");

        void Option(string name, string id, CharacterPartSlot slot, bool empty)
        {
            parts.Add(Create<CharacterPartDefinition>(name, value =>
            {
                value.id = id; value.slot = slot; value.emptyOption = empty; value.placeholder = !empty;
                value.skeleton = skeleton;
                value.authoringNote = empty ? "Intentional no-attachment option." : "No mesh available. Attachment omitted.";
            }));
        }
        PlayerAppearanceDefinition Appearance(string name, int height, string face, string hair, string facial)
            => Create<PlayerAppearanceDefinition>("Appearance_" + name, value =>
            {
                value.id = "appearance.prototype." + name.ToLowerInvariant();
                value.heightProfile = AssetDatabase.LoadAssetAtPath<PlayerHeightProfile>($"{Data}/Height_{height}.asset");
                value.faceId = "face.prototype." + face; value.hairId = hair; value.facialHairId = facial;
            });
    }

    static AppearanceColor ColorEntry(string id, Color color) => new AppearanceColor { id = id, color = color };

    static T Create<T>(string name, Action<T> configure) where T : ScriptableObject
    {
        string path = $"{Data}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing; // Preserve stable GUIDs and the owner's Inspector edits.
        var asset = ScriptableObject.CreateInstance<T>();
        configure(asset);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    [MenuItem("Blacktop/Characters/Wire Existing Player Prefabs")]
    public static void WirePrefabs()
    {
        var skeleton = AssetDatabase.LoadAssetAtPath<CharacterSkeletonDefinition>(Data + "/CanonicalSkeleton.asset");
        var catalog = AssetDatabase.LoadAssetAtPath<PlayerAppearanceCatalog>(Data + "/PrototypeCatalog.asset");
        var standard = AssetDatabase.LoadAssetAtPath<PlayerAppearanceDefinition>(Data + "/Appearance_Standard.asset");
        if (skeleton == null || catalog == null || standard == null) throw new InvalidOperationException("Generate foundation assets first.");
        var model = PrefabUtility.LoadPrefabContents(Model);
        try
        {
            var adapter = model.GetComponent<PlayerVisualRig>() ?? model.AddComponent<PlayerVisualRig>();
            adapter.skeleton = skeleton;
            adapter.animator = model.GetComponentInChildren<Animator>(true);
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            adapter.bodyRenderer = skins.OrderByDescending(r => r.sharedMesh.vertexCount).First();
            adapter.integratedHeadRenderers = skins.Where(r => r != adapter.bodyRenderer).Cast<Renderer>().ToArray();
            PrefabUtility.SaveAsPrefabAsset(model, Model);
        }
        finally { PrefabUtility.UnloadPrefabContents(model); }
        var player = PrefabUtility.LoadPrefabContents(Player);
        try
        {
            var assembler = player.GetComponent<PlayerVisualAssembler>() ?? player.AddComponent<PlayerVisualAssembler>();
            assembler.catalog = catalog;
            assembler.defaultAppearance = standard;
            assembler.rig = player.GetComponentInChildren<PlayerVisualRig>(true);
            PrefabUtility.SaveAsPrefabAsset(player, Player);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Blacktop/Characters/Validate All Appearance Assets")]
    public static void ValidateAll()
    {
        int errors = 0;
        var catalogs = AssetDatabase.FindAssets("t:PlayerAppearanceCatalog")
            .Select(guid => AssetDatabase.LoadAssetAtPath<PlayerAppearanceCatalog>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        foreach (var catalog in catalogs)
            foreach (string error in catalog.ValidateCatalog())
            { Debug.LogError($"{catalog.name}: {error}", catalog); errors++; }
        foreach (string guid in AssetDatabase.FindAssets("t:PlayerAppearanceDefinition"))
        {
            var appearance = AssetDatabase.LoadAssetAtPath<PlayerAppearanceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            // Separate catalogs may own different art families. A definition must resolve in at least one.
            if (catalogs.Any(catalog => catalog.Validate(appearance, out _, false).Count == 0)) continue;
            Debug.LogError($"{appearance.name}: no installed catalog can resolve this complete selection.", appearance); errors++;
        }
        Debug.Log($"Appearance validation finished: {errors} errors. Placeholder art still requires authoring.");
        if (errors > 0) throw new InvalidOperationException("Appearance asset validation failed; see the diagnostics above.");
    }

    // Command-line entry point is also useful for reproducible import/CI validation.
    public static void SetupAndExit()
    {
        GenerateAssets();
        WirePrefabs();
        ValidateAll();
        EditorApplication.Exit(0);
    }
}
