using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// All the crack lines in one object: grows outward from the impact point as `radius` increases.
public class CrackGraphic : MaskableGraphic
{
    public const int Steps = 4;   // the cracks appear in this many hard frames

    [System.Serializable]
    public struct Segment
    {
        public Vector2 a, b;
        public int step;         // the frame this line first appears in (0 = the very first)
        public bool thin;        // a small splinter rather than a main crack
        public Segment(Vector2 a, Vector2 b, int step, bool thin)
        {
            this.a = a; this.b = b; this.step = step; this.thin = thin;
        }
    }

    public List<Segment> segments = new List<Segment>();
    public int step = -1;        // lines with step <= this are drawn
    public Color fill = Color.white;
    public Color outline = Color.black;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        // dark lines first, bright lines on top, so junctions stay clean
        for (int pass = 0; pass < 2; pass++)
        {
            Color c = pass == 0 ? outline : fill;
            for (int i = 0; i < segments.Count; i++)
            {
                Segment s = segments[i];
                if (s.step > step) continue;
                float width = s.thin ? (pass == 0 ? 6f : 2f) : (pass == 0 ? 10f : 4f);
                AddLine(vh, s.a, s.b, width, c);
            }
        }
    }

    static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
    {
        Vector2 d = b - a;
        if (d.sqrMagnitude < 0.01f) return;
        Vector2 n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
        int at = vh.currentVertCount;
        vh.AddVert(new Vector3(a.x - n.x, a.y - n.y, 0f), c, Vector2.zero);
        vh.AddVert(new Vector3(a.x + n.x, a.y + n.y, 0f), c, Vector2.zero);
        vh.AddVert(new Vector3(b.x + n.x, b.y + n.y, 0f), c, Vector2.zero);
        vh.AddVert(new Vector3(b.x - n.x, b.y - n.y, 0f), c, Vector2.zero);
        vh.AddTriangle(at, at + 1, at + 2);
        vh.AddTriangle(at, at + 2, at + 3);
    }
}
