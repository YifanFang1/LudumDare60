using System;
using UnityEngine;

// Shared plumbing for every kind of match: owns the sim, the tick runner, and the events.
// Subclasses only decide WHERE the two players' inputs come from:
//
//   LocalMatchSession        keyboard + AI, no networking at all
//   RelayLockstepSession     keyboard + a remote player through the Cloudflare relay (Net/)
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

    public int LocalCharacter { get; set; }
    public int OpponentCharacter { get; set; }

    public virtual bool SupportsRooms { get { return false; } }
    public virtual string ServerAddress { get { return ""; } set { } }

    public event Action<GameSim> MatchStarted;
    public event Action<SimEvent> SimEventRaised;
    public event Action MatchEnded;

    protected virtual void Awake()
    {
        // Lockstep needs both windows to keep running even when one is not focused.
        Application.runInBackground = true;
        sim.Reset(0, CharacterList.Create(0), CharacterList.Create(0));   // so Sim is never empty
        Phase = MatchPhase.Lobby;
        Warning = "";
        Status = "";
        RoomCode = "";
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
        MatchSetup setup = new MatchSetup();
        setup.seed = 12345;
        setup.p0Character = LocalCharacter;
        setup.p1Character = OpponentCharacter;
        StartMatch(setup, new KeyboardInputSource(), new AIInputSource(sim, 1), 0, 0, false);
    }

    // Subclasses that have no online mode just say so.
    public virtual void CreateRoom() { Status = "Online play is not available in this build"; }
    public virtual void JoinRoom(string code) { Status = "Online play is not available in this build"; }

    public virtual void Leave()
    {
        StopMatch();
    }

    // ---------- for subclasses ----------

    // Build the fighters, reset the sim, and start ticking. `delay` is the input delay in ticks
    // (0 offline, a few ticks online).
    protected void StartMatch(MatchSetup setup, IInputSource p0, IInputSource p1, int delay, int localSlot, bool online)
    {
        LocalSlot = localSlot;
        IsOnline = online;
        Warning = "";
        sim.Reset(setup.seed, CharacterList.Create(setup.p0Character), CharacterList.Create(setup.p1Character));

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
