using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Pre-snap play selection. Cycles a small playbook using the Previous/Next input
// actions — already bound to Keyboard 1/2 and Gamepad D-pad left/right in
// InputSystem_Actions, just never wired to anything until now — and pushes the
// selected PlayCallData into PlayState. Only responds while the play is dead
// (!PlayState.IsLive), i.e. the window between a tackle/score and the next
// ResetPlay() (still triggered by the existing Reset Play / "R" input). That means
// cycling plays never competes with in-play controls, and R doubles as "confirm and
// snap" — no separate confirm button needed.
//
// v1 scope: every play here is uniform-route (all eligible receivers run the same
// route — see PlayCallData.uniformRoute). Differentiated per-receiver route trees are
// a straightforward follow-on once these are proven out; PlayCallData already supports
// it via its routes list, so this component won't need to change when that happens.
//
// KNOWN LIMITATION: PlayState starts IsLive = true with no pre-snap dead window before
// the very first play of a session, so you can't cycle plays before the opening snap —
// it always runs whatever's at currentIndex (0) when the scene loads. Fine for now;
// revisit if an actual kickoff/dead-ball intro state gets built later.
//
// SETUP: drop on GameManager, alongside PlayState/DefenderCoordinator/BlockingCoordinator.
// Assign your PlayCallData assets to availablePlays in the Inspector, in playbook order.
// Builds its own HUD at runtime — no manual Canvas/Text setup, same pattern as GamebreakerHUD.
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
        BuildHUD();

        // Pushed here, not Start() — Unity guarantees every Awake() runs before any
        // Start(), so PlayState.Start()'s AssignRoutes() call is guaranteed to see this
        // selection already set regardless of GameObject/script execution order. Same
        // trick BallController already uses for its OnPlayReset subscription timing.
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
        // Rich text carries the per-line active/inactive color, so the whole playbook
        // is one Text component instead of one GameObject per play — trivial to rebuild
        // on every cycle and just as trivial to extend if the playbook grows later.
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