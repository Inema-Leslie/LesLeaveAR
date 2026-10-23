using System.Collections.Generic;
using UnityEngine;

namespace LesLeave
{
    /// <summary>
    /// On-screen interface (IMGUI, laid out for a portrait phone). It only reads state from the other
    /// managers and calls SimulationManager methods, so the game logic never depends on how it looks.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // palette
        private static readonly Color Cream = new Color(0.953f, 0.945f, 0.918f);
        private static readonly Color Ink = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color Muted = new Color(0.45f, 0.45f, 0.42f);
        private static readonly Color Wine = new Color(0.478f, 0.102f, 0.180f);
        private static readonly Color Night = new Color(0.082f, 0.114f, 0.102f);
        private static readonly Color Tile = new Color(0.918f, 0.914f, 0.882f);
        private static readonly Color Green = new Color(0.18f, 0.42f, 0.29f);
        private static readonly Color Amber = new Color(0.97f, 0.91f, 0.76f);

        private GameManager gm;
        private readonly List<Rect> rects = new List<Rect>();
        private readonly Dictionary<int, Texture2D> roundCache = new Dictionary<int, Texture2D>();
        private readonly Dictionary<string, GUIStyle> styleCache = new Dictionary<string, GUIStyle>();
        private float uiScale = 1f;

        private Texture2D white;
        private Texture2D chevron;
        private Font heading;
        private Font bodyFont;

        private string warning = "";
        private float warningTime = -10f;
        private float exposure;
        private int sessionCount;
        private float bestScore;
        private bool showHistory;
        private readonly List<Session> recent = new List<Session>();

        public void Init(GameManager g)
        {
            gm = g;
            white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();
            chevron = MakeChevron();
            if (gm.Assets != null)
            {
                heading = gm.Assets.headingFont;
                bodyFont = gm.Assets.bodyFont;
            }

            gm.Bus.Subscribe<WarningEvent>(e => Warn(e.Message));
            gm.Bus.Subscribe<ExposureChangedEvent>(e => exposure = e.Value);
            gm.Bus.Subscribe<ExitBlockedEvent>(e => Warn("The main exit is blocked. Use another way out."));
            gm.Bus.Subscribe<AlarmActivatedEvent>(e => Warn("Alarm activated."));
            gm.Bus.Subscribe<DoorInspectedEvent>(e => Warn(e.Hot ? "The door is HOT. Do not open it." : "The door is cool. It is safe to use."));
            gm.Bus.Subscribe<DrillEndedEvent>(e => RefreshHistory());
            gm.Bus.Subscribe<StateChangedEvent>(e => { if (e.Name == "Menu") RefreshHistory(); });
        }

        private void Warn(string msg)
        {
            warning = msg;
            warningTime = Time.unscaledTime;
        }

