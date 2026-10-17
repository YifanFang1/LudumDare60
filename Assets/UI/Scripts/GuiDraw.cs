using UnityEngine;

// Tiny drawing helpers shared by the OnGUI-based views.
public static class GuiDraw
{
    public const float PxPerMilli = 0.04f;     // sim milli-units -> virtual pixels

    // The views draw on a virtual 540-pixel-tall screen that scales to the real window.
    public static void BeginVirtualScreen(out float width, out float height)
    {
        float s = Screen.height / 540f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        width = Screen.width / s;
        height = 540f;
    }

    public static void Fill(float x, float y, float w, float h, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
