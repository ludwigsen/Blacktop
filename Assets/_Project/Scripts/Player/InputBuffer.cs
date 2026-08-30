using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Rolling input buffer so a Juke/Hurdle press slightly early (still mid-transition,
// or polled mid-frame) isn't silently dropped. Without this, mistimed-by-a-frame
// inputs read as "the controls dropped my input" even though nothing's technically wrong.
public class InputBuffer : MonoBehaviour
{
    // One entry per buffered-but-not-yet-consumed press.
    struct BufferedInput
    {
        public string action;
        public float timestamp; // Time.time at press — used both for expiry and FIFO priority
    }

    // ~100ms landed as the tuned value: fighting-game buffer windows run roughly
    // 50-160ms depending on how forgiving the game wants to feel. Went toward the
    // generous end since Blacktop is arcade, not precision-execution focused.
    [SerializeField] float bufferWindow = 0.1f;

    List<BufferedInput> buffer = new List<BufferedInput>();
    InputSystem_Actions controls;

    void Awake()
    {
        controls = new InputSystem_Actions();
    }

    void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.Juke.performed += ctx => Record("Juke");
        controls.Player.Hurdle.performed += ctx => Record("Hurdle");
        controls.Player.StiffArm.performed += ctx => Record("StiffArm");
        controls.Player.Pass.performed += ctx => Record("Pass");
        controls.Player.Pitch.performed += ctx => Record("Pitch");
    }

    void OnDisable() => controls.Player.Disable();

    void Record(string action) =>
        buffer.Add(new BufferedInput { action = action, timestamp = Time.time });

    void Update()
    {
        // Expire anything older than the buffer window every frame. Cheap enough at this scale (2 actions).
        buffer.RemoveAll(b => Time.time - b.timestamp > bufferWindow);
    }

    // Peek without consuming — lets a caller validate eligibility (e.g. CanTrigger checks,
    // cooldown state) BEFORE committing to consume the input. Without this split, a move
    // that fails its own trigger condition would still eat the buffered press.
    public string PeekEarliestValid(string[] validActions)
    {
        var next = buffer
            .Where(b => validActions.Contains(b.action))
            .OrderBy(b => b.timestamp) // FIFO — whichever button was physically pressed first wins
            .Cast<BufferedInput?>()
            .FirstOrDefault();

        return next?.action;
    }

    // Actually removes the entry from the buffer. Callers should only call this after
    // confirming eligibility via PeekEarliestValid + their own CanTrigger check —
    // consuming during cooldown, for example, should never happen (input just expires instead).
    public bool TryConsume(string action)
    {
        int idx = buffer.FindIndex(b => b.action == action);
        if (idx == -1) return false;
        buffer.RemoveAt(idx);
        return true;
    }
}