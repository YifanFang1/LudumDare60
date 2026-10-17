using System;

public enum MatchPhase { Lobby, Waiting, Playing }

// THE ONE DOOR between "a match is happening" and everything that wants to look at it.
//
// FightView, LobbyUI, ControlsUI and anything (later also juice and audio) you add later talk ONLY to this.
// They never know whether the match is offline, over a relay, over FishNet, or anything else.
// To change or remove networking, you swap the component that implements this interface and
// nothing else in the project has to change.
//
// This interface must stay network-neutral: no sockets, no relay words, no "ping" types.
public interface IMatchSession
{
    // ----- read -----
    MatchPhase Phase { get; }
    GameSim Sim { get; }
    int LocalSlot { get; }          // which fighter this machine controls (0 or 1)
    bool IsOnline { get; }
    bool Stalled { get; }           // waiting for the other player's input
    string Warning { get; }         // "" normally; text to show if something is wrong (e.g. desync)
    string Status { get; }          // last message for the lobby ("Room is full", ...)

    // Character ids (see CharacterList). For Play vs AI: yours and the AI's.
    // Online matches do not use these yet (character ids are not exchanged over the network).
    int LocalCharacter { get; set; }
    int OpponentCharacter { get; set; }

    // Optional online extras. When SupportsRooms is false the lobby simply hides them.
    bool SupportsRooms { get; }
    string RoomCode { get; }
    string ServerAddress { get; set; }
    bool PublicRoom { get; set; }   // rooms you create: public ones can be found by Quick match, private ones need the code

    // ----- subscribe -----
    event Action<GameSim> MatchStarted;      // a new match just began (sim is freshly reset)
    event Action<SimEvent> SimEventRaised;   // something happened in a tick (attack, hit, KO, ...)
    event Action MatchEnded;                 // back to the lobby

    // ----- commands -----
    void PlayVsAI();
    void QuickMatch();              // join whoever is waiting, or wait to be joined (needs SupportsRooms)
    void CreateRoom();
    void JoinRoom(string code);
    void Leave();
}
