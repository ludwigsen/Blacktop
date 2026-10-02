using UnityEngine;

// Rotates to face the main camera every frame — keeps a world-space nameplate readable
// regardless of CameraFollow's turnover-swing orbit yaw.
public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.rotation = cam.transform.rotation;
    }
}