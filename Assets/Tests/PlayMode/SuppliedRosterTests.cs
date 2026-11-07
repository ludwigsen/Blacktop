using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Source parity and real match consumers use disposable saves, never the owner's roster.
public class SuppliedRosterTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] Fields = { "passing", "speed", "blocking", "agility", "catching", "runPower",
        "carrying", "tackling", "coverage", "dMoves", "swagger", "routeRunning" };
    FrontEndSettings settings;
    string directory, savePath;
    RosterService service;
    float captureDelta;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        FrontEndRuntime.Match = null;
        captureDelta = Time.captureDeltaTime;
        yield return SceneManager.LoadSceneAsync("SampleScene"); yield return null;
        settings = Object.FindAnyObjectByType<MatchBootstrap>().settings;
        directory = Path.Combine(Path.GetTempPath(), "blacktop-supplied-" + Guid.NewGuid());
        savePath = Path.Combine(directory, "rosters.json");
        service = new RosterService(settings, new RosterSaveStore(savePath));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        FrontEndRuntime.Match = null;
        Time.captureDeltaTime = captureDelta;
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Supplied roster cleanup"));
        yield return SceneManager.UnloadSceneAsync(scene);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    static object Call(object o, string m, params object[] args) => o.GetType().GetMethod(m, Private).Invoke(o, args);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);

    [Test]
    public void SixTeamsHaveExactlyTheSuppliedIdsIdentityRolesAndAllTwelveRatings()
    {
        Assert.That(service.Snapshot.teams.Count, Is.EqualTo(6));
        Assert.That(service.Snapshot.teams.Sum(t => t.players.Count), Is.EqualTo(42));
        Assert.That(service.Snapshot.teams.SelectMany(t => t.players).Select(p => p.record.id).Distinct().Count(), Is.EqualTo(42));
        foreach (var team in service.Snapshot.teams)
        {
            Assert.That(service.Readiness(team.teamId), Is.Null);
            Assert.That(MatchLineups.Validate(team), Is.Null);
            var qb = team.players.Single(p => p.record.id == team.active.Single(a => a.offense == TeamMember.RosterSlot.QB).playerId);
            Assert.That(qb.record.passing, Is.EqualTo(team.players.Max(p => p.record.passing)));
        }
#if UNITY_EDITOR
        // Independent comparison against the owner's unchanged CSV, including column order.
        string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Data/Teams/blacktop_42_player_roster.csv"));
        var lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines.Length, Is.EqualTo(43));
        foreach (var line in lines.Skip(1))
        {
            var cells = line.Split(','); var team = service.Team(cells[0].ToLowerInvariant());
            var player = team.players.Single(p => p.record.id == cells[1]).record;
            Assert.That(player.displayName, Is.EqualTo(cells[2]));
            Assert.That(player.jerseyNumber, Is.EqualTo(int.Parse(cells[3])));
            Assert.That(player.archetypeLabel, Is.EqualTo(cells[4] + " / " + cells[5]));
            Assert.That(RosterRatings.Get(player), Is.EqualTo(cells.Skip(6).Select(int.Parse).ToArray()), player.displayName);
        }
