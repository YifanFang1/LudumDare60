using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One character on the select screen. This is a prefab (Assets/UI/Prefabs/CharacterButton.prefab):
// restyle it once and every character button changes.
public class CharacterButtonUI : MonoBehaviour
{
    public Button button;
    public Image portrait;          // the character's sprite
    public Image colorStrip;        // shown instead of a sprite: the character's placeholder colour
    public TMP_Text nameText;
    public GameObject selectedMark; // switched on for the chosen character

    public int CharacterId { get; private set; }

    public void Setup(int id, Action<int> onClick)
    {
        CharacterId = id;
        if (nameText != null) nameText.text = CharacterList.NameOf(id);

        CharacterData art = CharacterRegistry.Get(id);   // null for characters written purely in code
        bool hasSprite = art != null && art.sprite != null;
        if (portrait != null)
        {
            portrait.enabled = hasSprite;
            if (hasSprite)
            {
                portrait.sprite = art.sprite;
                portrait.preserveAspect = true;
            }
        }
        if (colorStrip != null)
        {
            colorStrip.enabled = art != null;
            if (art != null) colorStrip.color = art.placeholderColor;
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick(id));
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedMark != null && selectedMark.activeSelf != selected) selectedMark.SetActive(selected);
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
        if (button != null) button.interactable = interactable;
    }
}
