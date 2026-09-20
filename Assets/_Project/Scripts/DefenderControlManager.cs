using UnityEngine;
using UnityEngine.InputSystem;

// Decides WHICH defender the human is piloting while their team is on defense, and owns
// switching. Sits on GameManager next to DefenderCoordinator (same per-frame coordinator
// pattern). Does nothing at all while the user has the ball — the carrier-side stack
// (PossessionController) handles that case.
//
// Selection rules:
//   - PRE-SNAP (dead ball): the switch button loops the defense array in formation-slot
//     order, 0 through 6 and back to 0. Each new dead ball starts at 0.
//   - At the snap: the defender closest to the ball, unless the human already picked one
//     pre-snap.
//   - POST-SNAP: the switch button toggles between the two defenders closest to the ball
//     (the closest one that isn't the current guy — if you're not one of the two, you go
//     to the closest).
//   - Auto-switch whenever the ball changes hands or state (throw, catch, handoff, fumble):
//     to the closest defender to where the ball is, or where a pass is headed. That's what
//     puts you in position to break up a throw without having to guess.
//
// "Closest to the ball" = the carrier while held, the flight target while a pass/pitch is
// in the air, the ball itself when loose.
public class DefenderControlManager : MonoBehaviour
{
    public static DefenderControlManager Instance { get; private set; }

    [Header("Controlled Defender Feel (mirrors PlayerMovement)")]
    [Tooltip("Base top speed of the human-piloted defender, x DefenderAttributes.speedMult. AI defenders chase at DefenderAI.baseMoveSpeed (5); the ball carrier tops out at 8.")]
    [SerializeField] float maxSpeed = 7f;
    [SerializeField] float acceleration = 40f;
    [SerializeField] float deceleration = 60f;
    [SerializeField] float rotationSpeed = 720f;

    [Header("Switching")]
    [Tooltip("Seconds between manual switches — stops one press from double-firing across two frames.")]
    [SerializeField] float switchCooldown = 0.15f;

    public float MaxSpeed => maxSpeed;
    public float Acceleration => acceleration;
    public float Deceleration => deceleration;
    public float RotationSpeed => rotationSpeed;

    InputSystem_Actions controls;
    DefenderControl current;

    bool pickedPreSnap;   // human chose a defender during the dead ball — don't overwrite it at the snap
    float lastSwitchTime = float.NegativeInfinity;

    // Ball-change detection for auto-switching.
    bool hasLastBall;
    BallController.BallState lastState;
    Transform lastCarrier;

    void Awake()
    {
        Instance = this;
        controls = new InputSystem_Actions();
        controls.Player.SwitchPlayer.performed += HandleSwitchPressed;
    }

    void OnEnable() => controls.Player.Enable();

    // Subscribed in Start(), not OnEnable() — PlayState.Instance is only guaranteed to
    // exist after every Awake() has run (same reason BallController does this).
    void Start()
    {
        if (PlayState.Instance == null) return;
        PlayState.Instance.OnPlayReset += HandlePlayReset;
        PlayState.Instance.OnPlayEnded += HandlePlayEnded;
        PlayState.Instance.OnPossessionChanged += HandlePossessionChanged;
    }

    void OnDisable()
    {
        controls.Player.Disable();
        Release();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (PlayState.Instance != null)
        {
            PlayState.Instance.OnPlayReset -= HandlePlayReset;
            PlayState.Instance.OnPlayEnded -= HandlePlayEnded;
            PlayState.Instance.OnPossessionChanged -= HandlePossessionChanged;
        }
        controls?.Dispose();
    }

    void Update()
    {
        var play = PlayState.Instance;
        if (play == null) return;

        // User has the ball: normal carrier control applies, nobody on defense is ours.
        if (!play.IsUserDefending)
        {
            Release();
            return;
        }

        // First frame of defense, a new dead ball, or the piloted guy became invalid
        // (possession change, destroyed) — pick a default: slot 0 of the defense array
        // pre-snap, closest to the ball once live.
        if (!IsValid(current))
            Select(play.IsLive ? ClosestUserDefenderTo(FocusPoint(), null) : NextInDefenseArray(null));

        if (play.IsLive) AutoSwitchOnBallChange();
    }

    // ---- Switching -------------------------------------------------------------

