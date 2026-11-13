#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Tools > Gameplay > Build Map Gameplay (newmap)
// Makes the team's new map (newmap) the playable hub. Safe to run again: everything it adds
// lives under "Gameplay (generated)" and is rebuilt each time. The map itself is not touched.
//  - copies the team's prototype Player (sprite-sheet animations, DoorTransition chevron wipe),
//    the minimap canvas/manager/cameras and the 2D light from MinimapTest
//  - one MapZone + minimap RoomData per room, worked out from the tilemaps
//  - Lobby guide NPC, Server Admin (server down), AI Assistant (-> LLM minigame)
//  - quest stations: File Transfer, Wire Game, Harness Game (+ quest panel teleport spots)
//  - dialogue box + interact prompt UI
public static class MapGameplayBuilder
{
    const string SCENE = "Assets/Scenes/newmap.unity";
    const string PROTO = "Assets/Scenes/MinimapTest.unity";       // team's player/door/minimap prototype
    const string ART = "Assets/Minigames/Harness Minigame/FrontEnd/Art/";
    const string ROOT_NAME = "Gameplay (generated)";
    const int MINIMAP_LAYER = 6;

    static Sprite box, circle;
    static TMP_FontAsset font;
    static int baseOrder = 10;        // sorting order for characters (taken from the player)

    class Room
    {
        public string id, name;
        public GameObject root;
        public List<Tilemap> floors = new List<Tilemap>();
        public Bounds bounds;
        public List<Vector3> cells = new List<Vector3>();   // walkable cell centres
        public Vector3 step = Vector3.one;                   // world size of one cell
        public Vector3 centroid;
        public readonly List<Vector3> taken = new List<Vector3>();
    }

    static List<Tilemap> blockers = new List<Tilemap>();
    static List<Room> rooms = new List<Room>();

