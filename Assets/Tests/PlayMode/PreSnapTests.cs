using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Exercise real scene phases and bindings with virtual devices.
public class PreSnapTests
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    PlayState play;
    CPUPlayCaller cpu;
    PreSnapPlayPreview preview;
    Keyboard keyboard;
    Gamepad gamepad;
    float oldCaptureDelta;
    InputSettings.BackgroundBehavior oldBackgroundInput;
#if UNITY_EDITOR
    InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
#endif

    [UnitySetUp]
    public IEnumerator LoadMatch()
    {
        oldCaptureDelta = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f / 60f;
        // Route virtual input to gameplay even when the batch Editor has no focused Game view.
        oldBackgroundInput = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        FrontEndRuntime.Match = null;
        keyboard = InputSystem.AddDevice<Keyboard>();
        gamepad = InputSystem.AddDevice<Gamepad>();
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        play = PlayState.Instance;
        cpu = play.GetComponent<CPUPlayCaller>();
        preview = play.GetComponent<PreSnapPlayPreview>();
        Set(play, "formationMoveSpeed", 1000f);
        Set(play, "playEndPresentationDelay", 0f);
        Set(cpu, "snapDelayAfterSetRange", Vector2.one * 100f);
    }

    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        InputSystem.RemoveDevice(keyboard);
        InputSystem.RemoveDevice(gamepad);
        InputSystem.settings.backgroundBehavior = oldBackgroundInput;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
