using UnityEngine;
using UnityEngine.UI;

// Rating ownership stays with the roster/session. This view only paints exactly twenty cells.
public class AttributeSegments : MonoBehaviour
{
    Image[] segments;int value;
    public int Value=>value;
    public void Initialize(Image[] cells,int rating){segments=cells;SetValue(rating);}
    public void SetValue(int rating){value=Mathf.Clamp(rating,0,20);Paint();}
    void OnEnable(){UITheme.Changed+=Paint;Paint();}
    void OnDisable()=>UITheme.Changed-=Paint;
    void Paint()
    {
        if(segments==null)return;
        for(int i=0;i<segments.Length;i++)segments[i].color=i<value?UITheme.RatingColor(value):UITheme.Color(UIColorRole.BackgroundSecondary);
    }
}