    [MenuItem("Tools/Gameplay/Build Map Gameplay (newmap)")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        PrepareSprites();
        font = TMP_Settings.defaultFontAsset;

        foreach (var r in scene.GetRootGameObjects())
            if (r.name == ROOT_NAME || r.name == "QuestAreas") Object.DestroyImmediate(r);
        var gen = new GameObject(ROOT_NAME);

        // ---------- rooms from the tilemaps ----------
        bodies.Clear();
        ReadRooms(scene);
        if (rooms.Count == 0) { Debug.LogError("[Gameplay] No rooms (Grid objects) found in " + SCENE); return; }
        var lobby = Find("lobby") ?? rooms[0];

        // ---------- copy the team's prototype pieces ----------
        var copied = CopyFromPrototype(scene, gen);
        var player = copied.player;
        if (player == null) { Debug.LogError("[Gameplay] No player found in " + PROTO); return; }
        var sr = player.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseOrder = sr.sortingOrder;

        // ---------- player ----------
        var spawn = FreeSpot(lobby, lobby.centroid, 2f);
        lobby.taken.Add(spawn);
        player.transform.position = spawn;
        player.tag = "Player";
        if (player.GetComponent<ZoneTracker>() == null) player.AddComponent<ZoneTracker>();
        MinimapIcon(player.transform, new Color32(255, 70, 70, 255), 3f);

        // ---------- UI: interact prompt + dialogue box ----------
        var (interactionUI, dialogueManager) = MakeDialogueUI(gen);
        var pi = player.GetComponent<PlayerInteraction>();
        if (pi == null) pi = player.AddComponent<PlayerInteraction>();
        var pso = new SerializedObject(pi);
        pso.FindProperty("interactionUI").objectReferenceValue = interactionUI;
        pso.FindProperty("dialogueManager").objectReferenceValue = dialogueManager;
        pso.ApplyModifiedPropertiesWithoutUndo();

        // ---------- NPCs ----------
        var guide = MakeNpc(gen, "NPC - Lobby Guide", "Nimbus", new Color32(255, 204, 64, 255),
            FreeSpot(lobby, spawn + new Vector3(2.5f, 1.5f), 2.5f),
            new[]
            {
                ("Nimbus", "Hiya! I'm Nimbus, the NYP Cloud mascot. Yes, an actual cloud. Please don't poke me - I'm 90% water vapour and 10% data."),
                ("Nimbus", "Bad news. The Open House got SO popular that a Traffic Storm of visitors flooded our servers... and the whole NYP Cloud crashed!"),
                ("Nimbus", "No cloud means no files, no AI and no website. Right now it's basically raining error messages in here."),
                ("Nimbus", "Every quest on the left brings one cloud service back online. Fix them all and the sky clears up!"),
                ("Nimbus", "Pick a quest and follow the glowing path on the floor. WASD or arrow keys to walk, E to talk, M for the map."),
                ("Nimbus", "Our Server Admin in the " + NameOf("north_east", "North East Room") + " is panicking the most. Maybe start there?"),
            });
        lobby.taken.Add(guide.transform.position);

        var ne = Find("north_east") ?? Find("north");
        if (ne != null)
        {
            var admin = MakeNpc(gen, "NPC 1 - Server Admin", "Ray (Server Admin)", new Color32(255, 128, 64, 255),
                FreeSpot(ne, ne.centroid + new Vector3(-2.5f, 0), 2.5f),
                new[]
                {
                    ("Ray", "AAAH! A visitor! Sorry, sorry. It's been a long morning. The Traffic Storm knocked out our AI cloud service."),
                    ("Ray", "Normally our AI runs on servers in the cloud. You type a question, the cloud does the heavy thinking, and the answer comes back in seconds."),
                    ("Ray", "But since the crash, our AI Assistant has been... confused. Someone asked it for the canteen menu and it wrote a poem about rice."),
                    ("Ray", "Please go chat with BYTE over there. If anyone can get it thinking straight again, it's you."),
                });
            ne.taken.Add(admin.transform.position);

            var ai = MakeNpc(gen, "NPC 2 - AI Assistant", "BYTE (AI Assistant)", new Color32(80, 220, 140, 255),
                FreeSpot(ne, admin.transform.position + new Vector3(3f, 0), 2.5f),
                new[]
                {
                    ("BYTE", "Hello! I am BYTE, the booth's AI Assistant. I live in the cloud. Well... I did. Now I mostly live in error.log."),
                    ("BYTE", "The crash scrambled my instructions. I still know lots of words, I just forgot how to put them together sensibly."),
                    ("BYTE", "Help me remember how I'm supposed to think, and I'll be answering questions at cloud speed again. Ready?"),
                });
            ne.taken.Add(ai.transform.position);
            var launcher = ai.AddComponent<MinigameLauncher>();
            launcher.questName = "LLM Game";
            UnityEventTools.AddPersistentListener(DialogueCompletedEvent(ai.GetComponent<DialogueTrigger>()), launcher.Launch);
            AddQuestArea(ai, ne, "LLM Game");
        }

        // ---------- quest stations ----------
        Station(gen, Find("showcase") ?? lobby, "Station - File Transfer", "File Transfer", new Color32(170, 120, 255, 255), "Download/Upload Game",
            new[]
            {
                ("Archie", "Welcome to the Showcase Room! Or it would be... if any of our showcase files had actually arrived."),
                ("Archie", "When the cloud crashed, every upload got stuck halfway. Project videos, posters, and someone's 300 cat photos."),
                ("Archie", "Cloud storage is like a giant locker you can open from anywhere. Upload a file once, download it on any device."),
                ("Archie", "Help me get the files uploaded and downloaded to the right places before visitors notice all the empty screens!"),
            });
        Station(gen, Find("south_west") ?? Find("south") ?? lobby, "Station - Wire Game", "Wire Game", new Color32(255, 96, 96, 255), "Wire Game",
            new[]
            {
                ("Network Technician", "Uh oh... when the network crashed, the cables in this server rack got yanked out!"),
                ("Network Technician", "Every server needs the right connection - otherwise data can't travel between the servers and the internet."),
                ("Network Technician", "Match each cable to the right port and get the data flowing again. Can you rewire it?"),
            });
        Station(gen, Find("meeting") ?? Find("staff") ?? lobby, "Station - Harness Game", "Harness Game", new Color32(64, 160, 255, 255), "Harness Game",
            new[]
            {
                ("Pixel", "The booth website crashed too. Right now our homepage just says 'Error 503'. Not the vibe."),
                ("Pixel", "Good news: an AI website builder in the cloud can rebuild the whole page in seconds."),
                ("Pixel", "Bad news: AI only does exactly what you tell it. I once asked it to 'make it pop' and it made a website about popcorn."),
                ("Pixel", "The instructions you give an AI are called a harness. Set it up properly and we're back online!"),
            });

        // ---------- path guide (quest panel -> walking path) ----------
        BakePathGuide(scene, gen);

        // ---------- camera + minimap ----------
        SetupCamera(scene, player);
        SetupMinimap(copied, lobby);
        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = gen;
        Debug.Log("[Gameplay] Built into " + SCENE + ": " + rooms.Count + " rooms (" + string.Join(", ", rooms.Select(r => r.name)) + "). Press Play to test.");
    }

