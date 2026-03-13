using UnityEngine;
using ProjectionMapper;

namespace VJSystem
{
    public class DualDeckGUI : MonoBehaviour
    {
        public DualDeckManager deckManager;
        public OutputManager outputManager;
        public MidiDebugMonitor midiMonitor;
        public DualDeckPostFXRouter postFXRouter;

        int _activeTab = 0;
        Vector2 _scrollPos;
        bool _guiVisible = true;

        GUIStyle _headerStyle;
        GUIStyle _takeBtnStyle;
        GUIStyle _smallLabel;
        bool _stylesInit;

        // Keyboard shortcuts handled in OnGUI via Event.current so they work
        // regardless of whether the project uses the legacy or new Input System.
        void HandleKeyboardShortcuts()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.F1)
            {
                _guiVisible = !_guiVisible;
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.Space && deckManager != null)
            {
                deckManager.Take();
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.Escape)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                e.Use();
            }
        }

        void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _headerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _takeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };

            _smallLabel = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        }

        void OnGUI()
        {
            HandleKeyboardShortcuts();
            if (!_guiVisible || deckManager == null) return;
            InitStyles();

            const float targetW = 1920f;
            const float targetH = 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(Screen.width / targetW, Screen.height / targetH, 1f));

            float sw = targetW;
            float sh = targetH;

            // Top bar
            GUILayout.BeginArea(new Rect(0, 0, sw, 40));
            GUILayout.BeginHorizontal(GUI.skin.box);

            string[] tabNames = { "MASTER", "DECK A", "DECK B", "MAPPING", "MIDI" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                GUI.backgroundColor = _activeTab == i ? Color.cyan : Color.gray;
                if (GUILayout.Button(tabNames[i], GUILayout.Width(90), GUILayout.Height(30)))
                    _activeTab = i;
            }

            GUILayout.FlexibleSpace();

            bool liveA = deckManager.liveDeck == DeckIdentity.A;
            GUI.backgroundColor = Color.red;
            GUILayout.Label(liveA ? " LIVE: DECK A " : " LIVE: DECK B ", _headerStyle,
                GUILayout.Width(140), GUILayout.Height(30));

            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("TAKE (Space)", _takeBtnStyle, GUILayout.Width(130), GUILayout.Height(30)))
                deckManager.Take();

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Main panel
            GUILayout.BeginArea(new Rect(4, 44, sw - 8, sh - 68));
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            switch (_activeTab)
            {
                case 0: DrawMasterPanel(); break;
                case 1: DrawDeckPanel(deckManager.stageA, "DECK A"); break;
                case 2: DrawDeckPanel(deckManager.stageB, "DECK B"); break;
                case 3: DrawMappingPanel(); break;
                case 4: DrawMidiPanel(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Footer
            GUI.Label(new Rect(4, sh - 22, sw, 20),
                "Esc: Quit | F1: Toggle GUI | Space: Take | Tab: Switch mapping output | Hold 1-4 + Arrows: Move corners (Shift=fine, Ctrl=coarse)");
        }

        // ==================== MASTER PANEL ====================

        void DrawMasterPanel()
        {
            var live = deckManager.LiveStage;
            var standby = deckManager.StandbyStage;
            string liveName = deckManager.liveDeck == DeckIdentity.A ? "DECK A" : "DECK B";
            string standbyName = deckManager.liveDeck == DeckIdentity.A ? "DECK B" : "DECK A";

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            GUILayout.Label($" LIVE OUTPUT ({liveName}) ", _headerStyle);
            GUI.backgroundColor = Color.white;

            if (live.cameraRig != null)
            {
                GUILayout.BeginHorizontal();
                DrawPreview(live.cameraRig.GetRT(0), "Projector 1");
                DrawPreview(live.cameraRig.GetRT(1), "Projector 2");
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
            GUILayout.Label($" STANDBY ({standbyName}) ", _headerStyle);
            GUI.backgroundColor = Color.white;

            if (standby.cameraRig != null)
            {
                GUILayout.BeginHorizontal();
                DrawPreview(standby.cameraRig.GetRT(0), "Cam 1");
                DrawPreview(standby.cameraRig.GetRT(1), "Cam 2");
                GUILayout.EndHorizontal();
            }
        }

        void DrawPreview(RenderTexture rt, string label)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(460));
            GUILayout.Label(label);
            if (rt != null)
            {
                Rect r = GUILayoutUtility.GetRect(456, 256);
                GUI.DrawTexture(r, rt, ScaleMode.ScaleToFit);
            }
            else
            {
                GUILayout.Label("(no render texture)");
            }
            GUILayout.EndVertical();
        }

        // ==================== DECK PANELS ====================

        void DrawDeckPanel(StageController stage, string deckName)
        {
            if (stage == null) return;
            bool isLive = deckManager.LiveStage == stage;

            GUI.backgroundColor = isLive ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.4f);
            GUILayout.Label(isLive ? $" {deckName} - LIVE " : $" {deckName} - STANDBY (EDITING) ",
                _headerStyle);
            GUI.backgroundColor = Color.white;

            if (stage.cameraRig != null)
            {
                GUILayout.BeginHorizontal();
                DrawPreview(stage.cameraRig.GetRT(0), "Cam 1");
                DrawPreview(stage.cameraRig.GetRT(1), "Cam 2");
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            if (stage.cameraRig != null) DrawCameraControls(stage.cameraRig);
            GUILayout.Space(8);
            if (stage.lightRig != null) DrawLightControls(stage.lightRig);
            GUILayout.Space(8);
            DrawContentControls(stage);
            GUILayout.Space(8);
            bool isA = deckManager.stageA == stage;
            var spawn = postFXRouter != null ? (isA ? postFXRouter.meshSpawnA : postFXRouter.meshSpawnB) : null;
            DrawMeshSpawnControls(spawn);
            GUILayout.Space(8);
            DeckIdentity deckId = isA ? DeckIdentity.A : DeckIdentity.B;
            DrawPostFXControls(deckId, isLive);
        }

        void DrawCameraControls(DeckCameraRig rig)
        {
            GUILayout.Label(" Camera Controls ", _headerStyle);

            for (int i = 0; i < 2; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Cam {i + 1}:", GUILayout.Width(50));
                CameraBehavior cur = rig.GetBehavior(i);
                int camIdx = i;

                DrawBehaviorButton("Still", CameraBehavior.Still, cur, () => rig.SetBehavior(camIdx, CameraBehavior.Still));
                DrawBehaviorButton("Orbit", CameraBehavior.Orbit, cur, () => rig.SetBehavior(camIdx, CameraBehavior.Orbit));
                DrawBehaviorButton("Push", CameraBehavior.Push, cur, () => rig.SetBehavior(camIdx, CameraBehavior.Push));

                if (cur == CameraBehavior.Still)
                {
                    if (GUILayout.Button("Reshuffle", GUILayout.Width(70)))
                        rig.SetBehavior(camIdx, CameraBehavior.Still);
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            SliderRow("Orbit Speed:", ref rig.orbitSpeed, 1f, 45f, "F1", " deg/s");
            SliderRow("Orbit Radius:", ref rig.orbitRadius, 2f, 20f, "F1", "m");
            SliderRow("Cam Height:", ref rig.orbitHeight, 0.5f, 10f, "F1", "m");
        }

        void DrawBehaviorButton(string label, CameraBehavior behavior, CameraBehavior current, System.Action onClick)
        {
            GUI.backgroundColor = current == behavior ? Color.cyan : Color.white;
            if (GUILayout.Button(label, GUILayout.Width(60))) onClick();
        }

        void DrawLightControls(DeckLightRig rig)
        {
            GUILayout.Label(" Lighting ", _headerStyle);
            bool changed = false;

            // Count
            GUILayout.BeginHorizontal();
            GUILayout.Label("Count:", GUILayout.Width(80));
            int newCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(rig.activeLightCount, 0, 50, GUILayout.Width(200)));
            GUILayout.Label($"{newCount}", GUILayout.Width(30));
            if (newCount != rig.activeLightCount) { rig.activeLightCount = newCount; changed = true; }
            GUILayout.EndHorizontal();

            // Saturation
            GUILayout.BeginHorizontal();
            GUILayout.Label("Saturation:", GUILayout.Width(80));
            float newSat = GUILayout.HorizontalSlider(rig.lightSaturation, 0f, 1f, GUILayout.Width(200));
            GUILayout.Label($"{newSat:F2}", GUILayout.Width(40));
            if (Mathf.Abs(newSat - rig.lightSaturation) > 0.005f) { rig.lightSaturation = newSat; changed = true; }
            GUILayout.EndHorizontal();

            // Hue
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hue:", GUILayout.Width(80));
            float newHue = GUILayout.HorizontalSlider(rig.hue, 0f, 360f, GUILayout.Width(200));
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.HSVToRGB(newHue / 360f, Mathf.Max(0.5f, rig.lightSaturation), 1f);
            GUILayout.Label("   ", GUI.skin.box, GUILayout.Width(30));
            GUI.backgroundColor = oldBg;
            if (Mathf.Abs(newHue - rig.hue) > 0.5f) { rig.hue = newHue; changed = true; }
            GUILayout.EndHorizontal();

            // Spread
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hue Spread:", GUILayout.Width(80));
            float newSpread = GUILayout.HorizontalSlider(rig.hueSpread, 0f, 100f, GUILayout.Width(200));
            GUILayout.Label($"{newSpread:F0}%", GUILayout.Width(40));
            if (Mathf.Abs(newSpread - rig.hueSpread) > 0.5f) { rig.hueSpread = newSpread; changed = true; }
            GUILayout.EndHorizontal();

            // Hemisphere Radius
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hemi Radius:", GUILayout.Width(80));
            float newRadius = GUILayout.HorizontalSlider(rig.lightRadius, 1f, 30f, GUILayout.Width(200));
            GUILayout.Label($"{newRadius:F1}m", GUILayout.Width(40));
            if (Mathf.Abs(newRadius - rig.lightRadius) > 0.05f) { rig.lightRadius = newRadius; changed = true; }
            GUILayout.EndHorizontal();

            // Intensity
            GUILayout.BeginHorizontal();
            GUILayout.Label("Intensity:", GUILayout.Width(80));
            float newInt = GUILayout.HorizontalSlider(rig.lightIntensity, 0f, 30f, GUILayout.Width(200));
            GUILayout.Label($"{newInt:F1}", GUILayout.Width(40));
            if (Mathf.Abs(newInt - rig.lightIntensity) > 0.05f) { rig.lightIntensity = newInt; changed = true; }
            GUILayout.EndHorizontal();

            if (changed) rig.UpdateLights();
        }

        void DrawContentControls(StageController stage)
        {
            GUILayout.Label(" Content ", _headerStyle);
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Calibration Grid", GUILayout.Width(110)))
                stage.SpawnCalibrationGrid();
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Cubes (12)", GUILayout.Width(90)))
                stage.SpawnTestContent(PrimitiveType.Cube, 12);
            if (GUILayout.Button("Spheres (12)", GUILayout.Width(90)))
                stage.SpawnTestContent(PrimitiveType.Sphere, 12);
            if (GUILayout.Button("Cylinders (8)", GUILayout.Width(100)))
                stage.SpawnTestContent(PrimitiveType.Cylinder, 8);
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
                stage.ClearContent();
            GUILayout.EndHorizontal();
        }

        // ==================== POST FX CONTROLS ====================

        void DrawPostFXControls(DeckIdentity deck, bool isLive)
        {
            if (postFXRouter == null) return;

            var snap = postFXRouter.GetSnapshot(deck);
            string liveTag = isLive ? " (MIDI Ch 1-8)" : " (pre-stage)";
            GUILayout.Label($" Post FX{liveTag} ", _headerStyle);

            // Row 1 — Glitch (Ch 1-4). Glitch only applies to live deck.
            GUILayout.BeginHorizontal();
            PostFXSlider("ScanJitter", snap.scanLineJitter,  deck, 1, isLive ? 1f : 0f, isLive);
            PostFXSlider("VrtJump",    snap.verticalJump,    deck, 2, isLive ? 1f : 0f, isLive);
            PostFXSlider("ColorDrift", snap.colorDrift,      deck, 3, isLive ? 1f : 0f, isLive);
            PostFXSlider("DigGlitch",  snap.digitalIntensity,deck, 4, isLive ? 1f : 0f, isLive);
            GUILayout.EndHorizontal();

            // Row 2 — Volume FX (Ch 5-8). Both decks controllable.
            GUILayout.BeginHorizontal();
            PostFXSlider("LensFlare",  snap.lensFlare,        deck, 5, 1f, true);
            PostFXSlider("Flares",     snap.flareMultipliers, deck, 6, 1f, true);
            PostFXSlider("Streaks",    snap.streaks,          deck, 7, 1f, true);
            PostFXSlider("Bloom",      snap.bloom,            deck, 8, 1f, true);
            GUILayout.EndHorizontal();
        }

        void PostFXSlider(string label, float value, DeckIdentity deck, int ch, float max, bool interactive)
        {
            GUILayout.BeginVertical(GUILayout.Width(110));
            GUILayout.Label(label, _smallLabel);
            GUI.enabled = interactive;
            float next = GUILayout.HorizontalSlider(value, 0f, max, GUILayout.Width(100));
            GUI.enabled = true;
            GUILayout.Label($"{value:F2}", _smallLabel);
            GUILayout.EndVertical();

            if (interactive && Mathf.Abs(next - value) > 0.001f)
                postFXRouter.SetDeckFX(deck, ch, next);
        }

        // ==================== MAPPING PANEL ====================

        void DrawMappingPanel()
        {
            if (outputManager == null)
            {
                GUILayout.Label("OutputManager not assigned.", _headerStyle);
                return;
            }

            GUILayout.Label(" PROJECTION MAPPING ", _headerStyle);
            GUILayout.Space(4);

            GUILayout.Label("Keyboard: Tab=switch output, Hold 1/2/3/4 + Arrows=move corners, Shift=fine, Ctrl=coarse",
                _smallLabel);
            GUILayout.Space(8);

            // Output selector
            GUILayout.BeginHorizontal();
            GUILayout.Label("Editing Output:", GUILayout.Width(100));
            GUI.backgroundColor = outputManager.SelectedOutput == 0 ? Color.cyan : Color.white;
            if (GUILayout.Button("Projector 1", GUILayout.Width(100))) outputManager.SelectedOutput = 0;
            GUI.backgroundColor = outputManager.SelectedOutput == 1 ? Color.cyan : Color.white;
            if (GUILayout.Button("Projector 2", GUILayout.Width(100))) outputManager.SelectedOutput = 1;
            GUI.backgroundColor = Color.white;

            if (outputManager.HeldCorner >= 0)
            {
                string[] cornerNames = { "TL", "TR", "BR", "BL" };
                GUI.backgroundColor = Color.yellow;
                GUILayout.Label($"  Corner: {cornerNames[outputManager.HeldCorner]}", GUI.skin.box);
                GUI.backgroundColor = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Draw controls for both outputs
            DrawOutputMappingControls(outputManager.GetOutput(0), "Projector 1", 0);
            GUILayout.Space(10);
            DrawOutputMappingControls(outputManager.GetOutput(1), "Projector 2", 1);
        }

        void DrawOutputMappingControls(ProjectionSurface surface, string label, int outputIndex)
        {
            bool isSelected = outputManager.SelectedOutput == outputIndex;
            GUI.backgroundColor = isSelected ? new Color(0.6f, 0.8f, 1f) : Color.white;
            GUILayout.Label($" {label} ", _headerStyle);
            GUI.backgroundColor = Color.white;

            // Corner positions
            GUILayout.Label("Corners (TL, TR, BR, BL):", _smallLabel);
            string[] names = { "TL", "TR", "BR", "BL" };
            bool dirty = false;

            for (int c = 0; c < 4; c++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"  {names[c]}:", GUILayout.Width(30));

                GUILayout.Label("X:", GUILayout.Width(15));
                float newX = GUILayout.HorizontalSlider(surface.corners[c].x, 0f, 1f, GUILayout.Width(120));
                GUILayout.Label($"{newX:F3}", GUILayout.Width(40));

                GUILayout.Label("Y:", GUILayout.Width(15));
                float newY = GUILayout.HorizontalSlider(surface.corners[c].y, 0f, 1f, GUILayout.Width(120));
                GUILayout.Label($"{newY:F3}", GUILayout.Width(40));

                if (Mathf.Abs(newX - surface.corners[c].x) > 0.0001f ||
                    Mathf.Abs(newY - surface.corners[c].y) > 0.0001f)
                {
                    surface.corners[c] = new Vector2(newX, newY);
                    dirty = true;
                }
                GUILayout.EndHorizontal();
            }

            // Crop
            GUILayout.Label("Crop:", _smallLabel);
            GUILayout.BeginHorizontal();
            Rect crop = surface.sourceCropUV;
            GUILayout.Label("L:", GUILayout.Width(15));
            crop.x = GUILayout.HorizontalSlider(crop.x, 0f, 0.5f, GUILayout.Width(80));
            GUILayout.Label("R:", GUILayout.Width(15));
            crop.width = GUILayout.HorizontalSlider(crop.width, 0.5f, 1f, GUILayout.Width(80));
            GUILayout.Label("B:", GUILayout.Width(15));
            crop.y = GUILayout.HorizontalSlider(crop.y, 0f, 0.5f, GUILayout.Width(80));
            GUILayout.Label("T:", GUILayout.Width(15));
            crop.height = GUILayout.HorizontalSlider(crop.height, 0.5f, 1f, GUILayout.Width(80));
            if (crop.x != surface.sourceCropUV.x || crop.y != surface.sourceCropUV.y ||
                crop.width != surface.sourceCropUV.width || crop.height != surface.sourceCropUV.height)
            {
                surface.sourceCropUV = crop;
                dirty = true;
            }
            GUILayout.EndHorizontal();

            // Brightness & Gamma
            GUILayout.BeginHorizontal();
            float newBright = surface.brightness;
            float newGamma = surface.gamma;
            GUILayout.Label("Brightness:", GUILayout.Width(70));
            newBright = GUILayout.HorizontalSlider(newBright, 0.2f, 2f, GUILayout.Width(100));
            GUILayout.Label($"{newBright:F2}", GUILayout.Width(35));
            GUILayout.Label("Gamma:", GUILayout.Width(50));
            newGamma = GUILayout.HorizontalSlider(newGamma, 0.2f, 3f, GUILayout.Width(100));
            GUILayout.Label($"{newGamma:F2}", GUILayout.Width(35));
            if (Mathf.Abs(newBright - surface.brightness) > 0.001f) { surface.brightness = newBright; dirty = true; }
            if (Mathf.Abs(newGamma - surface.gamma) > 0.001f) { surface.gamma = newGamma; dirty = true; }
            GUILayout.EndHorizontal();

            // Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Corners", GUILayout.Width(100)))
            {
                surface.corners[0] = new Vector2(0f, 1f);
                surface.corners[1] = new Vector2(1f, 1f);
                surface.corners[2] = new Vector2(1f, 0f);
                surface.corners[3] = new Vector2(0f, 0f);
                dirty = true;
            }
            if (GUILayout.Button("Reset Crop", GUILayout.Width(100)))
            {
                surface.sourceCropUV = new Rect(0, 0, 1, 1);
                dirty = true;
            }
            if (GUILayout.Button("Reset All", GUILayout.Width(80)))
            {
                surface.corners[0] = new Vector2(0f, 1f);
                surface.corners[1] = new Vector2(1f, 1f);
                surface.corners[2] = new Vector2(1f, 0f);
                surface.corners[3] = new Vector2(0f, 0f);
                surface.sourceCropUV = new Rect(0, 0, 1, 1);
                surface.brightness = 1f;
                surface.gamma = 1f;
                dirty = true;
            }
            GUILayout.EndHorizontal();

            if (dirty) surface.dirty = true;
        }

        // ==================== MIDI PANEL ====================

        void DrawMidiPanel()
        {
            GUILayout.Label(" MIDI DEBUG MONITOR ", _headerStyle);
            GUILayout.Space(4);

            if (midiMonitor == null)
            {
                GUILayout.Label("MidiDebugMonitor not assigned.", _smallLabel);
                return;
            }

            // Device status
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"Device:  {midiMonitor.DeviceName}", GUILayout.Width(300));
            GUILayout.Label($"Events:  {midiMonitor.TotalEvents}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
                midiMonitor.ClearLog();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // ---- InputSystem device list ----
            GUILayout.Label(" InputSystem Devices ", _headerStyle);
            var devices = midiMonitor.DeviceList;
            if (devices.Count == 0)
            {
                GUILayout.Label("  (no InputSystem devices found)", _smallLabel);
            }
            else
            {
                foreach (var d in devices)
                {
                    GUI.contentColor = d.StartsWith("[MIDI]") ? Color.cyan : new Color(0.5f, 0.5f, 0.5f);
                    GUILayout.Label($"  {d}", _smallLabel);
                }
                GUI.contentColor = Color.white;
            }

            GUILayout.Space(6);

            // ---- MF64 8x8 grid ----
            GUILayout.Label(" Midi Fighter 64 ", _headerStyle);
            GUILayout.Space(2);

            float cellSize = 34f;
            string[] rowLabels = { "R1", "R2", "R3", "R4", "R5", "R6", "R7", "R8" };

            for (int r = 0; r < 8; r++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(rowLabels[r], _smallLabel, GUILayout.Width(22));
                for (int c = 0; c < 8; c++)
                {
                    GUI.backgroundColor = midiMonitor.GridState[r, c]
                        ? Color.green
                        : new Color(0.2f, 0.2f, 0.2f);
                    GUILayout.Box($"{c + 1}", GUILayout.Width(cellSize), GUILayout.Height(cellSize));
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);

            // ---- MIDI Mix strips ----
            GUILayout.Label(" Akai MIDI Mix ", _headerStyle);
            GUILayout.Space(2);

            float stripW = 52f;
            float barH   = 6f;

            // Column headers
            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(52));
            for (int ch = 0; ch < 8; ch++)
                GUILayout.Label($" Ch{ch + 1}", _smallLabel, GUILayout.Width(stripW));
            GUILayout.Label(" Mst", _smallLabel, GUILayout.Width(stripW));
            GUILayout.EndHorizontal();

            // Knob rows
            for (int row = 0; row < 3; row++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Knob R{row + 1}", _smallLabel, GUILayout.Width(52));
                for (int ch = 0; ch < 8; ch++)
                {
                    float v = midiMonitor.KnobValues[row, ch];
                    GUI.backgroundColor = Color.Lerp(new Color(0.15f, 0.15f, 0.2f), new Color(0.3f, 0.7f, 1f), v);
                    GUILayout.Box($"{v:F2}", _smallLabel, GUILayout.Width(stripW), GUILayout.Height(barH + 14));
                }
                GUI.backgroundColor = Color.white;
                GUILayout.Label("—", _smallLabel, GUILayout.Width(stripW));
                GUILayout.EndHorizontal();
            }

            // Faders
            GUILayout.BeginHorizontal();
            GUILayout.Label("Fader", _smallLabel, GUILayout.Width(52));
            for (int ch = 0; ch < 8; ch++)
            {
                float v = midiMonitor.FaderValues[ch];
                GUI.backgroundColor = Color.Lerp(new Color(0.1f, 0.1f, 0.15f), new Color(0.25f, 0.7f, 1f), v);
                GUILayout.Box($"{v:F2}", _smallLabel, GUILayout.Width(stripW), GUILayout.Height(barH + 14));
            }
            GUI.backgroundColor = Color.Lerp(new Color(0.1f, 0.1f, 0.15f), new Color(0.25f, 0.7f, 1f), midiMonitor.MasterFader);
            GUILayout.Box($"{midiMonitor.MasterFader:F2}", _smallLabel, GUILayout.Width(stripW), GUILayout.Height(barH + 14));
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // Mute buttons
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mute", _smallLabel, GUILayout.Width(52));
            for (int ch = 0; ch < 8; ch++)
            {
                GUI.backgroundColor = midiMonitor.MuteState[ch]
                    ? new Color(0.15f, 0.9f, 0.25f)
                    : new Color(0.08f, 0.18f, 0.08f);
                GUILayout.Box("M", _smallLabel, GUILayout.Width(stripW), GUILayout.Height(20));
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Label("", GUILayout.Width(stripW));
            GUILayout.EndHorizontal();

            // Rec Arm buttons
            GUILayout.BeginHorizontal();
            GUILayout.Label("Rec Arm", _smallLabel, GUILayout.Width(52));
            for (int ch = 0; ch < 8; ch++)
            {
                GUI.backgroundColor = midiMonitor.RecArmState[ch]
                    ? new Color(0.95f, 0.15f, 0.1f)
                    : new Color(0.2f, 0.06f, 0.06f);
                GUILayout.Box("R", _smallLabel, GUILayout.Width(stripW), GUILayout.Height(20));
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Label("", GUILayout.Width(stripW));
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // ---- Event log ----
            GUILayout.Label(" Event Log (newest first) ", _headerStyle);
            GUILayout.Space(2);

            var log = midiMonitor.EventLog;
            if (log.Count == 0)
            {
                GUILayout.Label("  (no events — connect a device and press a button or move a fader)", _smallLabel);
            }
            else
            {
                for (int i = 0; i < log.Count; i++)
                {
                    GUI.contentColor = i == 0 ? Color.white : new Color(0.65f, 0.65f, 0.65f);
                    GUILayout.Label($"  {log[i]}", _smallLabel);
                }
                GUI.contentColor = Color.white;
            }
        }

        // ==================== MESH SPAWN CONTROLS ====================

        void DrawMeshSpawnControls(MeshSpawnSystem spawn)
        {
            GUILayout.Label(" Mesh Spawn ", _headerStyle);

            if (spawn == null)
            {
                GUILayout.Label("  MeshSpawnSystem not assigned.", _smallLabel);
                return;
            }

            // Walk
            SliderRow("Step Size:",    ref spawn.stepMagnitude,   0.1f,  8f,   "F2", "m");
            SliderRow("Step Variance:",ref spawn.stepVariance,    0f,    4f,   "F2", "m");
            SliderRow("Spawn Height:", ref spawn.spawnHeight,     0f,    8f,   "F2", "m");
            SliderRow("Height Range:", ref spawn.spawnHeightRange,0f,    8f,   "F2", "m");
            SliderRow("Walk Radius:",  ref spawn.walkRadius,      1f,    20f,  "F1", "m");

            // Scale
            SliderRow("Min Scale:",    ref spawn.minScale,        0.05f, 3f,   "F2", "");
            SliderRow("Max Scale:",    ref spawn.maxScale,        0.1f,  5f,   "F2", "");
            SliderRow("Scale In:",     ref spawn.scaleInDuration, 0.05f, 2f,   "F2", "s");
            SliderRow("Scale Out:",    ref spawn.scaleOutDuration,0.05f, 2f,   "F2", "s");

            // Per-instance rotation range
            SliderRow("Rot Min:",      ref spawn.rotationSpeedMin, 0f,  90f,  "F1", "°/s");
            SliderRow("Rot Max:",      ref spawn.rotationSpeedMax, 0f,  180f, "F1", "°/s");

            // Global rotation multiplier (shared across both stages)
            GUILayout.BeginHorizontal();
            GUILayout.Label("Global Rot×:", GUILayout.Width(80));
            float newMult = GUILayout.HorizontalSlider(SpawnedMeshObject.globalSpeedMultiplier, 0.05f, 2f, GUILayout.Width(200));
            GUILayout.Label($"{newMult:F2}×", GUILayout.Width(50));
            if (Mathf.Abs(newMult - SpawnedMeshObject.globalSpeedMultiplier) > 0.01f)
                SpawnedMeshObject.globalSpeedMultiplier = newMult;
            GUILayout.EndHorizontal();
        }

        // ==================== HELPERS ====================

        void SliderRow(string label, ref float value, float min, float max, string fmt, string suffix)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(80));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(200));
            GUILayout.Label($"{value.ToString(fmt)}{suffix}", GUILayout.Width(80));
            GUILayout.EndHorizontal();
        }
    }
}
