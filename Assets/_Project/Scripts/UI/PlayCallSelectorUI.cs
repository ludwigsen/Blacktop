using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Pre-snap play selection for the HUMAN's side only.
//
// Interaction is deliberately modeled after a console football play-call screen:
// 1. Choose a play family (ALL / PASS / RUN / TRICK, or ALL / BLITZ / ZONE / MAN).
// 2. Move through the plays in that family.
// 3. Confirm to lock the highlighted play and break the huddle.
//
// The UI is generated in code so it can be dropped onto the existing GameManager without
// requiring a new prefab hierarchy. The authored playbook lists remain serialized exactly
// as before, so existing scene references survive this replacement.
public class PlayCallSelector : MonoBehaviour
{
    [SerializeField] List<PlayCallData> availablePlays = new();
    [SerializeField] List<DefensivePlayData> defensivePlays = new();
    [SerializeField] HUDTheme theme;

    [Header("Layout")]
    [SerializeField] Vector2 panelMargin = new Vector2(110f, 105f);
    [SerializeField] float panelHeight = 390f;
    [SerializeField] float categoryHeight = 78f;
    [SerializeField] float cardHeight = 190f;
    [SerializeField] float cardGap = 18f;

    [Header("Typography")]
    [SerializeField] int titleFontSize = 24;
    [SerializeField] int categoryFontSize = 22;
    [SerializeField] int playFontSize = 28;
    [SerializeField] int detailFontSize = 16;

    [Header("Colors")]
    [SerializeField] Color panelColor = new Color(0f, 0f, 0f, 0.88f);
    [SerializeField] Color cardColor = new Color(0.08f, 0.08f, 0.08f, 0.96f);
    [SerializeField] Color activeColor = new Color(1f, 0.78f, 0.1f, 1f);
    [SerializeField] Color textColor = Color.white;
    [SerializeField] Color mutedColor = new Color(1f, 1f, 1f, 0.48f);

    public IReadOnlyList<PlayCallData> OffensivePlays => availablePlays;
    public IReadOnlyList<DefensivePlayData> DefensivePlays => defensivePlays;

    enum SelectionMode { Categories, Plays }

    enum OffensiveCategory
    {
        All,
        Pass,
        Run,
        Trick
    }

    enum DefensiveCategory
    {
        All,
        Blitz,
        Zone,
        Man
    }

    InputSystem_Actions controls;
    CanvasGroup canvasGroup;
    RectTransform panel;
    TextMeshProUGUI titleText;
    TextMeshProUGUI situationText;
    readonly List<TextMeshProUGUI> categoryTexts = new();
    readonly List<TextMeshProUGUI> cardTitleTexts = new();
    readonly List<TextMeshProUGUI> cardDetailTexts = new();
    readonly List<Image> categoryBackgrounds = new();
    readonly List<Image> cardBackgrounds = new();

    SelectionMode selectionMode = SelectionMode.Categories;
    int categoryIndex;
    int playIndex;
    bool ShowingDefense => PlayState.Instance != null && PlayState.Instance.IsUserDefending;

    static readonly OffensiveCategory[] OffensiveCategories =
    {
        OffensiveCategory.All, OffensiveCategory.Pass, OffensiveCategory.Run, OffensiveCategory.Trick
    };

    static readonly DefensiveCategory[] DefensiveCategories =
    {
        DefensiveCategory.All, DefensiveCategory.Blitz, DefensiveCategory.Zone, DefensiveCategory.Man
    };

    void Awake()
    {
        controls = new InputSystem_Actions();
        controls.UI.Navigate.performed += HandleNavigate;
        controls.UI.Submit.performed += HandleSubmit;
        controls.UI.Cancel.performed += HandleCancel;
        BuildHUD();
    }

    void Start()
    {
        if (PlayState.Instance != null)
        {
            PlayState.Instance.OnPlayEnded += HandlePlayEnded;
            PlayState.Instance.OnPlayReset += HandlePlayReset;
            PlayState.Instance.OnPossessionChanged += HandlePossessionChanged;
            PlayState.Instance.OnHuddleStarted += HandleHuddleStarted;
        }

        ResetSelection();
        UpdateVisibility();
    }

    void OnEnable()
    {
        controls.UI.Enable();
    }

    void OnDisable()
    {
        controls.UI.Disable();
    }

    void OnDestroy()
    {
        if (PlayState.Instance != null)
        {
            PlayState.Instance.OnPlayEnded -= HandlePlayEnded;
            PlayState.Instance.OnPlayReset -= HandlePlayReset;
            PlayState.Instance.OnPossessionChanged -= HandlePossessionChanged;
            PlayState.Instance.OnHuddleStarted -= HandleHuddleStarted;
        }

        controls?.Dispose();
    }

