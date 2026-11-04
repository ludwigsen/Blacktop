using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Unified display for score, team meters, play clock, downs and event announcements.
// Clean geometry and Barlow typography bind to the shared UI theme; team accents remain
// TeamIdentity-owned. All values/events come from existing gameplay owners. The play
// clock is polled because it changes continuously; the remaining bindings are event-driven.
// Legacy serialized artwork/font fields remain compatible with existing scene references.
public class GameHUD : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] HUDTheme theme;

    [Header("Scorebug")]
    [SerializeField, Range(0.02f, 0.10f)] float horizontalMargin = 0.05f;
    [SerializeField, Range(0.05f, 0.20f)] float heightPercent = 0.12f;
    [SerializeField, Range(0.2f, 0.45f)] float teamPanelWidth = 0.44f; // each side; center strip gets the rest

    [SerializeField, Min(0f)] float topInset = 24f; // Canvas reference pixels, below the screen edge.

    [Header("Panel Backgrounds")]
    [Tooltip("Full scorebug texture. Assign Assets/_Project/Art/scorebug_base.png.")]
    [SerializeField] Sprite scorebugBase;
    [Tooltip("Neutral team-panel art, tinted by TeamIdentity. Assign Assets/_Project/Art/scorebug_base.png.")]
    [SerializeField] Sprite team1PanelBackground;
    [Tooltip("Optional alternate Team 2 background. When empty, Team 1's left-side sprite is mirrored automatically.")]
    [SerializeField] Sprite team2PanelBackground;

    [Header("Typography")]
    [SerializeField] int teamNameFontSize = 64;
    [SerializeField] int scoreFontSize = 84;
    [SerializeField] int gamebreakerLabelFontSize = 20;
    [SerializeField] int centerFontSize = 30;

    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.82f);

    [Header("Game Over")]
    [Tooltip("Ignore Confirm for this long after the final whistle so a mashed button can't skip the winner screen.")]
    [SerializeField] float restartInputDelay = 1.5f;

    TeamScorePanel team1Panel;
    TeamScorePanel team2Panel;
    TextMeshProUGUI clockText;
    TextMeshProUGUI downText;

    GameObject gameOverRoot;
    TextMeshProUGUI winnerText;
    TextMeshProUGUI finalScoreText;
    GameObject postPlayRoot;
    TextMeshProUGUI postPlayText;
    InputSystem_Actions controls;
    float gameOverTime;
    GameAnnouncement announcement;
    int driveBefore;float losBefore,yardsBefore;
    readonly bool[] gamebreakerWasActive=new bool[2];
    void CapturePrePlay()
    {
        var ps=PlayState.Instance;if(ps==null)return;
        driveBefore=ps.PossessionTeamId;losBefore=ps.CurrentLineOfScrimmageZ;
        yardsBefore=DownsTracker.Instance!=null?DownsTracker.Instance.YardsToGo:float.PositiveInfinity;
    }

    void Awake()
    {
        BuildHUD();

        // Same pattern as PlayCallSelector: each script owns its own actions instance.
        controls = new InputSystem_Actions();
        controls.Player.Confirm.performed += HandleConfirm;
    }

    void OnEnable() => controls?.Player.Enable();
    void OnDisable() => controls?.Player.Disable();

    // Start(), not OnEnable() — same reasoning as DownsTracker/ScoreBoard: guarantees
    // every relevant Awake() has already run regardless of GameObject order in the
    // scene. Identity is applied here rather than in Build() for the same reason —
    // PlayState.Instance isn't guaranteed to exist yet during GameHUD's own Awake().
    void Start()
    {
        if (PlayState.Instance != null)
        {
            team1Panel.ApplyIdentity(PlayState.Instance.IdentityFor(0));
            team2Panel.ApplyIdentity(PlayState.Instance.IdentityFor(1));

            PlayState.Instance.OnGameOver += ShowGameOver;
            PlayState.Instance.OnPlayReset+=CapturePrePlay;CapturePrePlay();
            PlayState.Instance.OnPossessionChanged+=RefreshPossession;RefreshPossession(PlayState.Instance.PossessionTeamId);
            PlayState.Instance.OnTeamMeterChanged += RefreshTeamMeter;
            PlayState.Instance.OnPostPlayPresentationStarted += ShowPostPlayResult;
            PlayState.Instance.OnPostPlayPresentationCompleted += HidePostPlayResult;
            RefreshTeamMeter(0, PlayState.Instance.MeterFor(0));
            RefreshTeamMeter(1, PlayState.Instance.MeterFor(1));
        }

        if (DownsTracker.Instance != null)
        {
            DownsTracker.Instance.OnDownsChanged += RefreshDowns;
            RefreshDowns();
        }

        if (ScoreBoard.Instance != null)
        {
            ScoreBoard.Instance.OnScoreChanged += RefreshScore;
            RefreshScore();
        }
    }

    void Update() => RefreshClock();

    void OnDestroy()
    {
        if (PlayState.Instance != null)
        {
            PlayState.Instance.OnTeamMeterChanged -= RefreshTeamMeter;
            PlayState.Instance.OnGameOver -= ShowGameOver;
            PlayState.Instance.OnPlayReset-=CapturePrePlay;
            PlayState.Instance.OnPossessionChanged-=RefreshPossession;
            PlayState.Instance.OnPostPlayPresentationStarted -= ShowPostPlayResult;
            PlayState.Instance.OnPostPlayPresentationCompleted -= HidePostPlayResult;
        }
        if (controls != null)
        {
            controls.Player.Confirm.performed -= HandleConfirm;
            controls.Dispose();
        }
        if (DownsTracker.Instance != null) DownsTracker.Instance.OnDownsChanged -= RefreshDowns;
        if (ScoreBoard.Instance != null) ScoreBoard.Instance.OnScoreChanged -= RefreshScore;
    }

    void BuildHUD()
    {
        var canvasGO = new GameObject("GameHUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.matchWidthOrHeight=.5f;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        BuildScorebug(canvasGO.transform);
        BuildPostPlayResult(canvasGO.transform);
        BuildGameOverOverlay(canvasGO.transform);
    }

    void BuildPostPlayResult(Transform parent)
    {
        postPlayRoot = new GameObject("PostPlayResult", typeof(Image));
        postPlayRoot.transform.SetParent(parent, false);
        var image = postPlayRoot.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.78f);
        var rt = postPlayRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.32f, 0.72f);
        rt.anchorMax = new Vector2(0.68f, 0.79f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var textGO = new GameObject("Result", typeof(TextMeshProUGUI));
        textGO.transform.SetParent(postPlayRoot.transform, false);
        postPlayText = ConfigureText(textGO, theme != null ? theme.legibleFont : null, 34);
        AnchorStrip(postPlayText.rectTransform, Vector2.zero, Vector2.one);
        postPlayRoot.SetActive(false);
        announcement=postPlayRoot.AddComponent<GameAnnouncement>();announcement.Initialize(postPlayText);
    }

    // Full-screen dim + winner + final score + prompt. Built hidden, last, with its own
    // override-sorting canvas so it covers the scorebug and play-call list. Same plain-
    // rectangle placeholder approach as the rest of this HUD.
    void BuildGameOverOverlay(Transform parent)
    {
        gameOverRoot = new GameObject("GameOver", typeof(Image), typeof(Canvas));
        gameOverRoot.transform.SetParent(parent, false);

        var overlayCanvas = gameOverRoot.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 100;

        gameOverRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);
        AnchorStrip(gameOverRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        var legibleFont = theme != null ? theme.legibleFont : null;

        var winnerGO = new GameObject("Winner", typeof(TextMeshProUGUI));
        winnerGO.transform.SetParent(gameOverRoot.transform, false);
        winnerText = ConfigureText(winnerGO, legibleFont, 110);
        AnchorStrip(winnerGO.GetComponent<RectTransform>(), new Vector2(0f, 0.55f), new Vector2(1f, 0.8f));

        var scoreGO = new GameObject("FinalScore", typeof(TextMeshProUGUI));
        scoreGO.transform.SetParent(gameOverRoot.transform, false);
        finalScoreText = ConfigureText(scoreGO, legibleFont, 56);
        AnchorStrip(scoreGO.GetComponent<RectTransform>(), new Vector2(0f, 0.42f), new Vector2(1f, 0.55f));

        var promptGO = new GameObject("RestartPrompt", typeof(TextMeshProUGUI));
        promptGO.transform.SetParent(gameOverRoot.transform, false);
        var prompt = ConfigureText(promptGO, legibleFont, 30);
        prompt.text = "PRESS CONFIRM TO PLAY AGAIN";
        ThemeableUI.Bind(prompt,UIColorRole.TextSecondary,UITypeRole.Body);
        AnchorStrip(promptGO.GetComponent<RectTransform>(), new Vector2(0f, 0.25f), new Vector2(1f, 0.35f));

        gameOverRoot.SetActive(false);
    }

    void ShowGameOver(int winnerTeamId)
    {
        if (gameOverRoot == null) return;

        var identity = PlayState.Instance != null ? PlayState.Instance.IdentityFor(winnerTeamId) : null;
        string winnerName = identity != null ? identity.teamName : $"TEAM {winnerTeamId + 1}";

        winnerText.text = $"{winnerName.ToUpperInvariant()} WINS";
        winnerText.color = identity != null ? identity.accentColor : Color.white;

        int s1 = ScoreBoard.Instance != null ? ScoreBoard.Instance.Team1Score : 0;
        int s2 = ScoreBoard.Instance != null ? ScoreBoard.Instance.Team2Score : 0;
        finalScoreText.text = $"FINAL  {s1} - {s2}";

        gameOverTime = Time.unscaledTime;
        gameOverRoot.SetActive(true);
    }

    void ShowPostPlayResult(PlayState.PlayEndReason reason)
    {
        if (postPlayRoot == null || PlayState.Instance == null || PlayState.Instance.IsGameOver) return;
        postPlayText.text = reason switch
        {
            PlayState.PlayEndReason.Touchdown => "TOUCHDOWN!",
            PlayState.PlayEndReason.Safety => "SAFETY",
            PlayState.PlayEndReason.Interception => "TURNOVER!",
            PlayState.PlayEndReason.Incomplete => "INCOMPLETE",
            PlayState.PlayEndReason.OutOfBounds => "OUT OF BOUNDS",
            _ => "PLAY DEAD"
        };
        var ps=PlayState.Instance;
        bool turnover=ps.PossessionTeamId!=driveBefore&&reason!=PlayState.PlayEndReason.Touchdown&&reason!=PlayState.PlayEndReason.Safety;
        // Scoring resets the LOS for the next drive; that reset is not a first-down gain.
        bool firstDown=reason!=PlayState.PlayEndReason.Touchdown&&reason!=PlayState.PlayEndReason.Safety&&
            reason!=PlayState.PlayEndReason.Interception&&!turnover&&
            ps.DirectionFor(driveBefore).YardsPastLOS(ps.CurrentLineOfScrimmageZ,losBefore)>=yardsBefore;
        var kind=reason==PlayState.PlayEndReason.Touchdown?AnnouncementKind.Touchdown:
            reason==PlayState.PlayEndReason.Interception||turnover?AnnouncementKind.Turnover:
            firstDown?AnnouncementKind.FirstDown:AnnouncementKind.Information;
        if(turnover)postPlayText.text="TURNOVER!";else if(firstDown)postPlayText.text="FIRST DOWN!";
        announcement.Show(kind,postPlayText.text);
    }

    void HidePostPlayResult(PlayState.PlayEndReason _)
    {
        if (postPlayRoot != null) postPlayRoot.SetActive(false);
    }

    // Restart = reload the scene. Every singleton (PlayState, ScoreBoard, BallController...)
    // sets itself up in Awake, so a reload is a guaranteed-clean rematch with no reset code
    // to keep in sync as systems get added. SampleScene is in Build Settings, so this is
    // safe in the editor and in builds.
    void HandleConfirm(InputAction.CallbackContext _)
    {
        if (PlayState.Instance == null || !PlayState.Instance.IsGameOver) return;
        if (Time.unscaledTime - gameOverTime < restartInputDelay) return;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void BuildScorebug(Transform parent)
    {
        // Anchors keep the art at a consistent broadcast-safe position at every aspect
        // ratio. Optional legacy full-width art sits behind the team-colored plates.
        var scorebugGO = new GameObject("Scorebug", typeof(Image));
        scorebugGO.transform.SetParent(parent, false);

        var scorebugRT = scorebugGO.GetComponent<RectTransform>();
        scorebugRT.anchorMin = new Vector2(horizontalMargin, 1f - heightPercent);
        scorebugRT.anchorMax = new Vector2(1f - horizontalMargin, 1f);
        scorebugRT.offsetMin = new Vector2(0f, -topInset);
        scorebugRT.offsetMax = new Vector2(0f, -topInset);
        var baseImage = scorebugGO.GetComponent<Image>();
        baseImage.sprite = null; // Existing serialized artwork is retained, but clean geometry owns the active silhouette.
        baseImage.type = Image.Type.Simple;
        ThemeableUI.Bind(baseImage,UIColorRole.ShadowPrimary);
        // Team plates supply the same clean silhouette in both themes.
        baseImage.enabled = true;
        baseImage.raycastTarget = false;

        BuildTeamPanel(scorebugGO.transform, isTeam1: true);
        BuildGameStatePanel(scorebugGO.transform);
        BuildTeamPanel(scorebugGO.transform, isTeam1: false);
    }

    void BuildTeamPanel(Transform parent, bool isTeam1)
    {
        var teamGO = new GameObject(isTeam1 ? "Team1" : "Team2", typeof(RectTransform));
        teamGO.transform.SetParent(parent, false);

        var rt = teamGO.GetComponent<RectTransform>();
        rt.anchorMin = isTeam1 ? new Vector2(0f, 0f) : new Vector2(1f - teamPanelWidth, 0f);
        rt.anchorMax = isTeam1 ? new Vector2(teamPanelWidth, 1f) : new Vector2(1f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var panel = teamGO.AddComponent<TeamScorePanel>();
        panel.Build(
            displayFont: theme != null ? theme.displayFont : null,
            legibleFont: theme != null ? theme.legibleFont : null,
            nameFontSize: teamNameFontSize,
            scoreFontSize: scoreFontSize,
            labelFontSize: gamebreakerLabelFontSize,
            reverseFill: !isTeam1,
            backgroundSprite: null,
            flipBackground: !isTeam1 && team2PanelBackground == null,
            textColor: theme != null ? theme.scorebugTextColor : Color.white,
            outlineWidth: .07f,
            logoScale: theme != null ? theme.logoScale : 1.25f,
            logoOutwardTilt: theme != null ? theme.logoOutwardTilt : 20f,
            nameOutwardShift: theme != null ? theme.nameOutwardShift : 0.10f,
            nameScoreGapReduction: theme != null ? theme.nameScoreGapReduction : 0.04f
        );
        panel.SetScore(0);

        if (isTeam1) team1Panel = panel; else team2Panel = panel;
    }

    void BuildGameStatePanel(Transform parent)
    {
        var centerGO = new GameObject("GameState", typeof(Image), typeof(Outline));
        centerGO.transform.SetParent(parent, false);
        var rt = centerGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(teamPanelWidth, 0f);
        rt.anchorMax = new Vector2(1f - teamPanelWidth, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // An opaque center keeps field action from reducing down/distance readability.
        ThemeableUI.Bind(centerGO.GetComponent<Image>(),UIColorRole.BackgroundPrimary);
        centerGO.GetComponent<Image>().raycastTarget = false;
        var border = centerGO.GetComponent<Outline>();
        border.effectColor = new Color(0.65f, 0.65f, 0.62f, 0.8f);
        border.effectDistance = new Vector2(1f, -1f);
        var legibleFont = theme != null ? theme.legibleFont : null;

        var clockGO = new GameObject("PlayClock", typeof(TextMeshProUGUI));
        clockGO.transform.SetParent(centerGO.transform, false);
        clockText = ConfigureText(clockGO, legibleFont, Mathf.RoundToInt(centerFontSize * 0.62f));
        ThemeableUI.Bind(clockText,UIColorRole.TextSecondary,UITypeRole.Navigation);
        AnchorStrip(clockGO.GetComponent<RectTransform>(), new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.29f));

        var downGO = new GameObject("DownAndDistance", typeof(TextMeshProUGUI));
        downGO.transform.SetParent(centerGO.transform, false);
        downText = ConfigureText(downGO, legibleFont, centerFontSize);
        AnchorStrip(downGO.GetComponent<RectTransform>(), new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.94f));
    }

    // The shared theme supplies the native font; null still permits TMP fixture fallback.
    static TextMeshProUGUI ConfigureText(GameObject go, TMP_FontAsset font, int fontSize)
    {
        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Normal;
        ThemeableUI.Bind(text,UIColorRole.TextPrimary,fontSize>=56?UITypeRole.Numeral:UITypeRole.Navigation);
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize * 0.65f;
        text.fontSizeMax = fontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    static void AnchorStrip(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void RefreshPossession(int teamId)
    {
        team1Panel?.SetPossession(teamId==0);team2Panel?.SetPossession(teamId==1);
    }

    void RefreshScore()
    {
        if (ScoreBoard.Instance == null) return;
        team1Panel?.SetScore(ScoreBoard.Instance.Team1Score);
        team2Panel?.SetScore(ScoreBoard.Instance.Team2Score);
    }

    void RefreshTeamMeter(int teamId, float value)
    {
        bool active=PlayState.Instance.IsGamebreakerActiveFor(teamId);
        (teamId == 0 ? team1Panel : team2Panel)?.SetGamebreaker(value,active);
        if(active&&!gamebreakerWasActive[teamId]&&announcement!=null&&!PlayState.Instance.IsPlayEnding)
            announcement.Show(AnnouncementKind.Gamebreaker,"GAMEBREAKER!",1.35f);
        gamebreakerWasActive[teamId]=active;
    }

    void RefreshClock()
    {
        if (clockText == null || PlayState.Instance == null) return;
        float remaining = Mathf.Max(0f, PlayState.Instance.PlayClockRemaining);
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        clockText.text = $"PLAY CLOCK  {minutes}:{seconds:00}";
    }

    void RefreshDowns()
    {
        if (downText == null || DownsTracker.Instance == null) return;
        var downs = DownsTracker.Instance;
        string distance = downs.IsGoalToGo ? "GOAL" : Mathf.CeilToInt(downs.YardsToGo).ToString();
        downText.text = $"{Ordinal(downs.CurrentDown).ToUpperInvariant()} & {distance}\n<size=70%>ON {Mathf.RoundToInt(downs.YardLine)}</size>";
    }

    static string Ordinal(int down) => down switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        4 => "4th",
        _ => $"{down}th"
    };
}
