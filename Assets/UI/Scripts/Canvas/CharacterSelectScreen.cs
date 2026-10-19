using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Character select. Vs AI there are two tabs (you / the AI); online you only pick your own and press Ready.
// The 3x3 grid fills itself from CharacterList using the CharacterButton prefab. Hovering or clicking a tile shows
// that character on the big panel (art from CharacterSelectArt); More opens their Background / Skills page.
// A 30 second gear counts down and confirms the current pick when it runs out.
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

    [Header("Character panel and More page (built by Fight > Rebuild Default Canvas)")]
    public GameObject listPage;                 // frame, grid, Back / Fight, tabs: hidden on the More page
    public GameObject detailsPage;              // the Background / Skills text page
    public CharacterProfilePanel profile;
    public RectTransform gear;
    public TMP_Text timerText;
    public Vector2 gearListPosition = new Vector2(845f, 183f);      // top-left in the 1920x1080 layout
    public Vector2 gearDetailsPosition = new Vector2(14f, 245f);
    public float pickSeconds = 30f;
    public Button backgroundTab;
    public Button skillsTab;
    public Image backgroundTabImage;
    public Image skillsTabImage;
    public Sprite backgroundOn, backgroundOff, skillsOn, skillsOff;
    public TMP_Text backgroundText;
    public RectTransform skillsList;
    public ScrollRect detailsScroll;

    const int RandomId = -1;   // the "Random" tile: the real pick is made when you confirm
    const int LockedId = -2;   // the greyed "coming soon" tile
    const int NoHover = int.MinValue;
    const int GridTiles = 9;

    // Grid order (3 per row): Patience, Ruy, Odette / Remy, K3N, Noir / Room, then Random and a locked slot.
    static readonly int[] DisplayOrder = { Executioner.Id, Samurai.Id, Painter.Id, PlagueDoctor.Id, K3N.Id, Cowboy.Id, Elephant.Id };

    IMatchSession match;
    bool wired;
    bool pickingOpponent;
    bool randomYou, randomOpponent;
    readonly List<CharacterButtonUI> buttons = new List<CharacterButtonUI>();
    readonly List<int> builtIds = new List<int>();

    int previewId = int.MinValue;   // the character on the big panel when nothing is hovered
    int hoverId = NoHover;
    bool detailsOpen;
    bool showSkills;
    int detailsShownId = int.MinValue;
    bool detailsShownSkills;
    float timeLeft;

    void OnEnable()
    {
        match = SessionFinder.Find();
        if (match == null) return;
        pickingOpponent = false;
        // every time character select opens, both sides start on Random
        randomYou = randomOpponent = true;
        if (!wired) { Wire(); wired = true; }
        Rebuild();
        timeLeft = pickSeconds;
        hoverId = NoHover;
        showSkills = false;
        SetDetails(false);
        if (previewId == int.MinValue || !builtIds.Contains(previewId)) previewId = FirstCharacter();
    }

    int FirstCharacter()
    {
        for (int i = 0; i < builtIds.Count; i++)
        {
            if (builtIds[i] != Sandbag.Id) return builtIds[i];
        }
        return builtIds.Count > 0 ? builtIds[0] : 0;
    }

    void Wire()
    {
        if (youTab != null) youTab.onClick.AddListener(() => { AudioManager.Click(); pickingOpponent = false; });
        if (opponentTab != null) opponentTab.onClick.AddListener(() => { AudioManager.Click(); pickingOpponent = true; });
        if (confirmButton != null) confirmButton.onClick.AddListener(() => { AudioManager.Confirm(); ResolveRandom(); match.ConfirmCharacter(); });
        if (backButton != null) backButton.onClick.AddListener(() => { AudioManager.Click(); match.Leave(); });
        if (profile != null && profile.moreButton != null)
            profile.moreButton.onClick.AddListener(() =>
            {
                if (!detailsOpen && !CharacterProfilePanel.HasArt(previewId)) return;
                AudioManager.Click();
                SetDetails(!detailsOpen);
            });
        if (backgroundTab != null) backgroundTab.onClick.AddListener(() => { AudioManager.Click(); showSkills = false; });
        if (skillsTab != null) skillsTab.onClick.AddListener(() => { AudioManager.Click(); showSkills = true; });
    }

    // Opens or closes the More page: the character slides left and the text page replaces the grid.
    void SetDetails(bool open)
    {
        detailsOpen = open;
        if (open) showSkills = false;
        if (listPage != null) listPage.SetActive(!open);
        if (detailsPage != null) detailsPage.SetActive(open);
        if (gear != null)
        {
            Vector2 p = open ? gearDetailsPosition : gearListPosition;
            CharacterProfilePanel.Place(gear, p.x, p.y, 215f, 215f);
        }
        detailsShownId = int.MinValue;   // refill the text page
    }

    void Update()
    {
        if (match == null) return;

        if (GridOutOfDate()) Rebuild();

        bool choosesOpponent = match.ChoosesOpponent;
        bool forOpponent = choosesOpponent && pickingOpponent;
        int current = forOpponent ? match.OpponentCharacter : match.LocalCharacter;
        bool randomNow = forOpponent ? randomOpponent : randomYou;

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].CharacterId == LockedId) { buttons[i].SetInteractable(false); continue; }
            bool isRandomTile = buttons[i].CharacterId == RandomId;
            if (isRandomTile)
            {
                buttons[i].SetSelected(randomNow);
                buttons[i].SetDimmed(false);
            }
            else if (randomNow)
            {
                buttons[i].SetSelected(false);
                buttons[i].SetDimmed(false);
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
        if (youTabLabel != null) youTabLabel.text = "You: " + (randomYou ? "Random" : CharacterList.NameOf(match.LocalCharacter));
        if (opponentTabLabel != null) opponentTabLabel.text = "Opponent: " + (randomOpponent ? "Random" : CharacterList.NameOf(match.OpponentCharacter));

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

        // The pick countdown only runs online; vs AI there is no timer and no gear.
        if (gear != null) gear.gameObject.SetActive(!choosesOpponent);
        if (!choosesOpponent) UpdateTimer();
        UpdateProfile();
    }

    // The gear counts down while you have not confirmed; at 0 the current pick (or Random) is confirmed for you.
    void UpdateTimer()
    {
        if (match.CharacterConfirmed)
        {
            if (timerText != null) timerText.text = "OK";
            return;
        }
        timeLeft -= Time.unscaledDeltaTime;
        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            if (detailsOpen) SetDetails(false);
            AudioManager.Confirm();
            ResolveRandom();
            match.ConfirmCharacter();
        }
        if (timerText != null) timerText.text = Mathf.CeilToInt(timeLeft) + "s";
    }

    // The big panel shows the hovered tile, else the last one you clicked.
    void UpdateProfile()
    {
        int show = !detailsOpen && hoverId != NoHover ? hoverId : previewId;
        if (profile != null) profile.Show(show, detailsOpen);
        if (!detailsOpen) return;

        if (backgroundTabImage != null) backgroundTabImage.sprite = showSkills ? backgroundOff : backgroundOn;
        if (skillsTabImage != null) skillsTabImage.sprite = showSkills ? skillsOn : skillsOff;
        if (show == detailsShownId && showSkills == detailsShownSkills) return;
        detailsShownId = show;
        detailsShownSkills = showSkills;

        CharacterSelectArt art = CharacterSelectArt.Load();
        CharacterSelectArt.Entry e = art != null ? art.Find(show) : null;
        if (backgroundText != null)
        {
            backgroundText.gameObject.SetActive(!showSkills);
            backgroundText.text = e != null && !string.IsNullOrEmpty(e.background) ? e.background : "Coming soon.";
        }
        if (skillsList != null)
        {
            skillsList.gameObject.SetActive(showSkills);
            if (showSkills) BuildSkillRows(e);
        }
        if (detailsScroll != null) detailsScroll.verticalNormalizedPosition = 1f;
    }

    void BuildSkillRows(CharacterSelectArt.Entry e)
    {
        for (int i = skillsList.childCount - 1; i >= 0; i--) Destroy(skillsList.GetChild(i).gameObject);
        if (e == null || e.skills == null || e.skills.Length == 0)
        {
            AddSkillRow(null, "Coming soon.");
            return;
        }
        for (int i = 0; i < e.skills.Length; i++)
        {
            CharacterSelectArt.Skill k = e.skills[i];
            AddSkillRow(e.skillBlock, "<color=#F5891F><size=125%>" + k.name + "</size></color>\n" + k.description);
        }
    }

    void AddSkillRow(Sprite block, string text)
    {
        GameObject row = new GameObject("Skill", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(skillsList, false);
        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 24f;
        h.childAlignment = TextAnchor.UpperLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        if (block != null)
        {
            GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            icon.transform.SetParent(row.transform, false);
            Image img = icon.GetComponent<Image>();
            img.sprite = block;
            img.raycastTarget = false;
            LayoutElement ile = icon.GetComponent<LayoutElement>();
            ile.preferredWidth = 100f;
            ile.preferredHeight = 95f;
        }
        GameObject tg = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
        tg.transform.SetParent(row.transform, false);
        TextMeshProUGUI t = tg.AddComponent<TextMeshProUGUI>();
        if (backgroundText != null)
        {
            t.font = backgroundText.font;
            t.fontSize = backgroundText.fontSize * 0.85f;
            t.color = backgroundText.color;
        }
        t.text = text;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.raycastTarget = false;
        LayoutElement le = tg.GetComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minHeight = 95f;
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
        buttons.Clear();
        builtIds.Clear();
        if (gridParent == null || buttonPrefab == null) return;
        // Clear EVERY tile under the grid, not just the ones this list knows about (a second Rebuild in the same
        // frame, or tiles left from an earlier open, used to pile up as duplicates). Deactivate first: Destroy
        // only happens at the end of the frame and the layout would still count them.
        for (int i = gridParent.childCount - 1; i >= 0; i--)
        {
            GameObject old = gridParent.GetChild(i).gameObject;
            old.SetActive(false);
            Destroy(old);
        }

        System.Collections.Generic.IList<int> ids = CharacterList.Ids;
        for (int i = 0; i < ids.Count; i++) builtIds.Add(ids[i]);

        // the grid order, then anything else that is not the training dummy
        List<int> order = new List<int>();
        for (int i = 0; i < DisplayOrder.Length; i++)
        {
            if (builtIds.Contains(DisplayOrder[i])) order.Add(DisplayOrder[i]);
        }
        for (int i = 0; i < builtIds.Count; i++)
        {
            if (builtIds[i] != Sandbag.Id && !order.Contains(builtIds[i])) order.Add(builtIds[i]);
        }
        for (int i = 0; i < order.Count; i++)
        {
            CharacterButtonUI b = Instantiate(buttonPrefab, gridParent);
            b.Setup(order[i], Pick, Hover);
            buttons.Add(b);
        }

        // Random next ("?"), then locked "coming soon" slots up to a full 3x3
        CharacterButtonUI random = Instantiate(buttonPrefab, gridParent);
        random.SetupSpecial(RandomId, "?", new Color(0.55f, 0.55f, 0.55f), Color.black, Pick, false);
        buttons.Add(random);
        while (buttons.Count < GridTiles)
        {
            CharacterButtonUI locked = Instantiate(buttonPrefab, gridParent);
            locked.SetupSpecial(LockedId, "SOON", Color.black, new Color(0.7f, 0.7f, 0.7f), Pick, true);
            buttons.Add(locked);
        }
    }

    void Hover(int id, bool on)
    {
        if (on) hoverId = id;
        else if (hoverId == id) hoverId = NoHover;
    }

    // Turns a Random tile into a real pick (Sandbag, the training dummy, is not in the draw).
    // Online the result is what gets sent to the opponent, so both sides agree.
    void ResolveRandom()
    {
        if (randomYou)
        {
            match.LocalCharacter = RandomCharacter();
            AudioManager.CharacterPicked(match.LocalCharacter);   // the rolled fighter still says their Select line
        }
        if (randomOpponent && match.ChoosesOpponent) match.OpponentCharacter = RandomCharacter();
        randomYou = randomOpponent = false;
    }

    // Everyone except Sandbag.
    static int RandomCharacter()
    {
        System.Collections.Generic.IList<int> ids = CharacterList.Ids;
        System.Collections.Generic.List<int> pool = new System.Collections.Generic.List<int>();
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] != Sandbag.Id) pool.Add(ids[i]);
        }
        if (pool.Count == 0) return ids.Count > 0 ? ids[0] : 0;
        return pool[Random.Range(0, pool.Count)];
    }

    void Pick(int id)
    {
        if (match.CharacterConfirmed) return;
        AudioManager.Click();
        bool forOpponent = match.ChoosesOpponent && pickingOpponent;
        if (id == RandomId)
        {
            if (forOpponent) randomOpponent = true; else randomYou = true;
            return;
        }
        previewId = id;
        if (forOpponent) randomOpponent = false; else randomYou = false;
        if (forOpponent) match.OpponentCharacter = id;
        else
        {
            match.LocalCharacter = id;
            AudioManager.CharacterPicked(id);   // your character's Select voice line
        }
    }
}
