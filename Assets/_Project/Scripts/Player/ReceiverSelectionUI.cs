using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

// Runtime UI for receiver selection. Displays 1-4 labels above eligible receivers (RB, WR1-3).
// Pressing 1-4 selects a receiver, queues a Pass input, and sets selectedReceiverIndex
// so PassMove knows which receiver to target.
// Labels fade out when pass is thrown, fade in on play reset.
public class ReceiverSelectionUI : MonoBehaviour
{
    public static ReceiverSelectionUI Instance { get; private set; }

    [SerializeField] Canvas uiCanvas;
    [SerializeField] PlayState playState;

    int selectedReceiverIndex = -1;
    readonly List<Transform> receivers = new();
    RectTransform[] receiverButtons;
    TextMeshProUGUI[] receiverLabels;
    CanvasGroup canvasGroup;
    float shownAt;

    void Awake()
    {
        Instance = this;
        if (playState == null)
            playState = FindAnyObjectByType<PlayState>();
    }

    void Start()
    {
        Debug.Log("[ReceiverSelectionUI] Start()");
        if (playState == null)
        {
            Debug.LogError("[ReceiverSelectionUI] PlayState is NULL");
            return;
        }

        // Add CanvasGroup for fade in/out
        canvasGroup = uiCanvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = uiCanvas.gameObject.AddComponent<CanvasGroup>();

        Debug.Log($"[ReceiverSelectionUI] PlayState found. offensivePlayers count: {playState.OffensivePlayers.Count}");
        CreateReceiverButtons();
        UpdateLabelPositions();

        // Subscribe to play state events
        if (playState != null)
        {
            playState.OnPlayReset += ShowLabels;
            playState.OnPlayEnded += HideLabels;
        }
    }

    void OnDestroy()
    {
        if (playState != null)
        {
            playState.OnPlayReset -= ShowLabels;
            playState.OnPlayEnded -= HideLabels;
        }
    }

    void Update()
    {
        // Listen for 1, 2, 3, 4 key presses to select receiver and pass directly
        if (Input.GetKeyDown(KeyCode.Alpha1)) TryPassToReceiver(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TryPassToReceiver(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) TryPassToReceiver(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) TryPassToReceiver(3);
    }

    void LateUpdate()
    {
        if (playState == null || !playState.IsLive) return;
        UpdateLabelPositions();

        // A short scale-in makes the prompt feel like it pops above the receiver.
        float scale = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.12f));
        if (receiverButtons != null)
        {
            foreach (RectTransform button in receiverButtons)
                if (button != null) button.localScale = Vector3.one * scale;
        }
    }

    // Create sprite-backed 1-4 prompts. The buttons are generated at runtime so the
    // scene needs no hand-authored UI children or per-receiver setup.
    void CreateReceiverButtons()
    {
        if (playState.OffensivePlayers == null) return;

        receivers.Clear();
        receivers.AddRange(ReceiverTargeting.GetEligibleReceivers(playState.OffensivePlayers));
        receiverButtons = new RectTransform[receivers.Count];
        receiverLabels = new TextMeshProUGUI[receivers.Count];
        Sprite buttonSprite = CreateButtonSprite();

        for (int i = 0; i < receivers.Count; i++)
        {
            var buttonObj = new GameObject($"PassPrompt_{i + 1}", typeof(RectTransform), typeof(Image));
            buttonObj.transform.SetParent(uiCanvas.transform, false);
            var button = buttonObj.GetComponent<RectTransform>();
            button.sizeDelta = new Vector2(48f, 48f);
            var image = buttonObj.GetComponent<Image>();
            image.sprite = buttonSprite;
            image.color = new Color(0.08f, 0.12f, 0.2f, 0.94f);

            var labelObj = new GameObject("Number", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(buttonObj.transform, false);

            var label = labelObj.GetComponent<TextMeshProUGUI>();
            label.text = $"{i + 1}";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 30;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.raycastTarget = false;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            receiverButtons[i] = button;
            receiverLabels[i] = label;
            Debug.Log($"[ReceiverSelectionUI] Created pass prompt {i + 1} for {receivers[i].name}");
        }

        shownAt = Time.unscaledTime;
    }

    // Move labels to screen space above each receiver's head
    void UpdateLabelPositions()
    {
        if (receiverLabels == null) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        for (int i = 0; i < receivers.Count; i++)
        {
            if (receivers[i] == null) continue;

            Vector3 worldPos = receivers[i].position + Vector3.up * 2.4f;
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

            // Only show if on-screen and in front of camera
            if (screenPos.z > 0 && screenPos.x > 0 && screenPos.x < Screen.width && screenPos.y > 0 && screenPos.y < Screen.height)
            {
                receiverButtons[i].position = screenPos;
                receiverLabels[i].color = Color.white;
                receiverButtons[i].gameObject.SetActive(true);
            }
            else
            {
                receiverButtons[i].gameObject.SetActive(false);
            }
        }
    }

    // Pressing 1-4 triggers this: set selected receiver and queue Pass
    void TryPassToReceiver(int uiIndex)
    {
        if (playState == null || playState.OffensivePlayers == null) return;
        if (!playState.IsLive) return;
        if (BallController.Instance == null || BallController.Instance.Carrier == null) return;

        int receiverCount = 0;
        for (int i = 0; i < playState.OffensivePlayers.Count; i++)
        {
            if (playState.OffensivePlayers[i] == null) continue;
            if (playState.OffensivePlayers[i].GetComponent<ReceiverAI>() == null) continue;

            if (receiverCount == uiIndex)
            {
                selectedReceiverIndex = uiIndex;
                Debug.Log($"[ReceiverSelectionUI] Selected receiver #{uiIndex}");

                // Resolve live — queue the Pass on the CARRIER's own InputBuffer, not a
                // cached/global one. Only the carrier's buffer is enabled right now
                // (PossessionController), so this has to target that specific instance.
                var carrierBuffer = BallController.Instance.Carrier.GetComponent<InputBuffer>();
                if (carrierBuffer != null)
                {
                    carrierBuffer.QueueInput("Pass");
                }
                else
                {
                    Debug.LogError("[ReceiverSelectionUI] Carrier has no InputBuffer!");
                }

                HideLabels();
                return;
            }
            receiverCount++;
        }
    }

    // Fade out labels when pass is thrown
    void HideLabels(PlayState.PlayEndReason reason)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    // Overload: fade out without reason parameter (called from TryPassToReceiver)
    void HideLabels()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    // Fade in labels on play reset
    void ShowLabels()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        shownAt = Time.unscaledTime;
    }

    // Public getters for PassMove
    public int GetSelectedReceiverIndex() => selectedReceiverIndex;

    public void ResetSelection() => selectedReceiverIndex = -1;

    Sprite CreateButtonSprite()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "PassPromptButtonSprite";
        texture.hideFlags = HideFlags.HideAndDontSave;
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center);
            float alpha = Mathf.InverseLerp(radius + 1f, radius - 1f, distance);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}
