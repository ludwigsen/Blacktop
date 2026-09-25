using UnityEngine;
using UnityEngine.UI;

// Single unified HUD for everything that isn't play selection: score, down & distance,
// and the offense/defense Gamebreaker meters. Supersedes DownsHUD and GamebreakerHUD —
// those built three separate runtime Canvases for what's really one scoreboard; this
// builds one. PlayCallSelector stays a completely separate system on purpose.
//
// Pure display, same rule as DownsHUD: every value comes from ScoreBoard/DownsTracker/
// PlayState's already-resolved state. If something here shows wrong, the bug is
// upstream, not in this file.
//
// SETUP: drop on GameManager, alongside PlayState/DownsTracker/ScoreBoard. Replaces
// DownsHUD and GamebreakerHUD — remove both before adding this, or you'll get
// duplicate gamebreaker bars.
public class GameHUD : MonoBehaviour
{
    [Header("Scorebug")]
    [SerializeField, Range(0.02f, 0.10f)]
    float horizontalMargin = 0.05f;

    [SerializeField]
    float scorebugHeight = 90f;

    [Header("Typography")]
    [SerializeField] int teamNameFontSize = 24;
    [SerializeField] int scoreFontSize = 42;
    [SerializeField] int gameStateFontSize = 20;
    [SerializeField] int downFontSize = 26;

    [Header("Team Colors")]
    [SerializeField]
    Color team1Color = new Color(0.15f, 0.55f, 1f);

    [SerializeField]
    Color team2Color = new Color(1f, 0.2f, 0.2f);

    [SerializeField]
    Color backgroundColor = new Color(0f, 0f, 0f, 0.82f);

    Text team1NameText;
    Text team1ScoreText;
    Image team1GamebreakerFill;

    Text team2NameText;
    Text team2ScoreText;
    Image team2GamebreakerFill;

    Text quarterText;
    Text clockText;
    Text downText;

    void Awake() => BuildHUD();

    // Start(), not OnEnable() — same reasoning as DownsTracker/ScoreBoard: guarantees
    // every relevant Awake() has already run regardless of GameObject order in the
    // scene. The old GamebreakerHUD used OnEnable for this; that was the fragile
    // version, not a pattern worth carrying forward.
    void Start()
    {
        if (PlayState.Instance != null)
        {
            //PlayState.Instance.OnOffenseMeterChanged += RefreshOffenseMeter;
            //PlayState.Instance.OnDefenseMeterChanged += RefreshDefenseMeter;
            //PlayState.Instance.OnOffenseGamebreakerActivated += HandleGamebreakerActivated;
            //RefreshOffenseMeter(PlayState.Instance.OffenseMeter);
            //RefreshDefenseMeter(PlayState.Instance.DefenseMeter);
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

    void OnDestroy()
    {
        if (PlayState.Instance != null)
        {
            //PlayState.Instance.OnOffenseMeterChanged -= RefreshOffenseMeter;
            //PlayState.Instance.OnDefenseMeterChanged -= RefreshDefenseMeter;
            //PlayState.Instance.OnOffenseGamebreakerActivated -= HandleGamebreakerActivated;
        }
        if (DownsTracker.Instance != null) DownsTracker.Instance.OnDownsChanged -= RefreshDowns;
        if (ScoreBoard.Instance != null) ScoreBoard.Instance.OnScoreChanged -= RefreshScore;
    }

    void BuildHUD()
    {
        var canvasGO = new GameObject(
            "GameHUDCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        BuildScorebug(canvasGO.transform);
    }

    void BuildScorebug(Transform parent)
    {
        var scorebugGO = new GameObject("Scorebug", typeof(RectTransform), typeof(Image));
        scorebugGO.transform.SetParent(parent, false);

        var scorebugRT = scorebugGO.GetComponent<RectTransform>();
        scorebugRT.anchorMin = new Vector2(horizontalMargin, 1f);
        scorebugRT.anchorMax = new Vector2(1f - horizontalMargin, 1f);
        scorebugRT.pivot = new Vector2(0.5f, 1f);
        scorebugRT.anchoredPosition = Vector2.zero;
        scorebugRT.sizeDelta = new Vector2(0f, scorebugHeight);

        scorebugGO.GetComponent<Image>().color = backgroundColor;

        BuildTeam1Panel(scorebugGO.transform);
        BuildGameStatePanel(scorebugGO.transform);
        BuildTeam2Panel(scorebugGO.transform);
    }

    // builds the panel for Team 1
    void BuildTeam1Panel(Transform parent)
    {
        var teamGO = new GameObject("Team1", typeof(RectTransform));
        teamGO.transform.SetParent(parent, false);

        var rt = teamGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0.4f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        team1NameText = ConfigureText(teamNameGO, teamNameFontSize);
        team1ScoreText = ConfigureText(teamScoreGO, scoreFontSize);

        team1GamebreakerFill = CreateGamebreaker(
            teamGO.transform,
            "Team1Gamebreaker",
            team1Color,
            false
        );
    }

    void BuildGameStatePanel(Transform parent)
    {
        var centerGO = new GameObject("GameState", typeof(RectTransform));
        centerGO.transform.SetParent(parent, false);

        var rt = centerGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.4f, 0f);
        rt.anchorMax = new Vector2(0.6f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        //quarterText = CreateText(centerGO.transform, "Quarter", gameStateFontSize);
        clockText = ConfigureText(clockGO, gameStateFontSize);
        downText = ConfigureText(downGO, downFontSize);
    }

    void BuildTeam2Panel(Transform parent)
    {
        var teamGO = new GameObject("Team2", typeof(RectTransform));
        teamGO.transform.SetParent(parent, false);

        var rt = teamGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.6f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        team2NameText = ConfigureText(teamNameGO, teamNameFontSize);
        team2ScoreText = ConfigureText(teamScoreGO, scoreFontSize);

        team2GamebreakerFill = CreateGamebreaker(
            teamGO.transform,
            "Team2Gamebreaker",
            team2Color,
            true
        );
    }

    Text ConfigureText(GameObject go, int fontSize)
    {
        var text = go.GetComponent<Text>();

        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    static void AnchorTopLeft(RectTransform rt, Vector2 anchoredPosition, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
    }

    Image CreateGamebreaker(Transform parent, string name, Color fillColor, bool reverse)
    {
        var bgGO = new GameObject(name + "_BG", typeof(Image));
        bgGO.transform.SetParent(parent, false);

        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.sizeDelta = new Vector2(150f, 16f);

        bgGO.GetComponent<Image>().color =
            new Color(1f, 1f, 1f, 0.15f);

        var fillGO = new GameObject(name + "_Fill", typeof(Image));
        fillGO.transform.SetParent(bgGO.transform, false);

        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        var fill = fillGO.GetComponent<Image>();
        fill.color = fillColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = reverse
            ? (int)Image.OriginHorizontal.Right
            : (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;

        return fill;
    }

    void RefreshScore()
    {
        if (ScoreBoard.Instance == null) return;

        if (team1ScoreText != null)
            team1ScoreText.text =
                ScoreBoard.Instance.Team1Score.ToString();

        if (team2ScoreText != null)
            team2ScoreText.text =
                ScoreBoard.Instance.Team2Score.ToString();
    }

    void RefreshDowns()
    {
        if (downText == null || DownsTracker.Instance == null) return;

        var downs = DownsTracker.Instance;

        downText.text = downs.IsGoalToGo
            ? $"{Ordinal(downs.CurrentDown)} & Goal"
            : $"{Ordinal(downs.CurrentDown)} & {Mathf.CeilToInt(downs.YardsToGo)}";
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