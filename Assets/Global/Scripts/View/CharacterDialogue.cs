// Existing single-character ending lines. Pre-match exchanges live in MatchupDialogue.
// Character ids and fighter class names stay unchanged.
public enum CharacterDialogueMoment { Starting, Winning, Losing }

public static class CharacterDialogue
{
    public static string Speaker(int id)
    {
        if (id == 16) return "K3N";
        if (id == 11) return "0d3tte";
        return CharacterList.NameOf(id);
    }

    public static string Line(int id, CharacterDialogueMoment moment, int variant)
    {
        if (id == 10) // Ruy
        {
            switch (moment)
            {
                case CharacterDialogueMoment.Starting:
                    return Pick(variant, "You know, they call me a genie. It's 'cause I grant death wishes.",
                        "Sure-you-can beat me?");
                case CharacterDialogueMoment.Winning:
                    return Pick(variant, "Man, I'm bored. Can you at least try to fight back?",
                        "Get dest-Ruy-ed!", "That was a Ruy-ly good punch.");
                case CharacterDialogueMoment.Losing:
                    return Pick(variant, "It seems I have been thoroughly pun-nished.",
                        "You're being such a Kill-Ruy.", "Hey! That's a Ruy-crime!");
            }
        }
        if (id == 14) // Penance
        {
            switch (moment)
            {
                case CharacterDialogueMoment.Starting: return "Draw your weapon. Your judgment awaits.";
                case CharacterDialogueMoment.Winning: return "May you find forgiveness beyond this life.";
                case CharacterDialogueMoment.Losing: return Pick(variant, "My penance... is not yet complete...", "You wear my patience thin.");
            }
        }
        switch (id)
        {
            case 11: // 0d3tte / Paintress
                return Ending(moment, "A masterpiece. Try not to smudge it.", "No... this wasn't how I pictured it.");
            case 12: // Noir
                return Ending(moment, "Case closed. Nobody rides away forever.", "Guess this is where the trail goes cold.");
            case 13: // ROOM
                return Ending(moment, "I did warn you. There wasn't room for both of us.", "I'll remember this. Elephants always do.");
            case 15: // Dr. Remy
                return Ending(moment, "A successful procedure. A regrettable survival rate.", "Remy... fetch the emergency prescription.");
            case 16: // K3N / Nek
                return Ending(moment, "High score! Who's got the next coin?", "Not game over... just out of credits.");
        }
        return "To be implemented.";
    }

    // One of several lines for the same moment, chosen by the variant (the same on both machines).
    // The variant is scrambled so consecutive matches don't just step through the lines in order.
    static string Pick(int variant, params string[] lines)
    {
        uint h = (uint)variant * 2654435761u;
        return lines[(int)((h >> 8) % (uint)lines.Length)];
    }

    static string Ending(CharacterDialogueMoment moment, string winning, string losing)
    {
        if (moment == CharacterDialogueMoment.Winning) return winning;
        if (moment == CharacterDialogueMoment.Losing) return losing;
        return "To be implemented.";
    }
}
