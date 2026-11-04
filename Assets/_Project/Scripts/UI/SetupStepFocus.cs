using UnityEngine;
using UnityEngine.EventSystems;

// Reuse the existing focus path without adding navigation steps or duplicating input actions.
public class SetupStepFocus : MonoBehaviour,ISelectHandler
{
    public SetupProgress progress;public int step;
    public void OnSelect(BaseEventData data)=>progress?.SetStep(step);
}
