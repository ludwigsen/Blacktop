using UnityEngine;

[RequireComponent(typeof(PlayerStateMachine))]
public class TackleContact : MonoBehaviour
{
    [SerializeField] string defenderTag = "Defender";
    [SerializeField] float contactRadius = 0.8f;

    // Testing value — no ballSecurity-style attribute exists yet, so this is a flat
    // base chance rather than attribute-scaled (mirrors StiffArmMove's baseShedChance
    // pattern, minus the multiplier since there's nothing to multiply by yet).
    [SerializeField] float baseFumbleChance = 0.5f;

    PlayerStateMachine stateMachine;

    void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
    }

    void Update()
    {
        if (PlayState.Instance == null || !PlayState.Instance.IsLive) return;
        if (PlayState.Instance.IsPostSnapGraceActive) return;
        if (stateMachine != null && stateMachine.IsTackleImmune) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, contactRadius);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag(defenderTag)) continue;

            // Sack — specifically the designated passer, tackled behind the current LOS,
            // before ever throwing. Not "any tackle for loss" — a receiver tackled behind
            // the LOS after a catch doesn't count.
            if (PlayState.Instance.Passer == transform && transform.position.z < PlayState.Instance.CurrentLineOfScrimmageZ)
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
