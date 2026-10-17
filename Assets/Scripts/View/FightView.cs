using UnityEngine;

// The 2D HUD: health and meter bars, names, round pips, banners, info text and the result screen.
// Drawn with OnGUI. The fighters themselves are drawn by WorldView (real GameObjects, visible in
// the Scene view). Talks only to IMatchSession, and only READS the sim.
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
        ShowBanner("ROUND 1");
    }

    void OnSimEvent(SimEvent e)
    {
        if (e.type != SimEventType.RoundStart) return;
        GameSim sim = match.Sim;
        bool final = sim.roundsWon[0] == GameSim.RoundsToWin - 1 && sim.roundsWon[1] == GameSim.RoundsToWin - 1;
        ShowBanner(final ? "FINAL ROUND" : "ROUND " + e.amount);
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

            // Name, and one pip per round won.
            string name = CharacterList.NameOf(f.characterId) + (full ? "   ULTIMATE READY" : "");
            GUI.Label(new Rect(left, 54, BarWidth, 24), name);
            for (int r = 0; r < GameSim.RoundsToWin; r++)
            {
                float pipX = i == 0 ? left + BarWidth + 6 + r * 14 : left - 18 - r * 14;
                Color pip = r < sim.roundsWon[i] ? Color.yellow : new Color(0.25f, 0.25f, 0.25f);
                GuiDraw.Fill(pipX, 24, 10, 10, pip);
            }
        }
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
                GUI.Label(new Rect(0, height * 0.35f, width, 50), CharacterList.NameOf(f.characterId) + ": ULTIMATE!", bigText);
            }
        }
    }

    void DrawInfo(GameSim sim, float width)
    {
        string info = match.IsOnline
            ? "Online - you are P" + (match.LocalSlot + 1) + (match.SupportsRooms ? "   room " + match.RoomCode : "")
            : "Offline vs AI";
        GUI.Label(new Rect(width / 2f - 150, 70, 300, 24), info);
        GUI.Label(new Rect(width / 2f - 350, 90, 700, 24), FightControls.Hint());
        GUI.Label(new Rect(width / 2f - 150, 110, 300, 24), "tick " + sim.tick + "   " + sim.fighters[match.LocalSlot].state);

        if (match.Stalled) GUI.Label(new Rect(width / 2f - 150, 130, 300, 24), "waiting for opponent input...");
        if (!string.IsNullOrEmpty(match.Warning)) GUI.Label(new Rect(width / 2f - 150, 150, 300, 24), match.Warning);
    }

    void DrawBanner(GameSim sim, float width)
    {
        string text = null;
        if (sim.roundOverTicks > 0 || sim.IsOver) text = "K.O.";
        else if (Time.unscaledTime < bannerUntil) text = banner;
        if (text != null) GUI.Label(new Rect(0, 160, width, 50), text, bigText);
    }

    void DrawResult(GameSim sim, float width)
    {
        if (!sim.IsOver) return;

        int winner = sim.Winner;
        string result = winner == 2 ? "Draw" : "P" + (winner + 1) + " wins " + sim.roundsWon[winner] + "-" + sim.roundsWon[1 - winner];
        GUI.Label(new Rect(width / 2f - 100, 215, 200, 30), result);
        if (GUI.Button(new Rect(width / 2f - 60, 245, 120, 30), "Back to lobby"))
        {
            AudioManager.Click();
            match.Leave();
        }
    }
}
