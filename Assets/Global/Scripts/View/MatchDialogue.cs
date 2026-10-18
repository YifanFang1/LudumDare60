// Shared on-screen announcer wording for Canvas and the fallback HUD. No audio.
public static class MatchDialogue
{
    public static string Round(GameSim sim)
    {
        bool final = sim.roundsWon[0] == GameSim.RoundsToWin - 1 &&
                     sim.roundsWon[1] == GameSim.RoundsToWin - 1;
        return final ? "FINAL ROUND!" : "ROUND " + sim.round + "!";
    }

    public static string Start(GameSim sim)
    {
        if (sim.countdownTicks <= 0) return sim.fightBannerTicks > 0 ? "FIGHT!" : "";
        string number = ((sim.countdownTicks + 59) / 60).ToString();
        if (sim.round == 1 && sim.countdownTicks > 120)
            return "MATCH START\n" + Round(sim) + "  " + number;
        return Round(sim) + "\n" + number;
    }

    public static string Result(GameSim sim, int localSlot, bool finalMatch)
    {
        int winner = finalMatch ? sim.Winner :
            sim.fighters[0].hp <= 0 && sim.fighters[1].hp <= 0 ? 2 :
            sim.fighters[0].hp > 0 ? 0 : 1;
        if (winner == 2) return "DRAW!";
        if (winner != localSlot) return "DEFEAT";
        Fighter fighter = sim.fighters[winner];
        return fighter.hp == fighter.MaxHp ? "PERFECT VICTORY!" : "VICTORY!";
    }
}
