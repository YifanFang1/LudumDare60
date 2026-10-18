using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The fight HUD: health bars, player and character names, info lines, the result panel, and the
// player names that float above the fighters. Also the shared-kit parts (Design/SHARED_KIT.md):
// Ultimate meters, round pips, ROUND / K.O. banners and the Ultimate flash. Talks only to
// IMatchSession. Every reference is optional: an empty slot is skipped. For a FightUI built
// before these existed, run  Fight > Add Meter And Round HUD.
public class HudScreen : MonoBehaviour
{
    [Header("Health bars (Image with Type = Filled)")]
    public Image leftBar;
    public Image rightBar;

    [Header("Under the health bars")]
    public TMP_Text leftName;
    public TMP_Text rightName;
    public TMP_Text leftCharacter;
    public TMP_Text rightCharacter;

    [Header("Info lines")]
    public TMP_Text infoText;
    public TMP_Text controlsText;
    public TMP_Text tickText;
    public TMP_Text stalledText;
    public TMP_Text warningText;

    [Header("Result")]
    public GameObject resultPanel;
    public TMP_Text resultText;
    public Button resultButton;
    public Button rematchButton;         // made at runtime next to resultButton when left empty
    TMP_Text rematchLabel;

    [Header("Names floating above the fighters")]
    public RectTransform worldNameLayer;        // stretched over the whole screen, pivot in the middle
    public TMP_Text[] worldNames = new TMP_Text[2];
    public float nameHeightAboveHead = 0.35f;   // Unity units above the top of the fighter's body
    public Color localNameColor = new Color(0.75f, 1f, 0.55f);
    public Color otherNameColor = Color.white;

    [Header("Ultimate meters (Image with Type = Filled)")]
    public Image leftMeter;
    public Image rightMeter;
    public Color meterColor = new Color(0.25f, 0.60f, 1f);
    public Color meterFullColor = new Color(0.75f, 0.92f, 1f);   // a full meter pulses between the two colours
    public TMP_Text leftUltimateReady;           // shown only while that meter is full
    public TMP_Text rightUltimateReady;

    [Header("Rounds won: one Image per round (RoundsToWin), first round first")]
    public Image[] leftRoundPips;
    public Image[] rightRoundPips;
    public Color pipWonColor = new Color(0.62f, 0.95f, 0.35f);
    public Color pipEmptyColor = new Color(0.07f, 0.09f, 0.08f);

    [Header("Round timer (top middle)")]
    public TMP_Text timerText;                   // made at run time when empty
    public Color timerColor = Color.white;
    public Color timerLowColor = new Color(1f, 0.35f, 0.3f);   // the last 10 seconds

    [Header("Banners")]
    public TMP_Text bannerText;                  // ROUND 1 / FINAL ROUND, then K.O.
    public float bannerSeconds = 1.5f;

    [Header("Ultimate flash (while the match freezes for an Ultimate)")]
    public Image superFlash;                     // a full-screen Image; its alpha follows the freeze
    public float superFlashMaxAlpha = 0.45f;
    public TMP_Text superFlashText;              // "<name>: ULTIMATE!"

    IMatchSession match;
    bool wired;
    int shownRound;                              // the round whose banner was last shown (0 = none)
    float bannerUntil;
    string bannerRound = "";
    Vector2 bannerHomePosition;

    void OnEnable()
    {
        // Older scene canvases have no banner assigned: add a centered TMP label.
        if (bannerText == null)
        {
            GameObject label = new GameObject("RoundBanner", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(transform, false);
            bannerText = label.GetComponent<TMP_Text>();
            if (resultText != null) bannerText.font = resultText.font;
            bannerText.fontSize = 96f;
            bannerText.enableAutoSizing = true;
            bannerText.fontSizeMin = 28f;
            bannerText.fontSizeMax = 96f;
            bannerText.fontStyle = FontStyles.Bold;
            bannerText.alignment = TextAlignmentOptions.Center;
            bannerText.color = new Color(0.80f, 1f, 0.45f);
            bannerText.raycastTarget = false;
            RectTransform rect = bannerText.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 180f);
            rect.anchoredPosition = Vector2.zero;
        }
        bannerHomePosition = bannerText.rectTransform.anchoredPosition;
        shownRound = 0;   // this screen is switched on for each match, so ROUND 1 is shown again
        match = SessionFinder.Find();
        if (match == null) return;
        if (resultText != null)
        {
            // long names shrink to fit instead of running off the panel
            resultText.enableAutoSizing = true;
            resultText.fontSizeMin = 24f;
            resultText.fontSizeMax = 60f;
        }
        EnsureMeters();
        EnsureTimerAndPips();
        if (!wired && resultButton != null)
        {
            MakeRematchButton();
            MakeExitButton();
            resultButton.onClick.AddListener(() =>
            {
                AudioManager.Click();
                match.Leave();
            });
            wired = true;
        }
    }

