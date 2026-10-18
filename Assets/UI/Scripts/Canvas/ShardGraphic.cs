using UnityEngine;
using UnityEngine.UI;

// One piece of the broken screen: a polygon textured with the matching part of the captured picture.
public class ShardGraphic : MaskableGraphic
{
    public Vector2[] points;      // corners, relative to this object's centre
    public Vector2 areaSize;      // size of the whole screen area
    public Vector2 areaOffset;    // this object's centre inside that area (origin = middle of the screen)
    public Texture texture;

    public override Texture mainTexture
    {
        get { return texture != null ? texture : Texture2D.whiteTexture; }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points == null || points.Length < 3) return;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 inArea = points[i] + areaOffset;
            Vector2 uv = new Vector2(inArea.x / areaSize.x + 0.5f, inArea.y / areaSize.y + 0.5f);
            vh.AddVert(new Vector3(points[i].x, points[i].y, 0f), color, uv);
        }
        for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(0, i, i + 1);
    }
}
