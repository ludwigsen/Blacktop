using UnityEngine;

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
        if (stateMachine != null && stateMachine.IsTackleImmune) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, contactRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag(defenderTag))
            {
                // Roll BEFORE ending the play — a fumble still ends the play as Tackled
                // (turnover-on-downs style stoppage), it just also drops the ball first.
                // Recovery/pickup logic doesn't exist yet — ball just sits wherever
                // BallController.Drop() leaves it (its last-followed position).
                if (BallController.Instance != null && BallController.Instance.IsHeld
                    && Random.value < baseFumbleChance)
                {
                    BallController.Instance.Drop();
                }

                PlayState.Instance.EndPlay(PlayState.PlayEndReason.Tackled);
                return;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, contactRadius);
    }
}