    void HandleNavigate(InputAction.CallbackContext context)
    {
        if (!CanInteract()) return;

        Vector2 input = context.ReadValue<Vector2>();
        if (Mathf.Abs(input.x) < 0.5f && Mathf.Abs(input.y) < 0.5f) return;

        if (selectionMode == SelectionMode.Categories)
        {
            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
                CycleCategory(input.x > 0f ? 1 : -1);
            else if (input.y < 0f)
                EnterPlaySelection();
        }
        else
        {
            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
                CyclePlay(input.x > 0f ? 1 : -1);
            else if (input.y > 0f)
                EnterCategorySelection();
        }
    }

    void HandleSubmit(InputAction.CallbackContext _)
    {
        if (!CanInteract()) return;

        if (selectionMode == SelectionMode.Categories)
            EnterPlaySelection();
        else
            ConfirmPlay();
    }

    void HandleCancel(InputAction.CallbackContext _)
    {
        if (!CanInteract()) return;

        if (selectionMode == SelectionMode.Plays)
            EnterCategorySelection();
    }

    bool CanInteract()
    {
        return PlayState.Instance != null &&
               !PlayState.Instance.IsLive &&
               !PlayState.Instance.IsPlayEnding;
    }

    void CycleCategory(int direction)
    {
        int count = ShowingDefense ? DefensiveCategories.Length : OffensiveCategories.Length;
        categoryIndex = (categoryIndex + direction + count) % count;
        playIndex = 0;
        PushHighlightedPlay();
        Refresh();
    }

    void CyclePlay(int direction)
    {
        var plays = GetFilteredPlays();
        if (plays.Count == 0) return;

        playIndex = (playIndex + direction + plays.Count) % plays.Count;
        PushHighlightedPlay();
        Refresh();
    }

    void EnterPlaySelection()
    {
        if (GetFilteredPlays().Count == 0) return;
        selectionMode = SelectionMode.Plays;
        Refresh();
    }

    void EnterCategorySelection()
    {
        selectionMode = SelectionMode.Categories;
        Refresh();
    }

    void ConfirmPlay()
    {
        if (GetFilteredPlays().Count == 0) return;
        PushHighlightedPlay();
        PlayState.Instance.BreakHuddle();
    }

    void PushHighlightedPlay()
    {
        var ps = PlayState.Instance;
        if (ps == null) return;

        if (ShowingDefense)
        {
            var plays = GetFilteredDefensivePlays();
            if (plays.Count > 0)
                ps.SetDefensivePlay(plays[Mathf.Clamp(playIndex, 0, plays.Count - 1)]);
        }
        else
        {
            var plays = GetFilteredOffensivePlays();
            if (plays.Count > 0)
                ps.SetPlayCall(plays[Mathf.Clamp(playIndex, 0, plays.Count - 1)]);
        }
    }

    List<PlayCallData> GetFilteredOffensivePlays()
    {
        var result = new List<PlayCallData>();
        OffensiveCategory category = OffensiveCategories[Mathf.Clamp(categoryIndex, 0, OffensiveCategories.Length - 1)];

        foreach (var play in availablePlays)
        {
            if (play == null) continue;
            if (category == OffensiveCategory.All ||
                (category == OffensiveCategory.Pass && play.PlayType == PlayType.Pass) ||
                (category == OffensiveCategory.Run && play.PlayType == PlayType.Run) ||
                (category == OffensiveCategory.Trick && play.PlayType == PlayType.Trick))
                result.Add(play);
        }

        return result;
    }

    List<DefensivePlayData> GetFilteredDefensivePlays()
    {
        var result = new List<DefensivePlayData>();
        DefensiveCategory category = DefensiveCategories[Mathf.Clamp(categoryIndex, 0, DefensiveCategories.Length - 1)];

        foreach (var play in defensivePlays)
        {
            if (play == null) continue;
            if (category == DefensiveCategory.All || MatchesDefenseCategory(play, category))
                result.Add(play);
        }

        return result;
    }

    List<Object> GetFilteredPlays()
    {
        var result = new List<Object>();
        if (ShowingDefense)
        {
            foreach (var play in GetFilteredDefensivePlays()) result.Add(play);
        }
        else
        {
            foreach (var play in GetFilteredOffensivePlays()) result.Add(play);
        }
        return result;
    }

