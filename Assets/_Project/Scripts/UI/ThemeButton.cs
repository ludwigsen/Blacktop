using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Native Button keeps Submit/navigation; this component owns visual state and bright-surface contrast.
[RequireComponent(typeof(Button))]
public class ThemeButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    Button button;
    TMP_Text label;
    Image frame;
    bool paintedSelection;
    bool hovered,focused,pressed,lastInteractable;
    public bool persistentSelection;
    void OnEnable(){UITheme.Changed+=Apply;Apply();}
    void OnDisable(){UITheme.Changed-=Apply;hovered=focused=pressed=false;}
    void LateUpdate(){if(button!=null&&lastInteractable!=button.IsInteractable())Apply();}
    public void SetSelected(bool value){persistentSelection=value;Apply();}
    public void Initialize(Image chalkFrame, bool paintWhenSelected)
    {
        frame = chalkFrame;
        if (frame.TryGetComponent<ThemeableUI>(out var binding))
            binding.tint = false;
        paintedSelection = paintWhenSelected;
        Apply();
    }

    public void Apply()
    {
        if(button==null)button=GetComponent<Button>();
        if(label==null)label=GetComponentInChildren<TMP_Text>();
        lastInteractable=button.IsInteractable();
        bool selected=lastInteractable&&(hovered||focused||persistentSelection);
        button.transition=Selectable.Transition.None;
        button.targetGraphic.color=UITheme.Color(selected?(pressed?UIColorRole.ActionConfirm:UIColorRole.SelectionHighlight):UIColorRole.SurfacePrimary);
        if(label!=null)label.color=UITheme.Color(!lastInteractable?UIColorRole.TextSecondary:selected?UIColorRole.TextOnBright:UIColorRole.TextPrimary);
        // Main menu selection uses paint. Smaller controls keep their stable chalk silhouette.
        bool painted = selected && paintedSelection && UITheme.Library?.brushPlate != null;
        if (button.targetGraphic is Image image)
            StreetArt.Apply(image, painted ? UITheme.Library.brushPlate : UITheme.Library?.panelSurface);
        if (frame != null)
        {
            frame.enabled = !painted && frame.sprite != null;
            frame.color = UITheme.Color(selected ? UIColorRole.SelectionHighlight : UIColorRole.BorderSubtle);
        }
        // Offset shadow and the separate focus marker communicate selection without color alone.
        var scale=pressed?.985f:1f;transform.localScale=Vector3.one*scale;
    }
    public void OnSelect(BaseEventData data){focused=true;Apply();}
    public void OnDeselect(BaseEventData data){focused=false;pressed=false;Apply();}
    public void OnPointerEnter(PointerEventData data){hovered=true;Apply();}
    public void OnPointerExit(PointerEventData data){hovered=false;pressed=false;Apply();}
    public void OnPointerDown(PointerEventData data){pressed=true;Apply();}
    public void OnPointerUp(PointerEventData data){pressed=false;Apply();}
}
