// Only the supplied Starting / Winning / Losing dialogue. Character ids stay unchanged.
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
                case CharacterDialogueMoment.Losing: return "My penance... is not yet complete...";
            }
        }
        return "To be implemented.";
    }
}
