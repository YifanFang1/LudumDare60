using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One line on the controls page: the action name and a button showing its key.
// This is a prefab (Assets/UI/Prefabs/ControlRow.prefab): restyle it once and every row changes.
public class ControlRowUI : MonoBehaviour
{
    public TMP_Text label;
    public Button bindButton;
    public TMP_Text bindingText;

    public void Setup(int index, string actionName, Action<int> onClick)
    {
        if (label != null) label.text = actionName;
        if (bindButton != null)
        {
            bindButton.onClick.RemoveAllListeners();
            bindButton.onClick.AddListener(() => { AudioManager.Click(); onClick(index); });
        }
    }

    public void SetBinding(string text)
    {
        if (bindingText != null && bindingText.text != text) bindingText.text = text;
    }
}