    // ================================================================ rooms
    static void ReadRooms(Scene scene)
    {
        rooms.Clear(); blockers.Clear();
        var roots = scene.GetRootGameObjects().Where(g => g.GetComponent<Grid>() != null).ToList();
        foreach (var root in roots)
            foreach (var tm in root.GetComponentsInChildren<Tilemap>(true))
            {
                var col = tm.GetComponent<TilemapCollider2D>();
                if (col != null && col.enabled) blockers.Add(tm);
            }

        foreach (var root in roots)
        {
            var room = new Room { id = root.name.ToLower(), name = Pretty(root.name), root = root };
            var all = root.GetComponentsInChildren<Tilemap>(true).Where(t => !blockers.Contains(t)).ToList();
            room.floors = all.Where(t => { var n = t.gameObject.name.ToLower(); return n.Contains("floor") || n.Contains("carpet"); }).ToList();
            if (room.floors.Count == 0) room.floors = all;
            if (room.floors.Count == 0) continue;

            bool first = true;
            var seen = new HashSet<Vector2Int>();
            foreach (var tm in room.floors)
            {
                tm.CompressBounds();
                var cs = tm.layoutGrid != null ? tm.layoutGrid.cellSize : Vector3.one;
                var ls = tm.transform.lossyScale;
                room.step = new Vector3(Mathf.Abs(cs.x * ls.x), Mathf.Abs(cs.y * ls.y), 1);
                foreach (var c in tm.cellBounds.allPositionsWithin)
                {
                    if (!tm.HasTile(c)) continue;
                    var w = tm.GetCellCenterWorld(c); w.z = 0;
                    if (first) { room.bounds = new Bounds(w, room.step); first = false; }
                    else room.bounds.Encapsulate(new Bounds(w, room.step));
                    var key = new Vector2Int(Mathf.RoundToInt(w.x * 4), Mathf.RoundToInt(w.y * 4));
                    if (!seen.Add(key)) continue;
                    if (!Blocked(w)) room.cells.Add(w);
                }
            }
            if (room.cells.Count == 0) continue;
            var sum = Vector3.zero; foreach (var c in room.cells) sum += c;
            room.centroid = sum / room.cells.Count;

            var z = root.GetComponent<MapZone>(); if (z == null) z = root.AddComponent<MapZone>();
            z.zoneName = room.name; z.bounds = room.bounds; z.floors = new List<Tilemap>(room.floors);
            EditorUtility.SetDirty(z);
            rooms.Add(room);
        }
    }

    static string Pretty(string n)
    {
        n = n.Replace("SIT_LVL3_", "").Replace("SIT Lvl 3 ", "").Replace("_", " ");
        n = System.Text.RegularExpressions.Regex.Replace(n, "([a-z])([A-Z])", "$1 $2");
        return n.Trim();
    }

    static Room Find(string key)
    {
        key = key.ToLower();
        return rooms.FirstOrDefault(r => r.id.Contains(key));
    }

