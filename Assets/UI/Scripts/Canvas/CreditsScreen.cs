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

    [Header("Auto scroll")]
    [Tooltip("How fast the names roll up, in pixels per second. 0 = no auto scroll.")]
    public float scrollSpeed = 40f;
    [Tooltip("Seconds to wait at the top before rolling, and at the bottom before jumping back to the top.")]
    public float pauseAtEnds = 2f;
    [Tooltip("Seconds after the player scrolls by hand before the auto scroll carries on.")]
    public float resumeAfter = 3f;

    MatchScreens screens;
    bool wired;
    ScrollRect scroll;
    float waitLeft;
    float lastPos;   // where the auto scroll left the list last frame, to notice the player scrolling

    void OnEnable()
    {
        screens = GetComponentInParent<MatchScreens>();
        if (namesText != null)
        {
            namesText.text = BuildList();
            scroll = namesText.GetComponentInParent<ScrollRect>();
        }
        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();   // size the list for the new text before scrolling it
            scroll.verticalNormalizedPosition = 1f;   // 1 = the top
            lastPos = 1f;
            waitLeft = pauseAtEnds;
        }
        if (screens == null || wired || backButton == null) return;
        backButton.onClick.AddListener(() => { AudioManager.Click(); screens.GoBack(); });
        wired = true;
    }

    void Update()
    {
        if (scroll == null || scrollSpeed <= 0f) return;

        // How many pixels of names don't fit in the box. Nothing to roll if they all fit.
        float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
        if (overflow <= 0f) return;

        float pos = scroll.verticalNormalizedPosition;
        if (Mathf.Abs(pos - lastPos) > 0.0001f) waitLeft = resumeAfter;   // the player moved it

        // Unscaled time, so it still rolls if the game is paused or slowed.
        if (waitLeft > 0f)
        {
            waitLeft -= Time.unscaledDeltaTime;
        }
        else if (pos <= 0f)
        {
            pos = 1f;   // reached the bottom and waited: back to the top
            waitLeft = pauseAtEnds;
        }
        else
        {
            pos = Mathf.Max(0f, pos - scrollSpeed * Time.unscaledDeltaTime / overflow);
            if (pos <= 0f) waitLeft = pauseAtEnds;   // hold on the last names before jumping back
        }

        scroll.verticalNormalizedPosition = pos;
        lastPos = scroll.verticalNormalizedPosition;
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
            int dash = line.LastIndexOf(" - ", StringComparison.Ordinal);   // the roles are after the last " - "
            string name = dash > 0 ? line.Substring(0, dash).Trim() : line;
            string[] roles = dash > 0 ? line.Substring(dash + 3).Split(',') : new string[] { "Team" };
            for (int r = 0; r < roles.Length; r++)   // one entry per role, so they show up under each one
            {
                Person p;
                p.name = name;
                p.role = roles[r].Trim();
                if (p.role.Length > 0) people.Add(p);
            }
        }
        if (people.Count == 0) return "Made for Ludum Dare 60";

        people.Sort((a, b) =>
        {
            int r = string.Compare(a.role, b.role, StringComparison.OrdinalIgnoreCase);
            return r != 0 ? r : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        });

        // A bold heading for each role, its people under it, and a gap before the next role.
        StringBuilder sb = new StringBuilder();
        string lastRole = null;
        for (int i = 0; i < people.Count; i++)
        {
            if (!string.Equals(lastRole, people[i].role, StringComparison.OrdinalIgnoreCase))
            {
                if (lastRole != null) sb.Append('\n');
                sb.Append("<b>").Append(people[i].role).Append("</b>\n");
                lastRole = people[i].role;
            }
            sb.Append("    ").Append(people[i].name).Append('\n');
        }
        return sb.ToString().TrimEnd();
    }
}
