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

        GUI.Label(new Rect(20, 42, 300, 24), CharacterList.NameOf(sim.fighters[0].characterId));
        GUI.Label(new Rect(width - 320, 42, 300, 24), CharacterList.NameOf(sim.fighters[1].characterId));
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
        string result = winner == 2 ? "Draw" : "P" + (winner + 1) + " wins";
        GUI.Label(new Rect(width / 2f - 50, 200, 200, 30), result);
        if (GUI.Button(new Rect(width / 2f - 60, 230, 120, 30), "Back to lobby"))
        {
            match.Leave();
        }
    }
}
