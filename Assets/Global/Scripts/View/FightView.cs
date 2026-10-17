using UnityEngine;

// The 2D HUD: health bars, names, info text and the result screen. Drawn with OnGUI.
// The fighters themselves are drawn by WorldView (real GameObjects, visible in the Scene view).
// Talks only to IMatchSession.
public class FightView : MonoBehaviour
{
    IMatchSession match;

    void Awake()
    {
        match = GetComponent<IMatchSession>();
        if (match == null)
        {
            Debug.LogError("FightView needs a component that implements IMatchSession (LocalMatchSession or RelayLockstepSession) on the same object.");
            enabled = false;
        }
    }

    void OnGUI()
    {
        if (match.Phase != MatchPhase.Playing) return;

        float width, height;
        GuiDraw.BeginVirtualScreen(out width, out height);

        GameSim sim = match.Sim;
        DrawWorldNames(sim);
        DrawHealthBars(sim, width);
        DrawInfo(sim, width);
        DrawResult(sim, width);
    }

    void DrawHealthBars(GameSim sim, float width)
    {
        GuiDraw.Fill(20, 20, 300, 20, new Color(0.2f, 0.2f, 0.2f));
        GuiDraw.Fill(width - 320, 20, 300, 20, new Color(0.2f, 0.2f, 0.2f));

        for (int i = 0; i < 2; i++)
        {
            Fighter f = sim.fighters[i];
            float hpWidth = 300f * Mathf.Max(0, f.hp) / f.MaxHp;
            if (i == 0) GuiDraw.Fill(20, 20, hpWidth, 20, Color.green);
            else GuiDraw.Fill(width - 20 - hpWidth, 20, hpWidth, 20, Color.green);
        }

        // Under each health bar: the player's name, then the character they picked.
        GUIStyle left = Style(TextAnchor.UpperLeft, 18, FontStyle.Bold);
        GUIStyle right = Style(TextAnchor.UpperRight, 18, FontStyle.Bold);
        GUIStyle leftSmall = Style(TextAnchor.UpperLeft, 13, FontStyle.Normal);
        GUIStyle rightSmall = Style(TextAnchor.UpperRight, 13, FontStyle.Normal);
        Shadowed(new Rect(20, 42, 300, 24), match.NameOf(0), left);
        Shadowed(new Rect(width - 320, 42, 300, 24), match.NameOf(1), right);
        Shadowed(new Rect(20, 64, 300, 20), CharacterList.NameOf(sim.fighters[0].characterId), leftSmall);
        Shadowed(new Rect(width - 320, 64, 300, 20), CharacterList.NameOf(sim.fighters[1].characterId), rightSmall);
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

    void DrawInfo(GameSim sim, float width)
    {
        string info = match.IsOnline
            ? "Online - you are P" + (match.LocalSlot + 1) + (match.SupportsRooms ? "   room " + match.RoomCode : "")
            : "Offline vs AI";
        GUI.Label(new Rect(width / 2f - 150, 50, 300, 24), info);
        GUI.Label(new Rect(width / 2f - 150, 70, 300, 24), FightControls.Hint());
        GUI.Label(new Rect(width / 2f - 150, 90, 300, 24), "tick " + sim.tick + "   " + sim.fighters[match.LocalSlot].state);

        if (match.Stalled) GUI.Label(new Rect(width / 2f - 150, 110, 300, 24), "waiting for opponent input...");
        if (!string.IsNullOrEmpty(match.Warning)) GUI.Label(new Rect(width / 2f - 150, 130, 300, 24), match.Warning);
    }

    void DrawResult(GameSim sim, float width)
    {
        if (!sim.IsOver) return;

        int winner = sim.Winner;
        string result = winner == 2 ? "Draw" : match.NameOf(winner) + " wins";
        GUI.Label(new Rect(width / 2f - 50, 200, 200, 30), result);
        if (GUI.Button(new Rect(width / 2f - 60, 230, 120, 30), "Back to lobby"))
        {
            AudioManager.Click();
            match.Leave();
        }
    }
}
