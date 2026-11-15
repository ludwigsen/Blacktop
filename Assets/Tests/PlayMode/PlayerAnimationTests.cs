using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Exercise the shipped player/controller stack while isolating visual sampling from gameplay AI.
public class PlayerAnimationTests
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    TeamMember[] actors;

    [UnitySetUp]
    public IEnumerator LoadMatch()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        actors = Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None);
    }

    [UnityTearDown]
    public IEnumerator LeaveMatch()
    {
        var match = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Animation cleanup"));
        yield return SceneManager.UnloadSceneAsync(match);
    }

    static void Set(object owner, string field, object value) => owner.GetType().GetField(field, PrivateInstance).SetValue(owner, value);
    static void Sample(PlayerAnimationPresenter presenter, float deltaTime) =>
        typeof(PlayerAnimationPresenter).GetMethod("UpdatePresentation", PrivateInstance).Invoke(presenter, new object[] { deltaTime });

    static object Call(object owner, string method, params object[] args) =>
        owner.GetType().GetMethod(method, PrivateInstance).Invoke(owner, args);

    void FreezeActors(bool keepPresenters = false)
    {
        // End the huddle coroutine and disable movement owners without disabling Animator.
        PlayState.Instance.StopAllCoroutines();
        PlayState.Instance.enabled = false;
        foreach (var actor in actors)
            foreach (var behaviour in actor.GetComponents<MonoBehaviour>())
                if (!keepPresenters || behaviour is not PlayerAnimationPresenter) behaviour.enabled = false;
    }

    static string DominantClip(Animator animator) => animator.GetCurrentAnimatorClipInfo(0)
        .OrderByDescending(info => info.weight).First().clip.name;

    static void MoveAndSample(TeamMember actor, float speed, int frames = 40)
    {
        const float dt = 0.02f;
        var presenter = actor.GetComponent<PlayerAnimationPresenter>();
        Set(presenter, "previousPosition", actor.transform.position);
        // Signed speed is relative to the actor so this can also sample a turning carrier.
        Vector3 step = actor.transform.forward * (speed * dt);
        for (int frame = 0; frame < frames; frame++)
        {
            actor.transform.position += step;
            Sample(presenter, dt);
            Vector3 gameplayPosition = actor.transform.position;
            presenter.Animator.Update(dt);
            Assert.That(actor.transform.position, Is.EqualTo(gameplayPosition), "Animation moved gameplay root");
            // Retargeted running must keep the pelvis near the existing two-unit capsule's center.
            float hipOffset = presenter.Animator.GetBoneTransform(HumanBodyBones.Hips).position.y - actor.transform.position.y;
            Assert.That(hipOffset, Is.InRange(-0.5f, 0.5f), "Imported run height does not match the fitted player body");
        }
    }

    [UnityTest]
    public IEnumerator AllFourteenPlayersShareTheHumanoidAnimationStack()
    {
        Assert.That(actors.Length, Is.EqualTo(14));
        var first = actors[0].GetComponent<PlayerAnimationPresenter>().Animator;
        Assert.That(first.GetLayerIndex("Football"), Is.GreaterThan(0));
        foreach (var actor in actors)
        {
            var presenter = actor.GetComponent<PlayerAnimationPresenter>();
            Assert.That(presenter, Is.Not.Null, actor.name);
            Assert.That(presenter.enabled, Is.True, "AI presentation must remain enabled");
            Assert.That(presenter.Animator.avatar, Is.SameAs(first.avatar));
            Assert.That(presenter.Animator.avatar.isValid && presenter.Animator.avatar.isHuman, Is.True);
            Assert.That(presenter.Animator.runtimeAnimatorController, Is.SameAs(first.runtimeAnimatorController));
            Assert.That(presenter.Animator.applyRootMotion, Is.False);
            string[] clips = presenter.Animator.runtimeAnimatorController.animationClips.Select(clip => clip.name).ToArray();
            foreach (string clip in new[] { "Running Backward", "Standard Run", "Fast Run", "Jump" })
                Assert.That(clips, Does.Contain(clip), actor.name);

            // Native clips keep their Humanoid bindings and have no gameplay release events.
            foreach (string name in new[] { "Carry Ready", "Carry Tuck", "Throw Windup", "Throw Follow Through", "Handoff Give", "Handoff Take" })
            {
                var motion = presenter.Animator.runtimeAnimatorController.animationClips.Single(clip => clip.name == name);
                Assert.That(motion.humanMotion, Is.True, name);
                Assert.That(motion.isLooping, Is.EqualTo(name.StartsWith("Carry")), name);
                Assert.That(motion.events, Is.Empty, name);
            }
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator EveryPlayerCanRunFastRunAndBackpedalWithoutAnimationMovingTheRoot()
    {
        FreezeActors();
        foreach (var actor in actors)
        {
            var presenter = actor.GetComponent<PlayerAnimationPresenter>();
            presenter.Animator.Rebind();
            presenter.Animator.Update(0f);
            MoveAndSample(actor, 6f);
            Assert.That(DominantClip(presenter.Animator), Is.EqualTo("Standard Run"), actor.name);
            MoveAndSample(actor, 10f);
            Assert.That(DominantClip(presenter.Animator), Is.EqualTo("Fast Run"), actor.name);
            MoveAndSample(actor, -4.5f);
            Assert.That(presenter.MovingBackward, Is.True);
            Assert.That(DominantClip(presenter.Animator), Is.EqualTo("Running Backward"), actor.name);
            MoveAndSample(actor, 0f, 60);
            Assert.That(presenter.Animator.GetFloat("Locomotion"), Is.EqualTo(0f).Within(0.01f));
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator TeleportsAndReenableDoNotProduceAFalseRun()
    {
        FreezeActors();
        var actor = actors[0];
        var presenter = actor.GetComponent<PlayerAnimationPresenter>();
        MoveAndSample(actor, 10f);
        actor.transform.position += Vector3.forward * 20f;
        Sample(presenter, 0.02f);
        Assert.That(presenter.PlanarSpeed, Is.Zero);
        Assert.That(presenter.Animator.GetFloat("Locomotion"), Is.Zero);
        actor.transform.position += Vector3.up * 2f;
        Sample(presenter, 0.02f);
        Assert.That(presenter.PlanarSpeed, Is.Zero, "Vertical hurdle motion is not running speed");
        presenter.enabled = true;
        Sample(presenter, 0.02f);
        Assert.That(presenter.PlanarSpeed, Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator HurdleUsesMoveDurationAndWhistleCancelsTheAnimation()
    {
        FreezeActors();
        var actor = actors[0];
        var presenter = actor.GetComponent<PlayerAnimationPresenter>();
        var machine = actor.GetComponent<PlayerStateMachine>();
        var hurdle = (HurdleMove)typeof(PlayerStateMachine).GetField("hurdleMove", PrivateInstance).GetValue(machine);
        var ctx = new PlayerContext { transform = actor.transform, attributes = machine.Attributes,
            setTackleImmune = _ => { }, setCooldown = _ => { }, addOffensePoints = _ => { } };
        float startingY = actor.transform.position.y;
        hurdle.Enter(ctx);
        Set(machine, "activeMove", hurdle);
        Set(machine, "currentState", PlayerState.Hurdle);
        Set(presenter, "previousPosition", actor.transform.position);
        // Feet legitimately tuck/extend in a hurdle; test extra body lift, not ankle travel.
        float baselineBodyOffset = presenter.Animator.bodyPosition.y - actor.transform.position.y;
        float maximumBodyOffset = float.NegativeInfinity, minimumBodyOffset = float.PositiveInfinity;
        for (int frame = 0; frame < 20; frame++)
        {
            hurdle.Tick(ctx, 0.02f);
            Sample(presenter, 0.02f);
            Vector3 gameplayPosition = actor.transform.position;
            presenter.Animator.Update(0.02f);
            Assert.That(actor.transform.position, Is.EqualTo(gameplayPosition));
            if (frame > 3)
            {
                float bodyOffset = presenter.Animator.bodyPosition.y - actor.transform.position.y;
                maximumBodyOffset = Mathf.Max(maximumBodyOffset, bodyOffset);
                minimumBodyOffset = Mathf.Min(minimumBodyOffset, bodyOffset);
            }
        }
        Assert.That(presenter.Animator.GetBool("Hurdling"), Is.True);
        Assert.That(DominantClip(presenter.Animator), Is.EqualTo("Jump"));
        var clip = presenter.Animator.GetCurrentAnimatorClipInfo(0).First().clip;
        Assert.That(presenter.Animator.GetFloat("HurdleRate"), Is.EqualTo(clip.length / hurdle.Duration).Within(0.001f));
        Assert.That(maximumBodyOffset - baselineBodyOffset, Is.LessThan(0.25f), "Source clip adds another upward body arc");
        Assert.That(maximumBodyOffset - minimumBodyOffset, Is.LessThan(0.05f), "Extracted jump body height must remain stable");
        typeof(PlayerStateMachine).GetMethod("HandlePlayEnded", PrivateInstance)
            .Invoke(machine, new object[] { PlayState.PlayEndReason.Tackled });
        Sample(presenter, 0.02f);
        // The first evaluation starts the exit transition; advance normal frames through its blend.
        for (int frame = 0; frame < 8; frame++) presenter.Animator.Update(0.02f);
        Assert.That(actor.transform.position.y, Is.EqualTo(startingY).Within(0.001f));
        Assert.That(presenter.Animator.GetBool("Hurdling"), Is.False);
        Assert.That(presenter.Animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), Is.True);
        yield return null;
    }

    // Breathing Idle.fbx retains its original imported take name.
    [TestCase(0f, "mixamo.com")]
    [TestCase(6f, "Standard Run")]
    [TestCase(10f, "Fast Run")]
    public void CarryUsesTheLiveHoldersGripWhileRulingsFollowThePlayerRoot(float speed, string gait)
    {
        FreezeActors(keepPresenters: true);
        var play = PlayState.Instance;
        var ball = BallController.Instance;
        var qb = SpotPlayer(play.UserTeamId, TeamMember.RosterSlot.QB, 0f, -3f);
        var runner = SpotPlayer(1 - play.UserTeamId, TeamMember.RosterSlot.RB, 3f, 1f);
        var appearance = ball.GetComponentInChildren<FootballAppearance>();
        var visual = appearance.transform;
        bool move = speed > 0f;
        var holder = move ? runner : qb;
        var other = move ? qb : runner;
        ball.AttachTo(holder.transform);
        Set(play, "<IsLive>k__BackingField", move);

        MoveAndSample(holder, speed);
        UpdateBallVisual();
        Assert.That(visual.parent, Is.EqualTo(ball.transform), "Only the visual child follows the grip");
        var hand = holder.GetComponent<PlayerAnimationPresenter>().Animator.GetBoneTransform(HumanBodyBones.RightHand);
        Vector3 gripInHand = hand.InverseTransformPoint(visual.position);
        Assert.That(gripInHand.magnitude, Is.LessThan(0.3f), "The rendered ball should sit at the live holder's hand");
        Assert.That(Vector3.Distance(visual.position, ball.RulingPosition), Is.GreaterThan(0.025f));
        Assert.That(ball.transform.position, Is.EqualTo(ball.RulingPosition));
        AssertFootballState(holder, "Carry");
        Assert.That(FootballDominantClip(holder), Is.EqualTo(move ? "Carry Tuck" : "Carry Ready"));
        Assert.That(DominantClip(holder.GetComponent<PlayerAnimationPresenter>().Animator), Is.EqualTo(gait),
            "The carry overlay must preserve the live locomotion gait");

        // Changing root orientation and movement must leave the held mesh attached to the same anatomical grip.
        holder.transform.Rotate(0f, 105f, 0f);
        MoveAndSample(holder, speed);
        UpdateBallVisual();
        Assert.That(Vector3.Distance(hand.InverseTransformPoint(visual.position), gripInHand), Is.LessThan(0.025f));
        Assert.That(ball.transform.position, Is.EqualTo(ball.RulingPosition));

        // An immediate possession change (including a turnover) moves the carry pose and the ball.
        ball.AttachTo(other.transform);
        MoveAndSample(other, 0f);
        MoveAndSample(holder, 0f);
        UpdateBallVisual();
        var nextHand = other.GetComponent<PlayerAnimationPresenter>().Animator.GetBoneTransform(HumanBodyBones.RightHand);
        Assert.That(Vector3.Distance(visual.position, nextHand.position), Is.LessThan(0.3f));
        AssertFootballState(other, "Carry");
        Assert.That(FootballStateActive(holder, "Carry"), Is.False);

        // A dead loose ball follows the existing ball root with its authored local offset.
        Set(play, "<IsLive>k__BackingField", false);
        ball.Drop();
        UpdateBallVisual();
        Assert.That(visual.localPosition.sqrMagnitude, Is.LessThan(0.000001f));

        void UpdateBallVisual()
        {
            // Mirror frame order: resolve possession and physics before moving the visible football.
            Call(ball, "LateUpdate");
            Call(appearance, "LateUpdate");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HumanWindupFollowsTheCommittedPassAndWhistleCancels(bool whistle)
    {
        var play = PlayState.Instance;
        var ball = BallController.Instance;
        var qb = ArrangePassingPlay(play.UserTeamId);
        var machine = qb.GetComponent<PlayerStateMachine>();
        var presenter = qb.GetComponent<PlayerAnimationPresenter>();
        PossessionController.ClaimControl(qb.transform, qb.teamId);
        StepFootball(presenter, 12);
        var hand = presenter.Animator.GetBoneTransform(HumanBodyBones.RightHand);
        Vector3 readyHand = hand.position;
        qb.GetComponent<InputBuffer>().QueueInput("Pass");
        Call(machine, "Update");
        Assert.That(machine.CurrentState, Is.EqualTo(PlayerState.Pass));

        Vector3 launch = ball.RulingPosition;
        Vector3 root = qb.transform.position;
        var pass = (PassMove)machine.GetType().GetField("passMove", PrivateInstance).GetValue(machine);
        float windup = (float)pass.GetType().GetField("windupDuration", PrivateInstance).GetValue(pass);
        Call(machine, "TickMove", windup * 0.5f);
        StepFootball(presenter, 4);
        AssertFootballState(qb, "Throw Windup");
        Assert.That(Vector3.Distance(hand.position, readyHand), Is.GreaterThan(0.05f),
            "The armed throw must move the shared Humanoid hand");
        Assert.That(ball.Carrier, Is.EqualTo(qb.transform));
        Assert.That(ball.RulingPosition, Is.EqualTo(launch));
        Assert.That(qb.transform.position, Is.EqualTo(root));

        if (whistle)
        {
            play.EndPlay(PlayState.PlayEndReason.Incomplete);
            StepFootball(presenter, 20);
            Assert.That(machine.CurrentState, Is.EqualTo(PlayerState.Idle));
            Assert.That(ball.Carrier, Is.EqualTo(qb.transform));
            Assert.That(FootballStateActive(qb, "Throw Windup"), Is.False);
            Assert.That(FootballStateActive(qb, "Throw Follow Through"), Is.False);
        }
        else
        {
            // Advance the existing gameplay timer to release exactly once. The animation has no release callback.
            Call(machine, "TickMove", windup * 0.51f);
            Assert.That(ball.IsForwardPass, Is.True);
            Assert.That(ball.Carrier, Is.Null);
            Assert.That(ball.transform.position, Is.EqualTo(launch));
            StepFootball(presenter, 4);
            AssertFootballState(qb, "Throw Follow Through");
            Assert.That(ball.transform.position, Is.EqualTo(launch));
            Assert.That(qb.transform.position, Is.EqualTo(root));
        }
    }

    [Test]
    public void CpuReleaseAlsoPlaysThrowFollowThrough()
    {
        var play = PlayState.Instance;
        var ball = BallController.Instance;
        var qb = ArrangePassingPlay(1 - play.UserTeamId);
        var cpu = qb.GetComponent<CPUCarrierAI>();
        Vector3 launch = ball.RulingPosition;

        // Finish the live read in one tick without changing the production release path.
        Set(cpu, "readTimer", 10f);
        Call(cpu, "Update");
        Assert.That(ball.IsForwardPass, Is.True);
        Assert.That(ball.Carrier, Is.Null);
        Assert.That(ball.transform.position, Is.EqualTo(launch));
        var presenter = qb.GetComponent<PlayerAnimationPresenter>();
        StepFootball(presenter, 4);
        AssertFootballState(qb, "Throw Follow Through");

        // A Street pitch shares BallController.Throw but has its own arc and no overhand pose.
        StepFootball(presenter, 20);
        ball.AttachTo(qb.transform);
        Transform back = SpotPlayer(qb.teamId, TeamMember.RosterSlot.RB, 0f, -5f).transform;
        ball.Throw(back.position, back, isPitch: true, arcHeight: 0.5f, duration: 0.15f);
        StepFootball(presenter, 4);
        Assert.That(ball.State, Is.EqualTo(BallController.BallState.InFlight));
        Assert.That(ball.IsForwardPass, Is.False);
        Assert.That(FootballStateActive(qb, "Throw Follow Through"), Is.False);
    }

    [Test]
    public void DesignedHandoffTransfersImmediatelyAndPlaysBothRoles()
    {
        FreezeActors(keepPresenters: true);
        var play = PlayState.Instance;
        var ball = BallController.Instance;
        var run = FindPlay(PlayType.Run);
        play.SetPlayCall(run);
        var qb = SpotPlayer(play.PossessionTeamId, TeamMember.RosterSlot.QB, 0f, -3f);
        var back = SpotPlayer(qb.teamId, run.HandoffReceiverSlot, -0.8f, -3.3f);
        var handoff = Object.FindAnyObjectByType<HandoffCoordinator>();
        var giver = qb.GetComponent<PlayerAnimationPresenter>();
        var taker = back.GetComponent<PlayerAnimationPresenter>();
        ball.AttachTo(qb.transform);
        Set(play, "<IsLive>k__BackingField", true);
        Vector3 qbPosition = qb.transform.position;

        Call(handoff, "Update");
        Assert.That(ball.Carrier, Is.EqualTo(back.transform), "The exchange keeps the existing mesh-point timing");
        Assert.That(ball.IsHeld, Is.True);
        var appearance = ball.GetComponentInChildren<FootballAppearance>();
        var givingHand = giver.Animator.GetBoneTransform(HumanBodyBones.RightHand);
        var takingHand = taker.Animator.GetBoneTransform(HumanBodyBones.RightHand);
        SampleExchange(1);
        AssertFootballState(qb, "Handoff Give");
        AssertFootballState(back, "Handoff Take");
        Assert.That(Vector3.Distance(appearance.transform.position, givingHand.position), Is.LessThan(0.3f),
            "The visible ball starts at the hand giving it");
        float initialGap = Vector3.Distance(appearance.transform.position, takingHand.position);
        SampleExchange(10);
        float remainingGap = Vector3.Distance(appearance.transform.position, takingHand.position);
        Assert.That(remainingGap, Is.LessThan(0.3f), "The visible ball ends at the hand receiving it");
        Assert.That(remainingGap, Is.LessThan(initialGap));
        Assert.That(ball.transform.position, Is.EqualTo(ball.RulingPosition));

        // Simulate a recovery by the QB at the same mesh point. One play has only one exchange.
        ball.AttachTo(qb.transform);
        Call(handoff, "Update");
        Assert.That(ball.Carrier, Is.EqualTo(qb.transform));

        play.EndPlay(PlayState.PlayEndReason.Tackled);
        StepFootball(giver, 20);
        StepFootball(taker, 20);
        Assert.That(FootballStateActive(qb, "Handoff Give"), Is.False);
        Assert.That(FootballStateActive(back, "Handoff Take"), Is.False);

        // Stage the next formation and use the real snap event to rearm the handoff.
        play.StopAllCoroutines();
        SpotPlayer(qb.teamId, qb.slot, 0f, -3f);
        SpotPlayer(back.teamId, back.slot, -0.8f, -3.3f);
        Call(play, "DoSnap");
        Assert.That(ball.Carrier, Is.EqualTo(qb.transform));
        Call(handoff, "Update");
        Assert.That(ball.Carrier, Is.EqualTo(back.transform), "A new snap rearms the designed exchange");
        StepFootball(giver, 1);
        StepFootball(taker, 1);
        AssertFootballState(qb, "Handoff Give");
        AssertFootballState(back, "Handoff Take");

        void SampleExchange(int frames)
        {
            // Sample the edited rig and render hand transfer after canonical held-position follow.
            StepFootball(giver, frames);
            StepFootball(taker, frames);
            Call(ball, "LateUpdate");
            Call(giver, "LateUpdate");
            Call(taker, "LateUpdate");
            Call(appearance, "UpdateBallVisual", frames * 0.02f);
            Assert.That(qb.transform.position, Is.EqualTo(qbPosition));
        }
    }

    TeamMember ArrangePassingPlay(int team)
    {
        FreezeActors(keepPresenters: true);
        var play = PlayState.Instance;
        Assert.That(play.TrySetPossession(team), Is.True);
        play.SetPlayCall(FindPlay(PlayType.Pass));
        var qb = SpotPlayer(team, TeamMember.RosterSlot.QB, 0f, -3f);
        foreach (Transform target in play.OffensivePlayers.Where(ReceiverTargeting.IsEligible))
            target.position = qb.transform.position + play.AttackDirection.Forward * 12f;
        BallController.Instance.AttachTo(qb.transform);
        Set(play, "<IsLive>k__BackingField", true);
        return qb;
    }

    static PlayCallData FindPlay(PlayType type) => Object.FindAnyObjectByType<PlayCallSelector>()
        .OffensivePlays.First(play => play.PlayType == type);

    TeamMember SpotPlayer(int team, TeamMember.RosterSlot slot, float x, float yardsFromLOS)
    {
        var actor = actors.Single(member => member.teamId == team && member.slot == slot);
        var play = PlayState.Instance;
        var direction = play.DirectionFor(team);
        actor.transform.SetPositionAndRotation(new Vector3(x, 1f, direction.Advance(play.CurrentLineOfScrimmageZ, yardsFromLOS)),
            direction.Rotation);
        // The fixture represents players already in formation before the live cue.
        Set(actor.GetComponent<PlayerAnimationPresenter>(), "previousPosition", actor.transform.position);
        return actor;
    }

    static void StepFootball(PlayerAnimationPresenter presenter, int frames)
    {
        const float step = 0.02f;
        for (int i = 0; i < frames; i++)
        {
            Sample(presenter, step);
            presenter.Animator.Update(step);
        }
    }

    static string FootballDominantClip(TeamMember actor)
    {
        var animator = actor.GetComponent<PlayerAnimationPresenter>().Animator;
        int layer = animator.GetLayerIndex("Football");
        return animator.GetCurrentAnimatorClipInfo(layer).OrderByDescending(info => info.weight).First().clip.name;
    }

    static bool FootballStateActive(TeamMember actor, string state)
    {
        var animator = actor.GetComponent<PlayerAnimationPresenter>().Animator;
        int layer = animator.GetLayerIndex("Football");
        Assert.That(layer, Is.GreaterThanOrEqualTo(0), actor.name);
        // An entry can be either the destination or current state over a valid Animator crossfade.
        return animator.GetLayerWeight(layer) > 0.01f && (animator.GetCurrentAnimatorStateInfo(layer).IsName(state) ||
            (animator.IsInTransition(layer) && animator.GetNextAnimatorStateInfo(layer).IsName(state)));
    }

    static void AssertFootballState(TeamMember actor, string state) =>
        Assert.That(FootballStateActive(actor, state), Is.True, $"{actor.name} should play {state}");
}
