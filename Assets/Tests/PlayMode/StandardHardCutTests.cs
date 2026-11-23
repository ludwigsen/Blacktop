using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class StandardHardCutTests
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Observe = typeof(StandardHardCut).GetMethod("Observe", PrivateInstance);
    GameObject root;
    PlayerVisualAssembler assembler;
    Animator animator;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        var settings = Object.FindAnyObjectByType<MatchBootstrap>().settings;
        // Use the shipped preview rig and catalog with no input, AI or possession component.
        root = new GameObject("Hard cut test actor");
        root.transform.position = Vector3.up;
        var model = Object.Instantiate(settings.visualPrefab, root.transform, false);
        assembler = root.AddComponent<PlayerVisualAssembler>();
        assembler.catalog = settings.appearances;
        assembler.defaultAppearance = settings.defaultAppearance;
        assembler.rig = model.GetComponent<PlayerVisualRig>();
        animator = assembler.rig.animator;
        Assert.That(animator.avatar.isValid && animator.isHuman, Is.True);
        Assert.That(animator.applyRootMotion, Is.False);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        var scene = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Hard cut cleanup"));
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    [Test]
    public void BothSidesHoldOutsideAnkleAndKeepGameplayRootAcrossAvailableBuildsAndHeights()
    {
        var appearance = ScriptableObject.CreateInstance<PlayerAppearanceDefinition>();
        try
        {
            var profiles = assembler.catalog.heightProfiles.OrderBy(p => p.Centimeters).ToArray();
            foreach (BodyArchetype build in Enum.GetValues(typeof(BodyArchetype)))
            foreach (var height in new[] { profiles[0].height, profiles[^1].height })
            {
                var selection = AppearanceSelection.From(assembler.defaultAppearance);
                selection.bodyArchetype = build;
                selection.bodyId = build.ToString().ToUpperInvariant();
                selection.height = height;
                selection.clothingIds = Array.Empty<string>();
                selection.Apply(appearance, assembler.catalog, "hard-cut");
                Assert.That(assembler.TryApply(appearance), Is.True, assembler.LastDiagnostics);

                animator = assembler.rig.animator; // Each fitted body owns its Humanoid Avatar.
                foreach (float sign in new[] { -1f, 1f })
                {
                    const float pace = 4.8f;
                    const float duration = 0.32f;
                    Vector3 exit = Quaternion.Euler(0f, sign * 65f, 0f) * Vector3.forward;
                    root.transform.SetPositionAndRotation(Vector3.up, Quaternion.identity);
                    animator.Rebind();
                    var pose = new StandardHardCut();
                    pose.Bind(animator, root.transform);
                    AdvanceGait(0.18f);
                    Assert.That(pose.CapturePlant(exit, Vector3.forward, pace), Is.True);
                    HumanBodyBones expected = sign < 0f ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot;
                    Assert.That(pose.PlantFoot, Is.SameAs(animator.GetBoneTransform(expected)));

                    var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    var rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    float last = 0f;
                    foreach (float t in new[] { 0.12f, StandardHardCut.PlantStart, 0.30f, StandardHardCut.PlantEnd, 0.58f, 1f })
                    {
                        // Ground travel is an input to the pose. Redirect without moving the pinned ankle target.
                        float approach = Mathf.Min(t, 0.24f);
                        Vector3 position = Vector3.up + duration * pace * (approach * Vector3.forward + (t - approach) * exit);
                        Quaternion rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.LookRotation(exit), t);
                        root.transform.SetPositionAndRotation(position, rotation);
                        AdvanceGait((t - last) * duration);
                        Vector3 gaitHips = hips.position;
                        Quaternion gaitFacing = root.transform.rotation;
                        Quaternion gaitCarryArm = rightArm.localRotation;
                        pose.ApplyPose(t, true);
                        if (t >= StandardHardCut.PlantStart && t <= StandardHardCut.PlantEnd)
                        {
                            string context = $"{build}, {height}, {expected}, {t}";
                            Assert.That(Vector3.Distance(pose.PlantFoot.position, pose.PlantWorldPosition), Is.LessThan(0.015f), context);
                            Assert.That(gaitHips.y - hips.position.y, Is.GreaterThan(0.09f), context);
                        }

                        Assert.That(Quaternion.Angle(rightArm.localRotation, gaitCarryArm), Is.LessThan(0.06f),
                            "The pose must keep the existing ball arm's local motion.");
                        Assert.That(root.transform.position, Is.EqualTo(position));
                        Assert.That(Quaternion.Angle(root.transform.rotation, gaitFacing), Is.LessThan(0.06f));
                        if (t == 1f) Assert.That(hips.position, Is.EqualTo(gaitHips), "Recovery must leave locomotion intact.");
                        last = t;
                    }
                }
            }
        }
        finally { Object.DestroyImmediate(appearance); }
    }

    [Test]
    public void SuddenNoBallSteeringTriggersAndRecoversWhileInterruptionClearsThePlant()
    {
        var pose = new StandardHardCut();
        pose.Bind(animator, root.transform);
        AdvanceGait(0.18f);
        for (int i = 0; i < 14; i++) Step(pose, Vector3.forward * 5f);
        Assert.That(pose.IsActive, Is.False, "Ordinary approach must continue the base gait.");
        Vector3 cutLeft = Quaternion.Euler(0f, -75f, 0f) * Vector3.forward * 5f;
        Step(pose, cutLeft);
        Assert.That(pose.IsActive, Is.True);
        Assert.That(pose.PlantFoot, Is.SameAs(animator.GetBoneTransform(HumanBodyBones.RightFoot)));
        var arm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Quaternion baseArm = arm.localRotation;
        pose.ApplyPose(0.27f, false);
        Assert.That(Quaternion.Angle(arm.localRotation, baseArm), Is.GreaterThan(5f));
        Step(pose, cutLeft, interrupted: true);
        Assert.That(pose.IsActive, Is.False);
        Assert.That(pose.PlantFoot, Is.Null);

        for (int i = 0; i < 14; i++) Step(pose, cutLeft);
        Step(pose, Vector3.forward * 5f);
        Assert.That(pose.IsActive, Is.True);
        for (int i = 0; i < 28; i++) Step(pose, Vector3.forward * 5f);
        Assert.That(pose.IsActive, Is.False, "Constant exit direction should finish and resume the base gait.");
    }

    [TestCase(-1f)]
    [TestCase(1f)]
    public void ExistingJukeCueUsesTheAcceptedSideAndClearsAtTheWhistle(float side)
    {
        var play = PlayState.Instance;
        play.StopAllCoroutines();
        typeof(PlayState).GetField("<IsLive>k__BackingField", PrivateInstance).SetValue(play, true);
        var qb = Object.FindObjectsByType<TeamMember>().Single(actor =>
            actor.teamId == play.UserTeamId && actor.slot == TeamMember.RosterSlot.QB);
        qb.transform.SetPositionAndRotation(Vector3.up, Quaternion.identity);
        var ball = BallController.Instance;
        ball.AttachTo(qb.transform);
        PossessionController.ClaimControl(qb.transform, qb.teamId);

        var machine = qb.GetComponent<PlayerStateMachine>();
        var movement = qb.GetComponent<PlayerMovement>();
        var presenter = qb.GetComponent<PlayerAnimationPresenter>();
        var pose = (StandardHardCut)typeof(PlayerAnimationPresenter).GetField("hardCut", PrivateInstance).GetValue(presenter);
        var sample = typeof(PlayerAnimationPresenter).GetMethod("UpdatePresentation", PrivateInstance);
        var tickMove = typeof(PlayerStateMachine).GetMethod("TickMove", PrivateInstance);
        var input = typeof(PlayerMovement).GetField("moveInput", PrivateInstance);
        input.SetValue(movement, Vector2.right * side);
        qb.GetComponent<InputBuffer>().QueueInput("Juke");
        typeof(PlayerStateMachine).GetMethod("Update", PrivateInstance).Invoke(machine, null);
        Assert.That(machine.CurrentState, Is.EqualTo(PlayerState.Juke));
        sample.Invoke(presenter, new object[] { 0.02f });
        Assert.That(pose.IsActive, Is.True);
        var outsideFoot = side > 0f ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
        Assert.That(pose.PlantFoot, Is.SameAs(presenter.Animator.GetBoneTransform(outsideFoot)));

        // Move ownership and its committed direction stay on the gameplay action after input reverses.
        input.SetValue(movement, Vector2.left * side);
        var before = qb.transform.position;
        tickMove.Invoke(machine, new object[] { 0.03f });
        sample.Invoke(presenter, new object[] { 0.03f });
        Assert.That((qb.transform.position - before).x * side, Is.GreaterThan(0f));
        Assert.That(qb.transform.position.z, Is.EqualTo(before.z));
        Assert.That(ball.Carrier, Is.SameAs(qb.transform));
        Assert.That(pose.PlantFoot, Is.SameAs(presenter.Animator.GetBoneTransform(outsideFoot)));

        play.EndPlay(PlayState.PlayEndReason.Incomplete);
        sample.Invoke(presenter, new object[] { 0.02f });
        Assert.That(machine.CurrentState, Is.EqualTo(PlayerState.Idle));
        Assert.That(pose.IsActive, Is.False);
    }

    void AdvanceGait(float seconds)
    {
        animator.SetFloat("Locomotion", 1f);
        animator.Update(seconds);
    }

    static void Step(StandardHardCut pose, Vector3 velocity, bool interrupted = false)
    {
        const float dt = 1f / 60f;
        Observe.Invoke(pose, new object[] { velocity * dt, dt, interrupted, null });
    }
}
