using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The rail reflects screen/focus state only; it cannot advance or alter match options.
public class SetupProgress : MonoBehaviour
{
    RectTransform rail;TMP_Text[] labels;
    static readonly string[] Names={"TYPE","TEAMS / PLAYERS","FIELD","MODIFIER","DIFFICULTY","START"};
    public void Show(FrontEndScreen screen,FrontEndSettings settings)
    {
        int step=screen switch
        {
            FrontEndScreen.Modes=>0,
            FrontEndScreen.HumanTeam or FrontEndScreen.Opponent or FrontEndScreen.Draft or FrontEndScreen.Lineup=>1,
            FrontEndScreen.Confirmation=>2,FrontEndScreen.Review=>5,_=>-1
        };
        if(rail!=null){rail.gameObject.SetActive(false);Destroy(rail.gameObject);}
        if(step<0){labels=null;return;}
        rail=FrontEndUI.Row(transform,28);rail.name="Setup progress";
        FrontEndUI.Anchor(rail,new Vector2(.33f,.85f),new Vector2(.96f,.88f));labels=new TMP_Text[Names.Length];
        for(int i=0;i<labels.Length;i++)
        {
            labels[i]=FrontEndUI.Text(rail,Names[i],settings,19,false,28);labels[i].alignment=TextAlignmentOptions.Center;
        }
        SetStep(step);
    }
    public void SetStep(int step)
    {
        if(labels==null)return;
        for(int i=0;i<labels.Length;i++)
        {
            labels[i].text=(i<step?"+ ":i==step?"> ":"")+Names[i];
            ThemeableUI.Bind(labels[i],i<step?UIColorRole.StateSuccess:i==step?UIColorRole.SelectionHighlight:UIColorRole.TextSecondary,UITypeRole.Navigation);
        }
    }
}
