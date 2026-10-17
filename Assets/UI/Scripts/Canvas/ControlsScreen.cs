using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The key bindings page. Click an action, press the new key (Esc cancels). Bindings are saved by
// FightControls. The rows fill themselves from FightControls, so a new button added there shows up here.
public class ControlsScreen : MonoBehaviour
{
    public RectTransform rowParent;      // a vertical layout that holds the rows
    public ControlRowUI rowPrefab;
    public Button resetButton;
    public Button backButton;
    public TMP_Text hintText;

    MatchScreens screens;
    bool wired;
    int waitingFor = -1;
    readonly List<ControlRowUI> rows = new List<ControlRowUI>();

    void OnEnable()
    {
        screens = GetComponentInParent<MatchScreens>();
        waitingFor = -1;
        if (!wired) { Wire(); wired = true; }
        BuildRows();
    }

    void Wire()
    {
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(() =>
            {
                if (FightControls.IsRebinding) return;
                AudioManager.Click();
                FightControls.ResetToDefaults();
            });
        }
        if (backButton != null)
        {
            backButton.onClick.AddListener(() =>
            {
                if (FightControls.IsRebinding || screens == null) return;
                AudioManager.Click();
                screens.GoBack();
            });
        }
    }

    void BuildRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null) Destroy(rows[i].gameObject);
        }
        rows.Clear();
        if (rowParent == null || rowPrefab == null) return;

        for (int i = 0; i < FightControls.Count; i++)
        {
            ControlRowUI row = Instantiate(rowPrefab, rowParent);
            row.Setup(i, FightControls.LabelOf(i), OnRowClicked);
            rows.Add(row);
        }
    }

    void OnRowClicked(int index)
    {
        if (FightControls.IsRebinding) return;
        AudioManager.Click();
        waitingFor = index;
        FightControls.StartRebind(index, () => waitingFor = -1);
    }

    void Update()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].SetBinding(waitingFor == i ? "press a key..." : FightControls.BindingOf(i));
        }
        if (hintText != null)
        {
            hintText.text = FightControls.IsRebinding ? "Press the new key (Esc cancels)" : "Click an action, then press a key";
        }
    }
}