    static string NameOf(string key, string fallback) { var r = Find(key); return r != null ? r.name : fallback; }

    static bool Blocked(Vector3 w)
    {
        foreach (var b in blockers) if (b != null && b.HasTile(b.WorldToCell(w))) return true;
        return false;
    }

    static bool Walkable(Vector3 w)
    {
        if (Blocked(w)) return false;
        foreach (var r in rooms) foreach (var f in r.floors) if (f.HasTile(f.WorldToCell(w))) return true;
        return false;
    }

    // walkable spot (with walkable neighbours) nearest to 'near', away from things already placed
    static Vector3 FreeSpot(Room room, Vector3 near, float minGap)
    {
        Vector3 best = room.centroid; float bestD = float.MaxValue; bool found = false;
        var s = room.step;
        foreach (var w in room.cells.OrderBy(c => (c - near).sqrMagnitude).Take(4000))
        {
            float d = Vector2.Distance(w, near);
            if (d >= bestD) break;
            if (room.taken.Exists(t => Vector2.Distance(t, w) < minGap)) continue;
            bool ok = true;
            for (int dx = -1; dx <= 1 && ok; dx++)
                for (int dy = -1; dy <= 1 && ok; dy++)
                    if ((dx != 0 || dy != 0) && !Walkable(w + new Vector3(dx * s.x, dy * s.y, 0))) ok = false;
            if (!ok) continue;
            best = w; bestD = d; found = true;
        }
        if (!found) Debug.LogWarning("[Gameplay] No open floor found in " + room.name + " - using its centre.");
        best.z = 0;
        return best;
    }

    // ================================================================ path guide
    static void BakePathGuide(Scene scene, GameObject gen)
    {
        Bounds all = rooms[0].bounds;
        foreach (var r in rooms) all.Encapsulate(r.bounds);
        float cell = rooms.Min(r => Mathf.Min(r.step.x, r.step.y));
        var origin = new Vector2(all.min.x + cell * 0.5f, all.min.y + cell * 0.5f);
        int w = Mathf.CeilToInt(all.size.x / cell), h = Mathf.CeilToInt(all.size.y / cell);
        var walk = new byte[w * h];
        int open = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector3(origin.x + x * cell, origin.y + y * cell, 0);
                bool ok = Walkable(p) && !bodies.Exists(b => Mathf.Abs(b.x - p.x) < 0.75f && Mathf.Abs(b.y - p.y) < 0.75f);
                if (ok) { walk[y * w + x] = 1; open++; }
            }

        var go = new GameObject("Path Guide");
        go.transform.SetParent(gen.transform, false);
        var pg = go.AddComponent<PathGuide>();
        pg.origin = origin; pg.cell = cell; pg.width = w; pg.height = h; pg.walk = walk;
        pg.dotSprite = circle;
        // just above the floor/furniture tiles, below characters + labels
        int top = 0;
        foreach (var tr in Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (tr.gameObject.scene == scene && tr.sortingLayerID == 0) top = Mathf.Max(top, tr.sortingOrder);
        pg.sortingOrder = Mathf.Min(top + 1, baseOrder + 70);
        EditorUtility.SetDirty(pg);
        Debug.Log("[Gameplay] Path guide baked: " + w + "x" + h + " cells, " + open + " walkable.");
    }

    // ================================================================ prototype copy
    class Copied { public GameObject player, canvas, manager; public Camera small, big; }

