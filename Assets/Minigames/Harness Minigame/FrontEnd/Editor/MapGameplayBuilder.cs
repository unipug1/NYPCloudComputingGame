#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Tools > Gameplay > Build Map Gameplay (Main scene)
// Turns the team's map (Main scene) into the playable hub. Safe to run again: everything it
// creates lives under "Gameplay (generated)" and is rebuilt; the team's own objects are only
// given components (TP pads, File Transfer Game).
//  - Player (circle) with movement + E-interact, camera follow, zone tracking, minimap icon
//  - 4 map sections (MapZone) from the room tilemaps, wall/furniture colliders
//  - "TP to ..." objects become teleport pads (press E); missing links back to the Lobby are added
//  - Lobby guide NPC, Server Admin NPC (server down), AI Assistant NPC (-> LLM minigame)
//  - Quest stations: File Transfer (existing object), Wire Game, Harness Game
//  - Dialogue box + interact prompt UI, minimap pointed at the real map
public static class MapGameplayBuilder
{
    const string SCENE = "Assets/Scenes/Main scene.unity";
    const string ART = "Assets/Minigames/Harness Minigame/FrontEnd/Art/";
    const string ROOT_NAME = "Gameplay (generated)";
    const int MINIMAP_LAYER = 6;

    const string LOBBY = "SIT Lvl 3 Lobby";
    const string SHOWCASE = "SIT Lvl 3 Showcase Room";
    const string NORTH_EAST = "SIT Lvl 3 North + East Room";
    const string SOUTH_WEST = "SIT Lvl 3 South + West Room";

    static Sprite box, circle;
    static TMP_FontAsset font;

    class Room
    {
        public string name;
        public GameObject root;
        public Tilemap floor;
        public List<Tilemap> blockers = new List<Tilemap>();
        public Bounds bounds;
        public MapZone zone;
        public readonly List<Vector3> taken = new List<Vector3>();
    }

    class Pad { public Room from; public Room to; public GameObject go; }

