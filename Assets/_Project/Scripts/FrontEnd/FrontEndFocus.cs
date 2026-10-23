using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A shape cue supplements selected color; keep focused entries visible inside scrolling lists.
public class FrontEndFocus : MonoBehaviour,ISelectHandler,IDeselectHandler
{
    public GameObject marker;
    public void OnSelect(BaseEventData data)
    {
        marker.SetActive(true);
        var scroll=GetComponentInParent<ScrollRect>();
        if(scroll==null)return;
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)transform;
        var point=scroll.viewport.InverseTransformPoint(rect.position);
        float half=scroll.viewport.rect.height*.5f;
        if(point.y>half || point.y< -half)
        {
            var position=scroll.content.anchoredPosition;
            position.y-=point.y-Mathf.Clamp(point.y,-half+rect.rect.height,half-rect.rect.height);
            scroll.content.anchoredPosition=position;
        }
    }
    public void OnDeselect(BaseEventData data) => marker.SetActive(false);
}
