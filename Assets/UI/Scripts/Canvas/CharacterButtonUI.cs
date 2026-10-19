using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One tile of the character grid. This is a prefab (Assets/UI/Prefabs/CharacterButton.prefab):
// the character's finished tile picture (CharacterSelectArt's `select`; without one, a coloured backing with the
// character's head and shoulders), covered by its black silhouette (`picked`) unless it is the current pick.
// Tiles with no silhouette picture get a bright frame when picked instead. Characters with no
// select art yet get their normal sprite on a plain colour and their name; the Random and locked tiles are
// plain colour with a big symbol (see SetupSpecial).
public class CharacterButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public const float TileWidth = 150f;
    public const float TileHeight = 200f;

    public Button button;
    public Image backing;           // the coloured gradient
    public Image portrait;          // the character, cropped to the tile (its parent has a RectMask2D)
    public TMP_Text nameText;       // only for tiles with no art: the name, or the symbol on a special tile
    public GameObject selectedMark; // switched on for the chosen character (a bright frame)
    public Image pickedIcon;        // covers the whole tile with the black silhouette while it is NOT the current pick

    // old field kept so existing prefabs still load
    public Image colorStrip;

    public int CharacterId { get; private set; }
    bool locked;
    Action<int, bool> hover;
    Sprite pickedSprite;            // null = no silhouette art yet: the tile stays in colour and uses the bright frame

    public void Setup(int id, Action<int> onClick, Action<int, bool> onHover = null)
    {
        CharacterId = id;
        hover = onHover;
        locked = false;

        CharacterSelectArt art = CharacterSelectArt.Load();
        CharacterSelectArt.Entry e = art != null ? art.Find(id) : null;
        SetPickedSprite(e != null ? e.picked : null);
        if (e != null && e.select != null) ShowTileArt(e.select);   // finished tile art: drawn as it is
        else if (e != null && e.sprite != null && e.backing != null)
        {
            backing.sprite = e.backing;
            backing.color = Color.white;
            portrait.enabled = true;
            portrait.sprite = e.sprite;
            portrait.color = Color.white;
            portrait.preserveAspect = false;
            // fill the tile's width with the character, top of the head 6 px from the top edge
            Rect b = e.spriteBounds.width > 0f ? e.spriteBounds : new Rect(0f, 0f, e.sprite.rect.width, e.sprite.rect.height);
            float s = TileWidth * 1.05f / b.width;
            if (e.spriteScale > 0f) s = Mathf.Min(s, (TileHeight - 12f) / b.height);   // small pixel art: show all of it
            RectTransform rt = portrait.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(e.sprite.rect.width * s, e.sprite.rect.height * s);
            rt.anchoredPosition = new Vector2(TileWidth * 0.5f - (b.x + b.width * 0.5f) * s, -(6f - b.y * s));
            if (e.tilePortraitRect.width > 0f)
            {
                rt.anchoredPosition = new Vector2(e.tilePortraitRect.x, e.tilePortraitRect.y);
                rt.sizeDelta = new Vector2(e.tilePortraitRect.width, e.tilePortraitRect.height);
            }
            if (nameText != null) nameText.gameObject.SetActive(false);
        }
        else
        {
            CharacterLook look = CharacterRegistry.GetLook(id);
            backing.sprite = null;
            backing.color = look != null ? look.placeholderColor : new Color(0.4f, 0.4f, 0.4f);
            bool hasSprite = look != null && look.sprite != null;
            portrait.enabled = hasSprite;
            if (hasSprite)
            {
                portrait.sprite = look.sprite;
                portrait.color = Color.white;
                portrait.preserveAspect = true;
                RectTransform rt = portrait.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(8f, 40f);
                rt.offsetMax = new Vector2(-8f, -8f);
            }
            if (nameText != null)
            {
                nameText.gameObject.SetActive(true);
                nameText.text = CharacterList.NameOf(id);
                nameText.fontSize = 24;
                nameText.rectTransform.anchorMin = Vector2.zero;
                nameText.rectTransform.anchorMax = new Vector2(1f, 0.2f);
                nameText.rectTransform.offsetMin = nameText.rectTransform.offsetMax = Vector2.zero;
            }
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick(id));
        }
    }

    // The Random tile ("?") and the locked "coming soon" tile: their tile art if there is some, else a plain
    // colour with one big symbol. `picked` is the tile's look while it is the current pick (Random has one).
    public void SetupSpecial(int id, string symbol, Color background, Color symbolColor, Action<int> onClick, bool isLocked, Sprite tile = null, Sprite picked = null)
    {
        CharacterId = id;
        locked = isLocked;
        hover = null;
        SetPickedSprite(picked);
        if (tile != null) ShowTileArt(tile);
        else
        {
            backing.sprite = null;
            backing.color = background;
            portrait.enabled = false;
        }
        if (nameText != null && tile == null)
        {
            nameText.gameObject.SetActive(true);
            nameText.text = symbol;
            nameText.color = symbolColor;
            nameText.fontSize = isLocked ? 30 : 160;
            nameText.fontStyle = FontStyles.Bold;
            nameText.rectTransform.anchorMin = Vector2.zero;
            nameText.rectTransform.anchorMax = Vector2.one;
            nameText.rectTransform.offsetMin = nameText.rectTransform.offsetMax = Vector2.zero;
        }
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (!isLocked) button.onClick.AddListener(() => onClick(id));
            button.interactable = !isLocked;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hover != null && !locked) hover(CharacterId, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hover != null && !locked) hover(CharacterId, false);
    }

    // Every tile shows its dark silhouette; the current pick (selected) and the tile under the mouse
    // (hovered) light up in colour. A tile with no silhouette art is always in colour and gets the
    // bright frame when selected instead (hovering doesn't add the frame).
    public void SetSelected(bool selected, bool hovered = false)
    {
        bool hasDark = pickedSprite != null && pickedIcon != null;
        bool showDark = hasDark && !selected && !hovered;
        if (pickedIcon != null && pickedIcon.enabled != showDark) pickedIcon.enabled = showDark;
        bool frame = selected && !hasDark;
        if (selectedMark != null && selectedMark.activeSelf != frame) selectedMark.SetActive(frame);
    }

    // A finished 150x200 tile picture (backing and portrait in one) fills the tile; no portrait, no name.
    void ShowTileArt(Sprite tile)
    {
        backing.sprite = tile;
        backing.color = Color.white;
        portrait.enabled = false;
        if (nameText != null) nameText.gameObject.SetActive(false);
    }

    void SetPickedSprite(Sprite picked)
    {
        pickedSprite = picked;
        if (pickedIcon == null) return;
        pickedIcon.sprite = picked;
        pickedIcon.enabled = false;
    }

    // Greyed out but still clickable (used by the Random picker for characters that are not in the draw).
    public void SetDimmed(bool dimmed)
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            if (!dimmed) return;
            group = gameObject.AddComponent<CanvasGroup>();
        }
        group.alpha = dimmed ? 0.35f : 1f;
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null) button.interactable = interactable && !locked;
    }
}
