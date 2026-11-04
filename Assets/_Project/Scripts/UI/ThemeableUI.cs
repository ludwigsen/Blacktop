using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small explicit bindings avoid hierarchy scans and leave team/attribute graphics untouched.
[DisallowMultipleComponent]
public class ThemeableUI : MonoBehaviour
{
    public UIColorRole role=UIColorRole.TextPrimary;
    public UITypeRole typography=UITypeRole.Body;
    public bool tint=true, setFont;
    [Range(0,1)] public float opacity=1;
    public bool textureMask;
    Graphic target;
    void OnEnable(){UITheme.Changed+=Apply;Apply();}
    void OnDisable()=>UITheme.Changed-=Apply;
    public void Apply()
    {
        if(target==null)target=GetComponent<Graphic>();
        if(target==null)return;
        if(tint)
        {
            var color=UITheme.Color(role);color.a=opacity*(textureMask?UITheme.Current.textureStrength:1);
            target.color=color;
        }
        if(setFont&&target is TMP_Text text)
        {
            var font=UITheme.Library?.FontFor(typography);
            if(font!=null){text.font=font;text.fontStyle=FontStyles.Normal;}
        }
    }
    public static ThemeableUI Bind(Graphic graphic,UIColorRole role,UITypeRole? type=null,float opacity=1)
    {
        var binding=graphic.GetComponent<ThemeableUI>()??graphic.gameObject.AddComponent<ThemeableUI>();
        binding.role=role;binding.opacity=opacity;binding.setFont=type.HasValue;
        if(type.HasValue)binding.typography=type.Value;
        binding.Apply();return binding;
    }
}
