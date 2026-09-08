using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Global input buffer singleton. Queues button presses with a 100ms window
// to forgive mistimed inputs (pressed during state transitions).
// PlayerStateMachine checks this buffer to decide which move to trigger.
// ReceiverSelectionUI queues "Pass" inputs programmatically when receiver selected.
public class InputBuffer : MonoBehaviour
{
    public static InputBuffer Instance { get; private set; }

    struct BufferedInput
    {
        public string action;
        public float timestamp;
    }

    [SerializeField] float bufferWindow = 0.1f;
    List<BufferedInput> buffer = new();
    InputSystem_Actions controls;

    void Awake()
    {
        Instance = this;

        controls = new InputSystem_Actions();
        controls.Player.Juke.performed += ctx => Record("Juke");
        controls.Player.Hurdle.performed += ctx => Record("Hurdle");
        controls.Player.StiffArm.performed += ctx => Record("StiffArm");
        controls.Player.Pass.performed += ctx => Record("Pass");
        controls.Player.Pitch.performed += ctx => Record("Pitch");
    }

    void OnEnable() => controls.Player.Enable();

    void OnDisable() => controls.Player.Disable();

    void OnDestroy() => controls?.Dispose();

    void Record(string action)
    {
        Debug.Log($"[InputBuffer] Recording action: {action}");
        buffer.Add(new BufferedInput { action = action, timestamp = Time.time });
        Debug.Log($"[InputBuffer] Buffer now has {buffer.Count} entries");
    }

    void Update()
    {
        // Expire inputs older than buffer window
        buffer.RemoveAll(b => Time.time - b.timestamp > bufferWindow);
    }

    // Peek without consuming — lets PlayerStateMachine check CanTrigger before committing
    public string PeekEarliestValid(string[] validActions)
    {
        var next = buffer
            .Where(b => validActions.Contains(b.action))
            .OrderBy(b => b.timestamp)
            .Cast<BufferedInput?>()
            .FirstOrDefault();

        return next?.action;
    }

    // Remove an input from the buffer after it's been consumed
    public bool TryConsume(string action)
    {
        int idx = buffer.FindIndex(b => b.action == action);
        if (idx == -1) return false;
        buffer.RemoveAt(idx);
        return true;
    }

    // Public API for external systems (e.g., ReceiverSelectionUI) to queue inputs programmatically
    public void QueueInput(string action)
    {
        Record(action);
    }
}