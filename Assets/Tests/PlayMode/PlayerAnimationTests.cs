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

    void FreezeActors()
    {
        // End the huddle coroutine and disable movement owners without disabling Animator.
        PlayState.Instance.StopAllCoroutines();
        PlayState.Instance.enabled = false;
        foreach (var actor in actors)
            foreach (var behaviour in actor.GetComponents<MonoBehaviour>())
                behaviour.enabled = false;
    }

    static string DominantClip(Animator animator) => animator.GetCurrentAnimatorClipInfo(0)
        .OrderByDescending(info => info.weight).First().clip.name;

    static void MoveAndSample(TeamMember actor, float speed, int frames = 40)
    {
        const float dt = 0.02f;
        var presenter = actor.GetComponent<PlayerAnimationPresenter>();
        actor.transform.rotation = Quaternion.identity;
        Set(presenter, "previousPosition", actor.transform.position);
        for (int frame = 0; frame < frames; frame++)
        {
            actor.transform.position += Vector3.forward * (speed * dt);
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
}