    static Copied CopyFromPrototype(Scene target, GameObject gen)
    {
        var result = new Copied();
        if (!System.IO.File.Exists(PROTO)) { Debug.LogWarning("[Gameplay] " + PROTO + " not found."); return result; }
        var proto = EditorSceneManager.OpenScene(PROTO, OpenSceneMode.Additive);
        try
        {
            var roots = proto.GetRootGameObjects();
            GameObject pPlayer = roots.FirstOrDefault(g => g.GetComponent<PlayerMovement>() != null);
            GameObject pCanvas = roots.FirstOrDefault(g => g.GetComponent<Canvas>() != null && g.transform.Find("SmallMinimap") != null);
            GameObject pManager = roots.FirstOrDefault(g => g.GetComponent<MinimapManager>() != null);
            Camera pSmall = null, pBig = null;
            foreach (var c in proto.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)))
            {
                if (c.name == "SmallMinimapCamera") pSmall = c;
                if (c.name == "BigMinimapCamera") pBig = c;
            }
            bool needLight = !target.GetRootGameObjects().Any(g => g.GetComponentsInChildren<Component>(true).Any(c => c != null && c.GetType().Name == "Light2D"));
            GameObject pLight = needLight ? roots.FirstOrDefault(g => g.GetComponents<Component>().Any(c => c != null && c.GetType().Name == "Light2D")) : null;

            // copy them together so references between them (player -> blackout panel,
            // minimap manager -> canvas + cameras) stay connected
            var temp = new GameObject("__copy");
            SceneManager.MoveGameObjectToScene(temp, proto);
            foreach (var go in new[] { pPlayer, pCanvas, pManager, pSmall ? pSmall.gameObject : null, pBig ? pBig.gameObject : null, pLight })
                if (go != null) go.transform.SetParent(temp.transform, true);
            var clone = Object.Instantiate(temp);
            SceneManager.MoveGameObjectToScene(clone, target);

            Transform T(string n) => clone.transform.Find(n);
            if (pPlayer) { result.player = T(pPlayer.name).gameObject; result.player.name = "Player"; }
            if (pCanvas) { result.canvas = T(pCanvas.name).gameObject; result.canvas.name = "Minimap + Door Wipe Canvas"; }
            if (pManager) result.manager = T(pManager.name).gameObject;
            if (pSmall) result.small = T(pSmall.name).GetComponent<Camera>();
            if (pBig) result.big = T(pBig.name).GetComponent<Camera>();
            GameObject light = pLight ? T(pLight.name).gameObject : null;

