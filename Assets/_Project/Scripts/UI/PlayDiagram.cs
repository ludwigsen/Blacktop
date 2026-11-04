using UnityEngine;
using UnityEngine.UI;

// Clean schematic from authored formation slots/routes/jobs. It cannot write play assignments.
[RequireComponent(typeof(CanvasRenderer))]
public class PlayDiagram : MaskableGraphic
{
    PlayCallData offense;DefensivePlayData defense;
    public void Show(PlayCallData play){offense=play;defense=null;SetVerticesDirty();}
    public void Show(DefensivePlayData play){defense=play;offense=null;SetVerticesDirty();}
    void OnThemeChanged()=>SetVerticesDirty();
    protected override void OnEnable(){base.OnEnable();UITheme.Changed+=OnThemeChanged;}
    protected override void OnDisable(){UITheme.Changed-=OnThemeChanged;base.OnDisable();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();if(offense==null&&defense==null)return;
        var formation=offense!=null?offense.OffensiveFormation: defense.Formation;
        if(formation==null)formation=offense!=null?UITheme.Library?.diagramOffense:UITheme.Library?.diagramDefense;
        if(formation==null)return; // No invented formations when the authoring asset is missing.
        var r=GetPixelAdjustedRect();float spread=1;
        if(offense!=null)foreach(var slot in formation.offensiveSlots)spread=Mathf.Max(spread,Mathf.Abs(slot.offsetFromLOS.x));
        else foreach(var slot in formation.defenderSlots)spread=Mathf.Max(spread,Mathf.Abs(slot.offsetFromLOS.x));
        Color routeColor=UITheme.Color(UIColorRole.SelectionHighlight),dot=UITheme.Color(UIColorRole.TextPrimary);
        Line(new Vector2(r.xMin+6,r.yMin+r.height*.33f),new Vector2(r.xMax-6,r.yMin+r.height*.33f),UITheme.Color(UIColorRole.BorderSubtle),1);
        if(offense!=null)
        {
            Dot(Point(formation.qbOffsetFromLOS),dot);
            for(int i=0;i<formation.offensiveSlots.Count;i++)
            {
                var slot=formation.offensiveSlots[i];var start=Point(slot.offsetFromLOS);Dot(start,dot);
                if(offense.PlayType==PlayType.Run)
                {
                    if(slot.label==offense.HandoffReceiverSlot.ToString())Arrow(Point(formation.qbOffsetFromLOS),start,routeColor);
                    continue;
                }
                var route=offense.GetRouteForReceiver(i);if(route==RoutePattern.None)continue;
                Vector2 end=start+new Vector2(0,r.height*.48f);
                switch(route)
                {
                    case RoutePattern.Slant:end=start+new Vector2(r.width*.14f,r.height*.43f);break;
                    case RoutePattern.Hitch:end=start+new Vector2(0,r.height*.25f);break;
                    case RoutePattern.Wheel:
                        var bend=start+new Vector2(r.width*.08f,r.height*.12f);
                        Line(start,bend,routeColor,2);start=bend;end=bend+new Vector2(0,r.height*.35f);break;
                    case RoutePattern.Comeback:
                        var apex=start+new Vector2(0,r.height*.45f);Line(start,apex,routeColor,2);
                        start=apex;end=apex+new Vector2(r.width*.05f,-r.height*.12f);break;
                }
                Arrow(start,end,routeColor);
            }
        }
        else for(int i=0;i<formation.defenderSlots.Count;i++)
        {
            var start=Point(formation.defenderSlots[i].offsetFromLOS);Dot(start,dot);
            var job=defense.GetAssignment(i);
            if(job.job==DefenderJob.Zone)
            {
                var end=Point(job.zoneOffsetFromLOS);Line(start,end,routeColor,2);
                Line(end-new Vector2(9,0),end+new Vector2(9,0),routeColor,2);
            }
            else if(job.job==DefenderJob.Rush)Arrow(start,start+new Vector2(0,-r.height*.23f),routeColor);
            else if(job.job==DefenderJob.Man)Line(start,start+new Vector2(0,r.height*.13f),dot,2);
        }
        Vector2 Point(Vector3 value)=>new Vector2(r.center.x+value.x/spread*r.width*.35f,r.yMin+r.height*(.33f+Mathf.Clamp(value.z/spread*.35f,-.28f,.48f)));
        Vector2 Clamp(Vector2 point)=>new Vector2(Mathf.Clamp(point.x,r.xMin+5,r.xMax-5),Mathf.Clamp(point.y,r.yMin+5,r.yMax-5));
        void Line(Vector2 a,Vector2 b,Color tint,float width)
        {
            a=Clamp(a);b=Clamp(b);var normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            int offset=vh.currentVertCount;vh.AddVert(a-normal,tint,Vector2.zero);vh.AddVert(a+normal,tint,Vector2.zero);
            vh.AddVert(b+normal,tint,Vector2.zero);vh.AddVert(b-normal,tint,Vector2.zero);
            vh.AddTriangle(offset,offset+1,offset+2);vh.AddTriangle(offset,offset+2,offset+3);
        }
        void Arrow(Vector2 a,Vector2 b,Color tint)
        {
            b=Clamp(b);Line(a,b,tint,2);var delta=(b-a).normalized;var side=new Vector2(-delta.y,delta.x);
            Line(b,b-delta*6+side*4,tint,2);Line(b,b-delta*6-side*4,tint,2);
        }
        void Dot(Vector2 point,Color tint){Line(point-new Vector2(2.5f,0),point+new Vector2(2.5f,0),tint,5);}
    }
}
