using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Presentation and the existing 1=WR1, 2=WR2, 3=WR3, 4=RB keyboard targeting.
// Layout shares one collision/safe-area pass with controlled-player nameplates.
public class ReceiverSelectionUI : MonoBehaviour
{
    public static ReceiverSelectionUI Instance { get; private set; }
    [SerializeField] Canvas uiCanvas;
    [SerializeField] PlayState playState;
    [SerializeField] HUDTheme theme;
    [SerializeField] GameplayLabelLayout labelLayout;
    readonly List<Transform> receivers = new();
    RectTransform[] receiverButtons;
    int selectedReceiverIndex = -1;
    bool suppressed;

    public GameplayLabelLayout LabelLayout => labelLayout;
    public IReadOnlyList<Transform> Receivers => receivers;
    public RectTransform PromptAt(int index) => receiverButtons != null && index >= 0 && index < receiverButtons.Length ? receiverButtons[index] : null;

    void Awake()
    {
        Instance = this;
        if (playState == null) playState = FindAnyObjectByType<PlayState>();
        if (labelLayout == null) labelLayout = GetComponent<GameplayLabelLayout>() ?? gameObject.AddComponent<GameplayLabelLayout>();
        labelLayout.Configure(uiCanvas, playState);
    }

    void Start()
    {
        if (playState == null || uiCanvas == null) return;
        // The old canvas-wide fade also hid links/identity. Each card now owns visibility.
        var group = uiCanvas.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1f;
        CreateReceiverButtons();
        playState.OnPlayReset += ShowLabels;
        playState.OnPlayEnded += HideLabels;
        playState.OnHuddleStarted += HideLabels;
        playState.OnPossessionChanged += HandlePossessionChanged;
    }

    void OnEnable() { if (labelLayout != null) labelLayout.enabled = true; }
    void OnDisable() { if (labelLayout != null) labelLayout.enabled = false; }
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (playState != null)
        {
            playState.OnPlayReset -= ShowLabels;
            playState.OnPlayEnded -= HideLabels;
            playState.OnHuddleStarted -= HideLabels;
            playState.OnPossessionChanged -= HandlePossessionChanged;
        }
        DestroyPrompts();
    }

    void HandlePossessionChanged(int _)
    {
        DestroyPrompts(); selectedReceiverIndex = -1; suppressed = false;
        CreateReceiverButtons();
    }

    void Update()
    {
        // Same physical keys, using the project's Input System rather than legacy Input.
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.digit1Key.wasPressedThisFrame) TryPassToReceiver(0);
        if (keyboard.digit2Key.wasPressedThisFrame) TryPassToReceiver(1);
        if (keyboard.digit3Key.wasPressedThisFrame) TryPassToReceiver(2);
        if (keyboard.digit4Key.wasPressedThisFrame) TryPassToReceiver(3);
    }

    void LateUpdate()
    {
        if (labelLayout != null) labelLayout.SetReceiverVisibility(!suppressed && CanOfferReceivers());
    }

    bool CanOfferReceivers()
    {
        var ball = BallController.Instance;
        return playState != null && playState.IsLive && !playState.IsUserDefending &&
            !playState.HasPasserCrossedLOS && ball != null && ball.IsHeld && ball.Carrier != null &&
            ball.Carrier.TryGetComponent<TeamMember>(out var member) && member.slot == TeamMember.RosterSlot.QB;
    }

    void CreateReceiverButtons()
    {
        if (playState == null || uiCanvas == null) return;
        receivers.Clear(); receivers.AddRange(ReceiverTargeting.GetEligibleReceivers(playState.OffensivePlayers));
        receiverButtons = new RectTransform[receivers.Count];
        for (int i = 0; i < receivers.Count; i++)
        {
            var go = new GameObject($"PassPrompt_{i + 1}", typeof(RectTransform), typeof(AngularPanel));
            go.transform.SetParent(uiCanvas.transform, false);
            var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = labelLayout.PromptSize;
            var image = go.GetComponent<Image>(); image.raycastTarget = false;
            ThemeableUI.Bind(image, UIColorRole.SurfacePrimary, null, .96f);
            var textObject = new GameObject("Receiver key and identity", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(go.transform, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            if (theme != null && theme.legibleFont != null) text.font = theme.legibleFont;
            text.text = $"[{i + 1}]  {ShortIdentity(receivers[i])}";
            text.alignment = TextAlignmentOptions.Center; text.fontSize = 24f; text.richText = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            ThemeableUI.Bind(text, UIColorRole.TextPrimary, UITypeRole.Navigation);
            var textRect = text.rectTransform; textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 0f); textRect.offsetMax = new Vector2(-6f, 0f);
            receiverButtons[i] = rect; labelLayout.AddPrompt(receivers[i], rect, text, i);
            go.SetActive(false);
        }
    }

    public static string ShortIdentity(Transform actor)
    {
        var record = actor != null ? actor.GetComponent<CharacterApplier>()?.Record : null;
        if (!record.HasValue) return actor != null ? actor.name : "";
        string name = record.Value.displayName.Trim(); int lastSpace = name.LastIndexOf(' ');
        string shortName = lastSpace > 0 ? name[0] + ". " + name.Substring(lastSpace + 1) : name;
        return $"#{record.Value.jerseyNumber} {shortName}";
    }

    void DestroyPrompts()
    {
        if (receiverButtons != null)
            foreach (var rect in receiverButtons)
                if (rect != null) { rect.gameObject.SetActive(false); Destroy(rect.gameObject); }
        if (labelLayout != null) labelLayout.ClearPrompts();
        receiverButtons = null;
    }

    void TryPassToReceiver(int index)
    {
        if (!CanOfferReceivers() || index < 0 || index >= receivers.Count || !ReceiverTargeting.IsEligible(receivers[index])) return;
        selectedReceiverIndex = index;
        // Resolve the current carrier at the action, preserving the sole possession authority.
        var buffer = BallController.Instance.Carrier.GetComponent<InputBuffer>();
        if (buffer != null) buffer.QueueInput("Pass");
        HideLabels();
    }

    void HideLabels(PlayState.PlayEndReason _) => HideLabels();
    void HideLabels() { suppressed = true; if (labelLayout != null) labelLayout.SetReceiverVisibility(false); }
    void ShowLabels() { suppressed = false; selectedReceiverIndex = -1; }
    public int GetSelectedReceiverIndex() => selectedReceiverIndex;
    public void ResetSelection() => selectedReceiverIndex = -1;
}
