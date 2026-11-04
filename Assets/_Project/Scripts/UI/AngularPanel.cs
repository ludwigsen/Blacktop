using UnityEngine;
using UnityEngine.UI;

// Scalable clipped corners need no raster atlas and leave child text unskewed.
public class AngularPanel : Image
{
    [Min(0)] public float corner=12;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        if(sprite!=null){base.OnPopulateMesh(vh);return;}
        vh.Clear();var r=GetPixelAdjustedRect();float c=Mathf.Min(corner,Mathf.Min(r.width,r.height)*.2f);
        Vector2[] points={new(r.xMin+c,r.yMin),new(r.xMax,r.yMin),new(r.xMax,r.yMax-c),new(r.xMax-c,r.yMax),new(r.xMin,r.yMax),new(r.xMin,r.yMin+c)};
        vh.AddVert(r.center,color,Vector2.zero);
        foreach(var point in points)vh.AddVert(point,color,Vector2.zero);
        for(int i=0;i<points.Length;i++)vh.AddTriangle(0,i+1,(i+1)%points.Length+1);
    }
}
