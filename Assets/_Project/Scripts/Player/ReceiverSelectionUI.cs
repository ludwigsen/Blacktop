using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

public class ReceiverSelectionUI : MonoBehaviour
{
    public static ReceiverSelectionUI Instance { get; private set; }

    [SerializeField] Canvas uiCanvas;
    [SerializeField] PlayState playState;
    [SerializeField] InputBuffer inputBuffer;

    int selectedReceiverIndex = -1;
    TextMeshProUGUI[] receiverLabels;
    CanvasGroup canvasGroup;

    void Awake()
    {
        Instance = this;
        if (playState == null)
            playState = FindAnyObjectByType<PlayState>();
        if (inputBuffer == null)
            inputBuffer = FindAnyObjectByType<InputBuffer>();
    }

    void Start()
    {
        Debug.Log("ReceiverSelectionUI.Start()");
        if (playState == null)
        {
            Debug.LogError("PlayState is NULL");
            return;
        }

        canvasGroup = uiCanvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = uiCanvas.gameObject.AddComponent<CanvasGroup>();

        Debug.Log($"PlayState found. offensivePlayers count: {playState.OffensivePlayers.Count}");
        CreateReceiverLabels();
        UpdateLabelPositions();

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
        // Listen for 1, 2, 3, 4 key presses to pass directly
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

    void CreateReceiverLabels()
    {
        if (playState.OffensivePlayers == null) return;

        var receiversList = new List<TextMeshProUGUI>();

        for (int i = 0; i < playState.OffensivePlayers.Count; i++)
        {
            var player = playState.OffensivePlayers[i];
            if (player == null) continue;

            var receiverAI = player.GetComponent<ReceiverAI>();
            if (receiverAI == null) continue;

            var labelObj = new GameObject($"ReceiverLabel_{i}");
            labelObj.transform.SetParent(uiCanvas.transform, false);

            var label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = $"{receiversList.Count + 1}";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 36;

            receiversList.Add(label);
            Debug.Log($"Created label {receiversList.Count} for receiver at slot {i}");
        }

        receiverLabels = receiversList.ToArray();
    }

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

            if (screenPos.z > 0 && screenPos.x > 0 && screenPos.x < Screen.width && screenPos.y > 0 && screenPos.y < Screen.height)
            {
                RectTransform rectTransform = receiverLabels[labelIndex].GetComponent<RectTransform>();
                rectTransform.position = screenPos;
                receiverLabels[labelIndex].color = Color.white;
            }
            else
            {
                receiverLabels[labelIndex].color = new Color(1, 1, 1, 0);
            }

            labelIndex++;
        }
    }

    void TryPassToReceiver(int uiIndex)
    {
        if (playState == null || playState.OffensivePlayers == null) return;
        if (!playState.IsLive) return;

        int receiverCount = 0;
        for (int i = 0; i < playState.OffensivePlayers.Count; i++)
        {
            if (playState.OffensivePlayers[i] == null) continue;
            if (playState.OffensivePlayers[i].GetComponent<ReceiverAI>() == null) continue;

            if (receiverCount == uiIndex)
            {
                // Store the UI index (0-3), not the slot index
                selectedReceiverIndex = uiIndex;  // <-- CHANGED: was "= i"
                Debug.Log($"[ReceiverSelectionUI] Selected receiver #{uiIndex}, queuing Pass");

                if (inputBuffer != null)
                {
                    inputBuffer.QueueInput("Pass");
                    Debug.Log($"[ReceiverSelectionUI] Pass queued to InputBuffer");
                }
                else
                {
                    Debug.LogError("[ReceiverSelectionUI] InputBuffer is NULL!");
                }

                HideLabels();
                return;
            }
            receiverCount++;
        }
        Debug.LogWarning($"[ReceiverSelectionUI] No receiver found at UI index {uiIndex}");
    }

    void HideLabels()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    void HideLabels(PlayState.PlayEndReason reason)
    {
        HideLabels(); // Call the parameterless version
    }

    void ShowLabels()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    public int GetSelectedReceiverIndex() => selectedReceiverIndex;

    public void ResetSelection() => selectedReceiverIndex = -1;
}