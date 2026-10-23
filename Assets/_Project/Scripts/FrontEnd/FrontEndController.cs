using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum FrontEndScreen { Main,HumanTeam,Opponent,Confirmation,Teams,Roster,Editor }

// Navigation owns screens only. Roster transactions and preview construction have independent owners.
public class FrontEndController : MonoBehaviour
{
    public FrontEndSettings settings;
    public RosterService Service { get; private set; }
    public FrontEndScreen Screen { get; private set; }
    public string SelectedTeam { get; private set; }
    public string SelectedPlayerId;
    public GameObject PreferredFocus;
    public string HumanTeam,OpponentTeam;
    public int HumanSide;
    public FieldDefinition Field;
    public PlayerEditorSession Session { get; private set; }
    public string Message { get; set; }
    Canvas canvas;
    RectTransform content,view;
    GameObject modal;
    GameObject priorFocus;
    InputAction back;

    void Awake()
    {
        FrontEndUI.EnsureInput();Service=FrontEndRuntime.Open(settings);
        Field=settings.fields.Length>0?settings.fields[0]:null;Message=Service.LoadMessage;
        canvas=FrontEndUI.CreateCanvas("Blacktop front end");canvas.transform.SetParent(transform,false);
        var background=FrontEndUI.Panel(canvas.transform,"Ink",Vector2.zero,Vector2.one,FrontEndUI.Ink);
        for(int i=0;i<3;i++)
        {
            var stripe=FrontEndUI.Panel(background,"Diagonal",new Vector2(.65f+i*.07f,-.3f),new Vector2(.68f+i*.07f,1.3f),new Color(.15f,.17f,.18f,.6f));
            stripe.localRotation=Quaternion.Euler(0,0,-18);
        }
        var paint=FrontEndUI.Panel(canvas.transform,"Brand paint",new Vector2(.025f,.90f),new Vector2(.37f,1.015f),new Color(.8f,.23f,.055f));
        paint.GetComponent<Image>().sprite=settings.paint;paint.localRotation=Quaternion.Euler(0,0,-3);
        var brand=FrontEndUI.Text(canvas.transform,"BLACKTOP",settings,56,true);FrontEndUI.Anchor(brand.rectTransform,new Vector2(.04f,.91f),new Vector2(.37f,.985f));
        var help=FrontEndUI.Text(canvas.transform,"7 ON 7  /  OWN THE BLOCK     •     ESC / B: BACK",settings,22);
        FrontEndUI.Anchor(help.rectTransform,new Vector2(.46f,.925f),new Vector2(.96f,.97f));help.alignment=TMPro.TextAlignmentOptions.MidlineRight;
        content=FrontEndUI.Panel(canvas.transform,"Screen",new Vector2(.04f,.07f),new Vector2(.96f,.89f),Color.clear);
        back=new InputAction("Menu back",binding:"<Keyboard>/escape");back.AddBinding("<Gamepad>/buttonEast");back.performed+=_=>Back();back.Enable();
        ShowMain();
        Debug.Log($"Blacktop front end initialized: {settings.teams.Length} teams. {Service.LoadMessage}",this);
    }
    void Clear(FrontEndScreen screen)
    {
        Screen=screen;PreferredFocus=null;
        if(view!=null){view.gameObject.SetActive(false);Destroy(view.gameObject);}
        view=FrontEndUI.Panel(content,screen.ToString(),Vector2.zero,Vector2.one,Color.clear);
    }
    public void ShowMain() {Clear(FrontEndScreen.Main);MainMenuView.Build(this,view);Focus();}
    public void ShowQuickPlay(FrontEndScreen stage) {Clear(stage);QuickPlayView.Build(this,view,stage);Focus();}
    public void ShowTeams() {Clear(FrontEndScreen.Teams);RosterBrowserView.Teams(this,view);Focus();}
    public void ShowRoster(string team,string selectedPlayer=null)
    {
        if(SelectedTeam!=team)SelectedPlayerId=null;
        if(selectedPlayer!=null)SelectedPlayerId=selectedPlayer;
        SelectedTeam=team;Session=null;Clear(FrontEndScreen.Roster);RosterBrowserView.Build(this,view,team);Focus();
    }
    public void Edit(RosterPlayer player,bool duplicate=false)
    {
        var assignment=Service.Team(SelectedTeam).active.Find(a=>a.playerId==player.record.id);
        Session=new PlayerEditorSession(player,SelectedTeam,assignment!=null?(int)assignment.offense:-1,assignment?.defense??0,duplicate);
        if(duplicate)Session.Draft.record.jerseyNumber=Service.NextNumber(SelectedTeam);
        Clear(FrontEndScreen.Editor);view.gameObject.AddComponent<PlayerEditorView>().Initialize(this,Session);Focus();
    }
    public void CreatePlayer() => Edit(PlayerEditorSession.New(settings,Service.NextNumber(SelectedTeam)));
    public void Back()
    {
        if(modal!=null){CloseModal();return;}
        switch(Screen)
        {
            case FrontEndScreen.Editor:
                if(Session.Dirty)Confirm("DISCARD UNSAVED CHANGES?","Your saved roster will stay unchanged.",()=>ShowRoster(SelectedTeam),"DISCARD");
                else ShowRoster(SelectedTeam);break;
            case FrontEndScreen.Roster:ShowTeams();break;
            case FrontEndScreen.Opponent:ShowQuickPlay(FrontEndScreen.HumanTeam);break;
            case FrontEndScreen.Confirmation:ShowQuickPlay(FrontEndScreen.Opponent);break;
            default:ShowMain();break;
        }
    }
    public void StartMatch()
    {
        if(!QuickPlayMatch.TryCreate(Service,HumanTeam,OpponentTeam,HumanSide,Field,out var match,out var error))
        {Message=error;ShowQuickPlay(FrontEndScreen.Confirmation);return;}
        FrontEndRuntime.Match=match;SceneManager.LoadScene(settings.gameplayScene);
    }
    public void Confirm(string title,string text,Action confirmed,string affirmative="CONFIRM")
    {
        if(modal!=null)return;
        priorFocus=EventSystem.current?.currentSelectedGameObject;
        var group=view.GetComponent<CanvasGroup>();
        if(group==null)group=view.gameObject.AddComponent<CanvasGroup>();
        group.interactable=false;group.blocksRaycasts=false; // Keep controller focus inside the dialog.
        modal=FrontEndUI.Panel(canvas.transform,"Confirm",Vector2.zero,Vector2.one,new Color(0,0,0,.88f)).gameObject;
        var frame=FrontEndUI.Panel(modal.transform,"Dialog",new Vector2(.25f,.29f),new Vector2(.75f,.72f),FrontEndUI.Ink);
        var list=FrontEndUI.Vertical(frame);FrontEndUI.Anchor(list,Vector2.zero,Vector2.one);
        FrontEndUI.Text(list,title,settings,38,true,78);FrontEndUI.Text(list,text,settings,25,false,104);
        FrontEndUI.Button(list,"CANCEL / KEEP EDITING",CloseModal,settings);
        FrontEndUI.Button(list,affirmative,()=>{CloseModal();confirmed();},settings);
        FrontEndUI.FocusFirst(frame);
    }
    void CloseModal()
    {
        if(modal==null)return;modal.SetActive(false);Destroy(modal);modal=null;
        if(view!=null&&view.TryGetComponent<CanvasGroup>(out var group)){group.interactable=true;group.blocksRaycasts=true;}
        if(priorFocus!=null&&priorFocus.activeInHierarchy)EventSystem.current.SetSelectedGameObject(priorFocus);
    }
    void Focus()
    {
        if(PreferredFocus!=null && EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(PreferredFocus);
        else FrontEndUI.FocusFirst(view);
    }
    public void Quit()
    {
        if (Application.isEditor)
        {
            if (FrontEndRuntime.EditorQuitRequested != null) FrontEndRuntime.EditorQuitRequested();
            else { Message="Quit requested. Stop Play Mode with the Editor Play button."; ShowMain(); }
        }
        else Application.Quit();
    }
    void OnDestroy() => back?.Dispose();
}
