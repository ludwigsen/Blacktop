using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pre-snap play selection for the HUMAN's side only. Shows offensive plays when the user
// has the ball and defensive plays when they don't (swaps on possession change). Cycle with
// Previous/Next, Confirm breaks the huddle for BOTH teams — the CPU already picked its own
// side, blind, the instant the huddle formed (see CPUPlayCaller), so nothing waits on it.
//
// SETUP: drop on GameManager. Assign PlayCallData assets to availablePlays and
// DefensivePlayData assets to defensivePlays. HUDTheme optional.
public class PlayCallSelector : MonoBehaviour
{
    // Field name kept as-is so the scene's already-serialized offensive list survives.
    [SerializeField] List<PlayCallData> availablePlays = new();
    [SerializeField] List<DefensivePlayData> defensivePlays = new();
    [SerializeField] HUDTheme theme;

    [Header("Layout")]
    [SerializeField] Vector2 anchoredPosition = new Vector2(30f, -30f);
    [SerializeField] int fontSize = 26;
    [SerializeField] Color activeColor = Color.yellow;
    [SerializeField] Color inactiveColor = new Color(1f, 1f, 1f, 0.6f);

    // The CPU picks from these same authored lists, so both sides always share one playbook.
    public IReadOnlyList<PlayCallData> OffensivePlays => availablePlays;
    public IReadOnlyList<DefensivePlayData> DefensivePlays => defensivePlays;

    InputSystem_Actions controls;
    int offenseIndex;
    int defenseIndex;
    TextMeshProUGUI listText;
    CanvasGroup canvasGroup;

    bool ShowingDefense => PlayState.Instance != null && PlayState.Instance.IsUserDefending;
    int ActiveCount => ShowingDefense ? defensivePlays.Count : availablePlays.Count;

    void Awake()
    {
        controls = new InputSystem_Actions();
        controls.Player.Previous.performed += ctx => Cycle(-1);
        controls.Player.Next.performed += ctx => Cycle(1);
        controls.Player.Confirm.performed += ctx => TryBreakHuddle();
        BuildHUD();
    }

    // Start(), not Awake/OnEnable: PlayState.Instance is only guaranteed after every Awake().
    void Start()
    {
        if (PlayState.Instance != null)
        {
            PlayState.Instance.OnPlayEnded += HandlePlayEnded;
            PlayState.Instance.OnPlayReset += HandlePlayReset;
            PlayState.Instance.OnPossessionChanged += HandlePossessionChanged;
        }

        PushSelection();
        RefreshText();
        UpdateVisibility();
    }

    void OnEnable() => controls.Player.Enable();
    void OnDisable() => controls.Player.Disable();

    void OnDestroy()
    {
        if (PlayState.Instance != null)
        {
            PlayState.Instance.OnPlayEnded -= HandlePlayEnded;
            PlayState.Instance.OnPlayReset -= HandlePlayReset;
            PlayState.Instance.OnPossessionChanged -= HandlePossessionChanged;
        }
        controls?.Dispose();
    }

    void Cycle(int dir)
    {
        int count = ActiveCount;
        if (count == 0) return;
        if (PlayState.Instance != null && PlayState.Instance.IsLive) return; // only between plays

        if (ShowingDefense) defenseIndex = (defenseIndex + dir + count) % count;
        else offenseIndex = (offenseIndex + dir + count) % count;

        PushSelection();
        RefreshText();
    }

    void TryBreakHuddle()
    {
        if (PlayState.Instance == null || PlayState.Instance.IsLive) return;
        PlayState.Instance.BreakHuddle();
    }

    // Pushes ONLY the human's side. The other side is CPUPlayCaller's job.
    void PushSelection()
    {
        var ps = PlayState.Instance;
        if (ps == null) return;

        if (ShowingDefense)
        {
            if (defensivePlays.Count > 0) ps.SetDefensivePlay(defensivePlays[defenseIndex]);
        }
        else if (availablePlays.Count > 0)
        {
            ps.SetPlayCall(availablePlays[offenseIndex]);
        }
    }

    // Fires during EndPlay, after rosters refresh — the list the user sees flips sides here.
    void HandlePossessionChanged(int _)
    {
        PushSelection();
        RefreshText();
    }

    void HandlePlayEnded(PlayState.PlayEndReason reason) => UpdateVisibility();
    void HandlePlayReset() => UpdateVisibility();

    void UpdateVisibility()
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = (PlayState.Instance != null && !PlayState.Instance.IsLive) ? 1f : 0f;
    }

    void BuildHUD()
    {
        var canvasGO = new GameObject("PlayCallCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGroup = canvasGO.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        var textGO = new GameObject("PlayList", typeof(TextMeshProUGUI));
        textGO.transform.SetParent(canvasGO.transform, false);

        listText = textGO.GetComponent<TextMeshProUGUI>();
        if (theme != null && theme.legibleFont != null) listText.font = theme.legibleFont;
        listText.fontSize = fontSize;
        listText.fontStyle = FontStyles.Bold;
        listText.alignment = TextAlignmentOptions.TopLeft;
        listText.enableWordWrapping = false;
        listText.overflowMode = TextOverflowModes.Overflow;

        var rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(400f, 300f);
    }

    void RefreshText()
    {
        if (listText == null) return;

        string activeHex = ColorUtility.ToHtmlStringRGB(activeColor);
        string inactiveHex = ColorUtility.ToHtmlStringRGB(inactiveColor);
        bool defense = ShowingDefense;
        int current = defense ? defenseIndex : offenseIndex;
        int count = ActiveCount;

        var sb = new StringBuilder();
        sb.Append($"<color=#{inactiveHex}>{(defense ? "DEFENSE" : "OFFENSE")}</color>\n");

        for (int i = 0; i < count; i++)
        {
            string name = defense
                ? (defensivePlays[i] != null ? defensivePlays[i].DisplayName : "(missing)")
                : (availablePlays[i] != null ? availablePlays[i].DisplayName : "(missing)");
            bool active = i == current;
            string hex = active ? activeHex : inactiveHex;
            string marker = active ? "> " : "   ";
            sb.Append($"<color=#{hex}>{marker}{i + 1}. {name}</color>\n");
        }
        listText.text = sb.ToString();
    }
}