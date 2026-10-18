using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
}
