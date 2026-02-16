using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VJSystem
{
    [System.Serializable]
    public class RuntimePreset
    {
        public string name;        // auto: "Saved_HH:MM:SS"
        public string effectType;  // "DoF" | "PixelSort" | "Chromatic"
        public string jsonData;    // JsonUtility.ToJson of PresetData
        public string savedAt;     // DateTime.Now.ToString()
    }

    [System.Serializable]
    public class RuntimePresetList
    {
        public List<RuntimePreset> presets = new();
    }

    /// <summary>
    /// Saves and loads runtime presets to JSON in Application.persistentDataPath.
    /// Trigger: Ctrl+S. Recall: Alt + Arrow keys + Enter.
    /// </summary>
    public class PresetSaveSystem : MonoBehaviour
    {
        [SerializeField] PostFXRouter postFXRouter;
        [SerializeField] KeyCode saveKey = KeyCode.S;

        public static event Action<string> OnPresetSaved;  // preset name

        RuntimePresetList _presetList = new();
        int _recallIndex = -1;
        bool _recallMode;

        const string SAVE_FILENAME = "vj_presets.json";

        public int PresetCount => _presetList.presets.Count;
        public bool RecallMode => _recallMode;
        public int RecallIndex => _recallIndex;

        public RuntimePreset GetPresetAtRecallIndex()
        {
            if (_recallIndex < 0 || _recallIndex >= _presetList.presets.Count)
                return null;
            return _presetList.presets[_recallIndex];
        }

        string SavePath => Path.Combine(Application.persistentDataPath, SAVE_FILENAME);

        void Awake()
        {
            LoadFromDisk();
        }

        void Update()
        {
            // Save: Ctrl+S
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(saveKey))
            {
                SaveCurrentPreset();
            }

            // Recall mode: Alt held
            bool altHeld = Input.GetKey(KeyCode.LeftAlt);
            if (altHeld && !_recallMode)
            {
                _recallMode = true;
                _recallIndex = _presetList.presets.Count > 0 ? 0 : -1;
            }
            else if (!altHeld && _recallMode)
            {
                _recallMode = false;
            }

            if (_recallMode && _presetList.presets.Count > 0)
            {
                // Filter to active effect type
                var filtered = GetFilteredPresets();

                if (Input.GetKeyDown(KeyCode.DownArrow))
                    _recallIndex = Mathf.Min(_recallIndex + 1, filtered.Count - 1);
                if (Input.GetKeyDown(KeyCode.UpArrow))
                    _recallIndex = Mathf.Max(_recallIndex - 1, 0);

                if (Input.GetKeyDown(KeyCode.Return) && _recallIndex >= 0 && _recallIndex < filtered.Count)
                {
                    ApplyRuntimePreset(filtered[_recallIndex]);
                }
            }
        }

        void SaveCurrentPreset()
        {
            var system = postFXRouter.ActiveSystem;
            if (system == null) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string name = $"Saved_{timestamp}";

            var preset = new RuntimePreset
            {
                name       = name,
                effectType = system.EffectTypeName,
                jsonData   = system.CaptureCurrentState(name),
                savedAt    = DateTime.Now.ToString()
            };

            _presetList.presets.Add(preset);
            WriteToDisk();

            OnPresetSaved?.Invoke(name);
        }

        void ApplyRuntimePreset(RuntimePreset preset)
        {
            if (preset == null) return;

            IPostFXSystem system = preset.effectType switch
            {
                "DoF"       => postFXRouter.GetSystemForRow(2),
                "PixelSort" => postFXRouter.GetSystemForRow(3),
                "Chromatic" => postFXRouter.GetSystemForRow(4),
                _ => null
            };

            if (system == null) return;

            // Deserialize and apply based on type
            switch (preset.effectType)
            {
                case "DoF":
                    var dofData = JsonUtility.FromJson<DoFPresetData>(preset.jsonData);
                    if (system is DepthOfFieldSystem dofSys)
                        dofSys.ApplyData(dofData);
                    break;
                case "PixelSort":
                    var psData = JsonUtility.FromJson<PixelSortPresetData>(preset.jsonData);
                    if (system is PixelSortSystem psSys)
                        psSys.ApplyData(psData);
                    break;
                case "Chromatic":
                    var chrData = JsonUtility.FromJson<ChromaticDisplacementPresetData>(preset.jsonData);
                    if (system is ChromaticDisplacementSystem chrSys)
                        chrSys.ApplyData(chrData);
                    break;
            }
        }

        List<RuntimePreset> GetFilteredPresets()
        {
            var system = postFXRouter.ActiveSystem;
            if (system == null) return _presetList.presets;

            string type = system.EffectTypeName;
            var filtered = new List<RuntimePreset>();
            foreach (var p in _presetList.presets)
            {
                if (p.effectType == type) filtered.Add(p);
            }
            return filtered;
        }

        void WriteToDisk()
        {
            try
            {
                string json = JsonUtility.ToJson(_presetList, prettyPrint: true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[VJSystem] PresetSaveSystem: Write failed — {e.Message}");
            }
        }

        void LoadFromDisk()
        {
            if (!File.Exists(SavePath)) return;

            try
            {
                string json = File.ReadAllText(SavePath);
                _presetList = JsonUtility.FromJson<RuntimePresetList>(json) ?? new RuntimePresetList();
                Debug.Log($"[VJSystem] PresetSaveSystem: Loaded {_presetList.presets.Count} runtime presets.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VJSystem] PresetSaveSystem: Load failed — {e.Message}");
                _presetList = new RuntimePresetList();
            }
        }
    }
}
