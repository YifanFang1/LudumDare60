using System.Collections.Generic;
using UnityEngine;

// The crack pattern for the shatter effect, as pure geometry (no UI objects):
//
//   1. Long straight cracks ("rays") run from the impact point to the edge of the screen, at uneven angles.
//      They cut the screen into wedges.
//   2. A few cross cracks, each a straight line tilted at a random angle, cut the wedges again, so the
//      shards near the impact are small and the ones at the edges are big.
//   3. Short splinters branch off the long cracks (decoration only).
//
// Every piece is a convex polygon, so it can be drawn as a simple fan of triangles.
public class Fracture
{
    public Vector2 impact;                                              // clamped to a point on screen
    public List<Vector2[]> pieces = new List<Vector2[]>();              // the shards, in screen-centred coordinates
    public List<CrackGraphic.Segment> cracks = new List<CrackGraphic.Segment>();   // every line to draw

    const float MinArea = 900f;   // never make a shard smaller than this (square pixels)

    public static Fracture Build(Vector2 size, Vector2 impactPoint, int rayCount, float crossCracks)
    {
        Fracture f = new Fracture();
        float hw = size.x * 0.5f;
        float hh = size.y * 0.5f;
        float diag = size.magnitude;
        Vector2 p0 = new Vector2(Mathf.Clamp(impactPoint.x, -hw * 0.9f, hw * 0.9f), Mathf.Clamp(impactPoint.y, -hh * 0.9f, hh * 0.9f));
        f.impact = p0;

        int n = Mathf.Max(5, rayCount);
        float tau = Mathf.PI * 2f;

        // the long cracks: uneven angles, so the wedges differ in size
        float[] ang = new float[n];
        float start = UnityEngine.Random.value * tau;
        for (int i = 0; i < n; i++) ang[i] = start + (i + UnityEngine.Random.Range(-0.35f, 0.35f)) * (tau / n);

        Vector2[] hit = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 d = Dir(ang[i]);
            hit[i] = p0 + d * ExitDistance(p0, d, hw, hh);
        }

        Vector2[] corner = { new Vector2(-hw, -hh), new Vector2(hw, -hh), new Vector2(hw, hh), new Vector2(-hw, hh) };
        float[] cornerAng = new float[4];
        for (int k = 0; k < 4; k++) cornerAng[k] = Mathf.Atan2(corner[k].y - p0.y, corner[k].x - p0.x);

        float[] bandMin = { 0.06f, 0.18f, 0.36f };
        float[] bandMax = { 0.16f, 0.32f, 0.55f };
        float[] chance = { 0.85f, 0.55f, 0.25f };
        float[] tiltMax = { 0.5f, 0.6f, 0.7f };

        for (int i = 0; i < n; i++)
        {
            float aStart = ang[i];
            float aEnd = i + 1 < n ? ang[i + 1] : ang[0] + tau;

            // the wedge between this crack and the next: impact, edge point, any screen corners in between, edge point
            List<float> cAngles = new List<float>();
            List<Vector2> cPoints = new List<Vector2>();
            for (int k = 0; k < 4; k++)
            {
                float c = cornerAng[k];
                while (c < aStart) c += tau;
                while (c >= aStart + tau) c -= tau;
                if (c >= aEnd) continue;
                int at = 0;
                while (at < cAngles.Count && cAngles[at] < c) at++;
                cAngles.Insert(at, c);
                cPoints.Insert(at, corner[k]);
            }
            List<Vector2> wedge = new List<Vector2>();
            wedge.Add(p0);
            wedge.Add(hit[i]);
            wedge.AddRange(cPoints);
            wedge.Add(hit[(i + 1) % n]);

            List<Vector2[]> parts = new List<Vector2[]>();
            parts.Add(wedge.ToArray());

            // cross cracks: straight lines at a random tilt, at growing distances from the impact
            float mid = (aStart + aEnd) * 0.5f;
            for (int b = 0; b < 3; b++)
            {
                if (UnityEngine.Random.value > chance[b] * crossCracks) continue;
                float dist = UnityEngine.Random.Range(bandMin[b], bandMax[b]) * diag;
                float tilt = UnityEngine.Random.Range(-tiltMax[b], tiltMax[b]);
                Vector2 through = p0 + Dir(mid) * dist;
                Vector2 normal = Dir(mid + tilt);

                List<Vector2[]> next = new List<Vector2[]>();
                for (int q = 0; q < parts.Count; q++)
                {
                    Vector2[] left, right;
                    Vector2 c0, c1;
                    if (Split(parts[q], through, normal, out left, out right, out c0, out c1))
                    {
                        next.Add(left);
                        next.Add(right);
                        float d = ((c0 + c1) * 0.5f - p0).magnitude / diag;
                        f.cracks.Add(new CrackGraphic.Segment(c0, c1, d < 0.15f ? 1 : (d < 0.3f ? 2 : 3), false));
                    }
                    else
                    {
                        next.Add(parts[q]);
                    }
                }
                parts = next;
            }
            f.pieces.AddRange(parts);
        }

