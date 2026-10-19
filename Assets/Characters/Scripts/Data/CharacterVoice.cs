using UnityEngine;

// One character's voice: spoken dialogue and short combat shouts. Lives on the character's art
// asset (CharacterLook.voice). Every slot is optional: an empty slot just stays quiet.
//
// Dialogue lines are matched by their TEXT. The intro exchanges are in MatchupDialogue.cs and the
// win/lose lines are in CharacterDialogue.cs: copy the line from there (or from the screen) into
// 'Line' and drag the recording into 'Clip'. Capitals, spaces and punctuation don't matter, so
// "Sure-you-can beat me?" also matches "sure you can beat me".
//
// The AudioManager plays all of this on a separate source per fighter, at normal pitch, and a new
// line from the same fighter cuts off the one before (so shouts never pile up).
[System.Serializable]
public class CharacterVoice
{
    [Tooltip("Spoken versions of this character's on-screen lines (intro exchanges, win and lose lines). 'Line' must match the text on screen.")]
    public VoiceLine[] dialogue;

    [Tooltip("Shout when a move starts. 'Move' is the move name: Light, Heavy, Grab, Special, Ultimate, JumpLight, JumpHeavy.")]
    public MoveSfx[] moves;
    [Tooltip("Getting hit. Picked at random.")]
    public AudioClip[] hurt;
    [Range(0f, 1f)]
    [Tooltip("Chance a hit plays a Hurt line, so a combo isn't a wall of grunts.")]
    public float hurtChance = 0.4f;
    [Tooltip("Getting knocked out.")]
    public AudioClip[] ko;
    [Tooltip("Winning a round (not the last one: the win line covers that).")]
    public AudioClip[] roundWin;

    // The clip for a line of on-screen text, or null.
    public AudioClip FindLine(string text)
    {
        if (dialogue == null || string.IsNullOrEmpty(text)) return null;
        string key = Simplify(text);
        for (int i = 0; i < dialogue.Length; i++)
        {
            if (dialogue[i] != null && dialogue[i].clip != null && Simplify(dialogue[i].line) == key)
                return dialogue[i].clip;
        }
        return null;
    }

    public AudioClip[] FindMove(string moveName)
    {
        if (moves == null || string.IsNullOrEmpty(moveName)) return null;
        for (int i = 0; i < moves.Length; i++)
        {
            if (moves[i] != null && moves[i].move == moveName && moves[i].clips != null && moves[i].clips.Length > 0)
                return moves[i].clips;
        }
        return null;
    }

    // Lower-case letters and digits only, so small typing differences still match.
    static string Simplify(string text)
    {
        if (text == null) return "";
        System.Text.StringBuilder b = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsLetterOrDigit(text[i])) b.Append(char.ToLowerInvariant(text[i]));
        }
        return b.ToString();
    }
}

// One spoken line: the text as it appears on screen, and its recording.
[System.Serializable]
public class VoiceLine
{
    [TextArea(1, 3)] public string line;
    public AudioClip clip;
}
