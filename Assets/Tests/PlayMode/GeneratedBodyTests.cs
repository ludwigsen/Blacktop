using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Real skins and installed clips cover rig continuity at every supported height, without inventing missing action coverage.
public class GeneratedBodyTests
{
    TeamMember actor;
    FrontEndSettings settings;
    PlayerVisualAssembler assembler;
    PlayerAppearanceDefinition definition;
    static readonly string[] Names = { "Lean", "Athletic", "Built", "Thick", "Heavy", "Massive" };

    [UnitySetUp]
    public IEnumerator Setup()
    {
        FrontEndRuntime.Match = null;
        yield return SceneManager.LoadSceneAsync("SampleScene"); yield return null;
        settings = Object.FindAnyObjectByType<MatchBootstrap>().settings;
        actor = Object.FindObjectsByType<TeamMember>()[0];
        PlayState.Instance.StopAllCoroutines(); PlayState.Instance.enabled = false;
        foreach (var member in Object.FindObjectsByType<TeamMember>())
            foreach (var behaviour in member.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
        assembler = actor.GetComponent<PlayerVisualAssembler>(); assembler.catalog = settings.appearances;
        definition = Object.Instantiate(settings.defaultAppearance);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Object.DestroyImmediate(definition);
        var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("Generated body cleanup"));
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    void Select(string name)
    {
        definition.bodyArchetype = (BodyArchetype)Enum.Parse(typeof(BodyArchetype), name);
        definition.bodyId = "body." + name.ToLowerInvariant() + ".parametric.v1";
        definition.faceId = "face.prototype.parametric.v1";
        definition.clothingIds = new[] { "top", "shorts", "footwear" }.Select(s => "gear." + s + "." + name.ToLowerInvariant() + ".parametric.v1").ToArray();
    }

    [Test]
    public void AllSixBodiesTwelveHeightsAndInstalledClipsPreserveSeamGripAndGameplay()
    {
        var collider = actor.GetComponent<CapsuleCollider>(); var capsule = (collider.center, collider.height, collider.radius);
        var position = actor.transform.position; var scale = actor.transform.localScale;
        string ratings = JsonUtility.ToJson(actor.GetComponent<PlayerStateMachine>().Attributes);
        var record = actor.GetComponent<CharacterApplier>().Record; var carrier = BallController.Instance.Carrier;
        var control = PossessionController.ActivelyControlled;
        var rig = assembler.rig; var clips = rig.animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
        Assert.That(clips.Length, Is.EqualTo(5), "Update coverage explicitly when new action clips are installed.");
        var bakedBody = new Mesh(); var bakedHead = new Mesh(); var bakedGarment = new Mesh();
        try
        {
            foreach (string name in Names)
            foreach (var height in settings.appearances.heightProfiles)
            {
                Select(name); definition.heightProfile = height;
                Assert.That(assembler.TryApply(definition), Is.True, assembler.LastDiagnostics);
                Assert.That(rig.animator.avatar, Is.SameAs(rig.skeleton.referenceModel.GetComponentInChildren<Animator>(true).avatar));
                var head = actor.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s => s.name == definition.faceId);
                Assert.That(rig.integratedHeadRenderers.All(r => r.enabled), Is.True, "Prototype eyes must remain visible with detached fallback head.");
                Assert.That(rig.bodyRenderer.bones.SequenceEqual(head.bones), Is.True);
                var bodyVertices = rig.bodyRenderer.sharedMesh.vertices; var headVertices = head.sharedMesh.vertices;
                var ring = bodyVertices.Select((p, i) => (p, i)).Where(v => Mathf.Abs(v.p.y - 1.515f) < .000001f)
                    .Select(v => (body: v.i, head: Array.FindIndex(headVertices, p => Vector3.Distance(p, v.p) < .000001f))).ToArray();
                Assert.That(ring.Length, Is.GreaterThan(70)); Assert.That(ring.All(p => p.head >= 0), Is.True);
                foreach (var clip in clips)
                foreach (float fraction in new[] { 0f, .35f, .7f })
                {
                    rig.animator.Rebind(); clip.SampleAnimation(rig.animator.gameObject, fraction * clip.length);
                    var leftHand = rig.animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                    var rightHand = rig.animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                    rig.bodyRenderer.BakeMesh(bakedBody); head.BakeMesh(bakedHead);
                    var body = bakedBody.vertices; var face = bakedHead.vertices;
                    Assert.That(body.All(Finite), Is.True, name + " / " + height.Centimeters + " / " + clip.name);
                    Assert.That(bakedBody.bounds.size.magnitude, Is.InRange(.5f, 5f));
                    foreach (var pair in ring) Assert.That(Vector3.Distance(body[pair.body], face[pair.head]), Is.LessThan(.00002f), "Animated neck seam separated.");
                    foreach (var garment in actor.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.name.StartsWith("gear.", StringComparison.Ordinal)))
                    {
                        garment.BakeMesh(bakedGarment); Assert.That(bakedGarment.vertices.All(Finite), Is.True);
                    }
                    // Swapping surface meshes cannot alter the sampled hand/ball grip anchors.
                    var generated = rig.bodyRenderer.sharedMesh; rig.bodyRenderer.sharedMesh = rig.skeleton.referenceSkin.sharedMesh;
                    Assert.That(rig.animator.GetBoneTransform(HumanBodyBones.LeftHand).position, Is.EqualTo(leftHand));
                    Assert.That(rig.animator.GetBoneTransform(HumanBodyBones.RightHand).position, Is.EqualTo(rightHand));
                    rig.bodyRenderer.sharedMesh = generated;
                }
                Assert.That(actor.transform.position, Is.EqualTo(position)); Assert.That(actor.transform.localScale, Is.EqualTo(scale));
                Assert.That((collider.center, collider.height, collider.radius), Is.EqualTo(capsule));
                Assert.That(rig.transform.localScale.y, Is.EqualTo(height.Centimeters / 183f).Within(.0001f));
            }
        }
        finally { Object.DestroyImmediate(bakedBody); Object.DestroyImmediate(bakedHead); Object.DestroyImmediate(bakedGarment); }
        Assert.That(JsonUtility.ToJson(actor.GetComponent<PlayerStateMachine>().Attributes), Is.EqualTo(ratings));
        Assert.That(actor.GetComponent<CharacterApplier>().Record, Is.EqualTo(record));
        Assert.That(BallController.Instance.Carrier, Is.SameAs(carrier)); Assert.That(PossessionController.ActivelyControlled, Is.SameAs(control));
    }

    [Test]
    public void LegacyIdsAndGeneratedSavesRoundTripWithoutSilentMigrationOrCrossFit()
    {
        foreach (string name in new[] { "Lean", "Athletic", "Thick", "Heavy", "Massive" })
        {
            definition.bodyArchetype = (BodyArchetype)Enum.Parse(typeof(BodyArchetype), name);
            definition.bodyId = "body." + name.ToLowerInvariant() + ".temporary"; definition.faceId = "face.prototype.a";
            definition.clothingIds = new[] { "gear.top." + name.ToLowerInvariant() + ".temporary" };
            var selection = JsonUtility.FromJson<AppearanceSelection>(JsonUtility.ToJson(AppearanceSelection.From(definition)));
            selection.Apply(definition, settings.appearances, "legacy");
            Assert.That(assembler.TryApply(definition), Is.True, assembler.LastDiagnostics);
            Assert.That(definition.bodyId.EndsWith(".temporary", StringComparison.Ordinal), Is.True);
        }
        Select("Built");
        var saved = JsonUtility.FromJson<AppearanceSelection>(JsonUtility.ToJson(AppearanceSelection.From(definition)));
        saved.Apply(definition, settings.appearances, "built"); Assert.That(assembler.TryApply(definition), Is.True, assembler.LastDiagnostics);
        Assert.That((int)BodyArchetype.Built, Is.EqualTo(5)); Assert.That((int)BodyArchetype.Massive, Is.EqualTo(4));
        Assert.That(settings.weights.Pounds(PlayerHeight.Ft6In0, BodyArchetype.Built), Is.EqualTo(205));
        var before = assembler.rig.bodyRenderer.sharedMesh;
        definition.clothingIds = new[] { "gear.top.athletic.temporary" };
        Assert.That(assembler.TryApply(definition), Is.False); Assert.That(assembler.rig.bodyRenderer.sharedMesh, Is.SameAs(before));
    }
    static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
}
