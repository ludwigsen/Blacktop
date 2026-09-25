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
    [Header("Scorebug (top-left)")]
    [SerializeField] Vector2 scorebugPosition = new Vector2(30f, -30f);
    [SerializeField] int scoreFontSize = 30;
    [SerializeField] int downFontSize = 24;

    [Header("Gamebreaker bars")]
    [SerializeField] Vector2 barSize = new Vector2(300f, 24f);
    [SerializeField] Vector2 screenMargin = new Vector2(30f, 30f);
    [SerializeField] int promptFontSize = 28;
    [SerializeField] Color offenseColor = new Color(1f, 0.6f, 0f);      // orange
    [SerializeField] Color defenseColor = new Color(0.2f, 0.6f, 1f);    // blue
    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);

    Text scoreText;
    Text downText;
    Image offenseFill;
    Image defenseFill;
    Text readyPrompt;

    void Awake() => BuildHUD();

    // Start(), not OnEnable() — same reasoning as DownsTracker/ScoreBoard: guarantees
    // every relevant Awake() has already run regardless of GameObject order in the
    // scene. The old GamebreakerHUD used OnEnable for this; that was the fragile
    // version, not a pattern worth carrying forward.
    void Start()
    {
        if (PlayState.Instance != null)
        {
            PlayState.Instance.OnOffenseMeterChanged += RefreshOffenseMeter;
            PlayState.Instance.OnDefenseMeterChanged += RefreshDefenseMeter;
            PlayState.Instance.OnOffenseGamebreakerActivated += HandleGamebreakerActivated;
            RefreshOffenseMeter(PlayState.Instance.OffenseMeter);
            RefreshDefenseMeter(PlayState.Instance.DefenseMeter);
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
            PlayState.Instance.OnOffenseMeterChanged -= RefreshOffenseMeter;
            PlayState.Instance.OnDefenseMeterChanged -= RefreshDefenseMeter;
            PlayState.Instance.OnOffenseGamebreakerActivated -= HandleGamebreakerActivated;
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
        BuildGamebreakerBars(canvasGO.transform);
        BuildReadyPrompt(canvasGO.transform);
    }

    void BuildScorebug(Transform parent)
    {
        var scoreGO = new GameObject("ScoreText", typeof(Text));
        scoreGO.transform.SetParent(parent, false);
        scoreText = ConfigureText(scoreGO, scoreFontSize);
        AnchorTopLeft(scoreGO.GetComponent<RectTransform>(), scorebugPosition, new Vector2(400f, 40f));

        var downGO = new GameObject("DownText", typeof(Text));
        downGO.transform.SetParent(parent, false);
        downText = ConfigureText(downGO, downFontSize);
        downText.color = new Color(1f, 1f, 1f, 0.85f);
        // Stacked directly under the score line — 36px is a rough line-height guess at
        // these font sizes, not a real layout pass. Fine for "does the data show up."
        AnchorTopLeft(downGO.GetComponent<RectTransform>(), scorebugPosition - new Vector2(0f, 36f), new Vector2(300f, 32f));
    }

    Text ConfigureText(GameObject go, int fontSize)
    {
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
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

    void BuildGamebreakerBars(Transform parent)
    {
        offenseFill = CreateBar(parent, "OffenseBar", offenseColor,
            anchor: new Vector2(0f, 0f), pivot: new Vector2(0f, 0f),
            anchoredPos: screenMargin);

        defenseFill = CreateBar(parent, "DefenseBar", defenseColor,
            anchor: new Vector2(1f, 0f), pivot: new Vector2(1f, 0f),
            anchoredPos: new Vector2(-screenMargin.x, screenMargin.y));
    }

    // Unchanged from GamebreakerHUD — background + fill pair. No sprite needed; an
    // Image with sprite == null still renders as a solid quad using its Color.
    Image CreateBar(Transform parent, string name, Color fillColor, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos)
    {
        var bgGO = new GameObject(name + "_BG", typeof(Image));
        bgGO.transform.SetParent(parent, false);

        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = anchor;
        bgRT.anchorMax = anchor;
        bgRT.pivot = pivot;
        bgRT.sizeDelta = barSize;
        bgRT.anchoredPosition = anchoredPos;
        bgGO.GetComponent<Image>().color = backgroundColor;

        var fillGO = new GameObject(name + "_Fill", typeof(Image));
        fillGO.transform.SetParent(bgGO.transform, false);

        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        var fillImg = fillGO.GetComponent<Image>();
        fillImg.color = fillColor;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0f;

        return fillImg;
    }

    void BuildReadyPrompt(Transform parent)
    {
        var promptGO = new GameObject("ReadyPrompt", typeof(Text));
        promptGO.transform.SetParent(parent, false);

        readyPrompt = promptGO.GetComponent<Text>();
        readyPrompt.text = "GAMEBREAKER READY — PRESS G";
        readyPrompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        readyPrompt.fontSize = promptFontSize;
        readyPrompt.fontStyle = FontStyle.Bold;
        readyPrompt.alignment = TextAnchor.MiddleCenter;
        readyPrompt.color = offenseColor;

        var promptRT = promptGO.GetComponent<RectTransform>();
        promptRT.anchorMin = new Vector2(0.5f, 1f);
        promptRT.anchorMax = new Vector2(0.5f, 1f);
        promptRT.pivot = new Vector2(0.5f, 1f);
        promptRT.anchoredPosition = new Vector2(0f, -30f);
        promptRT.sizeDelta = new Vector2(600f, 50f);

        promptGO.SetActive(false);
    }

    void RefreshScore()
    {
        if (scoreText == null || ScoreBoard.Instance == null) return;
        scoreText.text = $"T1  {ScoreBoard.Instance.Team1Score}   –   T2  {ScoreBoard.Instance.Team2Score}";
    }

    void RefreshDowns()
    {
        if (downText == null || DownsTracker.Instance == null) return;
        var downs = DownsTracker.Instance;
        downText.text = downs.IsGoalToGo
            ? $"{Ordinal(downs.CurrentDown)} & Goal"
            : $"{Ordinal(downs.CurrentDown)} & {Mathf.CeilToInt(downs.YardsToGo)}";
    }

    void RefreshOffenseMeter(float value)
    {
        if (offenseFill != null) offenseFill.fillAmount = value / 100f;

        if (readyPrompt != null)
        {
            bool ready = value >= 100f && PlayState.Instance != null && !PlayState.Instance.IsOffenseGamebreakerActive;
            readyPrompt.gameObject.SetActive(ready);
        }
    }

    void RefreshDefenseMeter(float value)
    {
        if (defenseFill != null) defenseFill.fillAmount = value / 100f;
    }

    void HandleGamebreakerActivated()
    {
        if (readyPrompt != null) readyPrompt.gameObject.SetActive(false);
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