        private void RefreshHistory()
        {
            List<Session> all = gm.Data.ListSessions();   // newest first
            sessionCount = 0;
            bestScore = 0f;
            recent.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].outcome == "InProgress") continue;
                sessionCount++;
                if (all[i].score > bestScore) bestScore = all[i].score;
                if (recent.Count < 5) recent.Add(all[i]);
            }
        }

        /// <summary>True if a screen position (origin bottom-left, as the input system reports it) is on a button or panel.</summary>
        public bool IsOverUI(Vector2 screenPos)
        {
            Vector2 g = new Vector2(screenPos.x / uiScale, (Screen.height - screenPos.y) / uiScale);
            for (int i = 0; i < rects.Count; i++) if (rects[i].Contains(g)) return true;
            return false;
        }

        // ------------------------------------------------------------------ drawing helpers

        private Texture2D Round(Color c, Color border, int radius)
        {
            int key = ((Color32)c).GetHashCode() * 31 + ((Color32)border).GetHashCode() * 7 + radius;
            Texture2D t;
            if (roundCache.TryGetValue(key, out t)) return t;

            int n = 64;
            t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (n - radius), 0f);
                    float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (n - radius), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    Color px = c;
                    if (border.a > 0f && d > radius - 2f) px = Color.Lerp(c, border, 1f);
                    px.a *= a;
                    t.SetPixel(x, y, px);
                }
            }
            t.Apply();
            roundCache[key] = t;
            return t;
        }

        private static Texture2D MakeChevron()
        {
            int w = 128, h = 64;
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - w / 2f);
                    float outer = h - dx * 0.5f;
                    float inner = outer - 22f;
                    bool inside = y <= outer && y >= inner && inner + 22f > 0f;
                    t.SetPixel(x, y, inside ? Color.white : new Color(1, 1, 1, 0));
                }
            }
            t.Apply();
            return t;
        }

        private GUIStyle Style(string key, bool useHeading, int size, Color color, TextAnchor anchor, bool bold)
        {
            string k = key + (useHeading ? "H" : "B") + size + (bold ? "b" : "") + anchor + ColorUtility.ToHtmlStringRGBA(color);
            GUIStyle s;
            if (styleCache.TryGetValue(k, out s)) return s;
            s = new GUIStyle(GUI.skin.label);
            Font f = useHeading ? heading : bodyFont;
            if (f != null) s.font = f;
            s.fontSize = size;
            s.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            s.alignment = anchor;
            s.wordWrap = true;
            s.richText = true;
            s.normal.textColor = color;
            styleCache[k] = s;
            return s;
        }

        private void Text(Rect r, string text, bool useHeading, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false)
        {
            GUI.Label(r, text, Style("t", useHeading, size, color, anchor, bold));
        }

        private void Fill(Rect r, Color c, int radius = 0, bool register = true)
        {
            Color old = GUI.color;
            GUI.color = Color.white;
            if (radius > 0)
            {
                GUIStyle s = new GUIStyle();
                s.normal.background = Round(c, new Color(0, 0, 0, 0), radius);
                s.border = new RectOffset(radius, radius, radius, radius);
                GUI.Box(r, GUIContent.none, s);
            }
            else
            {
                GUI.color = c;
                GUI.DrawTexture(r, white);
            }
            GUI.color = old;
            if (register) rects.Add(r);
        }

        private bool Button(Rect r, string text, int style)
        {
            // style: 0 filled wine, 1 outline, 2 dark translucent, 3 disabled
            const float MaxButtonWidth = 240f;
            if (r.width > MaxButtonWidth) r = new Rect(r.x + (r.width - MaxButtonWidth) * 0.5f, r.y, MaxButtonWidth, r.height);
            Color fill = Wine, txt = Color.white, border = new Color(0, 0, 0, 0);
            if (style == 1) { fill = Cream; txt = Ink; border = new Color(0.82f, 0.81f, 0.77f); }
            else if (style == 2) { fill = new Color(1f, 1f, 1f, 0.12f); txt = Color.white; }
            else if (style == 3) { fill = new Color(0.86f, 0.85f, 0.82f); txt = Muted; }

            GUIStyle s = new GUIStyle();
            Texture2D tex = Round(fill, border, 14);
            s.normal.background = tex;
            s.hover.background = tex;
            s.active.background = Round(Color.Lerp(fill, Color.black, 0.15f), border, 14);
            s.border = new RectOffset(14, 14, 14, 14);
            s.alignment = TextAnchor.MiddleCenter;
            if (bodyFont != null) s.font = bodyFont;
            s.fontSize = 16;
            s.fontStyle = FontStyle.Bold;
            s.normal.textColor = txt;
            s.hover.textColor = txt;
            s.active.textColor = txt;

            rects.Add(r);
            bool clicked = GUI.Button(r, text, s);
            return clicked && style != 3;
        }

        private static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        private int DoneCount()
        {
            int n = 0;
            List<TaskItem> t = gm.Tasks.Tasks;
            for (int i = 0; i < t.Count; i++) if (t[i].Done) n++;
            return n;
        }

        // ------------------------------------------------------------------ main draw

        private void OnGUI()
        {
            if (gm == null || gm.Sim == null || gm.Sim.Current == null) return;
            rects.Clear();

            uiScale = Mathf.Clamp(Screen.height / 844f, 0.5f, 4f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            float w = Screen.width / uiScale;
            float h = Screen.height / uiScale;

            SimulationManager sim = gm.Sim;
            SimState st = sim.Current;

            if (st is MenuState) { if (showHistory) DrawHistory(w, h); else DrawMenu(w, h); }
            else if (showHistory) showHistory = false;
            if (st is ScanningState) DrawSetup(w, h, 1);
            else if (st is PlacingState) DrawSetup(w, h, 2);
            else if (st is BriefingState) DrawBriefing(w, h);
            else if (st is RunningState) DrawHud(w, h);
            else if (st is PausedState) { DrawHud(w, h); DrawPaused(w, h, (PausedState)st); }
            else if (st is CompletedState || st is FailedState) DrawResult(w, h);

            GUI.matrix = old;
        }

        // ------------------------------------------------------------------ menu

        private void DrawMenu(float w, float h)
        {
            Fill(new Rect(0, 0, w, h), Cream);
            float x = 24f, iw = w - 48f, y = 56f;

            Text(new Rect(x, y, iw, 24f), "LesLeave AR", false, 15, Wine, TextAnchor.MiddleCenter, true);
            y += 44f;
            y += 8f;

            ScenarioData s = gm.Sim.Scenario;
            if (s != null)
            {
                Text(new Rect(x, y, iw, 22f), s.title, false, 15, Wine, TextAnchor.UpperLeft, true);
                y += 26f;
                Text(new Rect(x, y, iw, 100f), s.description, false, 14, Muted);
                y += 108f;
            }

            Text(new Rect(x, y, iw, 22f), "Scan the floor. Place the scene. Find the exit.", false, 14, Ink);
            y += 40f;

            string[] steps = { "Scan the floor", "Place the scene", "Find a safe exit" };
            float cw = iw / 3f;
            for (int i = 0; i < 3; i++)
                Text(new Rect(x + i * cw, y, cw - 6f, 36f), steps[i], false, 13, Ink, TextAnchor.UpperLeft, true);
            y += 56f;

            GUI.color = new Color(0, 0, 0, 0.12f);
            GUI.DrawTexture(new Rect(x, y, iw, 1f), white);
            GUI.color = Color.white;
            y += 16f;
            Text(new Rect(x, y, iw * 0.6f, 40f), "Best score", false, 14, Muted, TextAnchor.MiddleLeft);
            Text(new Rect(x + iw * 0.5f, y, iw * 0.5f, 40f), sessionCount == 0 ? "-" : Mathf.RoundToInt(bestScore) + " / 100",
                 true, 28, Ink, TextAnchor.MiddleRight, true);
            y += 60f;

            if (Button(new Rect(x, y, iw, 56f), "Start", 0)) gm.Sim.StartFlow();
            y += 64f;
            if (Button(new Rect(x, y, iw, 48f), "Recent sessions", 1)) { RefreshHistory(); showHistory = true; }
            y += 58f;
            Text(new Rect(x, y, iw, 20f), "Training only. Not for real emergencies.", false, 11, Muted, TextAnchor.UpperCenter);
        }

        // ------------------------------------------------------------------ history

        private void DrawHistory(float w, float h)
        {
            Fill(new Rect(0, 0, w, h), Cream);
            float x = 24f, iw = w - 48f, y = 56f;
            Text(new Rect(x, y, iw, 24f), "LesLeave AR", false, 15, Wine, TextAnchor.MiddleCenter, true);
            y += 44f;
            Text(new Rect(x, y, iw, 50f), "Recent sessions", true, 34, Ink, TextAnchor.UpperLeft, true);
            y += 62f;

            if (recent.Count == 0)
            {
                Text(new Rect(x, y, iw, 40f), "No drills played yet. Finish one and it will appear here.", false, 14, Muted);
            }
            for (int i = 0; i < recent.Count; i++)
            {
                Session s = recent[i];
                Rect row = new Rect(x, y, iw, 66f);
                Fill(row, Color.white, 14, false);
                bool ok = s.outcome == "Completed";
                Color oc = ok ? Green : (s.outcome == "Failed" ? Wine : Muted);
                Text(new Rect(row.x + 16f, row.y + 10f, iw * 0.55f, 22f), s.outcome, false, 15, oc, TextAnchor.UpperLeft, true);
                Text(new Rect(row.x + 16f, row.y + 34f, iw * 0.7f, 22f),
                     NiceDate(s.startTime) + "  \u00B7  " + Clock(s.duration) + "  \u00B7  " + Mathf.RoundToInt(s.peakExposure * 100f) + "% smoke",
                     false, 11, Muted);
                Text(new Rect(row.x + iw * 0.55f, row.y + 8f, iw * 0.45f - 16f, 50f), Mathf.RoundToInt(s.score) + " / 100", true, 24, Ink, TextAnchor.MiddleRight, true);
                y += 76f;
            }

            if (Button(new Rect(x, h - 90f, iw, 52f), "Back", 1)) showHistory = false;
        }

        private static string NiceDate(string raw)
        {
            System.DateTime d;
            if (System.DateTime.TryParse(raw, out d)) return d.ToString("d MMM, HH:mm");
            return raw;
        }

        // ------------------------------------------------------------------ AR setup (scan + place)

        private void DrawBackBar(float w, string badge)
        {
            if (Button(new Rect(14f, 40f, 44f, 40f), "<", 2)) gm.Sim.ToMenu();
            Text(new Rect(w - 220f, 44f, 206f, 32f), "<color=#B03050>●</color> " + badge, false, 11, Color.white, TextAnchor.MiddleRight, true);
        }

        private void DrawSetup(float w, float h, int step)
        {
            Fill(new Rect(0, 0, w, 110f), new Color(0, 0, 0, 0.35f), 0, false);
            DrawBackBar(w, "AR SETUP · " + step + " OF 2");

            Rect card = new Rect(10f, h - 170f, w - 20f, 150f);
            Fill(card, Cream, 22);
            float bw = (card.width - 56f) / 2f;
            Fill(new Rect(card.x + 22f, card.y + 20f, bw, 3f), Wine, 0, false);
            Fill(new Rect(card.x + 34f + bw, card.y + 20f, bw, 3f), step == 2 ? Wine : new Color(0.8f, 0.78f, 0.74f), 0, false);

            string title = step == 1 ? "Move your phone to scan the floor." : "Tap to place the scenario.";
            Text(new Rect(card.x + 22f, card.y + 34f, card.width - 44f, 80f), title, true, 28, Ink, TextAnchor.UpperLeft, true);
            string tip = step == 1 ? "Point at the floor a few metres ahead." : "Aim the ring where the classroom door should be.";
            Text(new Rect(card.x + 22f, card.y + 92f, card.width - 44f, 24f), tip, false, 12, Muted);
            Text(new Rect(card.x, card.y + 122f, card.width, 20f), "Training only", false, 11, Muted, TextAnchor.UpperCenter);
        }

        // ------------------------------------------------------------------ briefing + countdown

        private void DrawBriefing(float w, float h)
        {
            SimulationManager sim = gm.Sim;
            if (sim.CountdownRemaining >= 0f)
            {
                Fill(new Rect(0, 0, w, h), new Color(Night.r, Night.g, Night.b, 0.96f));
                Text(new Rect(0, h * 0.5f - 110f, w, 220f), Mathf.CeilToInt(sim.CountdownRemaining).ToString(), true, 190, Color.white, TextAnchor.MiddleCenter);
                return;
            }

            Fill(new Rect(0, 0, w, 110f), new Color(0, 0, 0, 0.35f), 0, false);
            if (Button(new Rect(14f, 40f, 44f, 40f), "<", 2)) sim.ToMenu();
            Text(new Rect(w - 220f, 44f, 206f, 32f), "<color=#B03050>●</color> TRAINING ONLY", false, 11, Color.white, TextAnchor.MiddleRight, true);

            Rect card = new Rect(10f, h - 420f, w - 20f, 400f);
            Fill(card, Cream, 22);
            float x = card.x + 24f, iw = card.width - 48f, y = card.y + 24f;
            Text(new Rect(x, y, iw, 44f), "Before you start", true, 32, Ink, TextAnchor.UpperLeft, true);
            y += 54f;
            string[] tips = { "Stay low under smoke", "Follow exit signs", "Use stairs, not elevators", "Do not re-enter" };
            for (int i = 0; i < tips.Length; i++)
            {
                Text(new Rect(x, y, iw, 26f), tips[i], false, 15, Ink);
                y += 30f;
            }
            y += 14f;
            if (Button(new Rect(x, y, iw, 54f), "Start drill", 0)) sim.StartCountdown();
            y += 62f;
            if (Button(new Rect(x, y, iw, 46f), "Move scenario", 1)) sim.Reposition();
            y += 56f;
            Text(new Rect(x, y, iw, 20f), "Training only", false, 11, Muted, TextAnchor.UpperCenter);
        }

        // ------------------------------------------------------------------ HUD

        private void DrawHud(float w, float h)
        {
            SimulationManager sim = gm.Sim;

            Fill(new Rect(0, 0, w, 156f), new Color(Night.r, Night.g, Night.b, 0.62f), 0, false);

            Text(new Rect(20f, 44f, 200f, 22f), "<color=#B03050>●</color> SIMULATION", false, 11, Color.white, TextAnchor.MiddleLeft, true);
            if (sim.Current is RunningState && Button(new Rect(w - 112f, 36f, 96f, 36f), "II  Pause", 2)) sim.Pause(false);

            // elapsed + smoke
            Text(new Rect(20f, 78f, 120f, 16f), "ELAPSED", false, 10, new Color(1, 1, 1, 0.6f));
            Text(new Rect(20f, 90f, 150f, 52f), Clock(sim.Clock), true, 40, Color.white, TextAnchor.UpperLeft, true);

            float sx = w * 0.5f;
            Fill(new Rect(sx - 14f, 84f, 1f, 52f), new Color(1, 1, 1, 0.25f), 0, false);
            Text(new Rect(sx, 78f, 100f, 16f), "SMOKE", false, 10, new Color(1, 1, 1, 0.6f));
            Text(new Rect(w - 80f, 74f, 60f, 22f), Mathf.RoundToInt(exposure * 100f) + "%", false, 14, new Color(0.9f, 0.3f, 0.35f), TextAnchor.MiddleRight, true);
            Rect bar = new Rect(sx, 108f, w - 20f - sx, 6f);
            Fill(bar, new Color(1, 1, 1, 0.2f), 3, false);
            Fill(new Rect(bar.x, bar.y, Mathf.Max(6f, bar.width * Mathf.Clamp01(exposure)), bar.height), new Color(0.82f, 0.2f, 0.3f), 3, false);

            // warning banner
            if (Time.unscaledTime - warningTime < 3.5f)
            {
                Rect b = new Rect(16f, 168f, w - 32f, 46f);
                Fill(b, Amber, 12, false);
                Text(new Rect(b.x + 14f, b.y, b.width - 24f, b.height), "⚠  " + warning, false, 13, Ink, TextAnchor.MiddleLeft, true);
            }

            DrawArrow(w, h);

            // checklist
            List<TaskItem> tasks = gm.Tasks.Tasks;
            float top = h - 104f - 14f - tasks.Count * 28f;
            Fill(new Rect(0, top - 34f, w, h - top + 34f), new Color(Night.r, Night.g, Night.b, 0.55f), 0, false);
            Text(new Rect(20f, top - 26f, 160f, 18f), "CHECKLIST", false, 10, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleLeft, true);
            Text(new Rect(w - 90f, top - 26f, 70f, 18f), DoneCount() + " / " + tasks.Count, false, 11, new Color(0.9f, 0.3f, 0.35f), TextAnchor.MiddleRight, true);
            for (int i = 0; i < tasks.Count; i++)
            {
                TaskItem t = tasks[i];
                float ry = top + i * 28f;
                Rect box = new Rect(20f, ry + 3f, 18f, 18f);
                if (t.Done) { Fill(box, Green, 4, false); Text(box, "✓", false, 12, Color.white, TextAnchor.MiddleCenter, true); }
                else if (t.Failed) { Fill(box, Wine, 4, false); Text(box, "✕", false, 11, Color.white, TextAnchor.MiddleCenter, true); }
                else Fill(new Rect(box.x + 6f, box.y + 6f, 6f, 6f), new Color(1, 1, 1, 0.5f), 3, false);
                Text(new Rect(48f, ry, w - 70f, 24f), t.Label, false, 14, Color.white, TextAnchor.MiddleLeft);
            }

            // next step card
            Rect nx = new Rect(16f, h - 104f, w - 32f, 78f);
            Fill(nx, Wine, 14, false);
            Text(new Rect(nx.x + 16f, nx.y + 10f, nx.width - 30f, 16f), "NEXT", false, 10, new Color(1, 1, 1, 0.7f), TextAnchor.UpperLeft, true);
            Text(new Rect(nx.x + 16f, nx.y + 26f, nx.width - 30f, 46f), NextHint(), false, 15, Color.white, TextAnchor.UpperLeft, true);

            if (gm.AR.IsSimulated)
                Text(new Rect(10f, h - 22f, w - 20f, 18f), "TEST: WASD move · Shift run · right mouse look · C crouch · T lose tracking · click to tap",
                     false, 9, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter);

            // red tint when exposed
            if (exposure > 0.01f) Fill(new Rect(0, 0, w, h), new Color(0.5f, 0.05f, 0f, exposure * 0.3f), 0, false);
        }

        private void DrawArrow(float w, float h)
        {
            string label;
            Vector3 pos;
            if (!gm.Tasks.TryGetTarget(out label, out pos)) return;
            Transform cam = gm.AR.CameraTransform;
            if (cam == null) return;

            Vector3 to = pos - cam.position;
            to.y = 0f;
            float dist = to.magnitude;

            Vector3 fwd = cam.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) { fwd = cam.up; fwd.y = 0f; }
            if (fwd.sqrMagnitude < 0.0001f || to.sqrMagnitude < 0.0001f) return;
            float angle = Vector3.SignedAngle(fwd.normalized, to.normalized, Vector3.up);

            Vector2 c = new Vector2(w * 0.5f, h * 0.44f);

            string tag = label + " · " + Mathf.RoundToInt(dist) + " m";
            Rect pill = new Rect(c.x - 80f, c.y - 100f, 160f, 30f);
            Fill(pill, new Color(0, 0, 0, 0.55f), 15, false);
            Text(pill, tag, false, 13, Color.white, TextAnchor.MiddleCenter, true);

            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, c);
            Color old = GUI.color;
            float[] alphas = { 0.35f, 0.65f, 1f };
            for (int i = 0; i < 3; i++)
            {
                GUI.color = new Color(0.62f, 0.14f, 0.24f, alphas[i]);
                GUI.DrawTexture(new Rect(c.x - 55f, c.y - 40f + (2 - i) * 30f - 20f, 110f, 55f), chevron);
            }
            GUI.color = old;
            GUI.matrix = saved;
        }

        private string NextHint()
        {
            Session s = gm.Sim.Session;
            List<TaskItem> t = gm.Tasks.Tasks;
            SmokeLayer smoke = gm.Entities.Smoke;

            if (smoke != null && smoke.Active && gm.Tasks.Player.deviceHeight > smoke.Bottom)
                return "Smoke! Crouch down low.";
            if (s != null && !s.alarmActivated) return "Find the red fire alarm on the wall and tap it.";

            TaskItem signs = null, stairs = null;
            for (int i = 0; i < t.Count; i++)
            {
                if (t[i].Id == "signs") signs = t[i];
                if (t[i].Id == "stairs") stairs = t[i];
            }
            if (signs != null && !signs.Done && !signs.Failed)
            {
                if (s != null && !s.inspectedDoor) return "Walk close to a door and tap it to feel if it is hot (orange) or cool (blue).";
                return "Walk through the COOL (blue) door.";
            }
            if (stairs != null && !stairs.Done && !stairs.Failed) return "Take the stairs. Never the elevator.";
            return "Walk to the green assembly point. If an exit is blocked, go another way.";
        }

        // ------------------------------------------------------------------ paused / tracking lost

        private void DrawStatsRow(Rect r, bool redExposure)
        {
            Fill(r, Tile, 14, false);
            float cw = r.width / 3f;
            string[] vals = { Clock(gm.Sim.Clock), Mathf.RoundToInt(exposure * 100f) + "%", DoneCount() + " / " + gm.Tasks.Tasks.Count };
            string[] labs = { "TIME", "EXPOSURE", "TASKS" };
            for (int i = 0; i < 3; i++)
            {
                Color c = (i == 1 && redExposure) ? Wine : Ink;
                Text(new Rect(r.x + i * cw, r.y + 12f, cw, 30f), vals[i], true, 22, c, TextAnchor.MiddleCenter);
                Text(new Rect(r.x + i * cw, r.y + 44f, cw, 14f), labs[i], false, 9, Muted, TextAnchor.MiddleCenter);
            }
        }

        private void DrawPaused(float w, float h, PausedState ps)
        {
            Fill(new Rect(0, 0, w, h), new Color(Night.r, Night.g, Night.b, 0.94f));
            ScenarioData sc = gm.Sim.Scenario;
            Text(new Rect(20f, 48f, w - 140f, 24f), sc != null ? sc.title : "", false, 15, Color.white, TextAnchor.MiddleLeft);
            Text(new Rect(w - 130f, 48f, 110f, 24f), ps.Auto ? "AUTO-PAUSED" : "PAUSED", false, 10, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleRight, true);

            Rect card = new Rect(16f, h * 0.2f, w - 32f, ps.Auto ? 300f : 330f);
            Fill(card, Cream, 20);
            float x = card.x + 22f, iw = card.width - 44f, y = card.y + 22f;
            Text(new Rect(x, y, iw, 44f), ps.Auto ? "Tracking lost" : "Paused", true, 32, Ink, TextAnchor.UpperLeft, true);
            y += 50f;
            if (ps.Auto)
            {
                Text(new Rect(x, y, iw, 40f), "Point at the floor and move your phone slowly.", false, 14, Muted);
                y += 54f;
            }
            DrawStatsRow(new Rect(x, y, iw, 66f), false);
            y += 84f;

            if (ps.Auto)
            {
                Button(new Rect(x, y, iw, 52f), "Finding floor...", 3);
            }
            else
            {
                if (Button(new Rect(x, y, iw, 52f), "Resume drill", 0)) gm.Sim.Resume();
                y += 62f;
                if (Button(new Rect(x, y, iw, 48f), "Menu", 1)) gm.Sim.Abort();
            }
            Text(new Rect(0, h - 40f, w, 20f), "Training only", false, 11, new Color(1, 1, 1, 0.5f), TextAnchor.UpperCenter);
        }

        // ------------------------------------------------------------------ result

        private void DrawResult(float w, float h)
        {
            SimulationManager sim = gm.Sim;
            bool ok = sim.Outcome == DrillOutcome.Completed;
            Session s = sim.Session;

            Fill(new Rect(0, 0, w, h), Cream);
            float x = 24f, iw = w - 48f, y = 90f;

            if (ok) Text(new Rect(x, y, iw, 18f), "DRILL COMPLETE", false, 11, Wine, TextAnchor.UpperLeft, true);
            y += 22f;
            Text(new Rect(x, y, iw, 50f), ok ? "Success" : "Failed", true, 40, ok ? Ink : Wine, TextAnchor.UpperLeft, true);
            y += 64f;

            float score = s != null ? s.score : 0f;
            if (ok)
            {
                Text(new Rect(x, y, iw, 90f), "<size=84>" + Mathf.RoundToInt(score) + "</size> <size=22><color=#777770>/ 100</color></size>",
                     true, 84, Ink, TextAnchor.UpperLeft, true);
                y += 110f;
            }
            else
            {
                Text(new Rect(x, y, iw, 44f), Mathf.RoundToInt(score) + " / 100", true, 34, Ink, TextAnchor.UpperLeft, true);
                y += 46f;
                Text(new Rect(x, y, iw, 44f), sim.EndReason, false, 14, Muted);
                y += 54f;
            }

            Rect stats = new Rect(x, y, iw, 76f);
            Fill(stats, Color.white, 16, false);
            float cw = stats.width / 3f;
            float peak = s != null ? s.peakExposure : 0f;
            string[] vals = { Clock(s != null ? s.duration : 0f), Mathf.RoundToInt(peak * 100f) + "%", DoneCount() + " / " + gm.Tasks.Tasks.Count };
            string[] labs = { "DRILL TIME", "EXPOSURE", "TASKS DONE" };
            for (int i = 0; i < 3; i++)
            {
                Color c = (i == 1 && peak >= 0.999f) ? Wine : Ink;
                Text(new Rect(stats.x + i * cw, stats.y + 14f, cw, 32f), vals[i], true, 24, c, TextAnchor.MiddleCenter);
                Text(new Rect(stats.x + i * cw, stats.y + 50f, cw, 14f), labs[i], false, 9, Muted, TextAnchor.MiddleCenter);
            }
            y += 96f;

            if (Button(new Rect(x, y, iw, 54f), "Try again", 0)) sim.Retry();
            y += 64f;
            if (Button(new Rect(x, y, iw, 50f), "Menu", 1)) sim.ToMenu();
        }
    }
}
