using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Per-player input buffer — NOT a singleton. PossessionController enables/disables
// each player's own InputBuffer based on live possession, so "the" active buffer is
// whichever one is currently enabled on the ball carrier. Never cache a single global
// instance here — that breaks possession routing the moment control changes hands.
public class InputBuffer : MonoBehaviour
{
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

    void Record(string action) =>
        buffer.Add(new BufferedInput { action = action, timestamp = Time.time });

    void Update()
    {
        buffer.RemoveAll(b => Time.time - b.timestamp > bufferWindow);
    }

    public string PeekEarliestValid(string[] validActions)
    {
        var next = buffer
            .Where(b => validActions.Contains(b.action))
            .OrderBy(b => b.timestamp)
            .Cast<BufferedInput?>()
            .FirstOrDefault();

        return next?.action;
    }

    public bool TryConsume(string action)
    {
        int idx = buffer.FindIndex(b => b.action == action);
        if (idx == -1) return false;
        buffer.RemoveAt(idx);
        return true;
    }

    // Programmatic queue — used by ReceiverSelectionUI to inject a Pass press on
    // the CARRIER's buffer specifically (resolved live, not cached — see caller).
    public void QueueInput(string action)
    {
        Record(action);
    }
}