using System.Collections.Generic;
using UnityEngine;

// The lobby, waiting and character select screens. Talks only to IMatchSession; has no networking code.
// Room and server controls are hidden when the session does not support rooms.
public class LobbyUI : MonoBehaviour
{
    const float Cell = 120;   // size of one character button on the select screen

    IMatchSession match;
    string codeInput = "";
    bool pickingOpponent;     // vs AI: the grid is choosing the AI's character
    Vector2 scroll;

    void Awake()
    {
        match = GetComponent<IMatchSession>();
        if (match == null)
        {
            Debug.LogError("LobbyUI needs a component that implements IMatchSession (LocalMatchSession or RelayLockstepSession) on the same object.");
            enabled = false;
        }
    }

    void OnGUI()
    {
        if (match.Phase == MatchPhase.Playing) return;

        float width, height;
        GuiDraw.BeginVirtualScreen(out width, out height);

        if (match.Phase == MatchPhase.Lobby) DrawLobby();
        else if (match.Phase == MatchPhase.CharacterSelect) DrawCharacterSelect(width, height);
        else DrawWaiting();
    }

    void DrawLobby()
    {
        GUILayout.BeginArea(new Rect(20, 20, 320, 500));
        GUILayout.Label("Your name");
        match.LocalName = GUILayout.TextField(match.LocalName, PlayerNames.MaxLength);
        GUILayout.Space(10);

        if (match.SupportsRooms)
        {
            GUILayout.Label("Server");
            match.ServerAddress = GUILayout.TextField(match.ServerAddress);

            if (GUILayout.Button("Quick match"))
            {
                match.QuickMatch();
            }

            match.PublicRoom = GUILayout.Toggle(match.PublicRoom, match.PublicRoom ? "Room is public (Quick match can find it)" : "Room is private (code only)");
            if (GUILayout.Button("Create room"))
            {
                AudioManager.Click();
                match.CreateRoom();
            }

            GUILayout.Space(10);
            GUILayout.Label("Room code");
            codeInput = GUILayout.TextField(codeInput, 4).ToUpper();
            if (GUILayout.Button("Join room"))
            {
                AudioManager.Click();
                match.JoinRoom(codeInput);
            }

            GUILayout.Space(10);
        }

        if (GUILayout.Button("Play vs AI"))
        {
            AudioManager.Click();
            pickingOpponent = false;
            match.PlayVsAI();
        }

        GUILayout.Space(10);
        GUILayout.Label(match.Status);
        GUILayout.EndArea();
    }

    // Vs AI you pick both characters (tabs switch between yours and the AI's). Online you pick
    // only your own, press Ready, and the fight starts when the opponent has picked too.
    void DrawCharacterSelect(float width, float height)
    {
        bool forOpponent = match.ChoosesOpponent && pickingOpponent;

        GUILayout.BeginArea(new Rect(20, 20, width - 40, height - 40));
        if (match.ChoosesOpponent)
        {
            GUILayout.Label("Choose characters");
            string[] tabs = {
                "You: " + CharacterList.NameOf(match.LocalCharacter),
                "Opponent: " + CharacterList.NameOf(match.OpponentCharacter)
            };
            bool wasPickingOpponent = pickingOpponent;
            pickingOpponent = GUILayout.Toolbar(pickingOpponent ? 1 : 0, tabs, GUILayout.Width(400)) == 1;
            if (pickingOpponent != wasPickingOpponent) AudioManager.Click();
        }
        else
        {
            GUILayout.Label("Choose your character");
        }

        GUILayout.Space(10);
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height - 160));
        GUI.enabled = !match.CharacterConfirmed;

        int current = forOpponent ? match.OpponentCharacter : match.LocalCharacter;
        int columns = Mathf.Max(1, (int)((width - 70) / (Cell + 8)));
        IList<int> ids = CharacterList.Ids;
        for (int i = 0; i < ids.Count; i++)
        {
            if (i % columns == 0) GUILayout.BeginHorizontal();

            int id = ids[i];
            if (DrawCharacterButton(id, id == current) && id != current)
            {
                AudioManager.Click();
                if (forOpponent) match.OpponentCharacter = id;
                else match.LocalCharacter = id;
            }

            if (i % columns == columns - 1 || i == ids.Count - 1) GUILayout.EndHorizontal();
        }

        GUI.enabled = true;
        GUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (!match.CharacterConfirmed && GUILayout.Button(match.ChoosesOpponent ? "Fight!" : "Ready", GUILayout.Width(120)))
        {
            AudioManager.Confirm();
            match.ConfirmCharacter();
        }
        if (GUILayout.Button(match.ChoosesOpponent ? "Back" : "Leave room", GUILayout.Width(120)))
        {
            AudioManager.Click();
            match.Leave();
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(match.Status);
        GUILayout.EndArea();
    }

    // One square button: the character's sprite and name, or a strip of its placeholder colour
    // when it has no sprite. Returns true while it is the selected one.
    static bool DrawCharacterButton(int id, bool selected)
    {
        CharacterData art = CharacterRegistry.Get(id);   // null for characters written in code
        Texture portrait = art != null && art.sprite != null ? art.sprite.texture : null;

        GUIContent content = new GUIContent(CharacterList.NameOf(id), portrait);
        bool on = GUILayout.Toggle(selected, content, GUI.skin.button, GUILayout.Width(Cell), GUILayout.Height(Cell));

        if (portrait == null && art != null && Event.current.type == EventType.Repaint)
        {
            Rect r = GUILayoutUtility.GetLastRect();
            GuiDraw.Fill(r.x + 8, r.y + 8, r.width - 16, 6, art.placeholderColor);
        }
        return on;
    }

    void DrawWaiting()
    {
        GUILayout.BeginArea(new Rect(20, 20, 320, 200));
        GUILayout.Label("Room code: " + match.RoomCode);
        GUILayout.Label("Give your opponent this code");
        GUILayout.Label(match.Status);
        if (GUILayout.Button("Cancel"))
        {
            AudioManager.Click();
            match.Leave();
        }
        GUILayout.EndArea();
    }
}
