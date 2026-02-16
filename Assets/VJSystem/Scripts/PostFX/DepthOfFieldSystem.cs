using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

namespace VJSystem
{
    /// <summary>
    /// Controls URP DepthOfField volume override. Applies 7 named presets
    /// and supports randomization within configurable bounds.
    /// </summary>
    public class DepthOfFieldSystem : MonoBehaviour, IPostFXSystem
    {
        [SerializeField] Volume globalVolume;
        [SerializeField] DoFPresetLibrary presetLibrary;
        [SerializeField] float tweenDuration = 0.3f;

        DepthOfField _dof;
        int _activePreset = -1;

        const string TWEEN_ID = "DoFTween";

        public string EffectTypeName => "DoF";
        public int ActivePresetIndex => _activePreset;

        public string ActivePresetName =>
            _activePreset >= 0 && _activePreset < presetLibrary.presets.Length
                ? presetLibrary.presets[_activePreset]?.presetName ?? "None"
                : "None";

        void Awake()
        {
            if (globalVolume == null || !globalVolume.profile.TryGet(out _dof))
            {
                Debug.LogError("[VJSystem] DepthOfFieldSystem: No DepthOfField override found on Volume.");
                enabled = false;
            }
        }

        public void ApplyPreset(int slotIndex)
        {
            if (presetLibrary == null || slotIndex < 0 || slotIndex >= presetLibrary.presets.Length)
                return;

            var preset = presetLibrary.presets[slotIndex];
            if (preset == null) return;

            _activePreset = slotIndex;
            ApplyData(preset);
        }

        public void ApplyData(DoFPresetData preset)
        {
            DOTween.Kill(TWEEN_ID);

            _dof.mode.value = preset.mode;
            _dof.mode.overrideState = true;

            DOTween.To(() => _dof.focusDistance.value,
                       x  => { _dof.focusDistance.value = x; _dof.focusDistance.overrideState = true; },
                       preset.focusDistance, tweenDuration).SetId(TWEEN_ID);

            DOTween.To(() => _dof.focalLength.value,
                       x  => { _dof.focalLength.value = x; _dof.focalLength.overrideState = true; },
                       preset.focalLength, tweenDuration).SetId(TWEEN_ID);

            DOTween.To(() => _dof.aperture.value,
                       x  => { _dof.aperture.value = x; _dof.aperture.overrideState = true; },
                       preset.aperture, tweenDuration).SetId(TWEEN_ID);

            DOTween.To(() => _dof.gaussianStart.value,
                       x  => { _dof.gaussianStart.value = x; _dof.gaussianStart.overrideState = true; },
                       preset.gaussianStart, tweenDuration).SetId(TWEEN_ID);

            DOTween.To(() => _dof.gaussianEnd.value,
                       x  => { _dof.gaussianEnd.value = x; _dof.gaussianEnd.overrideState = true; },
                       preset.gaussianEnd, tweenDuration).SetId(TWEEN_ID);
        }

        public void Randomize()
        {
            if (presetLibrary == null) return;
            var b = presetLibrary.randomBounds;

            var randomPreset = new DoFPresetData
            {
                presetName    = "Random",
                mode          = Random.value > 0.5f ? DepthOfFieldMode.Bokeh : DepthOfFieldMode.Gaussian,
                focusDistance  = Random.Range(b.focusDistance.x,  b.focusDistance.y),
                focalLength   = Random.Range(b.focalLength.x,   b.focalLength.y),
                aperture      = Random.Range(b.aperture.x,      b.aperture.y),
                gaussianStart = Random.Range(b.gaussianStart.x, b.gaussianStart.y),
                gaussianEnd   = Random.Range(b.gaussianEnd.x,   b.gaussianEnd.y)
            };

            _activePreset = -1;
            ApplyData(randomPreset);
        }

        public string CaptureCurrentState(string name)
        {
            var data = new DoFPresetData
            {
                presetName    = name,
                mode          = _dof.mode.value,
                focusDistance  = _dof.focusDistance.value,
                focalLength   = _dof.focalLength.value,
                aperture      = _dof.aperture.value,
                gaussianStart = _dof.gaussianStart.value,
                gaussianEnd   = _dof.gaussianEnd.value
            };
            return JsonUtility.ToJson(data);
        }
    }
}
