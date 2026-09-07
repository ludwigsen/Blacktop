using UnityEngine;
using UnityEngine.InputSystem;

// Player-facing Gamebreaker interactions: hold "Style" to fill the meter (rate scaled by
// Swagger), press "Gamebreaker" to activate once full. Gated entirely on "am I the live
// ball carrier right now" — no need to coordinate with PossessionController's
// Controlled/AI/Frozen modes, since a non-carrier just never satisfies that check anyway.
// Safe to leave enabled on every human-controllable offensive slot (UserPlayer + any
// skill-position Ally that can receive a pitch/pass and become the carrier).
//
// REQUIRES two new actions in the Player action map of InputSystem_Actions.inputactions:
//   "Style"       — Button, held (e.g. keyboard T, gamepad right shoulder)
//   "Gamebreaker" — Button, press (e.g. keyboard G, gamepad Y/triangle)
// Name them exactly this — the generated wrapper class property names come from these.
// Add them via the Input Actions editor window, not by hand-editing the .inputactions
// JSON — letting Unity's importer regenerate InputSystem_Actions.cs is the safe path.
public class GamebreakerController : MonoBehaviour
{
    [SerializeField] PlayerAttributes attributes;
    [SerializeField] float basePointsPerSecond = 2.5f; // at neutral (10) Swagger — matches design spec

    InputSystem_Actions controls;
    bool isStyling;

    void Awake()
    {
        controls = new InputSystem_Actions();
        controls.Player.Style.performed += HandleStylePerformed;
        controls.Player.Style.canceled += HandleStyleCanceled;
        controls.Player.Gamebreaker.performed += HandleGamebreakerPerformed;
    }

    void OnEnable()
    {
        controls.Player.Enable();
    }

    void OnDisable()
    {
        controls.Player.Disable();
        isStyling = false;
    }

    void OnDestroy() => controls?.Dispose();

    void HandleStylePerformed(InputAction.CallbackContext _) => isStyling = true;
    void HandleStyleCanceled(InputAction.CallbackContext _) => isStyling = false;
    void HandleGamebreakerPerformed(InputAction.CallbackContext _) => TryActivate();

    // Resolved live every frame rather than cached — same "resolve live, don't cache"
    // rule as everything else that checks ball possession. Whoever's carrying right now
    // is the only one who can style or activate.
    bool IsLiveCarrier =>
        PlayState.Instance != null && PlayState.Instance.IsLive &&
        BallController.Instance != null && BallController.Instance.Carrier == transform;

    void Update()
    {
        if (!isStyling || !IsLiveCarrier) return;

        // Swagger() is already a curve-evaluated multiplier centered on 1.0 at rating 10 —
        // this is literally "Swagger tells the system how quickly to fill it" with zero
        // extra math needed, since that's exactly what the existing attribute pipeline does.
        float rate = basePointsPerSecond * attributes.Swagger();
        PlayState.Instance.AddOffensePoints(rate * Time.deltaTime);
    }

    void TryActivate()
    {
        if (!IsLiveCarrier) return;
        PlayState.Instance.TryActivateOffenseGamebreaker();
    }
}