    void HandleSwitchPressed(InputAction.CallbackContext _)
    {
        var play = PlayState.Instance;
        if (play == null || !play.IsUserDefending) return;
        if (Time.time - lastSwitchTime < switchCooldown) return;

        // Dead ball: step through the defense array. Live ball: the closest defender to the
        // ball that isn't the current one (= toggling between the two closest).
        var next = play.IsLive
            ? ClosestUserDefenderTo(FocusPoint(), current)
            : NextInDefenseArray(current);
        if (next == null) return;

        lastSwitchTime = Time.time;
        if (!play.IsLive) pickedPreSnap = true;
        Select(next);
    }

    void HandlePlayReset()
    {
        // Ball-change tracking restarts at the snap so the first live frame doesn't read
        // "ball just went from huddle-QB to snap-QB" as a change worth switching for.
        hasLastBall = false;

        var play = PlayState.Instance;
        if (play == null || !play.IsUserDefending) return;
        if (!pickedPreSnap) Select(ClosestUserDefenderTo(FocusPoint(), null));
    }

    // New dead ball: forget last down's pick and drop control so Update() re-selects slot 0.
    void HandlePlayEnded(PlayState.PlayEndReason _)
    {
        pickedPreSnap = false;
        Release();
    }

    void HandlePossessionChanged(int _) => Release(); // Update() re-selects if the user is now defending

    void AutoSwitchOnBallChange()
    {
        var ball = BallController.Instance;
        if (ball == null) return;

        if (!hasLastBall)
        {
            hasLastBall = true;
            lastState = ball.State;
            lastCarrier = ball.Carrier;
            return;
        }

        if (ball.State == lastState && ball.Carrier == lastCarrier) return;

        lastState = ball.State;
        lastCarrier = ball.Carrier;
        Select(ClosestUserDefenderTo(FocusPoint(), null));
    }

    // ---- Selection -------------------------------------------------------------

    bool IsValid(DefenderControl c)
    {
        if (c == null || !c.gameObject.activeInHierarchy) return false;
        return c.TryGetComponent<TeamMember>(out var member) && PlayState.Instance.IsUserTeam(member.teamId);
    }

    void Select(DefenderAI ai)
    {
        if (ai == null || (current != null && current.gameObject == ai.gameObject)) return;

        Release();

        // Lazily added: no prefab surgery, and only defenders the human has actually
        // controlled ever carry the component.
        if (!ai.TryGetComponent<DefenderControl>(out var control))
            control = ai.gameObject.AddComponent<DefenderControl>();

        current = control;
        current.SetControlled(true);
    }

    void Release()
    {
        if (current == null) return;
        current.SetControlled(false);
        current = null;
    }

    // Next defender after `from` in FormationData.defenderSlots order, wrapping 6 -> 0.
    // from == null (or not in the array) starts at index 0. Skips empty slots and anyone
    // without DefenderAI.
    static DefenderAI NextInDefenseArray(DefenderControl from)
    {
        var defense = PlayState.Instance.DefensePlayers;
        int count = defense.Count;
        if (count == 0) return null;

        int start = -1;
        if (from != null)
        {
            for (int i = 0; i < count; i++)
            {
                if (defense[i] != null && defense[i].gameObject == from.gameObject) { start = i; break; }
            }
        }

        for (int step = 1; step <= count; step++)
        {
            int i = (start + step) % count; // start = -1 -> first probe is index 0
            if (defense[i] != null && defense[i].TryGetComponent<DefenderAI>(out var ai)) return ai;
        }

        return null;
    }

    // Where the action is: the carrier while held, where the pass is going while in
    // flight, the ball itself when loose.
    static Vector3 FocusPoint()
    {
        var ball = BallController.Instance;
        if (ball == null) return Vector3.zero;

        switch (ball.State)
        {
            case BallController.BallState.InFlight: return ball.FlightTarget;
            case BallController.BallState.Held when ball.Carrier != null: return ball.Carrier.position;
            default: return ball.transform.position;
        }
    }

    // Closest defender on the user's team to a point, optionally skipping one. Resolved
    // live via TeamMember — no cached roster, same rule as the rest of the project.
    static DefenderAI ClosestUserDefenderTo(Vector3 point, DefenderControl exclude)
    {
        DefenderAI best = null;
        float bestDist = float.MaxValue;

        foreach (var member in FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!PlayState.Instance.IsUserTeam(member.teamId)) continue;
            if (!member.TryGetComponent<DefenderAI>(out var ai)) continue;
            if (exclude != null && member.gameObject == exclude.gameObject) continue;

            float dist = Vector3.Distance(member.transform.position, point);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = ai;
            }
        }

        return best;
    }
}
