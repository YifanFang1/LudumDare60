using UnityEngine;

// Player display names: cleaning (so they are safe to send as text) and remembering your own.
public static class PlayerNames
{
    public const int MaxLength = 12;
    const string PrefKey = "playerName";

    // Keeps letters, digits, spaces and a few symbols; never a comma (the network messages are comma separated).
    public static string Clean(string raw)
    {
        if (raw == null) return "";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < raw.Length && sb.Length < MaxLength; i++)
        {
            char c = raw[i];
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '.') sb.Append(c);
        }
        return sb.ToString();   // not trimmed: the lobby text field calls this on every keystroke, and "Mary Ann" needs its space
    }

    public static string Load()
    {
        string saved = "";
        try { saved = PlayerPrefs.GetString(PrefKey, ""); } catch (System.Exception) { }
        saved = Clean(saved).Trim();
        return saved.Length > 0 ? saved : "Player";
    }

    public static void Save(string name)
    {
        try { PlayerPrefs.SetString(PrefKey, Clean(name).Trim()); PlayerPrefs.Save(); } catch (System.Exception) { }
    }
}
