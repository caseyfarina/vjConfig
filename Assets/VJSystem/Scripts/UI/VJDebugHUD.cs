using UnityEngine;
using TMPro;
using DG.Tweening;

namespace VJSystem
{
    /// <summary>
    /// Debug HUD overlay showing system state. Screen Space Overlay canvas so it
    /// doesn't affect the Spout output RenderTexture. Toggle with Tab.
    /// </summary>
    public class VJDebugHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] VJCameraSystem cameraSystem;
        [SerializeField] DepthOfFieldSystem dofSystem;
        [SerializeField] PixelSortSystem pixelSortSystem;
        [SerializeField] ChromaticDisplacementSystem chromaticSystem;
        [SerializeField] VJLightSystem lightSystem;
        [SerializeField] VJSceneSlotSystem sceneSlotSystem;
        [SerializeField] MidiEventManager midiEventManager;
        [SerializeField] PresetSaveSystem presetSaveSystem;

        [Header("UI Elements")]
        [SerializeField] Canvas canvas;
        [SerializeField] TextMeshProUGUI cameraLabel;
        [SerializeField] TextMeshProUGUI fpsLabel;
        [SerializeField] TextMeshProUGUI dofLabel;
        [SerializeField] TextMeshProUGUI pixelSortLabel;
        [SerializeField] TextMeshProUGUI chromaticLabel;
        [SerializeField] TextMeshProUGUI lightsLabel;
        [SerializeField] TextMeshProUGUI slotsLabel;
        [SerializeField] TextMeshProUGUI midiLabel;
        [SerializeField] TextMeshProUGUI savedLabel;
        [SerializeField] TextMeshProUGUI recallLabel;

        bool _visible = true;
        float _fpsTimer;
        int _fpsFrameCount;
        int _currentFps;

        void OnEnable()
        {
            PresetSaveSystem.OnPresetSaved += HandlePresetSaved;
        }

        void OnDisable()
        {
            PresetSaveSystem.OnPresetSaved -= HandlePresetSaved;
        }

        void Update()
        {
            // Toggle visibility
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _visible = !_visible;
                if (canvas != null)
                    canvas.enabled = _visible;
            }

            if (!_visible) return;

            UpdateFPS();
            UpdateLabels();
        }

        void UpdateFPS()
        {
            _fpsFrameCount++;
            _fpsTimer += Time.unscaledDeltaTime;

            if (_fpsTimer >= 0.5f)
            {
                _currentFps = Mathf.RoundToInt(_fpsFrameCount / _fpsTimer);
                _fpsFrameCount = 0;
                _fpsTimer = 0f;
            }
        }

        void UpdateLabels()
        {
            if (cameraLabel != null && cameraSystem != null)
                cameraLabel.text = $"CAM: {cameraSystem.ActiveCameraName}";

            if (fpsLabel != null)
                fpsLabel.text = $"FPS: {_currentFps}";

            if (dofLabel != null && dofSystem != null)
                dofLabel.text = $"DoF: {dofSystem.ActivePresetName}";

            if (pixelSortLabel != null && pixelSortSystem != null)
                pixelSortLabel.text = $"PxS: {pixelSortSystem.ActivePresetName}";

            if (chromaticLabel != null && chromaticSystem != null)
                chromaticLabel.text = $"Chr: {chromaticSystem.ActivePresetName}";

            if (lightsLabel != null && lightSystem != null)
                lightsLabel.text = $"LIGHTS  {FormatBoolArray(lightSystem.States, 8)}";

            if (slotsLabel != null && sceneSlotSystem != null)
                slotsLabel.text = $"SLOTS   {FormatBoolArray(sceneSlotSystem.States, 24)}";

            if (midiLabel != null && midiEventManager != null)
                midiLabel.text = $"MIDI: {midiEventManager.DeviceName}";

            if (savedLabel != null && presetSaveSystem != null)
                savedLabel.text = $"SAVED: {presetSaveSystem.PresetCount} runtime presets loaded";

            if (recallLabel != null && presetSaveSystem != null)
            {
                if (presetSaveSystem.RecallMode)
                {
                    var preset = presetSaveSystem.GetPresetAtRecallIndex();
                    string name = preset != null ? preset.name : "—";
                    recallLabel.text = $"RECALL [{presetSaveSystem.RecallIndex}]: {name}";
                    recallLabel.gameObject.SetActive(true);
                }
                else
                {
                    recallLabel.gameObject.SetActive(false);
                }
            }
        }

        static string FormatBoolArray(bool[] states, int count)
        {
            var sb = new System.Text.StringBuilder(count * 3);
            for (int i = 0; i < count && i < states.Length; i++)
            {
                sb.Append(states[i] ? "[■]" : "[□]");
            }
            return sb.ToString();
        }

        void HandlePresetSaved(string presetName)
        {
            if (savedLabel == null) return;

            // Flash yellow for 1.5s
            DOTween.Kill(savedLabel);
            savedLabel.color = Color.yellow;
            savedLabel.text = $"SAVED: {presetName}";
            savedLabel.DOColor(Color.white, 1.5f).SetDelay(0.1f);
        }
    }
}
