using System.Collections.Generic;
using UnityEngine;

namespace ProjectionMapper
{
    public static class ProjectionGUI
    {
        private static Vector2 _scroll;
        private static string _newProfileName = "";

        public static void DrawEditLabels(
            List<ProjectionSurface> surfaces, int selectedIndex, int heldCorner)
        {
            if (surfaces.Count == 0) return;
            int si = Mathf.Clamp(selectedIndex, 0, surfaces.Count - 1);
            var s = surfaces[si];
            string[] labels = { "1", "2", "3", "4" };
            Color[] colors = { Color.red, Color.green, Color.blue, Color.yellow };

            var ls = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold };

            for (int c = 0; c < 4; c++)
            {
                float sx = s.corners[c].x * Screen.width;
                float sy = (1f - s.corners[c].y) * Screen.height;
                ls.normal.textColor = heldCorner == c ? Color.white : colors[c];
                GUI.Label(new Rect(sx + 15, sy - 25, 50, 30), labels[c], ls);
            }

            Vector2 ctr = Vector2.zero;
            for (int c = 0; c < 4; c++) ctr += s.corners[c];
            ctr /= 4f;
            ls.normal.textColor = new Color(1, 1, 1, .7f);
            ls.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(ctr.x * Screen.width - 75,
                (1f - ctr.y) * Screen.height - 15, 150, 30), s.name, ls);

