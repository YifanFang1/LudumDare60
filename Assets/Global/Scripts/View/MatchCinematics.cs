using UnityEngine;

// Intro timing comes from lockstep. The final-match outro is presentation only: the result is settled.
[DefaultExecutionOrder(-20)]
public class MatchCinematics : MonoBehaviour
{
    public const float LineSeconds = GameSim.IntroLineTicks / 60f;
    public const float OutroSeconds = LineSeconds * 2f;
    IMatchSession match;
    GameSim previousSim;
    int previousTick;
    float outroElapsed;

    public bool IntroActive { get { return Playing && match.Sim.introTicks > 0; } }
    public bool HasOutro { get { return Playing && match.Sim.IsOver && match.Sim.Winner < 2; } }
    public bool OutroActive { get { return HasOutro && outroElapsed < OutroSeconds; } }
    public bool Active { get { return IntroActive || OutroActive; } }
    public bool LoserLineFinished { get { return !HasOutro || outroElapsed >= LineSeconds; } }
    public bool ResultsReady { get { return !HasOutro || outroElapsed >= OutroSeconds + 0.6f; } }
    bool Playing { get { return match != null && match.Phase == MatchPhase.Playing; } }

    public int FocusSlot
    {
        get
        {
            if (IntroActive) return match.Sim.IntroSlot;
            if (OutroActive) return outroElapsed < LineSeconds ? 1 - match.Sim.Winner : match.Sim.Winner;
            return -1;
        }
    }
    public CharacterDialogueMoment Moment
    {
        get { return IntroActive ? CharacterDialogueMoment.Starting :
            outroElapsed < LineSeconds ? CharacterDialogueMoment.Losing : CharacterDialogueMoment.Winning; }
    }
    public float LineElapsed
    {
        get
        {
            if (IntroActive)
                return (GameSim.IntroLineTicks * (match.Sim.IntroSlot == 0 ? 2 : 1) - match.Sim.introTicks) / 60f;
            return outroElapsed < LineSeconds ? outroElapsed : outroElapsed - LineSeconds;
        }
    }
    public string Speaker { get { return FocusSlot < 0 ? "" : CharacterDialogue.Speaker(match.Sim.fighters[FocusSlot].characterId); } }
    public string Text
    {
        get
        {
            int slot = FocusSlot;
            return slot < 0 ? "" : CharacterDialogue.Line(match.Sim.fighters[slot].characterId, Moment,
                match.Sim.seed + slot + match.Sim.round);
        }
    }

    void Awake() { match = GetComponent<IMatchSession>(); }
    void Update()
    {
        if (!Playing) { previousSim = null; outroElapsed = 0f; previousTick = 0; return; }
        GameSim sim = match.Sim;
        // Sessions reuse GameSim for rematches, so a tick rewind also resets the cinematic.
        if (sim != previousSim || sim.tick < previousTick) outroElapsed = 0f;
        previousSim = sim;
        previousTick = sim.tick;
        if (HasOutro) outroElapsed += Time.unscaledDeltaTime;
        else outroElapsed = 0f;
    }
    void OnDisable() { previousSim = null; previousTick = 0; outroElapsed = 0f; }
}
