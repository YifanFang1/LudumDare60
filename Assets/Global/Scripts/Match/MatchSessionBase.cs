using System;
using UnityEngine;

// Shared plumbing for every kind of match: owns the sim, the tick runner, and the events.
// Subclasses only decide WHERE the two players' inputs come from:
//
//   LocalMatchSession        keyboard + AI, no networking at all
//   RelayLockstepSession     keyboard + a remote player through the Cloudflare relay (Global/Scripts/Net/)
//
// A new networking method is a new subclass of this. Nothing outside Net/ should need to change.
[DisallowMultipleComponent]
public abstract class MatchSessionBase : MonoBehaviour, IMatchSession
{
    protected GameSim sim = new GameSim();
    protected LockstepRunner runner = new LockstepRunner();

    public MatchPhase Phase { get; protected set; }
    public GameSim Sim { get { return sim; } }
    public int LocalSlot { get; protected set; }
    public bool IsOnline { get; protected set; }
    public bool Stalled { get { return runner.Stalled; } }
    public string Warning { get; protected set; }
    public string Status { get; protected set; }
    public string RoomCode { get; protected set; }
    public bool IsQuickMatch { get; protected set; }

    string localName;
    string[] names = { "P1", "P2" };

    // Your name. Cleaned and saved so it is still there next time.
    public string LocalName
    {
        get { if (localName == null) localName = PlayerNames.Load(); return localName; }
        set { localName = PlayerNames.Clean(value); }
    }

    public string NameOf(int slot) { return names[slot]; }
    public string OpponentName { get; protected set; }
    public bool RematchRequested { get; protected set; }
    public bool OpponentWantsRematch { get; protected set; }

    // For subclasses: the opponent's name arrived (empty names are ignored).
    protected void SetName(int slot, string name)
    {
        string clean = PlayerNames.Clean(name).Trim();
        if (clean.Length > 0) names[slot] = clean;
    }

    public int LocalCharacter { get; set; }
    public int OpponentCharacter { get; set; }
    public bool ChoosesOpponent { get; protected set; }
    public bool CharacterConfirmed { get; protected set; }

    public virtual bool SupportsRooms { get { return false; } }
    public bool PublicRoom { get; set; }
    public virtual string ServerAddress { get { return ""; } set { } }

    public event Action<GameSim> MatchStarted;
    public event Action<SimEvent> SimEventRaised;
    public event Action MatchEnded;

    protected virtual void Awake()
    {
        // Lockstep needs both windows to keep running even when one is not focused.
        Application.runInBackground = true;
        sim.Reset(0, CharacterList.Factory(0), CharacterList.Factory(0));   // so Sim is never empty
        Phase = MatchPhase.Lobby;
        Warning = "";
        Status = "";
        RoomCode = "";
        OpponentName = "";
    }

    protected virtual void Update()
    {
        if (Phase == MatchPhase.Playing) runner.Update(Time.deltaTime);
    }

    protected virtual void OnDestroy()
    {
        Leave();
    }

    // ---------- commands ----------

    public virtual void PlayVsAI()
    {
        Leave();
        EnterCharacterSelect(true);
    }

    // Vs AI both picks are made on this machine, so confirming starts the match.
    public virtual void ConfirmCharacter()
    {
        if (Phase != MatchPhase.CharacterSelect || CharacterConfirmed) return;
        CharacterConfirmed = true;

        MatchSetup setup = new MatchSetup();
        setup.seed = 12345;
        setup.p0Character = LocalCharacter;
        setup.p1Character = OpponentCharacter;
        StartMatch(setup, new KeyboardInputSource(), new AIInputSource(sim, 1), 0, 0, false);
    }

    // Vs AI: straight back to character select. The online session overrides this to ask the other player first.
    public virtual void Rematch()
    {
        if (Phase != MatchPhase.Playing) return;
        BackToCharacterSelect(ChoosesOpponent);
    }

    // Subclasses that have no online mode just say so.
    public virtual void QuickMatch() { Status = "Online play is not available in this build"; }
    public virtual void CreateRoom() { Status = "Online play is not available in this build"; }
    public virtual void JoinRoom(string code) { Status = "Online play is not available in this build"; }

    public virtual void Leave()
    {
        StopMatch();
    }

    // ---------- for subclasses ----------

    protected void EnterCharacterSelect(bool choosesOpponent)
    {
        ChoosesOpponent = choosesOpponent;
        CharacterConfirmed = false;
        Status = "";
        Phase = MatchPhase.CharacterSelect;
    }

    // Ends the running match without leaving the room and opens character select again.
    protected void BackToCharacterSelect(bool choosesOpponent)
    {
        bool wasPlaying = Phase == MatchPhase.Playing;
        runner.Running = false;
        Warning = "";
        RematchRequested = false;
        OpponentWantsRematch = false;
        EnterCharacterSelect(choosesOpponent);
        if (wasPlaying && MatchEnded != null) MatchEnded();
    }

    // Build the fighters, reset the sim, and start ticking. `delay` is the input delay in ticks
    // (0 offline, a few ticks online).
    protected void StartMatch(MatchSetup setup, IInputSource p0, IInputSource p1, int delay, int localSlot, bool online)
    {
        LocalSlot = localSlot;
        IsOnline = online;
        string mine = LocalName.Trim();
        if (mine.Length == 0) mine = "Player";
        PlayerNames.Save(mine);
        names[localSlot] = mine;
        names[1 - localSlot] = online ? "P" + (2 - localSlot) : "AI";
        Warning = "";
        sim.countdownOn = true;
        sim.roundTimerOn = online;   // no timer vs AI: rounds last until a KO
        sim.Reset(setup.seed, CharacterList.Factory(setup.p0Character), CharacterList.Factory(setup.p1Character));

        runner.OnTick = OnTickDone;
        runner.Begin(sim, p0, p1, delay);
        Phase = MatchPhase.Playing;
        if (MatchStarted != null) MatchStarted(sim);
    }

    protected void StopMatch()
    {
        bool wasPlaying = Phase == MatchPhase.Playing;
        runner.Running = false;
        Phase = MatchPhase.Lobby;
        Warning = "";
        OpponentName = "";
        RematchRequested = false;
        OpponentWantsRematch = false;
        if (wasPlaying && MatchEnded != null) MatchEnded();
    }

    // Runs after every tick. Subclasses can add their own work (desync checks) after calling base.
    protected virtual void OnTickDone(int tick)
    {
        for (int i = 0; i < sim.events.Count; i++)
        {
            if (SimEventRaised != null) SimEventRaised(sim.events[i]);
        }
    }
}
