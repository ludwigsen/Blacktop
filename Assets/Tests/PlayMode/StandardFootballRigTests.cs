using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class StandardFootballRigTests
{
    Scene match;

    static readonly string[] ClipNames =
    {
        "Carry Ready", "Carry Tuck", "Throw Windup", "Throw Follow Through", "Handoff Give", "Handoff Take"
    };

    static readonly HumanBodyBones[] RequiredBones =
    {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightLittleProximal,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
    };

    [UnityTearDown]
    public IEnumerator UnloadMatch()
    {
        if (!match.IsValid() || !match.isLoaded) yield break;
        SceneManager.SetActiveScene(SceneManager.CreateScene("Football rig cleanup"));
        yield return SceneManager.UnloadSceneAsync(match);
    }

    [UnityTest]
    public IEnumerator StandardFootballMotionsDeformAllSixFittedHumanoidSkins()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        match = SceneManager.GetActiveScene();
        var settings = Object.FindAnyObjectByType<MatchBootstrap>().settings;
        var root = new GameObject("Football clip skin fixture");
        var model = Object.Instantiate(settings.visualPrefab, root.transform);
        var rig = model.GetComponent<PlayerVisualRig>();
        var animator = rig.animator;

        var controller = animator.runtimeAnimatorController;
        var appearance = ScriptableObject.CreateInstance<PlayerAppearanceDefinition>();
        var baked = new Mesh();
        var vertices = new List<Vector3>();
        try
        {
            Assert.That(rig.ValidateRig(out string diagnostic), Is.True, diagnostic);
            var authoredMotions = controller.animationClips.Distinct().ToArray();
            var clips = ClipNames.Select(name => authoredMotions.Single(clip => clip.name == name)).ToArray();
            var run = authoredMotions.Single(clip => clip.name == "Running");
            var distinctBodies = new HashSet<Mesh>();
            var assembler = root.AddComponent<PlayerVisualAssembler>();
            assembler.catalog = settings.appearances;
            assembler.rig = rig;
            foreach (BodyArchetype build in Enum.GetValues(typeof(BodyArchetype)))
            {
                var body = assembler.catalog.Find(build.ToString().ToUpperInvariant());
                var selection = AppearanceSelection.From(settings.defaultAppearance);
                selection.bodyId = body.id;
                selection.bodyArchetype = build;
                selection.clothingIds = Array.Empty<string>();
                foreach (PlayerHeight height in new[] { PlayerHeight.Ft5In7, PlayerHeight.Ft6In0, PlayerHeight.Ft6In7 })
                {
                    selection.height = height;
                    selection.Apply(appearance, assembler.catalog, "football-rig-test");
                    Assert.That(assembler.TryApply(appearance), Is.True, assembler.LastDiagnostics);
                    Assert.That(rig.bodyRenderer.sharedMesh, Is.SameAs(body.skeleton.referenceSkin.sharedMesh), body.id);
                    SampleSkins($"{build} {height}");
                }
                Assert.That(distinctBodies.Add(rig.bodyRenderer.sharedMesh), Is.True, "Body unexpectedly reuses another mesh");
            }

            void SampleSkins(string label)
            {
                animator = rig.animator;
                Assert.That(animator.avatar.isHuman && animator.avatar.isValid, Is.True, label);
                var bones = RequiredBones.Select(animator.GetBoneTransform).ToArray();
                for (int i = 0; i < bones.Length; i++)
                    Assert.That(bones[i], Is.Not.Null, RequiredBones[i].ToString());
                Assert.That(animator.applyRootMotion, Is.False, label);
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>().Where(skin => skin.enabled).ToArray();
                Assert.That(skins, Is.Not.Empty, label);
                foreach (var skin in skins)
                    Assert.That(skin.bones.All(bone => bone != null && bone.IsChildOf(animator.transform)), Is.True, skin.name);

                Vector3 gameplayPosition = root.transform.position;
                Quaternion gameplayRotation = root.transform.rotation;
                Vector3 modelScale = model.transform.localScale;
                var boneScales = bones.Select(bone => bone.localScale).ToArray();
                foreach (var clip in clips)
                {
                    Assert.That(clip.humanMotion, Is.True, clip.name);
                    Assert.That(clip.events, Is.Empty, $"{clip.name} must never own a gameplay event");
                    SampleMotion(clip, 0);
                }
                SampleMotion(run, -1);
                SampleMotion(run, 1);

                void SampleMotion(AnimationClip clip, int cutSide)
                {
                    var graph = PlayableGraph.Create();
                    try
                    {
                        var motion = AnimationClipPlayable.Create(graph, clip);
                        AnimationPlayableOutput.Create(graph, "Football clip", animator).SetSourcePlayable(motion);
                        graph.Play();
                        var cut = new StandardHardCut();
                        if (cutSide != 0)
                        {
                            graph.Evaluate(0f);
                            cut.Bind(animator, root.transform);
                            Assert.That(cut.CapturePlant(Vector3.right * cutSide, Vector3.forward), Is.True, label);
                        }
                        foreach (float phase in new[] { 0f, .35f, .72f, .96f })
                        {
                            motion.SetTime(phase * clip.length);
                            graph.Evaluate(0f);
                            if (cutSide != 0) cut.ApplyPose(phase, false);
                            string motionLabel = cutSide == 0 ? clip.name : cutSide < 0 ? "Hard cut left" : "Hard cut right";
                            string sample = $"{label} / {motionLabel} / {phase}";
                            Assert.That(root.transform.position, Is.EqualTo(gameplayPosition), sample);
                            Assert.That(root.transform.rotation, Is.EqualTo(gameplayRotation), sample);
                            Assert.That(model.transform.localScale, Is.EqualTo(modelScale), sample);
                            for (int i = 0; i < bones.Length; i++)
                                Assert.That(Vector3.Distance(bones[i].localScale, boneScales[i]), Is.LessThan(.0001f), sample);
                            foreach (var skin in skins)
                            {
                                skin.BakeMesh(baked);
                                baked.GetVertices(vertices);
                                Assert.That(vertices.Count, Is.EqualTo(skin.sharedMesh.vertexCount), sample);
                                Assert.That(vertices.TrueForAll(vertex => float.IsFinite(vertex.sqrMagnitude)),
                                    Is.True, $"{sample} / {skin.name}");
                            }
                        }
                    }
                    finally { graph.Destroy(); }
                }
            }
        }
        finally
        {
            Object.Destroy(root);
            Object.Destroy(appearance);
            Object.Destroy(baked);
        }
    }
}
