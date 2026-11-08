using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Use the shipped player capsule, move stack, venue prefabs and ball rulings.
public class VenueHazardTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    FieldLoader loader;
    PlayState play;
    BallController ball;
    float captureDelta;

    [UnitySetUp]
    public IEnumerator LoadMatch()
    {
        captureDelta = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f / 60f;
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        loader = Object.FindAnyObjectByType<FieldLoader>();
        play = PlayState.Instance;
        ball = BallController.Instance;
        play.StopAllCoroutines();
        // Tests drive the real movement methods without unrelated actors, AI
        // decisions, or phase coroutines moving the fixture between assertions.
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>())
                if (behaviour is not (FieldLoader or FieldBounds or FieldBoundaryWall or TeamMember or PlayerStateMachine))
                    behaviour.enabled = false;
        foreach (var member in Object.FindObjectsByType<TeamMember>())
            member.transform.position = new Vector3(0f, 1f, 0f);
        ball.Drop();
        Set(ball, "carryOffset", Vector3.zero);
    }

    [UnityTearDown]
    public IEnumerator LeaveMatch()
    {
        Time.captureDeltaTime = captureDelta;
        Scene match = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Venue hazard cleanup"));
        yield return SceneManager.UnloadSceneAsync(match);
    }

    [TestCase(0, 1, 0f, 30)]
    [TestCase(0, -1, 2f, 120)]
    [TestCase(1, -1, 1f, 30)]
    [TestCase(1, -1, 2f, 120)]
    public void FrontalAndGlancingSweepsRecoilAtBothStepRates(int venue, int side, float drift, int fps)
    {
        Assert.That(loader.SelectField(loader.AvailableFields[venue]), Is.True);
        GoLive();
        var player = Actor(0, TeamMember.RosterSlot.RB);
        player.transform.position = new Vector3(side * (FieldConstants.HalfWidth - 1f), 1f, 8f);
        Assert.That(Move(player, player.transform.position + new Vector3(8f * side, 0f, drift)), Is.False);
        Assert.That(player.CurrentState, Is.EqualTo(PlayerState.Stumble));
        Assert.That(player.IsTackleImmune, Is.False);
        Vector3 impact = player.transform.position;
        Assert.That(Mathf.Abs(impact.x), Is.LessThan(FieldConstants.HalfWidth + 0.06f));
        Assert.That(Vector3.Dot(player.StumbleNormal, Vector3.left * side), Is.GreaterThan(0.99f));

        FinishRecoil(player, fps);
        Assert.That(player.transform.position.x, Is.EqualTo(impact.x - side * 0.85f).Within(0.001f));
        Assert.That(player.transform.position.z, Is.EqualTo(impact.z).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator SustainedContactClipsAndSlidesUntilVoluntarySeparation()
    {
        GoLive();
        var player = Actor(0, TeamMember.RosterSlot.RB);
        player.transform.position = new Vector3(FieldConstants.HalfWidth - 1f, 1f, 0f);
        Move(player, player.transform.position + Vector3.right * 6f);
        FinishRecoil(player, 120);
        yield return null; // The wall impact keeps exclusive ownership on its entry frame.

        float startingZ = player.transform.position.z;
        for (int i = 0; i < 10; i++)
        {
            Assert.That(Move(player, player.transform.position + new Vector3(2f, 0f, 0.15f)), Is.True,
                "A clear slide should retain locomotion momentum.");
            Assert.That(player.IsStumbling, Is.False);
            Assert.That(player.transform.position.x, Is.LessThan(FieldConstants.HalfWidth + 0.06f));
        }
        Assert.That(player.transform.position.z, Is.GreaterThan(startingZ + 1f));

        Assert.That(Move(player, player.transform.position + Vector3.left * 2f), Is.True);
        Assert.That(Move(player, player.transform.position + Vector3.right * 5f), Is.False);
        Assert.That(player.IsStumbling, Is.True);
    }

    [UnityTest]
    public IEnumerator CornerClipsBothFacesAndDoesNotAlternateKnockback()
    {
        float end = FieldConstants.PlayLength * 0.5f;
        AddWall("Far end wall", new Vector3(0f, 1f, end + 0.6f),
            new Vector3(FieldConstants.HalfWidth * 2f + 2f, 2f, 0.1f), FieldBoundarySide.FarEndLine);
        GoLive();
        var player = Actor(1, TeamMember.RosterSlot.RB);
        player.transform.position = new Vector3(FieldConstants.HalfWidth - 1f, 1f, end - 1.1f);
        Vector3 corner = player.transform.position + new Vector3(5f, 0f, 5f);

        int impacts = 0;
        for (int i = 0; i < 6; i++)
        {
            Move(player, corner);
            if (player.IsStumbling) { impacts++; FinishRecoil(player, i % 2 == 0 ? 30 : 120); }
            Assert.That(player.transform.position.x, Is.LessThan(FieldConstants.HalfWidth + 0.07f));
            Assert.That(player.transform.position.z, Is.LessThan(end + 0.07f));
            yield return null;
        }
        Assert.That(impacts, Is.EqualTo(2), "One stumble per face, even with continued diagonal input.");
    }

    [Test]
    public void HurdleHitsLowBarrierAndCancelsItsHeightAndImmunity()
    {
        GoLive();
        var player = Actor(0, TeamMember.RosterSlot.RB);
        player.transform.SetPositionAndRotation(new Vector3(FieldConstants.HalfWidth - 2f, 1f, 0f),
            Quaternion.LookRotation(Vector3.right));
        ball.AttachTo(player.transform);
        PossessionController.ClaimControl(player.transform, 0);
        var context = (PlayerContext)player.GetType().GetField("ctx", Private).GetValue(player);
        var hurdle = new HurdleMove();
        Set(hurdle, "heightCurve", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        hurdle.Enter(context);
        context.setTackleImmune(true);
        Set(player, "activeMove", hurdle);
        Set(player, "currentState", PlayerState.Hurdle);

        Call(player, "TickMove", 0.1f);
        Assert.That(player.transform.position.y, Is.GreaterThan(1f));
        Call(player, "TickMove", 0.3f);
        Assert.That(player.IsStumbling, Is.True);
        Assert.That(player.IsTackleImmune, Is.False);
        Assert.That(player.transform.position.y, Is.EqualTo(1f));
        Assert.That(ball.Carrier, Is.SameAs(player.transform));
    }

    [Test]
    public void HumanCarrierVelocityCancelsOnRealLocomotionPath()
    {
        GoLive();
        var player = Actor(0, TeamMember.RosterSlot.QB);
        player.transform.position = new Vector3(FieldConstants.HalfWidth - 0.01f, 1f, 0f);
        ball.AttachTo(player.transform);
        PossessionController.ClaimControl(player.transform, 0);
        var movement = player.GetComponent<PlayerMovement>();
        Set(movement, "currentVelocity", Vector3.right * 100f);
        Call(movement, "Update");

        Assert.That(player.IsStumbling, Is.True);
        Assert.That(movement.GetType().GetField("currentVelocity", Private).GetValue(movement), Is.EqualTo(Vector3.zero));
        Assert.That(ball.Carrier, Is.SameAs(player.transform));
        Assert.That(PossessionController.ActivelyControlled, Is.SameAs(player.transform));
    }

    [Test]
    public void AiRouteAndDefensiveShoveHitWithoutHumanMovementEnabled()
    {
        GoLive();
        var qb = Actor(0, TeamMember.RosterSlot.QB);
        qb.transform.position = new Vector3(0f, 1f, play.AttackDirection.Advance(play.CurrentLineOfScrimmageZ, -3f));
        ball.AttachTo(qb.transform);
        var receiver = Actor(0, TeamMember.RosterSlot.WR1);
        receiver.transform.SetPositionAndRotation(new Vector3(FieldConstants.HalfWidth + 0.049f, 1f, 0f),
            Quaternion.LookRotation(Vector3.right));
        var route = receiver.GetComponent<ReceiverAI>();
        route.SetRoute(RoutePattern.Go);
        Call(route, "Update");
        Assert.That(receiver.IsStumbling, Is.True);
        Assert.That(receiver.GetComponent<PlayerMovement>().enabled, Is.False);

        var defender = Actor(1, TeamMember.RosterSlot.RB);
        defender.transform.position = new Vector3(-FieldConstants.HalfWidth + 0.01f, 1f, 0f);
        var ai = defender.GetComponent<DefenderAI>();
        ai.ApplyPushBack(Vector3.left, 20f);
        Call(ai, "Update");
        Assert.That(defender.IsStumbling, Is.True);
        Assert.That(ai.IsStunned, Is.False, "The wall move replaced the old shove.");
        Assert.That(defender.GetComponent<PlayerMovement>().enabled, Is.False);
    }

    [UnityTest]
    public IEnumerator TransferKeepsActorImpactAndWhistleReleasesIt()
    {
        GoLive();
        var player = Actor(0, TeamMember.RosterSlot.QB);
        player.transform.position = new Vector3(FieldConstants.HalfWidth - 1f, 1f, 0f);
        ball.AttachTo(player.transform);
        var possession = player.GetComponent<PossessionController>();
        Call(possession, "Update");
        Move(player, player.transform.position + Vector3.right * 5f);
        Assert.That(player.IsStumbling, Is.True);

        var receiver = Actor(0, TeamMember.RosterSlot.RB);
        receiver.transform.position = new Vector3(5f, 1f, 0f);
        ball.AttachTo(receiver.transform);
        Call(possession, "Update");
        Call(receiver.GetComponent<PossessionController>(), "Update");
        float impactX = player.transform.position.x;
        yield return null;
        Assert.That(player.IsStumbling, Is.True);
        Assert.That(player.transform.position.x, Is.LessThan(impactX));
        Assert.That(ball.Carrier, Is.SameAs(receiver.transform));
        Assert.That(PossessionController.ActivelyControlled, Is.SameAs(receiver.transform));

        play.EndPlay(PlayState.PlayEndReason.Tackled);
        Assert.That(player.CurrentState, Is.EqualTo(PlayerState.Idle));
        GoLive();
        Assert.That(Move(player, player.transform.position + Vector3.left), Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ResetAndDisableCancelAnImpactAndClearContactLatch(bool reset)
    {
        GoLive();
        var player = Actor(0, TeamMember.RosterSlot.RB);
        player.transform.position = new Vector3(FieldConstants.HalfWidth - 1f, 1f, 0f);
        Vector3 beyond = player.transform.position + Vector3.right * 4f;
        Assert.That(Move(player, beyond), Is.False);
        Assert.That(player.IsStumbling, Is.True);

        if (reset) Call(play, "DoSnap");
        else { player.enabled = false; player.enabled = true; }
        Assert.That(player.IsStumbling, Is.False);
        Assert.That(Move(player, beyond), Is.False);
        Assert.That(player.IsStumbling, Is.True, "A fresh contact can trigger after reset.");
    }

    [Test]
    public void AnOverlappingStartIsPushedBackToTheFieldSide()
    {
        GoLive();
        var player = Actor(0, TeamMember.RosterSlot.RB);
        player.transform.position = new Vector3(FieldConstants.HalfWidth + 0.65f, 1f, 0f);
        Assert.That(Move(player, player.transform.position + Vector3.right), Is.False);
        Assert.That(player.IsStumbling, Is.True);
        Assert.That(player.transform.position.x, Is.LessThan(FieldConstants.HalfWidth + 0.05f));
        Assert.That(player.StumbleNormal, Is.EqualTo(Vector3.left));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void LegalScoreTakesPrecedenceOverContactForEitherDirection(int team)
    {
        GoLive();
        var player = Actor(team, TeamMember.RosterSlot.RB);
        var direction = play.DirectionFor(team);
        player.transform.position = new Vector3(FieldConstants.HalfWidth - 1f, 1f,
            direction.Advance(direction.TargetGoalLineZ, 2f));
        ball.AttachTo(player.transform);
        Vector3 scoredAt = player.transform.position;

        Assert.That(Move(player, scoredAt + Vector3.right * 4f), Is.False);
        Assert.That(player.IsStumbling, Is.False);
        Assert.That(player.transform.position, Is.EqualTo(scoredAt));
        play.EndPlay(PlayState.PlayEndReason.Touchdown);
        Assert.That(play.LastScoringTeamId, Is.EqualTo(team));
    }

    [Test]
    public void DowntownGapRetainsOutOfBoundsAndCannotBePulledBackByWall()
    {
        Assert.That(loader.SelectField(loader.AvailableFields[1]), Is.True);
        GoLive();
        var player = Actor(0, TeamMember.RosterSlot.RB);
        player.transform.position = new Vector3(-FieldConstants.HalfWidth + 1f, 1f, 0f);
        ball.AttachTo(player.transform);
        Assert.That(Move(player, player.transform.position + Vector3.left * 4f), Is.True);
        Assert.That(play.IsBallOutOfBounds(loader.Bounds), Is.True);

        // Behind the fence, a move toward the field cannot save a pending OOB spot.
        player.transform.position += Vector3.forward * 8f;
        Vector3 outside = player.transform.position;
        Assert.That(Move(player, outside + Vector3.right * 5f), Is.False);
        Assert.That(player.transform.position, Is.EqualTo(outside));
        Assert.That(player.IsStumbling, Is.False);
        Call(Object.FindAnyObjectByType<SidelineCheck>(), "Update");
        Assert.That(play.IsLive, Is.False);
    }

    [Test]
    public void InvalidBoundaryDoesNotReplaceWorkingVenue()
    {
        var field = Object.Instantiate(loader.LoadedField);
        field.boundaryPrefab = new GameObject("Unmarked solid");
        field.boundaryPrefab.AddComponent<BoxCollider>();
        var previous = loader.Bounds.BoundaryWalls.ToArray();
        LogAssert.Expect(LogType.Error, "Field boundary prefabs may only contain visual components and marked solid walls.");
        Assert.That(loader.SelectField(field), Is.False);
        CollectionAssert.AreEqual(previous, loader.Bounds.BoundaryWalls);
        Object.DestroyImmediate(field.boundaryPrefab);
        Object.DestroyImmediate(field);
    }

    FieldBoundaryWall AddWall(string name, Vector3 center, Vector3 size, FieldBoundarySide side)
    {
        var root = new GameObject(name);
        root.transform.SetParent(loader.Bounds.transform);
        root.transform.position = center;
        root.AddComponent<BoxCollider>().size = size;
        var wall = root.AddComponent<FieldBoundaryWall>();
        Set(wall, "boundarySide", side);
        Physics.SyncTransforms();
        return wall;
    }

    PlayerStateMachine Actor(int team, TeamMember.RosterSlot slot) =>
        Object.FindObjectsByType<TeamMember>().Single(actor => actor.teamId == team && actor.slot == slot)
            .GetComponent<PlayerStateMachine>();

    void GoLive() => Set(play, "<IsLive>k__BackingField", true);
    static bool Move(PlayerStateMachine player, Vector3 destination) => (bool)Call(player, "MoveTo", destination);

    static void FinishRecoil(PlayerStateMachine player, int fps)
    {
        for (int frame = 0; frame < fps && player.IsStumbling; frame++) Call(player, "TickMove", 1f / fps);
        Assert.That(player.IsStumbling, Is.False);
    }

    static void Set(object owner, string field, object value) =>
        owner.GetType().GetField(field, Private).SetValue(owner, value);
    static object Call(object owner, string method, params object[] args) =>
        owner.GetType().GetMethod(method, Private).Invoke(owner, args);
}
