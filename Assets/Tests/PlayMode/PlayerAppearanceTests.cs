using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Real match actors verify that appearance remains separate from football state and geometry.
public class PlayerAppearanceTests
{
    TeamMember[] actors;

    [UnitySetUp]
    public IEnumerator LoadMatch()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        actors = Object.FindObjectsByType<TeamMember>();
    }

    [UnityTearDown]
    public IEnumerator LeaveMatch()
    {
        var match = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Appearance cleanup"));
        yield return SceneManager.UnloadSceneAsync(match);
    }

    void Freeze()
    {
        PlayState.Instance.StopAllCoroutines();
        PlayState.Instance.enabled = false;
        foreach (var actor in actors)
            foreach (var behaviour in actor.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
    }

    [Test]
    public void ShippedCatalogAndFourteenPlayersUseSharedAssetsWithoutMaterialClones()
    {
        Assert.That(actors.Length, Is.EqualTo(14));
        var first = actors[0].GetComponent<PlayerVisualAssembler>();
        Assert.That(first.catalog.ValidateCatalog(), Is.Empty);
        Assert.That(first.catalog.heightProfiles.Length, Is.EqualTo(12));
        Assert.That(first.catalog.sampleAppearances.Length, Is.EqualTo(3));
        var materials = first.rig.bodyRenderer.sharedMaterials;
        foreach (var actor in actors)
        {
            var assembler = actor.GetComponent<PlayerVisualAssembler>();
            Assert.That(assembler.AppliedAppearance, Is.Not.Null, assembler.LastDiagnostics);
            Assert.That(assembler.HasPlaceholders, Is.True, "Faces are explicit slots, not authored art");
            Assert.That(assembler.rig.animator.avatar, Is.SameAs(first.rig.animator.avatar));
            Assert.That(assembler.rig.bodyRenderer.sharedMaterials, Is.EqualTo(materials));
            Assert.That(actor.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        }
        Assert.That(actors.Sum(a => a.GetComponentsInChildren<SkinnedMeshRenderer>().Length), Is.EqualTo(70));
    }

    [Test]
    public void AllSixtyHeightBuildSelectionsLeaveGameplayAndGuidRatingsUntouched()
    {
        Freeze();
        var actor = actors[0];
        var assembler = actor.GetComponent<PlayerVisualAssembler>();
        var definition = Object.Instantiate(assembler.defaultAppearance);
        var collider = actor.GetComponent<CapsuleCollider>();
        var rootPosition = actor.transform.position;
        var rootScale = actor.transform.localScale;
        var originalCollider = (collider.center, collider.height, collider.radius);
        var machine = actor.GetComponent<PlayerStateMachine>();
        string ratings = JsonUtility.ToJson(machine.Attributes);
        var record = actor.GetComponent<CharacterApplier>().Record;
        var carrier = BallController.Instance.Carrier;
        int possession = PlayState.Instance.PossessionTeamId;
        var bounds = Object.FindAnyObjectByType<FieldBounds>();
        foreach (BodyArchetype build in Enum.GetValues(typeof(BodyArchetype)))
        foreach (var profile in assembler.catalog.heightProfiles)
        {
            definition.bodyArchetype = build;
            definition.bodyId = "body." + build.ToString().ToLowerInvariant() + ".prototype";
            definition.heightProfile = profile;
            Assert.That(assembler.TryApply(definition), Is.True, assembler.LastDiagnostics);
            Assert.That(actor.transform.position, Is.EqualTo(rootPosition));
            Assert.That(actor.transform.localScale, Is.EqualTo(rootScale));
            Assert.That((collider.center, collider.height, collider.radius), Is.EqualTo(originalCollider));
            // The fitted body stands around y=-1 relative to the centered gameplay root.
            float ratio = profile.Centimeters / 183f;
            Assert.That(assembler.rig.transform.localScale.y, Is.EqualTo(ratio).Within(0.0001f));
            Assert.That(assembler.rig.transform.localPosition.y - ratio, Is.EqualTo(-1f).Within(0.0001f));
            assembler.rig.animator.Update(0.02f);
            Assert.That(assembler.rig.animator.GetBoneTransform(HumanBodyBones.Head), Is.Not.Null);
        }
        Assert.That(JsonUtility.ToJson(machine.Attributes), Is.EqualTo(ratings));
        Assert.That(actor.GetComponent<CharacterApplier>().Record, Is.EqualTo(record));
        Assert.That(BallController.Instance.Carrier, Is.SameAs(carrier));
        Assert.That(PlayState.Instance.PossessionTeamId, Is.EqualTo(possession));
        Assert.That(Object.FindAnyObjectByType<FieldBounds>(), Is.SameAs(bounds));
        definition.headScale = 1.04f;
        Assert.That(assembler.TryApply(definition), Is.True, assembler.LastDiagnostics);
        assembler.rig.animator.Update(0.02f);
        Assert.That(assembler.rig.animator.GetBoneTransform(HumanBodyBones.Head).localScale.x, Is.EqualTo(1.04f).Within(0.0001f));
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void ExplicitIdsSurviveSerializationReorderingAndRepeatedAssembly()
    {
        var assembler = actors[0].GetComponent<PlayerVisualAssembler>();
        var saved = Object.Instantiate(assembler.defaultAppearance);
        string json = JsonUtility.ToJson(saved);
        saved.faceId = "face.prototype.c";
        JsonUtility.FromJsonOverwrite(json, saved);
        var catalog = Object.Instantiate(assembler.catalog);
        Array.Reverse(catalog.parts);
        assembler.catalog = catalog;
        var mesh = assembler.rig.bodyRenderer.sharedMesh;
        int transforms = assembler.rig.GetComponentsInChildren<Transform>(true).Length;
        for (int repeat = 0; repeat < 10; repeat++) Assert.That(assembler.TryApply(saved), Is.True, assembler.LastDiagnostics);
        Assert.That(saved.faceId, Is.EqualTo(assembler.defaultAppearance.faceId));
        Assert.That(assembler.rig.bodyRenderer.sharedMesh, Is.SameAs(mesh));
        Assert.That(assembler.rig.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(transforms));
        Object.DestroyImmediate(saved);
        Object.DestroyImmediate(catalog);
    }

    [Test]
    public void MissingDuplicateAndIncompatibleSelectionsRetainTheLastVisual()
    {
        var assembler = actors[0].GetComponent<PlayerVisualAssembler>();
        var saved = Object.Instantiate(assembler.defaultAppearance);
        var previous = assembler.AppliedAppearance;
        var scale = assembler.rig.transform.localScale;
        saved.faceId = "missing.face";
        Assert.That(assembler.TryApply(saved), Is.False);
        Assert.That(assembler.LastDiagnostics, Does.Contain("missing.face"));
        Assert.That(assembler.AppliedAppearance, Is.SameAs(previous));
        Assert.That(assembler.rig.transform.localScale, Is.EqualTo(scale));
        saved.faceId = previous.faceId;
        var catalog = Object.Instantiate(assembler.catalog);
        catalog.parts = catalog.parts.Concat(new[] { catalog.parts[0] }).ToArray();
        assembler.catalog = catalog;
        Assert.That(assembler.TryApply(saved), Is.False);
        Assert.That(assembler.LastDiagnostics, Does.Contain("duplicate"));
        assembler.catalog = actors[1].GetComponent<PlayerVisualAssembler>().catalog;
        var face = Object.Instantiate(assembler.catalog.Find(saved.faceId));
        face.placeholder = false;
        face.emptyOption = true;
        face.compatibleFaceIds = new[] { "another.face" };
        catalog.parts = assembler.catalog.parts.Select(p => p.id == face.id ? face : p).ToArray();
        assembler.catalog = catalog;
        Assert.That(assembler.TryApply(saved), Is.False);
        Assert.That(assembler.LastDiagnostics, Does.Contain("does not fit"));
        Object.DestroyImmediate(face);
        Object.DestroyImmediate(catalog);
        Object.DestroyImmediate(saved);
    }

    [Test]
    public void SkinValidationRejectsChangedBindPosesEvenWithIdenticalBoneNames()
    {
        var contract = actors[0].GetComponent<PlayerVisualAssembler>().rig.skeleton;
        Assert.That(contract.ValidateSkin(contract.referenceSkin, out string validError), Is.True, validError);
        var model = Object.Instantiate(contract.referenceModel);
        var skin = model.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(r => r.sharedMesh.vertexCount).First();
        var mesh = Object.Instantiate(skin.sharedMesh);
        var binds = mesh.bindposes;
        binds[0].m03 += 0.1f;
        mesh.bindposes = binds;
        skin.sharedMesh = mesh;
        Assert.That(contract.ValidateSkin(skin, out string error), Is.False);
        Assert.That(error, Does.Contain("bind pose"));
        Object.DestroyImmediate(model);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void AttachmentFixturesShareLiveBonesAndRefreshTeamColorsWithoutRebuilding()
    {
        Freeze();
        var assembler = actors[0].GetComponent<PlayerVisualAssembler>();
        var catalog = Object.Instantiate(assembler.catalog);
        var saved = Object.Instantiate(assembler.defaultAppearance);
        var hair = ScriptableObject.CreateInstance<CharacterPartDefinition>();
        hair.id = "test.skinned.attachment";
        hair.slot = CharacterPartSlot.Hair;
        hair.skeleton = assembler.rig.skeleton;
        // Existing eye geometry is a test fixture for skin binding, never shipped as hairstyle art.
        hair.skinnedSource = hair.skeleton.referenceModel.GetComponentsInChildren<SkinnedMeshRenderer>()
            .First(r => r != hair.skeleton.referenceSkin);
        hair.materialTints = new[] { CharacterTint.Hair };
        hair.lowerDetailLevels = new[] { new CharacterSkinLod { source = hair.skinnedSource, screenHeight = 0.1f } };
        var accessory = ScriptableObject.CreateInstance<CharacterPartDefinition>();
        accessory.id = "test.rigid.attachment";
        accessory.slot = CharacterPartSlot.Wristband;
        accessory.skeleton = hair.skeleton;
        accessory.socket = HumanBodyBones.LeftHand;
        var fixture = new GameObject("Attachment fixture", typeof(MeshFilter), typeof(MeshRenderer));
        fixture.GetComponent<MeshFilter>().sharedMesh = hair.skinnedSource.sharedMesh;
        fixture.GetComponent<MeshRenderer>().sharedMaterials = hair.skinnedSource.sharedMaterials;
        accessory.attachmentPrefab = fixture;
        accessory.materialTints = new[] { CharacterTint.TeamPrimary };
        catalog.parts = catalog.parts.Concat(new[] { hair, accessory }).ToArray();
        assembler.catalog = catalog;
        saved.hairId = hair.id;
        saved.accessoryIds = new[] { accessory.id };
        var team = ScriptableObject.CreateInstance<TeamIdentity>();
        team.accentColor = Color.red;
        Assert.That(assembler.TryApply(saved, team, 12), Is.True, assembler.LastDiagnostics);
        var skin = assembler.rig.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name == hair.id);
        Assert.That(skin.bones[0], Is.SameAs(assembler.rig.animator.GetBoneTransform(HumanBodyBones.Head)));
        Assert.That(skin.GetComponent<LODGroup>().GetLODs().Length, Is.EqualTo(2));
        var rigid = assembler.rig.GetComponentsInChildren<MeshRenderer>().First(r => r.name == accessory.id);
        Assert.That(rigid.transform.parent, Is.SameAs(assembler.rig.animator.GetBoneTransform(HumanBodyBones.LeftHand)));
        var material = rigid.sharedMaterial;
        var propertyBlock = new MaterialPropertyBlock();
        rigid.GetPropertyBlock(propertyBlock, 0);
        Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(Color.red));
        team.accentColor = Color.blue;
        assembler.ApplyTeam(team, 99);
        rigid.GetPropertyBlock(propertyBlock, 0);
        Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(Color.blue));
        Assert.That(rigid.sharedMaterial, Is.SameAs(material));
        Assert.That(assembler.TryApply(saved, team, 99), Is.True);
        Assert.That(assembler.rig.GetComponentsInChildren<MeshRenderer>().First(r => r.name == accessory.id), Is.SameAs(rigid));
        Object.DestroyImmediate(fixture);
        Object.DestroyImmediate(team);
        Object.DestroyImmediate(hair);
        Object.DestroyImmediate(accessory);
        Object.DestroyImmediate(saved);
        Object.DestroyImmediate(catalog);
    }

    [UnityTest]
    public IEnumerator RosterAppearanceResolvesAtSpawnWithoutChangingPlayerIdentity()
    {
        Freeze();
        var source = actors.First(a => a.GetComponent<CharacterApplier>().Record.HasValue);
        var record = source.GetComponent<CharacterApplier>().Record.Value;
        record.appearance = source.GetComponent<PlayerVisualAssembler>().catalog.sampleAppearances[0];
        var roster = ScriptableObject.CreateInstance<TeamRoster>();
        roster.players.Add(record);
        source.gameObject.SetActive(false);
        var clone = Object.Instantiate(source.gameObject);
        source.gameObject.SetActive(true);
        var applier = clone.GetComponent<CharacterApplier>();
        typeof(CharacterApplier).GetField("roster", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(applier, roster);
        clone.SetActive(true);
        clone.GetComponent<PlayerVisualAssembler>().enabled = true;
        yield return null;
        Assert.That(applier.Record.Value.id, Is.EqualTo(record.id));
        Assert.That(applier.Record.Value.speed, Is.EqualTo(record.speed));
        Assert.That(clone.GetComponent<PlayerVisualAssembler>().AppliedAppearance, Is.SameAs(record.appearance));
        Object.Destroy(clone);
        Object.Destroy(roster);
    }
}
