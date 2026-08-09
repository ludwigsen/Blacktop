using System.Collections.Generic;
using UnityEngine;

// Forward detection zone, polled every frame via OverlapBox rather than relying on
// OnTriggerEnter/Exit — trigger callbacks require a Rigidbody on at least one object,
// and this project deliberately has none (transform-based movement throughout, matching
// StiffArm's existing OverlapSphere pattern rather than adding Rigidbodies just to
// satisfy Unity's event requirements).
public class DefenderDetector : MonoBehaviour
{
    [SerializeField] string defenderTag = "Defender";
    [SerializeField] Vector3 boxSize = new Vector3(2f, 1.5f, 2f); // forward-facing zone for Hurdle — hurdling is inherently a forward move, doesn't need to detect from behind
    [SerializeField] Vector3 boxOffset = new Vector3(0f, 0f, 1.2f); // pushed forward from player center

    public bool DefenderInRange { get; private set; }
    public int DefenderCount { get; private set; }

    void Update()
    {
        Vector3 center = transform.position + transform.TransformDirection(boxOffset);
        Collider[] hits = Physics.OverlapBox(center, boxSize * 0.5f, transform.rotation);

        int count = 0;
        foreach (var hit in hits)
        {
            if (hit.CompareTag(defenderTag)) count++;
        }

        DefenderCount = count;
        DefenderInRange = count > 0;
    }

    // Visualize the detection box in Scene view even without a real collider component —
    // makes tuning boxSize/boxOffset visually verifiable instead of guessing blind.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = transform.position + transform.TransformDirection(boxOffset);
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
    }
}