using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The fight HUD: health bars, player and character names, info lines, the result panel, and the
// player names that float above the fighters. Talks only to IMatchSession.
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

    [Header("Names floating above the fighters")]
    public RectTransform worldNameLayer;        // stretched over the whole screen, pivot in the middle
    public TMP_Text[] worldNames = new TMP_Text[2];
    public float nameHeightAboveHead = 0.35f;   // Unity units above the top of the fighter's body
    public Color localNameColor = new Color(1f, 0.92f, 0.4f);
    public Color otherNameColor = Color.white;

    IMatchSession match;
    bool wired;

    void OnEnable()
    {
        match = SessionFinder.Find();
        if (match == null) return;
        if (!wired && resultButton != null)
        {
            resultButton.onClick.AddListener(() =>
            {
                AudioManager.Click();
                match.Leave();
            });
            wired = true;
        }
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
        SetText(leftCharacter, CharacterList.NameOf(sim.fighters[0].characterId));
        SetText(rightCharacter, CharacterList.NameOf(sim.fighters[1].characterId));

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
            SetText(resultText, winner == 2 ? "Draw" : match.NameOf(winner) + " wins");
        }

        UpdateWorldNames(sim);
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
