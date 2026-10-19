using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Character select. Online you pick your own fighter and press Ready. Vs AI it takes two steps on the same
// screen: pick yourself and press Ready, then the grid starts fresh under "Choose an opponent" and you pick the
// AI's fighter and press Fight! (Back on step 2 returns to step 1 with your pick kept). The steps swap through
// the same CRT glitch as every other screen change. The current pick's tile shows as a black silhouette.
// The 3x3 grid fills itself from CharacterList using the CharacterButton prefab. Hovering or clicking a tile shows
// that character on the big panel (art from CharacterSelectArt); More opens their Background / Skills page.
// Online, a 30 second gear counts down and confirms the current pick when it runs out.
public class CharacterSelectScreen : MonoBehaviour
{
    [Header("Grid")]
    public RectTransform gridParent;            // the Content of a scroll view with a GridLayoutGroup
    public CharacterButtonUI buttonPrefab;

    [Header("Buttons and text")]
    public Button confirmButton;                // "Ready": locks in your own pick (online, and step 1 vs AI)
    public Button fightButton;                  // "Fight!": vs AI step 2, locks in the AI's pick and starts the match
    public TMP_Text confirmLabel;
    public Button backButton;                   // "Back" vs AI, "Leave room" online
    public TMP_Text backLabel;
    public GameObject youTitle;                 // "Choose a renegade" picture
    public GameObject opponentTitle;            // "Choose an opponent" picture (vs AI step 2)
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
    const int NoTile = int.MinValue;
    const int GridTiles = 9;

    // Grid order (3 per row): Patience, Ruy, Odette / Remy, K3N, Noir / Room, then Random and the last slot
    // (Sandbag vs AI, a locked "coming soon" tile online).
    static readonly int[] DisplayOrder = { Executioner.Id, Samurai.Id, Painter.Id, PlagueDoctor.Id, K3N.Id, Cowboy.Id, Elephant.Id };

    IMatchSession match;
    MatchScreens screens;   // for its glitch transition between the two vs-AI steps
    bool wired;
    readonly List<CharacterButtonUI> buttons = new List<CharacterButtonUI>();
    readonly List<int> builtIds = new List<int>();
    bool builtVsAI;         // the grid was built with the Sandbag tile (vs AI) or the "coming soon" tile (online)

