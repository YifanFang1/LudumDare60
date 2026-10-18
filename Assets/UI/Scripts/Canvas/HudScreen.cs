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
    public Color localNameColor = new Color(1f, 0.92f, 0.4f);
    public Color otherNameColor = Color.white;

    [Header("Ultimate meters (Image with Type = Filled)")]
    public Image leftMeter;
    public Image rightMeter;
    public Color meterColor = new Color(0.9f, 0.75f, 0.1f);
    public Color meterFullColor = Color.white;   // a full meter pulses between the two colours
    public TMP_Text leftUltimateReady;           // shown only while that meter is full
    public TMP_Text rightUltimateReady;

    [Header("Rounds won: one Image per round (RoundsToWin), first round first")]
    public Image[] leftRoundPips;
    public Image[] rightRoundPips;
    public Color pipWonColor = new Color(1f, 0.85f, 0.2f);
    public Color pipEmptyColor = new Color(0.25f, 0.25f, 0.3f);

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

    void OnEnable()
    {
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
        if (!wired && resultButton != null)
        {
            MakeRematchButton();
            resultButton.onClick.AddListener(() =>
            {
                AudioManager.Click();
                match.Leave();
            });
            wired = true;
        }
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

        // result
        bool over = sim.IsOver;
        if (resultPanel != null && resultPanel.activeSelf != over) resultPanel.SetActive(over);
        if (over)
        {
            int winner = sim.Winner;
            SetText(resultText, winner == 2 ? "Draw" : match.NameOf(winner) + "\nwins " + sim.roundsWon[winner] + "-" + sim.roundsWon[1 - winner]);
            if (rematchButton != null)
            {
                rematchButton.interactable = !match.RematchRequested;
                SetText(rematchLabel, match.RematchRequested ? "Waiting..." : match.OpponentWantsRematch ? "Rematch!" : "Rematch");
            }
        }

        UpdateWorldNames(sim);
        UpdateMeters(sim);
        UpdateRoundPips(sim);
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
            if (ready[i] != null && ready[i].gameObject.activeSelf != full) ready[i].gameObject.SetActive(full);
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
            bool final = sim.roundsWon[0] == GameSim.RoundsToWin - 1 && sim.roundsWon[1] == GameSim.RoundsToWin - 1;
            bannerRound = final ? "FINAL ROUND" : "ROUND " + sim.round;
            bannerUntil = Time.unscaledTime + bannerSeconds;
        }

        string text = "";
        if (sim.roundOverTicks > 0 || sim.IsOver) text = "K.O.";
        else if (Time.unscaledTime < bannerUntil) text = bannerRound;
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
