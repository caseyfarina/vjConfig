using System.Collections.Generic;
using UnityEngine;
namespace ProjectionMapper
{
    [ExecuteAlways]
    public class ProjectionMapperManager : MonoBehaviour
    {
        [Header("Controls")]
        [Tooltip("Key to toggle the configuration GUI.")]
        public KeyCode guiToggleKey = KeyCode.F12;

        [Header("Surfaces")]
        public List<ProjectionSurface> surfaces = new List<ProjectionSurface>();

        [Header("State")]
        public bool showGUI = false;
        public bool editMode = false;
        public bool debugView = false;

        public int SelectedSurfaceIndex
        {
            get => _selectedSurfaceIndex;
            set => _selectedSurfaceIndex = value;
        }
        public int HeldCorner => _heldCorner;
        public string CurrentProfileName => _currentProfileName;

        private ProfileCollection _profileCollection;
        private string _currentProfileName = "Default";
        private int _selectedSurfaceIndex = 0;
        private int _heldCorner = -1;
        private Shader _warpShader;
        private Shader _debugShader;
        private Rect _guiWindowRect = new Rect(20, 20, 420, 600);
        private float _stepNormal = 0.001f;
        private float _stepFine = 0.0001f;
        private float _stepCoarse = 0.01f;

        private void OnEnable()
        {
            _warpShader = Shader.Find("ProjectionMapper/HomographyWarp");
            _debugShader = Shader.Find("ProjectionMapper/DebugGrid");
            if (_warpShader == null) Debug.LogError("[ProjectionMapper] HomographyWarp shader not found.");
            if (_debugShader == null) Debug.LogError("[ProjectionMapper] DebugGrid shader not found.");
            _profileCollection = ProjectionPersistence.LoadCollection();
            _currentProfileName = _profileCollection.lastUsedProfile;
            LoadCurrentProfile();
        }

        private void OnDisable()
        {
            SaveCurrentProfile();
            CleanupAll();
        }

        private void OnApplicationQuit() => SaveCurrentProfile();

        private void Update()
        {
            if (Application.isPlaying) HandleInput();
            foreach (var s in surfaces)
            {
                if (s.sourceMode == SurfaceSourceMode.Camera) s.EnsureManagedRT();
                if (s.dirty) s.RecomputeHomography();
            }
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(guiToggleKey)) showGUI = !showGUI;

            // Skip keyboard controls when a GUI text field has focus
            if (GUIUtility.keyboardControl != 0) { _heldCorner = -1; return; }

            _heldCorner = -1;
            if (Input.GetKey(KeyCode.Keypad1) || Input.GetKey(KeyCode.Alpha1)) _heldCorner = 0;
            if (Input.GetKey(KeyCode.Keypad2) || Input.GetKey(KeyCode.Alpha2)) _heldCorner = 1;
            if (Input.GetKey(KeyCode.Keypad3) || Input.GetKey(KeyCode.Alpha3)) _heldCorner = 2;
            if (Input.GetKey(KeyCode.Keypad4) || Input.GetKey(KeyCode.Alpha4)) _heldCorner = 3;

