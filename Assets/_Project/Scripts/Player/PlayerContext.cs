using UnityEngine;

// Shared state bundle passed into every IPlayerMove (Juke, Hurdle, StiffArm, future moves).
// This is the single channel moves use to touch the outside world — transform, attributes,
// live input, cooldown — so individual move scripts never need their own component
// references or Input.* calls. Keeps moves self-contained and easy to add/remove.
public class PlayerContext
{
    public Transform transform;
    public PlayerAttributes attributes;
    public InputBuffer inputBuffer;

    // Routes to PlayerMovement.CurrentMoveInput. Moves read live stick/key direction through
    // this delegate rather than calling Input.GetAxisRaw or the new Input System directly —
    // keeps input-reading centralized in PlayerMovement, moves just consume the result.
    public System.Func<Vector2> getMoveInput;

    // Move calls this in its own Exit() to start the shared cooldown timer. Cooldown begins
    // at move EXIT, not at trigger — a 0.3s move with a 0.2s cooldown measured from trigger
    // would expire before the move even finished playing out.
    public System.Action<float> setCooldown;
}