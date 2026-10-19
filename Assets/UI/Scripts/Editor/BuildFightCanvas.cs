using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Fight > Rebuild Default Canvas
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

    static readonly Color Dim = new Color(0.02f, 0.06f, 0.04f, 0.88f);
    static readonly Color PanelColor = new Color(0.06f, 0.14f, 0.10f, 0.96f);
    static readonly Color ButtonColor = new Color(0.12f, 0.42f, 0.24f, 1f);
    static readonly Color FieldColor = new Color(0.03f, 0.08f, 0.05f, 1f);
    static readonly Color Muted = new Color(0.66f, 0.82f, 0.70f, 1f);

    // Fight > Rebuild Default Canvas: deletes the FightUI in the open scene (if there is one) and builds the whole
    // default UI again, no questions asked. Your Match Screens options (debug UI, start page...) are kept.
    [MenuItem("Fight/Rebuild Default Canvas")]
    static void Build()
    {
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog("TextMesh Pro resources needed",
                "Run  Window > TextMeshPro > Import TMP Essential Resources  once, then run this menu item again.", "OK");
            return;
        }

        GameObject existing = GameObject.Find(RootName);
        MatchScreens oldScreens = existing != null ? existing.GetComponent<MatchScreens>() : null;
        bool keepOptions = oldScreens != null;
        bool oldDebug = keepOptions && oldScreens.showDebugUI;
        bool oldStartMain = keepOptions && oldScreens.startOnMainMenu;
        bool oldStartTitle = keepOptions && oldScreens.startOnTitle;
        bool oldReplaceImgui = keepOptions && oldScreens.replaceImguiUI;
        bool oldCrt = keepOptions && oldScreens.crtOverUI;
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        BuildSelectArt();
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

        GameObject titlePage = BuildTitle(root.transform);
        GameObject mainMenu = BuildMainMenu(root.transform);
        GameObject controlsPage = BuildControls(root.transform, BuildControlRowPrefab());
        GameObject modePage = BuildModeSelect(root.transform);
        GameObject lobby = BuildLobby(root.transform);
        GameObject waiting = BuildWaiting(root.transform);
        GameObject select = BuildCharacterSelect(root.transform, buttonPrefab);
        GameObject hud = BuildHud(root.transform);
        GameObject creditsPage = BuildCredits(root.transform);

        MatchScreens screens = root.AddComponent<MatchScreens>();
        screens.titleScreen = titlePage;
        screens.mainMenu = mainMenu;
        screens.controls = controlsPage;
        screens.modeSelect = modePage;
        screens.lobby = lobby;
        screens.waiting = waiting;
        screens.characterSelect = select;
        screens.hud = hud;
        screens.credits = creditsPage;
        EnsureTransition(root, screens);   // the glitch and the glass crack overlays, last in the canvas = drawn on top
        screens.editorPreview = UiScreen.Title;
        if (keepOptions)
        {
            screens.showDebugUI = oldDebug;
            screens.startOnMainMenu = oldStartMain;
            screens.startOnTitle = oldStartTitle;
            screens.replaceImguiUI = oldReplaceImgui;
            screens.crtOverUI = oldCrt;
        }
        Show(creditsPage, false);
        Show(titlePage, true);
        Show(mainMenu, false);
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
        Debug.Log("Fight UI rebuilt. Pick the screen to design with  Match Screens > Editor Preview  on '" + RootName + "'. See Assets/UI/CANVAS_UI.md.");
    }

    // Fight > Rebuild Default Canvas
    // For a FightUI you already built and edited: adds only the two new pages and wires them in,
    // without touching anything else.
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
            EditorUtility.DisplayDialog("No Fight UI found", "Run  Fight > Rebuild Default Canvas  first.", "OK");
            return;
        }
        if (screens.titleScreen != null && screens.mainMenu != null && screens.controls != null && screens.modeSelect != null)
        {
            EditorUtility.DisplayDialog("Already there", "This UI already has the title, start menu, mode and controls pages.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Add menu pages");
        if (screens.titleScreen == null)
        {
            screens.titleScreen = BuildTitle(root.transform);
            screens.titleScreen.transform.SetAsFirstSibling();
        }
        EnsureTransition(root, screens);
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

        screens.editorPreview = UiScreen.Title;
        Show(screens.titleScreen, true);
        Show(screens.mainMenu, false);
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

    const string TitleArtFolder = "Assets/UI/Assets";   // the title and main menu art (moved here from MenuScreen/PressAnyKey)

    static GameObject BuildTitle(Transform parent)
    {
        GameObject screen = NewScreen("TitleScreen", parent, false);
        Image bg = screen.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.05f, 1f);

        // Your art, if it is in the project: backdrop, logo and the "PRESS ANY KEY" picture. Laid out like Mockup.png.
        Texture2D backdrop = AssetDatabase.LoadAssetAtPath<Texture2D>(TitleArtFolder + "/Startscreen_backdrop.png");
        Texture2D logo = AssetDatabase.LoadAssetAtPath<Texture2D>(TitleArtFolder + "/Techtonic Logo.png");
        Texture2D promptArt = AssetDatabase.LoadAssetAtPath<Texture2D>(TitleArtFolder + "/PRESS ANY KEY.png");

        TitleScreen ts = screen.AddComponent<TitleScreen>();
        Sprite backdropSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TitleArtFolder + "/Startscreen_backdrop.png");
        if (backdropSprite != null)
        {
            // a normal Image, so it can be 9-sliced and tinted; Sliced when the sprite has borders set in the Sprite Editor
            GameObject backGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backGo.transform.SetParent(screen.transform, false);
            Stretch((RectTransform)backGo.transform);
            Image back = backGo.GetComponent<Image>();
            back.sprite = backdropSprite;
            back.type = backdropSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            back.raycastTarget = false;
        }
        else if (backdrop != null)
        {
            // the picture is not set up as a Sprite: fall back to a Raw Image
            RawImage back = Raw(screen.transform, "Backdrop", backdrop);
            Stretch(back.rectTransform);
        }
        else
        {
            // no art found: a slanted red band behind the title text
            GameObject band = new GameObject("Band", typeof(RectTransform), typeof(Image));
            band.transform.SetParent(screen.transform, false);
            Anchor((RectTransform)band.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(2800f, 300f));
            band.transform.localRotation = Quaternion.Euler(0f, 0f, -6f);
            band.GetComponent<Image>().color = new Color(0.87f, 0.07f, 0.15f, 1f);
            band.GetComponent<Image>().raycastTarget = false;
        }

        if (logo != null)
        {
            RawImage logoImage = Raw(screen.transform, "Logo", logo);
            Anchor(logoImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(15f, 115f), new Vector2(logo.width, logo.height));
        }
        else
        {
            TMP_Text title = FreeText(screen.transform, "Title", "FIGHT", 200, TextAlignmentOptions.Center, Color.white);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 18f;
            Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1500f, 260f));
            title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -6f);
        }

        if (promptArt != null)
        {
            RawImage prompt = Raw(screen.transform, "PromptImage", promptArt);
            Anchor(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(55f, -171f), new Vector2(promptArt.width, promptArt.height));
            ts.promptText = prompt;
        }
        else
        {
            TMP_Text prompt = FreeText(screen.transform, "PromptText", "PRESS ANY KEY", 48, TextAlignmentOptions.Center, Color.white);
            prompt.characterSpacing = 14f;
            Anchor(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -171f), new Vector2(1000f, 70f));
            ts.promptText = prompt;
        }
        return screen;
    }

    static RawImage Raw(Transform parent, string name, Texture texture)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RawImage img = go.GetComponent<RawImage>();
        img.texture = texture;
        img.raycastTarget = false;
        return img;
    }

    // The screen-change glitch: a full-screen object that holds the GlitchTransition and a see-through Image that
    // swallows clicks while it plays. It must be the last child of the canvas, so it sits over every page.
    static GlitchTransition BuildTransitionOverlay(Transform parent)
    {
        GameObject go = new GameObject("TransitionOverlay", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        GlitchTransition gt = go.AddComponent<GlitchTransition>();

        GameObject block = new GameObject("ClickBlocker", typeof(RectTransform), typeof(Image));
        block.transform.SetParent(go.transform, false);
        Stretch((RectTransform)block.transform);
        Image img = block.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;

        gt.blocker = img;
        block.SetActive(false);
        return gt;
    }

    // Finds or builds the transition and makes it the last child (drawn on top), then hooks it up.
    static void EnsureTransition(GameObject root, MatchScreens screens)
    {
        GlitchTransition t = root.GetComponentInChildren<GlitchTransition>(true);
        if (t == null) t = BuildTransitionOverlay(root.transform);
        t.transform.SetAsLastSibling();
        screens.transition = t;

        // the glass crack: used when you press a key on the title and when a match starts
        ShatterEffect shatter = root.GetComponentInChildren<ShatterEffect>(true);
        if (shatter == null)
        {
            GameObject go = new GameObject("ShatterOverlay", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            Stretch((RectTransform)go.transform);
            shatter = go.AddComponent<ShatterEffect>();
        }
        shatter.transform.SetAsLastSibling();
        screens.shatter = shatter;
    }

    // Fight > Rebuild Default Canvas
    // For a FightUI you already have: swaps the start menu for the Play / Options / Credits / Quit layout
    // made from the art in Assets/UI/Assets, adds the Credits page, and replaces the glass shatter
    // with the screen-change glitch. Nothing else in the UI is touched.
    static void RebuildMainMenu()
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
            EditorUtility.DisplayDialog("No Fight UI found", "Open the Menu scene, or run  Fight > Rebuild Default Canvas  first.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Rebuild main menu");
        if (screens.mainMenu != null) Undo.DestroyObjectImmediate(screens.mainMenu);
        screens.mainMenu = BuildMainMenu(root.transform);
        screens.mainMenu.transform.SetSiblingIndex(screens.titleScreen != null ? screens.titleScreen.transform.GetSiblingIndex() + 1 : 0);

        if (screens.credits == null) screens.credits = BuildCredits(root.transform);
        EnsureTransition(root, screens);

        screens.editorPreview = UiScreen.Main;
        if (screens.titleScreen != null) Show(screens.titleScreen, false);
        Show(screens.mainMenu, true);
        Show(screens.credits, false);
        if (screens.controls != null) Show(screens.controls, false);
        if (screens.modeSelect != null) Show(screens.modeSelect, false);
        if (screens.lobby != null) Show(screens.lobby, false);
        if (screens.waiting != null) Show(screens.waiting, false);
        if (screens.characterSelect != null) Show(screens.characterSelect, false);
        if (screens.hud != null) Show(screens.hud, false);

        Selection.activeGameObject = screens.mainMenu;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Main menu rebuilt, Credits page and glitch transition added to '" + RootName + "'.");
    }

    // A sprite from a texture, also when the texture is set to Multiple (the first sprite in it).
    static Sprite LoadSprite(string file)
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(TitleArtFolder + "/" + file);
        for (int i = 0; all != null && i < all.Length; i++)
        {
            if (all[i] is Sprite) return (Sprite)all[i];
        }
        return null;
    }

    // A visible scrollbar for a scroll box: dark track, bright handle. The viewport needs 20 px kept free on the right.
    static Scrollbar BuildScrollbar(Transform scrollRoot)
    {
        GameObject barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGo.transform.SetParent(scrollRoot, false);
        RectTransform barRt = (RectTransform)barGo.transform;
        barRt.anchorMin = new Vector2(1f, 0f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(1f, 0.5f);
        barRt.sizeDelta = new Vector2(16f, -8f);
        barRt.anchoredPosition = new Vector2(-4f, 0f);
        barGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        GameObject slide = new GameObject("Sliding Area", typeof(RectTransform));
        slide.transform.SetParent(barGo.transform, false);
        Stretch((RectTransform)slide.transform);
        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(slide.transform, false);
        Stretch((RectTransform)handle.transform);
        Image handleImg = handle.GetComponent<Image>();
        handleImg.color = new Color(0.55f, 0.85f, 0.45f, 1f);
        Scrollbar bar = barGo.GetComponent<Scrollbar>();
        bar.handleRect = (RectTransform)handle.transform;
        bar.targetGraphic = handleImg;
        bar.direction = Scrollbar.Direction.BottomToTop;
        return bar;
    }

    static GameObject BuildCredits(Transform parent)
    {
        GameObject screen = NewScreen("CreditsScreen", parent, false);
        Sprite art = LoadSprite("Options and Credits Menu Background.png");
        if (art != null)
        {
            GameObject backGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backGo.transform.SetParent(screen.transform, false);
            Stretch((RectTransform)backGo.transform);
            Image back = backGo.GetComponent<Image>();
            back.sprite = art;
            back.type = art.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            back.raycastTarget = false;
        }
        else
        {
            screen.AddComponent<Image>().color = Dim;
        }

        RectTransform panel = NewPanel("Panel", screen.transform, 640f);
        Txt(panel, "Title", "Credits", 48, TextAlignmentOptions.Center, Color.white, 70f);

        // the names scroll inside a fixed-height box; the list itself is Assets/UI/Resources/Credits.txt
        GameObject scrollGo = new GameObject("NamesScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
        scrollGo.transform.SetParent(panel, false);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        scrollGo.GetComponent<LayoutElement>().preferredHeight = 400f;
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollGo.transform, false);
        Stretch((RectTransform)viewport.transform);
        ((RectTransform)viewport.transform).offsetMax = new Vector2(-20f, 0f);

        TMP_Text names = FreeText(viewport.transform, "Names", "", 28, TextAlignmentOptions.TopLeft, Color.white);
        RectTransform namesRt = names.rectTransform;
        namesRt.anchorMin = new Vector2(0f, 1f);
        namesRt.anchorMax = new Vector2(1f, 1f);
        namesRt.pivot = new Vector2(0.5f, 1f);
        namesRt.offsetMin = new Vector2(16f, 0f);
        namesRt.offsetMax = new Vector2(-16f, 0f);
        names.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = namesRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        scroll.verticalScrollbar = BuildScrollbar(scrollGo.transform);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        TMP_Text unused;
        Button back2 = ArtLayoutButton(panel, "BackButton", "BACK.png", "BACKHOVER.png", 90f, "Back");

        CreditsScreen cs = screen.AddComponent<CreditsScreen>();
        cs.backButton = back2;
        cs.namesText = names;
        AddCornerQuit(screen);
        return screen;
    }

    // A picture button that sits in a panel's vertical layout (Online, Offline, Back, Reset...). Falls back to a
    // plain text button if the picture is missing.
    static Button ArtLayoutButton(Transform parent, string name, string file, string hoverFile, float height, string fallbackLabel)
    {
        Sprite normal = LoadSprite(file);
        if (normal == null)
        {
            TMP_Text missing;
            return Btn(parent, name, fallbackLabel, height, out missing);
        }
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = normal;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        Button b = go.GetComponent<Button>();
        b.targetGraphic = img;
        Sprite hover = LoadSprite(hoverFile);
        if (hover != null)
        {
            b.transition = Selectable.Transition.SpriteSwap;
            SpriteState ss = b.spriteState;
            ss.highlightedSprite = hover;
            ss.selectedSprite = hover;
            ss.pressedSprite = hover;
            b.spriteState = ss;
        }
        return b;
    }

    // The QUIT picture in the bottom-right corner of a menu page (see QuitButton).
    static void AddCornerQuit(GameObject screen)
    {
        Sprite normal = LoadSprite("QUIT.png");
        Sprite hover = LoadSprite("QUITHOVER.png");
        if (normal == null) normal = hover;
        if (normal == null) return;
        GameObject go = new GameObject("QuitButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(screen.transform, false);
        Vector2 size = normal.rect.size * 0.45f;
        Anchor((RectTransform)go.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 20f), size);
        ((RectTransform)go.transform).pivot = new Vector2(1f, 0f);
        Image img = go.GetComponent<Image>();
        img.sprite = normal;
        img.type = Image.Type.Simple;
        Button b = go.GetComponent<Button>();
        b.targetGraphic = img;
        if (hover != null && hover != normal)
        {
            b.transition = Selectable.Transition.SpriteSwap;
            SpriteState ss = b.spriteState;
            ss.highlightedSprite = hover;
            ss.selectedSprite = hover;
            ss.pressedSprite = hover;
            b.spriteState = ss;
        }
        go.AddComponent<QuitButton>();
    }

    // The centre of the button group in the 1920x1080 layout (top-left origin). Every button is placed
    // relative to it, so the group can be moved or scaled as one.
    static readonly Vector2 ButtonGroupCenter = new Vector2(955f, 790f);
    static readonly Vector2 ButtonGroupSize = new Vector2(836f, 572f);

    // One of the main menu's picture buttons. left/top are where the sprite sits in the 1920x1080 layout
    // (top-left origin); it is placed relative to the middle of its parent group.
    static Button ArtButton(Transform parent, string name, Sprite normal, Sprite hover, float left, float top)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        Vector2 size = normal.rect.size;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(left + size.x * 0.5f - ButtonGroupCenter.x, ButtonGroupCenter.y - (top + size.y * 0.5f));

        Image img = go.GetComponent<Image>();
        img.sprite = normal;
        img.type = Image.Type.Simple;
        Button b = go.GetComponent<Button>();
        b.targetGraphic = img;
        if (hover != null)
        {
            b.transition = Selectable.Transition.SpriteSwap;
            SpriteState ss = b.spriteState;
            ss.highlightedSprite = hover;
            ss.selectedSprite = hover;
            ss.pressedSprite = hover;
            b.spriteState = ss;
        }
        else
        {
            // no separate hover picture: dim it a little, light it up on hover
            b.transition = Selectable.Transition.ColorTint;
            ColorBlock c = b.colors;
            c.normalColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            c.highlightedColor = Color.white;
            c.selectedColor = Color.white;
            c.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            b.colors = c;
        }
        return b;
    }

    // Laid out like Assets/UI/Assets/MainMenuMockup.png: the backdrop (it already has the title) and four picture buttons.
    // Without that art it falls back to the plain text menu below.
    static GameObject BuildMainMenu(Transform parent)
    {
        // the logo-free backdrop plus the logo picture; the old picture (title painted in) is the fallback
        Sprite noLogoBackdrop = LoadSprite("Main Menubg_no_logo_logo.png");
        Texture2D logo = AssetDatabase.LoadAssetAtPath<Texture2D>(TitleArtFolder + "/Techtonic Logo.png");
        bool addLogo = noLogoBackdrop != null && logo != null;
        Sprite backdrop = addLogo ? noLogoBackdrop : LoadSprite("Main Menu BACKGROUND.png");
        Sprite play = LoadSprite("PLAY.png");
        Sprite options = LoadSprite("OPTIONS.png");
        Sprite credits = LoadSprite("CREDITS.png");
        Sprite quitHover = LoadSprite("QUITHOVER.png");
        Sprite quitNormal = LoadSprite("QUIT.png");
        if (backdrop == null || play == null || options == null || credits == null || (quitNormal == null && quitHover == null))
        {
            return BuildPlainMainMenu(parent);
        }

        GameObject screen = NewScreen("MainMenuScreen", parent, false);
        GameObject backGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backGo.transform.SetParent(screen.transform, false);
        Stretch((RectTransform)backGo.transform);
        Image back = backGo.GetComponent<Image>();
        back.sprite = backdrop;
        back.type = backdrop.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;   // 9-sliced when borders are set in the Sprite Editor
        back.raycastTarget = false;

        if (addLogo)
        {
            // placed where the title sat on the old backdrop (centre of the logo art at about 955, 265 in 1920x1080)
            float scale = 0.6f;
            RawImage logoImage = Raw(screen.transform, "Logo", logo);
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Anchor(logoImage.rectTransform, mid, mid, new Vector2(-9f, 275f), new Vector2(logo.width * scale, logo.height * scale));
            logoImage.rectTransform.pivot = mid;
        }

        // One "Buttons" group holds all four, so you can scale or move the whole block in one go
        // (select Buttons, use its Scale or the Rect tool). It is anchored to the screen's centre.
        GameObject group = new GameObject("Buttons", typeof(RectTransform));
        group.transform.SetParent(screen.transform, false);
        RectTransform groupRt = (RectTransform)group.transform;
        groupRt.anchorMin = groupRt.anchorMax = groupRt.pivot = new Vector2(0.5f, 0.5f);
        groupRt.sizeDelta = ButtonGroupSize;
        groupRt.anchoredPosition = new Vector2(ButtonGroupCenter.x - 960f, -190f);   // Pos Y -190 (the mockup's exact spot was -250)

        // positions measured from MainMenuMockup.png (sprite top-left, in a 1920x1080 layout)
        // Hierarchy order is draw order (the last one is on top): Quit, Credits, Options, Play. The sprites
        // overlap a little, so Play is on top of Options, Options on top of Credits, and so on.
        Button quitB = quitNormal != null
            ? ArtButton(group.transform, "QuitButton", quitNormal, quitHover, 951f, 871f)
            : ArtButton(group.transform, "QuitButton", quitHover, null, 951f, 871f);   // QUIT.png is missing: QUITHOVER, dimmed until hovered
        Button creditsB = ArtButton(group.transform, "CreditsButton", credits, LoadSprite("CREDITSHOVER.png"), 537f, 859f);
        Button optionsB = ArtButton(group.transform, "OptionsButton", options, LoadSprite("OPTIONSHOVER.png"), 686f, 671f);
        Button playB = ArtButton(group.transform, "PlayButton", play, LoadSprite("PLAYHOVER.png"), 785f, 504f);

        MainMenuScreen ms = screen.AddComponent<MainMenuScreen>();
        ms.playButton = playB;
        ms.controlsButton = optionsB;
        ms.creditsButton = creditsB;
        ms.quitButton = quitB;
        return screen;
    }

    static GameObject BuildPlainMainMenu(Transform parent)
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
        GameObject screen = NewArtScreen("ModeSelectScreen", parent, false, "Main Menubg_no_logo_logo.png", false);
        RectTransform panel = NewPanel("Panel", screen.transform, 560f);

        Txt(panel, "Title", "Choose mode", 56, TextAlignmentOptions.Center, Color.white, 80f);
        Txt(panel, "NameLabel", "Your name", 22, TextAlignmentOptions.Center, Muted, 28f);
        TMP_InputField nameInput = InputBox(panel, "NameInput", "Enter a name", 38f, 90f);
        TMP_Text unused;
        Button online = ArtLayoutButton(panel, "OnlineButton", "ONLINE.png", "ONLINEHOVER.png", 110f, "Online");
        Button offline = ArtLayoutButton(panel, "OfflineButton", "OFFLINE.png", "OFFLINEHOVER.png", 110f, "Offline (vs AI)");
        Button back = ArtLayoutButton(panel, "BackButton", "BACK.png", "BACKHOVER.png", 135f, "Back");

        ModeSelectScreen ms = screen.AddComponent<ModeSelectScreen>();
        ms.nameInput = nameInput;
        ms.onlineButton = online;
        ms.offlineButton = offline;
        ms.backButton = back;
        AddCornerQuit(screen);
        return screen;
    }

    static GameObject BuildControls(Transform parent, ControlRowUI rowPrefab)
    {
        // same backdrop as Credits
        GameObject screen = NewArtScreen("ControlsScreen", parent, false, "Options and Credits Menu Background.png", false);
        RectTransform panel = NewPanel("Panel", screen.transform, 560f);

        Txt(panel, "Title", "Controls", 44, TextAlignmentOptions.Center, Color.white, 64f);
        TMP_Text hint = Txt(panel, "HintText", "Click an action, then press a key", 22, TextAlignmentOptions.Center, Muted, 32f);

        // the rows scroll inside a fixed-height box, so a long list never grows past the screen
        GameObject scrollGo = new GameObject("RowsScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
        scrollGo.transform.SetParent(panel, false);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        scrollGo.GetComponent<LayoutElement>().preferredHeight = 340f;
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollGo.transform, false);
        Stretch((RectTransform)viewport.transform);

        GameObject rows = NewGroup("Rows", viewport.transform);
        RectTransform rowsRt = (RectTransform)rows.transform;
        rowsRt.anchorMin = new Vector2(0f, 1f);
        rowsRt.anchorMax = new Vector2(1f, 1f);
        rowsRt.pivot = new Vector2(0.5f, 1f);
        rowsRt.offsetMin = Vector2.zero;
        rowsRt.offsetMax = Vector2.zero;
        rows.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(8, 8, 8, 8);
        rows.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = rowsRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        // a bar you can actually see: dark track, bright handle, always shown
        ((RectTransform)viewport.transform).offsetMax = new Vector2(-20f, 0f);
        GameObject barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGo.transform.SetParent(scrollGo.transform, false);
        RectTransform barRt = (RectTransform)barGo.transform;
        barRt.anchorMin = new Vector2(1f, 0f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(1f, 0.5f);
        barRt.sizeDelta = new Vector2(16f, -8f);
        barRt.anchoredPosition = new Vector2(-4f, 0f);
        barGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        GameObject slide = new GameObject("Sliding Area", typeof(RectTransform));
        slide.transform.SetParent(barGo.transform, false);
        Stretch((RectTransform)slide.transform);
        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(slide.transform, false);
        Stretch((RectTransform)handle.transform);
        Image handleImg = handle.GetComponent<Image>();
        handleImg.color = new Color(0.55f, 0.85f, 0.45f, 1f);
        Scrollbar bar = barGo.GetComponent<Scrollbar>();
        bar.handleRect = (RectTransform)handle.transform;
        bar.targetGraphic = handleImg;
        bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        TMP_Text unused;
        Button reset = ArtLayoutButton(panel, "ResetButton", "RESET.png", "RESETHOVER.png", 90f, "Reset to defaults");
        Button back = ArtLayoutButton(panel, "BackButton", "BACK.png", "BACKHOVER.png", 90f, "Back");

        ControlsScreen cs = screen.AddComponent<ControlsScreen>();
        cs.rowParent = (RectTransform)rows.transform;
        cs.rowPrefab = rowPrefab;
        cs.resetButton = reset;
        cs.backButton = back;
        cs.hintText = hint;
        AddCornerQuit(screen);
        return screen;
    }

    static GameObject BuildLobby(Transform parent)
    {
        GameObject screen = NewArtScreen("LobbyScreen", parent, false, "Main Menubg_no_logo_logo.png", false);
        RectTransform panel = NewPanel("Panel", screen.transform, 600f);

        Txt(panel, "Title", "ONLINE", 64, TextAlignmentOptions.Center, Color.white, 80f);

        GameObject online = NewGroup("OnlineGroup", panel);
        TMP_Text unused;
        Button quick = Btn(online.transform, "QuickMatchButton", "Quick match", 72f, out unused);
        Txt(online.transform, "PrivateLabel", "Private room", 22, TextAlignmentOptions.MidlineLeft, Muted, 28f);
        Button create = Btn(online.transform, "CreateRoomButton", "Create private room", 60f, out unused);
        TMP_InputField codeInput = InputBox(online.transform, "CodeInput", "Room code (ABCD)", 52f);
        Button join = Btn(online.transform, "JoinRoomButton", "Join with code", 60f, out unused);

        Button back = Btn(panel, "BackButton", "Back", 60f, out unused);

        LobbyScreen ls = screen.AddComponent<LobbyScreen>();
        ls.codeInput = codeInput;
        ls.quickMatchButton = quick;
        ls.createRoomButton = create;
        ls.joinRoomButton = join;
        ls.backButton = back;
        ls.onlineGroup = online;
        AddCornerQuit(screen);
        return screen;
    }

    static GameObject BuildWaiting(Transform parent)
    {
        GameObject screen = NewArtScreen("WaitingScreen", parent);
        RectTransform panel = NewPanel("Panel", screen.transform, 600f);

        TMP_Text heading = Txt(panel, "Heading", "Room code", 28, TextAlignmentOptions.Center, Muted, 40f);
        TMP_Text code = Txt(panel, "RoomCodeText", "ABCD", 110, TextAlignmentOptions.Center, Color.white, 130f);
        TMP_Text hint = Txt(panel, "HintText", "Give your opponent this code", 24, TextAlignmentOptions.Center, Muted, 36f);
        TMP_Text status = Txt(panel, "StatusText", "", 22, TextAlignmentOptions.Center, new Color(0.75f, 0.95f, 0.40f), 34f);
        TMP_Text unused;
        Button cancel = Btn(panel, "CancelButton", "Cancel", 60f, out unused);

        WaitingScreen ws = screen.AddComponent<WaitingScreen>();
        ws.headingText = heading;
        ws.roomCodeText = code;
        ws.hintText = hint;
        ws.statusText = status;
        ws.cancelButton = cancel;
        AddCornerQuit(screen);
        return screen;
    }

    // ================= character select =================
    // Laid out like the mockups in Assets/UI/CharacterSelect: a 1920x1080 stage (scaled to fit the window) with
    // the framed 3x3 grid on the left, the gear timer, and the big character panel on the right. More swaps the
    // grid for the Background / Skills text page and slides the character left. Positions are top-left pixels.

    const string SelectArtFolder = "Assets/UI/CharacterSelect";
    const string SelectArtAssetPath = "Assets/UI/Resources/CharacterSelectArt.asset";
    static readonly Color SelectBackdrop = new Color(12f / 255f, 1f / 255f, 21f / 255f, 1f);
    static readonly Color TimerOrange = new Color(0.96f, 0.54f, 0.12f, 1f);

    static GameObject BuildCharacterSelect(Transform parent, CharacterButtonUI buttonPrefab)
    {
        TMP_FontAsset font = TradeWindsFont();
        GameObject screen = NewScreen("CharacterSelectScreen", parent, false);

        GameObject back = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(screen.transform, false);
        Stretch((RectTransform)back.transform);
        Image backImg = back.GetComponent<Image>();
        backImg.color = SelectBackdrop;
        backImg.raycastTarget = false;

        GameObject stageGo = new GameObject("Stage", typeof(RectTransform), typeof(StageFit));
        stageGo.transform.SetParent(screen.transform, false);
        RectTransform stage = (RectTransform)stageGo.transform;
        stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
        stage.anchoredPosition = Vector2.zero;
        stage.sizeDelta = new Vector2(1920f, 1080f);

        Texture2D logo = AssetDatabase.LoadAssetAtPath<Texture2D>(TitleArtFolder + "/Techtonic Logo.png");
        if (logo != null)
        {
            RawImage logoImage = Raw(stage, "Logo", logo);
            logoImage.raycastTarget = false;
            CharacterProfilePanel.Place(logoImage.rectTransform, 795f, -4f, logo.width * 0.28f, logo.height * 0.28f);
        }

        // ----- list page: frame, grid, Back / Fight, tabs (vs AI), status -----
        GameObject list = new GameObject("ListPage", typeof(RectTransform));
        list.transform.SetParent(stage, false);
        Stretch((RectTransform)list.transform);

        PlacedImage(list.transform, "Frame", LoadSprite("UIPanel.png"), 38f, 28f, 732f, 865f);
        PlacedImage(list.transform, "Title", SelectSprite("General/choose_a_renegade.png"), 101f, 90f, 618f, 71f);

        GameObject gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.transform.SetParent(list.transform, false);
        CharacterProfilePanel.Place((RectTransform)gridGo.transform, 166f, 172f, 490f, 654f);
        GridLayoutGroup grid = gridGo.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CharacterButtonUI.TileWidth, CharacterButtonUI.TileHeight);
        grid.spacing = new Vector2(20f, 27f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        Button backBtn = PlacedArtButton(list.transform, "BackButton", LoadSprite("BACK.png"), LoadSprite("BACKHOVER.png"), 85f, 872f);
        Button confirm = PlacedArtButton(list.transform, "ConfirmButton", SelectSprite("General/fight.png"), SelectSprite("General/fight_hover.png"), 424f, 872f);

        TMP_Text title = FreeText(list.transform, "TitleText", "Choose your character", 26, TextAlignmentOptions.Center, Muted);
        CharacterProfilePanel.Place(title.rectTransform, 780f, 405f, 440f, 70f);
        UseFont(title, font);

        GameObject tabs = new GameObject("Tabs", typeof(RectTransform), typeof(VerticalLayoutGroup));
        tabs.transform.SetParent(list.transform, false);
        CharacterProfilePanel.Place((RectTransform)tabs.transform, 800f, 490f, 440f, 132f);
        VerticalLayoutGroup tabLayout = tabs.GetComponent<VerticalLayoutGroup>();
        tabLayout.spacing = 12f;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;
        TMP_Text youLabel;
        TMP_Text oppLabel;
        Button youTab = Btn(tabs.transform, "YouTab", "You", 60f, out youLabel);
        Button oppTab = Btn(tabs.transform, "OpponentTab", "Opponent", 60f, out oppLabel);
        youLabel.fontSize = 24;
        oppLabel.fontSize = 24;

        TMP_Text status = FreeText(list.transform, "StatusText", "", 24, TextAlignmentOptions.Center, new Color(0.75f, 0.95f, 0.40f));
        CharacterProfilePanel.Place(status.rectTransform, 780f, 640f, 440f, 100f);

        // ----- the gear timer (shown on both pages) -----
        GameObject gearGo = new GameObject("TimerGear", typeof(RectTransform), typeof(Image));
        gearGo.transform.SetParent(stage, false);
        Image gearImg = gearGo.GetComponent<Image>();
        Sprite gearSprite = SelectSprite("General/timer_gear.png");
        gearImg.sprite = gearSprite;
        gearImg.raycastTarget = false;
        CharacterProfilePanel.Place((RectTransform)gearGo.transform, 845f, 183f, 215f, 215f);
        TMP_Text timer = FreeText(gearGo.transform, "Timer", "30s", 52, TextAlignmentOptions.Center, TimerOrange);
        Stretch(timer.rectTransform);
        UseFont(timer, font);

        // ----- the big character panel -----
        GameObject profileGo = new GameObject("Profile", typeof(RectTransform), typeof(CharacterProfilePanel));
        profileGo.transform.SetParent(stage, false);
        RectTransform profileRt = (RectTransform)profileGo.transform;
        CharacterProfilePanel.Place(profileRt, 0f, 0f, 1920f, 1080f);
        CharacterProfilePanel profile = profileGo.GetComponent<CharacterProfilePanel>();
        profile.group = profileRt;
        profile.backing = PlacedImage(profileRt, "Backing", null, 1260f, 80f, 500f, 1000f);
        profile.sprite = PlacedImage(profileRt, "Sprite", null, 1260f, 80f, 500f, 1000f);
        profile.nameArt = PlacedImage(profileRt, "Name", null, 1260f, 80f, 100f, 100f);
        profile.traits = new Image[3];
        for (int i = 0; i < 3; i++) profile.traits[i] = PlacedImage(profileRt, "Trait" + (i + 1), null, 1260f, 80f, 100f, 100f);
        profile.minis = new Image[4];
        for (int i = 0; i < 4; i++) profile.minis[i] = PlacedImage(profileRt, "Skill" + (i + 1), null, 1260f, 80f, 100f, 100f);
        TMP_Text fallbackName = FreeText(profileRt, "FallbackName", "", 90, TextAlignmentOptions.Center, Color.white);
        UseFont(fallbackName, font);
        fallbackName.gameObject.SetActive(false);
        profile.fallbackName = fallbackName;

        GameObject moreGo = new GameObject("MoreButton", typeof(RectTransform), typeof(Image), typeof(Button));
        moreGo.transform.SetParent(profileRt, false);
        Image moreImg = moreGo.GetComponent<Image>();
        Button moreBtn = moreGo.GetComponent<Button>();
        moreBtn.targetGraphic = moreImg;
        ColorBlock mc = moreBtn.colors;
        mc.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        mc.highlightedColor = Color.white;
        mc.selectedColor = Color.white;
        mc.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        moreBtn.colors = mc;
        profile.moreButton = moreBtn;
        profile.moreImage = moreImg;

        // ----- details page: Background / Skills text -----
        GameObject details = new GameObject("DetailsPage", typeof(RectTransform));
        details.transform.SetParent(stage, false);
        Stretch((RectTransform)details.transform);

        Button bgTab = PlacedArtButton(details.transform, "BackgroundTab", SelectSprite("General/background_tab.png"), null, 1125f, 118f);
        Button skTab = PlacedArtButton(details.transform, "SkillsTab", SelectSprite("General/skills_tab_dark.png"), null, 1485f, 126f);
        bgTab.transition = Selectable.Transition.None;
        skTab.transition = Selectable.Transition.None;

        PlacedImage(details.transform, "Bar", SolidSprite(), 1085f, 270f, 6f, 770f);

        GameObject scrollGo = new GameObject("TextScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(details.transform, false);
        CharacterProfilePanel.Place((RectTransform)scrollGo.transform, 1115f, 270f, 770f, 770f);
        scrollGo.GetComponent<Image>().color = Color.clear;
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollGo.transform, false);
        Stretch((RectTransform)viewport.transform);
        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRt = (RectTransform)content.transform;
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;
        VerticalLayoutGroup cv = content.GetComponent<VerticalLayoutGroup>();
        cv.spacing = 20f;
        cv.padding = new RectOffset(0, 24, 0, 40);
        cv.childAlignment = TextAnchor.UpperLeft;
        cv.childControlWidth = true;
        cv.childControlHeight = true;
        cv.childForceExpandWidth = true;
        cv.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = contentRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        TMP_Text bgText = FreeText(content.transform, "BackgroundText", "", 32, TextAlignmentOptions.TopLeft, new Color(0.94f, 0.94f, 0.96f));
        UseFont(bgText, font);
        GameObject skillsGo = new GameObject("SkillsList", typeof(RectTransform), typeof(VerticalLayoutGroup));
        skillsGo.transform.SetParent(content.transform, false);
        VerticalLayoutGroup sv = skillsGo.GetComponent<VerticalLayoutGroup>();
        sv.spacing = 26f;
        sv.childAlignment = TextAnchor.UpperLeft;
        sv.childControlWidth = true;
        sv.childControlHeight = true;
        sv.childForceExpandWidth = true;
        sv.childForceExpandHeight = false;
        skillsGo.SetActive(false);

        CharacterSelectScreen cs = screen.AddComponent<CharacterSelectScreen>();
        cs.gridParent = (RectTransform)gridGo.transform;
        cs.buttonPrefab = buttonPrefab;
        cs.tabsGroup = tabs;
        cs.youTab = youTab;
        cs.opponentTab = oppTab;
        cs.youTabLabel = youLabel;
        cs.opponentTabLabel = oppLabel;
        cs.confirmButton = confirm;
        cs.confirmLabel = null;
        cs.backButton = backBtn;
        cs.backLabel = null;
        cs.titleText = title;
        cs.statusText = status;
        cs.listPage = list;
        cs.detailsPage = details;
        cs.profile = profile;
        cs.gear = (RectTransform)gearGo.transform;
        cs.timerText = timer;
        cs.backgroundTab = bgTab;
        cs.skillsTab = skTab;
        cs.backgroundTabImage = bgTab.GetComponent<Image>();
        cs.skillsTabImage = skTab.GetComponent<Image>();
        cs.backgroundOn = SelectSprite("General/background_tab.png");
        cs.backgroundOff = SelectSprite("General/background_tab_dark.png");
        cs.skillsOn = SelectSprite("General/skills_tab.png");
        cs.skillsOff = SelectSprite("General/skills_tab_dark.png");
        cs.backgroundText = bgText;
        cs.skillsList = (RectTransform)skillsGo.transform;
        cs.detailsScroll = scroll;
        details.SetActive(false);
        return screen;
    }

    // An Image placed by top-left pixels in the 1920x1080 layout.
    static Image PlacedImage(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        CharacterProfilePanel.Place((RectTransform)go.transform, x, y, w, h);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.enabled = sprite != null;
        img.raycastTarget = false;
        return img;
    }

    // A picture button placed by top-left pixels, at the picture's own size. Hover picture optional.
    static Button PlacedArtButton(Transform parent, string name, Sprite normal, Sprite hover, float x, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Vector2 size = normal != null ? normal.rect.size : new Vector2(200f, 80f);
        CharacterProfilePanel.Place((RectTransform)go.transform, x, y, size.x, size.y);
        Image img = go.GetComponent<Image>();
        img.sprite = normal;
        Button b = go.GetComponent<Button>();
        b.targetGraphic = img;
        if (hover != null)
        {
            b.transition = Selectable.Transition.SpriteSwap;
            SpriteState ss = b.spriteState;
            ss.highlightedSprite = hover;
            ss.selectedSprite = hover;
            ss.pressedSprite = hover;
            b.spriteState = ss;
        }
        return b;
    }

    static void UseFont(TMP_Text t, TMP_FontAsset font)
    {
        if (font != null) t.font = font;
    }

    // A sprite from Assets/UI/CharacterSelect, set up as a plain Sprite (no mipmaps, uncompressed) if it is not yet.
    static Sprite SelectSprite(string relativePath)
    {
        string path = SelectArtFolder + "/" + relativePath;
        TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return null;
        bool dirty = false;
        if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; dirty = true; }
        if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
        if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }
        if (imp.maxTextureSize < 2048) { imp.maxTextureSize = 2048; dirty = true; }
        if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
        if (dirty) imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // The non-transparent part of a picture, in pixels from its top-left.
    static Rect OpaqueBounds(Sprite sprite)
    {
        if (sprite == null) return new Rect();
        string path = AssetDatabase.GetAssetPath(sprite);
        string full = Application.dataPath + path.Substring("Assets".Length);
        if (!System.IO.File.Exists(full)) return new Rect();
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(System.IO.File.ReadAllBytes(full));
        Color32[] px = tex.GetPixels32();
        int w = tex.width, h = tex.height;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a <= 20) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        Object.DestroyImmediate(tex);
        if (maxX < 0) return new Rect();
        return new Rect(minX, h - 1 - maxY, maxX - minX + 1, maxY - minY + 1);   // y from the top
    }

    // Trade Winds (the font for the timer and the text pages) as a TextMeshPro font asset, made on first use.
    static TMP_FontAsset TradeWindsFont()
    {
        const string ttf = "Assets/UI/Fonts/TradeWinds-Regular.ttf";
        const string assetPath = "Assets/UI/Fonts/TradeWinds SDF.asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;
        Font font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        if (font == null)
        {
            Debug.LogWarning("Trade Winds font not found at " + ttf + ": the timer and text pages use the default font.");
            return null;
        }
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (fa == null) return null;
        fa.name = "TradeWinds SDF";
        AssetDatabase.CreateAsset(fa, assetPath);
        if (fa.atlasTexture != null) { fa.atlasTexture.name = "TradeWinds Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa); }
        if (fa.material != null) { fa.material.name = "TradeWinds Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    // Fills Assets/UI/Resources/CharacterSelectArt.asset from the pictures in Assets/UI/CharacterSelect/<FOLDER>/.
    // Pictures are refreshed every time; the Background / Skills texts you edit in the asset are kept.
    static CharacterSelectArt BuildSelectArt()
    {
        AssetDatabase.Refresh();
        CharacterSelectArt asset = AssetDatabase.LoadAssetAtPath<CharacterSelectArt>(SelectArtAssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<CharacterSelectArt>();
            AssetDatabase.CreateAsset(asset, SelectArtAssetPath);
        }

        // folder, character id, label, number of banners, name picture's bottom-right corner
        object[][] defs =
        {
            new object[] { "RUY", Samurai.Id, "Ruy", 3, new Vector2(1868f, 830f) },
            // Odette: arrow-shaped banners hug the right edge (x is clamped to fit)
            new object[] { "ODETTE", Painter.Id, "Odette", 3, new Vector2(1868f, 800f),
                new[] { new Vector2(1915f, 40f), new Vector2(1915f, 270f), new Vector2(1915f, 375f) } },
            new object[] { "ROOM", Elephant.Id, "Room", 3, new Vector2(1800f, 800f), null, 183.9017f },
            new object[] { "PATIENCE", Executioner.Id, "Patience", 1, new Vector2(1868f, 830f) },
            new object[] { "DR_REMY", PlagueDoctor.Id, "Dr. Remy", 3, new Vector2(1868f, 880f) },
            new object[] { "K3N", K3N.Id, "K3N", 3, new Vector2(1868f, 830f) },
            new object[] { "NOIR", Cowboy.Id, "Noir", 3, new Vector2(1868f, 830f) },
        };
        System.Collections.Generic.List<CharacterSelectArt.Entry> entries = new System.Collections.Generic.List<CharacterSelectArt.Entry>();
        for (int d = 0; d < defs.Length; d++)
        {
            string folder = (string)defs[d][0];
            int id = (int)defs[d][1];
            CharacterSelectArt.Entry e = asset.Find(id);
            bool isNew = e == null;
            if (isNew) e = new CharacterSelectArt.Entry();
            e.characterId = id;
            e.label = (string)defs[d][2];
            e.backing = SelectSprite(folder + "/backing.png");
            e.sprite = SelectSprite(folder + "/sprite.png");
            e.spriteBounds = OpaqueBounds(e.sprite);
            e.nameArt = SelectSprite(folder + "/name.png");
            e.moreArt = SelectSprite(folder + "/more.png");
            e.backArt = SelectSprite(folder + "/back.png");
            e.miniSkill = SelectSprite(folder + "/mini_skill.png");
            e.skillBlock = SelectSprite(folder + "/skill_block.png");
            int n = (int)defs[d][3];
            e.traits = new Sprite[n];
            for (int t = 0; t < n; t++) e.traits[t] = SelectSprite(folder + "/trait_" + (t + 1) + ".png");
            if (isNew) e.nameCorner = (Vector2)defs[d][4];
            if (isNew && defs[d].Length > 5 && defs[d][5] != null) e.traitPositions = (Vector2[])defs[d][5];
            if ((isNew || e.tilePortraitY == 0f) && defs[d].Length > 6) e.tilePortraitY = (float)defs[d][6];
            if (isNew || string.IsNullOrEmpty(e.background)) e.background = "Coming soon.";
            if (id == PlagueDoctor.Id && (isNew || e.background == "Coming soon.")) FillRemyText(e);
            entries.Add(e);
        }
        // keep entries for characters this builder does not know about
        if (asset.entries != null)
        {
            for (int i = 0; i < asset.entries.Length; i++)
            {
                CharacterSelectArt.Entry old = asset.entries[i];
                if (old == null) continue;
                bool known = false;
                for (int d = 0; d < defs.Length; d++) if ((int)defs[d][1] == old.characterId) known = true;
                if (!known) entries.Add(old);
            }
        }
        asset.entries = entries.ToArray();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    // Dr. Remy's page text (Background from the mockup, Skills from his design). Only written when empty.
    static void FillRemyText(CharacterSelectArt.Entry e)
    {
        e.background = "It wasn't the rats that started the plague. It was the tick infected with the plague that bit the rat and started the plague. The rats were victims too, and they need a cure. So, to gain the humans' trust, the rats went undercover. Dr. Remy was formed, saving patients with his unique set of cures... The people Dr. Remy encountered would describe them as rather quiet. \"He's always mumbling to himself, a squeaky little thing.\" Regardless, the doctor got the job done, and learned a lot about medicine in the process. They were so close to finding the cure, but then their plans were upturned with the abduction...";
        e.skills = new[]
        {
            new CharacterSelectArt.Skill { name = "LIGHT: CANE JAB", description = "A medium-range jab with his cane." },
            new CharacterSelectArt.Skill { name = "HEAVY: THROW REMY", description = "Lobs Remy the rat in a high arc that comes down far away." },
            new CharacterSelectArt.Skill { name = "GRAB: LEECHES", description = "Holds the opponent close, then sticks leeches on them. They drain health over time and heal Dr. Remy." },
            new CharacterSelectArt.Skill { name = "SPECIAL: REMEDIES", description = "Tap to switch between a poison cloud, a healing drink and a slowing potion. Hold to use it." },
            new CharacterSelectArt.Skill { name = "ULTIMATE: UNLEASH THE RATS", description = "After a short delay he sprays a shower of rats across the stage. It can't be blocked and can't be interrupted." },
        };
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
        TMP_Text stalled = TopCenterText(screen.transform, "StalledText", 26, new Color(0.75f, 0.95f, 0.40f), -130f);
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
        Anchor((RectTransform)resultGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 560f));
        StylePanel(resultGo.GetComponent<Image>());
        TMP_Text resultText = FreeText(resultGo.transform, "ResultText", "", 64, TextAlignmentOptions.Center, Color.white);
        Anchor(resultText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(580f, 170f));
        TMP_Text unused;
        Button back = Btn(resultGo.transform, "BackToLobbyButton", "Back to lobby", 64f, out unused);
        Anchor((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 115f), new Vector2(320f, 64f));
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
        bg.GetComponent<Image>().color = new Color(0.06f, 0.14f, 0.10f, 1f);

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(bg.transform, false);
        RectTransform fillRt = (RectTransform)fillGo.transform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(3f, 3f);
        fillRt.offsetMax = new Vector2(-3f, -3f);
        Image fill = fillGo.GetComponent<Image>();
        fill.sprite = SolidSprite();   // a flat block: the rounded built-in sprite faded to nothing at the bar's ends
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
        ((RectTransform)go.transform).sizeDelta = new Vector2(CharacterButtonUI.TileWidth, CharacterButtonUI.TileHeight);
        Image root = go.GetComponent<Image>();
        root.color = Color.clear;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = root;
        button.transition = Selectable.Transition.None;

        // bright frame behind the tile: it shows as a border around the tile when chosen
        GameObject selected = new GameObject("Selected", typeof(RectTransform), typeof(Image));
        selected.transform.SetParent(go.transform, false);
        RectTransform sr = (RectTransform)selected.transform;
        sr.anchorMin = Vector2.zero;
        sr.anchorMax = Vector2.one;
        sr.offsetMin = new Vector2(-6f, -6f);
        sr.offsetMax = new Vector2(6f, 6f);
        Image sel = selected.GetComponent<Image>();
        sel.color = new Color(1f, 0.93f, 0.55f, 1f);
        sel.raycastTarget = false;
        selected.SetActive(false);

        GameObject backingGo = new GameObject("Backing", typeof(RectTransform), typeof(Image));
        backingGo.transform.SetParent(go.transform, false);
        Stretch((RectTransform)backingGo.transform);
        Image backing = backingGo.GetComponent<Image>();
        backing.raycastTarget = false;

        GameObject maskGo = new GameObject("Mask", typeof(RectTransform), typeof(RectMask2D));
        maskGo.transform.SetParent(go.transform, false);
        Stretch((RectTransform)maskGo.transform);
        GameObject portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitGo.transform.SetParent(maskGo.transform, false);
        Image portrait = portraitGo.GetComponent<Image>();
        portrait.raycastTarget = false;

        TMP_Text nameText = FreeText(go.transform, "Name", "Name", 24, TextAlignmentOptions.Center, Color.white);
        Stretch(nameText.rectTransform);
        nameText.gameObject.SetActive(false);

        CharacterButtonUI ui = go.GetComponent<CharacterButtonUI>();
        ui.button = button;
        ui.backing = backing;
        ui.portrait = portrait;
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

    // A screen with the logo-free menu picture behind it (Online / Quick match / Private room pages).
    // Without the art it falls back to the dark dim.
    static GameObject NewArtScreen(string name, Transform parent, bool logoInCorner = false,
        string artFile = "Main Menubg_no_logo_logo.png", bool withLogo = true)
    {
        Sprite art = LoadSprite(artFile);
        GameObject screen = NewScreen(name, parent, art == null);
        if (art == null) return screen;
        GameObject backGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backGo.transform.SetParent(screen.transform, false);
        Stretch((RectTransform)backGo.transform);
        Image back = backGo.GetComponent<Image>();
        back.sprite = art;
        back.type = art.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        back.raycastTarget = false;

        // the logo is not part of this backdrop: centred above the panel, or small in the corner on crowded screens
        Texture2D logo = AssetDatabase.LoadAssetAtPath<Texture2D>(TitleArtFolder + "/Techtonic Logo.png");
        if (withLogo && logo != null)
        {
            float scale = logoInCorner ? 0.28f : 0.5f;
            RawImage logoImage = Raw(screen.transform, "Logo", logo);
            logoImage.raycastTarget = false;
            Vector2 anchor = logoInCorner ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f);
            Anchor(logoImage.rectTransform, anchor, anchor, new Vector2(logoInCorner ? 10f : 0f, 30f),
                new Vector2(logo.width * scale, logo.height * scale));
            logoImage.rectTransform.pivot = anchor;
        }
        return screen;
    }

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
    // The UI Panel art (Assets/UI/Assets/UIPanel.png) behind every menu panel; flat colour if it is missing.
    static void StylePanel(Image img)
    {
        Sprite art = LoadSprite("UIPanel.png");
        if (art != null)
        {
            img.sprite = art;
            img.type = Image.Type.Sliced;   // the sprite has its borders set (gears stay full size, the middle stretches)
            img.color = Color.white;
        }
        else img.color = PanelColor;
    }

    static RectTransform NewPanel(string name, Transform parent, float width)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width + 220f, 100f);   // the art's empty margin plus a roomier frame
        StylePanel(go.GetComponent<Image>());
        VerticalLayoutGroup v = go.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(100, 100, 140, 90);   // clear of the frame and the gears
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
        colors.highlightedColor = new Color(1.10f, 1.30f, 1.10f, 1f);
        colors.pressedColor = new Color(0.75f, 0.9f, 0.78f, 1f);
        colors.disabledColor = new Color(0.70f, 0.95f, 0.35f, 0.95f);   // doubles as the "selected tab" look
        b.colors = colors;

        labelText = FreeText(go.transform, "Label", label, 28, TextAlignmentOptions.Center, Color.white);
        Stretch(labelText.rectTransform);

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        return b;
    }

    static TMP_InputField InputBox(Transform parent, string name, string placeholder, float height, float sideInset = 0f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        Transform inputParent = parent;
        if (sideInset > 0f)
        {
            // a holder takes the layout slot; the box inside it stops short of the panel's edges
            GameObject holder = new GameObject(name + "Holder", typeof(RectTransform), typeof(LayoutElement));
            holder.transform.SetParent(parent, false);
            holder.GetComponent<LayoutElement>().preferredHeight = height;
            inputParent = holder.transform;
        }
        go.transform.SetParent(inputParent, false);
        if (sideInset > 0f)
        {
            RectTransform grt = (RectTransform)go.transform;
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(sideInset, 0f);
            grt.offsetMax = new Vector2(-sideInset, 0f);
        }
        Image bg = go.GetComponent<Image>();
        bg.sprite = BuiltIn("UI/Skin/InputFieldBackground.psd");
        bg.type = Image.Type.Sliced;
        bg.color = FieldColor;

        GameObject area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(go.transform, false);
        RectTransform areaRt = (RectTransform)area.transform;
        areaRt.anchorMin = Vector2.zero;
        areaRt.anchorMax = Vector2.one;
        areaRt.offsetMin = new Vector2(14f, 3f);
        areaRt.offsetMax = new Vector2(-14f, -4f);

        TMP_Text hint = FreeText(area.transform, "Placeholder", placeholder, 24, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.35f));
        hint.fontStyle = FontStyles.Italic;
        Stretch(hint.rectTransform);
        TMP_Text text = FreeText(area.transform, "Text", "", 24, TextAlignmentOptions.Center, Color.white);
        Stretch(text.rectTransform);

        TMP_InputField field = go.AddComponent<TMP_InputField>();
        field.targetGraphic = bg;
        field.textViewport = areaRt;
        field.textComponent = text;
        field.placeholder = hint;

        if (sideInset <= 0f)
        {
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }
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

    // A plain white square sprite (Assets/UI/Assets/Solid.png, made on first use) for bars that must be opaque to the edge.
    static Sprite SolidSprite()
    {
        const string path = "Assets/UI/Assets/Solid.png";
        if (!System.IO.File.Exists(path))
        {
            Texture2D t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            t.SetPixels(px);
            System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteBorder = Vector4.zero;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
