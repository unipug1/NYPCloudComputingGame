using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// One-click restyle of the FileTransferGame scene to the pixel-art design.
/// Menu: Tools > File Transfer Game > Apply Pixel Theme.
/// Safe to run more than once. Undo with Ctrl+Z.
/// </summary>
public static class FileTransferPixelTheme
{
    const string Root = "Assets/Minigames/File Transfer Minigame";
    const string Sprites = Root + "/Sprites/";
    const string Fonts = Root + "/Fonts/";
    const string Ascii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    static readonly Color Navy = Hex("#141B2E");
    static readonly Color Ink = Hex("#0B0F1C");
    static readonly Color Cream = Hex("#FBF6E9");
    static readonly Color Amber = Hex("#F5C04A");
    static readonly Color Emerald = Hex("#3DDC97");
    static readonly Color DeepBlue = Hex("#232A4D");
    static readonly Color TrackBlue = Hex("#2C3560");
    static readonly Color Muted = Hex("#AEB8DA");

    static TMP_FontAsset bodyFont;
    static TMP_FontAsset headFont;

    [MenuItem("Tools/File Transfer Game/Apply Pixel Theme")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform canvas = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == "Canvas") canvas = go.transform;
        }
        Transform quiz = canvas != null ? canvas.Find("QuizWIndow") : null;
        Transform transfer = canvas != null ? canvas.Find("FileTransferWindow") : null;
        if (quiz == null || transfer == null)
        {
            Debug.LogError("[PixelTheme] Open the FileTransferGame scene first (Canvas/QuizWIndow and Canvas/FileTransferWindow not found).");
            return;
        }

        ImportSprites();
        bodyFont = FontAsset(Fonts + "PixelifySans-SemiBold.ttf", Fonts + "PixelifySans-SemiBold SDF.asset");
        headFont = FontAsset(Fonts + "PressStart2P-Regular.ttf", Fonts + "PressStart2P-Regular SDF.asset");
        if (bodyFont == null || headFont == null)
        {
            Debug.LogError("[PixelTheme] Could not create the pixel font assets.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Apply Pixel Theme");

        // Keep the whole 1920x1080 layout visible on any screen shape.
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        StyleQuiz(quiz);
        StyleTransfer(transfer);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[PixelTheme] Pixel theme applied to " + scene.name + ".");
    }

    // ------------------------------------------------------------------ quiz

    static void StyleQuiz(Transform quiz)
    {
        Background(quiz);
        TopBar(quiz, 1);

        RectTransform bubble = quiz.Find("SpeechBubble") as RectTransform;
        Place(bubble, 156, 237, 1456, 262);
        Sliced(bubble.GetComponent<Image>(), "ft_panel", Color.white);

        RectTransform question = bubble.Find("QuestionText") as RectTransform;
        Stretch(question, 48, 24, 60, 36);
        TextMeshProUGUI q = question.GetComponent<TextMeshProUGUI>();
        StyleText(q, bodyFont, 50, Navy, TextAlignmentOptions.Left);
        q.enableAutoSizing = true;
        q.fontSizeMin = 30;
        q.fontSizeMax = 50;

        RectTransform tail = Child(bubble, "Tail", typeof(Image));
        tail.anchorMin = tail.anchorMax = new Vector2(0, 0);
        tail.pivot = new Vector2(0, 1);
        tail.anchoredPosition = new Vector2(72, 20);
        tail.sizeDelta = new Vector2(40, 32);
        Simple(tail.GetComponent<Image>(), "ft_bubble_tail");

        RectTransform cloud = quiz.Find("StatusIcon") as RectTransform;
        Place(cloud, -542, -212, 640, 400);
        Simple(cloud.GetComponent<Image>(), "ft_cloud_neutral");

        string[] letters = { "A", "B", "C" };
        float[] ys = { -35, -195, -355 };
        for (int i = 0; i < 3; i++)
        {
            RectTransform b = quiz.Find("Answer" + (i + 1)) as RectTransform;
            if (b == null) continue;
            Place(b, 375, ys[i], 1014, 134);
            Sliced(b.GetComponent<Image>(), "ft_button", Cream);

            RectTransform label = b.Find("Text (TMP)") as RectTransform;
            if (label != null)
            {
                Stretch(label, 148, 8, 30, 18);
                StyleText(label.GetComponent<TextMeshProUGUI>(), bodyFont, 48, Navy, TextAlignmentOptions.Left);
            }

            // Letter tag. Kept AFTER the label so QuizTask still finds the label first.
            RectTransform tag = Child(b, "Tag", typeof(Image));
            tag.anchorMin = new Vector2(0, 0);
            tag.anchorMax = new Vector2(0, 1);
            tag.pivot = new Vector2(0, 0.5f);
            tag.offsetMin = new Vector2(8, 18);
            tag.offsetMax = new Vector2(116, -8);
            Image tagImage = tag.GetComponent<Image>();
            tagImage.sprite = null;
            tagImage.color = Navy;
            tagImage.raycastTarget = false;
            tag.SetAsLastSibling();
            RectTransform letter = Child(tag, "Letter", typeof(TextMeshProUGUI));
            Stretch(letter, 0, 0, 0, 0);
            TextMeshProUGUI lt = letter.GetComponent<TextMeshProUGUI>();
            lt.text = letters[i];
            StyleText(lt, headFont, 30, Amber, TextAlignmentOptions.Center);
        }

        QuizTask task = quiz.GetComponent<QuizTask>();
        if (task != null)
        {
            task.neutralSprite = Load("ft_cloud_neutral");
            task.correctSprite = Load("ft_cloud_happy");
            task.wrongSprite = Load("ft_cloud_sleep");
            task.normalButtonColor = Cream;
            EditorUtility.SetDirty(task);
        }
    }

    // -------------------------------------------------------------- transfer

    static void StyleTransfer(Transform win)
    {
        RectTransform winRect = win as RectTransform;
        Stretch(winRect, 0, 0, 0, 0);
        Background(win);

        RectTransform panel = Child(win, "Panel", typeof(Image));
        Place(panel, 6, -66, 1492, 732);
        Sliced(panel.GetComponent<Image>(), "ft_panel", Color.white);
        panel.GetComponent<Image>().raycastTarget = false;
        panel.SetAsFirstSibling();

        TopBar(win, 2);

        RectTransform file = win.Find("FileIcon") as RectTransform;
        Place(file, -470, 110, 200, 270);
        Simple(file.GetComponent<Image>(), "ft_icon_file");

        RectTransform folder = win.Find("FolderIcon") as RectTransform;
        Place(folder, 470, 75, 240, 200);
        Simple(folder.GetComponent<Image>(), "ft_icon_folder");

        RectTransform middle = win.Find("MiddleIcon") as RectTransform;
        Place(middle, 0, 90, 192, 192);
        Simple(middle.GetComponent<Image>(), "ft_arrow_idle");

        Label(win, "FileLabel", "report.docx", -470, -70, 420);
        Label(win, "FolderLabel", "Cloud Vault", 470, -70, 420);

        // Progress bar: navy frame, dark segment track, green segment fill.
        RectTransform bar = win.Find("ProgressBar") as RectTransform;
        Place(bar, -101, -190, 1102, 76);
        Slider slider = bar.GetComponent<Slider>();
        if (slider != null)
        {
            slider.interactable = false;           // players should not drag the bar
            slider.transition = Selectable.Transition.None;
        }
        RectTransform barBg = bar.Find("Background") as RectTransform;
        if (barBg != null)
        {
            Stretch(barBg, 0, 0, 0, 0);
            Image bg = barBg.GetComponent<Image>();
            bg.sprite = null;
            bg.color = Navy;
            RectTransform track = Child(barBg, "Track", typeof(Image));
            Stretch(track, 14, 14, 14, 14);
            Tiled(track.GetComponent<Image>(), "ft_bar_segment", TrackBlue);
        }
        RectTransform fillArea = bar.Find("Fill Area") as RectTransform;
        if (fillArea != null)
        {
            Stretch(fillArea, 14, 14, 14, 14);
            RectTransform fill = fillArea.Find("Fill") as RectTransform;
            if (fill != null)
            {
                fill.sizeDelta = Vector2.zero;
                fill.anchoredPosition = Vector2.zero;
                Tiled(fill.GetComponent<Image>(), "ft_bar_segment", Emerald);
            }
        }

        RectTransform percent = win.Find("PercentageText") as RectTransform;
        Place(percent, 565, -190, 190, 76);
        TextMeshProUGUI pt = percent.GetComponent<TextMeshProUGUI>();
        StyleText(pt, headFont, 36, Navy, TextAlignmentOptions.Right);

        RectTransform status = win.Find("StatusText") as RectTransform;
        Place(status, 0, -310, 1300, 90);
        TextMeshProUGUI st = status.GetComponent<TextMeshProUGUI>();
        st.text = "";
        StyleText(st, headFont, 32, Navy, TextAlignmentOptions.Center);

        RectTransform start = win.Find("StartButton") as RectTransform;
        Place(start, 5, -315, 430, 130);
        Sliced(start.GetComponent<Image>(), "ft_button", Emerald);
        RectTransform startLabel = Child(start, "Label", typeof(TextMeshProUGUI));
        Stretch(startLabel, 8, 8, 18, 18);
        TextMeshProUGUI sl = startLabel.GetComponent<TextMeshProUGUI>();
        sl.text = "START";
        StyleText(sl, headFont, 36, Ink, TextAlignmentOptions.Center);

        FileTransferTask task = win.GetComponent<FileTransferTask>();
        if (task != null)
        {
            task.neutralArrowSprite = Load("ft_arrow_idle");
            task.activeArrowSprite = Load("ft_arrow_active");
            task.checkmarkSprite = Load("ft_icon_check");
            EditorUtility.SetDirty(task);
        }
    }

    // --------------------------------------------------------------- shared

    static void Background(Transform window)
    {
        Image img = window.GetComponent<Image>();
        if (img != null) Tiled(img, "ft_bg_tile", Color.white);
    }

    static void TopBar(Transform window, int activeStep)
    {
        RectTransform bar = Child(window, "TopBar", typeof(Image));
        bar.anchorMin = new Vector2(0, 1);
        bar.anchorMax = new Vector2(1, 1);
        bar.pivot = new Vector2(0.5f, 1);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(0, 112);
        Image barImage = bar.GetComponent<Image>();
        barImage.sprite = null;
        barImage.color = Navy;
        barImage.raycastTarget = false;

        RectTransform edge = Child(bar, "Edge", typeof(Image));
        edge.anchorMin = new Vector2(0, 0);
        edge.anchorMax = new Vector2(1, 0);
        edge.pivot = new Vector2(0.5f, 0);
        edge.anchoredPosition = Vector2.zero;
        edge.sizeDelta = new Vector2(0, 8);
        Image edgeImage = edge.GetComponent<Image>();
        edgeImage.sprite = null;
        edgeImage.color = Ink;
        edgeImage.raycastTarget = false;

        RectTransform title = Child(bar, "Title", typeof(TextMeshProUGUI));
        title.anchorMin = new Vector2(0, 0);
        title.anchorMax = new Vector2(0, 1);
        title.pivot = new Vector2(0, 0.5f);
        title.offsetMin = new Vector2(56, 8);
        title.offsetMax = new Vector2(956, 0);
        TextMeshProUGUI tt = title.GetComponent<TextMeshProUGUI>();
        tt.text = "FILE TRANSFER TERMINAL";
        StyleText(tt, headFont, 28, Amber, TextAlignmentOptions.Left);

        Chip(bar, "StepVerify", "1 VERIFY", -316, 200, activeStep == 1);
        Chip(bar, "StepTransfer", "2 TRANSFER", -56, 240, activeStep == 2);
    }

    static void Chip(RectTransform bar, string name, string text, float right, float width, bool active)
    {
        RectTransform chip = Child(bar, name, typeof(Image));
        chip.anchorMin = chip.anchorMax = new Vector2(1, 0.5f);
        chip.pivot = new Vector2(1, 0.5f);
        chip.anchoredPosition = new Vector2(right, 4);
        chip.sizeDelta = new Vector2(width, 56);
        Sliced(chip.GetComponent<Image>(), "ft_chip", active ? Emerald : DeepBlue);
        chip.GetComponent<Image>().raycastTarget = false;

        RectTransform label = Child(chip, "Label", typeof(TextMeshProUGUI));
        Stretch(label, 0, 0, 0, 0);
        TextMeshProUGUI t = label.GetComponent<TextMeshProUGUI>();
        t.text = text;
        StyleText(t, headFont, 20, active ? Ink : Muted, TextAlignmentOptions.Center);
    }

    static void Label(Transform parent, string name, string text, float x, float y, float width)
    {
        RectTransform rt = Child(parent, name, typeof(TextMeshProUGUI));
        Place(rt, x, y, width, 60);
        TextMeshProUGUI t = rt.GetComponent<TextMeshProUGUI>();
        if (string.IsNullOrEmpty(t.text) || t.text == "New Text") t.text = text;
        StyleText(t, bodyFont, 38, Navy, TextAlignmentOptions.Center);
    }

    // -------------------------------------------------------------- helpers

    static RectTransform Child(Transform parent, string name, params Type[] components)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing as RectTransform;
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        foreach (Type c in components) go.AddComponent(c);
        Undo.RegisterCreatedObjectUndo(go, "Apply Pixel Theme");
        return go.GetComponent<RectTransform>();
    }

    /// <summary>Centre-anchored rect at (x, y) from the screen centre.</summary>
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one;
    }

    static void Stretch(RectTransform rt, float left, float top, float right, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static void StyleText(TextMeshProUGUI t, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = FontStyles.Normal;
        t.margin = Vector4.zero;
        t.enableAutoSizing = false;
        t.raycastTarget = false;
        EditorUtility.SetDirty(t);
    }

    static Sprite Load(string name)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + name + ".png");
        if (s == null) Debug.LogWarning("[PixelTheme] Missing sprite: " + name);
        return s;
    }

    static void Simple(Image img, string sprite)
    {
        img.sprite = Load(sprite);
        img.type = Image.Type.Simple;
        img.color = Color.white;
        img.preserveAspect = true;
        EditorUtility.SetDirty(img);
    }

    static void Sliced(Image img, string sprite, Color color)
    {
        img.sprite = Load(sprite);
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.pixelsPerUnitMultiplier = 1;
        img.color = color;
        EditorUtility.SetDirty(img);
    }

    static void Tiled(Image img, string sprite, Color color)
    {
        img.sprite = Load(sprite);
        img.type = Image.Type.Tiled;
        img.pixelsPerUnitMultiplier = 1;
        img.color = color;
        img.raycastTarget = false;
        EditorUtility.SetDirty(img);
    }

    static Color Hex(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }

    static void ImportSprites()
    {
        Sprite9("ft_panel", new Vector4(8, 20, 20, 8), false);
        Sprite9("ft_button", new Vector4(8, 18, 18, 8), false);
        Sprite9("ft_chip", new Vector4(6, 6, 6, 6), false);
        Sprite9("ft_bar_segment", Vector4.zero, true);
        Sprite9("ft_bg_tile", Vector4.zero, true);
        foreach (string n in new[] { "ft_cloud_neutral", "ft_cloud_happy", "ft_cloud_sleep", "ft_icon_file", "ft_icon_folder",
                                     "ft_arrow_idle", "ft_arrow_active", "ft_icon_check", "ft_bubble_tail" })
        {
            Sprite9(n, Vector4.zero, false);
        }
    }

    /// <summary>Pixel-art import settings. border = (left, bottom, right, top).</summary>
    static void Sprite9(string name, Vector4 border, bool tile)
    {
        string path = Sprites + name + ".png";
        TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null)
        {
            Debug.LogWarning("[PixelTheme] Missing texture: " + path);
            return;
        }
        TextureImporterSettings s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.textureType = TextureImporterType.Sprite;
        s.spriteMode = (int)SpriteImportMode.Single;
        s.spritePixelsPerUnit = 100;
        s.spriteBorder = border;
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.filterMode = FilterMode.Point;
        s.mipmapEnabled = false;
        s.alphaIsTransparency = true;
        s.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.SetTextureSettings(s);
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
    }

    static TMP_FontAsset FontAsset(string ttfPath, string assetPath)
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            Debug.LogError("[PixelTheme] Missing font file: " + ttfPath);
            return null;
        }
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (fa == null) return null;
        fa.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(fa, assetPath);
        fa.atlasTexture.name = fa.name + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
        fa.material.name = fa.name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.TryAddCharacters(Ascii);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }
}
