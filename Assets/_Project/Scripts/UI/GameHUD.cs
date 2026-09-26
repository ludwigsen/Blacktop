using UnityEngine;
using UnityEngine.UI;

// Single unified HUD for everything that isn't play selection: team score + Gamebreaker
// on each side, play clock + down/distance/field position in the center. Structure
// matches the concept board's HUD breakdown, minus quarter (no quarter system — the
// center clock is PlayState's PLAY clock only, not a running game clock) and minus the
// diagonal/grunge panel art (that needs real sprite assets — this is the honest
// placeholder pass, plain rectangles, not a fake paint texture).
//
// Pure display — every value comes from ScoreBoard/DownsTracker/PlayState's already-
// resolved state, with one deliberate exception: the play clock is polled every frame
// in Update() rather than event-driven, since it changes continuously and has no
// natural discrete "changed" event to hook.
//
// Layout is percentage-of-screen: fixed top bar, inset horizontally from each edge,
// sized as a fraction of screen height — genuinely that fraction at any resolution,
// not an approximation under CanvasScaler's reference resolution.
//
// SETUP: drop on GameManager, alongside PlayState/DownsTracker/ScoreBoard. Assign
// PlayState's Team1Identity/Team2Identity (Assets > Create > Blacktop > Team Identity)
// for real names/colors — everything falls back to a neutral placeholder if you don't.
public class GameHUD : MonoBehaviour
{
    [Header("Scorebug")]
    [SerializeField, Range(0.02f, 0.10f)] float horizontalMargin = 0.05f;
    [SerializeField, Range(0.05f, 0.20f)] float heightPercent = 0.10f;
    [SerializeField, Range(0.2f, 0.45f)] float teamPanelWidth = 0.35f; // each side; center strip gets the rest

    [Header("Typography")]
    [SerializeField] int teamNameFontSize = 22;
    [SerializeField] int scoreFontSize = 40;
    [SerializeField] int centerFontSize = 24;

    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.82f);

    TeamScorePanel team1Panel;
    TeamScorePanel team2Panel;
    Text clockText;
    Text downText;

    void Awake() => BuildHUD();

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
        panel.Build(teamNameFontSize, scoreFontSize, reverseFill: !isTeam1); // right side fills toward center
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

        var clockGO = new GameObject("PlayClock", typeof(Text));
        clockGO.transform.SetParent(centerGO.transform, false);
        clockText = ConfigureText(clockGO, centerFontSize);
        AnchorStrip(clockGO.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.48f, 1f));

        var dividerGO = new GameObject("Divider", typeof(Image));
        dividerGO.transform.SetParent(centerGO.transform, false);
        dividerGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);
        AnchorStrip(dividerGO.GetComponent<RectTransform>(), new Vector2(0.49f, 0.15f), new Vector2(0.51f, 0.85f));

        var downGO = new GameObject("DownAndDistance", typeof(Text));
        downGO.transform.SetParent(centerGO.transform, false);
        downText = ConfigureText(downGO, centerFontSize);
        AnchorStrip(downGO.GetComponent<RectTransform>(), new Vector2(0.52f, 0f), new Vector2(1f, 1f));
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