using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws coloured outlines around a sprite's shape (Resources/SpriteOutline.shader). The outline
/// follows the current animation frame, so it always matches the shape you see. View only.
/// <para>
/// Fighters get this automatically (WorldView adds it): Player colours on the fighter you control,
/// Enemy colours on the other one. To preview in the editor, add it to any GameObject with a
/// SpriteRenderer, or use the Outline option on CharacterPreview.
/// </para>
/// <para>
/// Style picks the colours: Player / Enemy use the shared OutlineProfile (menu Fight > Outline
/// Colors), Custom uses the layers set on this component.
/// </para>
/// The sprite must be imported with Mesh Type: Full Rect, otherwise the outline gets cut off.
/// Turning this component off gives the renderer its old material back.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutline : MonoBehaviour
{
    public const int MaxLayers = 8;   // must match MAX_LAYERS in SpriteOutline.shader

    public enum Style { Player, Enemy, Custom }

    [Tooltip("Player / Enemy: the colours in the OutlineProfile (Fight > Outline Colors). Custom: the layers below.")]
    public Style style = Style.Player;
    [Tooltip("Only used when Style is Custom. First = closest to the sprite.")]
    public OutlineLayer[] customLayers = { new OutlineLayer(Color.white, 0.04f) };

    [SerializeField, HideInInspector] Material originalMaterial;   // put back when this is turned off

    SpriteRenderer sr;
    MaterialPropertyBlock block;

    // Where the current sprite sits, measured once per sprite (frame changes are new sprites).
    Sprite measured;
    Vector4 localRect;
    Vector4 uvRect;
    bool tight;        // Mesh Type: Tight, see Measure

    static readonly float[] radii = new float[MaxLayers];
    static readonly Vector4[] colors = new Vector4[MaxLayers];
    static readonly HashSet<Sprite> warned = new HashSet<Sprite>();

    static readonly int LocalRectId = Shader.PropertyToID("_SpriteLocalRect");
    static readonly int UVRectId = Shader.PropertyToID("_SpriteUVRect");
    static readonly int WorldToUVId = Shader.PropertyToID("_WorldToUV");
    static readonly int TexSizeId = Shader.PropertyToID("_TexSize");
    static readonly int PadId = Shader.PropertyToID("_PadLocal");
    static readonly int CountId = Shader.PropertyToID("_OutlineCount");
    static readonly int RadiiId = Shader.PropertyToID("_OutlineRadii");
    static readonly int ColorsId = Shader.PropertyToID("_OutlineColors");

    void OnEnable()
    {
        sr = GetComponent<SpriteRenderer>();
        Material outline = OutlineProfile.Material;
        if (outline != null && sr.sharedMaterial != outline)
        {
            originalMaterial = sr.sharedMaterial;
            sr.sharedMaterial = outline;
        }
        OutlineProfile.Changed += Apply;
        Apply();
    }

    void OnDisable()
    {
        OutlineProfile.Changed -= Apply;
        if (sr == null) return;
        sr.SetPropertyBlock(null);
        if (sr.sharedMaterial == OutlineProfile.Material && originalMaterial != null) sr.sharedMaterial = originalMaterial;
    }

    void OnValidate()
    {
        if (isActiveAndEnabled) Apply();
    }

    // Every frame, so a new animation frame, a flip or a scale change is picked up right away.
    void LateUpdate()
    {
        Apply();
    }

    /// <summary>Sends the current sprite and outline settings to the shader.</summary>
    public void Apply()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;
        Sprite sprite = sr.sprite;
        if (sprite != measured) Measure(sprite);

        // The layers' outer edges, added up from the inside out.
        OutlineLayer[] layers = Layers();
        int count = 0;
        float total = 0f;
        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        for (int i = 0; layers != null && i < layers.Length && count < MaxLayers; i++)
        {
            if (layers[i] == null) continue;
            total += Mathf.Max(0f, layers[i].width);
            radii[count] = total;
            Color c = linear ? layers[i].color.linear : layers[i].color;   // SetVectorArray doesn't convert colours like SetColor does
            colors[count] = new Vector4(c.r, c.g, c.b, layers[i].color.a);
            count++;
        }

        // Widths are in world units, so turn them into UV and object-space distances with this
        // object's scale (fighters are scaled by spriteScale and flipped with a negative x).
        Vector3 scale = transform.lossyScale;
        float sx = Mathf.Max(Mathf.Abs(scale.x), 0.0001f);
        float sy = Mathf.Max(Mathf.Abs(scale.y), 0.0001f);
        float localW = Mathf.Max(localRect.z - localRect.x, 0.0001f);
        float localH = Mathf.Max(localRect.w - localRect.y, 0.0001f);
        float uvW = uvRect.z - uvRect.x;
        float uvH = uvRect.w - uvRect.y;
        Texture2D tex = sprite.texture;
        float texW = tex != null ? tex.width : 1f;
        float texH = tex != null ? tex.height : 1f;

        // Grow the quad by the outline plus 2 texels, so the soft outer edge isn't clipped. Only a
        // Full Rect quad can be grown safely: the shader pushes each corner away from the centre,
        // and on a Tight mesh (shaped like the art) that pushes some edges INTO the art and cuts
        // it off. A Tight sprite keeps its mesh, so its outline is clipped instead (see the warning).
        float marginX = 2f * localW / Mathf.Max(uvW * texW, 1f);
        float marginY = 2f * localH / Mathf.Max(uvH * texH, 1f);
        Vector4 pad = count > 0 && !tight ? new Vector4(total / sx + marginX, total / sy + marginY, 0f, 0f) : Vector4.zero;

        if (block == null) block = new MaterialPropertyBlock();
        sr.GetPropertyBlock(block);
        block.SetVector(LocalRectId, localRect);
        block.SetVector(UVRectId, uvRect);
        block.SetVector(WorldToUVId, new Vector4(uvW / localW / sx, uvH / localH / sy, 0f, 0f));
        block.SetVector(TexSizeId, new Vector4(texW, texH, 0f, 0f));
        block.SetVector(PadId, pad);
        block.SetFloat(CountId, count);
        block.SetFloatArray(RadiiId, radii);     // always the full MaxLayers length: Unity fixes an array's size the first time it is set
        block.SetVectorArray(ColorsId, colors);
        sr.SetPropertyBlock(block);
    }

    OutlineLayer[] Layers()
    {
        switch (style)
        {
            case Style.Player: return OutlineProfile.Current.player;
            case Style.Enemy: return OutlineProfile.Current.enemy;
            default: return customLayers;
        }
    }

    // The sprite's corners in object space and in its texture (its frame in a sprite sheet).
    void Measure(Sprite sprite)
    {
        measured = sprite;
        Vector2[] verts = sprite.vertices;
        Vector2[] uvs = sprite.uv;
        Vector2 vMin = verts[0], vMax = verts[0], uMin = uvs[0], uMax = uvs[0];
        for (int i = 1; i < verts.Length; i++)
        {
            vMin = Vector2.Min(vMin, verts[i]);
            vMax = Vector2.Max(vMax, verts[i]);
            uMin = Vector2.Min(uMin, uvs[i]);
            uMax = Vector2.Max(uMax, uvs[i]);
        }
        localRect = new Vector4(vMin.x, vMin.y, vMax.x, vMax.y);
        uvRect = new Vector4(uMin.x, uMin.y, uMax.x, uMax.y);

        // A Full Rect sprite is 4 corners. More means Mesh Type: Tight, which clips the outline.
        tight = verts.Length > 4;
        if (tight && warned.Add(sprite))
        {
            Debug.LogWarning("Sprite '" + sprite.name + "' uses Mesh Type: Tight, so its outline is cut off where the art meets its mesh. Select the image, set Mesh Type to Full Rect in the Inspector and press Apply.", sprite);
        }
    }
}
