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

    // Names shown above the fighters and under the health bars.
    string LocalName { get; set; }     // your own name (remembered between sessions)
    string NameOf(int slot);           // the name to show for fighter 0 or 1
    string OpponentName { get; }       // the other player's name once it is known online, "" before that and vs AI

    // Character ids (see CharacterList). For Play vs AI: yours and the AI's.
    // Online, only LocalCharacter is yours to set; the opponent picks their own.
    int LocalCharacter { get; set; }
    int OpponentCharacter { get; set; }

    // Character select (Phase == CharacterSelect). Change LocalCharacter (and OpponentCharacter
    // when ChoosesOpponent) while browsing, then call ConfirmCharacter. The match starts once
    // every player has confirmed: right away vs AI, when both have picked online.
    bool ChoosesOpponent { get; }       // true vs AI: you also pick the AI's character
    bool CharacterConfirmed { get; }    // you have locked in your pick

    // Rematch (after a match is over): back to character select with the same opponent.
    // Vs AI it happens at once; online both players have to ask for it.
    bool RematchRequested { get; }      // you asked and are waiting for the opponent
    bool OpponentWantsRematch { get; }  // the opponent asked

    // Optional online extras. When SupportsRooms is false the lobby simply hides them.
    bool SupportsRooms { get; }
    string RoomCode { get; }
    bool IsQuickMatch { get; }      // the room you are waiting in was found by Quick match, so there is no code to share
    string ServerAddress { get; set; }
    bool PublicRoom { get; set; }   // rooms you create: public ones can be found by Quick match, private ones need the code

    // ----- subscribe -----
    event Action<GameSim> MatchStarted;      // a new match just began (sim is freshly reset)
    event Action<SimEvent> SimEventRaised;   // something happened in a tick (attack, hit, KO, ...)
    event Action MatchEnded;                 // back to the lobby

    // ----- commands -----
    void PlayVsAI();
    void QuickMatch();              // join whoever is waiting, or wait to be joined (needs SupportsRooms)
    void ConfirmCharacter();
    void Rematch();
    void CreateRoom();
    void JoinRoom(string code);
    void Leave();
}
