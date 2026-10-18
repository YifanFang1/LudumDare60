using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The credits page. The names come from Assets/UI/Resources/Credits.txt, one person per line as
// "Name - Role". They are listed sorted by role (then by name), with a gap between roles.
public class CreditsScreen : MonoBehaviour
{
    public Button backButton;
    public TMP_Text namesText;   // inside a scroll view, so a long list scrolls

    MatchScreens screens;
    bool wired;

    void OnEnable()
    {
        screens = GetComponentInParent<MatchScreens>();
        if (namesText != null) namesText.text = BuildList();
        if (screens == null || wired || backButton == null) return;
        backButton.onClick.AddListener(() => { AudioManager.Click(); screens.GoBack(); });
        wired = true;
    }

    struct Person
    {
        public string name, role;
    }

    static string BuildList()
    {
        TextAsset file = Resources.Load<TextAsset>("Credits");
        if (file == null) return "Made for Ludum Dare 60";

        List<Person> people = new List<Person>();
        string[] lines = file.text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            int dash = line.LastIndexOf(" - ", StringComparison.Ordinal);   // the role is after the last " - "
            Person p;
            if (dash > 0) { p.name = line.Substring(0, dash).Trim(); p.role = line.Substring(dash + 3).Trim(); }
            else { p.name = line; p.role = "Team"; }
            people.Add(p);
        }
        if (people.Count == 0) return "Made for Ludum Dare 60";

        people.Sort((a, b) =>
        {
            int r = string.Compare(a.role, b.role, StringComparison.OrdinalIgnoreCase);
            return r != 0 ? r : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        });

        StringBuilder sb = new StringBuilder();
        string lastRole = null;
        for (int i = 0; i < people.Count; i++)
        {
            if (lastRole != null && !string.Equals(lastRole, people[i].role, StringComparison.OrdinalIgnoreCase)) sb.Append('\n');
            sb.Append(people[i].name).Append(" - ").Append(people[i].role).Append('\n');
            lastRole = people[i].role;
        }
        return sb.ToString().TrimEnd();
    }
}
