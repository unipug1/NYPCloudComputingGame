using System.Collections.Generic;
using UnityEngine;

// Draws a walking path (moving dots) from the player to the quest picked in the quest panel.
// The walkable grid is baked by Tools > Gameplay > Build Map Gameplay (floors = walkable,
// tilemaps with colliders + NPC/station bodies = blocked), so nothing heavy runs at start-up.
public class PathGuide : MonoBehaviour
{
    [Header("Baked grid (filled by the builder)")]
    public Vector2 origin;          // world position of cell (0,0)'s centre
    public float cell = 1f;
    public int width, height;
    [HideInInspector] public byte[] walk;   // 1 = walkable

    [Header("Look")]
    public Sprite dotSprite;
    public Color dotColor = new Color(0.35f, 0.9f, 1f, 0.95f);
    public float dotSize = 0.28f;
    public float spacing = 0.9f;
    public float speed = 1.6f;              // how fast the dots march towards the quest
    public int sortingOrder = 5;
    int sortingLayerID;
    public float arriveDistance = 1.8f;     // hide the path when this close
    public float repathEvery = 0.35f;

    Transform player;
    string trackedQuest;
    Vector3 targetPos;
    readonly List<Vector2> path = new List<Vector2>();
    readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
    SpriteRenderer ring;
    float repathTimer, scroll;
    Vector2Int lastStart = new Vector2Int(int.MinValue, 0);
    bool warned;

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p) player = p.transform;
        // draw on the player's sorting layer, just under the player, so the floor never hides the dots
        var psr = p ? p.GetComponentInChildren<SpriteRenderer>() : null;
        if (psr != null) { sortingLayerID = psr.sortingLayerID; sortingOrder = psr.sortingOrder - 1; }
        ring = NewSprite("Quest Target Ring");
        ring.gameObject.SetActive(false);
    }

    void Update()
    {
        var nav = QuestNavigator.Instance;
        string q = nav != null ? nav.TrackedQuest : null;
        var area = string.IsNullOrEmpty(q) ? null : MapArea.ForQuest(q);
        if (player == null || area == null || walk == null || walk.Length != width * height) { HideAll(); trackedQuest = null; return; }

        if (q != trackedQuest) { trackedQuest = q; repathTimer = 0; lastStart = new Vector2Int(int.MinValue, 0); }
        targetPos = area.transform.position;

        // pulsing ring on the quest
        ring.gameObject.SetActive(true);
        ring.transform.position = targetPos;
        float pulse = 1.6f + 0.35f * Mathf.Sin(Time.time * 5f);
        ring.transform.localScale = new Vector3(pulse, pulse, 1);
        var rc = dotColor; rc.a = 0.35f + 0.2f * Mathf.Sin(Time.time * 5f); ring.color = rc;

        if (Vector2.Distance(player.position, targetPos) < arriveDistance) { HideDots(); return; }

        repathTimer -= Time.deltaTime;
        var start = ToCell(player.position);
        if (repathTimer <= 0 || (start != lastStart && repathTimer < repathEvery * 0.5f))
        {
            repathTimer = repathEvery;
            lastStart = start;
            FindPath(player.position, targetPos);
        }
        DrawDots();
    }

    // ---------------- grid ----------------
    Vector2Int ToCell(Vector3 w) => new Vector2Int(Mathf.RoundToInt((w.x - origin.x) / cell), Mathf.RoundToInt((w.y - origin.y) / cell));
    Vector2 ToWorld(Vector2Int c) => new Vector2(origin.x + c.x * cell, origin.y + c.y * cell);
    bool In(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < width && c.y < height;
    bool Open(Vector2Int c) => In(c) && walk[c.y * width + c.x] != 0;

    Vector2Int NearestOpen(Vector2Int c, int maxR = 6)
    {
        if (Open(c)) return c;
        for (int r = 1; r <= maxR; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    var n = new Vector2Int(c.x + dx, c.y + dy);
                    if (Open(n)) return n;
                }
        return new Vector2Int(-1, -1);
    }

    static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1),
        new Vector2Int(1,1), new Vector2Int(1,-1), new Vector2Int(-1,1), new Vector2Int(-1,-1)
    };

    // A* with no corner cutting, then straightened where there's a clear line
    void FindPath(Vector3 from, Vector3 to)
    {
        path.Clear();
        var s = NearestOpen(ToCell(from));
        var g = NearestOpen(ToCell(to));
        if (!In(s) || !In(g)) return;

        int n = width * height;
        var gScore = new float[n];
        var came = new int[n];
        var closed = new bool[n];
        for (int i = 0; i < n; i++) { gScore[i] = float.MaxValue; came[i] = -1; }
        var open = new Heap();
        int si = s.y * width + s.x, gi = g.y * width + g.x;
        gScore[si] = 0; open.Push(si, H(s, g));

        int guard = 0;
        int best = si; float bestH = H(s, g);          // closest we got, in case there's no way in yet
        while (open.Count > 0 && guard++ < 60000)
        {
            int cur = open.Pop();
            if (cur == gi) break;
            if (closed[cur]) continue;
            closed[cur] = true;
            var c = new Vector2Int(cur % width, cur / width);
            float hc = H(c, g);
            if (hc < bestH) { bestH = hc; best = cur; }
            for (int d = 0; d < 8; d++)
            {
                var nb = c + Dirs[d];
                if (!Open(nb)) continue;
                if (d >= 4 && (!Open(new Vector2Int(nb.x, c.y)) || !Open(new Vector2Int(c.x, nb.y)))) continue;   // no corner cutting
                int ni = nb.y * width + nb.x;
                if (closed[ni]) continue;
                float ng = gScore[cur] + (d >= 4 ? 1.4142f : 1f);
                if (ng >= gScore[ni]) continue;
                gScore[ni] = ng; came[ni] = cur;
                open.Push(ni, ng + H(nb, g));
            }
        }
        // no full route (e.g. a closed door) -> lead to the closest reachable spot, then straight to the quest
        bool reached = came[gi] >= 0 || si == gi;
        int end = reached ? gi : best;
        if (!reached && !warned) { warned = true; Debug.Log("[PathGuide] No walkable route to " + trackedQuest + " (closed door / blocked tiles?) - showing the closest way."); }

        var cells = new List<Vector2Int>();
        for (int i = end; i >= 0; i = came[i]) { cells.Add(new Vector2Int(i % width, i / width)); if (i == si) break; }
        cells.Reverse();

        // string-pull: skip points we can see past
        path.Add(player.position);
        int k = 0;
        while (k < cells.Count - 1)
        {
            int far = k + 1;
            for (int j = cells.Count - 1; j > k + 1; j--)
                if (ClearLine(cells[k], cells[j])) { far = j; break; }
            path.Add(ToWorld(cells[far]));
            k = far;
        }
        if (!reached) path.Add(new Vector2(to.x, to.y));
    }

    static float H(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
        return (dx + dy) + (1.4142f - 2f) * Mathf.Min(dx, dy);
    }

    bool ClearLine(Vector2Int a, Vector2Int b)
    {
        int steps = Mathf.CeilToInt(Vector2Int.Distance(a, b) * 3f);
        for (int i = 1; i < steps; i++)
        {
            var p = Vector2.Lerp(a, b, i / (float)steps);
            // check the cells either side so the line keeps a little clearance
            if (!Open(new Vector2Int(Mathf.FloorToInt(p.x + 0.3f), Mathf.FloorToInt(p.y + 0.3f)))) return false;
            if (!Open(new Vector2Int(Mathf.CeilToInt(p.x - 0.3f), Mathf.CeilToInt(p.y - 0.3f)))) return false;
        }
        return true;
    }

    // ---------------- drawing ----------------
    void DrawDots()
    {
        if (path.Count < 2) { HideDots(); return; }
        path[0] = player.position;
        scroll = (scroll + Time.deltaTime * speed) % spacing;

        int used = 0;
        float carry = spacing - scroll;          // distance to the first dot
        float total = 0;
        for (int i = 0; i < path.Count - 1; i++) total += Vector2.Distance(path[i], path[i + 1]);
        float walked = 0;
        for (int i = 0; i < path.Count - 1 && used < 200; i++)
        {
            Vector2 a = path[i], b = path[i + 1];
            float len = Vector2.Distance(a, b);
            float t = carry;
            while (t <= len && used < 200)
            {
                var pos = Vector2.Lerp(a, b, t / len);
                var d = GetDot(used++);
                d.transform.position = new Vector3(pos.x, pos.y, 0);
                float along = (walked + t) / Mathf.Max(0.01f, total);
                var c = dotColor;
                c.a *= Mathf.Clamp01((walked + t) / 1.2f) * Mathf.Lerp(1f, 0.6f, along);   // fade in near the player
                d.color = c;
                t += spacing;
            }
            carry = t - len;
            walked += len;
        }
        for (int i = used; i < dots.Count; i++) dots[i].gameObject.SetActive(false);
    }

    SpriteRenderer GetDot(int i)
    {
        while (dots.Count <= i)
        {
            var sr = NewSprite("Path Dot");
            sr.transform.localScale = new Vector3(dotSize, dotSize, 1);
            dots.Add(sr);
        }
        dots[i].gameObject.SetActive(true);
        return dots[i];
    }

    SpriteRenderer NewSprite(string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = dotSprite; sr.color = dotColor; sr.sortingOrder = sortingOrder; sr.sortingLayerID = sortingLayerID;
        return sr;
    }

    void HideDots() { foreach (var d in dots) if (d.gameObject.activeSelf) d.gameObject.SetActive(false); }
    void HideAll() { HideDots(); if (ring != null && ring.gameObject.activeSelf) ring.gameObject.SetActive(false); }

    // tiny binary min-heap for A*
    class Heap
    {
        readonly List<int> items = new List<int>();
        readonly List<float> keys = new List<float>();
        public int Count => items.Count;
        public void Push(int item, float key)
        {
            items.Add(item); keys.Add(key);
            int i = items.Count - 1;
            while (i > 0) { int p = (i - 1) / 2; if (keys[p] <= keys[i]) break; Swap(i, p); i = p; }
        }
        public int Pop()
        {
            int top = items[0]; int last = items.Count - 1;
            items[0] = items[last]; keys[0] = keys[last];
            items.RemoveAt(last); keys.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < items.Count && keys[l] < keys[m]) m = l;
                if (r < items.Count && keys[r] < keys[m]) m = r;
                if (m == i) break;
                Swap(i, m); i = m;
            }
            return top;
        }
        void Swap(int a, int b)
        {
            (items[a], items[b]) = (items[b], items[a]);
            (keys[a], keys[b]) = (keys[b], keys[a]);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (walk == null || walk.Length != width * height) return;
        Gizmos.color = new Color(0, 1, 0.5f, 0.25f);
        for (int y = 0; y < height; y += 2)
            for (int x = 0; x < width; x += 2)
                if (walk[y * width + x] != 0) Gizmos.DrawCube(ToWorld(new Vector2Int(x, y)), Vector3.one * cell * 0.5f);
    }
}
