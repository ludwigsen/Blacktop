using System.Linq;
using UnityEditor;
using UnityEngine;

// A lightweight isolated preview: no match, player AI, input or gameplay managers are spawned.
public class PlayerAppearancePreview : EditorWindow
{
    PlayerAppearanceCatalog catalog;
    PlayerAppearanceDefinition appearance;
    TeamIdentity team;
    GameObject visualPrefab;
    PreviewRenderUtility preview;
    GameObject previewObject;
    PlayerVisualAssembler assembler;
    int jerseyNumber;
    float locomotion;
    string diagnostics;
    Vector2 scroll;

    [MenuItem("Blacktop/Characters/Appearance Preview")]
    public static void Open() => GetWindow<PlayerAppearancePreview>("Player Appearance");

    void OnEnable()
    {
        catalog = AssetDatabase.LoadAssetAtPath<PlayerAppearanceCatalog>(CharacterAppearanceSetup.Data + "/PrototypeCatalog.asset");
        appearance = AssetDatabase.LoadAssetAtPath<PlayerAppearanceDefinition>(CharacterAppearanceSetup.Data + "/Appearance_Standard.asset");
        visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PlayerModel.prefab");
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        catalog = (PlayerAppearanceCatalog)EditorGUILayout.ObjectField("Catalog", catalog, typeof(PlayerAppearanceCatalog), false);
        appearance = (PlayerAppearanceDefinition)EditorGUILayout.ObjectField("Appearance", appearance, typeof(PlayerAppearanceDefinition), false);
        visualPrefab = (GameObject)EditorGUILayout.ObjectField("Visual prefab", visualPrefab, typeof(GameObject), false);
        team = (TeamIdentity)EditorGUILayout.ObjectField("Team preview", team, typeof(TeamIdentity), false);
        jerseyNumber = EditorGUILayout.IntSlider("Jersey number", jerseyNumber, 0, 99);
        if (appearance != null && catalog != null)
        {
            var data = new SerializedObject(appearance);
            data.Update();
            foreach (var field in new[] { "id", "bodyArchetype", "heightProfile", "headScale", "appearanceSeed" })
                EditorGUILayout.PropertyField(data.FindProperty(field));
            var currentHeight = data.FindProperty("heightProfile").objectReferenceValue as PlayerHeightProfile;
            var nextHeight = (PlayerHeight)EditorGUILayout.EnumPopup("Discrete height", currentHeight != null ? currentHeight.height : PlayerHeight.Ft6In0);
            if (currentHeight == null || nextHeight != currentHeight.height)
            {
                var profile = catalog.heightProfiles.FirstOrDefault(p => p != null && p.height == nextHeight);
                if (profile != null) data.FindProperty("heightProfile").objectReferenceValue = profile;
            }
            Part("bodyId", CharacterPartSlot.Body);
            Part("faceId", CharacterPartSlot.Face);
            Part("hairId", CharacterPartSlot.Hair);
            Part("facialHairId", CharacterPartSlot.FacialHair);
            Palette("skinToneId", catalog.skinTones);
            Palette("hairColorId", catalog.hairColors);
            EditorGUILayout.PropertyField(data.FindProperty("clothingIds"), true);
            EditorGUILayout.PropertyField(data.FindProperty("accessoryIds"), true);
            data.ApplyModifiedProperties();
            var errors = catalog.Validate(appearance, out var selected);
            foreach (var error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (var part in selected.Where(p => p.placeholder))
                EditorGUILayout.HelpBox($"{part.id}: PLACEHOLDER. {part.authoringNote}", MessageType.Warning);

            void Part(string field, CharacterPartSlot slot)
            {
                var options = catalog.parts.Where(p => p != null && p.slot == slot &&
                    (slot != CharacterPartSlot.Body || p.bodyArchetype == (BodyArchetype)data.FindProperty("bodyArchetype").enumValueIndex)).ToArray();
                var property = data.FindProperty(field);
                int index = System.Array.FindIndex(options, p => p.id == property.stringValue);
                int next = EditorGUILayout.Popup(ObjectNames.NicifyVariableName(field), index,
                    options.Select(p => p.id + (p.placeholder ? " [PLACEHOLDER]" : "")).ToArray());
                // Invalid saved IDs stay visible as validation errors until explicitly selected.
                if (next >= 0 && next != index) property.stringValue = options[next].id;
            }
            void Palette(string field, AppearanceColor[] colors)
            {
                var property = data.FindProperty(field);
                int index = System.Array.FindIndex(colors, c => c.id == property.stringValue);
                int next = EditorGUILayout.Popup(ObjectNames.NicifyVariableName(field), index, colors.Select(c => c.id).ToArray());
                if (next >= 0 && next != index) property.stringValue = colors[next].id;
            }
        }
        if (GUILayout.Button("Assemble / refresh preview")) BuildPreview();
        locomotion = EditorGUILayout.Slider("Locomotion (-1 back, 0 idle, 1 run, 2 fast)", locomotion, -1f, 2f);
        if (!string.IsNullOrEmpty(diagnostics)) EditorGUILayout.HelpBox(diagnostics, MessageType.Info);
        var rect = GUILayoutUtility.GetRect(250f, 420f, GUILayout.ExpandWidth(true));
        if (preview != null && Event.current.type == EventType.Repaint)
        {
            var animator = assembler.rig.animator;
            animator.SetFloat("Locomotion", locomotion);
            animator.Update(0.02f);
            preview.BeginPreview(rect, GUIStyle.none);
            preview.Render(true);
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.ScaleToFit, false);
        }
        EditorGUILayout.EndScrollView();
    }

    void BuildPreview()
    {
        Cleanup();
        if (visualPrefab == null || appearance == null || catalog == null) return;
        preview = new PreviewRenderUtility();
        previewObject = new GameObject("Appearance preview");
        var visual = Instantiate(visualPrefab, previewObject.transform, false);
        assembler = previewObject.AddComponent<PlayerVisualAssembler>();
        assembler.catalog = catalog;
        assembler.rig = visual.GetComponent<PlayerVisualRig>();
        if (assembler.rig == null) { diagnostics = "Visual prefab needs a PlayerVisualRig adapter."; Cleanup(); return; }
        bool applied = assembler.TryApply(appearance, team, jerseyNumber);
        diagnostics = assembler.LastDiagnostics;
        preview.AddSingleGO(previewObject);
        preview.camera.transform.SetPositionAndRotation(new Vector3(3f, 1f, 4.5f), Quaternion.LookRotation(new Vector3(-3f, -0.9f, -4.5f)));
        preview.camera.nearClipPlane = 0.1f;
        preview.camera.farClipPlane = 30f;
        preview.camera.fieldOfView = 30f;
        preview.lights[0].intensity = 1.2f;
        preview.lights[0].transform.rotation = Quaternion.Euler(40f, 30f, 0f);
        preview.lights[1].intensity = 0.7f;
        if (!applied) Repaint();
    }

    void Cleanup()
    {
        // Destroy the assembler before its imported bones, so attachment cleanup remains safe.
        if (previewObject != null) DestroyImmediate(previewObject);
        preview?.Cleanup();
        preview = null;
    }
    void OnDisable() => Cleanup();
}
