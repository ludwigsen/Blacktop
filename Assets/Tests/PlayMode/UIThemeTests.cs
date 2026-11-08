using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Real scene integration with disposable rosters and restored preferences. No source save is mutated.
public class UIThemeTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    FrontEndController menu;FrontEndSettings settings;string directory;
    int preference;bool hadPreference;
    [UnitySetUp] public IEnumerator Setup()
    {
        hadPreference=PlayerPrefs.HasKey(UITheme.PreferenceKey);preference=PlayerPrefs.GetInt(UITheme.PreferenceKey,0);
        UITheme.Select(UIThemeId.StreetClassic);FrontEndRuntime.Match=null;
        yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;
        settings=Object.FindAnyObjectByType<MatchBootstrap>().settings;
        directory=Path.Combine(Path.GetTempPath(),"blacktop-themes-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        var service=new RosterService(settings,new RosterSaveStore(Path.Combine(directory,"rosters.json")));
        typeof(FrontEndRuntime).GetProperty("Rosters").SetValue(null,service);
        yield return SceneManager.LoadSceneAsync("MainMenu");yield return new WaitForSecondsRealtime(.4f);
        menu=Object.FindAnyObjectByType<FrontEndController>();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        FrontEndRuntime.Match=null;typeof(FrontEndRuntime).GetProperty("Rosters").SetValue(null,null);
        if(hadPreference)PlayerPrefs.SetInt(UITheme.PreferenceKey,preference);else PlayerPrefs.DeleteKey(UITheme.PreferenceKey);
        PlayerPrefs.Save();UITheme.LoadPreference();
        var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Theme cleanup"));
        yield return SceneManager.UnloadSceneAsync(old);
        if(Directory.Exists(directory))Directory.Delete(directory,true);
    }
    static Button Button(string name)=>Object.FindObjectsByType<Button>().Single(b=>b.name==name&&b.gameObject.activeInHierarchy);
    static void Click(string name)=>Button(name).onClick.Invoke();

    [UnityTest] public IEnumerator SettingsSwitchImmediatelyPersistAndPreserveRosterAndTeamColors()
    {
        string before=JsonUtility.ToJson(menu.Service.Snapshot);Color team=settings.teams[0].identity.accentColor;
        Click("SETTINGS");yield return new WaitForSecondsRealtime(.35f);
        var label=Object.FindObjectsByType<TMP_Text>().Single(t=>t.text=="UI THEME");
        label.transform.parent.GetComponentsInChildren<Button>().Last().onClick.Invoke();
        Assert.That(UITheme.Current.id,Is.EqualTo(UIThemeId.AfterDark));
        Assert.That(PlayerPrefs.GetInt(UITheme.PreferenceKey),Is.EqualTo(1));
        var target=Button("BACK");EventSystem.current.SetSelectedGameObject(target.gameObject);
        Assert.That(target.GetComponent<Image>().color,Is.EqualTo(UITheme.Color(UIColorRole.SelectionHighlight)));
        Assert.That(target.GetComponentInChildren<TMP_Text>().color,Is.EqualTo(UITheme.Color(UIColorRole.TextOnBright)));
        UITheme.LoadPreference();menu.ShowPlayerLab();yield return null;
        Assert.That(UITheme.Current.id,Is.EqualTo(UIThemeId.AfterDark));
        Assert.That(JsonUtility.ToJson(menu.Service.Snapshot),Is.EqualTo(before));
        Assert.That(settings.teams[0].identity.accentColor,Is.EqualTo(team));
        Assert.That(Object.FindObjectsByType<TMP_Text>().All(t=>t.font.name.Contains("Barlow")),Is.True);
    }

    [TestCase(0)] [TestCase(4)] [TestCase(5)] [TestCase(8)] [TestCase(9)] [TestCase(12)]
    [TestCase(13)] [TestCase(16)] [TestCase(17)] [TestCase(20)]
    public void RatingsUseTwentyExactCellsAndFixedTiersAcrossThemes(int rating)
    {
        var root=new GameObject("Rating fixture",typeof(RectTransform));
        try
        {
            var slider=FrontEndUI.SquareRating(root.transform,"SPEED",rating,_=>{},settings);
            var cells=slider.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("Step ")).ToArray();
            Assert.That(cells.Length,Is.EqualTo(20));
            foreach(UIThemeId theme in Enum.GetValues(typeof(UIThemeId)))
            {
                UITheme.Select(theme);
                Assert.That(cells.Count(i=>i.color==UITheme.RatingColor(rating)),Is.EqualTo(rating));
                Assert.That(cells.Take(rating).All(i=>i.color==UITheme.RatingColor(rating)),Is.True);
            }
        }
        finally{Object.DestroyImmediate(root);}
    }

    [UnityTest] public IEnumerator ResolvedTouchdownsCannotBeReplacedByFirstDownAfterTheReceivingDriveReset()
    {
        foreach(UIThemeId theme in Enum.GetValues(typeof(UIThemeId)))
        foreach(bool touchdown in new[]{true,false})
        {
            UITheme.Select(theme);
            yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;yield return null;
            var ps=PlayState.Instance;var ball=BallController.Instance;int team=ps.PossessionTeamId;
            var direction=ps.DirectionFor(team);
            var holder=Object.FindObjectsByType<TeamMember>().Single(p=>p.teamId==team&&p.slot==TeamMember.RosterSlot.RB);
            float z=touchdown?direction.Advance(direction.TargetGoalLineZ,1):direction.Advance(ps.CurrentLineOfScrimmageZ,DownsTracker.Instance.YardsToGo+1);
            typeof(PlayState).GetField("<IsLive>k__BackingField",Private).SetValue(ps,true);
            typeof(BallController).GetField("carryOffset",Private).SetValue(ball,Vector3.zero);
            holder.transform.position=new Vector3(0,1,z);ball.AttachTo(holder.transform);
            ps.EndPlay(touchdown?PlayState.PlayEndReason.Touchdown:PlayState.PlayEndReason.Tackled);
            var hud=Object.FindAnyObjectByType<GameHUD>();
            var root=(GameObject)typeof(GameHUD).GetField("postPlayRoot",Private).GetValue(hud);
            Assert.That(root.activeSelf,Is.True);
            Assert.That(root.GetComponentInChildren<TMP_Text>().text,Is.EqualTo(touchdown?"TOUCHDOWN!":"FIRST DOWN!"));
            Assert.That(root.GetComponents<GameAnnouncement>().Length,Is.EqualTo(1));
        }
    }

    [UnityTest] public IEnumerator DiagramsContainAuthoredGeometry()
    {
        Assert.That(UITheme.Library,Is.Not.Null);
        Assert.That(UITheme.Library.diagramOffense,Is.Not.Null);
        Assert.That(UITheme.Library.diagramOffense.offensiveSlots.Count,Is.EqualTo(6));
        yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;yield return null;
        Canvas.ForceUpdateCanvases();
        var diagrams=Object.FindObjectsByType<PlayDiagram>();Assert.That(diagrams.Length,Is.GreaterThan(0));
        foreach(var diagram in diagrams)
        {
            var mesh=diagram.canvasRenderer.GetMesh();
            Debug.Log($"DIAGRAM_MESH: {diagram.name} rect={diagram.rectTransform.rect} vertices={mesh.vertexCount} alpha={diagram.canvasRenderer.GetAlpha()} shader={diagram.material.shader.name} color={diagram.color}");
            Assert.That(mesh.vertexCount,Is.GreaterThan(10),"Authored diagram has no drawable geometry");
        }
        MenuPrototypeTests.Capture("UI-Diagram-Diagnostic",1920,1080);
    }

    [UnityTest] public IEnumerator LiveScorebugUsesTeamIdentityAndAuthoredDiagramsInBothThemes()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;yield return null;
        var hud=Object.FindAnyObjectByType<GameHUD>();var ps=PlayState.Instance;
        yield return new WaitForSecondsRealtime(1f); // Let the real field camera settle before capturing it.
        foreach(UIThemeId id in Enum.GetValues(typeof(UIThemeId)))
        {
            UITheme.Select(id);
            var panels=hud.GetComponentsInChildren<TeamScorePanel>();Assert.That(panels.Length,Is.EqualTo(2));
            for(int i=0;i<2;i++)
            {
                Assert.That(panels[i].transform.Find("AccentRule").GetComponent<Image>().color,Is.EqualTo(ps.IdentityFor(i).secondaryAccentColor));
                var plate = panels[i].transform.Find("Team paint plate").GetComponent<Image>();
                Assert.That(plate.sprite, Is.SameAs(UITheme.Library.brushPlate));
                Assert.That(plate.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(plate.color, Is.EqualTo(Color.Lerp(Color.black, ps.IdentityFor(i).accentColor, .53f)));
                Assert.That(plate.rectTransform.localScale.x, Is.EqualTo(i == 0 ? 1 : -1));
            }
            Assert.That(Object.FindObjectsByType<PlayDiagram>().Any(d=>d.enabled),Is.True);
            // Team names and Barlow numerals must bind separate atlas materials after a theme switch.
            foreach (var text in hud.GetComponentsInChildren<TMP_Text>())
            {
                Assert.That(text.font.name, Does.Contain(text.name == "TeamName" ? "BlacktopProDuo" : "Barlow"));
                if (text.name != "TeamName") continue;

                text.ForceMeshUpdate();
                Assert.That(text.isTextTruncated, Is.False);
                for (int j = 0; j < text.textInfo.characterCount; j++)
                {
                    var glyph = text.textInfo.characterInfo[j];
                    if (!char.IsWhiteSpace(glyph.character))
                        Assert.That(glyph.fontAsset, Is.SameAs(text.font));
                }
            }
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(3840,2160)})
            {
                Screen.SetResolution(size.x,size.y,false);yield return null;yield return null;
                typeof(PlayState).GetField("playClockTimer",Private).SetValue(ps,40f);
                ps.TrySetPossession(ps.UserTeamId);yield return null;
                MenuPrototypeTests.Capture($"StreetKit-{id}-{size.y}-Offense",size.x,size.y);
                ps.TrySetPossession(PlayState.OpponentOf(ps.UserTeamId));yield return null;
                MenuPrototypeTests.Capture($"StreetKit-{id}-{size.y}-Defense",size.x,size.y);
                var playCanvas=Object.FindAnyObjectByType<PlayCallSelector>().GetComponentInChildren<CanvasGroup>();
                float previousAlpha=playCanvas.alpha;playCanvas.alpha=0; // Visual fixture matches the normal post-play overlay state.
                foreach(AnnouncementKind kind in Enum.GetValues(typeof(AnnouncementKind)))
                {
                    var root=(GameObject)typeof(GameHUD).GetField("postPlayRoot",Private).GetValue(hud);
                    root.GetComponent<GameAnnouncement>().Show(kind,kind.ToString().ToUpperInvariant());
                    Assert.That(root.GetComponentInChildren<TMP_Text>().font, Is.SameAs(UITheme.Library.boldItalic));
                    Assert.That(root.transform.Find("Announcement paint").GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
                    yield return new WaitForSecondsRealtime(.25f);
                    MenuPrototypeTests.Capture($"StreetKit-{id}-{size.y}-{kind}",size.x,size.y);root.SetActive(false);
                }
                playCanvas.alpha=previousAlpha;
                typeof(GameHUD).GetMethod("ShowPostPlayResult",Private).Invoke(hud,new object[]{PlayState.PlayEndReason.Touchdown});
                Assert.That(hud.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="TOUCHDOWN!"),Is.True);
                typeof(GameHUD).GetMethod("HidePostPlayResult",Private).Invoke(hud,new object[]{PlayState.PlayEndReason.Touchdown});
            }
        }
    }

    [UnityTest] public IEnumerator ThemesRenderAtEverySupportedResolutionWithSafeSecondaryWordmarks()
    {
        foreach(UIThemeId id in Enum.GetValues(typeof(UIThemeId)))
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(3840,2160)})
        {
            UITheme.Select(id);Screen.SetResolution(size.x,size.y,false);yield return null;yield return null;
            menu.ShowMain();yield return new WaitForSecondsRealtime(.35f);Capture("Main");
            menu.ShowPreferences();yield return new WaitForSecondsRealtime(.35f);Capture("Settings");CheckWordmark();
            menu.ShowModes();yield return new WaitForSecondsRealtime(.35f);Capture("Modes");
            menu.BeginHeadToHead();yield return new WaitForSecondsRealtime(.35f);Capture("Teams");
            menu.ShowSettings();yield return new WaitForSecondsRealtime(.35f);Capture("MatchSettings");
            menu.BeginPickup();yield return new WaitForSecondsRealtime(.35f);Capture("Pickup");
            menu.ShowRoster("kings");yield return new WaitForSecondsRealtime(.35f);Capture("Roster");
            menu.ShowPlayerLab("kings");yield return new WaitForSecondsRealtime(.35f);Capture("Summary");
            menu.ShowLabPage(PlayerLabPage.Attributes);yield return new WaitForSecondsRealtime(.35f);Capture("Attributes");
            menu.ShowLabPage(PlayerLabPage.Body);yield return new WaitForSecondsRealtime(.35f);Capture("Accordion");CheckWordmark();
            var scroll=Object.FindAnyObjectByType<PlayerLabView>().GetComponentInChildren<ScrollRect>();
            var last=scroll.content.GetComponentsInChildren<Button>().Last();EventSystem.current.SetSelectedGameObject(last.gameObject);
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.viewport.rect.Contains(scroll.viewport.InverseTransformPoint(last.transform.position)),Is.True,"Controller focus must reveal accordion headers");
            void Capture(string screen)
            {
                MenuPrototypeTests.Capture($"StreetKit-{id}-{size.y}-{screen}",size.x,size.y);
                var controls=menu.GetComponentsInChildren<Selectable>().Where(c=>c.IsActive()&&c.GetComponentInParent<ScrollRect>()==null);
                foreach(var control in controls)
                {
                    var corners=new Vector3[4];((RectTransform)control.transform).GetWorldCorners(corners);
                    Assert.That(corners.All(p=>p.x>=-2&&p.y>=-2&&p.x<=Screen.width+2&&p.y<=Screen.height+2),Is.True,screen+" / "+control.name);
                }
            }
            void CheckWordmark()
            {
                var wordmark = menu.GetComponentsInChildren<Image>().Single(t => t.name == "Secondary Blacktop wordmark");
                Assert.That(wordmark.sprite, Is.SameAs(UITheme.Library.wordmark));
                Assert.That(wordmark.enabled, Is.True);
                var corners = new Vector3[4];
                wordmark.rectTransform.GetWorldCorners(corners);
                Assert.That(Screen.height - corners.Max(c => c.y),
                    Is.GreaterThanOrEqualTo(20 * wordmark.canvas.scaleFactor), "The logo artwork must keep its top clearance");
            }
        }
    }
}