    // The canvas HUD is built without Ultimate meters (older scenes never got them), so make them here
    // when the slots are empty: a bar and a label at the bottom corner under each fighter's side.
    static Sprite whiteSprite;

    void EnsureMeters()
    {
        if (leftMeter != null && rightMeter != null) return;
        if (whiteSprite == null)
        {
            Texture2D t = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
        }
        for (int side = 0; side < 2; side++)
        {
            bool right = side == 1;
            if ((right ? rightMeter : leftMeter) != null) continue;

            Vector2 corner = new Vector2(right ? 1f : 0f, 0f);
            GameObject bg = new GameObject(right ? "RightMeter" : "LeftMeter", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(transform, false);
            RectTransform bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = corner;
            bgRt.anchoredPosition = new Vector2(right ? -40f : 40f, 40f);
            bgRt.sizeDelta = new Vector2(520f, 24f);
            Image bgImage = bg.GetComponent<Image>();
            bgImage.sprite = whiteSprite;
            bgImage.color = new Color(0.06f, 0.14f, 0.10f, 1f);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bg.transform, false);
            RectTransform fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            Image fill = fillGo.GetComponent<Image>();
            fill.sprite = whiteSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = right ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.color = meterColor;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(transform, false);
            RectTransform lr = (RectTransform)labelGo.transform;
            lr.anchorMin = lr.anchorMax = lr.pivot = corner;
            lr.anchoredPosition = new Vector2(right ? -40f : 40f, 70f);
            lr.sizeDelta = new Vector2(520f, 34f);
            TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
            TMP_Text sample = leftName != null ? leftName : rightName;
            if (sample != null) label.font = sample.font;
            label.fontSize = 24f;
            label.alignment = right ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.BottomLeft;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = "ULTIMATE";

            if (right) { rightMeter = fill; rightUltimateReady = label; }
            else { leftMeter = fill; leftUltimateReady = label; }
        }
    }

    // Vs AI there is no room to leave, so a small Exit button sits at the bottom centre during the fight
    // (Escape does the same). Online matches don't show it: leaving there is a forfeit.
    Button exitButton;

    void MakeExitButton()
    {
        exitButton = Instantiate(resultButton, transform);
        exitButton.name = "ExitButton";
        exitButton.onClick.RemoveAllListeners();
        RectTransform rt = (RectTransform)exitButton.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(200f, 60f);
        rt.anchoredPosition = new Vector2(0f, 24f);
        TMP_Text label = exitButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Exit";
        exitButton.onClick.AddListener(() => { AudioManager.Click(); match.Leave(); });
        exitButton.gameObject.SetActive(false);
    }

    static Sprite circleSprite;