    [MenuItem("Tools/Gameplay/Build Map Gameplay (Main scene)")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        PrepareSprites();
        font = TMP_Settings.defaultFontAsset;

        // ---------- clean up older builds + old test objects ----------
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r.name == ROOT_NAME || r.name == "QuestAreas") UnityEngine.Object.DestroyImmediate(r);
        }
        foreach (var r in scene.GetRootGameObjects())
        {
            // superseded by GameSystems (shared QuestManager + progress bar)
            if (r.name == "QuestManager" || r.name == "CloudNetworkMeter" || r.name == "Cloud Bla")
            {
                r.SetActive(false);
                r.name += " (disabled - replaced by GameSystems)";
            }
        }
        var gen = new GameObject(ROOT_NAME);

        // ---------- rooms ----------
        var rooms = new Dictionary<string, Room>();
        foreach (var n in new[] { LOBBY, SHOWCASE, NORTH_EAST, SOUTH_WEST })
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == n);
            if (root == null) { Debug.LogWarning("[Gameplay] Room not found: " + n); continue; }
            var room = new Room { name = n, root = root };
            foreach (var tm in root.GetComponentsInChildren<Tilemap>(true))
            {
                var tn = tm.gameObject.name.ToLower();
                if (tn == "floor") room.floor = tm;
                else room.blockers.Add(tm);
                // solid walls + furniture so you can't walk through them
                if (tn == "walls" || tn == "furniture")
                {
                    if (tm.GetComponent<TilemapCollider2D>() == null) tm.gameObject.AddComponent<TilemapCollider2D>();
                }
            }
            if (room.floor == null) { Debug.LogWarning("[Gameplay] No Floor tilemap in " + n); continue; }
            room.floor.CompressBounds();
            room.bounds = WorldBounds(room.floor);
            var z = Get<MapZone>(root);
            z.zoneName = n; z.bounds = room.bounds;
            room.zone = z;
            rooms[n] = room;
        }
        if (!rooms.ContainsKey(LOBBY)) { Debug.LogError("[Gameplay] Lobby not found - stopping."); return; }

        // ---------- teleport pads ----------
        var pads = new List<Pad>();
        foreach (var room in rooms.Values)
            foreach (Transform t in room.root.transform)
            {
                if (!t.name.ToLower().StartsWith("tp to")) continue;
                var target = MatchRoom(t.name.Substring(5), rooms.Values);
                if (target == null) { Debug.LogWarning("[Gameplay] Can't tell which room '" + t.name + "' goes to."); continue; }
                pads.Add(new Pad { from = room, to = target, go = t.gameObject });
                room.taken.Add(t.position);
            }
        // every room links to the Lobby and back
        var lobby = rooms[LOBBY];
        foreach (var room in rooms.Values)
        {
            if (room == lobby) continue;
            if (!pads.Exists(p => p.from == lobby && p.to == room))
                pads.Add(new Pad { from = lobby, to = room, go = NewPad(gen, lobby, room) });
            if (!pads.Exists(p => p.from == room && p.to == lobby))
                pads.Add(new Pad { from = room, to = lobby, go = NewPad(gen, room, lobby) });
        }
        var arrivals = new GameObject("Arrival Points (move to change landing spots)");
        arrivals.transform.SetParent(gen.transform, false);
        foreach (var p in pads) SetupPad(p, pads, arrivals.transform);

        // ---------- player ----------
        var spawn = FreeSpot(lobby, lobby.bounds.center, 2.5f);
        var player = MakePlayer(gen, spawn);

        // ---------- UI: interact prompt + dialogue box ----------
        var (interactionUI, dialogueManager) = MakeDialogueUI(gen);
        var pi = player.GetComponent<PlayerInteraction>();
        var so = new SerializedObject(pi);
        so.FindProperty("interactionUI").objectReferenceValue = interactionUI;
        so.FindProperty("dialogueManager").objectReferenceValue = dialogueManager;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ---------- NPCs ----------
        var guide = MakeNpc(gen, "NPC - Lobby Guide", "Guide", new Color32(255, 204, 64, 255),
            FreeSpot(lobby, spawn + new Vector3(2.5f, 1.5f), 2.5f), "Talk",
            new[]
            {
                ("Guide", "Welcome to the NYP Open House! I'm your guide for today."),
                ("Guide", "Bad news... the NYP Cloud Network just went down, and the whole of Level 3 is offline!"),
                ("Guide", "You can help us bring it back. Your quests are on the left of the screen - each one fixes part of the network."),
                ("Guide", "Walk with WASD or the arrow keys. Press E to talk or use things, and press M to open the map."),
                ("Guide", "Stand on a TP pad and press E to travel between rooms. Start in the North + East Room - our server admin needs you!"),
            });
        lobby.taken.Add(guide.transform.position);

        if (rooms.TryGetValue(NORTH_EAST, out var ne))
        {
            var c = ne.bounds.center;
            var admin = MakeNpc(gen, "NPC 1 - Server Admin", "Server Admin", new Color32(255, 128, 64, 255),
                FreeSpot(ne, c + new Vector3(-2.5f, 0), 2.5f), "Talk",
                new[]
                {
                    ("Server Admin", "The server is DOWN! Visitors can't load anything!"),
                    ("Server Admin", "Too many people hit the booth page at once, and our AI services crashed with it."),
                    ("Server Admin", "Our AI Assistant is right over there. It knows how the AI side works - go talk to it!"),
                });
            ne.taken.Add(admin.transform.position);

            var ai = MakeNpc(gen, "NPC 2 - AI Assistant", "AI Assistant", new Color32(80, 220, 140, 255),
                FreeSpot(ne, c + new Vector3(2.5f, 0), 2.5f), "Talk",
                new[]
                {
                    ("AI Assistant", "Hi! I'm the booth's AI assistant... at least I'm supposed to be."),
                    ("AI Assistant", "Since the crash my answers have been all over the place."),
                    ("AI Assistant", "Help me figure out how an AI model really thinks, and we'll get the network back up. Ready?"),
                });
            ne.taken.Add(ai.transform.position);
            // after the chat -> open the LLM minigame
            var launcher = ai.AddComponent<MinigameLauncher>();
            launcher.questName = "LLM Game";
            UnityEventTools.AddPersistentListener(DialogueCompletedEvent(ai.GetComponent<DialogueTrigger>()), launcher.Launch);
            AddQuestArea(ai, ne, "LLM Game");
        }

        // ---------- quest stations ----------
        if (rooms.TryGetValue(SHOWCASE, out var show))
        {
            var ft = show.root.transform.Cast<Transform>().FirstOrDefault(t => t.name == "File Transfer Game");
            if (ft != null) SetupExistingStation(ft.gameObject, show, "Download/Upload Game", "File Transfer", new Color32(170, 120, 255, 255));
            else
            {
                var st = MakeStation(gen, "Station - File Transfer", "File Transfer", new Color32(170, 120, 255, 255),
                    FreeSpot(show, show.bounds.center, 2.5f), "Download/Upload Game");
                AddQuestArea(st, show, "Download/Upload Game");
            }
        }
        if (rooms.TryGetValue(SOUTH_WEST, out var sw))
        {
            var c = sw.bounds.center;
            var wire = MakeStation(gen, "Station - Wire Game", "Wire Game", new Color32(255, 96, 96, 255),
                FreeSpot(sw, c + new Vector3(-2.5f, 0), 2.5f), "Wire Game");
            sw.taken.Add(wire.transform.position);
            AddQuestArea(wire, sw, "Wire Game");
            var harness = MakeStation(gen, "Station - Harness Game", "Harness Game", new Color32(64, 160, 255, 255),
                FreeSpot(sw, c + new Vector3(2.5f, 0), 2.5f), "Harness Game");
            sw.taken.Add(harness.transform.position);
            AddQuestArea(harness, sw, "Harness Game");
        }

        // ---------- camera + minimap ----------
        SetupCamera(player);
        SetupMinimap(rooms);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = gen;
        Debug.Log("[Gameplay] Map gameplay built into " + SCENE + ". Press Play to test.");
    }

    // ================================================================ rooms + spots
    static Bounds WorldBounds(Tilemap tm)
    {
        var cb = tm.cellBounds;
        var a = tm.CellToWorld(cb.min);
        var b = tm.CellToWorld(cb.max);
        var bounds = new Bounds(a, Vector3.zero);
        bounds.Encapsulate(b);
        bounds.Encapsulate(tm.CellToWorld(new Vector3Int(cb.xMin, cb.yMax, 0)));
        bounds.Encapsulate(tm.CellToWorld(new Vector3Int(cb.xMax, cb.yMin, 0)));
        return bounds;
    }

    static string Norm(string s)
    {
        s = s.ToLower().Replace("level", "lvl").Replace("nyp", "");
        return new string(s.Where(char.IsLetterOrDigit).ToArray());
    }

    static Room MatchRoom(string text, IEnumerable<Room> rooms)
    {
        var n = Norm(text);
        Room best = null; int bestScore = 0;
        foreach (var r in rooms)
        {
            var rn = Norm(r.name);
            int score = n == rn ? 1000 : (n.Contains(rn) || rn.Contains(n)) ? 500 : CommonPrefix(n, rn);
            if (score > bestScore) { bestScore = score; best = r; }
        }
        return bestScore >= 6 ? best : null;
    }

    static int CommonPrefix(string a, string b)
    {
        int i = 0; while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
        return i;
    }

    // a walkable floor cell (with free neighbours) nearest to 'near', away from things already placed
    static Vector3 FreeSpot(Room room, Vector3 near, float minGap)
    {
        var tm = room.floor;
        var cb = tm.cellBounds;
        Vector3 best = room.bounds.center; float bestD = float.MaxValue;
        bool found = false;
        for (int x = cb.xMin + 1; x < cb.xMax - 1; x++)
            for (int y = cb.yMin + 1; y < cb.yMax - 1; y++)
            {
                if (!Walkable(room, x, y)) continue;
                bool ok = true;
                for (int dx = -1; dx <= 1 && ok; dx++)
                    for (int dy = -1; dy <= 1 && ok; dy++)
                        if (!Walkable(room, x + dx, y + dy)) ok = false;
                if (!ok) continue;
                var w = tm.GetCellCenterWorld(new Vector3Int(x, y, 0));
                if (room.taken.Exists(t => Vector2.Distance(t, w) < minGap)) continue;
                float d = Vector2.Distance(w, near);
                if (d < bestD) { bestD = d; best = w; found = true; }
            }
        if (!found) Debug.LogWarning("[Gameplay] No free floor found in " + room.name + ", using its centre.");
        best.z = 0;
        return best;
    }

    static bool Walkable(Room room, int x, int y)
    {
        var c = new Vector3Int(x, y, 0);
        if (!room.floor.HasTile(c)) return false;
        foreach (var b in room.blockers) if (b.HasTile(c)) return false;
        return true;
    }

    // ================================================================ teleport pads
    static GameObject NewPad(GameObject gen, Room from, Room to)
    {
        var go = new GameObject("TP to " + to.name);
        go.transform.SetParent(gen.transform, false);
        var c = from.bounds.center;
        var dir = (to.bounds.center - c).normalized;
        go.transform.position = FreeSpot(from, c + dir * Mathf.Min(from.bounds.extents.x, from.bounds.extents.y) * 0.6f, 2.5f);
        go.transform.localScale = new Vector3(1.6f, 1.6f, 1);
        go.AddComponent<BoxCollider2D>();
        from.taken.Add(go.transform.position);
        return go;
    }

    static void SetupPad(Pad p, List<Pad> all, Transform arrivals)
    {
        var go = p.go;
        var col = Get<BoxCollider2D>(go);
        col.isTrigger = true;                         // stand on it + press E

        // landing spot: next to the pad in the other room that leads back here
        var back = all.Find(x => x.from == p.to && x.to == p.from);
        Vector3 near = back != null
            ? back.go.transform.position + (p.to.bounds.center - back.go.transform.position).normalized * 2f
            : p.to.bounds.center;
        var arrive = new GameObject("Arrive in " + p.to.name + " (from " + p.from.name + ")");
        arrive.transform.SetParent(arrivals, false);
        arrive.transform.position = FreeSpot(p.to, near, 1.5f);

        var tp = Get<ZoneTeleporter>(go);
        tp.targetZoneName = p.to.name;
        tp.arrivalPoint = arrive.transform;

        var it = Get<Interactable>(go);
        var so = new SerializedObject(it);
        so.FindProperty("prompt").stringValue = "Go to " + Short(p.to.name);
        so.FindProperty("interactionArea").objectReferenceValue = col;
        so.ApplyModifiedPropertiesWithoutUndo();
        ClearPersistent(it.OnInteract);
        UnityEventTools.AddPersistentListener(it.OnInteract, tp.Go);

        // visuals (rebuilt each run)
        var old = go.transform.Find("Pad Visual"); if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var oldL = go.transform.Find("Pad Label"); if (oldL) UnityEngine.Object.DestroyImmediate(oldL.gameObject);
        var vis = MakeSprite(go.transform, "Pad Visual", box, new Color(0.25f, 0.85f, 1f, 0.45f), 4);
        vis.transform.localScale = new Vector3(col.size.x, col.size.y, 1);
        vis.transform.localPosition = col.offset;
        var label = Label(go.transform, "Pad Label", "TP: " + Short(p.to.name), new Color(0.75f, 0.95f, 1f));
        // keep the label upright + readable whatever the pad's scale
        var ls = go.transform.lossyScale;
        label.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1);
        label.transform.position = go.transform.position + new Vector3(0, col.size.y * ls.y * 0.5f + 0.45f, 0);
        MinimapIcon(go.transform, new Color(0.25f, 0.85f, 1f), 1.2f);
        EditorUtility.SetDirty(go);
    }

    static string Short(string room) => room.Replace("SIT Lvl 3 ", "");

    // ================================================================ player
    static GameObject MakePlayer(GameObject gen, Vector3 pos)
    {
        var p = new GameObject("Player");
        p.transform.SetParent(gen.transform, false);
        p.transform.position = pos;
        p.tag = "Player";
        var sr = p.AddComponent<SpriteRenderer>();
        sr.sprite = circle; sr.color = new Color32(90, 200, 255, 255); sr.sortingOrder = 30;
        p.transform.localScale = new Vector3(0.8f, 0.8f, 1);
        var rb = p.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0; rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var cc = p.AddComponent<CircleCollider2D>(); cc.radius = 0.45f;
        p.AddComponent<PlayerMovement>();
        p.AddComponent<PlayerInteraction>();
        p.AddComponent<ZoneTracker>();
        var lbl = Label(p.transform, "Name", "YOU", Color.white);
        lbl.transform.localScale = Vector3.one / 0.8f;
        lbl.transform.localPosition = new Vector3(0, 0.9f, 0);
        MinimapIcon(p.transform, new Color32(255, 70, 70, 255), 2.2f);
        return p;
    }

    // ================================================================ NPCs + stations
    static GameObject MakeNpc(GameObject gen, string objName, string displayName, Color color, Vector3 pos,
        string verb, (string who, string line)[] lines)
    {
        var go = Character(gen, objName, displayName, color, pos, box, 1.3f);
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
        so.FindProperty("prompt").stringValue = verb + " to " + displayName;
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

    static GameObject MakeStation(GameObject gen, string objName, string displayName, Color color, Vector3 pos, string quest)
    {
        var go = Character(gen, objName, displayName, color, pos, box, 1.4f);
        go.transform.Find("Body").localScale = new Vector3(1.1f, 1.1f, 1);
        var launcher = go.AddComponent<MinigameLauncher>();
        launcher.questName = quest;
        var it = go.AddComponent<Interactable>();
        var so = new SerializedObject(it);
        so.FindProperty("prompt").stringValue = "Start " + displayName;
        so.FindProperty("interactionArea").objectReferenceValue = go.GetComponent<CircleCollider2D>();
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(it.OnInteract, launcher.Launch);
        return go;
    }

    // the team's own "File Transfer Game" object: make it usable without moving it
    static void SetupExistingStation(GameObject go, Room room, string quest, string displayName, Color color)
    {
        var col = Get<BoxCollider2D>(go);
        col.isTrigger = true;
        // a solid body so you bump into it, plus a bigger trigger to interact from
        foreach (var n in new[] { "Station Visual", "Station Label", "MinimapIcon" })
        { var o = go.transform.Find(n); if (o) UnityEngine.Object.DestroyImmediate(o.gameObject); }
        var vis = MakeSprite(go.transform, "Station Visual", box, color, 20);
        vis.transform.localScale = Vector3.one;           // the object's own scale is its shape
        var ls = go.transform.lossyScale;
        col.offset = Vector2.zero;
        col.size = new Vector2(1 + 2.2f / Mathf.Max(0.01f, ls.x), 1 + 1.6f / Mathf.Max(0.01f, ls.y));
        var label = Label(go.transform, "Station Label", displayName, Color.white);
        label.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1);
        label.transform.position = go.transform.position + new Vector3(0, ls.y * 0.5f + 0.5f, 0);
        MinimapIcon(go.transform, new Color32(255, 220, 60, 255), 1.6f);

        var launcher = Get<MinigameLauncher>(go);
        launcher.questName = quest;
        var it = Get<Interactable>(go);
        var so = new SerializedObject(it);
        so.FindProperty("prompt").stringValue = "Start " + displayName;
        so.FindProperty("interactionArea").objectReferenceValue = col;
        so.ApplyModifiedPropertiesWithoutUndo();
        ClearPersistent(it.OnInteract);
        UnityEventTools.AddPersistentListener(it.OnInteract, launcher.Launch);
        var existingArea = go.GetComponent<MapArea>();
        if (existingArea) UnityEngine.Object.DestroyImmediate(existingArea);
        var oldSpawn = go.transform.Find("Quest Spawn Point"); if (oldSpawn) UnityEngine.Object.DestroyImmediate(oldSpawn.gameObject);
        room.taken.Add(go.transform.position);
        AddQuestArea(go, room, quest);
        EditorUtility.SetDirty(go);
    }

    static GameObject Character(GameObject gen, string objName, string displayName, Color color, Vector3 pos, Sprite shape, float talkRadius)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(gen.transform, false);
        go.transform.position = pos;
        var trig = go.AddComponent<CircleCollider2D>(); trig.isTrigger = true; trig.radius = talkRadius;
        var body = MakeSprite(go.transform, "Body", shape, color, 20);
        body.transform.localScale = new Vector3(0.9f, 0.9f, 1);
        body.gameObject.AddComponent<BoxCollider2D>();          // solid: you bump into it
        var lbl = Label(go.transform, "Name", displayName, Color.white);
        lbl.transform.localPosition = new Vector3(0, 0.95f, 0);
        MinimapIcon(go.transform, new Color32(255, 220, 60, 255), 1.6f);
        return go;
    }

    // quest panel click -> teleport next to this object
    static void AddQuestArea(GameObject go, Room room, string quest)
    {
        var area = go.AddComponent<MapArea>();
        area.areaName = room.name;
        area.questName = quest;
        var sp = new GameObject("Quest Spawn Point");
        sp.transform.SetParent(go.transform, true);
        sp.transform.position = FreeSpot(room, go.transform.position + new Vector3(0, -2f, 0), 1.2f);
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
        var btnRT = UIRect("InteractButton", canvasGO.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(460, 72));
        var btnImg = btnRT.gameObject.AddComponent<Image>();
        btnImg.sprite = panelSprite; btnImg.type = Image.Type.Sliced; btnImg.pixelsPerUnitMultiplier = 0.25f;
        var btn = btnRT.gameObject.AddComponent<Button>(); btn.targetGraphic = btnImg;
        var promptLabel = UIText("Label", btnRT, "Talk (E / tap)", 30, Color.white, TextAlignmentOptions.Center);
        Stretch(promptLabel.rectTransform, 12);

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
        Anchor(lineT.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(48, 40), new Vector2(-96, -120));
        lineT.rectTransform.offsetMin = new Vector2(48, 50); lineT.rectTransform.offsetMax = new Vector2(-48, -90);
        var cont = UIText("ContinuePrompt", boxRT, "E / click  >>", 24, new Color32(170, 190, 230, 255), TextAlignmentOptions.BottomRight);
        Anchor(cont.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 22), new Vector2(320, 40));
        cont.rectTransform.pivot = new Vector2(1, 0); cont.rectTransform.anchoredPosition = new Vector2(-40, 22);
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
    static void SetupCamera(GameObject player)
    {
        var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (cam == null) return;
        var follow = Get<CameraFollow>(cam.gameObject);
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        var p = player.transform.position;
        cam.transform.position = new Vector3(p.x, p.y, cam.transform.position.z);
        cam.cullingMask &= ~(1 << MINIMAP_LAYER);      // minimap icons only show on the minimap
    }

    static void SetupMinimap(Dictionary<string, Room> rooms)
    {
        var mm = UnityEngine.Object.FindFirstObjectByType<MinimapManager>(FindObjectsInactive.Include);
        if (mm == null) { Debug.LogWarning("[Gameplay] No MinimapManager in the scene."); return; }

        // the minimap cameras look at the real map + the icon layer
        foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam.name == "SmallMinimapCamera") { cam.cullingMask = (1 << 0) | (1 << MINIMAP_LAYER); cam.orthographicSize = 14; }
            if (cam.name == "BigMinimapCamera") { cam.cullingMask = (1 << 0) | (1 << MINIMAP_LAYER); }
            EditorUtility.SetDirty(cam);
        }

        // room list -> real room positions
        Bounds all = new Bounds(); bool first = true;
        foreach (var r in rooms.Values) { if (first) { all = r.bounds; first = false; } else all.Encapsulate(r.bounds); }
        foreach (var rd in mm.allRooms)
        {
            if (rd == null) continue;
            var key = string.IsNullOrEmpty(rd.roomName) ? rd.gameObject.name : rd.roomName;
            var room = MatchRoom(key, rooms.Values);
            if (room == null) continue;
            rd.cameraPosition = new Vector3(room.bounds.center.x, room.bounds.center.y, 0);
            float fit = Mathf.Max(room.bounds.extents.y, room.bounds.extents.x * 0.6f) + 2f;
            rd.defaultZoom = fit; rd.minZoom = 4; rd.maxZoom = Mathf.Max(fit * 2.5f, 30);
            room.zone.minimapRoom = rd;
            EditorUtility.SetDirty(rd); EditorUtility.SetDirty(room.zone);
        }
        mm.worldMin = new Vector2(all.min.x, all.min.y);
        mm.worldMax = new Vector2(all.max.x, all.max.y);
        if (mm.currentRoom == null && mm.allRooms.Count > 0) mm.currentRoom = mm.allRooms[0];
        EditorUtility.SetDirty(mm);

        // small minimap on, below the progress bar / player name (top-right)
        if (mm.smallMinimap != null)
        {
            mm.smallMinimap.SetActive(true);
            var rt = mm.smallMinimap.GetComponent<RectTransform>();
            if (rt != null) { rt.sizeDelta = new Vector2(320, 320); rt.anchoredPosition = new Vector2(-180, -310); }
            EditorUtility.SetDirty(mm.smallMinimap);
        }
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
        t.GetComponent<MeshRenderer>().sortingOrder = 91;
        // dark strip behind the text so it reads on any floor
        var bg = MakeSprite(go.transform, "Backdrop", box, new Color(0, 0, 0, 0.55f), 90);
        bg.transform.localScale = new Vector3(0.22f * text.Length + 0.3f, 0.42f, 1);
        return t;
    }

    static void MinimapIcon(Transform parent, Color c, float size)
    {
        var sr = MakeSprite(parent, "MinimapIcon", circle, c, 100);
        sr.gameObject.layer = MINIMAP_LAYER;
        var ls = parent.lossyScale;
        sr.transform.localScale = new Vector3(size / Mathf.Max(0.01f, ls.x), size / Mathf.Max(0.01f, ls.y), 1);
    }

    // GetComponent returns a "fake null" in the editor, so ?? can't be used for this
    static T Get<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void ClearPersistent(UnityEvent ev)
    {
        for (int i = ev.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(ev, i);
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
