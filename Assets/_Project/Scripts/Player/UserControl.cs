using UnityEngine;

// Live-resolvable answer to "which Transform is the human actually piloting right now."
// Today that's always the single UserPlayer object — there's no defensive
// player-switching system yet — but this exists as its own singleton NOW rather than
// letting "is this the user's guy?" be answered ad-hoc via CompareTag("Player")
// wherever it comes up. A hardcoded tag check breaks the instant the user is piloting a
// Defender-tagged capsule instead of the Player-tagged one.
//
// Same singleton/live-resolution pattern as BallController.Instance: defaults to finding
// the "Player"-tagged object if nothing has explicitly claimed control yet.
public class UserControl : MonoBehaviour
{
    public static UserControl Instance { get; private set; }

    [SerializeField] string playerTag = "Player";
    [SerializeField] Transform controlled;

    public Transform Controlled => controlled;

    void Awake()
    {
        Instance = this;

        if (controlled == null)
        {
            var player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null) controlled = player.transform;
        }
    }

    // Called by whatever system hands control to a different entity — a future
    // defensive player-switching controller, an eventual second human player, etc.
    // Everything that cares "is this the user's guy?" reads Controlled live off this
    // singleton, so calling this is the ONLY thing a control-switching system needs to do.
    public void SetControlled(Transform newControlled) => controlled = newControlled;
}