            if (heldCorner >= 0)
            {
                var hs = new GUIStyle(GUI.skin.box)
                { fontSize = 14 };
                hs.normal.textColor = Color.white;
                var cv = s.corners[heldCorner];
                string info = $"[{s.name}] Corner {heldCorner + 1}: ({cv.x:F4}, {cv.y:F4})";
                GUI.Box(new Rect(Screen.width / 2 - 200, 10, 400, 30), info, hs);
            }
        }

        public static void DrawConfigWindow(int windowID, ProjectionMapperManager mgr)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            // --- Profile ---
            GUILayout.Label("--- Profile ---", Bold());
            GUILayout.BeginHorizontal();
            GUILayout.Label("Active:", GUILayout.Width(50));
            var names = mgr.GetProfileNames();
            if (names.Length > 0)
            {
                int cur = System.Array.IndexOf(names, mgr.CurrentProfileName);
                if (cur < 0) cur = 0;
                int pick = GUILayout.SelectionGrid(cur, names, Mathf.Min(names.Length, 3));
                if (pick != cur) mgr.SwitchProfile(names[pick]);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            _newProfileName = GUILayout.TextField(_newProfileName, GUILayout.Width(200));
            if (GUILayout.Button("New", GUILayout.Width(50))
                && !string.IsNullOrEmpty(_newProfileName))
            {
                mgr.CreateProfile(_newProfileName);
                _newProfileName = "";
            }
            if (GUILayout.Button("Del", GUILayout.Width(45)))
                mgr.DeleteCurrentProfile();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Force Save")) mgr.ForceSave();
            GUILayout.Space(8);

            // --- Display ---
            GUILayout.Label("--- Display ---", Bold());
            GUILayout.BeginHorizontal();
            mgr.editMode = GUILayout.Toggle(mgr.editMode, " Edit Mode");
            mgr.debugView = GUILayout.Toggle(mgr.debugView, " Debug View");
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            // --- Surfaces ---
            GUILayout.Label($"--- Surfaces ({mgr.surfaces.Count}) ---", Bold());
            if (GUILayout.Button("+ Add Surface")) mgr.AddSurface();

            int removeIdx = -1;
            for (int i = 0; i < mgr.surfaces.Count; i++)
            {
                var s = mgr.surfaces[i];
                bool sel = i == mgr.SelectedSurfaceIndex;

                GUILayout.BeginHorizontal();
                Color bg = GUI.backgroundColor;
                if (sel) GUI.backgroundColor = new Color(.4f, .6f, 1f);
                if (GUILayout.Button(sel ? "\u25BC" : "\u25B6", GUILayout.Width(25)))
                    mgr.SelectedSurfaceIndex = mgr.SelectedSurfaceIndex == i ? -1 : i;
                GUI.backgroundColor = bg;
                s.enabled = GUILayout.Toggle(s.enabled, "", GUILayout.Width(20));
                s.name = GUILayout.TextField(s.name, GUILayout.Width(140));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(25))) removeIdx = i;
                GUILayout.EndHorizontal();

                if (sel) DrawDetail(s);
            }
            if (removeIdx >= 0) mgr.RemoveSurface(removeIdx);

            GUILayout.Space(8);
            GUILayout.Label("--- Controls ---", Bold());
            GUILayout.Label("Hold 1/2/3/4 + Arrow: move corner");
            GUILayout.Label("  Shift: fine  |  Ctrl: coarse");
            GUILayout.Label("[ / ]: prev/next surface");

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private static void DrawDetail(ProjectionSurface s)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            // Display
            GUILayout.BeginHorizontal();
            GUILayout.Label("Display:", GUILayout.Width(65));
            string ds = GUILayout.TextField(s.targetDisplay.ToString(), GUILayout.Width(40));
            if (int.TryParse(ds, out int dv)) s.targetDisplay = Mathf.Clamp(dv, 0, 7);
            GUILayout.EndHorizontal();

            // Source
            GUILayout.BeginHorizontal();
            GUILayout.Label("Source:", GUILayout.Width(65));
            if (GUILayout.Toggle(s.sourceMode == SurfaceSourceMode.Camera, "Camera", GUILayout.Width(75)))
                s.sourceMode = SurfaceSourceMode.Camera;
            if (GUILayout.Toggle(s.sourceMode == SurfaceSourceMode.RenderTexture, "RenderTex", GUILayout.Width(90)))
                s.sourceMode = SurfaceSourceMode.RenderTexture;
            GUILayout.EndHorizontal();

            if (s.sourceMode == SurfaceSourceMode.Camera)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Cam:", GUILayout.Width(65));
                s.sourceCameraPath = GUILayout.TextField(s.sourceCameraPath, GUILayout.Width(210));
                if (GUILayout.Button("Find", GUILayout.Width(45)))
                {
                    var go = GameObject.Find(s.sourceCameraPath);
                    if (go != null) s.sourceCamera = go.GetComponent<Camera>();
                }
                GUILayout.EndHorizontal();
                string cn = s.sourceCamera != null ? s.sourceCamera.name : "(not bound)";
                GUILayout.Label($"  -> {cn}");

                GUILayout.BeginHorizontal();
                GUILayout.Label("Res:", GUILayout.Width(65));
                string ws = GUILayout.TextField(s.renderResolution.x.ToString(), GUILayout.Width(55));
                GUILayout.Label("x", GUILayout.Width(12));
                string hs = GUILayout.TextField(s.renderResolution.y.ToString(), GUILayout.Width(55));
                if (int.TryParse(ws, out int rw) && int.TryParse(hs, out int rh))
                    s.renderResolution = new Vector2Int(
                        Mathf.Clamp(rw, 64, 7680), Mathf.Clamp(rh, 64, 4320));
                GUILayout.EndHorizontal();
            }

            // AA
            GUILayout.BeginHorizontal();
            GUILayout.Label("AA:", GUILayout.Width(65));
            if (GUILayout.Toggle(s.aaQuality == AAQuality.None, "None", GUILayout.Width(55)))
                s.aaQuality = AAQuality.None;
            if (GUILayout.Toggle(s.aaQuality == AAQuality.Low, "2x", GUILayout.Width(40)))
                s.aaQuality = AAQuality.Low;
            if (GUILayout.Toggle(s.aaQuality == AAQuality.High, "4x", GUILayout.Width(40)))
                s.aaQuality = AAQuality.High;
            GUILayout.EndHorizontal();

            // Source crop
            GUILayout.Label("Source Crop (UV):");
            GUILayout.BeginHorizontal();
            GUILayout.Label("  X:", GUILayout.Width(25));
            string cxs = GUILayout.TextField(s.sourceCropUV.x.ToString("F3"), GUILayout.Width(50));
            GUILayout.Label("Y:", GUILayout.Width(18));
            string cys = GUILayout.TextField(s.sourceCropUV.y.ToString("F3"), GUILayout.Width(50));
            GUILayout.Label("W:", GUILayout.Width(20));
            string cws = GUILayout.TextField(s.sourceCropUV.width.ToString("F3"), GUILayout.Width(50));
            GUILayout.Label("H:", GUILayout.Width(18));
            string chs = GUILayout.TextField(s.sourceCropUV.height.ToString("F3"), GUILayout.Width(50));
            GUILayout.EndHorizontal();
            {
                float cx = s.sourceCropUV.x, cy = s.sourceCropUV.y;
                float cw = s.sourceCropUV.width, ch = s.sourceCropUV.height;
                if (float.TryParse(cxs, out float vx)) cx = Mathf.Clamp01(vx);
                if (float.TryParse(cys, out float vy)) cy = Mathf.Clamp01(vy);
                if (float.TryParse(cws, out float vw)) cw = Mathf.Clamp(vw, 0.01f, 1f);
                if (float.TryParse(chs, out float vh)) ch = Mathf.Clamp(vh, 0.01f, 1f);
                s.sourceCropUV = new Rect(cx, cy, cw, ch);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("  Sliders:", GUILayout.Width(55));
            if (GUILayout.Button("Full", GUILayout.Width(40)))
                s.sourceCropUV = new Rect(0, 0, 1, 1);
            if (GUILayout.Button("L\u00bd", GUILayout.Width(30)))
                s.sourceCropUV = new Rect(0, 0, 0.5f, 1);
            if (GUILayout.Button("R\u00bd", GUILayout.Width(30)))
                s.sourceCropUV = new Rect(0.5f, 0, 0.5f, 1);
            if (GUILayout.Button("L\u2153", GUILayout.Width(30)))
                s.sourceCropUV = new Rect(0, 0, 0.333f, 1);
            if (GUILayout.Button("M\u2153", GUILayout.Width(30)))
                s.sourceCropUV = new Rect(0.333f, 0, 0.334f, 1);
            if (GUILayout.Button("R\u2153", GUILayout.Width(30)))
                s.sourceCropUV = new Rect(0.667f, 0, 0.333f, 1);
            GUILayout.EndHorizontal();

            // Edge feather
            GUILayout.Label("Edge Feather:");
            GUILayout.BeginHorizontal();
            GUILayout.Label("  L:", GUILayout.Width(25));
            float fl = GUILayout.HorizontalSlider(s.edgeFeather.x, 0f, 0.5f, GUILayout.Width(70));
            GUILayout.Label("R:", GUILayout.Width(18));
            float fr = GUILayout.HorizontalSlider(s.edgeFeather.y, 0f, 0.5f, GUILayout.Width(70));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("  B:", GUILayout.Width(25));
            float fb = GUILayout.HorizontalSlider(s.edgeFeather.z, 0f, 0.5f, GUILayout.Width(70));
            GUILayout.Label("T:", GUILayout.Width(18));
            float ft = GUILayout.HorizontalSlider(s.edgeFeather.w, 0f, 0.5f, GUILayout.Width(70));
            GUILayout.EndHorizontal();
            s.edgeFeather = new Vector4(fl, fr, fb, ft);

            // Brightness / Gamma
            GUILayout.BeginHorizontal();
            GUILayout.Label("Bright:", GUILayout.Width(65));
            s.brightness = GUILayout.HorizontalSlider(s.brightness, .5f, 2f, GUILayout.Width(170));
            GUILayout.Label(s.brightness.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Gamma:", GUILayout.Width(65));
            s.gamma = GUILayout.HorizontalSlider(s.gamma, .2f, 3f, GUILayout.Width(170));
            GUILayout.Label(s.gamma.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Corners
            GUILayout.Label("Corners:");
            string[] cl = { "1 TL", "2 TR", "3 BR", "4 BL" };
            for (int c = 0; c < 4; c++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(cl[c], GUILayout.Width(40));
                string xs = GUILayout.TextField(s.corners[c].x.ToString("F4"), GUILayout.Width(65));
                string ys = GUILayout.TextField(s.corners[c].y.ToString("F4"), GUILayout.Width(65));
                if (float.TryParse(xs, out float fx) && float.TryParse(ys, out float fy))
                {
                    var nv = new Vector2(Mathf.Clamp01(fx), Mathf.Clamp01(fy));
                    if (nv != s.corners[c]) { s.corners[c] = nv; s.dirty = true; }
                }
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Reset Corners")) s.ResetCorners();

            GUILayout.EndVertical();
        }

        private static GUIStyle Bold()
        {
            var st = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, fontSize = 14 };
            return st;
        }
    }
}