#endif
        Time.captureDeltaTime = oldCaptureDelta;
        var match = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Pre-snap cleanup"));
        yield return SceneManager.UnloadSceneAsync(match);
    }

    static void Set(object instance, string field, object value) =>
        instance.GetType().GetField(field, Private).SetValue(instance, value);

    void Keys(params Key[] pressed)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(pressed));
        InputSystem.Update();
    }
    void Pad(Vector2 move = default, bool hold = false, bool cycle = false)
    {
        var state = new GamepadState { leftStick = move, leftTrigger = hold ? 1f : 0f };
        if (cycle) state = state.WithButton(GamepadButton.RightShoulder);
        InputSystem.QueueStateEvent(gamepad, state);
        InputSystem.Update();
    }

    Canvas PreviewCanvas => preview.transform.Find("Pre-snap play preview").GetComponent<Canvas>();
    CanvasGroup PlayPicker => preview.transform.Find("PlayCallCanvas").GetComponent<CanvasGroup>();
    Transform Art => PreviewCanvas.transform.Find("Assignments on field");
    string Caption => PreviewCanvas.GetComponentInChildren<TMP_Text>(true).text;
    Transform[] Marks => Art.Cast<Transform>().Where(mark => mark.gameObject.activeInHierarchy).ToArray();
    PlayCallSelector Selector => play.GetComponent<PlayCallSelector>();

    IEnumerator SetFormation()
    {
        play.BreakHuddle();
        for (int frame = 0; frame < 60 && !play.IsSetAtLOS; frame++) yield return null;
        Assert.That(play.IsSetAtLOS, Is.True);
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator DefenseCyclesEachFormationSlotOnceSkipsInvalidAndKeepsLiveSelection()
    {
        Set(play, "userTeamId", 1);
        Keys(Key.Q, Key.V);
        yield return null;
        Assert.That(play.IsInHuddle, Is.True);
        Assert.That(PossessionController.ActivelyControlled, Is.Null);
        Assert.That(PreviewCanvas.gameObject.activeSelf, Is.False);
        Keys();
        yield return SetFormation();

        var slots = play.DefensePlayers.ToArray();
        Assert.That(slots.Length, Is.EqualTo(7));
        Assert.That(PossessionController.ActivelyControlled, Is.EqualTo(slots[0]));
        for (int i = 1; i <= slots.Length; i++)
        {
            Keys(Key.Q);
            yield return null;
            yield return null;
            Assert.That(PossessionController.ActivelyControlled, Is.EqualTo(slots[i % slots.Length]));
            Assert.That(slots.Count(slot => slot.GetComponent<DefenderAI>().IsUserControlled), Is.EqualTo(1));
            Keys();
            yield return null;
        }

        slots[1].gameObject.SetActive(false);
        Pad(cycle: true);
        yield return null;
        yield return null;
        Assert.That(PossessionController.ActivelyControlled, Is.EqualTo(slots[2]));
        slots[1].gameObject.SetActive(true);
        Pad();
        yield return null;
        play.ResetPlay();
        Assert.That(PossessionController.ActivelyControlled, Is.EqualTo(slots[2]));
        yield return null;

        // The next live press uses the existing nearest-other rule instead of slot order.
        Vector3 focus = play.Passer.position;
        foreach (var slot in slots) slot.position = focus + Vector3.right * 12f;
        slots[5].position = focus + Vector3.right * 2f;
        Pad(cycle: true);
        yield return null;
        Assert.That(PossessionController.ActivelyControlled, Is.EqualTo(slots[5]));
    }

    [UnityTest]
    public IEnumerator AdjustmentsStayThroughClockExpiryAndClearAtNextFormation()
    {
        Set(play, "userTeamId", 1);
        yield return SetFormation();
        Transform first = play.DefensePlayers[0];
        Transform second = play.DefensePlayers[1];
        Vector3 original = first.position;
        Vector3 originalSecond = second.position;
        Pad(Vector2.right);
        for (int i = 0; i < 12; i++) yield return null;
        Assert.That(Vector3.Distance(first.position, original), Is.GreaterThan(.2f));
        Vector3 firstAdjusted = first.position;

        Pad(Vector2.left, cycle: true);
        for (int i = 0; i < 12; i++) yield return null;
        Assert.That(PossessionController.ActivelyControlled, Is.EqualTo(second));
        Assert.That(Vector3.Distance(second.position, originalSecond), Is.GreaterThan(.2f));
        Pad();
        for (int i = 0; i < 16; i++) yield return null; // let the controlled player brake
        Vector3 secondAdjusted = second.position;
        Assert.That(first.position, Is.EqualTo(firstAdjusted));

        int starts = 0;
        play.OnPlayReset += () =>
        {
            if (++starts != 1) return;
            Assert.That(first.position, Is.EqualTo(firstAdjusted));
            Assert.That(second.position, Is.EqualTo(secondAdjusted));
            Assert.That(PossessionController.ActivelyControlled, Is.EqualTo(second));
        };
        Set(play, "playClockTimer", .005f);
        yield return null;
        Assert.That(starts, Is.EqualTo(1));
        Assert.That(play.IsLive, Is.True);
        play.EndPlay(PlayState.PlayEndReason.Incomplete);
        for (int i = 0; i < 60 && !play.IsInHuddle; i++) yield return null;
        Assert.That(play.IsInHuddle, Is.True);
        Assert.That(PossessionController.ActivelyControlled, Is.Null);
        yield return SetFormation();
        Assert.That(Vector3.Distance(first.position, original), Is.LessThan(.3f));
        Assert.That(Vector3.Distance(second.position, originalSecond), Is.LessThan(.3f));
    }

    [UnityTest]
    public IEnumerator SustainedDiagonalMovementSlidesAtLOSAndClampsAtFieldEdgesInBothDirections()
    {
        foreach (int offense in new[] { 0, 1 })
        {
            if (offense == 1)
            {
                // Change the driving team through the same turnover path used in a match.
                play.ResetPlay();
                BallController.Instance.AttachTo(play.DefensePlayers[2]);
                play.EndPlay(PlayState.PlayEndReason.Interception);
                for (int i = 0; i < 60 && !play.IsInHuddle; i++) yield return null;
                Assert.That(play.PossessionTeamId, Is.EqualTo(offense));
            }
            Set(play, "userTeamId", 1 - offense);
            yield return SetFormation();
            for (int i = 0; i < 30; i++) yield return null; // let the possession-relative camera swing settle
            var defender = PossessionController.ActivelyControlled;
            var bounds = Object.FindAnyObjectByType<FieldBounds>();
            var direction = play.AttackDirection;
            float line = play.CurrentLineOfScrimmageZ;

            defender.position = new Vector3(0f, 1f, direction.Advance(line, .25f));
            Pad(ScreenStick(Vector3.right - direction.Forward));
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                Assert.That(play.YardsPastLineOfScrimmage(defender.position.z), Is.GreaterThanOrEqualTo(0f));
            }
            Assert.That(defender.position.z, Is.EqualTo(line));
            float lateral = defender.position.x;
            for (int i = 0; i < 8; i++) yield return null;
            Assert.That(defender.position.x, Is.GreaterThan(lateral + .2f));
            Assert.That(defender.position.z, Is.EqualTo(line));

            float nearDefendingEnd = direction.FromAxis(bounds.HalfPlayableLength - .1f);
            defender.position = new Vector3(bounds.HalfPlayableWidth - .1f, 1f, nearDefendingEnd);
            Pad(ScreenStick(Vector3.right + direction.Forward));
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                Assert.That(bounds.IsInsidePlayableBounds(defender.position), Is.True);
            }
            Assert.That(defender.position.x, Is.EqualTo(bounds.HalfPlayableWidth));
            Assert.That(defender.position.z, Is.EqualTo(direction.FromAxis(bounds.HalfPlayableLength)));
            Pad();
            yield return null;
        }
    }

    static Vector2 ScreenStick(Vector3 world)
    {
        Vector3 stick = Quaternion.Euler(0f, -CameraFollow.ControlYaw, 0f) * world.normalized;
        return new Vector2(stick.x, stick.z);
    }

    [UnityTest]
    public IEnumerator OffenseKeepsItsPasserStationaryAndShowsOnlyItsAuthoredRouteAndBlockArt()
    {
        var slants = Selector.OffensivePlays.Single(call => call.DisplayName.Contains("Slant"));
        play.SetPlayCall(slants);
        yield return SetFormation();
        for (int i = 0; i < 50; i++) yield return null;
        Transform passer = play.Passer;
        var players = play.OffensivePlayers.Append(passer).ToArray();
        var positions = players.Select(actor => actor.position).ToArray();

        Keys(Key.V);
        yield return null;
        yield return null;
        Assert.That(Caption, Does.Contain(slants.DisplayName));
        Keys(Key.W, Key.Q, Key.V);
        for (int i = 0; i < 15; i++) yield return null;
        yield return null;
        Assert.That(PossessionController.ActivelyControlled, Is.EqualTo(passer));
        Assert.That(BallController.Instance.Carrier, Is.EqualTo(passer));
        Assert.That(players.Select(actor => actor.position), Is.EqualTo(positions));
        Assert.That(play.IsLive, Is.False);
        Assert.That(play.CurrentPlayCall, Is.SameAs(slants));
        Assert.That(PlayPicker.blocksRaycasts, Is.False);
        Assert.That(Caption, Does.Contain(slants.DisplayName));
        Assert.That(Marks.Any(mark => mark.name == "Route"), Is.True);
        Assert.That(Marks.Any(mark => mark.name == "Block"), Is.True);
        Assert.That(Marks.Any(mark => mark.name is "Man" or "Zone" or "Blitz"), Is.False);

        Keys();
        yield return null;
        yield return null;
        Assert.That(Marks, Is.Empty);
        Assert.That(Caption, Does.Contain("HOLD V / LT / L2"));
    }

    [UnityTest]
    public IEnumerator DefenseArtMovesWithItsActorsAndClearsOnReleaseAndStart()
    {
        Set(play, "userTeamId", 1);
        var zone = Selector.DefensivePlays.Single(call => call.DisplayName == "Zone");
        Assert.That(zone.ZoneCount, Is.EqualTo(6));
        play.SetDefensivePlay(zone);
        yield return SetFormation();
        for (int i = 0; i < 50; i++) yield return null;
        Pad(hold: true);
        yield return null;
        yield return null;
        Assert.That(Caption, Does.Contain(zone.DisplayName));
        Assert.That(Marks.Any(mark => mark.name == "Zone"), Is.True, "The shipped Zone play has authored zone jobs");
        Assert.That(Marks.Any(mark => mark.name is "Route" or "Block" or "Exchange"), Is.False);
        var drop = Marks.First(mark => mark.name == "Zone drop");
        Vector2 original = ((RectTransform)drop).anchoredPosition;
        Pad(Vector2.right, hold: true);
        for (int i = 0; i < 15; i++) yield return null;
        yield return null;
        Assert.That(play.CurrentDefensivePlay, Is.SameAs(zone), "Moving the defender must not change the locked call");
        Assert.That(Vector2.Distance(((RectTransform)Marks.First(mark => mark.name == "Zone drop")).anchoredPosition, original),
            Is.GreaterThan(1f));
        Pad(hold: true, cycle: true);
        yield return null;
        yield return null;
        Assert.That(PossessionController.ActivelyControlled, Is.SameAs(play.DefensePlayers[1]));
        Assert.That(Marks.Any(mark => mark.name == "Zone"), Is.True);

        Pad();
        yield return null;
        yield return null;
        Assert.That(Marks, Is.Empty);
        Pad(hold: true);
        yield return null;
        yield return null;
        Assert.That(Marks, Is.Not.Empty);
        play.ResetPlay();
        Assert.That(PreviewCanvas.gameObject.activeSelf, Is.False);
        Assert.That(Marks, Is.Empty);
    }

    [UnityTest]
    public IEnumerator CpuStartsAutomaticallyWithinTheConfiguredWindow()
    {
        Set(play, "userTeamId", 1);
        Set(cpu, "snapDelayAfterSetRange", new Vector2(2f, 8f));
        float setTime = -1f;
        play.OnSetAtLOS += () => setTime = Time.time;
        yield return SetFormation();
        for (int i = 0; i < 510 && !play.IsLive; i++) yield return null;
        Assert.That(play.IsLive, Is.True);
        Assert.That(Time.time - setTime, Is.InRange(1.99f, 8.1f));
    }

    [UnityTest]
    public IEnumerator ClockExpiryInHuddleSetsFormationBeforeStartingPlay()
    {
        Set(play, "userTeamId", 1);
        Assert.That(play.IsInHuddle, Is.True);
        bool formationReady = false;
        int starts = 0;
        play.OnSetAtLOS += () => formationReady = play.IsSetAtLOS && !play.IsLive;
        play.OnPlayReset += () =>
        {
            Assert.That(formationReady, Is.True);
            Assert.That(PossessionController.ActivelyControlled, Is.SameAs(play.DefensePlayers[0]));
            starts++;
        };
        Set(play, "playClockTimer", .005f);
        yield return null;
        yield return null;
        Assert.That(play.IsLive, Is.True);
        Assert.That(starts, Is.EqualTo(1));
    }
}
