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

    const int RandomId = -1;   // the "Random" tile: the real pick is made when you confirm

    IMatchSession match;
    bool wired;
    bool pickingOpponent;
    bool randomYou, randomOpponent;
    // Random picker: turning Random on puts everyone in the draw except Sandbag; click a character to take
    // it out (or put it back). Sandbag only comes up if you click it in.
    readonly HashSet<int> poolYou = new HashSet<int>();
    readonly HashSet<int> poolOpponent = new HashSet<int>();
    readonly List<CharacterButtonUI> buttons = new List<CharacterButtonUI>();
    readonly List<int> builtIds = new List<int>();

    void OnEnable()
    {
        match = SessionFinder.Find();
        if (match == null) return;
        pickingOpponent = false;
        // every time character select opens, both sides start on Random with everyone except Sandbag in the draw
        randomYou = randomOpponent = true;
        FillPool(poolYou);
        FillPool(poolOpponent);
        if (!wired) { Wire(); wired = true; }
        Rebuild();
    }

    void Wire()
    {
        if (youTab != null) youTab.onClick.AddListener(() => { AudioManager.Click(); pickingOpponent = false; });
        if (opponentTab != null) opponentTab.onClick.AddListener(() => { AudioManager.Click(); pickingOpponent = true; });
        if (confirmButton != null) confirmButton.onClick.AddListener(() => { AudioManager.Confirm(); ResolveRandom(); match.ConfirmCharacter(); });
        if (backButton != null) backButton.onClick.AddListener(() => { AudioManager.Click(); match.Leave(); });
    }

    void Update()
    {
        if (match == null) return;

        if (GridOutOfDate()) Rebuild();

        bool choosesOpponent = match.ChoosesOpponent;
        bool forOpponent = choosesOpponent && pickingOpponent;
        int current = forOpponent ? match.OpponentCharacter : match.LocalCharacter;
        bool randomNow = forOpponent ? randomOpponent : randomYou;
        HashSet<int> pool = forOpponent ? poolOpponent : poolYou;

        for (int i = 0; i < buttons.Count; i++)
        {
            bool isRandomTile = buttons[i].CharacterId == RandomId;
            if (isRandomTile)
            {
                buttons[i].SetSelected(randomNow);
                buttons[i].SetDimmed(false);
            }
            else if (randomNow)
            {
                // in the draw = highlighted, everything else greyed (none picked = all of them are in)
                bool inDraw = pool.Contains(buttons[i].CharacterId);
                buttons[i].SetSelected(inDraw);
                buttons[i].SetDimmed(!inDraw);
            }
            else
            {
                buttons[i].SetSelected(buttons[i].CharacterId == current);
                buttons[i].SetDimmed(false);
            }
            buttons[i].SetInteractable(!match.CharacterConfirmed);
        }

        if (tabsGroup != null) tabsGroup.SetActive(choosesOpponent);
        // The tab you are on is switched off, so its "disabled" look doubles as the selected look.
        if (youTab != null) youTab.interactable = pickingOpponent;
        if (opponentTab != null) opponentTab.interactable = !pickingOpponent;
        if (youTabLabel != null) youTabLabel.text = "You: " + (randomYou ? RandomLabel(poolYou) : CharacterList.NameOf(match.LocalCharacter));
        if (opponentTabLabel != null) opponentTabLabel.text = "Opponent: " + (randomOpponent ? RandomLabel(poolOpponent) : CharacterList.NameOf(match.OpponentCharacter));

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

        // the Random tile goes last
        CharacterButtonUI random = Instantiate(buttonPrefab, gridParent);
        random.Setup(RandomId, Pick);
        if (random.nameText != null) random.nameText.text = "Random";
        if (random.portrait != null) random.portrait.enabled = false;
        if (random.colorStrip != null) random.colorStrip.enabled = false;
        buttons.Add(random);
    }

    // Turns a Random tile into a real pick (Sandbag, the training dummy, is not in the draw).
    // Online the result is what gets sent to the opponent, so both sides agree.
    void ResolveRandom()
    {
        if (randomYou) match.LocalCharacter = RandomCharacter(poolYou);
        if (randomOpponent && match.ChoosesOpponent) match.OpponentCharacter = RandomCharacter(poolOpponent);
        randomYou = randomOpponent = false;
        poolYou.Clear();
        poolOpponent.Clear();
    }

    // Everyone except Sandbag.
    static void FillPool(HashSet<int> pool)
    {
        pool.Clear();
        System.Collections.Generic.IList<int> ids = CharacterList.Ids;
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] != Sandbag.Id) pool.Add(ids[i]);
        }
    }

    static string RandomLabel(HashSet<int> picked)
    {
        return "Random (" + picked.Count + ")";
    }

    // picked = the characters the player ticked; empty means everyone except Sandbag.
    static int RandomCharacter(HashSet<int> picked)
    {
        System.Collections.Generic.IList<int> ids = CharacterList.Ids;
        System.Collections.Generic.List<int> pool = new System.Collections.Generic.List<int>();
        for (int i = 0; i < ids.Count; i++)
        {
            if (picked.Count > 0 ? picked.Contains(ids[i]) : ids[i] != Sandbag.Id) pool.Add(ids[i]);
        }
        if (pool.Count == 0) return ids.Count > 0 ? ids[0] : 0;
        return pool[Random.Range(0, pool.Count)];
    }

    void Pick(int id)
    {
        if (match.CharacterConfirmed) return;
        AudioManager.Click();
        bool forOpponent = match.ChoosesOpponent && pickingOpponent;
        HashSet<int> pool = forOpponent ? poolOpponent : poolYou;
        bool randomOn = forOpponent ? randomOpponent : randomYou;
        if (id == RandomId)
        {
            // Random toggles: on = everyone is greyed, click characters to put them in the draw
            randomOn = !randomOn;
            if (randomOn) FillPool(pool); else pool.Clear();
            if (forOpponent) randomOpponent = randomOn; else randomYou = randomOn;
            return;
        }
        if (randomOn)
        {
            if (pool.Contains(id)) { if (pool.Count > 1) pool.Remove(id); }   // keep at least one in the draw
            else pool.Add(id);
            return;
        }
        if (forOpponent) match.OpponentCharacter = id;
        else match.LocalCharacter = id;
    }
}
