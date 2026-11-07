using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Exercise the real ball, player colliders, field/rules and marker components.
// Explicit time steps cover slow frames without depending on Editor frame timing.
public class BallFlightTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    BallController ball;
    PlayState play;
    TeamMember[] players;
    PlayState.PlayEndReason EndReason => (PlayState.PlayEndReason)play.GetType().GetField("lastEndReason", Private).GetValue(play);

    [UnitySetUp]
    public IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        ball = BallController.Instance;
        play = PlayState.Instance;
        play.StopAllCoroutines();
        // Freeze autonomous actors; tests advance the production ball integration.
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>())
                if (behaviour != ball && behaviour is not BallGroundMarkers && behaviour is not TeamMember)
                    behaviour.enabled = false;
        ball.enabled = false;
        Set(play, "<IsLive>k__BackingField", true);
        players = Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None);
        foreach (var player in players) player.transform.position = new Vector3(10f, 1f, 20f);
        Set(ball, "carryOffset", Vector3.zero);
        Set(ball, "baseInterceptChance", 0f);
        Set(ball, "fumbleScatterAngle", 0f);
        Set(ball, "fumbleMinDistance", 2f);
        Set(ball, "fumbleMaxDistance", 2f);
        Physics.SyncTransforms();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Scene match = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Ball cleanup"));
        yield return SceneManager.UnloadSceneAsync(match);
    }

    static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);
    void Step(float time) => Call(ball, "Simulate", time); // production owns transform synchronization

    TeamMember Player(int team)
    {
        foreach (var member in players) if (member.teamId == team) return member;
        Assert.Fail("Missing team " + team);
        return null;
    }

    void Pop(Vector3 spot)
    {
        ball.Drop();
        ball.transform.SetPositionAndRotation(spot, Quaternion.identity);
        ball.Fumble(spot, spot - Vector3.right);
    }

    [TestCase(8f, 0.3f)]
    [TestCase(36f, 1.2f)]
    [TestCase(-36f, 1.2f)]
    public void PassHasConstantGravityCommittedEndpointAndSpiral(float distance, float duration)
    {
        Vector3 start = new(0f, 1.2f, 0f);
        Vector3 target = start + Vector3.forward * distance;
        ball.Drop(); ball.transform.position = start;
        ball.Throw(target, null, false, 99f, duration);
        Vector3 initial = ball.Velocity;
        Step(duration * 0.25f);
        Assert.That(ball.transform.position.y, Is.EqualTo(start.y + 9.81f * duration * duration * 3f / 32f).Within(0.0001f));
        Assert.That(ball.Velocity.y, Is.EqualTo(initial.y - 9.81f * duration * 0.25f).Within(0.0001f));
        Assert.That(Vector3.Dot(ball.transform.forward, ball.Velocity.normalized), Is.GreaterThan(0.999f));
        Quaternion rotation = ball.transform.rotation;
        Step(duration * 0.25f);
        Assert.That(Quaternion.Angle(rotation, ball.transform.rotation), Is.GreaterThan(1f));
        Assert.That(ball.FlightTarget, Is.EqualTo(target));
        Step(duration * 0.5f);
        Assert.That(Vector3.Distance(ball.transform.position, target), Is.LessThan(0.0001f));
        Assert.That(EndReason, Is.EqualTo(PlayState.PlayEndReason.Incomplete));
        Assert.That(ball.IsLiveLoose, Is.False, "A missed forward pass must stay dead");
    }

    [TestCase(0)]
    [TestCase(1)]
    public void EitherTeamRecoversInAirExactlyOnceAndStopsMotion(int team)
    {
        Pop(new Vector3(0f, 2f, 0f));
        Assert.That(ball.State, Is.EqualTo(BallController.BallState.Loose));
        Step(0.12f);
        Assert.That(ball.transform.position.y, Is.GreaterThan(2f));
        var winner = Player(team);
        winner.transform.position = ball.transform.position;
        // Two colliders on the same actor still produce a root-transform carrier.
        var child = new GameObject("Extra recovery collider");
        child.transform.SetParent(winner.transform, false);
        child.AddComponent<SphereCollider>();
        Step(0.1f);
        Assert.That(ball.Carrier, Is.EqualTo(winner.transform));
        Assert.That(ball.IsHeld, Is.True);
        Assert.That(ball.Velocity, Is.EqualTo(Vector3.zero));
        Assert.That(play.IsLive, Is.True, "Air recovery is not an interception whistle");
        Assert.That(ball.IntendedReceiver, Is.Null);
        // Recovery must flow through the existing carrier/defender authority too.
        foreach (var member in players) Call(member.GetComponent<PossessionController>(), "Update");
        Call(DefenderControlManager.Instance, "Update");
        Assert.That(PossessionController.ActivelyControlledTeamId, Is.EqualTo(play.UserTeamId));
        if (team == play.UserTeamId)
            Assert.That(PossessionController.ActivelyControlled, Is.EqualTo(winner.transform));
        else
            Assert.That(PossessionController.ActivelyControlled, Is.Not.EqualTo(winner.transform));
    }

    [Test]
    public void SimultaneousRecoveryUsesEntityTieBreak()
    {
        var a = Player(0); var b = Player(1);
        Pop(new Vector3(0f, 2f, 0f));
        a.transform.position = b.transform.position = ball.transform.position;
        Step(0.02f);
        Transform expected = a.transform.GetEntityId().CompareTo(b.transform.GetEntityId()) < 0 ? a.transform : b.transform;
        Assert.That(ball.Carrier, Is.EqualTo(expected));
        Step(0.5f);
        Assert.That(ball.Carrier, Is.EqualTo(expected));
    }

    [TestCase(1f / 30f)]
    [TestCase(1f / 120f)]
    public void FreeBallBouncesRollsAndSettlesWithoutJitter(float frame)
    {
        Pop(new Vector3(-6f, 1f, 0f));
        bool descended = false, bounced = false;
        for (int i = 0; i < Mathf.CeilToInt(10f / frame); i++)
        {
            Step(frame);
            if (ball.Velocity.y < -0.5f) descended = true;
            if (descended && ball.Velocity.y > 0.5f) bounced = true;
            Assert.That(ball.transform.position.y, Is.GreaterThanOrEqualTo(0.09f));
        }
        Assert.That(bounced, Is.True);
        Assert.That(ball.Velocity.magnitude, Is.LessThan(0.01f));
        Vector3 resting = ball.transform.position;
        Step(1f);
        Assert.That(Vector3.Distance(resting, ball.transform.position), Is.LessThan(0.005f));
    }

    [Test]
    public void FastBallCannotTunnelThroughThinWall()
    {
        Pop(new Vector3(0f, 1f, 0f));
        var wall = new GameObject("Thin fence");
        wall.transform.position = new Vector3(2f, 1f, 0f);
        var collider = wall.AddComponent<BoxCollider>(); collider.size = new Vector3(0.03f, 5f, 5f);
        var motion = (BallLooseMotion)ball.GetType().GetField("looseMotion", Private).GetValue(ball);
        motion.Launch(ball.transform, new Vector3(80f, 0f, 0f));
        Step(0.08f);
        Assert.That(ball.transform.position.x, Is.LessThan(1.9f));
        Assert.That(ball.Velocity.x, Is.LessThan(0f));
        Object.DestroyImmediate(wall);
    }

    [Test]
    public void RecoveryRadiusCannotReachThroughSolidFence()
    {
        Pop(new Vector3(0f, 1f, 0f));
        var wall = new GameObject("Recovery fence");
        wall.transform.position = new Vector3(0.6f, 1f, 0f);
        wall.AddComponent<BoxCollider>().size = new Vector3(0.03f, 5f, 5f);
        Player(1).transform.position = new Vector3(1.1f, 1f, 0f);
        Step(0.01f);
        Assert.That(ball.Carrier, Is.Null);
        Object.DestroyImmediate(wall);
    }

    [Test]
    public void FumbleScatterStaysNearConfiguredFirstContactDistance()
    {
        Pop(new Vector3(0f, 1f, 0f));
        bool descended = false;
        for (int i = 0; i < 250; i++)
        {
            Step(1f / 240f);
            if (ball.Velocity.y < -0.5f) descended = true;
            if (descended && ball.Velocity.y >= 0f)
            {
                Assert.That(ball.transform.position.x, Is.EqualTo(2f).Within(0.12f));
                return;
            }
        }
        Assert.Fail("Expected first ground contact");
    }

    [Test]
    public void PlayerBodyDeflectsBallOutsideRecoveryRange()
    {
        Pop(new Vector3(0f, 1f, 0f));
        Set(ball, "recoveryRadius", 0.01f); // exercise body contact, rather than a scoop
        var player = Player(1); player.transform.position = new Vector3(2f, 1f, 0f);
        var motion = (BallLooseMotion)ball.GetType().GetField("looseMotion", Private).GetValue(ball);
        motion.Launch(ball.transform, new Vector3(30f, 0f, 0f));
        Step(0.1f);
        Assert.That(ball.Carrier, Is.Null);
        Assert.That(ball.Velocity.x, Is.LessThan(0f));
    }

    [Test]
    public void ReleaseOverlapsClearSpatiallyAndCanRecoverOnReturn()
    {
        var former = Player(0); former.transform.position = new Vector3(0f, 1f, 0f);
        ball.AttachTo(former.transform); Physics.SyncTransforms();
        ball.Fumble(former.transform.position, former.transform.position - Vector3.right);
        Step(0.01f);
        Assert.That(ball.IsHeld, Is.False, "The initial tackle overlap must not erase the pop");
        former.transform.position = new Vector3(-10f, 1f, 0f); Step(0.01f);
        former.transform.position = ball.transform.position; Step(0.01f);
        Assert.That(ball.Carrier, Is.EqualTo(former.transform));
    }

    [Test]
    public void ChasingInitialContactBecomesEligibleWithoutLeavingScoopRange()
    {
        var former = Player(0); former.transform.position = new Vector3(0f, 1f, 0f);
        ball.AttachTo(former.transform);
        ball.Fumble(former.transform.position, former.transform.position - Vector3.right);
        for (int i = 0; i < 40 && !ball.IsHeld; i++)
        {
            // Keep the same player inside the scoop volume throughout the pop.
            former.transform.position = ball.transform.position;
            Step(0.01f);
        }
        Assert.That(ball.Carrier, Is.EqualTo(former.transform), "Launch overlap must not become permanent exclusion");
        Assert.That(play.IsLive, Is.True);
    }

    [Test]
    public void LooseBallOutsideBoundaryIsWhistledBeforeRecovery()
    {
        Pop(new Vector3(FieldConstants.HalfWidth + FieldConstants.OutOfBoundsMargin - 0.1f, 2f, 0f));
        Step(0.2f);
        Assert.That(play.IsLive, Is.False);
        Assert.That(EndReason, Is.EqualTo(PlayState.PlayEndReason.OutOfBounds));
        Assert.That(ball.Carrier, Is.Null);
        Assert.That(ball.Velocity, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void MarkersTrackCommittedPositionsAndClearOnCatchWhistleResetDisable()
    {
        var markers = ball.GetComponent<BallGroundMarkers>();
        ball.enabled = true;
        ball.Drop(); ball.transform.position = new Vector3(0f, 2f, 0f);
        Vector3 target = new(4f, 1f, 7f);
        ball.Throw(target, null, false, 2f, 1f);
        Call(markers, "LateUpdate");
        Assert.That(markers.TargetVisible, Is.True); Assert.That(markers.LooseVisible, Is.False);
        Assert.That(markers.TargetPosition.x, Is.EqualTo(target.x));
        Assert.That(markers.TargetPosition.z, Is.EqualTo(target.z));
        ball.AttachTo(Player(0).transform); Call(markers, "LateUpdate");
        Assert.That(markers.TargetVisible, Is.False);
        Pop(new Vector3(2f, 3f, 2f)); Call(markers, "LateUpdate");
        Assert.That(markers.LooseVisible, Is.True); Assert.That(markers.TargetVisible, Is.False);
        Assert.That(markers.LoosePosition.x, Is.EqualTo(ball.transform.position.x));
        play.EndPlay(PlayState.PlayEndReason.Incomplete); Call(markers, "LateUpdate");
        Assert.That(markers.LooseVisible, Is.False);
        Call(ball, "HandlePlayReset"); Call(markers, "LateUpdate");
        Assert.That(markers.TargetVisible, Is.False);
        Set(play, "<IsLive>k__BackingField", true);
        Pop(new Vector3(0f, 3f, 0f)); Call(markers, "LateUpdate");
        markers.enabled = false;
        Assert.That(markers.LooseVisible, Is.False);
    }

    [Test]
    public void PitchStillTracksAndCompletesWithoutInterception()
    {
        var receiver = Player(0); receiver.transform.position = new Vector3(3f, 1f, 0f);
        ball.Drop(); ball.transform.position = new Vector3(0f, 1f, 0f);
        ball.Throw(receiver.transform.position, receiver.transform, true, 0.3f, 0.15f);
        receiver.transform.position += Vector3.forward * 2f;
        Step(0.15f);
        Assert.That(ball.Carrier, Is.EqualTo(receiver.transform));
        Assert.That(play.IsLive, Is.True);
    }

    [Test]
    public void DroppedPassDeflectionNeverShowsLiveMarkerOrRecovers()
    {
        var receiver = Player(0);
        receiver.transform.position = new Vector3(3f, 1f, 0f);
        Set(ball, "baseCatchChance", 0f);
        ball.Drop(); ball.transform.position = new Vector3(0f, 1f, 0f);
        ball.Throw(receiver.transform.position, receiver.transform, false, 0f, 0.2f);
        Step(0.2f);
        Assert.That(ball.State, Is.EqualTo(BallController.BallState.InFlight));
        Assert.That(ball.IsForwardPass, Is.False);
        ball.enabled = true;
        var markers = ball.GetComponent<BallGroundMarkers>(); Call(markers, "LateUpdate");
        Assert.That(markers.LooseVisible || markers.TargetVisible, Is.False);
        Step(0.3f);
        Assert.That(ball.Carrier, Is.Null);
        Assert.That(play.IsLive, Is.False);
        Assert.That(EndReason, Is.EqualTo(PlayState.PlayEndReason.Incomplete));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void SettledFumbleRemainsRecoverableByEitherTeam(int team)
    {
        Pop(new Vector3(-6f, 1f, 0f));
        Step(8f);
        Assert.That(ball.Velocity, Is.EqualTo(Vector3.zero));
        var winner = Player(team);
        winner.transform.position = ball.transform.position + Vector3.up;
        Step(0.02f);
        Assert.That(ball.Carrier, Is.EqualTo(winner.transform));
        Assert.That(play.IsLive, Is.True);
    }
}
