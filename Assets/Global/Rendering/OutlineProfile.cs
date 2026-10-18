using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// The outline colours and widths for fighters: one list for the PLAYER (the fighter this machine
/// controls) and one for the ENEMY. Online, each player sees themselves in the Player colours.
/// <para>
/// Edit it: menu Fight > Outline Colors (Player and Enemy), or select
/// Assets/Global/Rendering/Resources/OutlineProfile. Changes show right away, in the Scene view and
/// while playing, and are kept after you stop Play mode (it is an asset, not part of the scene).
/// </para>
/// Each list is drawn from the inside out: the first layer hugs the sprite, the next goes around
/// that one, and so on (up to SpriteOutline.MaxLayers). An empty list means no outline.
/// It must be named "OutlineProfile" and sit in a Resources folder so the game can find it.
/// </summary>
[CreateAssetMenu(menuName = "Fight/Outline Profile", fileName = "OutlineProfile")]
public class OutlineProfile : ScriptableObject
{
    [Tooltip("Outlines around the fighter you control. First = closest to the sprite.")]
    public OutlineLayer[] player = { new OutlineLayer(new Color(0.2f, 0.55f, 1f), 0.04f) };
    [Tooltip("Outlines around the opponent (the AI, or the other player online). First = closest to the sprite.")]
    public OutlineLayer[] enemy = { new OutlineLayer(new Color(1f, 0.25f, 0.25f), 0.04f) };

    /// <summary>Raised when any profile is edited in the inspector, so outlines redraw.</summary>
    public static event System.Action Changed;

    static OutlineProfile current;
    static Material material;

    /// <summary>The profile in Resources, or the default colours if there is none yet.</summary>
    public static OutlineProfile Current
    {
        get
        {
            if (current == null) current = Resources.Load<OutlineProfile>("OutlineProfile");
            if (current == null)
            {
                current = CreateInstance<OutlineProfile>();
                current.hideFlags = HideFlags.DontSave;
            }
            return current;
        }
    }

    /// <summary>The shared outline material (Resources/SpriteOutline.mat), made from the shader if it is missing.</summary>
    public static Material Material
    {
        get
        {
            if (material == null) material = Resources.Load<Material>("SpriteOutline");
            if (material == null)
            {
                Shader shader = Resources.Load<Shader>("SpriteOutline");
                if (shader == null) return null;
                material = new Material(shader);
                material.hideFlags = HideFlags.DontSave;
            }
            return material;
        }
    }

    /// <summary>Look the profile and material up again (the editor calls this after creating them).</summary>
    public static void Forget()
    {
        current = null;
        material = null;
        RaiseChanged();
    }

    void OnValidate()
    {
        RaiseChanged();
    }

    static void RaiseChanged()
    {
        if (Changed != null) Changed();
#if UNITY_EDITOR
        SceneView.RepaintAll();   // outside Play mode the Scene view doesn't redraw by itself
#endif
    }
}

/// <summary>One ring of outline: its colour (alpha = opacity) and how thick it is.</summary>
[System.Serializable]
public class OutlineLayer
{
    public Color color = Color.white;
    [Tooltip("Thickness in Unity units (a fighter is about 2 units tall). It looks the same on every character, whatever the art's resolution.")]
    [Range(0f, 0.5f)] public float width = 0.04f;

    public OutlineLayer() { }

    public OutlineLayer(Color color, float width)
    {
        this.color = color;
        this.width = width;
    }
}
