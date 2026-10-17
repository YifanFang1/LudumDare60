using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The lobby: name, server, quick match, rooms, play vs AI. Every control is an object you place and
// style in the editor; this script only connects them to the match session.
// Anything you leave empty is simply skipped, so you can delete controls you do not want.
public class LobbyScreen : MonoBehaviour
{
    [Header("Inputs")]
    public TMP_InputField nameInput;
    public TMP_InputField serverInput;   // optional: leave empty to use the address set on the session (Release Relay Url in builds)
    public TMP_InputField codeInput;
    public Toggle publicToggle;

    [Header("Buttons")]
    public Button quickMatchButton;
    public Button createRoomButton;
    public Button joinRoomButton;
    public Button playVsAiButton;
    public Button backButton;           // back to the Online / Offline choice

    [Header("Text")]
    public TMP_Text publicLabel;
    public TMP_Text statusText;

    [Header("Hidden when the session has no online play")]
    public GameObject onlineGroup;

    IMatchSession match;
    bool wired;

    void OnEnable()
    {
        match = SessionFinder.Find();
        if (match == null) return;
        if (!wired) { Wire(); wired = true; }
        Pull();
    }

    void Wire()
    {
        if (nameInput != null)
        {
            nameInput.characterLimit = PlayerNames.MaxLength;
            nameInput.onValueChanged.AddListener(value =>
            {
                string clean = PlayerNames.Clean(value);
                if (clean != value) nameInput.SetTextWithoutNotify(clean);
                match.LocalName = clean;
            });
        }
        if (serverInput != null) serverInput.onValueChanged.AddListener(value => match.ServerAddress = value);
        if (codeInput != null)
        {
            codeInput.characterLimit = 4;
            codeInput.onValueChanged.AddListener(value =>
            {
                string upper = value.ToUpper();
                if (upper != value) codeInput.SetTextWithoutNotify(upper);
            });
        }
        if (publicToggle != null)
        {
            publicToggle.onValueChanged.AddListener(on =>
            {
                match.PublicRoom = on;
                RefreshPublicLabel();
            });
        }

        Hook(quickMatchButton, () => match.QuickMatch());
        Hook(createRoomButton, () =>
        {
            match.PublicRoom = false;   // rooms you create are private: friends join with the code
            match.CreateRoom();
        });
        Hook(joinRoomButton, () => match.JoinRoom(codeInput != null ? codeInput.text : ""));
        Hook(playVsAiButton, () => match.PlayVsAI());
        Hook(backButton, () =>
        {
            MatchScreens screens = GetComponentInParent<MatchScreens>();
            if (screens != null) screens.GoTo(MenuPage.Mode);
        });
    }

    // Plays the click sound, then runs the action. Extra OnClick events you add in the Inspector still work.
    void Hook(Button button, System.Action action)
    {
        if (button == null) return;
        button.onClick.AddListener(() =>
        {
            AudioManager.Click();
            action();
        });
    }

    // Copy the session's current values into the controls (without triggering their change events).
    void Pull()
    {
        if (nameInput != null) nameInput.SetTextWithoutNotify(match.LocalName);
        if (serverInput != null) serverInput.SetTextWithoutNotify(match.ServerAddress);
        if (publicToggle != null) publicToggle.SetIsOnWithoutNotify(match.PublicRoom);
        if (onlineGroup != null) onlineGroup.SetActive(match.SupportsRooms);
        RefreshPublicLabel();
    }

    void RefreshPublicLabel()
    {
        if (publicLabel != null)
        {
            publicLabel.text = match.PublicRoom ? "Public room (Quick match can find it)" : "Private room (code only)";
        }
    }

    void Update()
    {
        if (match != null && statusText != null) statusText.text = match.Status;
    }
}
