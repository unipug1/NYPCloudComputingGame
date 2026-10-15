using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds the inspectable task 3 example using Unity's own scene APIs.</summary>
public static class Task3SceneSetup
{
    public const string ScenePath = "Assets/Scenes/MovementTesting.unity";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string RequestPath = "Temp/CampusTask3.setup-request";
    private const string ResultPath = "Temp/CampusTask3.setup-result.json";
    private const string VerifySessionKey = "CampusTask3.VerifyAfterEnteringPlay";
    private static readonly Stack<IEnumerator> verificationSteps = new Stack<IEnumerator>();
    private static readonly List<string> verifiedChecks = new List<string>();
    private static Touchscreen verificationTouchscreen;
    private static Keyboard verificationKeyboard;
    private static Keyboard originalKeyboard;
    private static InputSettings originalInputSettings;
    private static InputSettings verificationInputSettings;
    private static double verificationDeadline;
    private static int verificationFrame = -1;
    private static readonly Color Ink = new Color(0.055f, 0.09f, 0.16f);
    private static readonly Color Gold = new Color(1f, 0.78f, 0.30f);

    [MenuItem("Tools/Campus Game/Set Up Task 3")]
    public static void SetUpTask3()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before setting up task 3.");

        Scene scene = SceneManager.GetActiveScene();
        if (!string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        GameObject existingCanvas = FindRoot(scene, "InteractionCanvas");
        if (existingCanvas != null && existingCanvas.GetComponent<InteractionUI>() != null)
        {
            ValidateScene(scene);
            Debug.Log("Task 3 is already set up. Existing objects and settings were kept.");
            Selection.activeGameObject = existingCanvas;
            return;
        }

        string[] reservedNames = { "InteractionTargets", "InteractionCanvas", "DialogueManager" };
        if (reservedNames.Any(name => FindRoot(scene, name) != null))
            throw new InvalidOperationException("A task 3 object name is already in use. Check the Hierarchy before setup.");

        GameObject player = FindRoot(scene, "Player");
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        SpriteRenderer playerRenderer = player != null ? player.GetComponent<SpriteRenderer>() : null;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (movement == null || playerRenderer == null || playerRenderer.sprite == null || font == null)
            throw new InvalidOperationException("The test scene needs PlayerMovement, Player's sprite, and the existing TMP font.");

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set up task 3 interactions");

        try
        {
            Undo.RecordObject(player, "Tag Player");
            player.tag = "Player";

            GameObject targets = CreateObject("InteractionTargets", null);
            DialogueManager manager = Undo.AddComponent<DialogueManager>(CreateObject("DialogueManager", null));
            InteractionUI ui = CreateInterface(manager, font);

            PlayerInteraction playerInteraction = player.GetComponent<PlayerInteraction>();
            if (playerInteraction == null)
                playerInteraction = Undo.AddComponent<PlayerInteraction>(player);
            SetReferences(playerInteraction, "interactionUI", ui, "dialogueManager", manager, "movement", movement);

            CreateConversationTarget(targets.transform, playerRenderer, "TestSign", new Vector2(-3f, 0f),
                Gold, "Read sign", "Campus Notice", new[]
                {
                    "Welcome to campus! Explore the lobby, speak to the student helper, and try opening the purple door."
                });

            CreateConversationTarget(targets.transform, playerRenderer, "TestNPC", new Vector2(-3f, 3.5f),
                new Color(0.18f, 0.82f, 0.72f), "Talk", "Student Helper", new[]
                {
                    "Hi! Welcome to the campus lobby.",
                    "Use WASD or the arrow keys to move. Approach an object, then press E or tap its interaction button.",
                    "The purple door is another interaction you can try. Close this conversation when you are ready."
                });

            CreateDoor(targets.transform, playerRenderer);
            ConfigureEventSystem(scene);

            ValidateScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Unity could not save MovementTesting.");

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = targets;
            Debug.Log("Task 3 saved in MovementTesting: PlayerInteraction, three targets, interaction UI, dialogue and touch input.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    // -executeMethod Task3SceneSetup.BuildFromCommandLine can use the same setup.
    public static void BuildFromCommandLine()
    {
        SetUpTask3();
    }

    [MenuItem("Tools/Campus Game/Check Task 3 Setup")]
    public static void CheckTask3Setup()
    {
        ValidateScene(SceneManager.GetActiveScene());
        Debug.Log("Task 3 setup check passed: targets, trigger areas, buttons, dialogue and player references are connected.");
    }

    private static InteractionUI CreateInterface(DialogueManager manager, TMP_FontAsset font)
    {
        GameObject canvasObject = CreateObject("InteractionCanvas", null, typeof(RectTransform));
        Canvas canvas = Undo.AddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        Undo.AddComponent<GraphicRaycaster>(canvasObject);
        canvasObject.layer = 5;

        Button interact = CreateButton("InteractButton", canvasObject.transform, "Interact (E / tap)", font,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(360f, 64f), Gold, Ink);

        RectTransform panel = CreateRect("DialoguePanel", canvasObject.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(900f, 286f));
        Image background = Undo.AddComponent<Image>(panel.gameObject);
        background.color = new Color(Ink.r, Ink.g, Ink.b, 0.98f);
        CanvasGroup group = Undo.AddComponent<CanvasGroup>(panel.gameObject);

        TMP_Text speaker = CreateText("SpeakerName", panel, "", font, 28f, Gold,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -22f), new Vector2(836f, 40f));
        TMP_Text sentence = CreateText("DialogueText", panel, "", font, 26f, Color.white,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -78f), new Vector2(836f, 110f));

        Button next = CreateButton("ContinueButton", panel, "Continue", font,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-190f, 22f), new Vector2(180f, 54f), Gold, Ink);
        Button close = CreateButton("CloseButton", panel, "Close", font,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 22f), new Vector2(144f, 54f),
            new Color(0.19f, 0.25f, 0.35f), Color.white);

