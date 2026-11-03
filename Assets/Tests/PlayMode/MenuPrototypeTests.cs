using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Exercise the shipped UI against a unique save file, including persistence and normal-build gates.
public class MenuPrototypeTests
{
    FrontEndSettings settings;
    FrontEndController menu;
    RosterService service;
    string directory;
    bool authoring;
    [UnitySetUp]
    public IEnumerator Setup()
    {
        FrontEndRuntime.Match=null;
        yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;
        settings=Object.FindAnyObjectByType<MatchBootstrap>().settings;
        authoring=settings.allowDevelopmentEditing;settings.allowDevelopmentEditing=true;
        directory=Path.Combine(Path.GetTempPath(),"blacktop-menu-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        service=new RosterService(settings,new RosterSaveStore(Path.Combine(directory,"rosters.json")));
        typeof(FrontEndRuntime).GetProperty("Rosters").SetValue(null,service);
        yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
        menu=Object.FindAnyObjectByType<FrontEndController>();
        yield return new WaitForSecondsRealtime(.35f);
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        settings.allowDevelopmentEditing=authoring;
        FrontEndRuntime.Match=null;typeof(FrontEndRuntime).GetProperty("Rosters").SetValue(null,null);
        var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Menu prototype cleanup"));
        yield return SceneManager.UnloadSceneAsync(old);
        if(Directory.Exists(directory))Directory.Delete(directory,true);
    }
    static Button Button(string label)=>Object.FindObjectsByType<Button>().Single(b=>b.name==label&&b.gameObject.activeInHierarchy);
    static void Click(string label){var button=Button(label);Assert.That(button.interactable,Is.True,label);button.onClick.Invoke();}

    [UnityTest]
    public IEnumerator FieldCycleBlurAndMainNavigationWork()
    {
        Assert.That(settings.menuFields.Length,Is.EqualTo(4));Assert.That(settings.menuFields.All(t=>t!=null),Is.True);
        Assert.That(settings.menuTitleFont.name,Does.Contain("BlacktopProDuo"));Assert.That(settings.menuFont.name,Does.Contain("BarlowCondensed"));
        Assert.That(Object.FindObjectsByType<Button>().Count(b=>b.isActiveAndEnabled),Is.EqualTo(4));
        Capture("Main",1920,1080);
        int index=menu.Backdrop.CurrentIndex;
        float hold=settings.fieldHoldSeconds,fade=settings.fieldBlendSeconds;
        settings.fieldHoldSeconds=1;settings.fieldBlendSeconds=.1f;
        yield return new WaitForSecondsRealtime(1.25f);
        Assert.That(menu.Backdrop.CurrentIndex,Is.Not.EqualTo(index));
        settings.fieldHoldSeconds=hold;settings.fieldBlendSeconds=fade;
        Click("SETTINGS");yield return new WaitForSecondsRealtime(.4f);
        Assert.That(menu.Screen,Is.EqualTo(FrontEndScreen.Preferences));Assert.That(menu.Backdrop.Blur,Is.GreaterThan(.99f));
        Capture("Settings",1920,1080);menu.Back();yield return new WaitForSecondsRealtime(.4f);
        Assert.That(menu.Backdrop.Blur,Is.LessThan(.01f));
        Click("NEW GAME");Assert.That(menu.Screen,Is.EqualTo(FrontEndScreen.Modes));
        Assert.That(Object.FindObjectsByType<TeamMember>(),Is.Empty);
    }

    [UnityTest]
    public IEnumerator AttributeAndWeightEditsPersistThroughTheRealSaveWithoutSourceChanges()
    {
        string source=JsonUtility.ToJson(settings.Team("kings").roster);
        Click("EDIT PLAYERS");string id=menu.Session.Draft.record.id;
        Click("EDIT ATTRIBUTES");yield return new WaitForSecondsRealtime(.35f);
        var lab=Object.FindAnyObjectByType<PlayerLabView>();Assert.That(lab.GetComponentsInChildren<ScrollRect>(),Is.Empty);
        var sliders=lab.GetComponentsInChildren<Slider>();Assert.That(sliders.Length,Is.EqualTo(12));
        int original=service.Team("kings").players.Single(p=>p.record.id==id).record.passing;
        sliders[0].value=original==20?0:20;sliders[11].value=0;
        Assert.That(service.Team("kings").players.Single(p=>p.record.id==id).record.passing,Is.EqualTo(original));
        Click("SAVE PLAYER");Assert.That(menu.LabPage,Is.EqualTo(PlayerLabPage.Summary));
        Click("EDIT FIT & BODY");yield return null;
        Object.FindObjectsByType<TMP_InputField>().Single(f=>f.name=="WEIGHT (LB)").text="247";
        Click("SAVE PLAYER");
        var reloaded=new RosterService(settings,new RosterSaveStore(Path.Combine(directory,"rosters.json")));
        var saved=reloaded.Team("kings").players.Single(p=>p.record.id==id);
        Assert.That(saved.record.passing,Is.EqualTo(original==20?0:20));Assert.That(saved.appearance.weightPounds,Is.EqualTo(247));
        Assert.That(JsonUtility.ToJson(settings.Team("kings").roster),Is.EqualTo(source));
        // Legacy records still resolve the established estimate; invalid overrides are rejected atomically.
        saved.appearance.weightPounds=0;Assert.That(saved.appearance.Pounds(settings.weights),Is.EqualTo(settings.weights.Pounds(saved.appearance.height,saved.appearance.bodyArchetype)));
        saved.appearance.weightPounds=451;Assert.That(service.SavePlayer("kings",saved,-1,0,out _,out var error),Is.False);Assert.That(error,Does.Contain("weight"));
    }

    [UnityTest]
    public IEnumerator SwitchingPlayersPromptsAndDiscardKeepsTheSavedRoster()
    {
        menu.ShowPlayerLab("kings");string id=menu.Session.Draft.record.id;string before=JsonUtility.ToJson(service.Snapshot);
        menu.ShowLabPage(PlayerLabPage.Attributes);yield return null;
        var slider=Object.FindAnyObjectByType<PlayerLabView>().GetComponentsInChildren<Slider>()[0];slider.value=slider.value==20?0:20;
        string other=service.Team("kings").players.First(p=>p.record.id!=id).record.id;
        menu.SelectLabPlayer("kings",other);Click("CANCEL / KEEP EDITING");Assert.That(menu.Session.Draft.record.id,Is.EqualTo(id));
        menu.SelectLabPlayer("kings",other);Click("DISCARD");Assert.That(menu.Session.Draft.record.id,Is.EqualTo(other));
        Assert.That(JsonUtility.ToJson(service.Snapshot),Is.EqualTo(before));
    }

    [UnityTest]
    public IEnumerator StandardAccessLocksAuthoringButKeepsCosmeticsAvailable()
    {
        settings.allowDevelopmentEditing=false;menu.ShowPlayerLab("kings");
        Assert.That(Button("EDIT ATTRIBUTES").interactable,Is.False);
        menu.ShowLabPage(PlayerLabPage.Attributes);Assert.That(menu.LabPage,Is.EqualTo(PlayerLabPage.Summary));
        Click("EDIT FIT & BODY");yield return null;
        Assert.That(Object.FindAnyObjectByType<PlayerLabView>().GetComponentsInChildren<TMP_InputField>(),Is.Empty);
        Assert.That(Object.FindObjectsByType<Button>().Any(b=>b.name=="DEV ROSTER TOOLS"),Is.False);
        menu.ShowLabPage(PlayerLabPage.Identity);Assert.That(menu.LabPage,Is.EqualTo(PlayerLabPage.Summary));
        menu.ShowLabPage(PlayerLabPage.Fit);yield return null;
        Assert.That(Object.FindAnyObjectByType<PlayerLabView>().GetComponentsInChildren<Button>().Length,Is.GreaterThan(6));
    }

    [UnityTest]
    public IEnumerator EveryLabPageFitsWithoutScrollingAt1080And720()
    {
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
        {
            Screen.SetResolution(size.x,size.y,false);yield return null;yield return null;
            menu.ShowMain();yield return new WaitForSecondsRealtime(.35f);Capture("Main-"+size.y,size.x,size.y);
            menu.ShowPlayerLab("kings");
            foreach(PlayerLabPage page in Enum.GetValues(typeof(PlayerLabPage)))
            {
                menu.ShowLabPage(page);yield return new WaitForSecondsRealtime(.35f);Canvas.ForceUpdateCanvases();
                var lab=Object.FindAnyObjectByType<PlayerLabView>();Assert.That(lab.GetComponentsInChildren<ScrollRect>(),Is.Empty,page.ToString());
                foreach(var control in lab.GetComponentsInChildren<Selectable>())
                {
                    var corners=new Vector3[4];((RectTransform)control.transform).GetWorldCorners(corners);
                    Assert.That(corners.All(p=>p.x>=-2&&p.y>=-2&&p.x<=Screen.width+2&&p.y<=Screen.height+2),Is.True,page+" / "+control.name);
                }
                Capture(page+"-"+size.y,size.x,size.y);
            }
        }
    }

    static void Capture(string name,int width,int height)
    {
        // Render the real canvas with a native camera: batchmode has no reliable Game view capture.
        var go=new GameObject("Menu capture",typeof(Camera));var camera=go.GetComponent<Camera>();camera.cullingMask=-1;
        camera.transform.position=new Vector3(0,0,-10000);camera.orthographic=true;camera.orthographicSize=5;
        camera.nearClipPlane=.01f;camera.farClipPlane=10;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        var target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
        var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
        Canvas.ForceUpdateCanvases();camera.Render();
        var old=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();RenderTexture.active=old;
        Assert.That(texture.GetPixels32().Any(c=>c.r>40||c.g>40||c.b>40),Is.True,"UI capture is blank: "+name);
        string folder=Path.Combine(Application.dataPath,"../Builds/Validation");Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder,"Menu-"+name+".png"),texture.EncodeToPNG());Object.Destroy(texture);
        foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}
        camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(go);Canvas.ForceUpdateCanvases();
    }
}