    static Sprite Circle()
    {
        if (circleSprite != null) return circleSprite;
        const int n = 64;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Color32[] px = new Color32[n * n];
        float r = n * 0.5f - 1f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - n * 0.5f + 0.5f) * (x - n * 0.5f + 0.5f) + (y - n * 0.5f + 0.5f) * (y - n * 0.5f + 0.5f));
                byte a = (byte)(255f * Mathf.Clamp01(r - d + 1f));
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
        }
        t.SetPixels32(px);
        t.Apply();
        t.filterMode = FilterMode.Bilinear;
        circleSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        return circleSprite;
    }

    // The round timer at the top middle, and the rounds-won indicators beside it: two black spheres per
    // player, and the one for each round won lights up in the middle. Built here when the scene has none.
    void EnsureTimerAndPips()
    {
        TMP_Text sample = leftName != null ? leftName : rightName;
        if (timerText == null)
        {
            GameObject go = new GameObject("TimerText", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -14f);
            rt.sizeDelta = new Vector2(200f, 84f);
            timerText = go.GetComponent<TextMeshProUGUI>();
            if (sample != null) timerText.font = sample.font;
            timerText.fontSize = 72f;
            timerText.fontStyle = FontStyles.Bold;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.color = timerColor;
            timerText.raycastTarget = false;
        }

        for (int side = 0; side < 2; side++)
        {
            bool right = side == 1;
            Image[] have = right ? rightRoundPips : leftRoundPips;
            if (have != null && have.Length > 0) continue;
            Image[] made = new Image[GameSim.RoundsToWin];
            for (int r = 0; r < made.Length; r++)
            {
                GameObject outer = new GameObject((right ? "RightPip" : "LeftPip") + (r + 1), typeof(RectTransform), typeof(Image));
                outer.transform.SetParent(transform, false);
                RectTransform ort = (RectTransform)outer.transform;
                ort.anchorMin = ort.anchorMax = ort.pivot = new Vector2(0.5f, 1f);
                float step = 44f;
                float x = 100f + step * r;               // the first round won sits nearest the timer
                ort.anchoredPosition = new Vector2(right ? x : -x, -50f);
                ort.sizeDelta = new Vector2(34f, 34f);
                Image shell = outer.GetComponent<Image>();
                shell.sprite = Circle();
                shell.color = new Color(0.02f, 0.03f, 0.03f, 1f);   // the black sphere
                shell.raycastTarget = false;

                GameObject inner = new GameObject("Light", typeof(RectTransform), typeof(Image));
                inner.transform.SetParent(outer.transform, false);
                RectTransform irt = (RectTransform)inner.transform;
                irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                irt.sizeDelta = new Vector2(16f, 16f);
                Image light = inner.GetComponent<Image>();
                light.sprite = Circle();
                light.color = pipEmptyColor;
                light.raycastTarget = false;
                made[r] = light;
            }
            if (right) rightRoundPips = made; else leftRoundPips = made;
        }
    }

    void UpdateTimer(GameSim sim)
    {
        if (timerText == null) return;
        bool show = sim.roundTimerOn;
        if (timerText.gameObject.activeSelf != show) timerText.gameObject.SetActive(show);
        if (!show) return;
        int seconds = Mathf.CeilToInt(Mathf.Max(0, sim.roundTimeTicks) / 60f);
        SetText(timerText, seconds.ToString());
        timerText.color = seconds <= 10 ? timerLowColor : timerColor;
    }

    // Copies the Back button: Rematch on the left, Back on the right.
    void MakeRematchButton()
    {
        if (rematchButton == null)
        {
            rematchButton = Instantiate(resultButton, resultButton.transform.parent);
            rematchButton.name = "RematchButton";
            rematchButton.onClick.RemoveAllListeners();
            RectTransform a = (RectTransform)rematchButton.transform;
            RectTransform b = (RectTransform)resultButton.transform;
            a.sizeDelta = b.sizeDelta = new Vector2(290f, b.sizeDelta.y);
            a.anchoredPosition = new Vector2(-160f, b.anchoredPosition.y);
            b.anchoredPosition = new Vector2(160f, b.anchoredPosition.y);
        }
        rematchLabel = rematchButton.GetComponentInChildren<TMP_Text>(true);
        TMP_Text backLabel = resultButton.GetComponentInChildren<TMP_Text>(true);
        if (backLabel != null) backLabel.text = "Menu";
        rematchButton.onClick.AddListener(() =>
        {
            AudioManager.Confirm();
            match.Rematch();
        });
    }

    void Update()
    {
        if (match == null) return;
        GameSim sim = match.Sim;
        if (sim == null || sim.fighters[0] == null || sim.fighters[1] == null) return;

        // health
        if (leftBar != null) leftBar.fillAmount = Mathf.Clamp01(sim.fighters[0].hp / (float)sim.fighters[0].MaxHp);
        if (rightBar != null) rightBar.fillAmount = Mathf.Clamp01(sim.fighters[1].hp / (float)sim.fighters[1].MaxHp);

        // names
        SetText(leftName, match.NameOf(0));
        SetText(rightName, match.NameOf(1));
        SetText(leftCharacter, CharacterLine(sim.fighters[0]));
        SetText(rightCharacter, CharacterLine(sim.fighters[1]));

        // info
        string info = match.IsOnline
            ? "Online - you are P" + (match.LocalSlot + 1) + (match.SupportsRooms ? "   room " + match.RoomCode : "")
            : "Offline vs AI";
        ShowDebug(infoText, info);
        ShowDebug(controlsText, FightControls.Hint());
        ShowDebug(tickText, "tick " + sim.tick + "   " + sim.fighters[match.LocalSlot].state);
        SetText(stalledText, match.Stalled ? "waiting for opponent input..." : "");
        SetText(warningText, match.Warning);

        // exit (vs AI only, while the fight is on)
        bool canExit = !match.IsOnline && !sim.IsOver;
        if (exitButton != null && exitButton.gameObject.activeSelf != canExit) exitButton.gameObject.SetActive(canExit);
        if (canExit && UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            match.Leave();
            return;
        }

        // result
        bool over = sim.IsOver;
        if (resultPanel != null && resultPanel.activeSelf != over) resultPanel.SetActive(over);
        if (over)
        {
            int winner = sim.Winner;
            string outcome = MatchDialogue.Result(sim, match.LocalSlot, true);
            SetText(resultText, winner == 2 ? outcome : outcome + "\n" + match.NameOf(winner) +
                "\nwins " + sim.roundsWon[winner] + "-" + sim.roundsWon[1 - winner]);
            if (rematchButton != null)
            {
                rematchButton.interactable = !match.RematchRequested;
                SetText(rematchLabel, match.RematchRequested ? "Waiting..." : match.OpponentWantsRematch ? "Rematch!" : "Rematch");
            }
        }

        UpdateWorldNames(sim);
        UpdateMeters(sim);
        UpdateRoundPips(sim);
        UpdateTimer(sim);
        UpdateBanner(sim);
        UpdateSuperFlash(sim);
    }

    void UpdateMeters(GameSim sim)
    {
        float pulse = Mathf.PingPong(Time.unscaledTime * 3f, 1f);
        Image[] meters = { leftMeter, rightMeter };
        TMP_Text[] ready = { leftUltimateReady, rightUltimateReady };
        for (int i = 0; i < 2; i++)
        {
            Fighter f = sim.fighters[i];
            bool full = f.meter >= Fighter.MeterMax;
            if (meters[i] != null)
            {
                meters[i].fillAmount = Mathf.Clamp01(f.meter / (float)Fighter.MeterMax);
                meters[i].color = full ? Color.Lerp(meterColor, meterFullColor, pulse) : meterColor;
            }
            if (ready[i] != null)
            {
                if (!ready[i].gameObject.activeSelf) ready[i].gameObject.SetActive(true);
                string text = !full ? "ULTIMATE" : i == match.LocalSlot ? "ULTIMATE READY - press " + FightControls.BindingOf(7) + " to ultimate" : "ULTIMATE READY";
                SetText(ready[i], text);
            }
        }
    }

    void UpdateRoundPips(GameSim sim)
    {
        Image[][] pips = { leftRoundPips, rightRoundPips };
        for (int i = 0; i < 2; i++)
        {
            if (pips[i] == null) continue;
            for (int r = 0; r < pips[i].Length; r++)
            {
                if (pips[i][r] != null) pips[i][r].color = r < sim.roundsWon[i] ? pipWonColor : pipEmptyColor;
            }
        }
    }

    // Worked out from the sim's round number rather than from events, because this screen is only
    // switched on once the match is already running and could miss the first one.
    void UpdateBanner(GameSim sim)
    {
        if (bannerText == null) return;
        if (sim.round != shownRound)
        {
            shownRound = sim.round;
            bannerRound = MatchDialogue.Round(sim);
            bannerUntil = Time.unscaledTime + bannerSeconds;
        }

        string text = "";
        if (sim.IsOver) text = sim.timeUp ? "TIME!" : "K.O.";
        else if (sim.roundOverTicks > 0)
            text = sim.hitStopTicks > 0 || sim.roundOverTicks > 90 ? (sim.timeUp ? "TIME!" : "K.O.") : MatchDialogue.Result(sim, match.LocalSlot, false);
        else if (MatchDialogue.Start(sim) != "") text = MatchDialogue.Start(sim);
        else if (Time.unscaledTime < bannerUntil) text = bannerRound;
        // Keep the final K.O. above the result panel, clear of the winner and buttons.
        RectTransform bannerRect = bannerText.rectTransform;
        bannerRect.anchoredPosition = bannerHomePosition;
        if (sim.IsOver && resultPanel != null)
        {
            RectTransform panelRect = resultPanel.transform as RectTransform;
            if (panelRect != null)
            {
                Vector3 panelTop = panelRect.TransformPoint(new Vector3(panelRect.rect.center.x, panelRect.rect.yMax, 0f));
                Vector3 localTop = bannerRect.parent.InverseTransformPoint(panelTop);
                Vector3 position = bannerRect.localPosition;
                position.y = localTop.y + bannerRect.rect.height * bannerRect.pivot.y + 20f;
                bannerRect.localPosition = position;
            }
        }
        bool show = text != "";
        if (bannerText.gameObject.activeSelf != show) bannerText.gameObject.SetActive(show);
        SetText(bannerText, text);
    }

    // A white flash that fades out over the Ultimate's freeze, and whose Ultimate it is. Driven by
    // the sim's freeze counter, so it lasts exactly as long as the freeze.
    void UpdateSuperFlash(GameSim sim)
    {
        bool frozen = sim.freezeTicks > 0;
        if (superFlash != null)
        {
            if (superFlash.enabled != frozen) superFlash.enabled = frozen;
            Color c = superFlash.color;
            c.a = frozen ? superFlashMaxAlpha * sim.freezeTicks / GameSim.SuperFlashTicks : 0f;
            superFlash.color = c;
        }
        if (superFlashText == null) return;

        string who = "";
        for (int i = 0; i < 2 && frozen; i++)
        {
            Fighter f = sim.fighters[i];
            if (f.move != null && f.move.ultimate && f.state == FighterStateId.Attack) who = match.NameOf(i);
        }
        bool show = who != "";
        if (superFlashText.gameObject.activeSelf != show) superFlashText.gameObject.SetActive(show);
        if (show) SetText(superFlashText, who + ": ULTIMATE!");
    }

    // The character's name, then its resources and status effects (ammo, potion, SLOW...).
    static string CharacterLine(Fighter f)
    {
        string status = FighterStatusText.For(f);
        string name = CharacterList.NameOf(f.characterId);
        return status.Length > 0 ? name + "   " + status : name;
    }

    // Debug lines: hidden when Match Screens > Show Debug UI is off.
    static void ShowDebug(TMP_Text text, string value)
    {
        if (text == null) return;
        if (text.gameObject.activeSelf != MatchScreens.DebugUI) text.gameObject.SetActive(MatchScreens.DebugUI);
        if (MatchScreens.DebugUI) SetText(text, value);
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null && text.text != value) text.text = value;
    }

    void UpdateWorldNames(GameSim sim)
    {
        Camera cam = Camera.main;
        if (cam == null || worldNameLayer == null) return;

        Canvas canvas = worldNameLayer.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        for (int i = 0; i < 2 && i < worldNames.Length; i++)
        {
            TMP_Text label = worldNames[i];
            if (label == null) continue;

            Box body = sim.fighters[i].HurtboxWorld();
            Vector3 world = new Vector3((body.x + body.w / 2f) * WorldView.Scale, (body.y + body.h) * WorldView.Scale + nameHeightAboveHead, 0f);
            Vector3 screen = cam.WorldToScreenPoint(world);

            bool visible = screen.z >= 0f;
            if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
            if (!visible) continue;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(worldNameLayer, screen, uiCamera, out local);
            label.rectTransform.anchoredPosition = local;
            label.text = match.NameOf(i);
            label.color = i == match.LocalSlot ? localNameColor : otherNameColor;
        }
    }
}