#endif
    }

    [UnityTest]
    public IEnumerator EverySuppliedTeamReachesBothMatchSidesWithPrivateRatingsInEveryGameplayConsumer()
    {
        var types = new[] { typeof(PlayerMovement), typeof(PlayerStateMachine), typeof(ReceiverAI),
            typeof(DefenderAI), typeof(AllyBlocker), typeof(GamebreakerController), typeof(CPUCarrierAI) };
        var coveredSides = new HashSet<string>();
        for (int i = 0; i < settings.teams.Length; i++)
        {
            string home = settings.teams[i].id, away = settings.teams[(i + 1) % settings.teams.Length].id;
            Assert.That(QuickPlayMatch.TryHeadToHead(service, home, away, 0, new MatchOptions(),
                new SeededMatchRandom(13), out var match, out var error), Is.True, error);
            FrontEndRuntime.Match = match;
            yield return SceneManager.LoadSceneAsync("SampleScene"); yield return null;
            var privateAttributes = new HashSet<PlayerAttributes>();
            foreach (var applier in Object.FindObjectsByType<CharacterApplier>())
            {
                var record = applier.Record.Value;
                coveredSides.Add(record.id + ":" + applier.GetComponent<TeamMember>().teamId);
                var expected = service.Snapshot.teams.SelectMany(t => t.players).Single(p => p.record.id == record.id).record;
                var attributes = applier.GetComponent<PlayerStateMachine>().Attributes;
                Assert.That(privateAttributes.Add(attributes), Is.True, "No player shares a mutable runtime attribute object");
                for (int stat = 0; stat < Fields.Length; stat++)
                    Assert.That((int)typeof(PlayerAttributes).GetField(Fields[stat]).GetValue(attributes), Is.EqualTo(RosterRatings.Get(expected)[stat]), record.displayName + " / " + Fields[stat]);
                foreach (var type in types)
                {
                    var consumer = applier.GetComponent(type); if (consumer == null) continue;
                    Assert.That(type.GetField("attributes", Private).GetValue(consumer), Is.SameAs(attributes), type.Name + " must consume this player, not a prefab neutral asset");
                }
                Assert.That(applier.transform.Find("Nameplate/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo($"#{record.jerseyNumber} {record.displayName}"));
            }
            Assert.That(privateAttributes.Count, Is.EqualTo(14));
        }
        Assert.That(coveredSides.Count, Is.EqualTo(84), "All 42 supplied people must bind on both physical match sides");
    }

    RosterSaveData OldSave(bool conflict)
    {
        var old = service.Snapshot; old.catalogRevision = 0;
        old.teams.RemoveAll(t => t.teamId != "kings" && t.teamId != "vipers");
        foreach (var team in old.teams)
        {
            var ids = new Dictionary<string, string>();
            for (int i = 0; i < team.players.Count; i++)
            {
                var player = team.players[i]; ids[player.record.id] = player.record.previousId;
                player.record.id = player.record.previousId; player.record.jerseyNumber = 100 + i;
                player.record.passing = 0; // distinguish the retired seed from the supplied values
            }
            foreach (var slot in team.active) slot.playerId = ids[slot.playerId];
        }
        var kings = old.teams.Single(t => t.teamId == "kings");
        kings.players[0].appearance.height = PlayerHeight.Ft6In3;
        var custom = kings.players[0].Copy(); custom.record.id = Guid.NewGuid().ToString();
        custom.record.previousId = null; custom.record.displayName = "Custom Kept";
        custom.record.jerseyNumber = conflict ? settings.Team("kings").roster.players[0].jerseyNumber : 999;
        kings.players.Add(custom); return old;
    }

    [Test]
    public void LegacyCatalogUpgradePreservesCustomPlayersCosmeticsDiskAndSubsequentEdits()
    {
        new RosterSaveStore(savePath).Write(OldSave(false)); string disk = File.ReadAllText(savePath);
        var loaded = new RosterService(settings, new RosterSaveStore(savePath));
        Assert.That(loaded.LoadMessage, Does.Contain("Updated installed"));
        Assert.That(File.ReadAllText(savePath), Is.EqualTo(disk), "Loading never overwrites the owner's save");
        Assert.That(loaded.Snapshot.teams.Sum(t => t.players.Count), Is.EqualTo(43));
        Assert.That(loaded.Team("kings").players.Count(p => p.record.displayName == "Custom Kept"), Is.EqualTo(1));
        var marcus = loaded.Team("kings").players.Single(p => p.record.id == settings.Team("kings").roster.players[0].id);
        Assert.That(marcus.appearance.height, Is.EqualTo(PlayerHeight.Ft6In3));
        Assert.That(marcus.record.passing, Is.EqualTo(17));
        Assert.That(loaded.Snapshot.teams.All(t => loaded.Readiness(t.teamId) == null), Is.True);
        Assert.That(loaded.SaveCurrent(out var error), Is.True, error);
        var assignment = loaded.Team("kings").active.Single(a => a.playerId == marcus.record.id);
        marcus.record.passing = 1;
        Assert.That(loaded.SavePlayer("kings", marcus, (int)assignment.offense, assignment.defense, out _, out error), Is.True, error);
        var restart = new RosterService(settings, new RosterSaveStore(savePath));
        Assert.That(restart.Team("kings").players.Single(p => p.record.id == marcus.record.id).record.passing, Is.EqualTo(1), "Revision is applied once; later author edits remain edits");
    }

    [Test]
    public void JerseyConflictKeepsExistingSaveAndPlayersVisibleUntilResolved()
    {
        new RosterSaveStore(savePath).Write(OldSave(true)); string disk = File.ReadAllText(savePath);
        var loaded = new RosterService(settings, new RosterSaveStore(savePath));
        Assert.That(loaded.LoadMessage, Does.Contain("needs attention"));
        Assert.That(loaded.Team("kings").players.Any(p => p.record.displayName == "Custom Kept"), Is.True);
        Assert.That(File.ReadAllText(savePath), Is.EqualTo(disk));
        var custom = loaded.Team("kings").players.Single(p => p.record.displayName == "Custom Kept");
        custom.record.jerseyNumber = loaded.NextNumber("kings");
        Assert.That(loaded.SavePlayer("kings", custom, -1, 0, out _, out var error), Is.True, error);
        var restart = new RosterService(settings, new RosterSaveStore(savePath));
        Assert.That(restart.LoadMessage, Does.Contain("Updated installed"));
        Assert.That(restart.Team("kings").players.Any(p => p.record.displayName == "Marcus Reed" && p.record.passing == 17), Is.True);
    }

    [UnityTest]
    public IEnumerator RealMovementPassingAndFumbleResistanceReflectSuppliedDifferences()
    {
        Assert.That(QuickPlayMatch.TryHeadToHead(service, "outlaws", "reapers", 0, new MatchOptions(),
            new SeededMatchRandom(9), out var match, out var error), Is.True, error);
        FrontEndRuntime.Match = match; yield return SceneManager.LoadSceneAsync("SampleScene"); yield return null;
        // A batch Editor frame can be far shorter than a controller frame. Keep
        // duration-to-release comparisons deterministic without changing game tuning.
        Time.captureDeltaTime = 1f / 120f; yield return null;
        var play = PlayState.Instance; play.StopAllCoroutines(); play.enabled = false;
        Set(play, "<IsLive>k__BackingField", true);
        var all = Object.FindObjectsByType<CharacterApplier>();
        foreach (var actor in all)
            foreach (var component in actor.GetComponents<MonoBehaviour>())
                if (component is not CharacterApplier && component is not TeamMember) component.enabled = false;
        DefenderControlManager.Instance.enabled = false; BallController.Instance.enabled = false;
        CharacterApplier Named(string name) => all.Single(a => a.Record.Value.displayName == name);
        float Move(string name)
        {
            var actor = Named(name); actor.transform.position = Vector3.up;
            BallController.Instance.AttachTo(actor.transform); PossessionController.ClaimControl(actor.transform, actor.GetComponent<TeamMember>().teamId);
            var movement = actor.GetComponent<PlayerMovement>(); movement.enabled = true;
            Set(movement, "moveInput", Vector2.up);
            for (int i = 0; i < 40; i++) Call(movement, "Update");
            float distance = Vector3.Distance(Vector3.up, actor.transform.position); movement.enabled = false; return distance;
        }
        Assert.That(Move("Dante Holloway"), Is.GreaterThan(Move("Marcus Vaughn")), "20 Speed versus 12 Speed must change real movement");
        var target = Named("Dante Holloway").transform; target.position = new Vector3(0f, 1f, 20f);
        float Throw(string name)
        {
            var qb = Named(name); qb.transform.position = Vector3.up; BallController.Instance.AttachTo(qb.transform);
            Call(qb.GetComponent<CPUCarrierAI>(), "ThrowTo", target);
            return (float)typeof(BallController).GetField("flightDuration", Private).GetValue(BallController.Instance);
        }
        Assert.That(Throw("Devon Cross"), Is.LessThan(Throw("Caleb Mercer")), "19 Passing versus 17 Passing changes the actual committed flight time");
        var tackler = Named("Tyson Grant").transform;
        float Fumble(string name) => (float)Call(Named(name).GetComponent<TackleContact>(), "CalculateFumbleChance", tackler);
        Assert.That(Fumble("Rico Bennett"), Is.LessThan(Fumble("Dante Holloway")), "17 Carrying versus 11 Carrying changes the real fumble calculation");
        float ContactRecovery(string name)
        {
            var actor = Named(name); actor.transform.position = Vector3.up;
            var opponent = Named("Tyson Grant"); opponent.transform.position = new Vector3(.4f, 1f, 0f);
            var blocker = actor.GetComponent<AllyBlocker>(); blocker.SetBlockingTarget(opponent.transform);
            BallController.Instance.AttachTo(Named("Caleb Mercer").transform);
            Physics.SyncTransforms(); Call(blocker, "Update");
            return (float)typeof(AllyBlocker).GetField("contactCooldownTimer", Private).GetValue(blocker);
        }
        Assert.That(ContactRecovery("Marcus Vaughn"), Is.LessThan(ContactRecovery("Dante Holloway")), "19 Blocking versus 5 Blocking changes actual contact recovery");
        BallController.Instance.AttachTo(Named("Devon Cross").transform); // both Outlaws now defend
        int BlockRecovery(string name)
        {
            var defender = Named(name).GetComponent<DefenderAI>();
            Set(defender, "shedTimer", 0f); defender.ApplyBlockPushBack(Vector3.right, .8f);
            int frames = 0;
            while (defender.IsStunned && frames < 200) { Call(defender, "Update"); frames++; }
            Assert.That(frames, Is.LessThan(200)); return frames;
        }
        Assert.That(BlockRecovery("Dante Holloway"), Is.LessThan(BlockRecovery("Marcus Vaughn")), "18 D-Moves versus 8 D-Moves releases the actual block recovery sooner");
    }
}
