// A short line describing a fighter's resources and status effects, for the HUDs
// ("Ammo 4/6  SLOW  BLEED x3"). COSMETIC: it only reads the sim. Shared by HudScreen (Canvas) and
// FightView (OnGUI) so both show the same thing.
public static class FighterStatusText
{
    public static string For(Fighter f)
    {
        if (f == null) return "";
        string s = f.HudInfo ?? "";
        if (f.slowTicks > 0 && !f.IgnoresStatus(StatusKind.Slow)) s = Add(s, "SLOW");
        if (f.bleedStacks > 0) s = Add(s, f.HasBleedWeakness ? "BLEED x" + f.bleedStacks + " (weak)" : "BLEED x" + f.bleedStacks);
        if (f.scrambleTicks > 0) s = Add(s, "SCRAMBLED");
        if (f.leechTicks > 0) s = Add(s, "LEECHES");
        if (f.state == FighterStateId.Held) s = Add(s, "HELD");
        return s;
    }

    static string Add(string line, string part)
    {
        return line.Length == 0 ? part : line + "  " + part;
    }
}
