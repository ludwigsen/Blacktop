using UnityEngine;
using TMPro;
using System.Collections.Generic;

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
    TextMeshProUGUI[] receiverLabels;
    CanvasGroup canvasGroup;

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
        CreateReceiverLabels();
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
    }

    // Create 1-4 labels, one for each receiver (skip OL, QB)
    void CreateReceiverLabels()
    {
        if (playState.OffensivePlayers == null) return;

        var receiversList = new List<TextMeshProUGUI>();

        for (int i = 0; i < playState.OffensivePlayers.Count; i++)
        {
            var player = playState.OffensivePlayers[i];
            if (player == null) continue;

            // Only create labels for players with ReceiverAI component
            var receiverAI = player.GetComponent<ReceiverAI>();
            if (receiverAI == null) continue;

            var labelObj = new GameObject($"ReceiverLabel_{i}");
            labelObj.transform.SetParent(uiCanvas.transform, false);

            var label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = $"{receiversList.Count + 1}"; // Label as 1, 2, 3, 4
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 36;
            label.color = Color.white;

            receiversList.Add(label);
            Debug.Log($"[ReceiverSelectionUI] Created label {receiversList.Count} for receiver at slot {i}");
        }

        receiverLabels = receiversList.ToArray();
    }

    // Move labels to screen space above each receiver's head
    void UpdateLabelPositions()
    {
        if (receiverLabels == null || playState.OffensivePlayers == null) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        int labelIndex = 0;
        for (int i = 0; i < playState.OffensivePlayers.Count; i++)
        {
            if (playState.OffensivePlayers[i] == null) continue;
            if (playState.OffensivePlayers[i].GetComponent<ReceiverAI>() == null) continue;
            if (labelIndex >= receiverLabels.Length) break;

            Vector3 worldPos = playState.OffensivePlayers[i].position + Vector3.up * 2f;
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

            // Only show if on-screen and in front of camera
            if (screenPos.z > 0 && screenPos.x > 0 && screenPos.x < Screen.width && screenPos.y > 0 && screenPos.y < Screen.height)
            {
                RectTransform rectTransform = receiverLabels[labelIndex].GetComponent<RectTransform>();
                rectTransform.position = screenPos;
                receiverLabels[labelIndex].color = Color.white;
            }
            else
            {
                receiverLabels[labelIndex].color = new Color(1, 1, 1, 0); // transparent
            }

            labelIndex++;
        }
    }

    // Pressing 1-4 triggers this: set selected receiver and queue Pass
    void TryPassToReceiver(int uiIndex)
    {
        if (playState == null || playState.OffensivePlayers == null) return;
        if (!playState.IsLive) return;

        // Map UI index (0-3) to actual receiver in the list
        int receiverCount = 0;
        for (int i = 0; i < playState.OffensivePlayers.Count; i++)
        {
            if (playState.OffensivePlayers[i] == null) continue;
            if (playState.OffensivePlayers[i].GetComponent<ReceiverAI>() == null) continue;

            if (receiverCount == uiIndex)
            {
                selectedReceiverIndex = uiIndex;
                Debug.Log($"[ReceiverSelectionUI] Selected receiver #{uiIndex}");

                // Queue Pass input
                if (InputBuffer.Instance != null)
                {
                    Debug.Log($"[ReceiverSelectionUI] About to call InputBuffer.QueueInput('Pass')");
                    InputBuffer.Instance.QueueInput("Pass");
                    Debug.Log($"[ReceiverSelectionUI] QueueInput returned");
                }
                else
                {
                    Debug.LogError("[ReceiverSelectionUI] InputBuffer.Instance is NULL!");
                }

                HideLabels();
                return;
            }
            receiverCount++;
        }

        Debug.LogWarning($"[ReceiverSelectionUI] No receiver found at UI index {uiIndex}");
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
    }

    // Public getters for PassMove
    public int GetSelectedReceiverIndex() => selectedReceiverIndex;

    public void ResetSelection() => selectedReceiverIndex = -1;
}