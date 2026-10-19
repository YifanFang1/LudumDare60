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
    readonly List<GameObject> volumeRows = new List<GameObject>();

    static readonly Color SliderTrack = new Color(0f, 0f, 0f, 0.45f);
    static readonly Color SliderFill = new Color(0.55f, 0.85f, 0.45f, 1f);   // the scrollbar's green

    // A volume row: made from the same row prefab as the key bindings (so it matches their look),
    // with the key button swapped for a slider built in code.
    void AddVolumeRow(AudioManager.VolumeChannel channel, string name)
    {
        ControlRowUI row = Instantiate(rowPrefab, rowParent);
        TMP_Text label = row.label;
        if (row.bindButton != null) row.bindButton.gameObject.SetActive(false);

        Slider slider = NewSlider(row.transform);
        RectTransform r = (RectTransform)slider.transform;
        r.anchorMin = new Vector2(0.5f, 0f);
        r.anchorMax = new Vector2(1f, 1f);
        r.offsetMin = new Vector2(12f, 0f);
        r.offsetMax = new Vector2(-12f, 0f);

        slider.value = AudioManager.Player(channel);
        SetVolumeLabel(label, name, slider.value);
        slider.onValueChanged.AddListener(v =>
        {
            AudioManager.SetPlayer(channel, v);
            SetVolumeLabel(label, name, v);
        });
        Destroy(row);   // the ControlRowUI script: this row isn't a key binding
        volumeRows.Add(row.gameObject);
    }

    static void SetVolumeLabel(TMP_Text label, string name, float value)
    {
        if (label != null) label.text = name + "  " + Mathf.RoundToInt(value * 100f) + "%";
    }

    // A plain horizontal slider: dark track, green fill, white handle.
    static Slider NewSlider(Transform parent)
    {
        GameObject root = new GameObject("VolumeSlider", typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);

        Image track = NewImage("Track", root.transform, SliderTrack);
        Band(track.rectTransform);

        RectTransform fillArea = NewRect("Fill Area", root.transform);
        Band(fillArea);
        Image fill = NewImage("Fill", fillArea, SliderFill);
        Stretch(fill.rectTransform);   // the slider only moves the right edge, so it needs full height

        RectTransform handleArea = NewRect("Handle Slide Area", root.transform);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(10f, 8f);
        handleArea.offsetMax = new Vector2(-10f, -8f);
        Image handle = NewImage("Handle", handleArea, Color.white);
        Stretch(handle.rectTransform);
        handle.rectTransform.sizeDelta = new Vector2(20f, 0f);   // the slider moves it along x

        Slider slider = root.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        return slider;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    // A thin band across the middle of the row.
    static void Band(RectTransform r)
    {
        r.anchorMin = new Vector2(0f, 0.38f);
        r.anchorMax = new Vector2(1f, 0.62f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

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

    void OnDisable()
    {
        PlayerPrefs.Save();   // the volume sliders, saved once when the page closes (not on every drag)
    }

    void BuildRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null) Destroy(rows[i].gameObject);
        }
        rows.Clear();
        for (int i = 0; i < volumeRows.Count; i++)
        {
            if (volumeRows[i] != null) Destroy(volumeRows[i]);
        }
        volumeRows.Clear();
        if (rowParent == null || rowPrefab == null) return;

        // Volume sliders first, then the key bindings.
        AddVolumeRow(AudioManager.VolumeChannel.Master, "Master");
        AddVolumeRow(AudioManager.VolumeChannel.Music, "Music");
        AddVolumeRow(AudioManager.VolumeChannel.Effects, "Effects");
        AddVolumeRow(AudioManager.VolumeChannel.Voices, "Voices");

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
