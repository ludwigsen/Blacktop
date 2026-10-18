using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Exercise the actual match scene and prefab stacks, including subscription order.
// Reflection only sets up rare gameplay situations; assertions use public outcomes.
public class PlayResolutionTests
{
    PlayState play;
    BallController ball;
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnitySetUp]
    public IEnumerator LoadMatch()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null; // let every Start subscribe before the first whistle
        play = PlayState.Instance;
        ball = BallController.Instance;
    }

    [UnityTearDown]
    public IEnumerator LeaveMatch()
    {
        // Isolate scores, singleton references, and coroutines between cases.
        Scene match = SceneManager.GetActiveScene();
        Scene empty = SceneManager.CreateScene("Test cleanup");
        SceneManager.SetActiveScene(empty);
        yield return SceneManager.UnloadSceneAsync(match);
    }

    static void Set(object owner, string name, object value) =>
        owner.GetType().GetField(name, PrivateInstance).SetValue(owner, value);

    static object Call(object owner, string name, params object[] args) =>
        owner.GetType().GetMethod(name, PrivateInstance).Invoke(owner, args);

    void GoLive() => Set(play, "<IsLive>k__BackingField", true);

    TeamMember Player(int team, TeamMember.RosterSlot slot)
    {
        foreach (var member in Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
            if (member.teamId == team && member.slot == slot) return member;
        Assert.Fail($"Missing team {team}, slot {slot}");
        return null;
    }

    void HoldAt(TeamMember holder, float x, float z)
    {
        holder.transform.SetPositionAndRotation(new Vector3(x, 1f, z), Quaternion.identity);
        Set(ball, "carryOffset", Vector3.zero); // make test spots exact; no tuning changes
        ball.AttachTo(holder.transform);
    }

    [TestCase(BallController.BallState.InFlight)]
    [TestCase(BallController.BallState.Loose)]
    public void UnheldBallCannotScore(BallController.BallState state)
    {
        GoLive();
        ball.Drop();
        Set(ball, "<State>k__BackingField", state);
        ball.transform.position = new Vector3(0f, 1f, FieldConstants.FarGoalLineZ + 1f);
        play.EndPlay(PlayState.PlayEndReason.Touchdown);
        Assert.That(play.IsLive, Is.True);
        Assert.That(ScoreBoard.Instance.Team1Score, Is.Zero);
        Assert.That(ScoreBoard.Instance.Team2Score, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GoalLineOutsideSidelineIsOutOfBounds(bool useFieldBounds)
    {
        if (useFieldBounds) new GameObject("Test field").AddComponent<FieldBounds>();
        GoLive();
        HoldAt(Player(0, TeamMember.RosterSlot.QB), FieldConstants.HalfWidth + 1f,
            FieldConstants.FarGoalLineZ + 1f);
        Assert.That(play.TryGetTouchdownTeam(out _), Is.False);
        play.EndPlay(PlayState.PlayEndReason.OutOfBounds);
        Assert.That(ScoreBoard.Instance.Team1Score, Is.Zero);
        Assert.That(play.LastScoringTeamId, Is.Null);
    }

    [TestCase(0, PlayState.PlayEndReason.Touchdown)]
    [TestCase(0, PlayState.PlayEndReason.Tackled)]
    [TestCase(1, PlayState.PlayEndReason.Touchdown)]
    [TestCase(1, PlayState.PlayEndReason.Tackled)]
    public void ScoreAndReceivingDriveFollowActualCarrier(int scoringTeam, PlayState.PlayEndReason source)
    {
        GoLive();
        FieldDirection direction = play.DirectionFor(scoringTeam);
        HoldAt(Player(scoringTeam, TeamMember.RosterSlot.RB), 0f,
            direction.Advance(direction.TargetGoalLineZ, 1f));
        // Leave the rendered ball behind: Update-time rulings must use the live held spot.
        ball.transform.position = Vector3.zero;
        int whistles = 0;
        play.OnPlayEnded += _ => whistles++;
        play.EndPlay(source);
        play.EndPlay(source);
        Assert.That(whistles, Is.EqualTo(1));
        Assert.That(play.LastScoringTeamId, Is.EqualTo(scoringTeam));
        Assert.That(ScoreBoard.Instance.ScoreFor(scoringTeam), Is.EqualTo(6));
        Assert.That(ScoreBoard.Instance.ScoreFor(1 - scoringTeam), Is.Zero);
        Assert.That(play.PossessionTeamId, Is.EqualTo(1 - scoringTeam));
        Assert.That(play.CurrentLineOfScrimmageZ,
            Is.EqualTo(play.AttackDirection.FromAxis(-10f)));
        Assert.That(DownsTracker.Instance.CurrentDown, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DefensiveRecoveryHandsSelectedDefenderToCarrierInEitherUpdateOrder(bool managerFirst)
    {
        Assert.That(play.TrySetPossession(1), Is.True);
        GoLive();
        HoldAt(Player(1, TeamMember.RosterSlot.QB), 0f, 0f);
        var manager = DefenderControlManager.Instance;
        Call(manager, "Update");
        Transform selected = PossessionController.ActivelyControlled;
        Assert.That(selected, Is.Not.Null);
        var possession = selected.GetComponent<PossessionController>();
        Call(possession, "Update"); // settle the original defender mode
        ball.AttachTo(selected);
        if (managerFirst) Call(manager, "Update");
        Call(possession, "Update");
        if (!managerFirst) Call(manager, "Update");
        Assert.That(play.IsUserDefending, Is.False);
        Assert.That(PossessionController.ActivelyControlled, Is.EqualTo(selected));
        Assert.That(selected.GetComponent<DefenderControl>().enabled, Is.False);
        Assert.That(selected.GetComponent<PlayerMovement>().enabled, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CpuRecoveryPreservesNewDefenderClaimInEitherUpdateOrder(bool managerFirst)
    {
        GoLive();
        var human = Player(0, TeamMember.RosterSlot.QB);
        HoldAt(human, 0f, 0f);
        var possession = human.GetComponent<PossessionController>();
        Call(possession, "Update");
        // Make the former carrier the obvious nearest defender after CPU recovery.
        foreach (var member in Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
            if (member.teamId == 0 && member != human) member.transform.position = Vector3.one * 100f;
        HoldAt(Player(1, TeamMember.RosterSlot.RB), 0f, 1f);
        var manager = DefenderControlManager.Instance;
        if (managerFirst) Call(manager, "Update");
        Call(possession, "Update");
        if (!managerFirst) Call(manager, "Update");
        Assert.That(play.IsUserDefending, Is.True);
        Assert.That(PossessionController.ActivelyControlled, Is.EqualTo(human.transform));
        Assert.That(human.GetComponent<DefenderControl>().enabled, Is.True);
        Assert.That(human.GetComponent<PlayerMovement>().enabled, Is.False);
    }

    [Test]
    public void RecoveringCpuQuarterbackRunsInHisOwnDirectionInsteadOfOldPlayCall()
    {
        GoLive();
        var runner = Player(1, TeamMember.RosterSlot.QB);
        HoldAt(runner, 0f, 0f);
        var ai = runner.GetComponent<CPUCarrierAI>();
        Set(ai, "avoidanceWeight", 0f); // isolate attack direction from steering
        Call(ai, "Update");
        Assert.That(runner.transform.position.z, Is.LessThan(0f));
        Assert.That(ball.IsHeld, Is.True); // QB did not throw to the old offense
    }

    [Test]
    public void WhistleCancelsHurdleHeightAndImmunity()
    {
        GoLive();
        var holder = Player(0, TeamMember.RosterSlot.QB);
        HoldAt(holder, 0f, 0f);
        var machine = holder.GetComponent<PlayerStateMachine>();
        machine.enabled = true;
        var context = (PlayerContext)machine.GetType().GetField("ctx", PrivateInstance).GetValue(machine);
        var hurdle = new HurdleMove();
        Set(hurdle, "heightCurve", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        hurdle.Enter(context);
        hurdle.Tick(context, 0.1f);
        Set(machine, "activeMove", hurdle);
        context.setTackleImmune(true); // deterministic successful negation
        Assert.That(holder.transform.position.y, Is.GreaterThan(1f));
        play.EndPlay(PlayState.PlayEndReason.Incomplete);
        Assert.That(machine.IsTackleImmune, Is.False);
        Assert.That(machine.CurrentState, Is.EqualTo(PlayerState.Idle));
        Assert.That(holder.transform.position.y, Is.EqualTo(1f).Within(0.001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InterruptedThrowDoesNotReleaseBall(bool pitch)
    {
        GoLive();
        var holder = Player(0, TeamMember.RosterSlot.QB);
        HoldAt(holder, 0f, 0f);
        var machine = holder.GetComponent<PlayerStateMachine>();
        machine.enabled = true;
        IPlayerMove move = pitch ? (IPlayerMove)new PitchMove() : new PassMove();
        Set(move, "target", Player(0, TeamMember.RosterSlot.WR1).transform);
        Set(machine, "activeMove", move);
        play.EndPlay(PlayState.PlayEndReason.Incomplete);
        Assert.That(ball.IsHeld, Is.True);
        Assert.That(ball.Carrier, Is.EqualTo(holder.transform));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ContactCannotFumbleAfterGoalLineOrSidelineRuling(bool outsideSideline)
    {
        GoLive();
        Set(play, "snapTimestamp", Time.time - 1f); // outside the normal tackle grace
        var holder = Player(0, TeamMember.RosterSlot.QB);
        HoldAt(holder, outsideSideline ? FieldConstants.HalfWidth + 1f : 0f,
            FieldConstants.FarGoalLineZ + 1f);
        Player(1, TeamMember.RosterSlot.RB).transform.position = holder.transform.position;
        var contact = holder.GetComponent<TackleContact>();
        Set(contact, "baseFumbleChance", 1f);
        Set(contact, "minFumbleChance", 1f);
        Set(contact, "maxFumbleChance", 1f); // would fumble if contact ran before the ruling
        Physics.SyncTransforms();
        Call(contact, "Update");
        Assert.That(play.IsLive, Is.False);
        Assert.That(ball.IsHeld, Is.True);
        Assert.That(ScoreBoard.Instance.Team1Score, Is.EqualTo(outsideSideline ? 0 : 6));
    }

    [Test]
    public void WinningDefensiveReturnStopsAtCorrectWinner()
    {
        GoLive();
        // Runtime-only rule change makes a single defensive TD the winning play.
        var rules = (GameRules)typeof(ScoreBoard).GetField("rules", PrivateInstance)
            .GetValue(ScoreBoard.Instance);
        rules.scoreToWin = 6;
        HoldAt(Player(1, TeamMember.RosterSlot.RB), 0f, FieldConstants.NearGoalLineZ - 1f);
        play.EndPlay(PlayState.PlayEndReason.Touchdown);
        Assert.That(play.IsGameOver, Is.True);
        Assert.That(play.WinnerTeamId, Is.EqualTo(1));
        Assert.That(ScoreBoard.Instance.Team2Score, Is.EqualTo(6));
        Assert.That(play.IsLive, Is.False);
    }

    [Test]
    public void LosingCarrierControlCancelsHurdleBeforeAnotherTick()
    {
        GoLive();
        var holder = Player(0, TeamMember.RosterSlot.QB);
        HoldAt(holder, 0f, 0f);
        var possession = holder.GetComponent<PossessionController>();
        Call(possession, "Update");
        var machine = holder.GetComponent<PlayerStateMachine>();
        var context = (PlayerContext)typeof(PlayerStateMachine).GetField("ctx", PrivateInstance)
            .GetValue(machine);
        var hurdle = new HurdleMove();
        Set(hurdle, "heightCurve", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        hurdle.Enter(context);
        hurdle.Tick(context, 0.1f);
        Set(machine, "activeMove", hurdle);
        context.setTackleImmune(true);
        ball.AttachTo(Player(1, TeamMember.RosterSlot.RB).transform);
        // Exercise stale-component Update first, then the normal enable/disable path.
        Call(machine, "Update");
        Call(possession, "Update");
        Assert.That(machine.IsTackleImmune, Is.False);
        Assert.That(machine.enabled, Is.False);
        Assert.That(holder.transform.position.y, Is.EqualTo(1f).Within(0.001f));
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    public void LooseBallHasOneAiMovementOwnerPerPlayer(int driveTeam, bool pushed)
    {
        Assert.That(play.TrySetPossession(driveTeam), Is.True);
        GoLive();
        ball.Drop();
        ball.transform.position = new Vector3(0f, 1f, 0f);

        foreach (var member in Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            var blocker = member.GetComponent<AllyBlocker>();
            var defender = member.GetComponent<DefenderAI>();
            defender.SetUserControlled(false);
            PossessionController.ReleaseControl(member.transform);
            member.transform.position = new Vector3(10f, 1f, 0f);
            // Force a stale shove on BOTH stacks; only the owning stack may advance it.
            Set(blocker, "pushBackTimer", pushed ? 1f : 0f);
            Set(defender, "pushBackTimer", pushed ? 1f : 0f);
            Set(blocker, "pushBackTarget", new Vector3(12f, 1f, 0f));
            Set(defender, "pushBackTarget", new Vector3(12f, 1f, 0f));
            Set(defender, "shedTimer", 0f);
            member.GetComponent<ReceiverAI>()?.AbortRoute();
            Vector3 before = member.transform.position;
            Call(blocker, "Update");
            Vector3 afterBlocker = member.transform.position;
            Call(defender, "Update");
            Vector3 afterDefender = member.transform.position;
            if (member.teamId == driveTeam)
            {
                Assert.That(afterBlocker, Is.Not.EqualTo(before), member.name + " offense must move");
                Assert.That(afterDefender, Is.EqualTo(afterBlocker), member.name + " defense must defer");
            }
            else
            {
                Assert.That(afterBlocker, Is.EqualTo(before), member.name + " blocker must defer");
                Assert.That(afterDefender, Is.Not.EqualTo(before), member.name + " defense must move");
            }
        }
    }

    [Test]
    public void SelectedDefenderOwnsLooseBallMovement()
    {
        Assert.That(play.TrySetPossession(1), Is.True);
        GoLive();
        HoldAt(Player(1, TeamMember.RosterSlot.QB), 0f, 0f);
        var manager = DefenderControlManager.Instance;
        Call(manager, "Update");
        Transform selected = PossessionController.ActivelyControlled;
        var defender = selected.GetComponent<DefenderAI>();
        var control = selected.GetComponent<DefenderControl>();
        ball.Drop();
        ball.transform.position = Vector3.zero;
        selected.position = new Vector3(10f, 1f, 0f);
        Vector3 before = selected.position;
        Call(selected.GetComponent<AllyBlocker>(), "Update");
        Call(defender, "Update");
        Assert.That(selected.position, Is.EqualTo(before), "AI must defer to selected defender");
        Set(control, "moveInput", Vector2.up);
        Call(control, "Update");
        Assert.That(selected.position, Is.Not.EqualTo(before), "Human movement remains available");
    }

    [UnityTest]
    public IEnumerator CompleteMatchAndRematchWithTeamZeroWinner() => CompleteMatchAndRematch(0);

    [UnityTest]
    public IEnumerator CompleteMatchAndRematchWithTeamOneWinner() => CompleteMatchAndRematch(1);

    IEnumerator ReadyAndSnap()
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!play.IsInHuddle && !play.IsGameOver && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(play.IsInHuddle, Is.True, "Post-play must reach the next huddle");
        play.BreakHuddle();
        while (!play.IsSetAtLOS && !play.IsLive && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (!play.IsLive) play.ResetPlay();
        Assert.That(play.IsLive, Is.True, "Formation must lead to a live play");
        Assert.That(ball.Carrier, Is.EqualTo(play.Passer), "QB starts holding the ball");
    }

    IEnumerator CompleteMatchAndRematch(int winner)
    {
        // Accelerate presentation only in this test instance. Rules, possession,
        // downs events and the actual formation/huddle coroutines still run normally.
        Set(play, "playEndPresentationDelay", 0.01f);
        Set(play, "formationMoveSpeed", 1000f);
        Set(ball, "carryOffset", Vector3.zero);
        var downs = DownsTracker.Instance;
        yield return ReadyAndSnap();
        float start = play.CurrentLineOfScrimmageZ;
        HoldAt(Player(0, TeamMember.RosterSlot.QB), FieldConstants.HalfWidth + 1f,
            play.AttackDirection.Advance(start, 4f));
        play.EndPlay(PlayState.PlayEndReason.OutOfBounds);
        Assert.That(downs.CurrentDown, Is.EqualTo(2));
        Assert.That(downs.YardsToGo, Is.EqualTo(6f).Within(0.001f));

        yield return ReadyAndSnap();
        float incompleteSpot = play.CurrentLineOfScrimmageZ;
        ball.Drop();
        play.EndPlay(PlayState.PlayEndReason.Incomplete);
        Assert.That(play.CurrentLineOfScrimmageZ, Is.EqualTo(incompleteSpot));
        Assert.That(downs.CurrentDown, Is.EqualTo(3));

        yield return ReadyAndSnap();
        HoldAt(Player(0, TeamMember.RosterSlot.QB), 0f,
            play.AttackDirection.Advance(play.CurrentLineOfScrimmageZ, 6f));
        play.EndPlay(PlayState.PlayEndReason.Tackled);
        Assert.That(downs.CurrentDown, Is.EqualTo(1));
        Assert.That(downs.YardsToGo, Is.EqualTo(10f).Within(0.001f));

        for (int down = 1; down <= DownsTracker.MaxDowns; down++)
        {
            yield return ReadyAndSnap();
            Assert.That(downs.CurrentDown, Is.EqualTo(down));
            ball.Drop();
            play.EndPlay(PlayState.PlayEndReason.Incomplete);
        }
        Assert.That(play.PossessionTeamId, Is.EqualTo(1), "Fourth-down failure must turn over");
        Assert.That(downs.DriveTeamId, Is.EqualTo(1));
        Assert.That(downs.CurrentDown, Is.EqualTo(1));

        yield return ReadyAndSnap();
        FieldDirection direction = play.AttackDirection;
        HoldAt(Player(1, TeamMember.RosterSlot.QB), 0f,
            direction.Advance(direction.OwnGoalLineZ, -1f));
        play.EndPlay(PlayState.PlayEndReason.Tackled);
        Assert.That(ScoreBoard.Instance.Team1Score, Is.EqualTo(2));
        Assert.That(play.PossessionTeamId, Is.EqualTo(0));

        // Alternate legal touchdowns up to the real default target (21). Team 0 has
        // the safety's two points; a defensive return lets either team win the match.
        for (int score = 0; score < 10 && !play.IsGameOver; score++)
        {
            yield return ReadyAndSnap();
            int team = score == 6 ? winner : play.PossessionTeamId;
            direction = play.DirectionFor(team);
            HoldAt(Player(team, TeamMember.RosterSlot.RB), 0f,
                direction.Advance(direction.TargetGoalLineZ, 1f));
            play.EndPlay(PlayState.PlayEndReason.Touchdown);
            Assert.That(play.LastScoringTeamId, Is.EqualTo(team));
            Assert.That(play.PossessionTeamId, Is.EqualTo(1 - team));
            if (!play.IsGameOver) Assert.That(downs.CurrentDown, Is.EqualTo(1));
        }
        Assert.That(play.IsGameOver, Is.True);
        Assert.That(play.WinnerTeamId, Is.EqualTo(winner));
        Assert.That(ScoreBoard.Instance.ScoreFor(winner), Is.GreaterThanOrEqualTo(21));
        int huddlesAfterWin = 0;
        play.OnHuddleStarted += () => huddlesAfterWin++;
        yield return new WaitForSeconds(0.05f);
        Assert.That(huddlesAfterWin, Is.Zero, "Winner must stop match progression");
        var hud = Object.FindAnyObjectByType<GameHUD>();
        var overlay = (GameObject)typeof(GameHUD).GetField("gameOverRoot", PrivateInstance).GetValue(hud);
        Assert.That(overlay.activeSelf, Is.True, "Winner UI must appear");

        // Invoke the real Confirm handler after its input delay; this is the shipped
        // scene-reload rematch path, not a bespoke test reset of managers/scores.
        Set(hud, "restartInputDelay", 0f);
        var confirm = typeof(GameHUD).GetMethod("HandleConfirm", PrivateInstance);
        confirm.Invoke(hud, new[] { System.Activator.CreateInstance(confirm.GetParameters()[0].ParameterType) });
        yield return null;
        yield return null;
        play = PlayState.Instance;
        ball = BallController.Instance;
        Assert.That(play.IsGameOver, Is.False);
        Assert.That(ScoreBoard.Instance.Team1Score, Is.Zero);
        Assert.That(ScoreBoard.Instance.Team2Score, Is.Zero);
        Assert.That(DownsTracker.Instance.CurrentDown, Is.EqualTo(1));
        Set(play, "formationMoveSpeed", 1000f);
        yield return ReadyAndSnap();
        ball.Drop();
        play.EndPlay(PlayState.PlayEndReason.Incomplete);
        Assert.That(DownsTracker.Instance.CurrentDown, Is.EqualTo(2), "Rematch must advance normally");
    }

    [Test]
    public void MatchHasSevenUniqueRosterSlotsOnEachTeam()
    {
        // Catch stale prefab overrides that silently leave a formation slot empty.
        var counts = new int[2, 7];
        foreach (var member in Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            Assert.That(member.teamId, Is.InRange(0, 1), member.name);
            counts[member.teamId, (int)member.slot]++;
        }
        for (int team = 0; team < 2; team++)
            for (int slot = 0; slot < 7; slot++)
                Assert.That(counts[team, slot], Is.EqualTo(1), $"Team {team}, slot {(TeamMember.RosterSlot)slot}");
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    public void GamebreakerRealInputActivatesOnlyControlledTeam(int possession, bool gamepad)
    {
        Assert.That(play.TrySetPossession(possession), Is.True);
        GoLive();
        var controlled = Player(0, TeamMember.RosterSlot.QB);
        PossessionController.ClaimControl(controlled.transform, 0);
        play.AddTeamPoints(0, 100f);
        play.AddTeamPoints(1, 100f);
        // Batch Editor has no focused Game View. Route synthetic devices to gameplay, then restore settings.
        var previousBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        InputDevice device = gamepad ? (InputDevice)InputSystem.AddDevice<Gamepad>() : InputSystem.AddDevice<Keyboard>();
        try
        {
            // Exercise the actual binding and fourteen prefab listeners, not just the public activation API.
            InputSystem.Update();
            if (gamepad) InputSystem.QueueStateEvent((Gamepad)device, new GamepadState().WithButton(GamepadButton.LeftShoulder));
            else InputSystem.QueueStateEvent((Keyboard)device, new KeyboardState(Key.G));
            InputSystem.Update();
            Assert.That(controlled.GetComponent<GamebreakerController>().enabled, Is.True);
            var input = (InputSystem_Actions)controlled.GetComponent<GamebreakerController>().GetType().GetField("controls", PrivateInstance).GetValue(controlled.GetComponent<GamebreakerController>());
            Assert.That(input.Player.Gamebreaker.enabled, Is.True, "Activation action enabled");
            Assert.That(input.Player.Gamebreaker.IsPressed(), Is.True, "Queued button reaches activation binding");
            Assert.That(PossessionController.ActivelyControlled, Is.EqualTo(controlled.transform));
            Assert.That(play.IsGamebreakerActiveFor(0), Is.True);
            Assert.That(play.MeterFor(0), Is.Zero);
            Assert.That(play.IsGamebreakerActiveFor(1), Is.False);
            Assert.That(play.MeterFor(1), Is.EqualTo(100f));
        }
        finally
        {
            InputSystem.RemoveDevice(device);
            InputSystem.settings.backgroundBehavior = previousBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
#endif
        }
    }

    [Test]
    public void GamebreakerCannotActivateDeadUnderfilledOrTwice()
    {
        play.AddTeamPoints(0, 99f);
        Assert.That(play.TryActivateGamebreaker(0), Is.False);
        GoLive();
        Assert.That(play.TryActivateGamebreaker(0), Is.False);
        play.AddTeamPoints(0, 1f);
        Assert.That(play.TryActivateGamebreaker(0), Is.True);
        play.AddTeamPoints(0, 100f);
        Assert.That(play.MeterFor(0), Is.Zero, "Active teams cannot refill during the boost");
        Assert.That(play.TryActivateGamebreaker(0), Is.False);
        play.EndGame(0);
        Assert.That(play.IsGamebreakerActiveFor(0), Is.False);
        Assert.That(play.TryActivateGamebreaker(0), Is.False);
    }

    [TestCase(0)]
    [TestCase(1)]
    public void GamebreakerBoostsAllAttributesOfOnlyItsTeam(int team)
    {
        GoLive();
        play.AddTeamPoints(team, 100f);
        Assert.That(play.TryActivateGamebreaker(team), Is.True);
        foreach (var member in Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            var attr = member.GetComponent<PlayerStateMachine>().Attributes;
            Assert.That(attr.ownerTeamId, Is.EqualTo(member.teamId));
            foreach (AttributeStat stat in System.Enum.GetValues(typeof(AttributeStat)))
            {
                float curve = attr.curves != null ? attr.curves.Evaluate(stat, 10) : 1f;
                var cap = attr.curves != null ? attr.curves.HardCap(stat) : (0.5f, 2f);
                float expected = Mathf.Clamp(curve * (member.teamId == team ? 1.2f : 1f), cap.Item1, cap.Item2);
                Assert.That(attr.Effective(stat, 10), Is.EqualTo(expected).Within(0.0001f), $"{member.name}: {stat}");
            }
        }
    }

    [Test]
    public void DefensiveGamebreakerImprovesCoverageAndFumbleOddsWithoutGuarantee()
    {
        GoLive();
        var runner = Player(0, TeamMember.RosterSlot.QB);
        var defender = Player(1, TeamMember.RosterSlot.QB);
        var contact = runner.GetComponent<TackleContact>();
        var defense = defender.GetComponent<DefenderAI>();
        float coverage = defense.CoverageMult;
        float chance = (float)Call(contact, "CalculateFumbleChance", defender.transform);
        play.AddTeamPoints(1, 100f);
        Assert.That(play.TryActivateGamebreaker(1), Is.True);
        Assert.That(defense.CoverageMult, Is.GreaterThan(coverage));
        float boosted = (float)Call(contact, "CalculateFumbleChance", defender.transform);
        Assert.That(boosted, Is.GreaterThan(chance));
        Assert.That(boosted, Is.LessThan(1f));
    }

    [UnityTest]
    public IEnumerator BothGamebreakersPersistAcrossDownsAndExpireOnFourthDownTurnover()
    {
        Set(play, "playEndPresentationDelay", 0.01f);
        Set(play, "formationMoveSpeed", 1000f);
        yield return ReadyAndSnap();
        for (int team = 0; team < 2; team++)
        {
            play.AddTeamPoints(team, 100f);
            Assert.That(play.TryActivateGamebreaker(team), Is.True);
        }
        ball.Fumble(ball.Carrier.position, ball.Carrier.position + Vector3.right);
        Assert.That(play.IsGamebreakerActiveFor(0), Is.True, "A fumble can be recovered by the same offense");
        for (int down = 1; down <= DownsTracker.MaxDowns; down++)
        {
            ball.Drop();
            play.EndPlay(PlayState.PlayEndReason.Incomplete);
            for (int team = 0; team < 2; team++)
                Assert.That(play.IsGamebreakerActiveFor(team), Is.EqualTo(down < DownsTracker.MaxDowns));
            if (down < DownsTracker.MaxDowns) yield return ReadyAndSnap();
        }
        Assert.That(play.PossessionTeamId, Is.EqualTo(1));
    }

    [Test]
    public void CpuSpendsItsOwnFullMeterOnBothSides()
    {
        var cpu = Object.FindFirstObjectByType<CPUPlayCaller>();
        for (int possession = 0; possession < 2; possession++)
        {
            Set(play, "<IsLive>k__BackingField", false);
            Assert.That(play.TrySetPossession(possession), Is.True);
            GoLive();
            play.AddTeamPoints(1, 100f);
            Call(cpu, "Update");
            Assert.That(play.IsGamebreakerActiveFor(1), Is.True);
            Assert.That(play.IsGamebreakerActiveFor(0), Is.False);
        }
    }

    [Test]
    public void EveryPlayerInheritsCorrectedBodyAndActivationComponent()
    {
        foreach (var member in Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            Assert.That(member.GetComponent<GamebreakerController>(), Is.Not.Null);
            Transform body = member.transform.Find("PlayerVisuals/PlayerModel/PlayerBody");
            Assert.That(body, Is.Not.Null, member.name);
            // Issue #2: rotate the previous backwards visual by 180 degrees, without rotating the gameplay root.
            Assert.That(Quaternion.Angle(body.localRotation, Quaternion.identity), Is.LessThan(0.01f));
            Assert.That(body.GetComponent<Animator>().applyRootMotion, Is.False);
            Assert.That(member.GetComponent<MeshRenderer>().enabled, Is.False);
        }
    }
}
