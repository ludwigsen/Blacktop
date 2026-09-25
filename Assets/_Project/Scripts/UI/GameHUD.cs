using UnityEngine;
using UnityEngine.UI;

// Single unified HUD for everything that isn't play selection: score, down & distance,
// and each team's own Gamebreaker meter. Supersedes DownsHUD and GamebreakerHUD — those
// built three separate runtime Canvases for what's really one scorebug; this builds one.
// PlayCallSelector stays a completely separate system on purpose (different lifecycle —
// pre-snap only — and a different corner of the screen).
//
// Pure display: every value comes from ScoreBoard/DownsTracker/PlayState's already-
// resolved state. If something here shows wrong, the bug is upstream, not in this file.
//
// Layout is percentage-of-screen, not fixed pixels: a fixed top bar, inset horizontally
// from each edge, sized as a fraction of screen height — actually that fraction at any
// resolution/aspect ratio, not an approximation under CanvasScaler's reference res.
//
// SETUP: drop on GameManager, alongside PlayState/DownsTracker/ScoreBoard. Replaces
// DownsHUD and GamebreakerHUD — remove both before adding this, or you'll get
// duplicate gamebreaker bars.
public class GameHUD : MonoBehaviour
{
    [Header("Scorebug")]
    [SerializeField, Range(0.02f, 0.10f)] float horizontalMargin = 0.05f;
    [SerializeField, Range(0.05f, 0.20f)] float heightPercent = 0.10f;
    [SerializeField, Range(0.2f, 0.45f)] float teamPanelWidth = 0.35f; // each side; center strip gets the rest

    [Header("Typography")]
    [SerializeField] int teamNameFontSize = 22;
    [SerializeField] int scoreFontSize = 40;
    [SerializeField] int downFontSize = 26;

    [Header("Team Colors")]
    [SerializeField] Color team1Color = new Color(0.15f, 0.55f, 1f);
    [SerializeField] Color team2Color = new Color(1f, 0.2f, 0.2f);
    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.82f);

    TeamScorePanel team1Panel;
    TeamScorePanel team2Panel;
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

    void OnDestroy()
    {
        if (PlayState.Instance != null) PlayState.Instance.OnTeamMeterChanged -= RefreshTeamMeter;
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
    }

    void BuildScorebug(Transform parent)
    {
        // position: fixed; top: 0; padding: 5% left/right; height: 10% — expressed as
        // anchors, not a pixel sizeDelta, so it's genuinely that fraction of the real
        // screen at any resolution/aspect ratio.
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
            accentColor: isTeam1 ? team1Color : team2Color,
            nameFontSize: teamNameFontSize,
            scoreFontSize: scoreFontSize,
            reverseFill: !isTeam1 // right-side panel fills toward the center, not off-screen
        );
        panel.SetTeam(isTeam1 ? "T1" : "T2"); // placeholder — no team display-name concept anywhere yet
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

        var downGO = new GameObject("DownText", typeof(Text));
        downGO.transform.SetParent(centerGO.transform, false);
        downText = ConfigureText(downGO, downFontSize);
    }

    static Text ConfigureText(GameObject go, int fontSize)
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