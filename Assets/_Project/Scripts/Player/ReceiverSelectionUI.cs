using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Runtime UI — displays receiver index labels above heads, handles keyboard selection (1/2/3).
// Sits on the GameManager or main canvas. Needs TextMesh Pro for labels.
public class ReceiverSelectionUI : MonoBehaviour
{
    [SerializeField] Canvas uiCanvas;
    [SerializeField] PlayState playState;

    int selectedReceiverIndex = -1; // -1 = auto-select (PassMove default behavior)
    TextMeshProUGUI[] receiverLabels;

    void Awake()
    {
        if (playState == null) playState = PlayState.Instance;
    }

    void OnEnable()
    {
        // Subscribe to input
        var controls = new InputSystem_Actions();
        controls.Player.Enable();
        controls.Player.Juke.performed += ctx => TrySelectReceiver(0);
        controls.Player.Hurdle.performed += ctx => TrySelectReceiver(1);
        controls.Player.StiffArm.performed += ctx => TrySelectReceiver(2);
        // Alternative: add explicit receiver-select actions to InputSystem_Actions if you prefer dedicated keys
    }

    void Start()
    {
        if (playState == null) return;
        CreateReceiverLabels();
        UpdateLabelPositions();
    }

    void LateUpdate()
    {
        if (playState == null || !playState.IsLive) return;
        UpdateLabelPositions();
    }

    void CreateReceiverLabels()
    {
        if (playState.offensivePlayers == null) return;

        receiverLabels = new TextMeshProUGUI[playState.offensivePlayers.Count];

        for (int i = 0; i < receiverLabels.Length; i++)
        {
            var labelObj = new GameObject($"ReceiverLabel_{i}");
            labelObj.transform.SetParent(uiCanvas.transform, false);

            var label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = $"{i + 1}";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 36;

            receiverLabels[i] = label;
        }
    }

    void UpdateLabelPositions()
    {
        if (receiverLabels == null || playState.offensivePlayers == null) return;

        for (int i = 0; i < receiverLabels.Length; i++)
        {
            if (receiverLabels[i] == null || playState.offensivePlayers[i] == null) continue;

            Vector3 worldPos = playState.offensivePlayers[i].position + Vector3.up * 2f; // offset above head
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // Only show if on-screen
            if (screenPos.z > 0)
            {
                receiverLabels[i].transform.position = screenPos;
                receiverLabels[i].color = i == selectedReceiverIndex ? Color.green : Color.white;
            }
            else
            {
                receiverLabels[i].color = Color.clear;
            }
        }
    }

    void TrySelectReceiver(int index)
    {
        if (playState == null || playState.offensivePlayers == null) return;

        // Clamp to valid receiver range (skip QB at index 0 if used, adjust per your lineup)
        if (index >= 0 && index < playState.offensivePlayers.Count)
        {
            selectedReceiverIndex = index;
            Debug.Log($"Receiver {index + 1} selected");
        }
    }

    // Public API for PassMove to check selection
    public int GetSelectedReceiverIndex() => selectedReceiverIndex;

    // Call this on ResetPlay to clear selection
    public void ResetSelection() => selectedReceiverIndex = -1;
}