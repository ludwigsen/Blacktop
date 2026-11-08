using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum FrontEndScreen { Main,HumanTeam,Opponent,Confirmation,Teams,Roster,Editor,Modes,Draft,Lineup,Review,PlayerLab,Preferences }

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
    public ExhibitionMode Mode { get; private set; }
    public MatchOptions HeadOptions { get; } = new MatchOptions();
    public MatchOptions PickupOptions { get; } = new MatchOptions();
    public MatchOptions Options => Mode == ExhibitionMode.Pickup ? PickupOptions : HeadOptions;
    public int PickupSide;
    public int MatchSide { get => Mode == ExhibitionMode.Pickup ? PickupSide : HumanSide; set { if(Mode==ExhibitionMode.Pickup)PickupSide=value;else HumanSide=value; } }
    public PickupDraft Draft { get; private set; }
    public System.Collections.Generic.List<LineupAssignment> PickupLineup { get; private set; }
    public QuickPlayMatch PreparedMatch { get; private set; }
    public int? TestSeed { get; set; }
    public PlayerEditorSession Session { get; private set; }
    public string Message { get; set; }
    public bool CanAuthorPlayers => settings.CanAuthorPlayers;
    public PlayerLabPage LabPage { get; private set; }
    public int RosterPage = -1;
    public MenuSound Sound { get; private set; }
    public FrontEndBackdrop Backdrop { get; private set; }
    RectTransform chrome;
    Canvas canvas;
    System.Collections.IEnumerator transition;
    RectTransform outgoing;
    bool goingBack;
    RectTransform content,view;
    GameObject modal;
    GameObject priorFocus;
    InputAction back;

    void Awake()
    {
        FrontEndUI.EnsureInput();Service=FrontEndRuntime.Open(settings);
        FrontEndRuntime.Match=null; // Entering the menu always ends the prior match's transient context.
        Message=Service.LoadMessage;
        canvas=FrontEndUI.CreateCanvas("Blacktop front end");canvas.transform.SetParent(transform,false);
        Sound=gameObject.AddComponent<MenuSound>();
        var background=FrontEndUI.Panel(canvas.transform,"Field slideshow",Vector2.zero,Vector2.one,Color.black);
        Backdrop=background.gameObject.AddComponent<FrontEndBackdrop>();Backdrop.Initialize(settings);
        chrome=FrontEndUI.Panel(canvas.transform,"Submenu chrome",Vector2.zero,Vector2.one,Color.clear);
        chrome.GetComponent<Image>().raycastTarget=false;
        var wordmark = UITheme.Library?.wordmark;
        var brand = StreetArt.Layer(chrome, "Secondary Blacktop wordmark", wordmark,
            new Vector2(.04f, 1), new Vector2(.04f, 1), UIColorRole.TextPrimary);
        // Keep the established reference-pixel top inset and the sprite's native proportions.
        const float brandHeight = 80f;
        float width = wordmark == null ? 0 : brandHeight * wordmark.rect.width / wordmark.rect.height;
        brand.rectTransform.offsetMin = new Vector2(0, -settings.submenuLogoTopPadding - brandHeight);
        brand.rectTransform.offsetMax = new Vector2(width, -settings.submenuLogoTopPadding);
        var help=FrontEndUI.Text(chrome,"7 ON 7  /  OWN THE BLOCK     •     ESC / B: BACK",settings,22);
        FrontEndUI.Anchor(help.rectTransform,new Vector2(.46f,.885f),new Vector2(.96f,.94f));help.alignment=TMPro.TextAlignmentOptions.MidlineRight;
        content=FrontEndUI.Panel(canvas.transform,"Screen",new Vector2(.04f,.055f),new Vector2(.96f,.89f),Color.clear);
        content.GetComponent<Image>().raycastTarget=false;
        back=new InputAction("Menu back",binding:"<Keyboard>/escape");back.AddBinding("<Gamepad>/buttonEast");back.performed+=_=>Back();back.Enable();
        ShowMain();
        Debug.Log($"Blacktop front end initialized: {settings.teams.Length} teams. {Service.LoadMessage}",this);
    }
    void Clear(FrontEndScreen screen)
    {
        Screen=screen;PreferredFocus=null;
        if(transition!=null){StopCoroutine(transition);transition=null;}
        if(outgoing!=null)Destroy(outgoing.gameObject);
        outgoing=view;
        if(outgoing!=null)
        {
            // Retire the old page's controls immediately while its artwork fades away.
            foreach(var control in outgoing.GetComponentsInChildren<Selectable>())control.interactable=false;
            var oldGroup=outgoing.GetComponent<CanvasGroup>()??outgoing.gameObject.AddComponent<CanvasGroup>();
            oldGroup.interactable=false;oldGroup.blocksRaycasts=false;
            outgoing.gameObject.SetActive(false); // Retired controls and preview cameras leave input queries immediately.
        }
        bool main=screen==FrontEndScreen.Main;
        chrome.gameObject.SetActive(!main);Backdrop.SetSubmenu(!main);
        FrontEndUI.Anchor(content,new Vector2(.04f,main?0:.055f),new Vector2(.96f,main?1:1-(settings.submenuLogoTopPadding+125)/1080f));
        view=FrontEndUI.Panel(content,screen.ToString(),Vector2.zero,Vector2.one,Color.clear);
        view.GetComponent<Image>().raycastTarget=false;
        if(!main)
        {
            var surface=FrontEndUI.Panel(view,"Page surface",Vector2.zero,Vector2.one,UIColorRole.BackgroundPrimary,.87f);
            surface.GetComponent<Image>().raycastTarget=false;
            FrontEndUI.Mask(surface, UITheme.Library?.asphaltGrain);
            StreetArt.Layer(view, "Page chalk frame", UITheme.Library?.chalkFrame,
                Vector2.zero, Vector2.one, UIColorRole.BorderSubtle, .7f);
        }
        var progress=chrome.GetComponent<SetupProgress>()??chrome.gameObject.AddComponent<SetupProgress>();
        progress.Show(screen,settings);
    }
    public void ShowPreferences(){Clear(FrontEndScreen.Preferences);MainMenuView.Preferences(this,view);Focus();}
    public void ShowPlayerLab(string team=null,string id=null)
    {
        team??=SelectedTeam??settings.teams[0].id;
        var roster=Service.Team(team);var player=roster.players.Find(p=>p.record.id==(id??SelectedPlayerId))??roster.players.FirstOrDefault();
        if(player==null){ShowTeams();return;}
        SelectedTeam=team;SelectedPlayerId=player.record.id;RosterPage=-1;
        var assignment=roster.active.Find(a=>a.playerId==player.record.id);
        Session=new PlayerEditorSession(player,team,assignment!=null?(int)assignment.offense:-1,assignment?.defense??0);
        ShowLabPage(PlayerLabPage.Summary);
    }
    public void SelectLabPlayer(string team,string id)=>GuardLabChange(()=>ShowPlayerLab(team,id));
    public void GuardLabChange(Action action)
    {
        if(Session!=null&&Session.Dirty)Confirm("DISCARD UNSAVED CHANGES?","Save this player before switching, or discard the draft.",action,"DISCARD");
        else action();
    }
    public void ReloadLabPlayer(string id=null)=>ShowPlayerLab(Session.TeamId,id??Session.Draft.record.id);
    public void ShowLabPage(PlayerLabPage page)
    {
        if(Session==null){ShowPlayerLab();return;}
        if(!CanAuthorPlayers&&(page==PlayerLabPage.Attributes||page==PlayerLabPage.Identity))page=PlayerLabPage.Summary;
        if(Session.HasUncommittedInput&&page!=LabPage)
        {
            var next=page;
            Confirm("INVALID INPUT","Correct the current input, or discard its invalid text to continue.",()=>{Session.HasUncommittedInput=false;ShowLabPage(next);},"DISCARD INVALID TEXT");return;
        }
        LabPage=page;Clear(FrontEndScreen.PlayerLab);view.gameObject.AddComponent<PlayerLabView>().Initialize(this,page);Focus();
    }
    public void ShowMain() {Clear(FrontEndScreen.Main);MainMenuView.Build(this,view);Focus();}
    public void ShowModes() { PreparedMatch=null;Clear(FrontEndScreen.Modes);MainMenuView.Modes(this,view);Focus(); }
    public void BeginHeadToHead() {Mode=ExhibitionMode.HeadToHead;PreparedMatch=null;ShowQuickPlay(FrontEndScreen.HumanTeam);}
    public void BeginPickup() {Mode=ExhibitionMode.Pickup;PreparedMatch=null;Draft=new PickupDraft(Service);PickupLineup=null;ShowDraft();}
    public void ShowDraft() {Clear(FrontEndScreen.Draft);view.gameObject.AddComponent<PickupDraftView>().Initialize(this,view);Focus();}
    public void ShowLineup()
    {
        if(Draft.Selected.Count!=7){Message="Draft exactly seven players.";ShowDraft();return;}
        PickupLineup??=Draft.SuggestHuman();Clear(FrontEndScreen.Lineup);MatchSettingsView.HumanLineup(this,view);Focus();
    }
    public void DraftChanged() {PickupLineup=null;PreparedMatch=null;}
    public void ShowSettings() {Clear(FrontEndScreen.Confirmation);MatchSettingsView.Build(this,view);Focus();}
    public void ShowReview() {Clear(FrontEndScreen.Review);MatchSettingsView.Review(this,view);Focus();}
    public void ShowQuickPlay(FrontEndScreen stage) {Clear(stage);QuickPlayView.Build(this,view,stage);Focus();}
    public void ShowTeams() {Clear(FrontEndScreen.Teams);RosterBrowserView.Teams(this,view);Focus();}
    public void ShowRoster(string team,string selectedPlayer=null)
    {
        if(!CanAuthorPlayers){ShowPlayerLab(team);return;}
        if(SelectedTeam!=team)SelectedPlayerId=null;
        if(selectedPlayer!=null)SelectedPlayerId=selectedPlayer;
        SelectedTeam=team;Session=null;Clear(FrontEndScreen.Roster);RosterBrowserView.Build(this,view,team);Focus();
    }
    public void Edit(RosterPlayer player,bool duplicate=false)
    {
        if(!CanAuthorPlayers){ShowPlayerLab();return;}
        var assignment=Service.Team(SelectedTeam).active.Find(a=>a.playerId==player.record.id);
        Session=new PlayerEditorSession(player,SelectedTeam,assignment!=null?(int)assignment.offense:-1,assignment?.defense??0,duplicate);
        if(duplicate)Session.Draft.record.jerseyNumber=Service.NextNumber(SelectedTeam);
        Clear(FrontEndScreen.Editor);view.gameObject.AddComponent<PlayerEditorView>().Initialize(this,Session);Focus();
    }
    public void CreatePlayer() => Edit(PlayerEditorSession.New(settings,Service.NextNumber(SelectedTeam)));
    public void Back()
    {
        if(modal!=null){CloseModal();return;}
        goingBack=true;
        switch(Screen)
        {
            case FrontEndScreen.PlayerLab:
                if(LabPage!=PlayerLabPage.Summary)ShowLabPage(PlayerLabPage.Summary);
                else GuardLabChange(ShowMain);break;
            case FrontEndScreen.Editor:
                if(Session.Dirty)Confirm("DISCARD UNSAVED CHANGES?","Your saved roster will stay unchanged.",()=>ShowRoster(SelectedTeam),"DISCARD");
                else ShowRoster(SelectedTeam);break;
            case FrontEndScreen.Roster:ShowTeams();break;
            case FrontEndScreen.HumanTeam:ShowModes();break;
            case FrontEndScreen.Opponent:ShowQuickPlay(FrontEndScreen.HumanTeam);break;
            case FrontEndScreen.Draft:ShowModes();break;
            case FrontEndScreen.Lineup:ShowDraft();break;
            case FrontEndScreen.Confirmation:if(Mode==ExhibitionMode.Pickup)ShowLineup();else ShowQuickPlay(FrontEndScreen.Opponent);break;
            case FrontEndScreen.Review:ShowSettings();break;
            default:ShowMain();break;
        }
    }
    public void StartMatch()
    {
        if(PreparedMatch==null){PrepareMatch();return;}
        FrontEndRuntime.Match=PreparedMatch;SceneManager.LoadScene(settings.gameplayScene);
    }
    public void PrepareMatch()
    {
        var random=new SeededMatchRandom(TestSeed??Environment.TickCount);
        QuickPlayMatch match;string error;
        bool success=Mode==ExhibitionMode.Pickup
            ? QuickPlayMatch.TryPickup(Service,Draft.Selected,PickupLineup,PickupSide,Options,random,out match,out error)
            : QuickPlayMatch.TryHeadToHead(Service,HumanTeam,OpponentTeam,HumanSide,Options,random,out match,out error);
        if(!success){Message=error;ShowSettings();return;}
        PreparedMatch=match;Message="";ShowReview();
    }
    public void UpdatePreparedLineups(System.Collections.Generic.List<LineupAssignment> home,System.Collections.Generic.List<LineupAssignment> away)
    {
        if(PreparedMatch.TryWithLineups(home,away,Service,out var match,out var error))PreparedMatch=match;
        else Message=error;
    }
    public void Confirm(string title,string text,Action confirmed,string affirmative="CONFIRM")
    {
        if(modal!=null)return;Backdrop.SetSubmenu(true);
        priorFocus=EventSystem.current?.currentSelectedGameObject;
        var group=view.GetComponent<CanvasGroup>();
        if(group==null)group=view.gameObject.AddComponent<CanvasGroup>();
        group.interactable=false;group.blocksRaycasts=false; // Keep controller focus inside the dialog.
        modal=FrontEndUI.Panel(canvas.transform,"Confirm",Vector2.zero,Vector2.one,UIColorRole.ShadowPrimary,.92f).gameObject;
        var frame=FrontEndUI.Panel(modal.transform,"Dialog",new Vector2(.25f,.29f),new Vector2(.75f,.72f),UIColorRole.BackgroundPrimary);
        var list=FrontEndUI.Vertical(frame);FrontEndUI.Anchor(list,Vector2.zero,Vector2.one);
        FrontEndUI.Text(list,title,settings,38,true,78);FrontEndUI.Text(list,text,settings,25,false,104);
        FrontEndUI.Button(list,"CANCEL / KEEP EDITING",CloseModal,settings);
        FrontEndUI.Button(list,affirmative,()=>{CloseModal();confirmed();},settings);
        FrontEndUI.FocusFirst(frame);
    }
    void CloseModal()
    {
        if(modal==null)return;modal.SetActive(false);Destroy(modal);modal=null;Backdrop.SetSubmenu(Screen!=FrontEndScreen.Main);
        if(view!=null&&view.TryGetComponent<CanvasGroup>(out var group)){group.interactable=true;group.blocksRaycasts=true;}
        if(priorFocus!=null&&priorFocus.activeInHierarchy)EventSystem.current.SetSelectedGameObject(priorFocus);
    }
    void Focus()
    {
        transition=AnimatePages(goingBack?-1:1);goingBack=false;StartCoroutine(transition);
        if(PreferredFocus!=null && EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(PreferredFocus);
        else FrontEndUI.FocusFirst(view);
    }
    System.Collections.IEnumerator AnimatePages(int direction)
    {
        var incoming=view;
        var group=incoming.gameObject.AddComponent<CanvasGroup>();
        float duration=Mathf.Max(.1f,settings.menuTransitionSeconds);
        for(float time=0;time<duration;time+=Time.unscaledDeltaTime)
        {
            float t=Mathf.SmoothStep(0,1,time/duration);
            group.alpha=t;incoming.anchoredPosition=new Vector2((1-t)*70*direction,0);
            if(outgoing!=null)
            {
                outgoing.GetComponent<CanvasGroup>().alpha=1-t;
                outgoing.anchoredPosition=new Vector2(-t*40*direction,0);
            }
            yield return null;
        }
        group.alpha=1;incoming.anchoredPosition=Vector2.zero;
        if(outgoing!=null){outgoing.gameObject.SetActive(false);Destroy(outgoing.gameObject);outgoing=null;}
        transition=null;
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
