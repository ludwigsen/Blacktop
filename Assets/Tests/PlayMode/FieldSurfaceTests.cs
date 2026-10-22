using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Real scene regressions: visuals must never become a second gameplay/condition system.
public class FieldSurfaceTests
{
    FieldLoader loader;
    const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [UnitySetUp]
    public IEnumerator LoadMatch()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        loader = Object.FindAnyObjectByType<FieldLoader>();
        Assert.That(loader, Is.Not.Null);
    }

    [UnityTearDown]
    public IEnumerator LeaveMatch()
    {
        Scene match = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Field test cleanup"));
        yield return SceneManager.UnloadSceneAsync(match);
    }

    [UnityTest]
    public IEnumerator BothVenuesKeepBoundsCollidersCoordinatesAndMaterialAssets()
    {
        int colliders = Object.FindObjectsByType<Collider>().Length;
        var ball = BallController.Instance;
        Transform holder = ball.Carrier;
        var rootPosition = holder != null ? holder.position : Vector3.zero;
        float los = PlayState.Instance.CurrentLineOfScrimmageZ;
        Assert.That(loader.AvailableFields.Count, Is.EqualTo(2));
        foreach (var field in loader.AvailableFields)
        {
            Color savedColor = field.baseMaterial.GetColor("_BaseColor");
            Assert.That(loader.SelectField(field), Is.True);
            Assert.That(loader.LoadedField, Is.SameAs(field));
            Assert.That(Object.FindObjectsByType<FieldSurface>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<FieldBounds>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Collider>().Length, Is.EqualTo(colliders));
            Assert.That(loader.ActiveSurface.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(loader.ActiveSurface.SurfaceRenderer.sharedMaterial, Is.SameAs(field.baseMaterial));
            Assert.That(field.baseMaterial.GetColor("_BaseColor"), Is.EqualTo(savedColor));
            Assert.That(ball.Carrier, Is.SameAs(holder));
            if (holder != null) Assert.That(holder.position, Is.EqualTo(rootPosition));
            Assert.That(PlayState.Instance.CurrentLineOfScrimmageZ, Is.EqualTo(los));
            var bounds = loader.Bounds;
            Assert.That(bounds.PlayableWidth, Is.EqualTo(FieldConstants.HalfWidth * 2f));
            Assert.That(bounds.PlayableLength, Is.EqualTo(FieldConstants.PlayLength));
            Assert.That(bounds.EndZoneDepth, Is.EqualTo(FieldConstants.EndZoneDepth));
            Assert.That(bounds.NearGoalLineLocalZ, Is.EqualTo(FieldConstants.NearGoalLineZ));
            Assert.That(bounds.FarGoalLineLocalZ, Is.EqualTo(FieldConstants.FarGoalLineZ));
            Assert.That(bounds.GetEndZone(new Vector3(0f, 1f, FieldConstants.NearGoalLineZ - 1f)), Is.EqualTo(FieldEndZone.Near));
            Assert.That(bounds.GetEndZone(new Vector3(0f, 1f, FieldConstants.FarGoalLineZ + 1f)), Is.EqualTo(FieldEndZone.Far));
            Assert.That(bounds.GetEndZone(new Vector3(FieldConstants.HalfWidth + 1f, 1f, FieldConstants.FarGoalLineZ + 1f)), Is.EqualTo(FieldEndZone.None));
            Assert.That(bounds.GetState(new Vector3(FieldConstants.HalfWidth + 1f, 0f, 0f)), Is.EqualTo(FieldBoundsState.InSafetyBand));
            Assert.That(bounds.GetState(new Vector3(bounds.HalfSafetyWidth + 1f, 0f, 0f)), Is.EqualTo(FieldBoundsState.OutOfBounds));
            var mesh = loader.ActiveSurface.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.bounds.size, Is.EqualTo(new Vector3(bounds.PlayableWidth, 0f, bounds.PlayableLength)));
        }
        yield return null; // Deferred old visuals must also be gone after the frame ends.
        Assert.That(Object.FindObjectsByType<FieldSurface>().Length, Is.EqualTo(1));
    }

    [Test]
    public void TintAndTilingUsePropertyBlockWithoutMaterialCopies()
    {
        var definition = Object.Instantiate(loader.SelectedField);
        definition.useTint = true;
        definition.tint = new Color(0.7f, 0.8f, 0.9f);
        definition.textureTiling = new Vector2(3f, 7f);
        Assert.That(loader.SelectField(definition), Is.True);
        var block = new MaterialPropertyBlock();
        loader.ActiveSurface.SurfaceRenderer.GetPropertyBlock(block);
        Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(definition.baseMaterial.GetColor("_BaseColor") * definition.tint));
        Assert.That(block.GetVector("_BaseMap_ST"), Is.EqualTo(new Vector4(3f, 7f, 0f, 0f)));
        Assert.That(loader.ActiveSurface.SurfaceRenderer.sharedMaterial, Is.SameAs(definition.baseMaterial));
        loader.SelectField(null);
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void MissingOptionalArtAndNullSelectionUseSafeDefaults()
    {
        var sparse = ScriptableObject.CreateInstance<FieldDefinition>();
        sparse.surfacePrefab = loader.SelectedField.surfacePrefab;
        Assert.That(loader.SelectField(sparse), Is.True);
        Assert.That(loader.ActiveSurface.SurfaceRenderer.sharedMaterial, Is.Not.Null);
        Assert.That(loader.SelectField(null), Is.True);
        Assert.That(loader.LoadedField.venueId, Is.EqualTo("the-lot"));
        var invalid = ScriptableObject.CreateInstance<FieldDefinition>();
        var previous = loader.ActiveSurface;
        Assert.That(loader.SelectField(invalid), Is.False);
        Assert.That(loader.ActiveSurface, Is.SameAs(previous));
        Object.DestroyImmediate(invalid);
        Object.DestroyImmediate(sparse);
    }

    [Test]
    public void DecorativeCollidersAndExtraBoundsAreRejected()
    {
        var definition = Object.Instantiate(loader.SelectedField);
        var decoration = new GameObject("Invalid decoration");
        decoration.SetActive(false);
        decoration.AddComponent<BoxCollider>();
        definition.detailPrefab = decoration;
        LogAssert.Expect(LogType.Error, "Field art must contain only renderers/transforms and FieldSurface; no gameplay or colliders.");
        Assert.That(loader.SelectField(definition), Is.False);
        Object.DestroyImmediate(decoration);
        Object.DestroyImmediate(definition);
        var duplicate = new GameObject("Duplicate bounds");
        duplicate.AddComponent<FieldBounds>();
        LogAssert.Expect(LogType.Error, "FieldLoader requires one active FieldBounds at world identity.");
        Assert.That(loader.ApplySelectedField(), Is.False);
        Object.DestroyImmediate(duplicate);
    }

    [TestCase(0, 0)]
    [TestCase(0, 1)]
    [TestCase(1, 0)]
    [TestCase(1, 1)]
    public void EitherVenueScoresHeldBallForEitherTeam(int venue, int team)
    {
        Assert.That(loader.SelectField(loader.AvailableFields[venue]), Is.True);
        TeamMember scorer = null;
        foreach (var member in Object.FindObjectsByType<TeamMember>())
            if (member.teamId == team && member.slot == TeamMember.RosterSlot.RB) scorer = member;
        Assert.That(scorer, Is.Not.Null);
        var play = PlayState.Instance;
        var ball = BallController.Instance;
        typeof(BallController).GetField("carryOffset", PrivateInstance).SetValue(ball, Vector3.zero);
        var direction = play.DirectionFor(team);
        scorer.transform.position = new Vector3(0f, 1f, direction.Advance(direction.TargetGoalLineZ, 1f));
        ball.AttachTo(scorer.transform);
        typeof(PlayState).GetField("<IsLive>k__BackingField", PrivateInstance).SetValue(play, true);
        Assert.That(play.TryGetTouchdownTeam(out int actualTeam), Is.True);
        Assert.That(actualTeam, Is.EqualTo(team));
        play.EndPlay(PlayState.PlayEndReason.Touchdown);
        play.EndPlay(PlayState.PlayEndReason.Touchdown); // Repeated terminal requests must not double-score.
        Assert.That(team == 0 ? ScoreBoard.Instance.Team1Score : ScoreBoard.Instance.Team2Score, Is.EqualTo(6));
        Assert.That(play.PossessionTeamId, Is.EqualTo(PlayState.OpponentOf(team)));
    }

    [Test]
    public void SwitchingDuringLivePlayIsRejected()
    {
        var play = PlayState.Instance;
        typeof(PlayState).GetField("<IsLive>k__BackingField", PrivateInstance).SetValue(play, true);
        var previous = loader.ActiveSurface;
        Assert.That(loader.SelectField(loader.AvailableFields[1]), Is.False);
        Assert.That(loader.ActiveSurface, Is.SameAs(previous));
        typeof(PlayState).GetField("<IsLive>k__BackingField", PrivateInstance).SetValue(play, false);
    }

    [Test]
    public void ClearAndVenueChangesLeaveEveryStatUnmodified()
    {
        var clear = ScriptableObject.CreateInstance<ConditionData>();
        var stats = ScriptableObject.CreateInstance<PlayerAttributes>();
        foreach (ConditionSeverity severity in System.Enum.GetValues(typeof(ConditionSeverity)))
            Assert.That(clear.GetEffectiveModifiers(severity), Is.Empty);
        foreach (var field in loader.AvailableFields)
        {
            Assert.That(loader.SelectField(field), Is.True);
            foreach (AttributeStat stat in System.Enum.GetValues(typeof(AttributeStat)))
                Assert.That(stats.Effective(stat, 10), Is.EqualTo(1f));
        }
        Object.DestroyImmediate(stats);
        Object.DestroyImmediate(clear);
    }

    [UnityTest]
    public IEnumerator GameplayOverlaysStayAboveDecorationAndFollowLOS()
    {
        var lines = Object.FindAnyObjectByType<FieldLines>();
        var los = (Transform)typeof(FieldLines).GetField("losLine", PrivateInstance).GetValue(lines);
        var first = (Transform)typeof(FieldLines).GetField("firstDownLine", PrivateInstance).GetValue(lines);
        foreach (var field in loader.AvailableFields)
        {
            Assert.That(loader.SelectField(field), Is.True);
            yield return null;
            Assert.That(los.localPosition.y, Is.GreaterThan(0.008f));
            Assert.That(first.localPosition.y, Is.GreaterThan(0.008f));
            Assert.That(los.position.z, Is.EqualTo(PlayState.Instance.CurrentLineOfScrimmageZ).Within(0.001f));
            Assert.That(first.position.z, Is.EqualTo(PlayState.Instance.AttackDirection.Advance(los.position.z, DownsTracker.Instance.YardsToGo)).Within(0.001f));
            Assert.That(los.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(los.GetComponent<MeshRenderer>().sharedMaterial.shader.isSupported, Is.True);
            foreach (var renderer in loader.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.transform == los || renderer.transform == first || renderer.name.Contains("Goal Line")) continue;
                Assert.That(renderer.bounds.max.y, Is.LessThan(los.position.y));
            }
        }
    }
}
