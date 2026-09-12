using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Pre-snap play selection. Cycle with Previous/Next, confirm with the Confirm action
// (Enter) to break the huddle — offense visibly forms up into the selected play, still
// dead — then Reset Play (R) snaps it live. Cycling itself never moves anyone; only
// Confirm does, matching "browse, then commit" rather than every cycle press yanking
// players around mid-scroll.
//
// v1 scope: every play here is uniform-route (see PlayCallData.uniformRoute).
//
// KNOWN LIMITATION: no pre-snap dead window before the very first play of a session —
// PlayState now starts dead by default, so this is only relevant on scenes that
// override that. Revisit only if that changes.
//
// SETUP: drop on GameManager. Assign PlayCallData assets to availablePlays. Builds its
// own HUD — no manual Canvas/Text setup needed.
public class PlayCallSelector : MonoBehaviour
{
    [SerializeField] List<PlayCallData> availablePlays = new();

    [Header("Layout")]
    [SerializeField] Vector2 anchoredPosition = new Vector2(30f, -30f);
    [SerializeField] int fontSize = 26;
    [SerializeField] Color activeColor = Color.yellow;
    [SerializeField] Color inactiveColor = new Color(1f, 1f, 1f, 0.6f);

    InputSystem_Actions controls;
    int currentIndex;
    Text listText;
    CanvasGroup canvasGroup;

    void Awake()
    {
        controls = new InputSystem_Actions();
        controls.Player.Previous.performed += ctx => Cycle(-1);
        controls.Player.Next.performed += ctx => Cycle(1);
        controls.Player.Confirm.performed += ctx => TryBreakHuddle();
        BuildHUD();

        // Pushed here, not Start() — Unity guarantees every Awake() runs before any
        // Start(), so PlayState's first BreakHuddle()/ResetPlay() is guaranteed to see
        // this selection already set regardless of GameObject/script execution order.
        PushSelection();
    }

    void Start()
    {
        RefreshText();
        UpdateVisibility();
    }

    void OnEnable()
    {
        controls.Player.Enable();
        if (PlayState.Instance != null)
        {
            PlayState.Instance.OnPlayEnded += HandlePlayEnded;
            PlayState.Instance.OnPlayReset += HandlePlayReset;
        }
    }

    void OnDisable()
    {
        controls.Player.Disable();
        if (PlayState.Instance != null)
        {
            PlayState.Instance.OnPlayEnded -= HandlePlayEnded;
            PlayState.Instance.OnPlayReset -= HandlePlayReset;
        }
    }

    void OnDestroy() => controls?.Dispose();

    void Cycle(int dir)
    {
        if (availablePlays.Count == 0) return;
        if (PlayState.Instance != null && PlayState.Instance.IsLive) return; // only between plays

        currentIndex = (currentIndex + dir + availablePlays.Count) % availablePlays.Count;
        PushSelection();
        RefreshText();
    }

    void TryBreakHuddle()
    {
        if (PlayState.Instance == null || PlayState.Instance.IsLive) return;
        PlayState.Instance.BreakHuddle();
    }

    void PushSelection()
    {
        if (availablePlays.Count == 0 || PlayState.Instance == null) return;
        PlayState.Instance.SetPlayCall(availablePlays[currentIndex]);
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

        var textGO = new GameObject("PlayList", typeof(Text));
        textGO.transform.SetParent(canvasGO.transform, false);

        listText = textGO.GetComponent<Text>();
        listText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        listText.fontSize = fontSize;
        listText.fontStyle = FontStyle.Bold;
        listText.alignment = TextAnchor.UpperLeft;
        listText.horizontalOverflow = HorizontalWrapMode.Overflow;
        listText.verticalOverflow = VerticalWrapMode.Overflow;
        listText.supportRichText = true;

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

        var sb = new StringBuilder();
        for (int i = 0; i < availablePlays.Count; i++)
        {
            string name = availablePlays[i] != null ? availablePlays[i].DisplayName : "(missing)";
            bool active = i == currentIndex;
            string hex = active ? activeHex : inactiveHex;
            string marker = active ? "> " : "   ";
            sb.Append($"<color=#{hex}>{marker}{i + 1}. {name}</color>\n");
        }
        listText.text = sb.ToString();
    }
}