        SetReferences(manager, "cinematicRoot", panel.gameObject, "cinematicCanvasGroup", group,
            "cinematicRootRect", panel, "dialogueBoxRect", panel,
            "cinematicNameText", speaker, "cinematicSentenceText", sentence,
            "cinematicContinuePrompt", next.gameObject);
        SerializedObject managerData = new SerializedObject(manager);
        managerData.FindProperty("pointerInputHandledByButtons").boolValue = true;
        managerData.ApplyModifiedProperties();

        InteractionUI ui = Undo.AddComponent<InteractionUI>(canvasObject);
        SetReferences(ui, "interactButton", interact,
            "promptLabel", interact.transform.Find("Label").GetComponent<TMP_Text>(),
            "continueButton", next, "closeButton", close, "dialogueManager", manager);

        next.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);
        interact.gameObject.SetActive(false);
        return ui;
    }

    private static void CreateConversationTarget(Transform parent, SpriteRenderer template, string name,
        Vector2 position, Color color, string prompt, string speaker, string[] sentences)
    {
        GameObject root = CreateObject(name, parent);
        root.transform.localPosition = position;
        CircleCollider2D area = Undo.AddComponent<CircleCollider2D>(root);
        area.isTrigger = true;
        area.radius = 1.8f;

        GameObject visual = CreateVisual("Visual", root.transform, template, color, new Vector2(0.9f, 1.2f));
        Undo.AddComponent<BoxCollider2D>(visual);

        DialogueTrigger trigger = Undo.AddComponent<DialogueTrigger>(root);
        SerializedObject triggerData = new SerializedObject(trigger);
        triggerData.FindProperty("triggerMode").enumValueIndex = (int)DialogueTrigger.TriggerMode.External;
        triggerData.FindProperty("triggerOnlyOnce").boolValue = false;
        SerializedProperty lines = triggerData.FindProperty("dialogueLines");
        lines.arraySize = sentences.Length;
        for (int index = 0; index < sentences.Length; index++)
        {
            SerializedProperty line = lines.GetArrayElementAtIndex(index);
            line.FindPropertyRelative("speakerName").stringValue = speaker;
            line.FindPropertyRelative("sentence").stringValue = sentences[index];
            line.FindPropertyRelative("typingSpeed").floatValue = 0.015f;
            line.FindPropertyRelative("hasChoices").boolValue = false;
        }
        triggerData.ApplyModifiedProperties();

        Interactable target = Undo.AddComponent<Interactable>(root);
        SetReferences(target, "interactionArea", area, "dialogueTrigger", trigger);
        SetPrompt(target, prompt, false);
    }

    private static void CreateDoor(Transform parent, SpriteRenderer template)
    {
        GameObject root = CreateObject("TestDoor", parent);
        root.transform.localPosition = new Vector2(4.5f, 3.5f);
        CircleCollider2D area = Undo.AddComponent<CircleCollider2D>(root);
        area.isTrigger = true;
        area.radius = 1.8f;

        GameObject closed = CreateVisual("ClosedDoorVisual", root.transform, template,
            new Color(0.65f, 0.46f, 0.92f), new Vector2(1.2f, 2.4f));
        Undo.AddComponent<BoxCollider2D>(closed);
        GameObject open = CreateVisual("OpenDoorVisual", root.transform, template,
            new Color(0.3f, 0.85f, 0.55f), new Vector2(0.2f, 2.4f));
        open.transform.localPosition = new Vector2(0.8f, 0f);
        open.SetActive(false);

        Interactable target = Undo.AddComponent<Interactable>(root);
        SetReferences(target, "interactionArea", area);
        SetPrompt(target, "Open door", true);
        UnityEventTools.AddBoolPersistentListener(target.OnInteract, closed.SetActive, false);
        UnityEventTools.AddBoolPersistentListener(target.OnInteract, open.SetActive, true);
        EditorUtility.SetDirty(target);
    }

    private static void ConfigureEventSystem(Scene scene)
    {
        EventSystem eventSystem = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).FirstOrDefault();
        if (eventSystem == null)
            eventSystem = Undo.AddComponent<EventSystem>(CreateObject("EventSystem", null));

        StandaloneInputModule oldModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (oldModule != null)
            Undo.DestroyObjectImmediate(oldModule);
        InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (module == null)
            module = Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
        if (module.actionsAsset == null)
            module.AssignDefaultActions();
    }

    private static GameObject CreateVisual(string name, Transform parent, SpriteRenderer template, Color color, Vector2 size)
    {
        GameObject visual = CreateObject(name, parent);
        visual.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = Undo.AddComponent<SpriteRenderer>(visual);
        renderer.sprite = template.sprite;
        renderer.sharedMaterial = template.sharedMaterial;
        renderer.sortingOrder = template.sortingOrder;
        renderer.color = color;
        return visual;
    }

    private static Button CreateButton(string name, Transform parent, string label, TMP_FontAsset font,
        Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color background, Color foreground)
    {
        RectTransform rect = CreateRect(name, parent, anchor, pivot, position, size);
        Image image = Undo.AddComponent<Image>(rect.gameObject);
        image.color = background;
        Button button = Undo.AddComponent<Button>(rect.gameObject);
        button.targetGraphic = image;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        TMP_Text text = CreateText("Label", rect, label, font, 25f, foreground,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(12f, 8f));
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string contents, TMP_FontAsset font,
        float size, Color color, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 bounds)
    {
        RectTransform rect = CreateRect(name, parent, anchor, pivot, position, bounds);
        TextMeshProUGUI text = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.text = contents;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        GameObject go = CreateObject(name, parent, typeof(RectTransform));
        go.layer = 5;
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static GameObject CreateObject(string name, Transform parent, params Type[] components)
    {
        GameObject go = new GameObject(name, components);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetReferences(UnityEngine.Object target, params object[] references)
    {
        SerializedObject data = new SerializedObject(target);
        for (int index = 0; index < references.Length; index += 2)
            data.FindProperty((string)references[index]).objectReferenceValue = (UnityEngine.Object)references[index + 1];
        data.ApplyModifiedProperties();
    }

    private static void SetPrompt(Interactable target, string prompt, bool once)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty("prompt").stringValue = prompt;
        data.FindProperty("interactOnce").boolValue = once;
        data.ApplyModifiedProperties();
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
    }

    private static void ValidateScene(Scene scene)
    {
        GameObject player = FindRoot(scene, "Player");
        GameObject canvas = FindRoot(scene, "InteractionCanvas");
        GameObject manager = FindRoot(scene, "DialogueManager");
        GameObject targets = FindRoot(scene, "InteractionTargets");
        if (player == null || player.GetComponent<PlayerInteraction>() == null || !player.CompareTag("Player")
            || canvas == null || canvas.GetComponent<InteractionUI>() == null
            || manager == null || manager.GetComponent<DialogueManager>() == null || targets == null)
            throw new InvalidOperationException("Task 3 root objects or Player tag/components are missing.");

        foreach (string name in new[] { "TestSign", "TestNPC", "TestDoor" })
        {
            Transform target = targets.transform.Find(name);
            Interactable component = target != null ? target.GetComponent<Interactable>() : null;
            if (component == null || component.InteractionArea == null || !component.InteractionArea.isTrigger)
                throw new InvalidOperationException(name + " needs an Interactable and a trigger area.");
        }

        RequireReferences(player.GetComponent<PlayerInteraction>(), "interactionUI", "dialogueManager", "movement");
        RequireReferences(canvas.GetComponent<InteractionUI>(), "interactButton", "promptLabel", "continueButton", "closeButton", "dialogueManager");
        RequireReferences(manager.GetComponent<DialogueManager>(), "cinematicRoot", "cinematicNameText", "cinematicSentenceText");
        InputSystemUIInputModule input = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<InputSystemUIInputModule>(true)).FirstOrDefault();
        if (input == null || input.actionsAsset == null)
            throw new InvalidOperationException("The EventSystem needs InputSystemUIInputModule with assigned actions.");
    }

    private static void RequireReferences(UnityEngine.Object target, params string[] fields)
    {
        SerializedObject data = new SerializedObject(target);
        foreach (string field in fields)
            if (data.FindProperty(field).objectReferenceValue == null)
                throw new InvalidOperationException(target.name + ": " + field + " is unassigned.");
    }

    // A one-time request lets this setup run in the already-open Unity editor.
    // It does nothing during ordinary imports unless this task's request file exists.
    [InitializeOnLoadMethod]
    private static void RunRequestedSetup()
    {
        EditorApplication.playModeStateChanged += ResumeVerification;
        if (SessionState.GetBool(VerifySessionKey, false))
            EditorApplication.delayCall += TryStartVerification;

        if (!File.Exists(RequestPath))
            return;

        EditorApplication.delayCall += () =>
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                    || !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Open and save MovementTesting, stop Play mode, then use Tools > Campus Game > Set Up Task 3.");

                bool verify = File.ReadAllText(RequestPath).Trim() == "setup-and-verify";
                SetUpTask3();
                File.WriteAllText(ResultPath, "{\"success\":true,\"scene\":\"" + ScenePath + "\"}");
                if (verify)
                    VerifyTask3InPlayMode();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                File.WriteAllText(ResultPath, JsonUtility.ToJson(new SetupResult { success = false, message = error.Message }));
            }
            finally
            {
                File.Delete(RequestPath);
            }
        };
    }

    [MenuItem("Tools/Campus Game/Verify Task 3 in Play Mode")]
    public static void VerifyTask3InPlayMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before starting verification.");
        ValidateScene(SceneManager.GetActiveScene());
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the scene before running Play mode verification.");
        SessionState.SetBool(VerifySessionKey, true);
        EditorApplication.EnterPlaymode();
    }

    private static void ResumeVerification(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            TryStartVerification();
    }

    private static void TryStartVerification()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(VerifySessionKey, false)
            || verificationSteps.Count != 0)
            return;
        SessionState.SetBool(VerifySessionKey, false);
        verifiedChecks.Clear();
        verificationDeadline = EditorApplication.timeSinceStartup + 45d;
        verificationFrame = -1;
        verificationSteps.Push(CheckInteractions());
        EditorApplication.update += TickVerification;
    }

    private static void TickVerification()
    {
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > verificationDeadline)
                throw new InvalidOperationException("Play mode verification was stopped or timed out.");
            if (verificationFrame == Time.frameCount)
                return;
            verificationFrame = Time.frameCount;
            while (verificationSteps.Count != 0)
            {
                IEnumerator step = verificationSteps.Peek();
                if (!step.MoveNext())
                {
                    verificationSteps.Pop();
                    continue;
                }
                if (step.Current is IEnumerator child)
                {
                    verificationSteps.Push(child);
                    continue;
                }
                return;
            }
            FinishVerification(true, "Keyboard, touch, conversation, movement and door checks passed.");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            FinishVerification(false, error.Message);
        }
    }

    private static IEnumerator CheckInteractions()
    {
        // Simulated test events must go to the game even if the Scene/Console tab has focus.
        // Use an unsaved clone so the project's actual Input Settings asset is never edited.
        originalInputSettings = InputSystem.settings;
        verificationInputSettings = UnityEngine.Object.Instantiate(originalInputSettings);
        verificationInputSettings.hideFlags = HideFlags.HideAndDontSave;
        verificationInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        verificationInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings = verificationInputSettings;
        originalKeyboard = Keyboard.current;
        verificationKeyboard = InputSystem.AddDevice<Keyboard>("Task3VerificationKeyboard");
        yield return WaitFrames(3);
        GameObject player = GameObject.Find("Player");
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Transform canvas = GameObject.Find("InteractionCanvas").transform;
        Button prompt = canvas.Find("InteractButton").GetComponent<Button>();
        Button next = canvas.Find("DialoguePanel/ContinueButton").GetComponent<Button>();
        Button close = canvas.Find("DialoguePanel/CloseButton").GetComponent<Button>();
        TMP_Text speaker = canvas.Find("DialoguePanel/SpeakerName").GetComponent<TMP_Text>();
        TMP_Text sentence = canvas.Find("DialoguePanel/DialogueText").GetComponent<TMP_Text>();
        DialogueManager manager = DialogueManager.Instance;
        Require(interaction.CurrentTarget == null && !prompt.gameObject.activeSelf, "Prompt is hidden when no target is nearby.");
        interaction.TryInteract();
        Require(!manager.IsDialogueRunning, "Interaction from outside every area does nothing.");

        MovePlayer(body, new Vector2(-1.8f, 0f));
        yield return WaitFrames(3);
        Require(interaction.CurrentTarget != null && interaction.CurrentTarget.name == "TestSign"
            && prompt.gameObject.activeSelf, "Approaching the sign shows its prompt.");
        InputSystem.QueueStateEvent(verificationKeyboard, new KeyboardState(Key.E));
        yield return WaitFor(() => manager.IsDialogueRunning);
        InputSystem.QueueStateEvent(verificationKeyboard, new KeyboardState());
        Require(speaker.text == "Campus Notice" && sentence.text.StartsWith("Welcome to campus!"), "E opens the sign's first message.");
        Vector2 stoppedPosition = body.position;
        InputSystem.QueueStateEvent(verificationKeyboard, new KeyboardState(Key.D));
        yield return WaitSeconds(0.2f);
        InputSystem.QueueStateEvent(verificationKeyboard, new KeyboardState());
        Require(movement.IsMovementBlocked && Vector2.Distance(stoppedPosition, body.position) < 0.02f,
            "Movement pauses while the message is open.");
        yield return WaitFor(() => next.gameObject.activeInHierarchy);
        next.onClick.Invoke();
        yield return WaitFor(() => !manager.IsDialogueRunning);
        Require(!movement.IsMovementBlocked, "Finishing the message restores movement.");

        MovePlayer(body, new Vector2(-3f, 1.7f));
        yield return WaitFrames(3);
        Require(interaction.CurrentTarget != null && interaction.CurrentTarget.name == "TestSign", "Overlapping areas select the nearer sign.");
        MovePlayer(body, new Vector2(-3f, 1.8f));
        yield return WaitFrames(3);
        Require(interaction.CurrentTarget != null && interaction.CurrentTarget.name == "TestNPC", "The target changes to the nearer NPC.");

        MovePlayer(body, new Vector2(-1.8f, 3.5f));
        yield return WaitFrames(3);
        verificationTouchscreen = InputSystem.AddDevice<Touchscreen>("Task3VerificationTouch");
        yield return Tap(prompt);
        yield return WaitFor(() => manager.IsDialogueRunning);
        Require(speaker.text == "Student Helper" && sentence.text == "Hi! Welcome to the campus lobby.",
            "A simulated touch opens the NPC's first line without skipping it.");
        yield return WaitFor(() => next.gameObject.activeInHierarchy);
        ScreenCapture.CaptureScreenshot("Temp/CampusTask3-preview.png");
        yield return WaitFrames(2);
        yield return Tap(next);
        yield return WaitFor(() => sentence.text.StartsWith("Use WASD"));
        Require(manager.IsDialogueRunning, "Touching Continue advances to the next NPC line.");
        yield return Tap(close);
        yield return WaitFor(() => !manager.IsDialogueRunning);
        Require(!movement.IsMovementBlocked, "Touching Close cancels typing and restores movement.");

        Transform door = GameObject.Find("InteractionTargets").transform.Find("TestDoor");
        GameObject closed = door.Find("ClosedDoorVisual").gameObject;
        GameObject open = door.Find("OpenDoorVisual").gameObject;
        Require(closed.activeSelf && !open.activeSelf, "The door starts closed with its solid blocker active.");
        MovePlayer(body, new Vector2(3.25f, 3.5f));
        yield return WaitFrames(3);
        Require(interaction.CurrentTarget != null && interaction.CurrentTarget.name == "TestDoor", "Approaching the door selects its prompt.");
        yield return Tap(prompt);
        yield return WaitFrames(2);
        Require(!closed.activeSelf && open.activeSelf && !door.GetComponent<Interactable>().CanInteract,
            "Touch opens the door once and removes its solid blocker.");
        InputSystem.QueueStateEvent(verificationKeyboard, new KeyboardState(Key.D));
        yield return WaitSeconds(0.65f);
        InputSystem.QueueStateEvent(verificationKeyboard, new KeyboardState());
        Require(body.position.x > 5.2f, "The player can walk through the open doorway.");

        MovePlayer(body, Vector2.zero);
        yield return WaitFrames(3);
        Require(!prompt.gameObject.activeSelf && interaction.CurrentTarget == null, "Leaving all targets hides the prompt again.");
    }

    private static void MovePlayer(Rigidbody2D body, Vector2 position)
    {
        body.linearVelocity = Vector2.zero;
        body.position = position;
        Physics2D.SyncTransforms();
    }

    private static IEnumerator WaitFrames(int count)
    {
        int target = Time.frameCount + count;
        while (Time.frameCount < target)
            yield return null;
    }

    private static IEnumerator WaitSeconds(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end)
            yield return null;
    }

    private static IEnumerator WaitFor(Func<bool> condition)
    {
        double end = EditorApplication.timeSinceStartup + 8d;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > end)
                throw new InvalidOperationException("Timed out waiting for the next interaction state.");
            yield return null;
        }
    }

    private static IEnumerator Tap(Button button)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        InputSystem.QueueStateEvent(verificationTouchscreen, new TouchState
        {
            touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = point, pressure = 1f
        });
        yield return WaitFrames(2);
        InputSystem.QueueStateEvent(verificationTouchscreen, new TouchState
        {
            touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = point
        });
        yield return WaitFrames(2);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        verifiedChecks.Add(message);
    }

    private static void FinishVerification(bool success, string message)
    {
        EditorApplication.update -= TickVerification;
        verificationSteps.Clear();
        if (verificationKeyboard != null)
            InputSystem.RemoveDevice(verificationKeyboard);
        verificationKeyboard = null;
        if (originalKeyboard != null && originalKeyboard.added)
            originalKeyboard.MakeCurrent();
        originalKeyboard = null;
        if (verificationTouchscreen != null)
            InputSystem.RemoveDevice(verificationTouchscreen);
        verificationTouchscreen = null;
        if (originalInputSettings != null)
            InputSystem.settings = originalInputSettings;
        originalInputSettings = null;
        if (verificationInputSettings != null)
            UnityEngine.Object.DestroyImmediate(verificationInputSettings);
        verificationInputSettings = null;
        File.WriteAllText("Temp/CampusTask3.play-result.json", JsonUtility.ToJson(new VerificationResult
        {
            success = success, message = message, checks = verifiedChecks.ToArray()
        }, true));
        if (success)
            Debug.Log("Task 3 Play mode checks passed (" + verifiedChecks.Count + ").");
        EditorApplication.isPlaying = false;
    }

    [Serializable]
    private class VerificationResult
    {
        public bool success;
        public string message;
        public string[] checks;
    }

    [Serializable]
    private class SetupResult
    {
        public bool success;
        public string message;
    }
}
