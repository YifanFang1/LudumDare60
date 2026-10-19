using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The big character display on the select screen: coloured backing, the character, their name, three tilted
// word banners, four skill blocks and the More / Back button. Everything is placed in 1920x1080 pixels from the
// top-left of the stage. In "details" mode the whole group slides left to make room for the text page.
public class CharacterProfilePanel : MonoBehaviour
{
    public RectTransform group;      // moved sideways in details mode
    public Image backing;
    public Image sprite;
    public Image nameArt;
    public Image[] traits;           // 3
    public Image[] minis;            // 4
    public Button moreButton;
    public Image moreImage;
    public TMP_Text fallbackName;    // shown instead of the name picture for characters with no art yet

    public const float DetailsShift = -910f;
    const float SpriteCenterX = 1335f;   // where the opaque part of the picture is centred (list mode)
    static readonly Vector2[] AutoTraits = { new Vector2(1393f, 143f), new Vector2(1502f, 270f), new Vector2(1495f, 505f) };

    int shownId = int.MinValue;
    bool shownDetails;

    // True if this character has the full art (and so a More page).
    public static bool HasArt(int id)
    {
        CharacterSelectArt art = CharacterSelectArt.Load();
        CharacterSelectArt.Entry e = art != null ? art.Find(id) : null;
        return e != null && e.sprite != null;
    }

    public void Show(int id, bool details)
    {
        if (id == shownId && details == shownDetails) return;
        shownId = id;
        shownDetails = details;
        if (group != null) group.anchoredPosition = new Vector2(details ? DetailsShift : 0f, 0f);

        CharacterSelectArt art = CharacterSelectArt.Load();
        CharacterSelectArt.Entry e = art != null ? art.Find(id) : null;
        if (e == null || e.sprite == null)
        {
            ShowFallback(id);
            return;
        }

        Put(backing, e.backing, 1260f, 80f, 500f, 1000f);
        backing.color = Color.white;

        // the character: native size (shrunk only if taller than the panel), bottom on the screen edge
        float h = e.sprite.rect.height;
        float s = e.spriteScale > 0f ? e.spriteScale : (h > 1000f ? 1000f / h : 1f);
        Rect b = e.spriteBounds.width > 0f ? e.spriteBounds : new Rect(0f, 0f, e.sprite.rect.width, h);
        // the opaque part's bottom edge sits on the bottom of the screen (pictures with empty space under the feet too),
        // then spriteOffset nudges characters whose face would be hidden by the name or banners
        float spriteX = SpriteCenterX + e.spriteOffset.x - (b.x + b.width * 0.5f) * s;
        float spriteY = 1080f + e.spriteOffset.y - (b.y + b.height) * s;
        Put(sprite, e.sprite, spriteX, spriteY, e.sprite.rect.width * s, h * s);
        sprite.preserveAspect = false;

        if (e.nameArt != null)
        {
            // the bottom-right corner of the picture's visible part sits on nameCorner (pictures with empty space around the lettering line up too)
            Rect nb = e.nameBounds.width > 0f ? e.nameBounds : new Rect(0f, 0f, e.nameArt.rect.width, e.nameArt.rect.height);
            Put(nameArt, e.nameArt, e.nameCorner.x - (nb.x + nb.width), e.nameCorner.y - (nb.y + nb.height), e.nameArt.rect.width, e.nameArt.rect.height);
        }
        else Hide(nameArt);
        if (fallbackName != null) fallbackName.gameObject.SetActive(false);

        for (int i = 0; traits != null && i < traits.Length; i++)
        {
            Sprite t = e.traits != null && i < e.traits.Length ? e.traits[i] : null;
            if (t == null) { Hide(traits[i]); continue; }
            Vector2 p = e.traitPositions != null && i < e.traitPositions.Length ? e.traitPositions[i] : AutoTraits[Mathf.Min(i, AutoTraits.Length - 1)];
            float x = Mathf.Clamp(p.x, 0f, 1915f - t.rect.width);
            Put(traits[i], t, x, p.y, t.rect.width, t.rect.height);
        }

        for (int i = 0; minis != null && i < minis.Length; i++)
        {
            // this slot's own icon if set, else the plain Mini Skill square; icons are fitted into the square's size
            Sprite icon = e.skillIcons != null && i < e.skillIcons.Length ? e.skillIcons[i] : null;
            Sprite shown = icon != null ? icon : e.miniSkill;
            if (shown == null) { Hide(minis[i]); continue; }
            Sprite size = e.miniSkill != null ? e.miniSkill : shown;
            Put(minis[i], shown, 1375f + 125f * i, 825f, size.rect.width, size.rect.height);
            if (minis[i] != null) minis[i].preserveAspect = true;
        }

        Sprite more = details ? e.backArt : e.moreArt;
        if (more != null && moreImage != null)
        {
            Put(moreImage, more, 1645f, 965f, more.rect.width, more.rect.height);
            moreButton.gameObject.SetActive(true);
        }
        else if (moreButton != null) moreButton.gameObject.SetActive(false);
    }

    // A character with no select art yet: its normal sprite on a plain colour, its name in text, no More page.
    void ShowFallback(int id)
    {
        CharacterLook data = CharacterRegistry.GetLook(id);
        backing.sprite = null;
        backing.color = data != null ? data.placeholderColor : new Color(0.4f, 0.4f, 0.4f);
        Place(backing.rectTransform, 1260f, 80f, 500f, 1000f);
        backing.enabled = true;

        bool hasSprite = data != null && data.sprite != null;
        sprite.enabled = hasSprite;
        if (hasSprite)
        {
            sprite.sprite = data.sprite;
            sprite.color = Color.white;
            sprite.preserveAspect = true;
            Place(sprite.rectTransform, 1260f, 180f, 500f, 900f);
        }
        Hide(nameArt);
        for (int i = 0; traits != null && i < traits.Length; i++) Hide(traits[i]);
        for (int i = 0; minis != null && i < minis.Length; i++) Hide(minis[i]);
        if (moreButton != null) moreButton.gameObject.SetActive(false);
        if (fallbackName != null)
        {
            fallbackName.gameObject.SetActive(true);
            fallbackName.text = CharacterList.NameOf(id).ToUpperInvariant();
            Place(fallbackName.rectTransform, 1260f, 700f, 500f, 120f);
        }
    }

    static void Put(Image img, Sprite s, float x, float y, float w, float h)
    {
        if (img == null) return;
        img.enabled = s != null;
        img.sprite = s;
        img.color = Color.white;
        Place(img.rectTransform, x, y, w, h);
    }

    static void Hide(Image img)
    {
        if (img != null) img.enabled = false;
    }

    // Top-left corner at (x, y) of the 1920x1080 layout.
    public static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }
}