            foreach (var go in new[] { result.player, result.canvas, result.manager, light })
                if (go != null) go.transform.SetParent(gen.transform, true);
            var cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.scene == target && c.CompareTag("MainCamera"));
            foreach (var c in new[] { result.small, result.big })
            {
                if (c == null) continue;
                if (cam != null) { c.transform.SetParent(cam.transform, false); c.transform.localPosition = Vector3.zero; }
                else c.transform.SetParent(gen.transform, true);
            }
            Object.DestroyImmediate(clone);
        }
        finally
        {
            EditorSceneManager.CloseScene(proto, true);   // prototype scene is left exactly as it was
        }
        return result;
    }

    // ================================================================ NPCs + stations
    static GameObject MakeNpc(GameObject gen, string objName, string displayName, Color color, Vector3 pos,
        (string who, string line)[] lines)
    {
        var go = Character(gen, objName, displayName, color, pos, 1.4f);
        var trigger = go.AddComponent<DialogueTrigger>();
        var tso = new SerializedObject(trigger);
        tso.FindProperty("triggerMode").enumValueIndex = (int)DialogueTrigger.TriggerMode.External;
        tso.FindProperty("triggerOnlyOnce").boolValue = false;
        tso.ApplyModifiedPropertiesWithoutUndo();
        var list = (List<DialogueLine>)typeof(DialogueTrigger)
            .GetField("dialogueLines", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trigger);
        list.Clear();
        foreach (var (who, line) in lines)
            list.Add(new DialogueLine { speakerName = who, sentence = line, typingSpeed = 0.02f, onLineEndEvent = new UnityEvent() });

        var it = go.AddComponent<Interactable>();
        var so = new SerializedObject(it);
        so.FindProperty("prompt").stringValue = "Talk to " + displayName;
        so.FindProperty("interactionArea").objectReferenceValue = go.GetComponent<CircleCollider2D>();
        so.FindProperty("dialogueTrigger").objectReferenceValue = trigger;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trigger);
        return go;
    }

    static UnityEvent DialogueCompletedEvent(DialogueTrigger t)
    {
        var f = typeof(DialogueTrigger).GetField("onDialogueCompleted", BindingFlags.NonPublic | BindingFlags.Instance);
        var ev = (UnityEvent)f.GetValue(t);
        if (ev == null) { ev = new UnityEvent(); f.SetValue(t, ev); }
        return ev;
    }

    // a quest station: a short story chat, then its minigame starts
    static void Station(GameObject gen, Room room, string objName, string displayName, Color color, string quest,
        (string who, string line)[] story)
    {
        var pos = FreeSpot(room, room.centroid, 2.5f);
        room.taken.Add(pos);
        var go = MakeNpc(gen, objName, displayName, color, pos, story);
        go.GetComponent<CircleCollider2D>().radius = 1.8f;
        go.transform.Find("Body").localScale = new Vector3(1.1f, 1.1f, 1);
        var so = new SerializedObject(go.GetComponent<Interactable>());
        so.FindProperty("prompt").stringValue = "Start " + displayName;
        so.ApplyModifiedPropertiesWithoutUndo();
        var launcher = go.AddComponent<MinigameLauncher>();
        launcher.questName = quest;
        UnityEventTools.AddPersistentListener(DialogueCompletedEvent(go.GetComponent<DialogueTrigger>()), launcher.Launch);
        AddQuestArea(go, room, quest);
    }

    static readonly List<Vector3> bodies = new List<Vector3>();

    static GameObject Character(GameObject gen, string objName, string displayName, Color color, Vector3 pos, float talkRadius)
    {
        bodies.Add(pos);
        var go = new GameObject(objName);
        go.transform.SetParent(gen.transform, false);
        go.transform.position = pos;
        var trig = go.AddComponent<CircleCollider2D>(); trig.isTrigger = true; trig.radius = talkRadius;
        var body = MakeSprite(go.transform, "Body", box, color, baseOrder);
        body.transform.localScale = new Vector3(0.9f, 0.9f, 1);
        body.gameObject.AddComponent<BoxCollider2D>();          // solid: you bump into it
        var lbl = Label(go.transform, "Name", displayName, Color.white);
        lbl.transform.localPosition = new Vector3(0, 1f, 0);
        MinimapIcon(go.transform, new Color32(255, 220, 60, 255), 2.4f);
        return go;
    }

    // quest panel click -> wipe to a spot next to this object
    static void AddQuestArea(GameObject go, Room room, string quest)
    {
        var area = go.AddComponent<MapArea>();
        area.areaName = room.name;
        area.questName = quest;
        var sp = new GameObject("Quest Spawn Point (move me)");
        sp.transform.SetParent(go.transform, true);
        sp.transform.position = FreeSpot(room, go.transform.position + new Vector3(0, -1.3f, 0), 1.0f);
        area.spawnPoint = sp.transform;
    }

    // ================================================================ dialogue + prompt UI
    static (InteractionUI, DialogueManager) MakeDialogueUI(GameObject gen)
    {
        var canvasGO = new GameObject("Gameplay UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(gen.transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;   // above HUD (10), below home screen (200)
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ART + "Panels/dark_panel.png");

        // interact prompt (bottom centre)
        var btnRT = UIRect("InteractButton", canvasGO.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(640, 72));
        var btnImg = btnRT.gameObject.AddComponent<Image>();
        btnImg.sprite = panelSprite; btnImg.type = Image.Type.Sliced; btnImg.pixelsPerUnitMultiplier = 0.25f;
        var btn = btnRT.gameObject.AddComponent<Button>(); btn.targetGraphic = btnImg;
        var promptLabel = UIText("Label", btnRT, "Talk (E / tap)", 30, Color.white, TextAlignmentOptions.Center);
        promptLabel.textWrappingMode = TextWrappingModes.NoWrap;
        promptLabel.enableAutoSizing = true; promptLabel.fontSizeMin = 18; promptLabel.fontSizeMax = 30;
        Stretch(promptLabel.rectTransform, 16);

        // dialogue box (bottom, full width)
        var root = UIRect("DialogueRoot", canvasGO.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 40), new Vector2(-240, 270));
        root.pivot = new Vector2(0.5f, 0); root.anchoredPosition = new Vector2(0, 40);
        var cg = root.gameObject.AddComponent<CanvasGroup>();
        var boxRT = UIRect("DialogueBox", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var boxImg = boxRT.gameObject.AddComponent<Image>();
        boxImg.sprite = panelSprite; boxImg.type = Image.Type.Sliced; boxImg.pixelsPerUnitMultiplier = 0.25f;
        var nameT = UIText("SpeakerName", boxRT, "", 36, new Color32(255, 210, 90, 255), TextAlignmentOptions.TopLeft);
        Anchor(nameT.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, -28), new Vector2(-96, 50));
        var lineT = UIText("DialogueText", boxRT, "", 32, Color.white, TextAlignmentOptions.TopLeft);
        lineT.rectTransform.anchorMin = Vector2.zero; lineT.rectTransform.anchorMax = Vector2.one;
        lineT.rectTransform.offsetMin = new Vector2(48, 50); lineT.rectTransform.offsetMax = new Vector2(-48, -90);
        var cont = UIText("ContinuePrompt", boxRT, "E / click  >>", 24, new Color32(170, 190, 230, 255), TextAlignmentOptions.BottomRight);
        cont.rectTransform.anchorMin = cont.rectTransform.anchorMax = new Vector2(1, 0);
        cont.rectTransform.pivot = new Vector2(1, 0); cont.rectTransform.sizeDelta = new Vector2(320, 40);
        cont.rectTransform.anchoredPosition = new Vector2(-40, 22);
        root.gameObject.SetActive(false);

        var ui = canvasGO.AddComponent<InteractionUI>();
        var dmGO = new GameObject("DialogueManager");
        dmGO.transform.SetParent(gen.transform, false);
        var dm = dmGO.AddComponent<DialogueManager>();
        var dso = new SerializedObject(dm);
        dso.FindProperty("cinematicRoot").objectReferenceValue = root.gameObject;
        dso.FindProperty("cinematicCanvasGroup").objectReferenceValue = cg;
        dso.FindProperty("cinematicRootRect").objectReferenceValue = root;
        dso.FindProperty("dialogueBoxRect").objectReferenceValue = boxRT;
        dso.FindProperty("cinematicNameText").objectReferenceValue = nameT;
        dso.FindProperty("cinematicSentenceText").objectReferenceValue = lineT;
        dso.FindProperty("cinematicContinuePrompt").objectReferenceValue = cont.gameObject;
        dso.FindProperty("continuePromptRect").objectReferenceValue = cont.rectTransform;
        dso.ApplyModifiedPropertiesWithoutUndo();

        var uso = new SerializedObject(ui);
        uso.FindProperty("interactButton").objectReferenceValue = btn;
        uso.FindProperty("promptLabel").objectReferenceValue = promptLabel;
        uso.FindProperty("dialogueManager").objectReferenceValue = dm;
        uso.ApplyModifiedPropertiesWithoutUndo();
        btnRT.gameObject.SetActive(false);
        return (ui, dm);
    }

    // ================================================================ camera + minimap
    static void SetupCamera(Scene scene, GameObject player)
    {
        var cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(c => c.gameObject.scene == scene && c.CompareTag("MainCamera"));
        if (cam == null) { Debug.LogWarning("[Gameplay] No Main Camera in the map."); return; }
        var follow = cam.GetComponent<CameraFollow>(); if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        var p = player.transform.position;
        cam.transform.position = new Vector3(p.x, p.y, cam.transform.position.z);
        cam.cullingMask &= ~(1 << MINIMAP_LAYER);      // minimap icons only show on the minimap
        EditorUtility.SetDirty(cam);
    }

    static void SetupMinimap(Copied c, Room lobby)
    {
        // a RoomData per room, so the map dropdown + "you are here" use the new map
        var datas = new List<RoomData>();
        Bounds all = rooms[0].bounds;
        foreach (var r in rooms.OrderBy(r => r == lobby ? 0 : 1))
        {
            all.Encapsulate(r.bounds);
            var rd = r.root.GetComponent<RoomData>(); if (rd == null) rd = r.root.AddComponent<RoomData>();
            rd.roomName = r.name;
            rd.cameraPosition = new Vector3(r.bounds.center.x, r.bounds.center.y, 0);
            float fit = Mathf.Max(r.bounds.extents.y, r.bounds.extents.x * 0.6f) + 2f;
            rd.defaultZoom = fit; rd.minZoom = 4; rd.maxZoom = Mathf.Max(fit * 2.5f, 40);
            r.root.GetComponent<MapZone>().minimapRoom = rd;
            EditorUtility.SetDirty(rd); EditorUtility.SetDirty(r.root);
            datas.Add(rd);
        }

        foreach (var cam in new[] { c.small, c.big })
        {
            if (cam == null) continue;
            cam.cullingMask = (1 << 0) | (1 << MINIMAP_LAYER);   // the real map + icons
            EditorUtility.SetDirty(cam);
        }
        if (c.small != null) c.small.orthographicSize = 14;

        var mm = c.manager != null ? c.manager.GetComponent<MinimapManager>() : null;
        if (mm == null) { Debug.LogWarning("[Gameplay] No MinimapManager copied - minimap skipped."); return; }
        mm.allRooms = datas;
        mm.currentRoom = datas[0];
        mm.worldMin = new Vector2(all.min.x, all.min.y);
        mm.worldMax = new Vector2(all.max.x, all.max.y);
        if (mm.smallMinimap != null)
        {
            mm.smallMinimap.SetActive(true);
            var rt = mm.smallMinimap.GetComponent<RectTransform>();
            if (rt != null) { rt.sizeDelta = new Vector2(320, 320); rt.anchoredPosition = new Vector2(-180, -310); }   // below the player name
        }
        EditorUtility.SetDirty(mm);
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include) != null) return;
        var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var t = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (t != null) es.AddComponent(t); else es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    // ================================================================ small helpers
    static void PrepareSprites()
    {
        foreach (var (file, ppu) in new[] { ("box.png", 16f), ("circle.png", 32f) })
        {
            var path = ART + "Gameplay/" + file;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning("[Gameplay] Missing " + path); continue; }
            if (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single
                || !Mathf.Approximately(ti.spritePixelsPerUnit, ppu) || ti.filterMode != FilterMode.Point)
            {
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = ppu; ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
        }
        box = AssetDatabase.LoadAssetAtPath<Sprite>(ART + "Gameplay/box.png");
        circle = AssetDatabase.LoadAssetAtPath<Sprite>(ART + "Gameplay/circle.png");
    }

    static SpriteRenderer MakeSprite(Transform parent, string name, Sprite s, Color c, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.color = c; sr.sortingOrder = order;
        return sr;
    }

    static TextMeshPro Label(Transform parent, string name, string text, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = 3; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = new Vector2(8, 1);
        t.GetComponent<MeshRenderer>().sortingOrder = baseOrder + 81;
        var bg = MakeSprite(go.transform, "Backdrop", box, new Color(0, 0, 0, 0.55f), baseOrder + 80);
        bg.transform.localScale = new Vector3(0.22f * text.Length + 0.3f, 0.42f, 1);
        return t;
    }

    static void MinimapIcon(Transform parent, Color c, float size)
    {
        var old = parent.Find("MinimapIcon"); if (old) Object.DestroyImmediate(old.gameObject);
        var sr = MakeSprite(parent, "MinimapIcon", circle, c, baseOrder + 100);
        sr.gameObject.layer = MINIMAP_LAYER;
        var ls = parent.lossyScale;
        sr.transform.localScale = new Vector3(size / Mathf.Max(0.01f, ls.x), size / Mathf.Max(0.01f, ls.y), 1);
    }

    static RectTransform UIRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0, 1); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(pad, 0); rt.offsetMax = new Vector2(-pad, 0);
    }

    static TextMeshProUGUI UIText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false;
        return t;
    }
}
#endif
