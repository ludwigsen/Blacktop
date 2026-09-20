using UnityEngine;

[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(TeamMember))]
public class TackleContact : MonoBehaviour
{
    [SerializeField] float contactRadius = 0.8f;

    // Testing value — no ballSecurity-style attribute exists yet, so this is a flat
    // base chance rather than attribute-scaled (mirrors StiffArmMove's baseShedChance
    // pattern, minus the multiplier since there's nothing to multiply by yet).
    [SerializeField] float baseFumbleChance = 0.5f;

    PlayerStateMachine stateMachine;
    TeamMember teamMember;

    void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
        teamMember = GetComponent<TeamMember>();
    }

    void Update()
    {
        if (PlayState.Instance == null || !PlayState.Instance.IsLive) return;
        if (PlayState.Instance.IsPostSnapGraceActive) return;
        if (stateMachine != null && stateMachine.IsTackleImmune) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, contactRadius);
        foreach (var hit in hits)
        {
            // Anyone on a different team can tackle you — this is what makes tackling
            // work regardless of which side is currently on offense, instead of only
            // ever recognizing a hardcoded "Defender" tag.
            if (!hit.TryGetComponent<TeamMember>(out var otherTeam)) continue;
            if (otherTeam.teamId == teamMember.teamId) continue;

            // Sack — specifically the designated passer, tackled behind the current LOS,
            // before ever throwing. Not "any tackle for loss" — a receiver tackled behind
            // the LOS after a catch doesn't count.
            if (PlayState.Instance.Passer == transform && PlayState.Instance.YardsPastLineOfScrimmage(transform.position.z) < 0f)
                PlayState.Instance.AddDefensePoints(8f);

            if (BallController.Instance != null && BallController.Instance.IsHeld)
            {
                bool guaranteed = PlayState.Instance.ConsumeGuaranteedTurnover();
                if (guaranteed || Random.value < baseFumbleChance)
                {
                    BallController.Instance.Drop();
                    PlayState.Instance.AddDefensePoints(8f);
                    PlayState.Instance.NotifyFumble();
                    // A fumble remains a live ball. BallController's recovery pass runs in
                    // LateUpdate, so ending the play here would prevent any recovery.
                    return;
                }
            }

            PlayState.Instance.EndPlay(PlayState.PlayEndReason.Tackled);
            return;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, contactRadius);
    }
}