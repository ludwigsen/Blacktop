using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Use unique temporary saves: automated UI never reads or overwrites a developer's actual roster file.
public class FrontEndTests
{
    FrontEndSettings settings;
    RosterService service;
    string directory,path;
    [UnitySetUp]
    public IEnumerator Setup()
    {
        FrontEndRuntime.Match=null;
        yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;
        settings=Object.FindAnyObjectByType<MatchBootstrap>().settings;
        directory=Path.Combine(Path.GetTempPath(),"blacktop-roster-test-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        path=Path.Combine(directory,"rosters.json");service=new RosterService(settings,new RosterSaveStore(path));
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        FrontEndRuntime.Match=null;
        typeof(FrontEndRuntime).GetProperty("Rosters").SetValue(null,null);
        var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Front end cleanup"));
        yield return SceneManager.UnloadSceneAsync(scene);
        if(Directory.Exists(directory))Directory.Delete(directory,true);
    }
    void InjectService()=>typeof(FrontEndRuntime).GetProperty("Rosters").SetValue(null,service);
    RosterPlayer Rookie(string team="kings")
    {
        var player=PlayerEditorSession.New(settings,service.NextNumber(team));player.record.displayName="Rook Example";
        player.appearance.height=PlayerHeight.Ft6In3;player.appearance.bodyArchetype=BodyArchetype.Thick;
        player.appearance.bodyId="body.thick.temporary";
        player.appearance.clothingIds=new[]{"gear.top.thick.temporary","gear.shorts.thick.temporary","gear.footwear.thick.temporary"};
        for(int i=0;i<12;i++)RosterRatings.Set(ref player.record,i,20-i);
        return player;
    }
    [Test]
    public void CrudTransactionsPreserveIdentityAndKeepUnlimitedReservesSeparate()
    {
        string canonical=JsonUtility.ToJson(settings.Team("kings").roster);
        var draft=Rookie();var session=new PlayerEditorSession(draft,"kings");
        Assert.That(session.Save(service,out string id,out string error),Is.True,error);
        Assert.That(Guid.TryParse(id,out _),Is.True);Assert.That(draft.record.id,Is.Empty);
        Assert.That(service.Team("kings").players.Count,Is.EqualTo(8));Assert.That(service.Team("kings").active.Count,Is.EqualTo(7));
        var saved=service.Team("kings").players.Single(p=>p.record.id==id);
        var duplicate=new PlayerEditorSession(saved,"kings",duplicate:true);duplicate.Draft.record.jerseyNumber=service.NextNumber("kings");
        Assert.That(duplicate.Save(service,out string other,out error),Is.True,error);Assert.That(other,Is.Not.EqualTo(id));
        saved.record.jerseyNumber=42;saved.record.displayName="Renamed Player";
        Assert.That(service.SavePlayer("kings",saved,-1,0,out var edited,out error),Is.True,error);Assert.That(edited,Is.EqualTo(id));
        Assert.That(service.Transfer(id,"vipers",out error),Is.True,error);
        Assert.That(service.Team("kings").players.Any(p=>p.record.id==id),Is.False);
        var moved=service.Team("vipers").players.Single(p=>p.record.id==id);
        Assert.That(RosterRatings.Get(moved.record),Is.EqualTo(RosterRatings.Get(draft.record)));
        Assert.That(moved.appearance.height,Is.EqualTo(PlayerHeight.Ft6In3));
        Assert.That(service.SetActive("kings",other,0,6,out error),Is.True,error);
        Assert.That(service.Team("kings").active.Count,Is.EqualTo(7));
        Assert.That(service.Team("kings").active.Select(a=>a.defense).Distinct().Count(),Is.EqualTo(7));
        Assert.That(service.Delete(other,out error),Is.True,error);Assert.That(service.Readiness("kings"),Does.Contain("6/7"));
        var reloaded=new RosterService(settings,new RosterSaveStore(path));
        Assert.That(reloaded.Team("vipers").players.Single(p=>p.record.id==id).record.displayName,Is.EqualTo("Renamed Player"));
        Assert.That(JsonUtility.ToJson(settings.Team("kings").roster),Is.EqualTo(canonical));
    }
    [Test]
    public void ValidationFailuresDoNotPartiallyChangeMemoryDiskOrDraftGuids()
    {
        Assert.That(service.SaveCurrent(out var error),Is.True,error);string disk=File.ReadAllText(path),before=JsonUtility.ToJson(service.Snapshot);
        var draft=Rookie();draft.record.jerseyNumber=service.Team("kings").players[0].record.jerseyNumber;
        Assert.That(service.SavePlayer("kings",draft,-1,0,out _,out error),Is.False);Assert.That(error,Does.Contain("jersey"));
        draft.record.jerseyNumber=service.NextNumber("kings");draft.record.speed=21;
        Assert.That(service.SavePlayer("kings",draft,-1,0,out _,out error),Is.False);
        draft.record.speed=10;draft.appearance.faceId="not.installed";
        Assert.That(service.SavePlayer("kings",draft,-1,0,out _,out error),Is.False);
        Assert.That(draft.record.id,Is.Empty);Assert.That(File.ReadAllText(path),Is.EqualTo(disk));
        Assert.That(JsonUtility.ToJson(service.Snapshot),Is.EqualTo(before));
        string blocked=Path.Combine(directory,"not-a-directory");File.WriteAllText(blocked,"block");
        var failed=new RosterService(settings,new RosterSaveStore(Path.Combine(blocked,"save.json")));
        Assert.That(failed.SaveCurrent(out error),Is.False);Assert.That(error,Does.Contain("Nothing changed"));
    }
    [Test]
    public void RestartBackupRecoveryRestoreAndCancelKeepCanonicalDefaultsIntact()
    {
        service.SaveCurrent(out _);service.SavePlayer("kings",Rookie(),-1,0,out _,out _);
        File.WriteAllText(path,"{invalid json");var recovered=new RosterService(settings,new RosterSaveStore(path));
        Assert.That(recovered.LoadMessage,Does.Contain("Recovered"));Assert.That(recovered.Team("kings").players.Count,Is.EqualTo(7));
        var session=new PlayerEditorSession(Rookie(),"kings");session.Draft.record.speed=1;Assert.That(session.Dirty,Is.True);
        session.Reset();Assert.That(session.Dirty,Is.False);Assert.That(service.Team("kings").players.Count,Is.EqualTo(8));
        Assert.That(service.RestoreDefaults(out var error),Is.True,error);Assert.That(service.Team("kings").players.Count,Is.EqualTo(7));
        File.WriteAllText(path,"{\"version\":99,\"teams\":[]}");File.Delete(path+".bak");
        var outdated=new RosterService(settings,new RosterSaveStore(path));Assert.That(outdated.LoadMessage,Does.Contain("Unsupported"));
        Assert.That(outdated.Team("kings").players.Select(p=>p.record.id),Is.EqualTo(settings.Team("kings").roster.players.Select(p=>p.id)));
    }
    [Test]
    public void HeightWeightAndTemporarySilhouettesUseOneRigWithoutChangingRatings()
    {
        int[] baseline={165,190,220,250,275};var meshes=new System.Collections.Generic.HashSet<Mesh>();
        // These original five saved IDs remain the legacy migration regression.
        foreach(BodyArchetype build in new[]{BodyArchetype.Lean,BodyArchetype.Athletic,BodyArchetype.Thick,BodyArchetype.Heavy,BodyArchetype.Massive})
        {
            Assert.That(settings.weights.Pounds(PlayerHeight.Ft6In0,build),Is.EqualTo(baseline[(int)build]));
            var part=settings.appearances.parts.Single(p=>p.slot==CharacterPartSlot.Body&&p.bodyArchetype==build&&p.temporaryVisual&&p.id.EndsWith(".temporary",StringComparison.Ordinal));
            Assert.That(part.skeleton.ValidateSkin(part.skinnedSource,out string error,part.skinnedMeshOverride),Is.True,error);
            meshes.Add(part.skinnedMeshOverride);
            foreach(PlayerHeight height in Enum.GetValues(typeof(PlayerHeight)))Assert.That(settings.weights.Pounds(height,build),Is.InRange(120,390));
        }
        Assert.That(meshes.Count,Is.EqualTo(5));
        var heavy=settings.appearances.Find("body.heavy.temporary").skinnedMeshOverride;
        var massive=settings.appearances.Find("body.massive.temporary").skinnedMeshOverride;
        Assert.That(heavy.vertices,Is.Not.EqualTo(massive.vertices),"Heavy and Massive must not be identical IDs on one mesh");
    }
    static Button ButtonNamed(string name)=>Object.FindObjectsByType<Button>().First(b=>b.gameObject.name==name);
    static void Click(string name)
    {
        var button=ButtonNamed(name);Assert.That(button.interactable,Is.True,name);
        ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
    }
    [UnityTest]
    public IEnumerator MenuCreateSaveActivateAndQuickPlayActuallyBindTheNewPlayer()
    {
        InjectService();yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
        var menu=Object.FindAnyObjectByType<FrontEndController>();Assert.That(menu.Screen,Is.EqualTo(FrontEndScreen.Main));
        Click("PLAYERS");Assert.That(menu.Screen,Is.EqualTo(FrontEndScreen.Teams));menu.ShowRoster("kings");Click("CREATE PLAYER");
        yield return null;
        var inputs=Object.FindObjectsByType<TMP_InputField>();inputs.Single(f=>f.name=="First name").text="Created";inputs.Single(f=>f.name=="Last name").text="InMenu";
        // Populate the editor's actual working copy, then use the visible Save command.
        var rookie=Rookie();menu.Session.Draft.appearance=rookie.appearance;
        for(int i=0;i<12;i++)RosterRatings.Set(ref menu.Session.Draft.record,i,RosterRatings.Get(rookie.record)[i]);
        Click("SAVE PLAYER");Assert.That(menu.Screen,Is.EqualTo(FrontEndScreen.Roster));
        var player=service.Team("kings").players.Single(p=>p.record.displayName=="Created InMenu");
        Assert.That(service.SetActive("kings",player.record.id,(int)TeamMember.RosterSlot.RB,0,out var error),Is.True,error);
        menu.ShowMain();Click("NEW GAME");Click("HEAD 2 HEAD");
        var human=Object.FindObjectsByType<Button>().First(b=>b.name.StartsWith("KINGS"));human.onClick.Invoke();
        Assert.That(menu.Screen,Is.EqualTo(FrontEndScreen.Opponent));
        Assert.That(Object.FindObjectsByType<Button>().First(b=>b.name.StartsWith("KINGS")).interactable,Is.False);
        Object.FindObjectsByType<Button>().First(b=>b.name.StartsWith("VIPERS")).onClick.Invoke();
        Assert.That(menu.Screen,Is.EqualTo(FrontEndScreen.Confirmation));Click("REVIEW MATCH");Click("START MATCH");yield return null;yield return null;
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("SampleScene"));
        var actor=Object.FindObjectsByType<CharacterApplier>().Single(a=>a.Record.HasValue&&a.Record.Value.id==player.record.id);
        Assert.That(actor.GetComponent<TeamMember>().slot,Is.EqualTo(TeamMember.RosterSlot.RB));
        Assert.That(actor.GetComponent<PlayerStateMachine>().Attributes.speed,Is.EqualTo(19));
        Assert.That(actor.GetComponent<PlayerVisualAssembler>().AppliedAppearance.bodyArchetype,Is.EqualTo(BodyArchetype.Thick));
        Assert.That(actor.GetComponent<PlayerVisualAssembler>().AppliedAppearance.heightProfile.height,Is.EqualTo(PlayerHeight.Ft6In3));
        Assert.That(actor.GetComponentInChildren<TMP_Text>().text,Does.Contain("Created InMenu"));
        Assert.That(Object.FindObjectsByType<TeamMember>().Length,Is.EqualTo(14));
        Assert.That(actor.transform.localScale,Is.EqualTo(Vector3.one));
        Assert.That(actor.GetComponent<CapsuleCollider>().height,Is.EqualTo(2f));
        Assert.That(actor.GetComponent<CapsuleCollider>().radius,Is.EqualTo(.5f));
        PlayState.Instance.StopAllCoroutines();
        typeof(PlayState).GetField("<IsLive>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(PlayState.Instance,true);
        BallController.Instance.AttachTo(actor.transform);yield return null;
        Assert.That(BallController.Instance.Carrier,Is.EqualTo(actor.transform));
        Assert.That(PossessionController.ActivelyControlled,Is.EqualTo(actor.transform));
    }
    [UnityTest]
    public IEnumerator HumanSideOneFieldSelectionRematchAndReturnToMenuStayCorrect()
    {
        Assert.That(QuickPlayMatch.TryCreate(service,"kings","vipers",1,settings.fields[1],out var match,out var error),Is.True,error);
        FrontEndRuntime.Match=match;yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;
        Assert.That(PlayState.Instance.UserTeamId,Is.EqualTo(1));Assert.That(PlayState.Instance.IdentityFor(1).teamName,Is.EqualTo(settings.Team("kings").identity.teamName));
        Assert.That(Object.FindAnyObjectByType<FieldLoader>().LoadedField,Is.SameAs(settings.fields[1]));
        Assert.That(PlayState.Instance.DirectionFor(0).Forward.z,Is.GreaterThan(0));Assert.That(PlayState.Instance.DirectionFor(1).Forward.z,Is.LessThan(0));
        yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;
        Assert.That(PlayState.Instance.UserTeamId,Is.EqualTo(1));Assert.That(Object.FindObjectsByType<TeamMember>().Count(m=>m.teamId==1),Is.EqualTo(7));
        Object.FindAnyObjectByType<MatchBootstrap>().GoToMenu();yield return null;yield return null;
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("MainMenu"));Assert.That(FrontEndRuntime.Match,Is.Null);
        Assert.That(Object.FindObjectsByType<TeamMember>(),Is.Empty,"Preview must never register gameplay actors");
    }
    [UnityTest]
    public IEnumerator CancelDiscardAndPreviewCameraDoNotLeakIntoTheRoster()
    {
        InjectService();yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
        var menu=Object.FindAnyObjectByType<FrontEndController>();menu.ShowRoster("kings");menu.CreatePlayer();yield return null;
        int count=service.Team("kings").players.Count;
        Object.FindObjectsByType<TMP_InputField>().Single(f=>f.name=="First name").text="Discarded";
        var preview=Object.FindAnyObjectByType<RuntimePlayerPreview>();preview.Rotate(45);preview.Zoom(-.5f);preview.SetHeadView(true);preview.ResetCamera();
        Click("CANCEL");Assert.That(Object.FindObjectsByType<Button>().Any(b=>b.name=="DISCARD"),Is.True);
        Click("DISCARD");yield return null;
        Assert.That(menu.Screen,Is.EqualTo(FrontEndScreen.Roster));Assert.That(service.Team("kings").players.Count,Is.EqualTo(count));
        Assert.That(Object.FindObjectsByType<RuntimePlayerPreview>(),Is.Empty);
        Assert.That(Object.FindObjectsByType<TeamMember>(),Is.Empty);
    }
#if UNITY_EDITOR
    [Test]
    public void ExplicitDevelopmentCommitWritesFixtureSourcesAndPreservesGuids()
    {
        // Dedicated source fixtures guarantee that this test never commits to a developer's real rosters.
        string folder="Assets/FrontEndCommitFixture_"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);
        UnityEditor.AssetDatabase.Refresh();var fixture=Object.Instantiate(settings);
        var generated=new System.Collections.Generic.List<string>();
        bool playersFolderExisted=Directory.Exists("Assets/_Project/Data/Characters/Players");
        try
        {
            fixture.teams=settings.teams.Select((source,index)=>{
                var team=Object.Instantiate(source);var roster=Object.Instantiate(source.roster);
                var ids=new System.Collections.Generic.Dictionary<string,string>();
                for(int i=0;i<roster.players.Count;i++)
                {
                    var record=roster.players[i];string old=record.id;record.id=Guid.NewGuid().ToString();record.appearance=null;
                    roster.players[i]=record;ids[old]=record.id;
                    generated.Add("Assets/_Project/Data/Characters/Players/"+record.id+".asset");
                }
                team.roster=roster;team.activeLineup=source.activeLineup.Select(a=>new LineupAssignment {playerId=ids[a.playerId],offense=a.offense,defense=a.defense}).ToArray();
                UnityEditor.AssetDatabase.CreateAsset(roster,$"{folder}/Roster{index}.asset");UnityEditor.AssetDatabase.CreateAsset(team,$"{folder}/Team{index}.asset");return team;
            }).ToArray();
            UnityEditor.AssetDatabase.CreateAsset(fixture,folder+"/Settings.asset");
            var authoring=new RosterService(fixture,new RosterSaveStore(Path.Combine(directory,"authoring.json")));
            var draft=PlayerEditorSession.New(fixture,authoring.NextNumber("kings"));draft.record.displayName="Committed Fixture";
            int before=fixture.Team("kings").roster.players.Count;
            Assert.That(authoring.SavePlayer("kings",draft,-1,0,out string id,out string error),Is.True,error);
            Assert.That(fixture.Team("kings").roster.players.Count,Is.EqualTo(before));
            generated.Add("Assets/_Project/Data/Characters/Players/"+id+".asset");
            Assert.That(FrontEndRuntime.SourceCommitRequested,Is.Not.Null);
            FrontEndRuntime.SourceCommitRequested.Invoke(authoring);
            Assert.That(FrontEndRuntime.SourceCommitMessage,Does.Contain("committed"));
            Assert.That(fixture.Team("kings").roster.players.Single(p=>p.id==id).displayName,Is.EqualTo("Committed Fixture"));
            Assert.That(File.ReadAllText(folder+"/Roster0.asset"),Does.Contain(id));
            var restored=new RosterService(fixture,new RosterSaveStore(Path.Combine(directory,"fresh-source.json")));
            Assert.That(restored.Team("kings").players.Single(p=>p.record.id==id).appearance.bodyId,Is.EqualTo(draft.appearance.bodyId));
        }
        finally
        {
            foreach(string asset in generated)UnityEditor.AssetDatabase.DeleteAsset(asset);
            UnityEditor.AssetDatabase.DeleteAsset(folder);
            if(!playersFolderExisted&&Directory.Exists("Assets/_Project/Data/Characters/Players")&&!Directory.EnumerateFileSystemEntries("Assets/_Project/Data/Characters/Players").Any())
                UnityEditor.AssetDatabase.DeleteAsset("Assets/_Project/Data/Characters/Players");
        }
    }
