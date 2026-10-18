using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Character select. Vs AI there are two tabs (you / the AI); online you only pick your own and press Ready.
// The grid fills itself from CharacterList using the CharacterButton prefab.
public class CharacterSelectScreen : MonoBehaviour
{
    [Header("Grid")]
    public RectTransform gridParent;            // the Content of a scroll view with a GridLayoutGroup
    public CharacterButtonUI buttonPrefab;

    [Header("Tabs (vs AI only)")]
    public GameObject tabsGroup;
    public Button youTab;
    public Button opponentTab;
    public TMP_Text youTabLabel;
    public TMP_Text opponentTabLabel;

    [Header("Buttons and text")]
    public Button confirmButton;                // "Fight!" vs AI, "Ready" online
    public TMP_Text confirmLabel;
    public Button backButton;                   // "Back" vs AI, "Leave room" online
    public TMP_Text backLabel;
    public TMP_Text titleText;
    public TMP_Text statusText;

    IMatchSession match;
    bool wired;
    bool pickingOpponent;
    readonly List<CharacterButtonUI> buttons = new List<CharacterButtonUI>();
    readonly List<int> builtIds = new List<int>();

    void OnEnable()
    {
        match = SessionFinder.Find();
        if (match == null) return;
        pickingOpponent = false;
        if (!wired) { Wire(); wired = true; }
        Rebuild();
    }

    void Wire()
    {
        if (youTab != null) youTab.onClick.AddListener(() => { AudioManager.Click(); pickingOpponent = false; });
        if (opponentTab != null) opponentTab.onClick.AddListener(() => { AudioManager.Click(); pickingOpponent = true; });
        if (confirmButton != null) confirmButton.onClick.AddListener(() => { AudioManager.Confirm(); match.ConfirmCharacter(); });
        if (backButton != null) backButton.onClick.AddListener(() => { AudioManager.Click(); match.Leave(); });
    }

    void Update()
    {
        if (match == null) return;

        if (GridOutOfDate()) Rebuild();

        bool choosesOpponent = match.ChoosesOpponent;
        bool forOpponent = choosesOpponent && pickingOpponent;
        int current = forOpponent ? match.OpponentCharacter : match.LocalCharacter;

        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].SetSelected(buttons[i].CharacterId == current);
            buttons[i].SetInteractable(!match.CharacterConfirmed);
        }

        if (tabsGroup != null) tabsGroup.SetActive(choosesOpponent);
        // The tab you are on is switched off, so its "disabled" look doubles as the selected look.
        if (youTab != null) youTab.interactable = pickingOpponent;
        if (opponentTab != null) opponentTab.interactable = !pickingOpponent;
        if (youTabLabel != null) youTabLabel.text = "You: " + CharacterList.NameOf(match.LocalCharacter);
        if (opponentTabLabel != null) opponentTabLabel.text = "Opponent: " + CharacterList.NameOf(match.OpponentCharacter);

        if (titleText != null)
        {
            string title = choosesOpponent ? "Choose characters" : "Choose your character";
            if (!choosesOpponent && match.OpponentName.Length > 0) title += "\nvs " + match.OpponentName;
            titleText.text = title;
        }
        if (confirmButton != null) confirmButton.gameObject.SetActive(!match.CharacterConfirmed);
        if (confirmLabel != null) confirmLabel.text = choosesOpponent ? "Fight!" : "Ready";
        if (backLabel != null) backLabel.text = choosesOpponent ? "Back" : "Leave room";
        if (statusText != null) statusText.text = match.Status;
    }

    bool GridOutOfDate()
    {
        System.Collections.Generic.IList<int> ids = CharacterList.Ids;
        if (ids.Count != builtIds.Count) return true;
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] != builtIds[i]) return true;
        }
        return false;
    }

    // Characters can change after the game starts (the server sends its own), so the grid is rebuilt when they do.
    void Rebuild()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null) Destroy(buttons[i].gameObject);
        }
        buttons.Clear();
        builtIds.Clear();
        if (gridParent == null || buttonPrefab == null) return;

        System.Collections.Generic.IList<int> ids = CharacterList.Ids;
        for (int i = 0; i < ids.Count; i++)
        {
            builtIds.Add(ids[i]);
            CharacterButtonUI b = Instantiate(buttonPrefab, gridParent);
            b.Setup(ids[i], Pick);
            buttons.Add(b);
        }
    }

    void Pick(int id)
    {
        if (match.CharacterConfirmed) return;
        AudioManager.Click();
        if (match.ChoosesOpponent && pickingOpponent) match.OpponentCharacter = id;
        else match.LocalCharacter = id;
    }
}
