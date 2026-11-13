#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

// Builds the whole "Rebuild the Booth Page" mini-game UI, with every button already wired.
//   Tools > Harness > Rebuild Harness scene (all buttons wired)   <- use this one
// The pack folder is found automatically (wherever HarnessMinigame.cs lives).
public static class HarnessUIBuilder
{
    const string SCENE = "Assets/Scenes/Harness_Minigame.unity";
    const string OLD_PREFIX = "OLD ";

    // folder that holds Scenes/, Buttons/, Resources/ ... (parent of Scripts/)
    static string root;
    static string ROOT
    {
        get
        {
            if (root != null) return root;
            foreach (var g in AssetDatabase.FindAssets("HarnessMinigame t:MonoScript"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.EndsWith("/HarnessMinigame.cs")) continue;
                var scripts = System.IO.Path.GetDirectoryName(p).Replace('\\', '/');      // .../HarnessMinigame/Scripts
                root = System.IO.Path.GetDirectoryName(scripts).Replace('\\', '/') + "/";  // .../HarnessMinigame/
                return root;
            }
            root = "Assets/HarnessMinigame/";
            return root;
        }
    }

    // ---------------------------------------------------------------- import settings
    [MenuItem("Tools/Harness/1. Fix Import Settings")]
    public static void FixImportSettings()
    {
        int changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ROOT.TrimEnd('/') }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            // full-screen backgrounds must be ONE sprite (if they get sliced, the panel shows only a strip)
            bool background = path.Contains("/Scenes/") || path.Contains("/Scenes_NoText/");
            bool modeOk = !background || ti.spriteImportMode == SpriteImportMode.Single;
            if (modeOk && ti.textureType == TextureImporterType.Sprite && ti.filterMode == FilterMode.Point &&
                ti.textureCompression == TextureImporterCompression.Uncompressed && !ti.mipmapEnabled) continue;
            ti.textureType = TextureImporterType.Sprite;
            if (background || ti.spriteImportMode == SpriteImportMode.None) ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
            changed++;
        }
        Debug.Log("[Harness] Import settings checked (" + changed + " fixed). Folder: " + ROOT);
    }

    // ---------------------------------------------------------------- rebuild the Harness scene
    [MenuItem("Tools/Harness/Rebuild Harness scene (all buttons wired)")]
    public static void RebuildScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(SCENE);
        FixImportSettings();

        // keep the website link if one was already typed in
        string url = null;
        foreach (var old in Object.FindObjectsByType<HarnessMinigame>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!string.IsNullOrEmpty(old.websiteUrl) && !old.websiteUrl.Contains("your-friends-site")) url = old.websiteUrl;
            var top = old.transform.root.gameObject;
            if (top.name == "HarnessCanvas") { Object.DestroyImmediate(top); continue; }   // our previous build: replace it
            if (!top.name.StartsWith(OLD_PREFIX)) top.name = OLD_PREFIX + top.name + " (safe to delete)";
            top.SetActive(false);                                                            // your hand-made one: kept, but switched off
        }

        var hm = Build();
        if (url != null) hm.websiteUrl = url;
        EditorUtility.SetDirty(hm);

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = hm.gameObject;
        Debug.Log("[Harness] Harness scene rebuilt and saved. Press Play to test.");
    }

    // ---------------------------------------------------------------- build
    [MenuItem("Tools/Harness/2. Build Minigame UI (into open scene)")]
    public static void BuildMenu() { Build(); }

    public static HarnessMinigame Build()
    {
        // Canvas (below the shared progress bar, which is sort order 10)
        var canvasGO = new GameObject("HarnessCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            var t = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) es.AddComponent(t); else es.AddComponent<StandaloneInputModule>();
        }

        // Root (keeps a 1920x1080 frame centred on any screen)
        var rootRT = Rect("HarnessMinigame", canvasGO.transform, 0, 0, 1920, 1080);
        rootRT.anchorMin = rootRT.anchorMax = new Vector2(0.5f, 0.5f);
        rootRT.pivot = new Vector2(0.5f, 0.5f);
        rootRT.anchoredPosition = Vector2.zero;
        var hm = rootRT.gameObject.AddComponent<HarnessMinigame>();
        hm.startHidden = false;
        hm.questName = "Harness Game";

        // ===================== 1. STORY =====================
        var story = Panel("StoryPanel", rootRT, "Scenes/S1_Story_bg.png");
        Placeholder("NPC_Portrait (put your character here)", story, 96, 832, 144, 144);
        Text("StoryText", story, 300, 796, 1100, 230, 32, Color.white,
            "Too many visitors crashed the Open House website, and our booth page went down with it! We open in 10 minutes. The AI website builder can make a new page in seconds, but nobody set up its harness. Can you help?");
        Btn("Btn_LetsFixIt", story, "Buttons/btn_lets_fix_it", 1440, 928, hm.GoToBuilder);

        // ===================== 2-4. BUILDER =====================
        var builder = Panel("BuilderPanel", rootRT, "Scenes/S2_Builder_bg.png");
        // x position of card index 0,1,2 (correct card = index 0, shuffled on screen)
        float[] roleX = { 80, 466, 852 }, briefX = { 466, 80, 852 }, rulesX = { 852, 80, 466 };
        hm.roleCards = CardRow(builder, "Role", "Builder/role", roleX, 308, hm.SelectRole);
        hm.briefCards = CardRow(builder, "Brief", "Builder/brief", briefX, 540, hm.SelectBrief);
        hm.rulesCards = CardRow(builder, "Rules", "Builder/rules", rulesX, 772, hm.SelectRules);
        hm.generateButton = Btn("Btn_Generate", builder, "Buttons/btn_generate", 80, 940, hm.Generate).gameObject;
        hm.builderContinueButton = Btn("Btn_ContinueWide", builder, "Buttons/btn_continue_wide", 80, 940, hm.GoToRecap).gameObject;
        hm.builderContinueButton.SetActive(false);
        hm.phoneScreen = Img("PhoneScreen", builder, "Resources/Harness/Phone/screen_empty.png", 1476, 184);
        hm.phoneScreen.raycastTarget = false;
        var frame = Img("PhoneFrame", builder, "Resources/Harness/Phone/phone_frame.png", 1440, 136);
        frame.raycastTarget = false;
        hm.checklistText = Text("Checklist", builder, 1440, 744, 440, 110, 26, Color.white, "");
        Placeholder("NPC_Portrait (put your character here)", builder, 1352, 922, 64, 64);
        hm.meiText = Text("NpcText", builder, 1440, 884, 420, 150, 24, Color.white, "");

        // ===================== 5. RECAP =====================
        var recap = Panel("RecapPanel", rootRT, "Scenes/S5_Recap_bg.png");
        Btn("Btn_Continue", recap, "Buttons/btn_continue", 1480, 930, hm.GoToQuiz);

        // ===================== 6. QUIZ =====================
        var quiz = Panel("QuizPanel", rootRT, "Scenes/S6_Quiz_bg.png");
        hm.answerCards = new Image[3];
        float[] ansY = { 380, 504, 628 };
        for (int i = 0; i < 3; i++)
        {
            var b = CardBtn("Answer_" + "ABC"[i], quiz, "Quiz/answer_" + "ABC"[i] + "_normal", 288, ansY[i]);
            UnityEventTools.AddIntPersistentListener(b.onClick, hm.Answer, i);
            hm.answerCards[i] = b.image;
        }
        hm.explanationText = Text("Explanation", quiz, 288, 760, 960, 100, 28, Hex("#1F2A36"), "");
        hm.quizContinueButton = Btn("Btn_Continue", quiz, "Buttons/btn_continue", 1272, 856, hm.GoToYourTurn).gameObject;
        hm.quizContinueButton.SetActive(false);

        // ===================== 7. YOUR TURN =====================
        var yt = Panel("YourTurnPanel", rootRT, "Scenes/S7_YourTurn_bg.png");
        float[] fourX = { 80, 340, 600, 860 }, threeX = { 80, 426, 772 };
        hm.topicCards = CardRow(yt, "Topic", "YourTurn/topic", fourX, 308, hm.SelectTopic);
        hm.vibeCards = CardRow(yt, "Vibe", "YourTurn/vibe", threeX, 664, hm.SelectVibe);
        hm.mustCards = CardRow(yt, "Must", "YourTurn/must", fourX, 842, hm.ToggleMust);
        string[] sw = { "blue", "purple", "red", "green" };
        float[] swX = { 92, 220, 348, 476 };
        hm.swatchRings = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            var ring = Img("SwatchRing_" + sw[i], yt, "Swatches/swatch_selected_ring.png", swX[i] - 12, 476);
            ring.raycastTarget = false; ring.gameObject.SetActive(false);
            hm.swatchRings[i] = ring.gameObject;
            var s = Img("Swatch_" + sw[i], yt, "Swatches/swatch_" + sw[i] + ".png", swX[i], 488);
            var b = s.gameObject.AddComponent<Button>(); b.targetGraphic = s;
            UnityEventTools.AddIntPersistentListener(b.onClick, hm.SelectColour, i);
        }
        hm.templateText = Text("TemplateText", yt, 1224, 412, 632, 340, 28, Hex("#D5DEE9"), "");
        Btn("Btn_BuildIt", yt, "Buttons/btn_build_it", 1192, 808, hm.BuildItForReal);
        hm.yourTurnError = Text("ErrorText", yt, 1192, 956, 696, 40, 22, Hex("#E24C46"), "");
        hm.yourTurnError.alignment = TextAlignmentOptions.Center;

        // ===================== 8. HANDOFF =====================
        var ho = Panel("HandoffPanel", rootRT, "Scenes/S8_Handoff_bg.png");
        hm.handoffPromptText = Text("PromptText", ho, 272, 432, 976, 240, 26, Hex("#D5DEE9"), "");
        // COPY button inside the prompt box (bottom-right)
        var copyRT = Rect("Btn_CopyPrompt", ho, 1072, 626, 168, 52);
        var copyImg = copyRT.gameObject.AddComponent<Image>();
        copyImg.color = Hex("#2E6FD8");
        var copyBtn = copyRT.gameObject.AddComponent<Button>();
        copyBtn.targetGraphic = copyImg;
        var cc = copyBtn.colors; cc.highlightedColor = Hex("#5B93EE"); cc.pressedColor = Hex("#1F4FA0"); copyBtn.colors = cc;
        UnityEventTools.AddPersistentListener(copyBtn.onClick, hm.CopyPrompt);
        hm.copyLabel = Text("Label", copyRT, 0, 0, 168, 52, 24, Color.white, "COPY");
        hm.copyLabel.alignment = TextAlignmentOptions.Center;
        Btn("Btn_OpenBuilder", ho, "Buttons/btn_open_builder", 240, 730, hm.OpenWebsite);
        Btn("Btn_BackToGame", ho, "Buttons/btn_back_to_game", 240, 860, hm.Close);

        // (no old meter HUD: the shared NYP Cloud Network bar from GameSystems sits on top)
        hm.hud = null;

        // assign panels
        hm.storyPanel = story.gameObject; hm.builderPanel = builder.gameObject; hm.recapPanel = recap.gameObject;
        hm.quizPanel = quiz.gameObject; hm.yourTurnPanel = yt.gameObject; hm.handoffPanel = ho.gameObject;
        foreach (var p in new[] { builder, recap, quiz, yt, ho }) p.gameObject.SetActive(false);  // story shows first

        EditorUtility.SetDirty(hm);
        Selection.activeGameObject = rootRT.gameObject;
        Debug.Log("[Harness] Mini-game UI built. Set 'Website Url' on HarnessMinigame, and drop your NPC portraits into the placeholders.");
        return hm;
    }

    // ---------------------------------------------------------------- helpers
    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);   // top-left
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    static Sprite Load(string rel)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(ROOT + rel);
        if (s == null) Debug.LogWarning("[Harness] Sprite not found: " + ROOT + rel);
        return s;
    }

    static RectTransform Panel(string name, RectTransform parent, string bg)
    {
        var rt = Rect(name, parent, 0, 0, 1920, 1080);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Load(bg);
        img.raycastTarget = true;   // blocks clicks reaching the game behind
        return rt;
    }

    static Image Img(string name, RectTransform parent, string rel, float x, float y, bool native = true)
    {
        var rt = Rect(name, parent, x, y, 100, 100);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Load(rel);
        if (native && img.sprite != null) img.SetNativeSize();
        return img;
    }

    static void Placeholder(string name, RectTransform parent, float x, float y, float w, float h)
    {
        var rt = Rect(name, parent, x, y, w, h);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.25f); img.raycastTarget = false; img.preserveAspect = true;
    }

    static Button Btn(string name, RectTransform parent, string baseRel, float x, float y, UnityAction call)
    {
        var img = Img(name, parent, baseRel + "_normal.png", x, y);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;
        b.spriteState = new SpriteState { pressedSprite = Load(baseRel + "_pressed.png"), highlightedSprite = null };
        UnityEventTools.AddPersistentListener(b.onClick, call);
        return b;
    }

    static Button CardBtn(string name, RectTransform parent, string cardRel, float x, float y)
    {
        var img = Img(name, parent, "Resources/Harness/Cards/" + cardRel + ".png", x, y);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.None;   // the script swaps sprites itself
        return b;
    }

    static Image[] CardRow(RectTransform parent, string label, string baseRel, float[] xs, float y, UnityAction<int> call)
    {
        var imgs = new Image[xs.Length];
        for (int i = 0; i < xs.Length; i++)
        {
            var b = CardBtn(label + "_" + i, parent, baseRel + "_" + i + "_normal", xs[i], y);
            UnityEventTools.AddIntPersistentListener(b.onClick, call, i);
            imgs[i] = b.image;
        }
        return imgs;
    }

    static TMP_Text Text(string name, RectTransform parent, float x, float y, float w, float h, float size, Color col, string content)
    {
        var rt = Rect(name, parent, x, y, w, h);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = content; t.fontSize = size; t.color = col;
        t.textWrappingMode = TextWrappingModes.Normal; t.richText = true; t.raycastTarget = false;
        t.alignment = TextAlignmentOptions.TopLeft;
        return t;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
#endif
