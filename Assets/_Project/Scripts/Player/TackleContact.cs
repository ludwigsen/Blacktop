using UnityEngine;

[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(TeamMember))]
public class TackleContact : MonoBehaviour
{
    [SerializeField] float contactRadius = 0.8f;

    // Neutral-vs-neutral contact produces baseFumbleChance * 0.5 because both
    // Tackling and Carrying are 1.0 at rating 10. Higher Tackling raises the chance;
    // higher Carrying lowers it. The clamp prevents extreme ratings from producing
    // either an automatic fumble or an impossible one.
    [SerializeField, Range(0f, 1f)] float baseFumbleChance = 0.5f;
    [SerializeField, Range(0.01f, 1f)] float minFumbleChance = 0.05f;
    [SerializeField, Range(0f, 1f)] float maxFumbleChance = 0.8f;

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

        // Only the CURRENT ball carrier can be tackled. Without this, a player who just
        // fumbled (or threw/pitched) keeps eating tackle checks every frame they're standing
        // near an opponent, and — since the ball's already gone — falls straight through to
        // an incorrect EndPlay(Tackled) before the fumble's pop-arc has even landed.
        // Resolved live rather than trusting PossessionController's enable/disable timing,
        // which races against this component with no defined execution order.
        if (BallController.Instance == null || BallController.Instance.Carrier != transform) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, contactRadius);
        foreach (var hit in hits)
        {
            // Anyone on a different team can tackle you — this is what makes tackling
            // work regardless of which side is currently on offense, instead of only
            // ever recognizing a hardcoded "Defender" tag.
            var otherTeam = hit.GetComponentInParent<TeamMember>();
            if (otherTeam == null) continue;
            if (otherTeam.teamId == teamMember.teamId) continue;

            // Sack — specifically the designated passer, tackled behind the current LOS,
            // before ever throwing. Not "any tackle for loss" — a receiver tackled behind
            // the LOS after a catch doesn't count.
            if (PlayState.Instance.Passer == transform && PlayState.Instance.YardsPastLineOfScrimmage(transform.position.z) < 0f)
                PlayState.Instance.AddDefensePoints(8f);

            if (BallController.Instance != null && BallController.Instance.IsHeld)
            {
                bool guaranteed = PlayState.Instance.ConsumeGuaranteedTurnover();
                float fumbleChance = CalculateFumbleChance(hit.transform);
                if (guaranteed || Random.value < fumbleChance)
                {
                    BallController.Instance.Fumble(transform.position, hit.transform.position); // was: Drop()
                    PlayState.Instance.AddDefensePoints(8f);
                    PlayState.Instance.NotifyFumble();
                    // A fumble remains a live ball. BallController's recovery pass runs in
                    // LateUpdate once the pop-arc lands, so ending the play here would prevent
                    // any recovery.
                    return;
                }
            }

            PlayState.Instance.EndPlay(PlayState.PlayEndReason.Tackled);
            return;
        }
    }

    float CalculateFumbleChance(Transform tackler)
    {
        // Use the same runtime attributes that the rest of the player stack consumes.
        // Tackling belongs to the tackler; Carrying belongs to the current carrier.
        var carrierAttributes = stateMachine != null ? stateMachine.Attributes : null;
        var tacklerState = tackler.GetComponent<PlayerStateMachine>();

        float carrying = carrierAttributes != null ? carrierAttributes.Carrying() : 1f;
        float tackling = tacklerState != null && tacklerState.Attributes != null
            ? tacklerState.Attributes.Tackling()
            : 1f;

        float matchup = tackling / Mathf.Max(0.01f, carrying);
        return Mathf.Clamp(baseFumbleChance * matchup, minFumbleChance, maxFumbleChance);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, contactRadius);
    }
}