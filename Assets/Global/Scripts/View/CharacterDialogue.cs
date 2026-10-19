// Intro / victory / defeat text. Ruy and Penance use supplied lines; other roster lines are original.
// Character ids and fighter class names stay unchanged.
public enum CharacterDialogueMoment { Starting, Winning, Losing }

public static class CharacterDialogue
{
    public static string Speaker(int id)
    {
        if (id == 14) return "Penance"; // existing Executioner
        return CharacterList.NameOf(id);
    }

    public static string Line(int id, CharacterDialogueMoment moment, int variant)
    {
        bool alternate = (variant & 1) != 0;
        if (id == 10) // Ruy
        {
            switch (moment)
            {
                case CharacterDialogueMoment.Starting:
                    return alternate ? "Sure-you-can beat me?" :
                        "You know, they call me a genie. It's cause I grant death wishes.";
                case CharacterDialogueMoment.Winning:
                    return alternate ? "Get dest-Ruy-ed" :
                        "Man, I'm bored, can you at least try to fight back?";
                case CharacterDialogueMoment.Losing:
                    return alternate ? "You're being such a Kill-Ruy" :
                        "It seems I have been throoughly pun-nished";
            }
        }
        if (id == 14) // Penance
        {
            switch (moment)
            {
                case CharacterDialogueMoment.Starting: return "Draw your weapon. Your judgment awaits";
                case CharacterDialogueMoment.Winning: return "May you find forgiveness beyond this life";
                case CharacterDialogueMoment.Losing: return alternate ? "You wear my patience." : "My penance... is not yet complete...";
            }
        }
        switch (id)
        {
            case 11: // Paintress
                return Pick(moment,
                    "Hold still. This next piece needs a little red.",
                    "A masterpiece. Try not to smudge it.",
                    "No... this wasn't how I pictured it.");
            case 12: // Noir
                return Pick(moment,
                    "Every story ends. Yours ends at sundown.",
                    "Case closed. Nobody rides away forever.",
                    "Guess this is where the trail goes cold.");
            case 13: // Room
                return Pick(moment,
                    "We should address the elephant in the room. Me.",
                    "I did warn you. There wasn't room for both of us.",
                    "I'll remember this. Elephants always do.");
            case 15: // Dr. Remy
                return Pick(moment,
                    "Don't be afraid. The treatment is usually worse.",
                    "A successful procedure. A regrettable survival rate.",
                    "Remy... fetch the emergency prescription.");
            case 16: // Nek
                return Pick(moment,
                    "Stay close, Zeep Zorp. It's time to power up.",
                    "High score! Who's got the next coin?",
                    "Not game over... just out of credits.");
        }
        return "To be implemented.";
    }

    static string Pick(CharacterDialogueMoment moment, string starting, string winning, string losing)
    {
        switch (moment)
        {
            case CharacterDialogueMoment.Starting: return starting;
            case CharacterDialogueMoment.Winning: return winning;
            case CharacterDialogueMoment.Losing: return losing;
            default: return "To be implemented.";
        }
    }
}
