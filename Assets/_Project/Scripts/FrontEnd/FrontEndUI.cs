using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Small shared uGUI construction helpers; data and navigation live outside presentation.
public static class FrontEndUI
{
    public static readonly Color Ink=new Color(.055f,.065f,.075f), Paper=new Color(.94f,.92f,.86f), Accent=new Color(1f,.37f,.10f);
    public static Canvas CreateCanvas(string name,int order=0)
    {
        var go=new GameObject(name,typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=order;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        return canvas;
    }
    public static RawImage PreviewSurface(Transform parent, Vector2 min, Vector2 max)
    {
        var frame=Panel(parent,"Portrait frame",min,max,Ink);
        var go=new GameObject("Live character",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(frame,false);
        Anchor(go.GetComponent<RectTransform>(),Vector2.zero,Vector2.one);
        var aspect=go.AddComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=1f;
        return go.GetComponent<RawImage>();
    }
    public static void EnsureInput()
    {
        if(EventSystem.current!=null)return;
        var go=new GameObject("Front End EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
    public static void Anchor(RectTransform rect,Vector2 min,Vector2 max)
    {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;}
    public static RectTransform Panel(Transform parent,string name,Vector2 min,Vector2 max,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();Anchor(rect,min,max);go.GetComponent<Image>().color=color;
        return rect;
    }
    public static RectTransform Vertical(Transform parent,string name="List",int padding=16)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(VerticalLayoutGroup));go.transform.SetParent(parent,false);
        var layout=go.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(padding,padding,padding,padding);
        layout.spacing=10;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;
        layout.childForceExpandWidth=true;return go.GetComponent<RectTransform>();
    }
    public static RectTransform Row(Transform parent,float height=64)
    {
        var go=new GameObject("Row",typeof(RectTransform),typeof(HorizontalLayoutGroup),typeof(LayoutElement));go.transform.SetParent(parent,false);
        var layout=go.GetComponent<HorizontalLayoutGroup>();layout.spacing=10;layout.childControlWidth=true;layout.childControlHeight=true;
        layout.childForceExpandWidth=true;layout.childForceExpandHeight=true;go.GetComponent<LayoutElement>().preferredHeight=height;
        return go.GetComponent<RectTransform>();
    }
    public static TextMeshProUGUI Text(Transform parent,string value,FrontEndSettings settings,int size=26,bool display=false,float height=44)
    {
        var go=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI),typeof(LayoutElement));go.transform.SetParent(parent,false);
        var text=go.GetComponent<TextMeshProUGUI>();text.text=value;text.fontSize=size;text.color=Paper;
        text.font=settings?.theme!=null?(display?settings.theme.displayFont:settings.theme.legibleFont):TMP_Settings.defaultFontAsset;
        text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Ellipsis;
        go.GetComponent<LayoutElement>().preferredHeight=height;
        go.GetComponent<LayoutElement>().minWidth=0;go.GetComponent<LayoutElement>().preferredWidth=0;
        if(display){text.fontStyle=FontStyles.Bold;var outline=go.AddComponent<Outline>();outline.effectColor=Color.black;outline.effectDistance=new Vector2(2,-2);}
        return text;
    }
    public static Button Button(Transform parent,string label,Action click,FrontEndSettings settings,float height=64)
    {
        var go=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));go.transform.SetParent(parent,false);
        go.GetComponent<Image>().color=new Color(.13f,.15f,.17f);
        var button=go.GetComponent<Button>();button.targetGraphic=go.GetComponent<Image>();
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.4f,1.4f,1.4f);
        colors.selectedColor=Accent;colors.pressedColor=new Color(.7f,.7f,.7f);colors.disabledColor=new Color(.4f,.4f,.4f);
        button.colors=colors;button.onClick.AddListener(()=>click());
        go.GetComponent<LayoutElement>().preferredHeight=height;
        var outline=go.AddComponent<Outline>();outline.effectColor=new Color(.3f,.31f,.32f);outline.effectDistance=new Vector2(2,2);
        var text=Text(go.transform,label,settings,26,false,height);Anchor(text.rectTransform,new Vector2(.06f,.03f),new Vector2(.94f,.97f));
        text.alignment=TextAlignmentOptions.MidlineLeft;
        var cue=Panel(go.transform,"Focus marker",new Vector2(0,0),new Vector2(.018f,1),Paper);
        cue.gameObject.SetActive(false);go.AddComponent<FrontEndFocus>().marker=cue.gameObject;
        return button;
    }
    public static RectTransform Scroll(Transform parent,string name="Scroll")
    {
        var root=Panel(parent,name,Vector2.zero,Vector2.one,new Color(0,0,0,.12f));
        var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=35f;
        var viewport=Panel(root,"Viewport",Vector2.zero,Vector2.one,Color.white);viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;
        var content=Vertical(viewport,"Content");Anchor(content,new Vector2(0,1),new Vector2(1,1));content.pivot=new Vector2(.5f,1);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport;scroll.content=content;return content;
    }
    public static Action<int> Picker(Transform parent,string label,IList<string> options,int selected,Action<int> changed,FrontEndSettings settings)
    {
        var row=Row(parent,62);row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth=false;
        Text(row,label,settings,23,false,62).GetComponent<LayoutElement>().flexibleWidth=1.2f;
        int index=selected;TextMeshProUGUI value=null;
        var previous=Button(row,"‹",()=>Cycle(-1),settings);previous.GetComponent<LayoutElement>().preferredWidth=54;
        previous.GetComponent<LayoutElement>().flexibleWidth=0;
        value=Text(row,index>=0&&index<options.Count?options[index]:"UNAVAILABLE",settings,23,false,62);value.alignment=TextAlignmentOptions.Midline;
        value.GetComponent<LayoutElement>().flexibleWidth=2.3f;
        var next=Button(row,"›",()=>Cycle(1),settings);next.GetComponent<LayoutElement>().preferredWidth=54;next.GetComponent<LayoutElement>().flexibleWidth=0;
        previous.interactable=next.interactable=options.Count>0;
        return nextIndex=>{index=nextIndex;value.text=index>=0&&index<options.Count?options[index]:"UNAVAILABLE";};
        void Cycle(int direction)
        {if(options.Count==0)return;index=(Mathf.Max(index,0)+direction+options.Count)%options.Count;value.text=options[index];changed(index);}
    }
    public static TMP_InputField Input(Transform parent,string label,string value,Action<string> changed,FrontEndSettings settings,bool numeric=false)
    {
        var row=Row(parent,66);Text(row,label,settings,24,false,66).GetComponent<LayoutElement>().flexibleWidth=1f;
        var root=Panel(row,label,new Vector2(0,0),new Vector2(1,1),new Color(.16f,.18f,.20f));
        var layout=root.gameObject.AddComponent<LayoutElement>();layout.flexibleWidth=2;
        var viewport=Panel(root,"Text area",new Vector2(.04f,.05f),new Vector2(.96f,.95f),Color.clear);
        var text=Text(viewport,"",settings,25,false,66);Anchor(text.rectTransform,Vector2.zero,Vector2.one);text.alignment=TextAlignmentOptions.MidlineLeft;
        var input=root.gameObject.AddComponent<TMP_InputField>();input.textViewport=viewport;input.textComponent=text;input.targetGraphic=root.GetComponent<Image>();
        input.contentType=numeric?TMP_InputField.ContentType.IntegerNumber:TMP_InputField.ContentType.Standard;
        input.text=value;input.onValueChanged.AddListener(v=>changed(v));return input;
    }
    public static void Rating(Transform parent,string label,int value,Action<int> changed,FrontEndSettings settings)
    {
        var row=Row(parent,48);Text(row,label,settings,23,false,48).GetComponent<LayoutElement>().flexibleWidth=1.3f;
        var track=Panel(row,"Rating",Vector2.zero,Vector2.one,new Color(.2f,.22f,.24f));track.gameObject.AddComponent<LayoutElement>().flexibleWidth=2;
        var fill=Panel(track,"Fill",new Vector2(0,.28f),new Vector2(1,.72f),Accent);
        var handle=Panel(track,"Handle",new Vector2(0,0),new Vector2(0,1),Paper);handle.sizeDelta=new Vector2(20,0);
        var slider=track.gameObject.AddComponent<Slider>();slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();
        slider.minValue=0;slider.maxValue=20;slider.wholeNumbers=true;slider.value=value;
        var number=Text(row,value.ToString(),settings,25,false,48);number.GetComponent<LayoutElement>().preferredWidth=42;
        slider.onValueChanged.AddListener(v=>{number.text=((int)v).ToString();changed((int)v);});
    }
    public static void FocusFirst(Transform root)
    {
        var selectable=root.GetComponentsInChildren<Selectable>().FirstOrDefaultSafe();
        if(selectable!=null && EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }
    static Selectable FirstOrDefaultSafe(this Selectable[] controls)
    {foreach(var control in controls)if(control.IsActive()&&control.IsInteractable())return control;return null;}
}
