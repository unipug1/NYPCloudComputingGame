#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

// Builds Home / Login / Sign up / Credits + the Cloud Network progress bar + the left Quest panel
// into Assets/Scenes/Harness_Minigame.unity.
//   Menu: Tools > Front End > Build Home + HUD into Harness_Minigame
public static class FrontEndBuilder
{
    const string BASE = "Assets/Minigames/Harness Minigame/FrontEnd/";
    const string ART = BASE + "Art/";
    const string SCENE = "Assets/Scenes/Main scene.unity";          // the team's map: home screen + map live here
    const string HARNESS_SCENE = "Assets/Scenes/Harness_Minigame.unity";
    const string PREFAB = BASE + "GameSystems.prefab";
    const string MARKER = BASE + "Editor/RUN_BUILD_ONCE.txt";
    const float S = 4f;   // pixel-art scale on a 1920x1080 canvas

    static TMP_FontAsset font;

    // ============================================================ import settings
    static void FixImports()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ART.TrimEnd('/') }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 2048;
            ti.spritePixelsPerUnit = 100;
            string f = Path.GetFileNameWithoutExtension(path);
            // spriteBorder = (left, bottom, right, top)
            if (f == "dark_panel") ti.spriteBorder = new Vector4(6, 6, 6, 6);
            else if (f == "quest_panel") ti.spriteBorder = new Vector4(6, 6, 6, 36);
            else if (f.StartsWith("quest_item")) ti.spriteBorder = new Vector4(4, 4, 4, 4);
            else if (f.StartsWith("btn_blank")) ti.spriteBorder = new Vector4(4, 4, 4, 4);
            ti.SaveAndReimport();
        }
    }

    // ============================================================ build
    [MenuItem("Tools/Front End/Build Home + HUD into Main scene")]
    public static void Build()
    {
        FixImports();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // keep the Firebase keys (and front-end settings) across rebuilds
        string fbProject = null, fbKey = null, fbCollection = null;
        int[] askAfter = null;
        var oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
        if (oldPrefab != null)
        {
            var ofb = oldPrefab.GetComponent<FirebaseService>();
            if (ofb != null) { fbProject = ofb.projectId; fbKey = ofb.webApiKey; fbCollection = ofb.collection; }
            var ofe = oldPrefab.GetComponent<FrontEndUI>();
            if (ofe != null) askAfter = ofe.askToSignUpAfter;
        }
        // 1) the Harness game is its own scene now: take the home screen / HUD out of it
        if (File.Exists(HARNESS_SCENE))
        {
            var hs = EditorSceneManager.OpenScene(HARNESS_SCENE, OpenSceneMode.Single);
            foreach (var n in new[] { "GameSystems", "FrontEndCanvas", "GameHUDCanvas", "FrontEndSystems" })
            {
                var o = GameObject.Find(n);
                if (o) UnityEngine.Object.DestroyImmediate(o);
            }
            EditorSceneManager.SaveScene(hs);
        }

        // 2) open (or create) the map scene that holds the home screen
        UnityEngine.SceneManagement.Scene scene;
        if (File.Exists(SCENE)) scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, SCENE);
        }

        // remove an older build so this can be re-run safely
        foreach (var n in new[] { "GameSystems", "FrontEndCanvas", "GameHUDCanvas", "FrontEndSystems" })
        {
            var old = GameObject.Find(n);
            if (old) UnityEngine.Object.DestroyImmediate(old);
        }

        font = FindFont();
        EnsureEventSystem();
        AddScenesToBuild();

        // ---------------- GameSystems: survives scene changes ----------------
        var sys = new GameObject("GameSystems");
        sys.AddComponent<PersistentRoot>();
        var qm = sys.AddComponent<QuestManager>();
        qm.quests.Add(new Quest { questName = "Wire Game", type = QuestType.Main, description = "Connect the wires", sceneName = "" });
        qm.quests.Add(new Quest { questName = "Download/Upload Game", type = QuestType.Main, description = "File transfer", sceneName = "FileTransferGame" });
        qm.quests.Add(new Quest { questName = "LLM Game", type = QuestType.Main, description = "Talk to the AI", sceneName = "LLM_Minigame" });
        qm.quests.Add(new Quest { questName = "Harness Game", type = QuestType.Main, description = "Rebuild the booth page", sceneName = "Harness_Minigame", unlockAfterOthers = true });
        var fe = sys.AddComponent<FrontEndUI>();
        if (askAfter != null && askAfter.Length > 0) fe.askToSignUpAfter = (int[])askAfter.Clone();
        var fb = sys.AddComponent<FirebaseService>();
        if (!string.IsNullOrEmpty(fbProject)) fb.projectId = fbProject;
        if (!string.IsNullOrEmpty(fbKey)) fb.webApiKey = fbKey;
        if (!string.IsNullOrEmpty(fbCollection)) fb.collection = fbCollection;
        var nav = sys.AddComponent<QuestNavigator>();
        nav.mapSceneName = Path.GetFileNameWithoutExtension(SCENE);

        // ============================== GAME HUD (progress bar + quests) ==============================
        var hudCanvas = MakeCanvas("GameHUDCanvas", 10); hudCanvas.SetParent(sys.transform, false);
        var hud = Full("GameHUD", hudCanvas);
        fe.gameHudRoot = hud.gameObject;

        // --- Cloud Network progress bar (top centre) ---
        var barBox = Rect("CloudNetworkBar", hud, 0, 0, 1920, 120);
        barBox.anchorMin = barBox.anchorMax = new Vector2(0.5f, 1); barBox.pivot = new Vector2(0.5f, 1);
        barBox.anchoredPosition = Vector2.zero;
        var label = Text("Label", barBox, 120, 38, 380, 50, 30, Color.white, "NYP CLOUD NETWORK");
        label.alignment = TextAlignmentOptions.Right; Outline(label);
        var empty = Img("BarEmpty", barBox, ART + "Progress/design1_bar_empty.png", 520, 30);
        var fill = Img("BarFill", barBox, ART + "Progress/design1_bar_full.png", 520, 30);
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left; fill.fillAmount = 0;
        var icon = Img("Icon", barBox, ART + "Progress/design1_icon_question.png", 1436, 36);
        var pct = Text("Percent", barBox, 1500, 34, 160, 50, 32, Color.white, "0%");
        var meter = barBox.gameObject.AddComponent<CloudNetworkMeter>();
        meter.fillImage = fill; meter.percentText = pct;
        qm.meter = meter;
        var pi = barBox.gameObject.AddComponent<ProgressBarIcon>();
        pi.fill = fill; pi.icon = icon;
        pi.questionSprite = Load(ART + "Progress/design1_icon_question.png");
        pi.medalSprite = Load(ART + "Progress/design1_icon_medal.png");
        fe.playerNameText = Text("PlayerName", barBox, 1680, 40, 220, 40, 24, new Color32(220, 230, 255, 255), "Guest");

        // --- Quest panel (left) ---
        var slider = Rect("QuestSlider", hud, 0, 0, 480, 900);
        var panel = Img("QuestPanel", slider, ART + "Quest/quest_panel.png", 24, 120, false);
        Sliced(panel, 94 * S, 128 * S);
        var rows = Rect("Rows", panel.rectTransform, 28, 120, 320, 600);

        var template = Img("RowTemplate", rows, ART + "Quest/quest_item_normal.png", 0, 0, false);
        Sliced(template, 80 * S, 16 * S);
        template.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        var rowText = Text("Label", template.rectTransform, 22, 6, 290, 52, 24, new Color32(20, 24, 40, 255), "Quest name");
        rowText.alignment = TextAlignmentOptions.MidlineLeft;
        rowText.textWrappingMode = TextWrappingModes.NoWrap;
        rowText.enableAutoSizing = true; rowText.fontSizeMin = 18; rowText.fontSizeMax = 30;
        var cursor = Img("Cursor", template.rectTransform, ART + "Quest/quest_cursor.png", -26, 16);
        cursor.raycastTarget = false;

        var collapse = Img("Tab_Collapse", slider, ART + "Quest/tab_collapse.png", 24 + 94 * S - 8, 160);
        var expand = Img("Tab_Expand", slider, ART + "Quest/tab_expand.png", 24 + 94 * S - 8, 160);
        var qp = slider.gameObject.AddComponent<QuestPanelUI>();
        AddButton(collapse, qp.Collapse);
        AddButton(expand, qp.Expand);
        qp.slider = slider; qp.panel = panel.rectTransform; qp.rowContainer = rows;
        qp.rowTemplate = template.gameObject; qp.collapseTab = collapse.gameObject; qp.expandTab = expand.gameObject;
        qp.rowNormal = Load(ART + "Quest/quest_item_normal.png");
        qp.rowDone = Load(ART + "Quest/quest_item_selected.png");
        qp.collapsedOffset = -(24 + 94 * S - 8);
        template.gameObject.SetActive(false);

        // area name banner (shown after teleporting)
        var bannerImg = Img("AreaBanner", hud, ART + "Panels/dark_panel.png", (1920 - 900) / 2, 150, false);
        Sliced(bannerImg, 900, 96); bannerImg.raycastTarget = false;
        var bannerText = Text("Text", bannerImg.rectTransform, 0, 0, 900, 96, 44, Color.white, "Area name");
        bannerText.alignment = TextAlignmentOptions.Center;
        nav.banner = bannerImg.gameObject; nav.bannerText = bannerText;
        bannerImg.gameObject.SetActive(false);

        hud.gameObject.SetActive(false);

        // ============================== FRONT END (home / login / sign up / credits) ==============================
        var feCanvas = MakeCanvas("FrontEndCanvas", 200); feCanvas.SetParent(sys.transform, false);
        var root = Full("FrontEnd", feCanvas);
        fe.frontEndRoot = root.gameObject;

        var bg = Full("Background", root).gameObject.AddComponent<Image>();
        fe.homeBackground = bg.gameObject;
        bg.sprite = Load(ART + "Home/home_bg.png");
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fitter.aspectRatio = 1672f / 941f;

        // --- HOME ---
        var home = Full("HomePanel", root);
        var title = Text("Title", home, 0, 150, 1920, 110, 84, Color.white, "NYP OPEN HOUSE");
        title.alignment = TextAlignmentOptions.Center; Outline(title, 0.3f);
        var sub = Text("Subtitle", home, 0, 260, 1920, 60, 38, new Color32(220, 232, 255, 255), "Restore the NYP Cloud Network");
        sub.alignment = TextAlignmentOptions.Center; Outline(sub);
        fe.welcomeText = Text("Welcome", home, 0, 450, 1920, 50, 30, Color.white, "");
        fe.welcomeText.alignment = TextAlignmentOptions.Center;

        // no SIGN UP here: guests are asked to sign up in-game after their 1st and 3rd quest
        var outBtns = Rect("LoggedOutButtons", home, 704, 500, 512, 420);
        HomeButton("Btn_Login", outBtns, "login", 0, fe.ShowLogin, null);
        HomeButton("Btn_PlayAsGuest", outBtns, "guest", 100, fe.PlayAsGuest, null);
        HomeButton("Btn_Credits", outBtns, "credits", 200, fe.ShowCredits, null);
        fe.loggedOutButtons = outBtns.gameObject;

        var inBtns = Rect("LoggedInButtons", home, 704, 500, 512, 420);
        HomeButton("Btn_Play", inBtns, "play", 0, fe.Play, null);
        HomeButton("Btn_Credits", inBtns, "credits", 100, fe.ShowCredits, null);
        HomeButton("Btn_LogOut", inBtns, "blank_grey", 200, fe.LogOut, "LOG OUT");
        fe.loggedInButtons = inBtns.gameObject;
        inBtns.gameObject.SetActive(false);
        fe.homePanel = home.gameObject;

        // --- LOGIN ---
        var login = DarkPanel("LoginPanel", root, 880, 600);
        PanelTitle(login, "LOGIN");
        BackButton(login, fe.LoginBack);
        Text("EmailLabel", login, 120, 170, 640, 40, 28, Color.white, "Email");
        fe.loginEmail = MakeInput("EmailInput", login, 120, 214, "you@example.com", TMP_InputField.ContentType.EmailAddress);
        fe.loginError = ErrorText(login, 300);
        WideButton("Btn_Submit", login, "blank_light", 184, 360, "LOG IN", fe.SubmitLogin);
        fe.loginAltLabel = WideButton("Btn_LoginAlt", login, "blank_navy", 184, 460, "NEW HERE? PLAY AS GUEST", fe.LoginAlt);
        fe.loginPanel = login.parent.gameObject;

        // --- SIGN UP (pop-up over the game after quests; title + reason set by FrontEndUI) ---
        var signup = DarkPanel("SignupPanel", root, 880, 900);
        fe.signupTitle = PanelTitle(signup, "SAVE YOUR PROGRESS");
        // no back arrow here (it covers the long title): MAYBE LATER closes the pop-up
        fe.signupReason = Text("Reason", signup, 100, 116, 680, 64, 26, new Color32(220, 232, 255, 255), "Quest complete! Sign up to keep your progress.");
        fe.signupReason.alignment = TextAlignmentOptions.Center;
        Text("NameLabel", signup, 120, 196, 640, 40, 28, Color.white, "Full name");
        fe.signupName = MakeInput("NameInput", signup, 120, 238, "Your name", TMP_InputField.ContentType.Name);
        Text("EmailLabel", signup, 120, 328, 640, 40, 28, Color.white, "Email");
        fe.signupEmail = MakeInput("EmailInput", signup, 120, 370, "you@example.com", TMP_InputField.ContentType.EmailAddress);
        var note = Text("Note", signup, 120, 456, 640, 40, 22, new Color32(170, 180, 210, 255), "Only used to send your e-certificate.");
        fe.signupError = ErrorText(signup, 494);
        WideButton("Btn_Submit", signup, "blank_light", 184, 560, "CREATE ACCOUNT", fe.SubmitSignup);
        WideButton("Btn_GoLogin", signup, "blank_navy", 184, 660, "HAVE AN ACCOUNT? LOG IN", fe.ShowLogin);
        WideButton("Btn_Later", signup, "blank_grey", 184, 760, "MAYBE LATER", fe.SkipSignup);
        fe.signupPanel = signup.parent.gameObject;

        // --- CREDITS ---
        var credits = DarkPanel("CreditsPanel", root, 880, 600);
        PanelTitle(credits, "CREDITS");
        BackButton(credits, fe.ShowHome);
        var ct = Text("CreditsText", credits, 80, 170, 720, 380, 28, Color.white,
            "NYP Cloud Computing - Open House Game\n\nArt: [name]\nStory: [names]\nGameplay: [names]\nBackend: [names]\n\nMade by NYP students");
        ct.alignment = TextAlignmentOptions.Top;
        fe.creditsPanel = credits.parent.gameObject;

        login.parent.gameObject.SetActive(false); signup.parent.gameObject.SetActive(false); credits.parent.gameObject.SetActive(false);

        // ============================== SCREEN TRANSITIONS (above everything) ==============================
        var fadeCanvas = MakeCanvas("TransitionCanvas", 1000); fadeCanvas.SetParent(sys.transform, false);
        var fadeRoot = Full("Fade", fadeCanvas);
        var cg = fadeRoot.gameObject.AddComponent<CanvasGroup>();
        var solid = fadeRoot.gameObject.AddComponent<Image>();
        solid.color = new Color32(14, 22, 48, 255);                        // NYP navy
        var skyRT = Full("Sky", fadeRoot);
        var skyImg = skyRT.gameObject.AddComponent<Image>();
        skyImg.sprite = Load(ART + "Home/home_bg.png");
        var skyFit = skyRT.gameObject.AddComponent<AspectRatioFitter>();
        skyFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; skyFit.aspectRatio = 1672f / 941f;
        var band = Rect("LoadingBand", fadeRoot, 0, 860, 1920, 110);
        band.gameObject.AddComponent<Image>().color = new Color32(8, 20, 50, 150);
        var loading = Text("Loading", band, 0, 0, 1920, 110, 44, Color.white, "Loading");
        loading.alignment = TextAlignmentOptions.Center;
        var fader = sys.AddComponent<ScreenFader>();
        fader.group = cg; fader.solid = solid; fader.sky = skyRT; fader.label = loading; fader.labelBand = band.gameObject;
        cg.alpha = 0; cg.blocksRaycasts = false;
        fadeRoot.gameObject.SetActive(false);

        EditorUtility.SetDirty(fe);
        AddAreaPlaceholders();

        // save GameSystems as a prefab too, so it can be dropped into the real map scene later
        PrefabUtility.SaveAsPrefabAssetAndConnect(sys, PREFAB, InteractionMode.AutomatedAction);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // the Harness scene gets the same shared bar (via the prefab) so it can be tested on its own
        if (File.Exists(HARNESS_SCENE))
        {
            SetupHarnessScene();
            scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        }
        var sysNow = GameObject.Find("GameSystems");
        if (sysNow) Selection.activeGameObject = sysNow;
        // NOTE: GameSystems lives in this scene and is kept alive (DontDestroyOnLoad) when other quest scenes load.
        Debug.Log("[FrontEnd] Built Home / Login / Credits + sign-up pop-up + progress bar + quest panel into " + SCENE);
    }

    // ============================================================ Harness scene
    // Puts the shared GameSystems prefab (progress bar + quest panel) into the Harness scene so it can be
    // tested on its own. When the game is played from Main_Map, this copy removes itself automatically.
    [MenuItem("Tools/Front End/Set up Harness scene (shared progress bar)")]
    public static void SetupHarnessScene()
    {
        if (!File.Exists(PREFAB)) { Debug.LogWarning("[FrontEnd] Build Main_Map first (Tools > Front End > Build...)."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var hs = EditorSceneManager.OpenScene(HARNESS_SCENE, OpenSceneMode.Single);

        foreach (var root in hs.GetRootGameObjects())
            if (root.name == "GameSystems" || root.name == "FrontEndCanvas" || root.name == "GameHUDCanvas" || root.name == "FrontEndSystems")
                UnityEngine.Object.DestroyImmediate(root);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
        PrefabUtility.InstantiatePrefab(prefab, hs);

        var hm = UnityEngine.Object.FindFirstObjectByType<HarnessMinigame>(FindObjectsInactive.Include);
        if (hm != null)
        {
            hm.startHidden = false;            // the minigame opens as soon as the scene loads
            hm.questName = "Harness Game";
            if (hm.questTrigger != null) { hm.questTrigger.questName = "Harness Game"; EditorUtility.SetDirty(hm.questTrigger); }
            EditorUtility.SetDirty(hm);
        }
        EnsureEventSystem();
        EditorSceneManager.MarkSceneDirty(hs);
        EditorSceneManager.SaveScene(hs);
        Debug.Log("[FrontEnd] Harness scene now uses the shared Cloud Network bar + quest panel.");
    }

    // ============================================================ pieces
    static void HomeButton(string name, RectTransform parent, string key, float y, UnityAction call, string label)
    {
        var img = Img(name, parent, ART + "Home/btn_" + key + "_normal.png", 0, y);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;
        var hover = Load(ART + "Home/btn_" + key + "_hover.png");
        b.spriteState = new SpriteState { highlightedSprite = hover, selectedSprite = hover, pressedSprite = hover };
        UnityEventTools.AddPersistentListener(b.onClick, call);
        if (label != null) Label(img.rectTransform, label, 48, new Color32(20, 20, 30, 255));
        var hint = Img("EnterHint", img.rectTransform, ART + "Home/enter_hint.png", 512 + 16, 20);
        hint.raycastTarget = false;
        img.gameObject.AddComponent<ButtonHoverHint>().hint = hint.gameObject;
        hint.gameObject.SetActive(false);
    }

    static TMP_Text WideButton(string name, RectTransform parent, string key, float x, float y, string label, UnityAction call)
    {
        var img = Img(name, parent, ART + "Home/btn_" + key + "_normal.png", x, y);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;
        var hover = Load(ART + "Home/btn_" + key + "_hover.png");
        b.spriteState = new SpriteState { highlightedSprite = hover, selectedSprite = hover, pressedSprite = hover };
        UnityEventTools.AddPersistentListener(b.onClick, call);
        return Label(img.rectTransform, label, 28, key.Contains("navy") ? Color.white : new Color32(20, 20, 30, 255));
    }

    static void BackButton(RectTransform parent, UnityAction call)
    {
        var img = Img("Btn_Back", parent, ART + "Home/btn_back_normal.png", 32, 28);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img; b.transition = Selectable.Transition.SpriteSwap;
        var hover = Load(ART + "Home/btn_back_hover.png");
        b.spriteState = new SpriteState { highlightedSprite = hover, selectedSprite = hover, pressedSprite = hover };
        UnityEventTools.AddPersistentListener(b.onClick, call);
    }

    static RectTransform DarkPanel(string name, RectTransform parent, float w, float h)
    {
        var holder = Full(name, parent);
        var dim = holder.gameObject.AddComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.45f);                    // darkens the home background
        var p = Img("Panel", holder, ART + "Panels/dark_panel.png", (1920 - w) / 2, (1080 - h) / 2, false);
        Sliced(p, w, h);
        return p.rectTransform;
    }

    static TMP_Text PanelTitle(RectTransform panel, string s)
    {
        var t = Text("Title", panel, 0, 40, panel.sizeDelta.x, 70, 52, Color.white, s);
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }

    static TMP_Text ErrorText(RectTransform panel, float y)
    {
        var t = Text("Error", panel, 120, y, 640, 56, 22, new Color32(255, 120, 110, 255), "");
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 22;
        return t;
    }

    static TMP_InputField MakeInput(string name, RectTransform parent, float x, float y, string placeholder, TMP_InputField.ContentType type)
    {
        var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Place(rt, x, y, 640, 76);
        var img = go.GetComponent<Image>();
        img.sprite = Load(ART + "Quest/quest_item_normal.png");
        img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f / S; img.color = Color.white;
        var field = go.GetComponent<TMP_InputField>();
        field.contentType = type;
        field.characterLimit = 60;
        var area = (RectTransform)go.transform.Find("Text Area");
        if (area) { area.offsetMin = new Vector2(20, 8); area.offsetMax = new Vector2(-20, -8); }
        foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (font) t.font = font;
            t.fontSize = 28;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.color = new Color32(20, 24, 40, 255);
        }
        if (field.placeholder is TMP_Text ph) { ph.text = placeholder; ph.color = new Color32(90, 95, 110, 200); ph.fontStyle = FontStyles.Normal; }
        return field;
    }

    // ============================================================ helpers
    static RectTransform MakeCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080); s.matchWidthOrHeight = 0.5f;
        return (RectTransform)go.transform;
    }

    static RectTransform Full(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        Place(rt, x, y, w, h);
        return rt;
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
    }

    static Sprite Load(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning("[FrontEnd] Sprite not found: " + path);
        return s;
    }

    // native pixel size x4
    static Image Img(string name, Transform parent, string path, float x, float y, bool scaled = true)
    {
        var rt = Rect(name, parent, x, y, 100, 100);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Load(path);
        if (scaled && img.sprite != null) rt.sizeDelta = img.sprite.rect.size * S;
        return img;
    }

    static void Sliced(Image img, float w, float h)
    {
        img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f / S;
        img.rectTransform.sizeDelta = new Vector2(w, h);
    }

    static void AddButton(Image img, UnityAction call)
    {
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        UnityEventTools.AddPersistentListener(b.onClick, call);
    }

    static TMP_Text Text(string name, Transform parent, float x, float y, float w, float h, float size, Color col, string s)
    {
        var rt = Rect(name, parent, x, y, w, h);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = s; t.fontSize = size; t.color = col; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.alignment = TextAlignmentOptions.TopLeft;
        return t;
    }

    static TMP_Text Label(RectTransform btn, string s, float size, Color col)
    {
        var t = Text("Label", btn, 0, 0, btn.sizeDelta.x, btn.sizeDelta.y, size, col, s);
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = size;
        t.margin = new Vector4(16, 0, 16, 0);
        return t;
    }

    // dark shadow behind light text so it reads on the sky background
    static void Outline(TMP_Text t, float w = 0.2f)
    {
        var sh = Text(t.name + "_Shadow", t.transform.parent, 0, 0, 10, 10, t.fontSize, new Color32(8, 30, 80, 220), t.text);
        var a = (RectTransform)t.transform; var b = (RectTransform)sh.transform;
        b.anchorMin = a.anchorMin; b.anchorMax = a.anchorMax; b.pivot = a.pivot; b.sizeDelta = a.sizeDelta;
        b.anchoredPosition = a.anchoredPosition + new Vector2(4, -4);
        sh.alignment = t.alignment;
        sh.transform.SetSiblingIndex(t.transform.GetSiblingIndex());   // behind the real text
    }

    static TMP_FontAsset FindFont()
    {
        foreach (var g in AssetDatabase.FindAssets("KIdDOS-Regular SDF t:TMP_FontAsset"))
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g));
        return TMP_Settings.defaultFontAsset;
    }

    // one MapArea per quest; move them onto the real map areas later
    static void AddAreaPlaceholders()
    {
        if (UnityEngine.Object.FindFirstObjectByType<MapArea>(FindObjectsInactive.Include) != null) return;
        var root = new GameObject("QuestAreas");
        var defs = new (string quest, string area, Vector3 pos)[]
        {
            ("Wire Game", "Area 1 (rename me)", new Vector3(-6, 3, 0)),
            ("Download/Upload Game", "Area 2 (rename me)", new Vector3(6, 3, 0)),
            ("LLM Game", "Area 3 (rename me)", new Vector3(-6, -3, 0)),
            ("Harness Game", "Area 4 (rename me)", new Vector3(6, -3, 0)),
        };
        foreach (var d in defs)
        {
            var go = new GameObject("Area_" + d.quest.Replace("/", "-"));
            go.transform.SetParent(root.transform, false);
            go.transform.position = d.pos;
            var a = go.AddComponent<MapArea>();
            a.questName = d.quest; a.areaName = d.area;
        }
    }

    static void AddScenesToBuild()
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        // the map scene first so the game starts on the home screen
        string[] wanted = { SCENE, "Assets/Scenes/FileTransferGame.unity", "Assets/Scenes/LLM_Minigame.unity", HARNESS_SCENE };
        foreach (var path in wanted)
        {
            if (!File.Exists(path)) continue;
            if (list.Exists(x => x.path == path)) continue;
            list.Add(new EditorBuildSettingsScene(path, true));
        }
        list.RemoveAll(x => x.path == "Assets/Scenes/Main_Map.unity");   // old placeholder map
        var start = list.FindIndex(x => x.path == SCENE);
        if (start > 0) { var s0 = list[start]; list.RemoveAt(start); list.Insert(0, s0); }
        EditorBuildSettings.scenes = list.ToArray();
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (t != null) es.AddComponent(t); else es.AddComponent<StandaloneInputModule>();
    }
}
#endif
