using System;

public enum MatchPhase { Lobby, Waiting, CharacterSelect, Playing }

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
    // Online, only LocalCharacter is yours to set; the opponent picks their own.
    int LocalCharacter { get; set; }
    int OpponentCharacter { get; set; }

    // Character select (Phase == CharacterSelect). Change LocalCharacter (and OpponentCharacter
    // when ChoosesOpponent) while browsing, then call ConfirmCharacter. The match starts once
    // every player has confirmed: right away vs AI, when both have picked online.
    bool ChoosesOpponent { get; }       // true vs AI: you also pick the AI's character
    bool CharacterConfirmed { get; }    // you have locked in your pick

    // Optional online extras. When SupportsRooms is false the lobby simply hides them.
    bool SupportsRooms { get; }
    string RoomCode { get; }
    string ServerAddress { get; set; }

    // ----- subscribe -----
    event Action<GameSim> MatchStarted;      // a new match just began (sim is freshly reset)
    event Action<SimEvent> SimEventRaised;   // something happened in a tick (attack, hit, KO, ...)
    event Action MatchEnded;                 // back to the lobby

    // ----- commands -----
    void PlayVsAI();                // goes to character select first
    void ConfirmCharacter();
    void CreateRoom();
    void JoinRoom(string code);
    void Leave();
}