    // The two steps. Online there is only the first.
    bool pickingOpponent;   // vs AI step 2: the grid picks the AI's fighter
    bool randomYou;         // the "?" tile is your current pick
    bool randomOpponent;    // the "?" tile is the AI's current pick
    bool youLocked;         // vs AI: Ready was pressed on step 1 (online, match.CharacterConfirmed says this)
    int youTile = RandomId; // the tile you locked in on step 1, so Back can restore it (Random stays Random)
    int lockedTile = NoTile;// the tile that stays picked once locked in: the "?" stays the "?" even after Random has rolled a fighter
    bool switching;         // a step change is waiting for the glitch or playing it: no clicks meanwhile
    bool switchToOpponent;
    bool glitchStarted;

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
        // every time character select opens, it starts on step 1 with both sides on Random
        pickingOpponent = false;
        randomYou = true;
        randomOpponent = true;
        youLocked = false;
        youTile = RandomId;
        lockedTile = NoTile;
        switching = false;
        glitchStarted = false;
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
        screens = GetComponentInParent<MatchScreens>(true);
        if (confirmButton != null) confirmButton.onClick.AddListener(LockIn);
        if (fightButton != null) fightButton.onClick.AddListener(LockIn);
        if (backButton != null) backButton.onClick.AddListener(Back);
        if (profile != null && profile.moreButton != null)
            profile.moreButton.onClick.AddListener(() =>
            {
                if (switching) return;
                if (!detailsOpen && !CharacterProfilePanel.HasArt(previewId)) return;
                AudioManager.Click();
                SetDetails(!detailsOpen);
            });
        if (backgroundTab != null) backgroundTab.onClick.AddListener(() => { AudioManager.Click(); showSkills = false; });
        if (skillsTab != null) skillsTab.onClick.AddListener(() => { AudioManager.Click(); showSkills = true; });
    }

    // Ready / Fight!: locks in the pick of the current step. Step 1 vs AI then moves on to the AI's pick;
    // otherwise the pick goes to the session (online it is sent to the opponent; vs AI the match starts).
    void LockIn()
    {
        if (LockedIn() || switching) return;
        AudioManager.Confirm();
        if (detailsOpen) SetDetails(false);
        lockedTile = SelectedTile();
        if (pickingOpponent)
        {
            if (randomOpponent) match.OpponentCharacter = RandomCharacter();
            randomOpponent = false;
            match.ConfirmCharacter();
            return;
        }
        if (randomYou)
        {
            match.LocalCharacter = RandomCharacter();
            AudioManager.CharacterPicked(match.LocalCharacter);   // the rolled fighter still says their Select line
        }
        randomYou = false;
        if (match.ChoosesOpponent)
        {
            youLocked = true;      // your tile goes black, then the glitch swaps to step 2
            youTile = lockedTile;
            SwitchStep(true);
        }
        else match.ConfirmCharacter();
    }

    void Back()
    {
        if (switching) return;
        AudioManager.Click();
        if (pickingOpponent && !match.CharacterConfirmed) SwitchStep(false);
        else match.Leave();
    }

    // The step only changes at the peak of the glitch, like a screen change. RunPendingSwitch starts it.
    void SwitchStep(bool toOpponent)
    {
        switching = true;
        switchToOpponent = toOpponent;
        glitchStarted = false;
        RunPendingSwitch();
    }

    void RunPendingSwitch()
    {
        if (!switching || glitchStarted) return;
        GlitchTransition glitch = screens != null ? screens.transition : null;
        if (glitch == null || !glitch.isActiveAndEnabled) { ApplyStep(switchToOpponent); return; }
        if (glitch.Busy) return;   // the glitch that opened this screen is still fading: start ours next frame
        glitchStarted = true;
        glitch.Run(() => ApplyStep(switchToOpponent));
    }

    void ApplyStep(bool opponent)
    {
        switching = false;
        glitchStarted = false;
        if (!isActiveAndEnabled) return;   // the screen closed during the glitch: OnEnable starts over anyway
        pickingOpponent = opponent;
        hoverId = NoHover;
        lockedTile = NoTile;
        SetDetails(false);
        if (opponent)
        {
            randomOpponent = true;          // a fresh grid for the AI's pick
            previewId = FirstCharacter();
        }
        else
        {
            youLocked = false;              // back on step 1: your pick is open again, as you left it
            randomYou = youTile == RandomId;
            if (!randomYou) previewId = match.LocalCharacter;
        }
        timeLeft = pickSeconds;
    }

    // Whether the current step's pick is locked in (its tile shows black and the grid stops taking clicks).
    bool LockedIn()
    {
        if (match.CharacterConfirmed) return true;
        return !pickingOpponent && youLocked;
    }

    // The tile of the current step's pick: the "?" while it is Random.
    int SelectedTile()
    {
        if (pickingOpponent) return randomOpponent ? RandomId : match.OpponentCharacter;
        return randomYou ? RandomId : match.LocalCharacter;
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
        RunPendingSwitch();

        bool vsAI = match.ChoosesOpponent;
        bool lockedIn = LockedIn();
        int shownTile = lockedIn ? lockedTile : SelectedTile();

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].CharacterId == LockedId) { buttons[i].SetInteractable(false); continue; }
            buttons[i].SetSelected(buttons[i].CharacterId == shownTile);
            buttons[i].SetDimmed(false);
            buttons[i].SetInteractable(!lockedIn && !switching);
        }

        if (youTitle != null) youTitle.SetActive(!pickingOpponent);
        if (opponentTitle != null) opponentTitle.SetActive(pickingOpponent);
        if (titleText != null)
        {
            string title = pickingOpponent ? "Choose an opponent" : "Choose your character";
            if (!vsAI && match.OpponentName.Length > 0) title += "\nvs " + match.OpponentName;
            titleText.text = title;
        }
        // Ready on step 1 (and online), Fight! on step 2. A canvas built before Fight! existed uses Ready for both.
        bool canConfirm = !lockedIn && !switching;
        bool fightShown = pickingOpponent && fightButton != null;
        if (confirmButton != null) confirmButton.gameObject.SetActive(canConfirm && !fightShown);
        if (fightButton != null) fightButton.gameObject.SetActive(canConfirm && fightShown);
        if (confirmLabel != null) confirmLabel.text = pickingOpponent ? "Fight!" : "Ready";
        if (backLabel != null) backLabel.text = vsAI ? "Back" : "Leave room";
        if (statusText != null) statusText.text = match.Status;

        // The pick countdown only runs online; vs AI there is no timer and no gear.
        if (gear != null) gear.gameObject.SetActive(!vsAI);
        if (!vsAI) UpdateTimer();
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
            LockIn();
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
        if (builtVsAI != match.ChoosesOpponent) return true;
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
        builtVsAI = match.ChoosesOpponent;
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
        for (int i = 0; i < order.Count; i++) AddCharacterTile(order[i]);

        // Random next ("?"), then Sandbag vs AI (bottom-right), then locked "coming soon" slots up to a full 3x3
        CharacterSelectArt art = CharacterSelectArt.Load();
        CharacterButtonUI random = Instantiate(buttonPrefab, gridParent);
        random.SetupSpecial(RandomId, "?", new Color(0.55f, 0.55f, 0.55f), Color.black, Pick, false,
            tile: art != null ? art.randomSelect : null, picked: art != null ? art.randomPicked : null);
        buttons.Add(random);
        if (builtVsAI && builtIds.Contains(Sandbag.Id)) AddCharacterTile(Sandbag.Id);
        while (buttons.Count < GridTiles)
        {
            CharacterButtonUI locked = Instantiate(buttonPrefab, gridParent);
            locked.SetupSpecial(LockedId, "SOON", Color.black, new Color(0.7f, 0.7f, 0.7f), Pick, true);
            buttons.Add(locked);
        }
    }

    void AddCharacterTile(int id)
    {
        CharacterButtonUI b = Instantiate(buttonPrefab, gridParent);
        b.Setup(id, Pick, Hover);
        buttons.Add(b);
    }

    void Hover(int id, bool on)
    {
        if (on) hoverId = id;
        else if (hoverId == id) hoverId = NoHover;
    }

    // Everyone except Sandbag (the training dummy is only ever a deliberate pick).
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

    // A click on a tile: it becomes the current step's pick (yours, or the AI's on step 2).
    void Pick(int id)
    {
        if (LockedIn() || switching) return;
        AudioManager.Click();
        if (id == RandomId)
        {
            if (pickingOpponent) randomOpponent = true;
            else randomYou = true;
            return;
        }
        previewId = id;
        if (pickingOpponent)
        {
            randomOpponent = false;
            match.OpponentCharacter = id;
        }
        else
        {
            randomYou = false;
            match.LocalCharacter = id;
        }
        AudioManager.CharacterPicked(id);   // the character's Select voice line
    }
}
