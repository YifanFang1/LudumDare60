using UnityEngine;

// The OnGUI HUD: health and meter bars, player and character names, round pips, banners, info
// text and the result screen. The fighters themselves are drawn by WorldView (real GameObjects,
// visible in the Scene view). Talks only to IMatchSession, and only READS the sim.
//
// When the Canvas UI (MatchScreens) is in the scene it turns this component off and HudScreen
// draws the HUD instead. This one stays as the fallback and for scenes without the canvas.
public class FightView : MonoBehaviour
{
    const float BarWidth = 300f;
    const float BannerSeconds = 1.5f;

    IMatchSession match;
    string banner = "";        // "ROUND 1", "FINAL ROUND"... shown for BannerSeconds
    float bannerUntil;
    GUIStyle bigText;

    void Awake()
    {
        match = GetComponent<IMatchSession>();
        if (match == null)
        {
            Debug.LogError("FightView needs a component that implements IMatchSession (LocalMatchSession or RelayLockstepSession) on the same object.");
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (match == null) return;
        match.MatchStarted += OnMatchStarted;
        match.SimEventRaised += OnSimEvent;
    }

    void OnDisable()
    {
        if (match == null) return;
        match.MatchStarted -= OnMatchStarted;
        match.SimEventRaised -= OnSimEvent;
    }

    void OnMatchStarted(GameSim sim)
    {
        ShowBanner(MatchDialogue.Round(sim));
    }

    void OnSimEvent(SimEvent e)
    {
        if (e.type != SimEventType.RoundStart) return;
        GameSim sim = match.Sim;
        ShowBanner(MatchDialogue.Round(sim));
    }

    void ShowBanner(string text)
    {
        banner = text;
        bannerUntil = Time.unscaledTime + BannerSeconds;
    }

    void OnGUI()
    {
        if (match.Phase != MatchPhase.Playing) return;

        float width, height;
        GuiDraw.BeginVirtualScreen(out width, out height);
        if (bigText == null)
        {
            bigText = new GUIStyle(GUI.skin.label);
            bigText.fontSize = 32;
            bigText.fontStyle = FontStyle.Bold;
            bigText.alignment = TextAnchor.MiddleCenter;
        }

        GameSim sim = match.Sim;
        DrawSuperFlash(sim, width, height);
        DrawWorldNames(sim);
        DrawBars(sim, width);
        DrawInfo(sim, width);
        DrawBanner(sim, width);
        DrawResult(sim, width);
    }

    void DrawBars(GameSim sim, float width)
    {
        for (int i = 0; i < 2; i++)
        {
            Fighter f = sim.fighters[i];
            float left = i == 0 ? 20f : width - 20f - BarWidth;

            // Health, draining toward the middle of the screen.
            GuiDraw.Fill(left, 20, BarWidth, 20, new Color(0.2f, 0.2f, 0.2f));
            float hpWidth = BarWidth * Mathf.Max(0, f.hp) / f.MaxHp;
            GuiDraw.Fill(i == 0 ? left : left + BarWidth - hpWidth, 20, hpWidth, 20, Color.green);

            // Ultimate meter under it. Pulses when full, because then O does something.
            GuiDraw.Fill(left, 44, BarWidth, 8, new Color(0.15f, 0.15f, 0.15f));
            float meterWidth = BarWidth * f.meter / Fighter.MeterMax;
            bool full = f.meter >= Fighter.MeterMax;
            Color meterColor = full ? Color.Lerp(Color.yellow, Color.white, Mathf.PingPong(Time.unscaledTime * 3f, 1f))
                                    : new Color(0.9f, 0.75f, 0.1f);
            GuiDraw.Fill(i == 0 ? left : left + BarWidth - meterWidth, 44, meterWidth, 8, meterColor);

            // One pip per round won, beside the health bar.
            for (int r = 0; r < GameSim.RoundsToWin; r++)
            {
                float pipX = i == 0 ? left + BarWidth + 6 + r * 14 : left - 18 - r * 14;
                Color pip = r < sim.roundsWon[i] ? Color.yellow : new Color(0.25f, 0.25f, 0.25f);
                GuiDraw.Fill(pipX, 24, 10, 10, pip);
            }
        }

        // Under each meter: the player's name, then the character they picked.
        GUIStyle left0 = Style(TextAnchor.UpperLeft, 18, FontStyle.Bold);
        GUIStyle right1 = Style(TextAnchor.UpperRight, 18, FontStyle.Bold);
        GUIStyle leftSmall = Style(TextAnchor.UpperLeft, 13, FontStyle.Normal);
        GUIStyle rightSmall = Style(TextAnchor.UpperRight, 13, FontStyle.Normal);
        Shadowed(new Rect(20, 56, BarWidth, 24), match.NameOf(0), left0);
        Shadowed(new Rect(width - 20 - BarWidth, 56, BarWidth, 24), match.NameOf(1), right1);
        Shadowed(new Rect(20, 80, BarWidth, 20), CharacterLine(sim.fighters[0]), leftSmall);
        Shadowed(new Rect(width - 20 - BarWidth, 80, BarWidth, 20), CharacterLine(sim.fighters[1]), rightSmall);
    }

    static string CharacterLine(Fighter f)
    {
        string status = FighterStatusText.For(f);
        return CharacterList.NameOf(f.characterId) + (f.meter >= Fighter.MeterMax ? "   ULTIMATE READY" : "")
             + (status.Length > 0 ? "   " + status : "");
    }

    // The player's name floating above their fighter, following it as it moves.
    void DrawWorldNames(GameSim sim)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float s = Screen.height / 540f;   // the virtual screen scale set by GuiDraw.BeginVirtualScreen
        GUIStyle style = Style(TextAnchor.MiddleCenter, 16, FontStyle.Bold);
        for (int i = 0; i < 2; i++)
        {
            Fighter f = sim.fighters[i];
            Box body = f.HurtboxWorld();
            Vector3 world = new Vector3((body.x + body.w / 2f) * WorldView.Scale, (body.y + body.h) * WorldView.Scale + 0.35f, 0f);
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f) continue;

            float x = screen.x / s;
            float y = (Screen.height - screen.y) / s;
            style.normal.textColor = i == match.LocalSlot ? new Color(1f, 0.92f, 0.4f) : Color.white;
            Shadowed(new Rect(x - 100, y - 12, 200, 24), match.NameOf(i), style);
        }
    }

    static GUIStyle Style(TextAnchor anchor, int size, FontStyle fontStyle)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = anchor;
        style.fontSize = size;
        style.fontStyle = fontStyle;
        return style;
    }

    // Text with a dark outline so it can be read over any background.
    static void Shadowed(Rect r, string text, GUIStyle style)
    {
        Color keep = style.normal.textColor;
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
        style.normal.textColor = keep;
        GUI.Label(r, text, style);
    }

    // While the sim is frozen for an Ultimate: a white flash that fades out over the freeze, and
    // whose Ultimate it is. Driven by the sim's freeze counter, so it lasts exactly as long.
    void DrawSuperFlash(GameSim sim, float width, float height)
    {
        if (sim.freezeTicks <= 0) return;

        float alpha = 0.45f * sim.freezeTicks / GameSim.SuperFlashTicks;
        GuiDraw.Fill(0, 0, width, height, new Color(1f, 1f, 1f, alpha));
        for (int i = 0; i < 2; i++)
        {
            Fighter f = sim.fighters[i];
            if (f.move != null && f.move.ultimate && f.state == FighterStateId.Attack)
            {
                GUI.Label(new Rect(0, height * 0.35f, width, 50), match.NameOf(i) + ": ULTIMATE!", bigText);
            }
        }
    }

    void DrawInfo(GameSim sim, float width)
    {
        string info = match.IsOnline
            ? "Online - you are P" + (match.LocalSlot + 1) + (match.SupportsRooms ? "   room " + match.RoomCode : "")
            : "Offline vs AI";
        GUI.Label(new Rect(width / 2f - 150, 100, 300, 24), info);
        GUI.Label(new Rect(width / 2f - 350, 120, 700, 24), FightControls.Hint());
        GUI.Label(new Rect(width / 2f - 150, 140, 300, 24), "tick " + sim.tick + "   " + sim.fighters[match.LocalSlot].state);

        if (match.Stalled) GUI.Label(new Rect(width / 2f - 150, 160, 300, 24), "waiting for opponent input...");
        if (!string.IsNullOrEmpty(match.Warning)) GUI.Label(new Rect(width / 2f - 150, 180, 300, 24), match.Warning);
    }

    void DrawBanner(GameSim sim, float width)
    {
        string text = null;
        if (sim.IsOver) text = sim.timeUp ? "TIME!" : "K.O.";
        else if (sim.roundOverTicks > 0)
            text = sim.hitStopTicks > 0 || sim.roundOverTicks > 90 ? (sim.timeUp ? "TIME!" : "K.O.") : MatchDialogue.Result(sim, match.LocalSlot, false);
        else if (MatchDialogue.Start(sim) != "") text = MatchDialogue.Start(sim);
        else if (Time.unscaledTime < bannerUntil) text = banner;
        if (text != null) GUI.Label(new Rect(0, 200, width, 100), text, bigText);
    }

    void DrawResult(GameSim sim, float width)
    {
        if (!sim.IsOver) return;

        int winner = sim.Winner;
        string result = MatchDialogue.Result(sim, match.LocalSlot, true) + (winner == 2 ? "" : " - " + match.NameOf(winner) + " wins " + sim.roundsWon[winner] + "-" + sim.roundsWon[1 - winner]);
        GUI.Label(new Rect(width / 2f - 150, 255, 300, 30), result);
        if (GUI.Button(new Rect(width / 2f - 60, 285, 120, 30), "Back to lobby"))
        {
            AudioManager.Click();
            match.Leave();
        }
    }
}
