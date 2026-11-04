using System;
using UnityEngine;

// Data-only presentation service: never enables, disables, or owns gameplay actors.
public static class UITheme
{
    public const string PreferenceKey="Blacktop.UITheme";
    public static event Action Changed;
    static UIThemeLibrary library;
    static BlacktopTheme current,fallback;
    public static UIThemeLibrary Library => library!=null?library:(library=Resources.Load<UIThemeLibrary>("BlacktopUI/ThemeLibrary"));
    public static BlacktopTheme Current
    {
        get {if(current==null)LoadPreference();return current;}
    }
    public static Color Color(UIColorRole role)=>Current.ColorFor(role);
    public static void LoadPreference()=>Apply((UIThemeId)PlayerPrefs.GetInt(PreferenceKey,0),false);
    public static void Select(UIThemeId id)=>Apply(id,true);
    static void Apply(UIThemeId id,bool persist)
    {
        if(id!=UIThemeId.AfterDark)id=UIThemeId.StreetClassic;
        var next=Library!=null?(id==UIThemeId.AfterDark?Library.afterDark:Library.streetClassic):null;
        if(next==null)
        {
            if(fallback==null){fallback=ScriptableObject.CreateInstance<BlacktopTheme>();fallback.hideFlags=HideFlags.HideAndDontSave;}
            fallback.SetDefaults(id);next=fallback;
        }
        current=next;
        if(persist){PlayerPrefs.SetInt(PreferenceKey,(int)id);PlayerPrefs.Save();}
        Changed?.Invoke();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Changed=null;current=null;library=null;
        if(fallback!=null)UnityEngine.Object.DestroyImmediate(fallback);
        fallback=null;
    }
    // Fixed, theme-independent rating scale. All lit segments use the current rating's tier.
    public static Color RatingColor(int rating)
    {
        string hex=rating<=4?"#E63946":rating<=8?"#FF8A00":rating<=12?"#FFD23F":rating<=16?"#3FA9F5":"#7FDBFF";
        ColorUtility.TryParseHtmlString(hex,out var color);return color;
    }
}
