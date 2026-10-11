using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Single unified HUD for everything that isn't play selection: team score + Gamebreaker
// on each side, play clock + down/distance/field position in the center. Structure
// matches the concept board's HUD breakdown, minus quarter (no quarter system — the
// center clock is PlayState's PLAY clock only) and minus the diagonal/grunge panel art
// (needs real sprite assets — this is the honest placeholder pass: plain rectangles,
// not a faked paint texture).
//
// Typeface comes from a HUDTheme asset (assign one in the Inspector once you've built
// TMP Font Assets from your real fonts); leave it unassigned and everything falls back
// to TMP's default font with zero errors. Center panel (clock/downs) uses
// theme.legibleFont; TeamScorePanel uses theme.displayFont — matches the concept
// board's own typographic split.
//
// Pure display — every value comes from ScoreBoard/DownsTracker/PlayState's already-
// resolved state, with one deliberate exception: the play clock is polled every frame
// in Update() rather than event-driven, since it changes continuously with no natural
// discrete "changed" event to hook.
//
// SETUP: drop on GameManager, alongside PlayState/DownsTracker/ScoreBoard. Assign a
// HUDTheme (Assets > Create > Blacktop > HUD Theme) and PlayState's
// Team1Identity/Team2Identity for real fonts/names/colors.
public class GameHUD : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] HUDTheme theme;

    [Header("Scorebug")]
    [SerializeField, Range(0.02f, 0.10f)] float horizontalMargin = 0.05f;
    [SerializeField, Range(0.05f, 0.20f)] float heightPercent = 0.10f;
    [SerializeField, Range(0.2f, 0.45f)] float teamPanelWidth = 0.35f; // each side; center strip gets the rest

    [Header("Panel Backgrounds")]
    [Tooltip("Background art behind Team 1's logo/name/score. Assign Assets/_Project/Art/ScorebugBG_Left.png.")]
    [SerializeField] Sprite team1PanelBackground;
    [Tooltip("Mirrored background for Team 2's panel, once that asset exists. Leave unassigned for now — falls back to the plain background color.")]
    [SerializeField] Sprite team2PanelBackground;

    [Header("Typography")]
    [SerializeField] int teamNameFontSize = 22;
    [SerializeField] int scoreFontSize = 40;
    [SerializeField] int gamebreakerLabelFontSize = 14;
    [SerializeField] int centerFontSize = 24;

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
    InputSystem_Actions controls;
    float gameOverTime;

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
            PlayState.Instance.OnTeamMeterChanged += RefreshTeamMeter;
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
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        BuildScorebug(canvasGO.transform);
        BuildGameOverOverlay(canvasGO.transform);
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

        var displayFont = theme != null ? theme.displayFont : null;
        var legibleFont = theme != null ? theme.legibleFont : null;

        var winnerGO = new GameObject("Winner", typeof(TextMeshProUGUI));
        winnerGO.transform.SetParent(gameOverRoot.transform, false);
        winnerText = ConfigureText(winnerGO, displayFont, 110);
        AnchorStrip(winnerGO.GetComponent<RectTransform>(), new Vector2(0f, 0.55f), new Vector2(1f, 0.8f));

        var scoreGO = new GameObject("FinalScore", typeof(TextMeshProUGUI));
        scoreGO.transform.SetParent(gameOverRoot.transform, false);
        finalScoreText = ConfigureText(scoreGO, legibleFont, 56);
        AnchorStrip(scoreGO.GetComponent<RectTransform>(), new Vector2(0f, 0.42f), new Vector2(1f, 0.55f));

        var promptGO = new GameObject("RestartPrompt", typeof(TextMeshProUGUI));
        promptGO.transform.SetParent(gameOverRoot.transform, false);
        var prompt = ConfigureText(promptGO, legibleFont, 30);
        prompt.text = "PRESS CONFIRM TO PLAY AGAIN";
        prompt.color = new Color(1f, 1f, 1f, 0.7f);
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
        // position: fixed; top: 0; padding: 5% left/right; height: 10% — anchors, not a
        // pixel sizeDelta, so it's genuinely that fraction of the real screen at any
        // resolution/aspect ratio. Plain rectangle, not the concept's diagonal-cut
        // panel — that needs a sprite/mesh asset, not something worth faking in code.
        var scorebugGO = new GameObject("Scorebug", typeof(Image));
        scorebugGO.transform.SetParent(parent, false);

        var scorebugRT = scorebugGO.GetComponent<RectTransform>();
        scorebugRT.anchorMin = new Vector2(horizontalMargin, 1f - heightPercent);
        scorebugRT.anchorMax = new Vector2(1f - horizontalMargin, 1f);
        scorebugRT.offsetMin = Vector2.zero;
        scorebugRT.offsetMax = Vector2.zero;
        scorebugGO.GetComponent<Image>().color = backgroundColor;

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
            nameFontSize: teamNameFontSize,
            scoreFontSize: scoreFontSize,
            labelFontSize: gamebreakerLabelFontSize,
            reverseFill: !isTeam1,
            backgroundSprite: isTeam1 ? team1PanelBackground : team2PanelBackground
        );
        panel.SetScore(0);

        if (isTeam1) team1Panel = panel; else team2Panel = panel;
    }

    void BuildGameStatePanel(Transform parent)
    {
        var centerGO = new GameObject("GameState", typeof(RectTransform));
        centerGO.transform.SetParent(parent, false);
        var rt = centerGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(teamPanelWidth, 0f);
        rt.anchorMax = new Vector2(1f - teamPanelWidth, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var legibleFont = theme != null ? theme.legibleFont : null;

        var clockGO = new GameObject("PlayClock", typeof(TextMeshProUGUI));
        clockGO.transform.SetParent(centerGO.transform, false);
        clockText = ConfigureText(clockGO, legibleFont, centerFontSize);
        AnchorStrip(clockGO.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.48f, 1f));

        var dividerGO = new GameObject("Divider", typeof(Image));
        dividerGO.transform.SetParent(centerGO.transform, false);
        dividerGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);
        AnchorStrip(dividerGO.GetComponent<RectTransform>(), new Vector2(0.49f, 0.15f), new Vector2(0.51f, 0.85f));

        var downGO = new GameObject("DownAndDistance", typeof(TextMeshProUGUI));
        downGO.transform.SetParent(centerGO.transform, false);
        downText = ConfigureText(downGO, legibleFont, centerFontSize);
        AnchorStrip(downGO.GetComponent<RectTransform>(), new Vector2(0.52f, 0f), new Vector2(1f, 1f));
    }

    // font left null is fine — TMP falls back to TMP_Settings.defaultFontAsset, same as
    // ReceiverSelectionUI already relies on elsewhere in this project.
    static TextMeshProUGUI ConfigureText(GameObject go, TMP_FontAsset font, int fontSize)
    {
        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    static void AnchorStrip(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void RefreshScore()
    {
        if (ScoreBoard.Instance == null) return;
        team1Panel?.SetScore(ScoreBoard.Instance.Team1Score);
        team2Panel?.SetScore(ScoreBoard.Instance.Team2Score);
    }

    void RefreshTeamMeter(int teamId, float value)
    {
        (teamId == 0 ? team1Panel : team2Panel)?.SetGamebreaker(value);
    }

    void RefreshClock()
    {
        if (clockText == null || PlayState.Instance == null) return;
        float remaining = Mathf.Max(0f, PlayState.Instance.PlayClockRemaining);
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        clockText.text = $"{minutes}:{seconds:00}";
    }

    void RefreshDowns()
    {
        if (downText == null || DownsTracker.Instance == null) return;
        var downs = DownsTracker.Instance;
        string distance = downs.IsGoalToGo ? "GOAL" : Mathf.CeilToInt(downs.YardsToGo).ToString();
        downText.text = $"{Ordinal(downs.CurrentDown)} & {distance}\nON {Mathf.RoundToInt(downs.YardLine)}";
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