using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Validate the versioned character catalogs and saved appearance presets.
public static class CharacterAppearanceSetup
{
    public const string Data = "Assets/_Project/Data/Characters";
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

    // Batch validation reads the authored production catalogs without generating assets.
    public static void SetupAndExit()
    {
        ValidateAll();
        EditorApplication.Exit(0);
    }
}