    static bool MatchesDefenseCategory(DefensivePlayData play, DefensiveCategory category)
    {
        // DefensivePlayData already exposes authored job counts, so categorization stays
        // data-driven instead of depending on play names.
        switch (category)
        {
            case DefensiveCategory.Blitz:
                return play.RushCount >= 2;
            case DefensiveCategory.Zone:
                return play.ZoneCount > play.ManCount && play.RushCount < 2;
            case DefensiveCategory.Man:
                return play.ManCount >= play.ZoneCount && play.RushCount < 2;
            default:
                return true;
        }
    }

    void HandleHuddleStarted()
    {
        if (!PlayState.Instance.IsUserTeam(PlayState.Instance.PossessionTeamId))
            return;

        ResetSelection();
        UpdateVisibility();
    }

    void HandlePossessionChanged(int _)
    {
        ResetSelection();
        UpdateVisibility();
    }

    void HandlePlayEnded(PlayState.PlayEndReason _) => UpdateVisibility();
    void HandlePlayReset() => UpdateVisibility();

    public void ResetSelection()
    {
        categoryIndex = 0;
        playIndex = 0;
        selectionMode = SelectionMode.Categories;

        // Always give PlayState a valid human-side selection when a playbook has one.
        PushHighlightedPlay();
        Refresh();
    }

    void UpdateVisibility()
    {
        if (canvasGroup == null) return;
        bool visible = PlayState.Instance != null &&
                       !PlayState.Instance.IsLive &&
                       !PlayState.Instance.IsPlayEnding;
        canvasGroup.alpha = visible ? 1f : 0f;
    }

    void BuildHUD()
    {
        var canvasGO = new GameObject("PlayCallCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasGO.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        var panelGO = new GameObject("PlayCallPanel", typeof(Image));
        panelGO.transform.SetParent(canvasGO.transform, false);
        panel = panelGO.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.offsetMin = panelMargin;
        panel.offsetMax = new Vector2(-panelMargin.x, panelMargin.y + panelHeight);
        panelGO.GetComponent<Image>().color = panelColor;

        BuildHeader(panelGO.transform);
        BuildCategories(panelGO.transform);
        BuildPlayCards(panelGO.transform);
    }

    void BuildHeader(Transform parent)
    {
        var header = new GameObject("Header", typeof(RectTransform));
        header.transform.SetParent(parent, false);
        var rt = header.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -18f);
        rt.sizeDelta = new Vector2(0f, 54f);

        titleText = CreateText(header.transform, "OFFENSE", titleFontSize, TextAlignmentOptions.Left);
        Anchor(titleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.45f, 1f));

