using UnityEngine;

// The lobby and waiting screens. Talks only to IMatchSession; has no networking code.
// Room and server controls are hidden when the session does not support rooms.
public class LobbyUI : MonoBehaviour
{
    IMatchSession match;
    string codeInput = "";

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
        else DrawWaiting();
    }

    void DrawLobby()
    {
        GUILayout.BeginArea(new Rect(20, 20, 320, 450));
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
                match.CreateRoom();
            }

            GUILayout.Space(10);
            GUILayout.Label("Room code");
            codeInput = GUILayout.TextField(codeInput, 4).ToUpper();
            if (GUILayout.Button("Join room"))
            {
                match.JoinRoom(codeInput);
            }

            GUILayout.Space(10);
        }

        GUILayout.Label("Play vs AI (click to change character)");
        if (GUILayout.Button("You: " + CharacterList.NameOf(match.LocalCharacter)))
        {
            match.LocalCharacter = NextCharacter(match.LocalCharacter);
        }
        if (GUILayout.Button("Opponent: " + CharacterList.NameOf(match.OpponentCharacter)))
        {
            match.OpponentCharacter = NextCharacter(match.OpponentCharacter);
        }
        if (GUILayout.Button("Play vs AI"))
        {
            match.PlayVsAI();
        }

        GUILayout.Space(10);
        GUILayout.Label(match.Status);
        GUILayout.EndArea();
    }

    static int NextCharacter(int current)
    {
        System.Collections.Generic.IList<int> ids = CharacterList.Ids;
        int index = ids.IndexOf(current);
        return ids[(index + 1) % ids.Count];
    }

    void DrawWaiting()
    {
        GUILayout.BeginArea(new Rect(20, 20, 320, 200));
        GUILayout.Label("Room code: " + match.RoomCode);
        GUILayout.Label("Give your opponent this code");
        GUILayout.Label(match.Status);
        if (GUILayout.Button("Cancel"))
        {
            match.Leave();
        }
        GUILayout.EndArea();
    }
}