        // draw the long cracks in four stages, so they reach out in steps; add a couple of splinters to each
        float[] reach = { 0.08f * diag, 0.2f * diag, 0.34f * diag };
        for (int i = 0; i < n; i++)
        {
            Vector2 d = (hit[i] - p0);
            float length = d.magnitude;
            if (length < 1f) continue;
            d = d / length;

            float from = 0f;
            for (int k = 0; k < CrackGraphic.Steps; k++)
            {
                float to = k < CrackGraphic.Steps - 1 ? Mathf.Min(reach[k], length) : length;
                if (to > from + 0.5f) f.cracks.Add(new CrackGraphic.Segment(p0 + d * from, p0 + d * to, k, false));
                from = to;
            }

            for (int sp = 0; sp < 2; sp++)
            {
                float along = UnityEngine.Random.Range(0.12f, 0.5f) * length;
                float side = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(0.3f, 0.6f);
                float len = UnityEngine.Random.Range(0.04f, 0.1f) * diag;
                Vector2 a = p0 + d * along;
                Vector2 b = a + Dir(Mathf.Atan2(d.y, d.x) + side) * len;
                int stageOf = along < reach[0] ? 0 : (along < reach[1] ? 1 : (along < reach[2] ? 2 : 3));
                f.cracks.Add(new CrackGraphic.Segment(a, b, stageOf, true));
            }
        }
        return f;
    }

    static Vector2 Dir(float angle)
    {
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    // How far a ray from `o` along `d` travels before it leaves the screen rectangle.
    static float ExitDistance(Vector2 o, Vector2 d, float hw, float hh)
    {
        float t = float.MaxValue;
        if (d.x > 1e-5f) t = Mathf.Min(t, (hw - o.x) / d.x);
        else if (d.x < -1e-5f) t = Mathf.Min(t, (-hw - o.x) / d.x);
        if (d.y > 1e-5f) t = Mathf.Min(t, (hh - o.y) / d.y);
        else if (d.y < -1e-5f) t = Mathf.Min(t, (-hh - o.y) / d.y);
        return t;
    }

    // Cuts a convex polygon with the line through `p` whose normal is `n`. False if the line misses it
    // or would leave a sliver. c0 and c1 are the two points where the line enters and leaves the polygon.
    static bool Split(Vector2[] poly, Vector2 p, Vector2 n, out Vector2[] left, out Vector2[] right, out Vector2 c0, out Vector2 c1)
    {
        left = null;
        right = null;
        c0 = Vector2.zero;
        c1 = Vector2.zero;

        List<Vector2> l = new List<Vector2>();
        List<Vector2> r = new List<Vector2>();
        Vector2[] cut = new Vector2[2];
        int cuts = 0;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Length];
            float da = Dot(a - p, n);
            float db = Dot(b - p, n);
            if (da >= 0f) l.Add(a);
            if (da <= 0f) r.Add(a);
            if ((da > 0f && db < 0f) || (da < 0f && db > 0f))
            {
                Vector2 x = a + (b - a) * (da / (da - db));
                l.Add(x);
                r.Add(x);
                if (cuts < 2) cut[cuts] = x;
                cuts++;
            }
        }
        if (cuts != 2 || l.Count < 3 || r.Count < 3) return false;
        if (Area(l) < MinArea || Area(r) < MinArea) return false;

        left = l.ToArray();
        right = r.ToArray();
        c0 = cut[0];
        c1 = cut[1];
        return true;
    }

    static float Dot(Vector2 a, Vector2 b)
    {
        return a.x * b.x + a.y * b.y;
    }

    static float Area(List<Vector2> poly)
    {
        float sum = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Count];
            sum += a.x * b.y - b.x * a.y;
        }
        return Mathf.Abs(sum) * 0.5f;
    }
}
