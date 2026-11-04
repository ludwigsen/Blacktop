using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// One explicit asset-wiring command; it never regenerates scenes, rosters, rigs or venue data.
public static class MenuPrototypeSetup
{
    const string Art="Assets/_Project/Art/MainMenu";
    [MenuItem("Blacktop/Front End/Wire Menu Prototype Assets")]
    public static void Wire()
    {
        AssetDatabase.Refresh();
        var settings=AssetDatabase.LoadAssetAtPath<FrontEndSettings>(FrontEndSetup.SettingsPath);
        settings.menuTitleFont=Font("BlacktopProDuo", "Blacktop");
        settings.menuFont=Font("BarlowCondensed-SemiBold",new string(Enumerable.Range(32,95).Select(i=>(char)i).ToArray())+"‹›′″−–—•×");
        settings.menuFields=new[]{"Alley","Beach","Downtown","Junkyard","Quad","Rooftop","Shipyard","Suburbs","The-Lot","Underpass","Warehouse"}.Select(name=>{
            string path=Art+"/Fields/"+name+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(importer==null)throw new InvalidOperationException("Missing menu field: "+path);
            // Keep the supplied 3840x2160 framing instead of rounding to power-of-two dimensions.
            importer.textureType=TextureImporterType.Default;importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;
            importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }).ToArray();
        settings.menuBlurShader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"/MenuFieldBlur.shader");
        settings.menuBlurFilterShader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"/MenuFieldGaussian.shader");
        EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
        Debug.Log("MENU_PROTOTYPE_ASSETS_READY: "+settings.menuFields.Length+" fields, Blacktop Pro Duo and Barlow Condensed.");
    }

    public static TMP_FontAsset Font(string name,string characters)
    {
        string path=Art+"/Fonts/"+name+" SDF.asset";
        var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(asset!=null)return asset;
        var source=AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(Art+"/Fonts/"+name+".ttf");
        if(source==null)throw new InvalidOperationException("Missing font "+name);
        asset=TMP_FontAsset.CreateFontAsset(source,100,12,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
        asset.name=name+" SDF";AssetDatabase.CreateAsset(asset,path);
        if(!asset.TryAddCharacters(characters,out string missing))Debug.LogWarning(name+" missing glyphs: "+missing);
        // Persist atlas/material sub-assets so standalone builds have the same prewarmed typography.
        foreach(var atlas in asset.atlasTextures){atlas.name=name+" Atlas";AssetDatabase.AddObjectToAsset(atlas,asset);}
        asset.material.name=name+" Material";AssetDatabase.AddObjectToAsset(asset.material,asset);
        EditorUtility.SetDirty(asset);return asset;
    }

    public static void WireAndExit()
    {
        try{Wire();EditorApplication.Exit(0);}
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }

    [MenuItem("Blacktop/Front End/Build Menu Prototype (macOS)")]
    public static void Build()
    {
        Wire();Directory.CreateDirectory("Builds/MenuPrototype");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            locationPathName="Builds/MenuPrototype/Blacktop Menu Prototype.app",target=BuildTarget.StandaloneOSX,
            options=BuildOptions.Development });
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Menu prototype build failed: "+report.summary.result);
        Debug.Log("MENU_PROTOTYPE_BUILD_PASS: "+report.summary.totalSize+" bytes");
    }
    public static void BuildAndExit()
    {
        try{Build();EditorApplication.Exit(0);}
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
