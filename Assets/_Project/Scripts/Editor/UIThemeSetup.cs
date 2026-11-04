using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Idempotent presentation wiring only; never regenerate scenes, player data, field art or prefabs.
public static class UIThemeSetup
{
    const string Folder="Assets/_Project/Resources/BlacktopUI";
    [MenuItem("Blacktop/UI/Wire Theme Assets")]
    public static void Wire()
    {
        MenuPrototypeSetup.Wire();Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var library=AssetDatabase.LoadAssetAtPath<UIThemeLibrary>(Folder+"/ThemeLibrary.asset");
        if(library==null){library=ScriptableObject.CreateInstance<UIThemeLibrary>();AssetDatabase.CreateAsset(library,Folder+"/ThemeLibrary.asset");}
        library.streetClassic=Theme(UIThemeId.StreetClassic);library.afterDark=Theme(UIThemeId.AfterDark);
        var settings=AssetDatabase.LoadAssetAtPath<FrontEndSettings>(FrontEndSetup.SettingsPath);
        library.brand=settings.menuTitleFont;library.semiBold=settings.menuFont;
        string characters=new string(Enumerable.Range(32,95).Select(i=>(char)i).ToArray())+"‹›′″−–—•×+";
        library.regular=MenuPrototypeSetup.Font("BarlowCondensed-Regular",characters);
        library.extraBold=MenuPrototypeSetup.Font("BarlowCondensed-ExtraBold",characters);
        library.boldItalic=MenuPrototypeSetup.Font("BarlowCondensed-BoldItalic",characters);
        library.diagramOffense=AssetDatabase.FindAssets("Shotgun_Base t:FormationData").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<FormationData>).FirstOrDefault();
        library.diagramDefense=AssetDatabase.FindAssets("7v7_Base t:FormationData").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<FormationData>).FirstOrDefault();
        if(library.diagramOffense==null||library.diagramDefense==null)throw new InvalidOperationException("Missing installed schematic fallback formations");
        // All standard game UI now uses Barlow. The wordmark remains exclusive to the brand role.
        settings.theme.displayFont=library.extraBold;settings.theme.legibleFont=library.semiBold;
        EditorUtility.SetDirty(settings.theme);EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
        Debug.Log("UI_THEMES_READY: Street Classic / After Dark / native Barlow weights");
    }
    static BlacktopTheme Theme(UIThemeId id)
    {
        string path=Folder+"/"+id+".asset";var theme=AssetDatabase.LoadAssetAtPath<BlacktopTheme>(path);
        if(theme==null){theme=ScriptableObject.CreateInstance<BlacktopTheme>();theme.SetDefaults(id);AssetDatabase.CreateAsset(theme,path);}
        return theme;
    }
    public static void WireAndExit()
    {
        try{Wire();EditorApplication.Exit(0);}catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    [MenuItem("Blacktop/UI/Build Themed UI (macOS)")]
    public static void Build()
    {
        Wire();Directory.CreateDirectory("Builds/UIOverhaul");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            locationPathName="Builds/UIOverhaul/Blacktop.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("UI build failed: "+report.summary.result);
        Debug.Log($"UI_BUILD_PASS: {report.summary.totalErrors} errors / {report.summary.totalWarnings} warnings / {report.summary.totalSize} bytes");
    }
    public static void BuildAndExit()
    {
        try{Build();EditorApplication.Exit(0);}catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