            if (editMode && _heldCorner >= 0 && surfaces.Count > 0)
            {
                float step = _stepNormal;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step = _stepFine;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) step = _stepCoarse;
                int si = Mathf.Clamp(_selectedSurfaceIndex, 0, surfaces.Count - 1);
                Vector2 d = Vector2.zero;
                if (Input.GetKeyDown(KeyCode.LeftArrow))  d.x = -step;
                if (Input.GetKeyDown(KeyCode.RightArrow)) d.x =  step;
                if (Input.GetKeyDown(KeyCode.UpArrow))    d.y =  step;
                if (Input.GetKeyDown(KeyCode.DownArrow))  d.y = -step;
                float hr = step * 0.3f * Time.deltaTime / 0.016f;
                if (Input.GetKey(KeyCode.LeftArrow)  && !Input.GetKeyDown(KeyCode.LeftArrow))  d.x -= hr;
                if (Input.GetKey(KeyCode.RightArrow) && !Input.GetKeyDown(KeyCode.RightArrow)) d.x += hr;
                if (Input.GetKey(KeyCode.UpArrow)    && !Input.GetKeyDown(KeyCode.UpArrow))    d.y += hr;
                if (Input.GetKey(KeyCode.DownArrow)  && !Input.GetKeyDown(KeyCode.DownArrow))  d.y -= hr;
                if (d.sqrMagnitude > 0f) surfaces[si].MoveCorner(_heldCorner, d);
            }
            if (Input.GetKeyDown(KeyCode.LeftBracket))
                _selectedSurfaceIndex = Mathf.Max(0, _selectedSurfaceIndex - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                _selectedSurfaceIndex = Mathf.Min(surfaces.Count - 1, _selectedSurfaceIndex + 1);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            // Draw warped surfaces using GL during Repaint event (before IMGUI widgets)
            if (Event.current.type == EventType.Repaint)
            {
                int disp = 0;
                if (debugView)
                    ProjectionRenderer.RenderDebugView(surfaces, disp, _debugShader);
                else
                    ProjectionRenderer.RenderWarpedSurfaces(surfaces, disp, _warpShader);
                if (editMode)
                    ProjectionRenderer.RenderEditOverlays(surfaces, disp, _selectedSurfaceIndex, _heldCorner);
            }

            if (editMode) ProjectionGUI.DrawEditLabels(surfaces, _selectedSurfaceIndex, _heldCorner);
            if (!showGUI) return;
            GUI.skin.label.fontSize = 13;
            GUI.skin.button.fontSize = 13;
            GUI.skin.textField.fontSize = 13;
            GUI.skin.toggle.fontSize = 13;
            _guiWindowRect = GUILayout.Window(9999, _guiWindowRect,
                (id) => ProjectionGUI.DrawConfigWindow(id, this),
                "Projection Mapper Config");
        }

        // --- Public API for GUI ---
        public string[] GetProfileNames()
        {
            if (_profileCollection == null) return new string[0];
            var n = new string[_profileCollection.profiles.Count];
            for (int i = 0; i < n.Length; i++) n[i] = _profileCollection.profiles[i].profileName;
            return n;
        }
        public void SwitchProfile(string name) { SaveCurrentProfile(); _currentProfileName = name; LoadCurrentProfile(); }
        public void CreateProfile(string name) { SaveCurrentProfile(); _currentProfileName = name; surfaces.Clear(); SaveCurrentProfile(); }
        public void DeleteCurrentProfile()
        {
            if (_profileCollection.profiles.Count <= 1) return;
            var p = _profileCollection.profiles.Find(x => x.profileName == _currentProfileName);
            if (p != null) { _profileCollection.profiles.Remove(p); _currentProfileName = _profileCollection.profiles[0].profileName; LoadCurrentProfile(); ProjectionPersistence.SaveCollection(_profileCollection); }
        }
        public void ForceSave() => SaveCurrentProfile();
        public void AddSurface() { var s = new ProjectionSurface { name = $"Surface {surfaces.Count + 1}" }; surfaces.Add(s); _selectedSurfaceIndex = surfaces.Count - 1; }
        public void RemoveSurface(int i) { if (i >= 0 && i < surfaces.Count) { surfaces[i].Cleanup(); surfaces.RemoveAt(i); _selectedSurfaceIndex = Mathf.Clamp(_selectedSurfaceIndex, 0, surfaces.Count - 1); } }

        // --- Persistence ---
        private void LoadCurrentProfile()
        {
            CleanupAll();
            surfaces = ProjectionPersistence.LoadProfile(_profileCollection, _currentProfileName);
            foreach (var s in surfaces)
            {
                if (s.sourceMode == SurfaceSourceMode.Camera && !string.IsNullOrEmpty(s.sourceCameraPath))
                {
                    var go = GameObject.Find(s.sourceCameraPath);
                    if (go != null) s.sourceCamera = go.GetComponent<Camera>();
                }
                s.dirty = true;
            }
        }

        private void SaveCurrentProfile()
        {
            if (_profileCollection == null) return;
            foreach (var s in surfaces)
                if (s.sourceCamera != null) s.sourceCameraPath = GetPath(s.sourceCamera.gameObject);
            ProjectionPersistence.SaveProfile(_profileCollection, _currentProfileName, surfaces);
        }

        private void CleanupAll() { if (surfaces != null) foreach (var s in surfaces) s.Cleanup(); }
        private static string GetPath(GameObject go)
        {
            if (go == null) return "";
            string p = go.name; var t = go.transform.parent;
            while (t != null) { p = t.name + "/" + p; t = t.parent; }
            return p;
        }
    }
}
