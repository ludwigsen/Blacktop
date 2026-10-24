using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Every test uses a disposable save; canonical rosters and the developer's save are never written.
public class ExhibitionTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    FrontEndSettings settings;
    RosterService service;
    string directory,path;
    [UnitySetUp]
    public IEnumerator Setup()
    {
        FrontEndRuntime.Match=null;yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;
        settings=Object.FindAnyObjectByType<MatchBootstrap>().settings;
        directory=Path.Combine(Path.GetTempPath(),"blacktop-exhibition-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        path=Path.Combine(directory,"rosters.json");service=new RosterService(settings,new RosterSaveStore(path));
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        FrontEndRuntime.Match=null;typeof(FrontEndRuntime).GetProperty("Rosters").SetValue(null,null);
        var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Exhibition cleanup"));
        yield return SceneManager.UnloadSceneAsync(old);Directory.Delete(directory,true);
    }
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    static object Call(object target,string method,params object[] values)=>target.GetType().GetMethod(method,Private).Invoke(target,values);
    QuickPlayMatch Head(int side=0,MatchOptions options=null)
    {
        Assert.That(QuickPlayMatch.TryHeadToHead(service,"kings","vipers",side,options??new MatchOptions(),new SeededMatchRandom(17),out var match,out var error),Is.True,error);return match;
    }
    PickupDraft MixedDraft()
    {
        var draft=new PickupDraft(service);
        foreach(var player in draft.Pool.Where(p=>p.SourceTeamId=="kings").Take(4).Concat(draft.Pool.Where(p=>p.SourceTeamId=="vipers").Take(3)))
            Assert.That(draft.Add(player.Id,out var error),Is.True,error);
        return draft;
    }
    QuickPlayMatch Pickup(int side=0,MatchOptions options=null)
    {
        var draft=MixedDraft();Assert.That(QuickPlayMatch.TryPickup(service,draft.Selected,draft.SuggestHuman(),side,options??new MatchOptions(),
            new SeededMatchRandom(51),out var match,out var error),Is.True,error);return match;
    }
    [TestCase(0)] [TestCase(1)]
    public void HeadSetupPreservesSavedIdentityAndIndependentAssignments(int side)
    {
        var match=Head(side);var data=match.Snapshot;Assert.That(match.HumanSide,Is.EqualTo(side));
        foreach(var squad in data.teams)
        {
            Assert.That(MatchLineups.Validate(squad),Is.Null);
            Assert.That(JsonUtility.ToJson(squad),Is.EqualTo(JsonUtility.ToJson(service.Team(squad.teamId))));
            foreach(var p in squad.players)Assert.That(match.CurvesFor(p.record.id),Is.SameAs(settings.Team(squad.teamId).roster.curves));
        }
        data.teams[0].players.Clear();Assert.That(match.Snapshot.teams[0].players.Count,Is.GreaterThanOrEqualTo(7));
    }
    [Test]
    public void SameTeamAndShortLineupAreRejectedWithoutAutofill()
    {
        Assert.That(QuickPlayMatch.TryHeadToHead(service,"kings","kings",0,new MatchOptions(),new SeededMatchRandom(0),out _,out var error),Is.False);
        Assert.That(error,Does.Contain("different"));var player=service.Team("kings").active[0].playerId;
        Assert.That(service.SetActive("kings",player,-1,0,out error),Is.True,error);
        Assert.That(QuickPlayMatch.TryHeadToHead(service,"kings","vipers",0,new MatchOptions(),new SeededMatchRandom(0),out _,out error),Is.False);
        Assert.That(error,Does.Contain("6/7"));Assert.That(service.Team("kings").players.Count,Is.EqualTo(7));
    }
    [Test]
    public void PickupDraftsFourteenUniqueSavedPlayersAcrossTeamsWithoutSaving()
    {
        service.SaveCurrent(out _);string disk=File.ReadAllText(path),source=JsonUtility.ToJson(service.Snapshot);
        var match=Pickup();var all=match.Snapshot.teams.SelectMany(t=>t.players).ToArray();
        Assert.That(all.Length,Is.EqualTo(14));Assert.That(all.Select(p=>p.record.id).Distinct().Count(),Is.EqualTo(14));
        Assert.That(match.Snapshot.teams[match.HumanSide].players.Select(p=>match.SourceTeamFor(p.record.id)).Distinct().Count(),Is.EqualTo(2));
        Assert.That(match.Brand(0).Name,Is.EqualTo("PICKUP HOME"));Assert.That(match.Brand(1).Name,Is.EqualTo("PICKUP AWAY"));
        Assert.That(match.Brand(0).Primary,Is.Not.EqualTo(match.Brand(1).Primary));
        foreach(var player in all)
        {
            var saved=service.Team(match.SourceTeamFor(player.record.id)).players.Single(p=>p.record.id==player.record.id);
            Assert.That(JsonUtility.ToJson(player),Is.EqualTo(JsonUtility.ToJson(saved)));
        }
        Assert.That(JsonUtility.ToJson(service.Snapshot),Is.EqualTo(source));Assert.That(File.ReadAllText(path),Is.EqualTo(disk));
        var restart=new RosterService(settings,new RosterSaveStore(path));Assert.That(JsonUtility.ToJson(restart.Snapshot),Is.EqualTo(source));
    }
    [Test]
    public void PoolIncludesReservesAndSupportsTeamNameRatingAndRoleBrowsing()
    {
        var reserve=PlayerEditorSession.New(settings,service.NextNumber("kings"));reserve.record.displayName="ZZZ Reserve";
        reserve.record.speed=20;Assert.That(service.SavePlayer("kings",reserve,-1,0,out var id,out var error),Is.True,error);
        var pool=new PickupDraft(service);
        Assert.That(pool.Pool.Count,Is.EqualTo(15));Assert.That(pool.Browse("ZZZ","kings",PickupSort.Name).Single().Id,Is.EqualTo(id));
        Assert.That(pool.Browse("ZZZ","vipers",PickupSort.Role),Is.Empty);
        Assert.That(pool.Browse("",null,PickupSort.Rating).Select(p=>p.Overall),Is.Ordered.Descending);
        Assert.That(pool.Pool.Single(p=>p.Id==id).Role,Is.EqualTo("RESERVE"));
        var exposed=pool.Pool[0].Player;exposed.record.speed=0;Assert.That(pool.Pool[0].Player.record.speed,Is.Not.EqualTo(0));
    }
    [Test]
    public void DraftRejectsDuplicateAndEighthPicksAndInsufficientPool()
    {
        var draft=MixedDraft();Assert.That(draft.Add(draft.Selected[0],out _),Is.False);
        Assert.That(draft.Add(draft.Pool.First(p=>!draft.Selected.Contains(p.Id)).Id,out _),Is.False);
        var duplicate=draft.Selected.ToArray();duplicate[1]=duplicate[0];
        Assert.That(QuickPlayMatch.TryPickup(service,duplicate,draft.SuggestHuman(),0,new MatchOptions(),new SeededMatchRandom(0),out _,out _),Is.False);
        Assert.That(service.Delete(service.Team("vipers").players[0].record.id,out var error),Is.True,error);
        var shortPool=new PickupDraft(service);Assert.That(shortPool.PoolError,Does.Contain("14 unique"));
        Assert.That(shortPool.Add(shortPool.Pool[0].Id,out _),Is.False);
    }
    [Test]
    public void IndependentSlotsRejectMissingAndDuplicateAssignmentsAndAllowSwaps()
    {
        var draft=MixedDraft();var lineup=draft.SuggestHuman();
        MatchLineups.Swap(lineup,lineup[0].playerId,6,false);MatchLineups.Swap(lineup,lineup[0].playerId,4,true);
        Assert.That(QuickPlayMatch.TryPickup(service,draft.Selected,lineup,0,new MatchOptions(),new SeededMatchRandom(0),out var match,out var error),Is.True,error);
        Assert.That(match.Snapshot.teams[0].active.Single(a=>a.playerId==lineup[0].playerId).defense,Is.EqualTo(4));
        lineup[1].defense=lineup[0].defense;
        Assert.That(QuickPlayMatch.TryPickup(service,draft.Selected,lineup,0,new MatchOptions(),new SeededMatchRandom(0),out _,out _),Is.False);
        lineup=draft.SuggestHuman();lineup[1].offense=lineup[0].offense;
        Assert.That(QuickPlayMatch.TryPickup(service,draft.Selected,lineup,0,new MatchOptions(),new SeededMatchRandom(0),out _,out _),Is.False);
        var data=match.Snapshot;data.teams[1].active[0].playerId=data.teams[0].active[0].playerId;
        Assert.That(match.TryWithLineups(data.teams[0].active,data.teams[1].active,service,out _,out _),Is.False);
    }
    [Test]
    public void RandomUsesOnlyPlayableCatalogEntriesAndIsSeededAndDetached()
    {
        var fixture=Object.Instantiate(settings);var unavailable=ScriptableObject.CreateInstance<FieldDefinition>();
        unavailable.venueId="unavailable";fixture.fields=new[]{null,unavailable}.Concat(settings.fields).ToArray();
        try
        {
            var options=new MatchOptions {condition=MatchCondition.Random,difficulty=MatchDifficulty.Random};
            var seen=new HashSet<MatchDifficulty>();
            for(int seed=0;seed<120;seed++)
            {
                Assert.That(MatchConfiguration.TryResolve(fixture,options,new SeededMatchRandom(seed),out var a,out var error),Is.True,error);
                Assert.That(MatchConfiguration.TryResolve(fixture,options,new SeededMatchRandom(seed),out var b,out _),Is.True);
                Assert.That(a.Field,Is.SameAs(b.Field));Assert.That(a.Condition,Is.EqualTo(b.Condition));Assert.That(a.Difficulty,Is.EqualTo(b.Difficulty));
                Assert.That(a.Condition,Is.Not.EqualTo(MatchCondition.Random));Assert.That(a.Difficulty,Is.Not.EqualTo(MatchDifficulty.Random));
                Assert.That(settings.fields,Does.Contain(a.Field));seen.Add(a.Difficulty);
            }
            Assert.That(seen.Count,Is.EqualTo(4));
            Assert.That(QuickPlayMatch.TryHeadToHead(service,null,null,-1,options,new SeededMatchRandom(5),out var match,out _),Is.True);
            Assert.That(match.Team0Id,Is.Not.EqualTo(match.Team1Id));Assert.That(match.RequestedHumanSide,Is.EqualTo(-1));
            options.difficulty=MatchDifficulty.Easy;Assert.That(match.Configuration.RequestedDifficulty,Is.EqualTo(MatchDifficulty.Random));
        }
        finally {Object.Destroy(fixture);Object.Destroy(unavailable);}
    }
    [TestCase(ConditionSeverity.Clean,.05f)] [TestCase(ConditionSeverity.Regular,.1f)] [TestCase(ConditionSeverity.Hard,.18f)]
    public void SlickSeparatesAccelerationFromAgilityAndAppliesExactlyOnce(ConditionSeverity severity,float amount)
    {
        var attr=ScriptableObject.CreateInstance<PlayerAttributes>();var legacy=ScriptableObject.CreateInstance<FieldModifiers>();
        legacy.entries.Add(new FieldModifiers.Entry {stat=AttributeStat.Speed,additivePercent=.1f});
        try
        {
            attr.BindMatchModifiers(MatchModifiers.From(settings.Condition(MatchCondition.Slick),severity));
            Assert.That(attr.Speed(),Is.EqualTo(1f+amount).Within(.0001f));Assert.That(attr.Speed(legacy),Is.EqualTo(attr.Speed()));
            Assert.That(attr.Agility(),Is.EqualTo(1f-amount).Within(.0001f));Assert.That(attr.Acceleration(),Is.EqualTo(1f-amount).Within(.0001f));
            Assert.That(attr.Carrying(),Is.EqualTo(1f));
        }
        finally {Object.Destroy(attr);Object.Destroy(legacy);}
    }
    [TestCase(MatchCondition.Cold,AttributeStat.RunPower,1.1f)]
    [TestCase(MatchCondition.Loose,AttributeStat.Carrying,1.1f)]
    [TestCase(MatchCondition.Raw,AttributeStat.Carrying,.9f)]
    public void ConditionsMapPowerAndCarryingToExistingStats(MatchCondition condition,AttributeStat stat,float expected)
    {
        var attr=ScriptableObject.CreateInstance<PlayerAttributes>();
        try {attr.BindMatchModifiers(MatchModifiers.From(settings.Condition(condition),ConditionSeverity.Regular));Assert.That(attr.Effective(stat,10),Is.EqualTo(expected).Within(.0001f));}
        finally {Object.Destroy(attr);}
    }
    [Test]
    public void ModifierAggregationAndFinalCapsRemainBoundedAndAffectActualRouteDistance()
    {
        var data=ScriptableObject.CreateInstance<ConditionData>();var attr=ScriptableObject.CreateInstance<PlayerAttributes>();
        var curves=ScriptableObject.CreateInstance<AttributeCurves>();
        try
        {
            data.buffs.Add(new StatModifier {stat=ConditionStat.Speed,regularPercent=100});
            data.buffs.Add(new StatModifier {stat=ConditionStat.Speed,regularPercent=100});
            var modifiers=MatchModifiers.From(data,ConditionSeverity.Hard);Assert.That(modifiers.Get(AttributeStat.Speed),Is.EqualTo(.3f));
            curves.entries.Add(new AttributeCurves.Entry {stat=AttributeStat.Speed,curve=AnimationCurve.Linear(0,1,20,1),hardCapMin=.8f,hardCapMax=1.2f});
            attr.curves=curves;attr.BindMatchModifiers(modifiers);Assert.That(attr.Speed(),Is.EqualTo(1.2f));
            attr.curves=null;var receiver=Object.FindObjectsByType<ReceiverAI>().First();receiver.SetAttributes(attr);
            receiver.transform.position=Vector3.zero;receiver.transform.rotation=Quaternion.identity;receiver.SetRoute(RoutePattern.Go);
            attr.BindMatchModifiers(MatchModifiers.Clear);float clear=receiver.PredictedPosition(1f).z;
            attr.BindMatchModifiers(MatchModifiers.From(settings.Condition(MatchCondition.Slick),ConditionSeverity.Regular));
            Assert.That(receiver.PredictedPosition(1f).z,Is.EqualTo(clear*1.1f).Within(.001f));
        }
        finally {Object.Destroy(data);Object.Destroy(attr);Object.Destroy(curves);}
    }
    [Test]
    public void CpuDraftQualityRanksSavedReservesWithoutChangingTheirAttributes()
    {
        // Extra real saved records make selection quality observable beyond the default fourteen-player fixture.
        for(int i=0;i<14;i++)
        {
            var player=PlayerEditorSession.New(settings,service.NextNumber("vipers"));player.record.displayName="Draft reserve "+i;
            for(int stat=0;stat<12;stat++)RosterRatings.Set(ref player.record,stat,i<7?20:0);
            Assert.That(service.SavePlayer("vipers",player,-1,0,out _,out var error),Is.True,error);
        }
        var draft=MixedDraft();var easy=new DifficultyTuning(settings.difficulties.Single(p=>p.difficulty==MatchDifficulty.Easy));
        var legend=new DifficultyTuning(settings.difficulties.Single(p=>p.difficulty==MatchDifficulty.StreetLegend));
        string before=JsonUtility.ToJson(service.Snapshot);bool different=false;
        for(int seed=0;seed<10;seed++)
        {
            var low=PickupDraft.DraftCpu(draft.Pool,draft.Selected,easy,new SeededMatchRandom(seed));
            var high=PickupDraft.DraftCpu(draft.Pool,draft.Selected,legend,new SeededMatchRandom(seed));
            Assert.That(high.Average(p=>p.Overall),Is.GreaterThanOrEqualTo(low.Average(p=>p.Overall)));
            different|=high.Average(p=>p.Overall)>low.Average(p=>p.Overall);
            Assert.That(high.Select(p=>p.Id).Intersect(draft.Selected),Is.Empty);
        }
        Assert.That(different,Is.True);Assert.That(JsonUtility.ToJson(service.Snapshot),Is.EqualTo(before));
    }
    [Test]
    public void ClearIsZeroForEveryStatAndAccelerationAndGamebreakerIsAppliedOnce()
    {
        var attr=ScriptableObject.CreateInstance<PlayerAttributes>();
        try
        {
            attr.BindMatchModifiers(MatchModifiers.From(settings.Condition(MatchCondition.Clear),ConditionSeverity.Hard,true));
            foreach(AttributeStat stat in Enum.GetValues(typeof(AttributeStat)))Assert.That(attr.Effective(stat,10),Is.EqualTo(1f));
            Assert.That(attr.Acceleration(),Is.EqualTo(1f));
            var play=PlayState.Instance;play.StopAllCoroutines();Set(play,"<IsLive>k__BackingField",true);
            attr.ownerTeamId=0;play.AddTeamPoints(0,1000f);Assert.That(play.TryActivateGamebreaker(0),Is.True);
            attr.BindMatchModifiers(MatchModifiers.From(settings.Condition(MatchCondition.Slick),ConditionSeverity.Regular));
            float boost=play.GetGamebreakerMult(0,AttributeStat.Speed);
            Assert.That(attr.Speed(),Is.EqualTo(1.1f*boost).Within(.0001f));Assert.That(attr.Acceleration(),Is.EqualTo(.9f*boost).Within(.0001f));
        }
        finally {Object.Destroy(attr);}
    }
    [UnityTest]
    public IEnumerator DifficultyChangesActualPursuitCoverageAndOffensiveDecisions()
    {
        var play=PlayState.Instance;play.StopAllCoroutines();Set(play,"<IsLive>k__BackingField",true);
        var defender=Object.FindObjectsByType<DefenderAI>().First(d=>d.GetComponent<TeamMember>().teamId==1);
        var qb=play.Passer;BallController.Instance.AttachTo(qb);defender.SetRole(DefenderAI.Role.Engage);defender.SetUserControlled(false);
        defender.transform.position=qb.position+Vector3.right*8f;var start=defender.transform.position;
        FrontEndRuntime.Match=Head(0,new MatchOptions {difficulty=MatchDifficulty.Easy});Call(defender,"Update");Assert.That(defender.transform.position,Is.EqualTo(start));
        FrontEndRuntime.Match=Head(0,new MatchOptions {difficulty=MatchDifficulty.StreetLegend});Call(defender,"Update");Assert.That(defender.transform.position,Is.Not.EqualTo(start));
        var receiver=Object.FindObjectsByType<TeamMember>().First(m=>m.teamId==0&&m.slot==TeamMember.RosterSlot.WR1).transform;
        defender.SetCoverTarget(receiver);defender.transform.position=start;BallController.Instance.Throw(receiver.position,receiver,false,.5f,1f);
        Set(defender,"ballInFlightTimer",.2f);FrontEndRuntime.Match=Head(0,new MatchOptions {difficulty=MatchDifficulty.Easy});Call(defender,"UpdateInFlightState",BallController.Instance);
        Assert.That(defender.transform.position,Is.EqualTo(start));
        FrontEndRuntime.Match=Head(0,new MatchOptions {difficulty=MatchDifficulty.StreetLegend});Call(defender,"UpdateInFlightState",BallController.Instance);
        Assert.That(defender.transform.position,Is.Not.EqualTo(start));
        var caller=Object.FindAnyObjectByType<CPUPlayCaller>();Set(caller,"shortYardageThreshold",100f);
        FrontEndRuntime.Match=Head(0,new MatchOptions {difficulty=MatchDifficulty.Easy});float easy=(float)Call(caller,"RunChanceForSituation");
        FrontEndRuntime.Match=Head(0,new MatchOptions {difficulty=MatchDifficulty.Hard});float hard=(float)Call(caller,"RunChanceForSituation");
        Assert.That(easy,Is.Not.EqualTo(hard));Assert.That(DifficultyTuning.ForTeam(0),Is.SameAs(DifficultyTuning.Normal));
        yield return null;
    }
    [Test]
    public void NormalProfileRetainsBaselineAndProfilesAreFrozen()
    {
        var match=Head();var normal=match.Configuration.Cpu;
        Assert.That(normal.PursuitReaction,Is.Zero);Assert.That(normal.PursuitLead,Is.Zero);Assert.That(normal.PassReactionScale,Is.EqualTo(1f));
        Assert.That(normal.CoverageCushionScale,Is.EqualTo(1f));Assert.That(normal.ZoneAwarenessScale,Is.EqualTo(1f));
        Assert.That(normal.RunChance(.55f,.25f),Is.EqualTo(.25f).Within(.0001f));Assert.That(normal.QbReadTimeScale,Is.EqualTo(1f));
        var source=settings.difficulties.Single(d=>d.difficulty==MatchDifficulty.Normal);float original=source.passReactionScale;
        try {source.passReactionScale=9f;Assert.That(normal.PassReactionScale,Is.EqualTo(1f));}
        finally {source.passReactionScale=original;}
    }
    [UnityTest] public IEnumerator HeadHomeLifecycle()=>Lifecycle(false,0);
    [UnityTest] public IEnumerator HeadAwayLifecycle()=>Lifecycle(false,1);
    [UnityTest] public IEnumerator PickupHomeLifecycle()=>Lifecycle(true,0);
    [UnityTest] public IEnumerator PickupAwayLifecycle()=>Lifecycle(true,1);
    IEnumerator Lifecycle(bool pickup,int side)
    {
        var options=new MatchOptions {condition=MatchCondition.Random,difficulty=MatchDifficulty.Random};
        var match=pickup?Pickup(side,options):Head(side,options);string before=JsonUtility.ToJson(match.Snapshot);
        FrontEndRuntime.Match=match;yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;
        Assert.That(PlayState.Instance.UserTeamId,Is.EqualTo(side));Assert.That(Object.FindObjectsByType<TeamMember>().Length,Is.EqualTo(14));
        foreach(var actor in Object.FindObjectsByType<CharacterApplier>())
        {
            var member=actor.GetComponent<TeamMember>();var record=actor.Record.Value;var team=match.Snapshot.teams[member.teamId];
            Assert.That(team.active.Single(a=>a.playerId==record.id).offense,Is.EqualTo(member.slot));
            Assert.That(team.active.Single(a=>a.playerId==record.id).defense,Is.EqualTo(member.defensiveLineupSlot));
            Assert.That(PlayState.Instance.IdentityFor(member.teamId).teamName,Is.EqualTo(match.Brand(member.teamId).Name));
        }
        yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;
        Assert.That(FrontEndRuntime.Match,Is.SameAs(match));Assert.That(JsonUtility.ToJson(match.Snapshot),Is.EqualTo(before));
        Assert.That(Object.FindObjectsByType<CharacterApplier>().Select(a=>a.Record.Value.id).Distinct().Count(),Is.EqualTo(14));
        Object.FindAnyObjectByType<MatchBootstrap>().GoToMenu();yield return null;yield return null;
        Assert.That(FrontEndRuntime.Match,Is.Null);Assert.That(Object.FindObjectsByType<TeamMember>(),Is.Empty);
        FrontEndRuntime.Match=pickup?Head():Pickup();yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;
        Assert.That(Object.FindObjectsByType<TeamMember>().Length,Is.EqualTo(14));
        var neutral=Object.FindObjectsByType<PlayerStateMachine>().First().Attributes;
        var caps=neutral.curves.HardCap(AttributeStat.Speed);
        float baseline=Mathf.Clamp(neutral.curves.Evaluate(AttributeStat.Speed,neutral.speed),caps.min,caps.max);
        Assert.That(FrontEndRuntime.Match.Configuration.Condition,Is.EqualTo(MatchCondition.Clear));
        Assert.That(neutral.Speed(),Is.EqualTo(baseline).Within(.0001f),"The previous match's condition must not survive into a new Clear match");
    }
    [UnityTest]
    public IEnumerator PickupMenuDraftLineupReviewAndStartAreWired()
    {
        typeof(FrontEndRuntime).GetProperty("Rosters").SetValue(null,service);yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
        var menu=Object.FindAnyObjectByType<FrontEndController>();menu.TestSeed=2;
        Click("NEW GAME");yield return null;Capture("Modes");
        Click("PICKUP GAME");Assert.That(menu.Screen,Is.EqualTo(FrontEndScreen.Draft));yield return null;Capture("Pickup-pool");
        var ids=MixedDraft().Selected.ToArray();
        foreach(string id in ids)
        {
            var name=menu.Draft.Pool.Single(p=>p.Id==id).Player.record.displayName;
            Object.FindObjectsByType<Button>().First(b=>b.name.Contains(name)&&!b.name.Contains("REMOVE")).onClick.Invoke();Click("DRAFT SELECTED");
        }
        Assert.That(menu.Draft.Selected.Count,Is.EqualTo(7));yield return null;Capture("Pickup-seven");
        Click("ASSIGN LINEUP");yield return null;Capture("Pickup-lineup");Click("MATCH SETTINGS");yield return null;Capture("Match-settings");
        menu.PickupOptions.condition=MatchCondition.Slick;menu.PickupOptions.difficulty=MatchDifficulty.Hard;
        Click("REVIEW MATCH");Assert.That(menu.PreparedMatch.Mode,Is.EqualTo(ExhibitionMode.Pickup));
        yield return null;Capture("Pickup-review");
        var initial=menu.PreparedMatch;
        Object.FindObjectsByType<Button>().First(b=>b.name=="›").onClick.Invoke();
        Assert.That(menu.PreparedMatch.Configuration,Is.SameAs(initial.Configuration),"Lineup edits must not reroll Random or redraft");
        var frozen=menu.PreparedMatch;Click("START MATCH");yield return null;yield return null;
        Assert.That(FrontEndRuntime.Match,Is.SameAs(frozen));Assert.That(PlayState.Instance.IdentityFor(0).teamName,Is.EqualTo("PICKUP HOME"));
        Capture("Pickup-gameplay");
        foreach(var label in Object.FindObjectsByType<TMP_Text>().Where(t=>t.name=="TeamName"))
        {
            label.ForceMeshUpdate();Assert.That(label.isTextTruncated,Is.False,"PICKUP HOME/AWAY must remain distinguishable in the scorebug");
            Assert.That(label.textInfo.characterCount,Is.EqualTo(label.text.Length),"The HOME/AWAY suffix must actually render");
        }
    }
    [UnityTest] public IEnumerator HeadCompletesMatchAndUsesWinnerRematch()=>CompleteMode(false);
    [UnityTest] public IEnumerator PickupCompletesMatchAndUsesWinnerRematch()=>CompleteMode(true);
    IEnumerator CompleteMode(bool pickup)
    {
        var match=pickup?Pickup(1,new MatchOptions {condition=MatchCondition.Slick,difficulty=MatchDifficulty.Hard}):Head();
        string frozen=JsonUtility.ToJson(match.Snapshot);FrontEndRuntime.Match=match;
        yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;
        var play=PlayState.Instance;var ball=BallController.Instance;
        Set(play,"playEndPresentationDelay",.01f);Set(play,"formationMoveSpeed",1000f);Set(ball,"carryOffset",Vector3.zero);
        int winner=match.HumanSide;
        for(int touchdown=0;touchdown<4;touchdown++)
        {
            yield return ReadyAndSnap();
            var carrier=Object.FindObjectsByType<TeamMember>().Single(p=>p.teamId==winner&&p.slot==TeamMember.RosterSlot.RB).transform;
            var direction=play.DirectionFor(winner);carrier.position=new Vector3(0f,1f,direction.Advance(direction.TargetGoalLineZ,1f));
            ball.AttachTo(carrier);play.EndPlay(PlayState.PlayEndReason.Touchdown);
        }
        Assert.That(play.IsGameOver,Is.True);Assert.That(play.WinnerTeamId,Is.EqualTo(winner));
        Assert.That(ScoreBoard.Instance.ScoreFor(winner),Is.GreaterThanOrEqualTo(21));
        var hud=Object.FindAnyObjectByType<GameHUD>();
        Assert.That((GameObject)hud.GetType().GetField("gameOverRoot",Private).GetValue(hud),Is.Not.Null);
        Set(hud,"restartInputDelay",0f);Call(hud,"HandleConfirm",default(UnityEngine.InputSystem.InputAction.CallbackContext));
        yield return null;yield return null;
        Assert.That(FrontEndRuntime.Match,Is.SameAs(match));Assert.That(JsonUtility.ToJson(match.Snapshot),Is.EqualTo(frozen));
        Assert.That(PlayState.Instance.IsGameOver,Is.False);Assert.That(ScoreBoard.Instance.ScoreFor(winner),Is.Zero);
        Assert.That(Object.FindObjectsByType<TeamMember>().Length,Is.EqualTo(14));
        Set(PlayState.Instance,"formationMoveSpeed",1000f);yield return ReadyAndSnap();
        BallController.Instance.Drop();PlayState.Instance.EndPlay(PlayState.PlayEndReason.Incomplete);
        Assert.That(DownsTracker.Instance.CurrentDown,Is.EqualTo(2));
    }
    IEnumerator ReadyAndSnap()
    {
        var play=PlayState.Instance;float deadline=Time.realtimeSinceStartup+5f;
        while(!play.IsInHuddle&&!play.IsGameOver&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.That(play.IsInHuddle,Is.True);play.BreakHuddle();
        while(!play.IsSetAtLOS&&!play.IsLive&&Time.realtimeSinceStartup<deadline)yield return null;
        if(!play.IsLive)play.ResetPlay();Assert.That(play.IsLive,Is.True);
    }
    static void Capture(string name)
    {
        // Batch-mode screenshots need an explicit render; restore all canvas bindings afterward.
        var target=new RenderTexture(1920,1080,24);target.Create();
        var cameraObject=new GameObject("Exhibition evidence camera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();
        camera.transform.position=new Vector3(0,0,-10000);camera.orthographic=true;camera.orthographicSize=5;
        camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.targetTexture=target;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=FrontEndUI.Ink;
        var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var bindings=canvases.Select(c=>(c.renderMode,c.worldCamera,c.planeDistance)).ToArray();
        for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceCamera;canvases[i].worldCamera=camera;canvases[i].planeDistance=1;}
        Canvas.ForceUpdateCanvases();camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();RenderTexture.active=previous;
        string folder=Path.Combine(Application.dataPath,"../Builds/Validation");Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder,"Exhibition-"+name+".png"),texture.EncodeToPNG());Object.Destroy(texture);
        for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=bindings[i].renderMode;canvases[i].worldCamera=bindings[i].worldCamera;canvases[i].planeDistance=bindings[i].planeDistance;}
        camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(cameraObject);Canvas.ForceUpdateCanvases();
    }
    static void Click(string label)=>Object.FindObjectsByType<Button>().First(b=>b.name==label&&b.gameObject.activeInHierarchy).onClick.Invoke();
}
