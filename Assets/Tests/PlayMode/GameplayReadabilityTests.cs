using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Verify actual rendered bounds as well as actor/key mapping in the real match scene.
public class GameplayReadabilityTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    PlayState play;
    BallController ball;
    ReceiverSelectionUI ui;
    GameplayLabelLayout layout;
    TeamMember[] actors;
    int preference;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        preference = PlayerPrefs.GetInt(UITheme.PreferenceKey, 0);
        FrontEndRuntime.Match = null;
        yield return SceneManager.LoadSceneAsync("SampleScene"); yield return null;
        play = PlayState.Instance; ball = BallController.Instance; ui = ReceiverSelectionUI.Instance; layout = ui.LabelLayout;
        play.StopAllCoroutines(); play.enabled = false; ball.enabled = false;
        actors = Object.FindObjectsByType<TeamMember>();
        foreach (var actor in actors)
            foreach (var component in actor.GetComponents<MonoBehaviour>())
                if (component is not TeamMember && component is not CharacterApplier && component is not PlayerVisualAssembler)
                    component.enabled = false;
        DefenderControlManager.Instance.enabled = false;
        var camera = Camera.main;
        foreach (var component in camera.GetComponents<MonoBehaviour>()) component.enabled = false;
        camera.transform.position = new Vector3(0f, 14f, -20f); camera.transform.LookAt(new Vector3(0f, 1f, 0f));
        Set(play, "<IsLive>k__BackingField", true);
        Arrange(0);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        UITheme.Select((UIThemeId)preference); FrontEndRuntime.Match = null;
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Readability cleanup"));
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    static void Set(object o, string f, object v) => o.GetType().GetField(f, Private).SetValue(o, v);
    static void Call(object o, string m, params object[] args) => o.GetType().GetMethod(m, Private).Invoke(o, args);
    void UpdateLabels() { Call(ui, "LateUpdate"); Call(layout, "LateUpdate"); Canvas.ForceUpdateCanvases(); }

    void Arrange(int team)
    {
        Set(play, "<IsLive>k__BackingField", false); play.TrySetPossession(team);
        Set(play, "userTeamId", team); Set(play, "<IsLive>k__BackingField", true);
        foreach (var actor in actors) actor.transform.position = new Vector3(10f, 1f, 8f);
        var qb = actors.Single(a => a.teamId == team && a.slot == TeamMember.RosterSlot.QB);
        qb.transform.position = new Vector3(0f, 1f, -4f); ball.AttachTo(qb.transform);
        PossessionController.ClaimControl(qb.transform, team);
        foreach (var receiver in ui.Receivers) receiver.position = new Vector3(0f, 1f, 0f); // worst-case projection overlap
        Call(ui, "ShowLabels"); UpdateLabels();
    }

    static Rect ScreenRect(RectTransform rect, Camera camera = null)
    {
        var points = new Vector3[4]; rect.GetWorldCorners(points);
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        Camera projection = canvas.renderMode == RenderMode.WorldSpace ? Camera.main :
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var screen = points.Select(p => RectTransformUtility.WorldToScreenPoint(projection, p)).ToArray();
        return Rect.MinMaxRect(screen.Min(p => p.x), screen.Min(p => p.y), screen.Max(p => p.x), screen.Max(p => p.y));
    }

    [UnityTest]
    public IEnumerator CrowdedPromptsAndIdentityHaveRealNonOverlappingBoundsInBothThemesDirectionsAndResolutions()
    {
        foreach (int height in new[] { 720, 1080 })
        {
            Screen.SetResolution(height * 16 / 9, height, false); yield return null; yield return null;
            foreach (var theme in new[] { UIThemeId.StreetClassic, UIThemeId.AfterDark })
            foreach (int team in new[] { 0, 1 })
            {
                UITheme.Select(theme); Arrange(team); UpdateLabels();
                Assert.That(layout.PlacedLabels.Count, Is.EqualTo(5));
                var actual = new List<Rect>();
                var cards = new List<RectTransform>();
                for (int i = 0; i < ui.Receivers.Count; i++)
                {
                    var prompt = ui.PromptAt(i); Assert.That(prompt.gameObject.activeSelf, Is.True);
                    Rect bounds = ScreenRect(prompt); actual.Add(bounds); cards.Add(prompt);
                    Assert.That(bounds.width, Is.EqualTo(layout.PromptSize.x * Screen.height / 1080f).Within(1f), "Nested canvas scale must not shrink receiver cards");
                    Assert.That(prompt.GetComponentInChildren<TMP_Text>().text, Does.Contain($"[{i + 1}]"));
                    Assert.That(prompt.GetComponentInChildren<TMP_Text>().text, Does.Contain(ReceiverSelectionUI.ShortIdentity(ui.Receivers[i])));
                    Assert.That(ui.Receivers[i].Find("Nameplate").GetComponent<Canvas>().enabled, Is.False, "No duplicate receiver nameplate");
                }
                var controlled = PossessionController.ActivelyControlled.Find("Nameplate").GetComponent<RectTransform>();
                Assert.That(controlled.GetComponent<Canvas>().enabled, Is.True);
                actual.Add(ScreenRect(controlled)); cards.Add(controlled);
                foreach (var bounds in actual)
                {
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(layout.SafeArea.xMin - 1f));
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(layout.SafeArea.xMax + 1f));
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(layout.SafeArea.yMin - 1f));
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(layout.SafeArea.yMax + 1f));
                }
                for (int i = 0; i < actual.Count; i++)
                for (int j = i + 1; j < actual.Count; j++) Assert.That(actual[i].Overlaps(actual[j]), Is.False);

                // Exact stacks may conceal every anchor. Stagger a tight formation
                // enough to leave visible connectors while still displacing cards.
                for (int i = 0; i < ui.Receivers.Count; i++)
                    ui.Receivers[i].position = new Vector3((i - 1.5f) * 2.2f, 1f, 0f);
                UpdateLabels(); actual = cards.Select(r => ScreenRect(r)).ToList();
                // A screen-space connector must not paint over any card, including
                // cards on the world canvas underneath it.
                var links = Object.FindObjectsByType<RectTransform>().Where(r => r.name == "Label actor link").ToArray();
                Assert.That(links, Is.Not.Empty, "Crowded actors need visible links");
                var corners = new Vector3[4];
                foreach (var link in links)
                {
                    link.GetWorldCorners(corners);
                    var a = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[1]) * .5f);
                    var b = RectTransformUtility.WorldToScreenPoint(null, (corners[2] + corners[3]) * .5f);
                    for (int step = 1; step < 64; step++)
                    {
                        Vector2 point = Vector2.Lerp(a, b, step / 64f);
                        Assert.That(actual.Any(r => Rect.MinMaxRect(r.xMin + 1f, r.yMin + 1f, r.xMax - 1f, r.yMax - 1f).Contains(point)),
                            Is.False, "Links cannot paint over other cards");
                    }
                }
            }
        }
    }

    [TestCase(0, TeamMember.RosterSlot.WR1)]
    [TestCase(1, TeamMember.RosterSlot.WR2)]
    [TestCase(2, TeamMember.RosterSlot.WR3)]
    [TestCase(3, TeamMember.RosterSlot.RB)]
    public void VisibleKeyStillQueuesPassOnTheCurrentCarrierForTheSameActor(int index, TeamMember.RosterSlot slot)
    {
        Assert.That(ui.Receivers[index].GetComponent<TeamMember>().slot, Is.EqualTo(slot));
        var carrier = ball.Carrier;
        Call(ui, "TryPassToReceiver", index);
        Assert.That(ui.GetSelectedReceiverIndex(), Is.EqualTo(index));
        Assert.That(carrier.GetComponent<InputBuffer>().TryConsume("Pass"), Is.True);
        UpdateLabels();
        Assert.That(Enumerable.Range(0, 4).All(i => !ui.PromptAt(i).gameObject.activeSelf), Is.True);
    }

    [Test]
    public void CatchRecoveryAndDefenderCyclingFollowCanonicalIdentityAndClearOnPhaseExit()
    {
        var receiver = ui.Receivers[0]; ball.AttachTo(receiver); PossessionController.ClaimControl(receiver, 0); UpdateLabels();
        Assert.That(layout.PlacedLabels.Keys, Is.EquivalentTo(new[] { receiver }));
        var cpu = actors.First(a => a.teamId == 1).transform;
        ball.AttachTo(cpu); var defender = actors.First(a => a.teamId == 0).transform;
        PossessionController.ClaimControl(defender, 0); UpdateLabels();
        Assert.That(layout.PlacedLabels.Keys, Is.EquivalentTo(new[] { cpu, defender }));
        var next = actors.First(a => a.teamId == 0 && a.transform != defender).transform;
        PossessionController.ClaimControl(next, 0); UpdateLabels();
        Assert.That(defender.Find("Nameplate").GetComponent<Canvas>().enabled, Is.False);
        Assert.That(next.Find("Nameplate").GetComponent<Canvas>().enabled, Is.True);
        ball.Drop(); PossessionController.ReleaseControl(next); UpdateLabels(); Assert.That(layout.PlacedLabels, Is.Empty);
        Set(play, "<IsLive>k__BackingField", false); UpdateLabels(); Assert.That(layout.PlacedLabels, Is.Empty);
    }

    [Test]
    public void OffscreenInvalidActorsDisableAndRebuildNeverLeavesOldCards()
    {
        var receiver = ui.Receivers[0]; receiver.position = new Vector3(1000f, 1f, 0f); UpdateLabels();
        Assert.That(ui.PromptAt(0).gameObject.activeSelf, Is.False);
        receiver.position = Vector3.zero; receiver.gameObject.SetActive(false); UpdateLabels();
        Assert.That(ui.PromptAt(0).gameObject.activeSelf, Is.False); receiver.gameObject.SetActive(true);
        var old = ui.PromptAt(0); Call(ui, "HandlePossessionChanged", 0); UpdateLabels();
        Assert.That(old.gameObject.activeSelf, Is.False);
        ui.enabled = false;
        Assert.That(actors.All(a => !a.transform.Find("Nameplate").GetComponent<Canvas>().enabled), Is.True);
    }
}
