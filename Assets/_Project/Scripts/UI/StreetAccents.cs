using UnityEngine;
using UnityEngine.UI;

// Geometry placeholders for restrained paint accents; optional final masks can replace the rough edge.
public class StreetAccents : MonoBehaviour
{
    Image[] strokes;
    void Start()
    {
        strokes=new Image[4];
        for(int i=0;i<strokes.Length;i++)
        {
            var stroke=FrontEndUI.Panel(transform,"Accent slash "+i,new Vector2(.06f+i*.23f,.25f),new Vector2(.19f+i*.23f,.45f),UIColorRole.ActionConfirm);
            stroke.localRotation=Quaternion.Euler(0,0,-6);strokes[i]=stroke.GetComponent<Image>();strokes[i].raycastTarget=false;
        }
        Apply();
    }
    void OnEnable(){UITheme.Changed+=Apply;Apply();}
    void OnDisable()=>UITheme.Changed-=Apply;
    void Apply()
    {
        if(strokes==null)return;
        for(int i=0;i<strokes.Length;i++)strokes[i].gameObject.SetActive(UITheme.Current.id==UIThemeId.StreetClassic||i==0||i==3);
    }
}
