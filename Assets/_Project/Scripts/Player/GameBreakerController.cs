using UnityEngine;
using UnityEngine.InputSystem;

// Each player listens to input, but only the canonical controlled player may spend the team meter.
[RequireComponent(typeof(TeamMember))]
public class GamebreakerController : MonoBehaviour
{
    [SerializeField] PlayerAttributes attributes;
    [SerializeField] float basePointsPerSecond = 2.5f;
    public void SetAttributes(PlayerAttributes value) => attributes = value;
    InputSystem_Actions controls;
    TeamMember member;
    bool isStyling;

    void Awake()
    {
        member = GetComponent<TeamMember>();
        controls = new InputSystem_Actions();
        controls.Player.Style.performed += _ => isStyling = true;
        controls.Player.Style.canceled += _ => isStyling = false;
        controls.Player.Gamebreaker.performed += _ => TryActivate();
    }

    void OnEnable() => controls.Player.Enable();
    void OnDisable()
    {
        controls.Player.Disable();
        isStyling = false;
    }
    void OnDestroy() => controls?.Dispose();

    bool IsControlledLive => PlayState.Instance != null && PlayState.Instance.IsLive &&
        !PlayState.Instance.IsGameOver && PossessionController.ActivelyControlled == transform &&
        PlayState.Instance.IsUserTeam(member.teamId);

    void Update()
    {
        // Styling earns points only with a held ball; defense uses its existing tackle/turnover awards.
        if (!isStyling || !IsControlledLive || BallController.Instance == null ||
            !BallController.Instance.IsHeld || BallController.Instance.Carrier != transform) return;
        float swagger = attributes != null ? attributes.Swagger() : 1f;
        PlayState.Instance.AddTeamPoints(member.teamId, basePointsPerSecond * swagger * Time.deltaTime);
    }

    public bool TryActivate() => IsControlledLive && PlayState.Instance.TryActivateGamebreaker(member.teamId);
}
