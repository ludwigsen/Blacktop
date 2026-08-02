using UnityEngine;

// Simple fixed-angle follow camera, mimicking NFL Street's high 3/4 broadcast-style
// angle. Deliberately NOT parented/rotated to the player — camera stays field-oriented
// regardless of which way the player is facing, matching the original's fixed perspective.
// Cinemachine will replace this later for smoothing/zoom/gamebreaker pull-back; this is
// just enough to unblock playtesting movement and moves.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target; // the player capsule
    [SerializeField] Vector3 offset = new Vector3(0f, 9f, -7f); // height, pulled back — tune to taste
    [SerializeField] float followSpeed = 8f; // smoothing — higher = snappier follow, lower = more lag/drift

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1f); // look slightly above player base, not at their feet
    }
}