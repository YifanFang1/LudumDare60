using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public enum UiScreen { Lobby, Waiting, CharacterSelect, Hud, Main, Controls, Mode, Title, Credits }

// The pages you can navigate between while no match is running.
// Play = the online lobby (quick match and rooms). Mode = the Online / Offline choice that comes after Play.
public enum MenuPage { Main, Controls, Mode, Play, Title, Credits }

// The conductor of the canvas UI: shows the one screen that matches the match phase and hides the rest.
// Each screen is an ordinary GameObject you can restyle in the Unity editor (see Assets/UI/CANVAS_UI.md).
// Build the default set with the menu  Fight > Rebuild Default Canvas.
[DisallowMultipleComponent]
public class MatchScreens : MonoBehaviour
{
    [Header("Screens (each one is a normal GameObject you can edit)")]
    public GameObject lobby;
    public GameObject waiting;
    public GameObject characterSelect;
    public GameObject hud;
    public GameObject mainMenu;
    public GameObject controls;
    public GameObject modeSelect;
    public GameObject titleScreen;   // "press any key" start screen
    public GameObject credits;
    [Tooltip("The sliding metal door played between screens. Empty = screens switch instantly.")]
    public DoorTransition door;

    [Header("Options")]
    [Tooltip("Start on the Play / Controls / Quit screen. Off: start straight in the lobby.")]
    public bool startOnMainMenu = true;

    [Tooltip("Open on the arcade-style 'press any key' screen (needs a Title Screen assigned above).")]
    public bool startOnTitle = true;

    [Tooltip("Turn off the old OnGUI lobby and HUD (LobbyUI, FightView) while this canvas is in the scene.")]
    public bool replaceImguiUI = true;

    [Header("Debug")]
    [Tooltip("Off: hides the debug UI (tick / controls / info lines, hurtbox and hitbox overlays and their checkboxes). Fighter placeholder boxes stay visible.")]
    public bool showDebugUI = true;

    // Read by the HUD and WorldView so one checkbox here controls every debug overlay.
    public static bool DebugUI = true;

    [Tooltip("Which screen to show in the editor while the game is not running, so you can design each one.")]
    public UiScreen editorPreview = UiScreen.Lobby;

    IMatchSession match;
    MenuPage page;
    MenuPage previousPage;
    MenuPage pageBeforeMatch = MenuPage.Mode;
    MatchPhase lastPhase = MatchPhase.Lobby;
    MatchPhase shownPhase;     // what is on screen right now: it follows phase/page only once the door is closed
    MenuPage shownPage;
    bool shownReady;

    // Go to another page (Main, Controls, Play = the lobby). Used by the buttons on the screens.
    public void GoTo(MenuPage next)
    {
        if (next == page) return;
        previousPage = page;
        page = next;
    }

    public void GoBack()
    {
        GoTo(previousPage == page ? MenuPage.Main : previousPage);
    }

    [Tooltip("Render the UI through an overlay camera so the CRT filter covers it too.")]
    public bool crtOverUI = true;

    void Awake()
    {
        DebugUI = showDebugUI;
        if (crtOverUI && GetComponent<UiUnderCrt>() == null && GetComponent<Canvas>() != null)
            gameObject.AddComponent<UiUnderCrt>();
        match = SessionFinder.Find();
        if (match == null)
        {
            Debug.LogError("MatchScreens: no match session found. Put LocalMatchSession or RelayLockstepSession on a GameObject in the scene.");
            enabled = false;
            return;
        }

        page = startOnTitle && titleScreen != null ? MenuPage.Title : (startOnMainMenu ? MenuPage.Main : MenuPage.Mode);
        previousPage = MenuPage.Main;

        if (replaceImguiUI)
        {
            DisableImguiScreens();
        }
        EnsureEventSystem();
    }

    void Update()
    {
        DebugUI = showDebugUI;   // so ticking the box while playing takes effect at once
        MatchPhase phase = match.Phase;
        // Leaving the menus for a room or match remembers the page; backing out of it lands on that same page again.
        if (phase != MatchPhase.Lobby && lastPhase == MatchPhase.Lobby) pageBeforeMatch = page;
        if (phase == MatchPhase.Lobby && lastPhase != MatchPhase.Lobby) page = pageBeforeMatch;
        lastPhase = phase;

        // The door covers every change of screen: the new screen only appears once it is closed.
        if (!shownReady || door == null)
        {
            shownPhase = phase;
            shownPage = page;
            shownReady = true;
        }
        else if (!door.Busy && (shownPhase != phase || shownPage != page))
        {
            door.Run(() => { shownPhase = match.Phase; shownPage = page; ApplyScreens(); });
        }
        ApplyScreens();
    }

    void ApplyScreens()
    {
        bool inMenus = shownPhase == MatchPhase.Lobby;
        Show(mainMenu, inMenus && shownPage == MenuPage.Main);
        Show(titleScreen, inMenus && shownPage == MenuPage.Title);
        Show(controls, inMenus && shownPage == MenuPage.Controls);
        Show(modeSelect, inMenus && shownPage == MenuPage.Mode);
        Show(lobby, inMenus && shownPage == MenuPage.Play);
        Show(credits, inMenus && shownPage == MenuPage.Credits);
        Show(waiting, shownPhase == MatchPhase.Waiting);
        Show(characterSelect, shownPhase == MatchPhase.CharacterSelect);
        Show(hud, shownPhase == MatchPhase.Playing);
    }

    static void Show(GameObject screen, bool on)
    {
        if (screen != null && screen.activeSelf != on) screen.SetActive(on);
    }

    void DisableImguiScreens()
    {
        LobbyUI oldLobby = SessionFinder.FindInScene<LobbyUI>();
        if (oldLobby != null) oldLobby.enabled = false;
        FightView oldHud = SessionFinder.FindInScene<FightView>();
        if (oldHud != null) oldHud.enabled = false;
        if (controls != null)   // the canvas has its own controls page
        {
            ControlsUI oldControls = SessionFinder.FindInScene<ControlsUI>();
            if (oldControls != null) oldControls.enabled = false;
        }
    }

    // Buttons need an EventSystem. The project uses the new Input System, so it needs that module.
    static void EnsureEventSystem()
    {
        if (SessionFinder.FindInScene<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

#if UNITY_EDITOR
    // Pick a preview screen in the Inspector and it is the only one visible while you design.
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            Show(lobby, editorPreview == UiScreen.Lobby);
            Show(waiting, editorPreview == UiScreen.Waiting);
            Show(characterSelect, editorPreview == UiScreen.CharacterSelect);
            Show(hud, editorPreview == UiScreen.Hud);
            Show(mainMenu, editorPreview == UiScreen.Main);
            Show(controls, editorPreview == UiScreen.Controls);
            Show(titleScreen, editorPreview == UiScreen.Title);
            Show(modeSelect, editorPreview == UiScreen.Mode);
            Show(credits, editorPreview == UiScreen.Credits);
        };
    }
#endif
}