        situationText = CreateText(header.transform, "", detailFontSize, TextAlignmentOptions.Right);
        Anchor(situationText.rectTransform, new Vector2(0.45f, 0f), new Vector2(1f, 1f));
        situationText.color = mutedColor;
    }

    void BuildCategories(Transform parent)
    {
        var row = new GameObject("Categories", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -78f);
        rt.sizeDelta = new Vector2(-36f, categoryHeight);

        for (int i = 0; i < 4; i++)
        {
            var card = new GameObject($"Category{i}", typeof(Image));
            card.transform.SetParent(row.transform, false);
            var image = card.GetComponent<Image>();
            categoryBackgrounds.Add(image);

            var cardRT = card.GetComponent<RectTransform>();
            float minX = i / 4f;
            float maxX = (i + 1) / 4f;
            cardRT.anchorMin = new Vector2(minX, 0f);
            cardRT.anchorMax = new Vector2(maxX, 1f);
            cardRT.offsetMin = new Vector2(6f, 0f);
            cardRT.offsetMax = new Vector2(-6f, 0f);

            var label = CreateText(card.transform, "", categoryFontSize, TextAlignmentOptions.Center);
            Anchor(label.rectTransform, Vector2.zero, Vector2.one);
            categoryTexts.Add(label);
        }
    }

    void BuildPlayCards(Transform parent)
    {
        var row = new GameObject("PlayCards", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(18f, 22f);
        rt.offsetMax = new Vector2(-18f, -168f);

        // Five cards give the screen the Madden/NFL Street feel without turning the
        // playbook into a tiny unreadable list. The center card is the current play.
        const int visibleCards = 5;
        for (int i = 0; i < visibleCards; i++)
        {
            var card = new GameObject($"PlayCard{i}", typeof(Image));
            card.transform.SetParent(row.transform, false);
            var image = card.GetComponent<Image>();
            cardBackgrounds.Add(image);

            var cardRT = card.GetComponent<RectTransform>();
            float width = 1f / visibleCards;
            cardRT.anchorMin = new Vector2(i * width, 0f);
            cardRT.anchorMax = new Vector2((i + 1) * width, 1f);
            cardRT.offsetMin = new Vector2(cardGap / 2f, 0f);
            cardRT.offsetMax = new Vector2(-cardGap / 2f, 0f);

            var title = CreateText(card.transform, "", playFontSize, TextAlignmentOptions.Center);
            title.enableAutoSizing = false;
            Anchor(title.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 0.82f));
            cardTitleTexts.Add(title);

            var detail = CreateText(card.transform, "", detailFontSize, TextAlignmentOptions.Center);
            detail.color = mutedColor;
            Anchor(detail.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.38f));
            cardDetailTexts.Add(detail);
        }
    }

    void Refresh()
    {
        if (titleText == null) return;

        bool defense = ShowingDefense;
        titleText.text = defense ? "DEFENSE" : "OFFENSE";
        situationText.text = BuildSituationText();

        string[] labels = defense
            ? new[] { "ALL", "BLITZ", "ZONE", "MAN" }
            : new[] { "ALL", "PASS", "RUN", "TRICK" };

        for (int i = 0; i < categoryTexts.Count; i++)
        {
            bool active = i == categoryIndex;
            categoryTexts[i].text = labels[i];
            categoryTexts[i].color = active ? activeColor : textColor;
            categoryBackgrounds[i].color = active
                ? new Color(activeColor.r, activeColor.g, activeColor.b, 0.18f)
                : cardColor;
        }

        var plays = ShowingDefense
            ? GetFilteredDefensivePlays()
            : null;

        if (defense)
            RefreshDefenseCards(plays);
        else
            RefreshOffenseCards(GetFilteredOffensivePlays());
    }

    void RefreshOffenseCards(List<PlayCallData> plays)
    {
        for (int slot = 0; slot < cardTitleTexts.Count; slot++)
        {
            int index = CardIndexForSlot(slot, plays.Count);
            bool selected = index == playIndex && plays.Count > 0;

            if (index < 0)
            {
                SetCard(slot, "", "", false);
                continue;
            }

            var play = plays[index];
            SetCard(slot,
                play != null ? play.DisplayName : "(missing)",
                play != null ? play.PlayType.ToString().ToUpperInvariant() : "",
                selected);
        }
    }

    void RefreshDefenseCards(List<DefensivePlayData> plays)
    {
        for (int slot = 0; slot < cardTitleTexts.Count; slot++)
        {
            int index = CardIndexForSlot(slot, plays.Count);
            bool selected = index == playIndex && plays.Count > 0;

            if (index < 0)
            {
                SetCard(slot, "", "", false);
                continue;
            }

            var play = plays[index];
            SetCard(slot,
                play != null ? play.DisplayName : "(missing)",
                play != null ? DefenseSummary(play) : "",
                selected);
        }
    }

    int CardIndexForSlot(int slot, int count)
    {
        if (count == 0) return -1;

        // Keep the selected play in the center whenever possible, while allowing the
        // carousel to clamp naturally at either end.
        int start = Mathf.Clamp(playIndex - 2, 0, Mathf.Max(0, count - 5));
        int index = start + slot;
        return index < count ? index : -1;
    }

    void SetCard(int slot, string title, string detail, bool selected)
    {
        cardTitleTexts[slot].text = title;
        cardDetailTexts[slot].text = detail;
        cardTitleTexts[slot].color = selected ? activeColor : textColor;
        cardDetailTexts[slot].color = selected ? activeColor : mutedColor;
        cardBackgrounds[slot].color = selected
            ? new Color(activeColor.r, activeColor.g, activeColor.b, 0.22f)
            : cardColor;
    }

    static string DefenseSummary(DefensivePlayData play)
    {
        return $"RUSH {play.RushCount}  ZONE {play.ZoneCount}  MAN {play.ManCount}";
    }

    string BuildSituationText()
    {
        var downs = DownsTracker.Instance;
        if (downs == null) return "CHOOSE YOUR PLAY";

        string distance = downs.IsGoalToGo ? "GOAL" : Mathf.CeilToInt(downs.YardsToGo).ToString();
        return $"{Ordinal(downs.CurrentDown)} & {distance}   |   ON {Mathf.RoundToInt(downs.YardLine)}";
    }

    static TextMeshProUGUI CreateText(Transform parent, string value, int size, TextAlignmentOptions alignment)
    {
        var go = new GameObject("Text", typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;

        // Use the project's legible font when available. The attitude/display font can
        // be introduced later without changing this selector's behavior.
        return text;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static string Ordinal(int down) => down switch
    {
        1 => "1ST",
        2 => "2ND",
        3 => "3RD",
        4 => "4TH",
        _ => $"{down}TH"
    };
}
