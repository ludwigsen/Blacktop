using UnityEngine;

// Polled Z-threshold check, same "no Rigidbody/trigger-callback dependency" pattern as
// TackleContact/DefenderDetector — checked every frame rather than relying on
// OnTriggerEnter. Sits on the player (or could be a generic "ball carrier" component
// once a real ball object exists) and fires once when crossing into the end zone.
public class TouchdownZone : MonoBehaviour
{
    [SerializeField] float endZoneZ = 25f; // field's far boundary — set to match your plane's actual scale

    void Update()
    {
        if (PlayState.Instance == null || !PlayState.Instance.IsLive) return;

        if (transform.position.z >= endZoneZ)
        {
            PlayState.Instance.EndPlay(PlayState.PlayEndReason.Touchdown);
        }
    }
}