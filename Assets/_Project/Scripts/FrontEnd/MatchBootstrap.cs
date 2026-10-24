using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Before CharacterApplier (-100) and PlayState: configure the existing actors, not another spawner.
[DefaultExecutionOrder(-2000)]
public class MatchBootstrap : MonoBehaviour
{
    public FrontEndSettings settings;
    public TeamMember[] players;
    readonly List<Object> ownedData=new();
    InputAction menuAction;
    void Awake()
    {
        var match=FrontEndRuntime.Match;
        if(match==null)return; // Direct SampleScene launch retains its established development defaults.
        var snapshot=match.Snapshot;
        var identities=new TeamIdentity[2];
        for(int side=0;side<2;side++)
        {
            string teamId=side==0?match.Team0Id:match.Team1Id;
            var source=snapshot.teams.Find(t=>t.teamId==teamId);
            identities[side]=match.Brand(side).CreateRuntime();ownedData.Add(identities[side]);
            foreach(var assignment in source.active)
            {
                // Each source player's curve reference survives a mixed-team pickup draft.
                var roster=ScriptableObject.CreateInstance<TeamRoster>();ownedData.Add(roster);
                roster.curves=match.CurvesFor(assignment.playerId);roster.teamName=identities[side].teamName;
                var saved=source.players.Find(p=>p.record.id==assignment.playerId);
                var appearance=ScriptableObject.CreateInstance<PlayerAppearanceDefinition>();ownedData.Add(appearance);
                saved.appearance.Apply(appearance,settings.appearances,saved.record.id);
                var record=saved.record;record.appearance=appearance;record.accentColor=identities[side].accentColor;
                roster.players.Add(record);
                var actor=players.Single(p=>p.teamId==side && p.slot==assignment.offense);
                actor.defensiveLineupSlot=assignment.defense;
                actor.GetComponent<CharacterApplier>().Configure(roster,record.id,match.Configuration.Modifiers);
                var visual=actor.GetComponent<PlayerVisualAssembler>();visual.catalog=settings.appearances;visual.defaultAppearance=settings.defaultAppearance;
            }
        }
        GetComponent<PlayState>().ConfigureMatch(identities[0],identities[1],match.HumanSide);
        var field=FindAnyObjectByType<FieldLoader>();
        if(field!=null && !field.SelectField(match.Field)) Debug.LogError("Selected field could not load; existing valid venue retained.",this);
    }
    void Start()
    {
        menuAction=new InputAction("Return to main menu",binding:"<Keyboard>/escape");
        menuAction.AddBinding("<Gamepad>/start");menuAction.performed+=ReturnToMenu;menuAction.Enable();
        var canvas=FrontEndUI.CreateCanvas("Match menu",90);
        var button=FrontEndUI.Button(canvas.transform,"MENU  ·  ESC / START",()=>GoToMenu(),settings);
        FrontEndUI.Anchor(button.GetComponent<RectTransform>(),new Vector2(.015f,.015f),new Vector2(.19f,.065f));
    }
    void ReturnToMenu(InputAction.CallbackContext _) => GoToMenu();
    public void GoToMenu()
    {
        FrontEndRuntime.Match=null;
        SceneManager.LoadScene(settings.menuScene);
    }
    void OnDestroy()
    {
        menuAction?.Dispose();
        foreach(var asset in ownedData) if(asset!=null)Destroy(asset);
    }
}
