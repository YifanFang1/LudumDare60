using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Fight > Build Default UI Canvas
// Creates a ready-to-use canvas UI in the open scene: lobby, waiting, character select and HUD.
// Everything it makes is a normal GameObject (Button, TextMeshPro text, Image, ...). Restyle, move,
// re-colour, swap sprites and fonts, or delete what you do not want. The scripts on each screen only
// need the references already filled in on them.
public static class BuildFightCanvas
{
    const string RootName = "FightUI";
    const string PrefabFolder = "Assets/UI/Prefabs";
    const string ButtonPrefabPath = PrefabFolder + "/CharacterButton.prefab";
    const string RowPrefabPath = PrefabFolder + "/ControlRow.prefab";

    static readonly Color Dim = new Color(0.05f, 0.07f, 0.12f, 0.88f);
    static readonly Color PanelColor = new Color(0.12f, 0.16f, 0.26f, 0.96f);
    static readonly Color ButtonColor = new Color(0.27f, 0.36f, 0.58f, 1f);
    static readonly Color FieldColor = new Color(0.07f, 0.09f, 0.16f, 1f);
    static readonly Color Muted = new Color(0.7f, 0.74f, 0.85f, 1f);

    [MenuItem("Fight/Build Default UI Canvas")]
    static void Build()
    {
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog("TextMesh Pro resources needed",
                "Run  Window > TextMeshPro > Import TMP Essential Resources  once, then run this menu item again.", "OK");
            return;
        }

        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Replace the UI?", "'" + RootName + "' already exists in this scene. Replace it? Your edits to it are lost.", "Replace", "Cancel")) return;
            Undo.DestroyObjectImmediate(existing);
        }

        CharacterButtonUI buttonPrefab = BuildCharacterButtonPrefab();

        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Build Fight UI");
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject mainMenu = BuildMainMenu(root.transform);
        GameObject controlsPage = BuildControls(root.transform, BuildControlRowPrefab());
        GameObject modePage = BuildModeSelect(root.transform);
        GameObject lobby = BuildLobby(root.transform);
        GameObject waiting = BuildWaiting(root.transform);
        GameObject select = BuildCharacterSelect(root.transform, buttonPrefab);
        GameObject hud = BuildHud(root.transform);

        MatchScreens screens = root.AddComponent<MatchScreens>();
        screens.mainMenu = mainMenu;
        screens.controls = controlsPage;
        screens.modeSelect = modePage;
        screens.lobby = lobby;
        screens.waiting = waiting;
        screens.characterSelect = select;
        screens.hud = hud;
        screens.editorPreview = UiScreen.Main;
        Show(mainMenu, true);
        Show(controlsPage, false);
        Show(modePage, false);
        Show(lobby, false);
        Show(waiting, false);
        Show(select, false);
        Show(hud, false);

        EnsureEventSystem();

        if (SessionFinder.Find() == null)
        {
            Debug.LogWarning("Fight UI built, but no match session is in this scene yet. Add LocalMatchSession or RelayLockstepSession to a GameObject.");
        }

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Fight UI built. Pick the screen to design with  Match Screens > Editor Preview  on '" + RootName + "'. See Assets/UI/CANVAS_UI.md.");
    }

    // Fight > Add Start Menu And Controls Pages
    // For a FightUI you already built and edited: adds only the two new pages and wires them in,
    // without touching anything else.
    [MenuItem("Fight/Add Start Menu And Controls Pages")]
    static void AddPages()
    {
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog("TextMesh Pro resources needed",
                "Run  Window > TextMeshPro > Import TMP Essential Resources  once, then run this menu item again.", "OK");
            return;
        }

        GameObject root = GameObject.Find(RootName);
        MatchScreens screens = root != null ? root.GetComponent<MatchScreens>() : null;
        if (screens == null)
        {
            EditorUtility.DisplayDialog("No Fight UI found", "Run  Fight > Build Default UI Canvas  first.", "OK");
            return;
        }
        if (screens.mainMenu != null && screens.controls != null && screens.modeSelect != null)
        {
            EditorUtility.DisplayDialog("Already there", "This UI already has the start menu, mode and controls pages.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Add menu pages");
        if (screens.mainMenu == null)
        {
            screens.mainMenu = BuildMainMenu(root.transform);
            screens.mainMenu.transform.SetAsFirstSibling();
        }
        if (screens.controls == null) screens.controls = BuildControls(root.transform, BuildControlRowPrefab());
        if (screens.modeSelect == null)
        {
            screens.modeSelect = BuildModeSelect(root.transform);
        }

        // a Back button on the lobby, if it has none
        if (screens.lobby != null)
        {
            LobbyScreen ls = screens.lobby.GetComponent<LobbyScreen>();
            if (ls != null && ls.backButton == null)
            {
                Transform panel = screens.lobby.transform.Find("Panel");
                if (panel != null)
                {
                    TMP_Text unused;
                    ls.backButton = Btn(panel, "BackButton", "Back", 60f, out unused);
                }
            }
        }

        screens.editorPreview = UiScreen.Main;
        Show(screens.mainMenu, true);
        Show(screens.controls, false);
        Show(screens.modeSelect, false);
        if (screens.lobby != null) Show(screens.lobby, false);
        if (screens.waiting != null) Show(screens.waiting, false);
        if (screens.characterSelect != null) Show(screens.characterSelect, false);
        if (screens.hud != null) Show(screens.hud, false);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Menu pages added to '" + RootName + "'.");
    }

    // ================= screens =================

    static GameObject BuildMainMenu(Transform parent)
    {
        GameObject screen = NewScreen("MainMenuScreen", parent, true);
        RectTransform panel = NewPanel("Panel", screen.transform, 520f);

        Txt(panel, "Title", "FIGHT", 96, TextAlignmentOptions.Center, Color.white, 130f);
        TMP_Text unused;
        Button play = Btn(panel, "PlayButton", "Play", 72f, out unused);
        Button controls = Btn(panel, "ControlsButton", "Controls", 72f, out unused);
        Button quit = Btn(panel, "QuitButton", "Quit", 72f, out unused);

        MainMenuScreen ms = screen.AddComponent<MainMenuScreen>();
        ms.playButton = play;
        ms.controlsButton = controls;
        ms.quitButton = quit;
        return screen;
    }

    static GameObject BuildModeSelect(Transform parent)
    {
        GameObject screen = NewScreen("ModeSelectScreen", parent, true);
        RectTransform panel = NewPanel("Panel", screen.transform, 560f);

        Txt(panel, "Title", "Choose mode", 56, TextAlignmentOptions.Center, Color.white, 80f);
        Txt(panel, "NameLabel", "Your name", 22, TextAlignmentOptions.MidlineLeft, Muted, 28f);
        TMP_InputField nameInput = InputBox(panel, "NameInput", "Enter a name", 52f);
        TMP_Text unused;
        Button online = Btn(panel, "OnlineButton", "Online", 72f, out unused);
        Button offline = Btn(panel, "OfflineButton", "Offline (vs AI)", 72f, out unused);
        Button back = Btn(panel, "BackButton", "Back", 60f, out unused);

        ModeSelectScreen ms = screen.AddComponent<ModeSelectScreen>();
        ms.nameInput = nameInput;
        ms.onlineButton = online;
        ms.offlineButton = offline;
        ms.backButton = back;
        return screen;
    }

    static GameObject BuildControls(Transform parent, ControlRowUI rowPrefab)
    {
        GameObject screen = NewScreen("ControlsScreen", parent, true);
        RectTransform panel = NewPanel("Panel", screen.transform, 760f);

        Txt(panel, "Title", "Controls", 56, TextAlignmentOptions.Center, Color.white, 80f);
        TMP_Text hint = Txt(panel, "HintText", "Click an action, then press a key", 24, TextAlignmentOptions.Center, Muted, 36f);

        GameObject rows = NewGroup("Rows", panel);
        TMP_Text unused;
        Button reset = Btn(panel, "ResetButton", "Reset to defaults", 60f, out unused);
        Button back = Btn(panel, "BackButton", "Back", 60f, out unused);

        ControlsScreen cs = screen.AddComponent<ControlsScreen>();
        cs.rowParent = (RectTransform)rows.transform;
        cs.rowPrefab = rowPrefab;
        cs.resetButton = reset;
        cs.backButton = back;
        cs.hintText = hint;
        return screen;
    }

    static GameObject BuildLobby(Transform parent)
    {
        GameObject screen = NewScreen("LobbyScreen", parent, true);
        RectTransform panel = NewPanel("Panel", screen.transform, 600f);

        Txt(panel, "Title", "ONLINE", 64, TextAlignmentOptions.Center, Color.white, 80f);

        GameObject online = NewGroup("OnlineGroup", panel);
        TMP_Text unused;
        Button quick = Btn(online.transform, "QuickMatchButton", "Quick match", 60f, out unused);
        TMP_Text publicLabel;
        Toggle publicToggle = MakeToggle(online.transform, "PublicToggle", "Private room (code only)", out publicLabel);
        Button create = Btn(online.transform, "CreateRoomButton", "Create room", 60f, out unused);
        Txt(online.transform, "CodeLabel", "Room code", 22, TextAlignmentOptions.MidlineLeft, Muted, 28f);
        TMP_InputField codeInput = InputBox(online.transform, "CodeInput", "ABCD", 52f);
        Button join = Btn(online.transform, "JoinRoomButton", "Join room", 60f, out unused);

        TMP_Text status = Txt(panel, "StatusText", "", 22, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f), 34f);
        Button back = Btn(panel, "BackButton", "Back", 60f, out unused);

        LobbyScreen ls = screen.AddComponent<LobbyScreen>();
        ls.codeInput = codeInput;
        ls.publicToggle = publicToggle;
        ls.publicLabel = publicLabel;
        ls.quickMatchButton = quick;
        ls.createRoomButton = create;
        ls.joinRoomButton = join;
        ls.backButton = back;
        ls.statusText = status;
        ls.onlineGroup = online;
        return screen;
    }

    static GameObject BuildWaiting(Transform parent)
    {
        GameObject screen = NewScreen("WaitingScreen", parent, true);
        RectTransform panel = NewPanel("Panel", screen.transform, 600f);

        Txt(panel, "Heading", "Room code", 28, TextAlignmentOptions.Center, Muted, 40f);
        TMP_Text code = Txt(panel, "RoomCodeText", "ABCD", 110, TextAlignmentOptions.Center, Color.white, 130f);
        TMP_Text hint = Txt(panel, "HintText", "Give your opponent this code", 24, TextAlignmentOptions.Center, Muted, 36f);
        TMP_Text status = Txt(panel, "StatusText", "", 22, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f), 34f);
        TMP_Text unused;
        Button cancel = Btn(panel, "CancelButton", "Cancel", 60f, out unused);

        WaitingScreen ws = screen.AddComponent<WaitingScreen>();
        ws.roomCodeText = code;
        ws.hintText = hint;
        ws.statusText = status;
        ws.cancelButton = cancel;
        return screen;
    }

    static GameObject BuildCharacterSelect(Transform parent, CharacterButtonUI buttonPrefab)
    {
        GameObject screen = NewScreen("CharacterSelectScreen", parent, true);

        TMP_Text title = FreeText(screen.transform, "Title", "Choose your character", 48, TextAlignmentOptions.Center, Color.white);
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(1000f, 70f));

        // tabs (vs AI)
        GameObject tabs = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabs.transform.SetParent(screen.transform, false);
        Anchor((RectTransform)tabs.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -135f), new Vector2(900f, 60f));
        HorizontalLayoutGroup tabLayout = tabs.GetComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 16f;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;
        TMP_Text youLabel;
        TMP_Text oppLabel;
        Button youTab = Btn(tabs.transform, "YouTab", "You", 60f, out youLabel);
        Button oppTab = Btn(tabs.transform, "OpponentTab", "Opponent", 60f, out oppLabel);

        // scrolling grid of characters
        GameObject scrollGo = new GameObject("Characters", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(screen.transform, false);
        RectTransform scrollRt = (RectTransform)scrollGo.transform;
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(80f, 170f);
        scrollRt.offsetMax = new Vector2(-80f, -190f);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollGo.transform, false);
        Stretch((RectTransform)viewport.transform);

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRt = (RectTransform)content.transform;
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;
        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(170f, 200f);
        grid.spacing = new Vector2(14f, 14f);
        grid.padding = new RectOffset(14, 14, 14, 14);
        grid.childAlignment = TextAnchor.UpperCenter;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = contentRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        // bottom row
        GameObject row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(screen.transform, false);
        Anchor((RectTransform)row.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 95f), new Vector2(560f, 64f));
        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 20f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;
        TMP_Text confirmLabel;
        TMP_Text backLabel;
        Button confirm = Btn(row.transform, "ConfirmButton", "Ready", 64f, out confirmLabel);
        Button back = Btn(row.transform, "BackButton", "Back", 64f, out backLabel);

        TMP_Text status = FreeText(screen.transform, "StatusText", "", 24, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f));
        Anchor(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1000f, 40f));

        CharacterSelectScreen cs = screen.AddComponent<CharacterSelectScreen>();
        cs.gridParent = contentRt;
        cs.buttonPrefab = buttonPrefab;
        cs.tabsGroup = tabs;
        cs.youTab = youTab;
        cs.opponentTab = oppTab;
        cs.youTabLabel = youLabel;
        cs.opponentTabLabel = oppLabel;
        cs.confirmButton = confirm;
        cs.confirmLabel = confirmLabel;
        cs.backButton = back;
        cs.backLabel = backLabel;
        cs.titleText = title;
        cs.statusText = status;
        return screen;
    }

    static GameObject BuildHud(Transform parent)
    {
        GameObject screen = NewScreen("Hud", parent, false);

        // health bars: left grows from the left edge, right from the right edge
        Image leftBar = HealthBar(screen.transform, "LeftHealth", false);
        Image rightBar = HealthBar(screen.transform, "RightHealth", true);

        TMP_Text leftName = FreeText(screen.transform, "LeftName", "P1", 34, TextAlignmentOptions.TopLeft, Color.white);
        Anchor(leftName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -84f), new Vector2(600f, 44f));
        TMP_Text rightName = FreeText(screen.transform, "RightName", "P2", 34, TextAlignmentOptions.TopRight, Color.white);
        Anchor(rightName.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -84f), new Vector2(600f, 44f));
        TMP_Text leftChar = FreeText(screen.transform, "LeftCharacter", "", 24, TextAlignmentOptions.TopLeft, Muted);
        Anchor(leftChar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -128f), new Vector2(600f, 34f));
        TMP_Text rightChar = FreeText(screen.transform, "RightCharacter", "", 24, TextAlignmentOptions.TopRight, Muted);
        Anchor(rightChar.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -128f), new Vector2(600f, 34f));

        TMP_Text info = TopCenterText(screen.transform, "InfoText", 26, Color.white, -30f);
        TMP_Text controls = TopCenterText(screen.transform, "ControlsText", 22, Muted, -62f);
        TMP_Text tick = TopCenterText(screen.transform, "TickText", 22, Muted, -90f);
        TMP_Text stalled = TopCenterText(screen.transform, "StalledText", 26, new Color(1f, 0.85f, 0.4f), -130f);
        TMP_Text warning = TopCenterText(screen.transform, "WarningText", 26, new Color(1f, 0.4f, 0.4f), -162f);

        // names that follow the fighters
        GameObject layerGo = new GameObject("WorldNames", typeof(RectTransform));
        layerGo.transform.SetParent(screen.transform, false);
        RectTransform layer = (RectTransform)layerGo.transform;
        Stretch(layer);
        TMP_Text[] names = new TMP_Text[2];
        for (int i = 0; i < 2; i++)
        {
            names[i] = FreeText(layer, "FighterName" + i, "P" + (i + 1), 34, TextAlignmentOptions.Center, Color.white);
            names[i].fontStyle = FontStyles.Bold;
            Anchor(names[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 50f));
        }

        // result
        GameObject resultGo = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
        resultGo.transform.SetParent(screen.transform, false);
        Anchor((RectTransform)resultGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 320f));
        resultGo.GetComponent<Image>().color = PanelColor;
        TMP_Text resultText = FreeText(resultGo.transform, "ResultText", "", 64, TextAlignmentOptions.Center, Color.white);
        Anchor(resultText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(600f, 100f));
        TMP_Text unused;
        Button back = Btn(resultGo.transform, "BackToLobbyButton", "Back to lobby", 64f, out unused);
        Anchor((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(320f, 64f));
        resultGo.SetActive(false);

        HudScreen hud = screen.AddComponent<HudScreen>();
        hud.leftBar = leftBar;
        hud.rightBar = rightBar;
        hud.leftName = leftName;
        hud.rightName = rightName;
        hud.leftCharacter = leftChar;
        hud.rightCharacter = rightChar;
        hud.infoText = info;
        hud.controlsText = controls;
        hud.tickText = tick;
        hud.stalledText = stalled;
        hud.warningText = warning;
        hud.resultPanel = resultGo;
        hud.resultText = resultText;
        hud.resultButton = back;
        hud.worldNameLayer = layer;
        hud.worldNames = names;
        return screen;
    }

    static Image HealthBar(Transform parent, string name, bool right)
    {
        GameObject bg = new GameObject(name, typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);
        Vector2 a = right ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        Anchor((RectTransform)bg.transform, a, a, new Vector2(right ? -40f : 40f, -36f), new Vector2(640f, 38f));
        ((RectTransform)bg.transform).pivot = a;
        bg.GetComponent<Image>().sprite = BuiltIn("UI/Skin/Background.psd");
        bg.GetComponent<Image>().type = Image.Type.Sliced;
        bg.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 1f);

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(bg.transform, false);
        RectTransform fillRt = (RectTransform)fillGo.transform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(3f, 3f);
        fillRt.offsetMax = new Vector2(-3f, -3f);
        Image fill = fillGo.GetComponent<Image>();
        fill.sprite = BuiltIn("UI/Skin/Background.psd");
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = right ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.color = new Color(0.25f, 0.85f, 0.35f, 1f);
        return fill;
    }

    static TMP_Text TopCenterText(Transform parent, string name, int size, Color color, float y)
    {
        TMP_Text t = FreeText(parent, name, "", size, TextAlignmentOptions.Top, color);
        Anchor(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(760f, 34f));
        return t;
    }

    // ================= character button prefab =================

    static CharacterButtonUI BuildCharacterButtonPrefab()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/UI", "Prefabs");

        GameObject go = new GameObject("CharacterButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CharacterButtonUI));
        ((RectTransform)go.transform).sizeDelta = new Vector2(170f, 200f);
        Image bg = go.GetComponent<Image>();
        bg.sprite = BuiltIn("UI/Skin/UISprite.psd");
        bg.type = Image.Type.Sliced;
        bg.color = ButtonColor;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;

        GameObject portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitGo.transform.SetParent(go.transform, false);
        RectTransform pr = (RectTransform)portraitGo.transform;
        pr.anchorMin = new Vector2(0f, 0.24f);
        pr.anchorMax = new Vector2(1f, 1f);
        pr.offsetMin = new Vector2(10f, 0f);
        pr.offsetMax = new Vector2(-10f, -18f);
        Image portrait = portraitGo.GetComponent<Image>();
        portrait.raycastTarget = false;
        portrait.preserveAspect = true;

        GameObject stripGo = new GameObject("ColorStrip", typeof(RectTransform), typeof(Image));
        stripGo.transform.SetParent(go.transform, false);
        RectTransform sr = (RectTransform)stripGo.transform;
        sr.anchorMin = new Vector2(0f, 1f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.pivot = new Vector2(0.5f, 1f);
        sr.offsetMin = new Vector2(8f, -14f);
        sr.offsetMax = new Vector2(-8f, -8f);
        Image strip = stripGo.GetComponent<Image>();
        strip.raycastTarget = false;

        TMP_Text nameText = FreeText(go.transform, "Name", "Name", 24, TextAlignmentOptions.Center, Color.white);
        nameText.rectTransform.anchorMin = new Vector2(0f, 0f);
        nameText.rectTransform.anchorMax = new Vector2(1f, 0.24f);
        nameText.rectTransform.offsetMin = new Vector2(6f, 4f);
        nameText.rectTransform.offsetMax = new Vector2(-6f, 0f);

        GameObject selected = new GameObject("Selected", typeof(RectTransform), typeof(Image));
        selected.transform.SetParent(go.transform, false);
        Stretch((RectTransform)selected.transform);
        Image sel = selected.GetComponent<Image>();
        sel.sprite = BuiltIn("UI/Skin/UISprite.psd");
        sel.type = Image.Type.Sliced;
        sel.color = new Color(1f, 0.9f, 0.3f, 0.4f);
        sel.raycastTarget = false;
        selected.SetActive(false);

        CharacterButtonUI ui = go.GetComponent<CharacterButtonUI>();
        ui.button = button;
        ui.portrait = portrait;
        ui.colorStrip = strip;
        ui.nameText = nameText;
        ui.selectedMark = selected;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, ButtonPrefabPath);
        Object.DestroyImmediate(go);
        return saved.GetComponent<CharacterButtonUI>();
    }

    static ControlRowUI BuildControlRowPrefab()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/UI", "Prefabs");

        GameObject go = new GameObject("ControlRow", typeof(RectTransform), typeof(LayoutElement), typeof(ControlRowUI));
        go.GetComponent<LayoutElement>().preferredHeight = 56f;

        TMP_Text label = FreeText(go.transform, "Label", "Action", 28, TextAlignmentOptions.MidlineLeft, Color.white);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, 0f);
        lr.anchorMax = new Vector2(0.5f, 1f);
        lr.offsetMin = new Vector2(8f, 0f);
        lr.offsetMax = Vector2.zero;

        TMP_Text unused;
        Button bind = Btn(go.transform, "BindButton", "Key", 56f, out unused);
        Object.DestroyImmediate(bind.GetComponent<LayoutElement>());
        RectTransform br = (RectTransform)bind.transform;
        br.anchorMin = new Vector2(0.5f, 0f);
        br.anchorMax = new Vector2(1f, 1f);
        br.offsetMin = Vector2.zero;
        br.offsetMax = Vector2.zero;

        ControlRowUI ui = go.GetComponent<ControlRowUI>();
        ui.label = label;
        ui.bindButton = bind;
        ui.bindingText = unused;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, RowPrefabPath);
        Object.DestroyImmediate(go);
        return saved.GetComponent<ControlRowUI>();
    }

    // ================= building blocks =================

    static GameObject NewScreen(string name, Transform parent, bool dim)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        if (dim)
        {
            Image bg = go.AddComponent<Image>();
            bg.color = Dim;
        }
        return go;
    }

    // A centered panel that grows with its content (a vertical layout).
    static RectTransform NewPanel(string name, Transform parent, float width)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, 100f);
        go.GetComponent<Image>().color = PanelColor;
        VerticalLayoutGroup v = go.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(32, 32, 28, 28);
        v.spacing = 12f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    static GameObject NewGroup(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);
        VerticalLayoutGroup v = go.GetComponent<VerticalLayoutGroup>();
        v.spacing = 12f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return go;
    }

    // Text inside a layout (has a fixed height).
    static TMP_Text Txt(Transform parent, string name, string content, int size, TextAlignmentOptions align, Color color, float height)
    {
        TMP_Text t = FreeText(parent, name, content, size, align, color);
        LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        return t;
    }

    // Text positioned by hand.
    static TMP_Text FreeText(Transform parent, string name, string content, int size, TextAlignmentOptions align, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    static Button Btn(Transform parent, string name, string label, float height, out TMP_Text labelText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = BuiltIn("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = ButtonColor;
        Button b = go.GetComponent<Button>();
        b.targetGraphic = img;
        ColorBlock colors = b.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.3f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.9f, 1f);
        colors.disabledColor = new Color(1f, 0.85f, 0.35f, 0.9f);   // doubles as the "selected tab" look
        b.colors = colors;

        labelText = FreeText(go.transform, "Label", label, 28, TextAlignmentOptions.Center, Color.white);
        Stretch(labelText.rectTransform);

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        return b;
    }

    static TMP_InputField InputBox(Transform parent, string name, string placeholder, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image bg = go.GetComponent<Image>();
        bg.sprite = BuiltIn("UI/Skin/InputFieldBackground.psd");
        bg.type = Image.Type.Sliced;
        bg.color = FieldColor;

        GameObject area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(go.transform, false);
        RectTransform areaRt = (RectTransform)area.transform;
        areaRt.anchorMin = Vector2.zero;
        areaRt.anchorMax = Vector2.one;
        areaRt.offsetMin = new Vector2(14f, 6f);
        areaRt.offsetMax = new Vector2(-14f, -7f);

        TMP_Text hint = FreeText(area.transform, "Placeholder", placeholder, 26, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.35f));
        hint.fontStyle = FontStyles.Italic;
        Stretch(hint.rectTransform);
        TMP_Text text = FreeText(area.transform, "Text", "", 26, TextAlignmentOptions.MidlineLeft, Color.white);
        Stretch(text.rectTransform);

        TMP_InputField field = go.AddComponent<TMP_InputField>();
        field.targetGraphic = bg;
        field.textViewport = areaRt;
        field.textComponent = text;
        field.placeholder = hint;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        return field;
    }

    static Toggle MakeToggle(Transform parent, string name, string label, out TMP_Text labelText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 40f;

        GameObject box = new GameObject("Background", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(go.transform, false);
        RectTransform boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = new Vector2(0f, 0.5f);
        boxRt.anchorMax = new Vector2(0f, 0.5f);
        boxRt.pivot = new Vector2(0f, 0.5f);
        boxRt.sizeDelta = new Vector2(36f, 36f);
        boxRt.anchoredPosition = Vector2.zero;
        Image boxImg = box.GetComponent<Image>();
        boxImg.sprite = BuiltIn("UI/Skin/UISprite.psd");
        boxImg.type = Image.Type.Sliced;
        boxImg.color = FieldColor;

        GameObject check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(box.transform, false);
        Stretch((RectTransform)check.transform);
        Image checkImg = check.GetComponent<Image>();
        checkImg.sprite = BuiltIn("UI/Skin/Checkmark.psd");
        checkImg.color = new Color(0.4f, 0.9f, 0.5f, 1f);

        labelText = FreeText(go.transform, "Label", label, 24, TextAlignmentOptions.MidlineLeft, Color.white);
        RectTransform lr = labelText.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(48f, 0f);
        lr.offsetMax = Vector2.zero;

        Toggle toggle = go.AddComponent<Toggle>();
        toggle.targetGraphic = boxImg;
        toggle.graphic = checkImg;
        toggle.isOn = false;
        return toggle;
    }

    static void EnsureEventSystem()
    {
        if (SessionFinder.FindInScene<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
#if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    static Sprite BuiltIn(string path)
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = min;   // pivot on the anchor corner, so the position is the offset from that corner
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static void Show(GameObject go, bool on)
    {
        go.SetActive(on);
    }
}
