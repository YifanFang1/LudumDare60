using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A letterboxed, typewritten dialogue overlay. Intro controls stay synchronized by GameSim.
public class CharacterDialogueScreen : MonoBehaviour
{
    CanvasGroup group;
    TMP_Text speaker, dialogue;
    MatchCinematics cinematic;
    string shown = "";
    BloodyDialogueText speakerBlood, dialogueBlood;

    void Awake()
    {
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = group.blocksRaycasts = false;
        HudScreen hud = GetComponentInParent<HudScreen>();
        TMP_FontAsset font = hud != null && hud.resultText != null ? hud.resultText.font : TMP_Settings.defaultFontAsset;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) gameObject.layer = canvas.gameObject.layer;

        Panel("SceneShade", transform, Vector2.zero, Vector2.one, new Color(0.025f, 0.005f, 0.015f, 0.18f));
        Panel("TopLetterbox", transform, new Vector2(0, 0.92f), Vector2.one, new Color(0.006f, 0.004f, 0.008f, 1f));
        RectTransform box = Panel("DialogueBox", transform, Vector2.zero, new Vector2(1, 0.29f), new Color(0.018f, 0.008f, 0.014f, 0.99f));
        Panel("UpperBorder", box, new Vector2(0, 0.987f), Vector2.one, new Color(0.28f, 0.055f, 0.075f));
        Panel("InnerPanel", box, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.9f), new Color(0.035f, 0.018f, 0.026f));
        Panel("Accent", box, new Vector2(0.05f, 0.16f), new Vector2(0.053f, 0.84f), new Color(0.65f, 0.12f, 0.17f));
        Panel("NameRule", box, new Vector2(0.075f, 0.635f), new Vector2(0.35f, 0.641f), new Color(0.3f, 0.09f, 0.12f));
        speaker = Label("Speaker", box, font, new Vector2(0.075f, 0.65f), new Vector2(0.925f, 0.92f), 40);
        speaker.fontStyle = FontStyles.Bold;
        speaker.color = new Color(0.94f, 0.48f, 0.48f);
        speaker.characterSpacing = 2.5f;
        dialogue = Label("Dialogue", box, font, new Vector2(0.075f, 0.12f), new Vector2(0.925f, 0.64f), 44);
        dialogue.enableAutoSizing = true;
        dialogue.fontSizeMin = 24;
        dialogue.fontSizeMax = 44;
        dialogue.richText = false;
        dialogue.color = new Color(0.9f, 0.87f, 0.84f);
        speakerBlood = speaker.gameObject.AddComponent<BloodyDialogueText>();
        dialogueBlood = dialogue.gameObject.AddComponent<BloodyDialogueText>();
    }

    void LateUpdate()
    {
        if (cinematic == null)
        {
            IMatchSession session = SessionFinder.Find();
            Component owner = session as Component;
            if (owner != null) cinematic = owner.GetComponent<MatchCinematics>();
        }
        bool active = cinematic != null && cinematic.Active;
        group.alpha = active ? 1f : 0f;
        bool executioner = active && cinematic.SpeakerCharacterId == 14;
        speakerBlood.SetBloody(executioner);
        dialogueBlood.SetBloody(executioner);
        if (!active) { shown = ""; return; }
        transform.SetAsLastSibling();
        speaker.text = cinematic.Speaker;
        string line = cinematic.Text;
        if (line != shown)
        {
            dialogue.text = line;
            shown = line;
        }
        // A short camera settle, then reveal the whole line well before its four-second beat ends.
        dialogue.maxVisibleCharacters = Mathf.Max(0, Mathf.FloorToInt((cinematic.LineElapsed - 0.2f) * 48f));
    }

    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        RectTransform rect = Rect(name, parent, min, max);
        UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }
    static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, Vector2 min, Vector2 max, float size)
    {
        RectTransform rect = Rect(name, parent, min, max);
        TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