#endif
    [UnityTest]
    public IEnumerator GamepadSubmitAndBackNavigateTheRealMenu()
    {
        InjectService();yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
        var previous=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var gamepad=InputSystem.AddDevice<Gamepad>();
        try
        {
            EventSystem.current.SetSelectedGameObject(ButtonNamed("NEW GAME").gameObject);
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            Assert.That(Object.FindAnyObjectByType<FrontEndController>().Screen,Is.EqualTo(FrontEndScreen.Modes));
            InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;
            EventSystem.current.SetSelectedGameObject(ButtonNamed("HEAD 2 HEAD").gameObject);
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            Assert.That(Object.FindAnyObjectByType<FrontEndController>().Screen,Is.EqualTo(FrontEndScreen.HumanTeam));
            InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.East));yield return null;
            Assert.That(Object.FindAnyObjectByType<FrontEndController>().Screen,Is.EqualTo(FrontEndScreen.Modes));
            InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;
            EventSystem.current.SetSelectedGameObject(ButtonNamed("PICKUP GAME").gameObject);
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            Assert.That(Object.FindAnyObjectByType<FrontEndController>().Screen,Is.EqualTo(FrontEndScreen.Draft));
            InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.East));yield return null;
            Assert.That(Object.FindAnyObjectByType<FrontEndController>().Screen,Is.EqualTo(FrontEndScreen.Modes));
            InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.East));yield return null;
            Assert.That(Object.FindAnyObjectByType<FrontEndController>().Screen,Is.EqualTo(FrontEndScreen.Main));
        }
        finally{InputSystem.RemoveDevice(gamepad);InputSystem.settings.backgroundBehavior=previous;}
    }
}
