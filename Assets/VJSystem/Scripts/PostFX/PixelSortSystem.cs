using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;

namespace VJSystem
{
    /// <summary>
    /// Controls the PixelSortVolume override via the Volume bridge pattern.
    /// Applies 7 named presets and supports randomization within configurable bounds.
    /// </summary>
    public class PixelSortSystem : MonoBehaviour, IPostFXSystem
    {
        [SerializeField] Volume globalVolume;
        [SerializeField] PixelSortPresetLibrary presetLibrary;
        [SerializeField] float tweenDuration = 0.25f;

        PixelSortVolume _volume;
        int _activePreset = -1;

        const string TWEEN_ID = "PixelSortTween";

        public string EffectTypeName => "PixelSort";
        public int ActivePresetIndex => _activePreset;

        public string ActivePresetName =>
            _activePreset >= 0 && _activePreset < presetLibrary.presets.Length
                ? presetLibrary.presets[_activePreset]?.presetName ?? "None"
                : "None";

        void Awake()
        {
            if (globalVolume == null || !globalVolume.profile.TryGet(out _volume))
            {
                Debug.LogError("[VJSystem] PixelSortSystem: No PixelSortVolume override found on Volume.");
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

        public void ApplyData(PixelSortPresetData preset)
        {
            DOTween.Kill(TWEEN_ID);

            if (!preset.enabled)
            {
                // Tween strength to 0 — Volume's IsActive() will return false
                TweenFloat(_volume.strength, 0f);
                return;
            }

            // Instant-set enums and int
            SetOverride(_volume.sortAxis, preset.sortAxis);
            SetOverride(_volume.thresholdMode, preset.thresholdMode);
            SetOverride(_volume.sortMode, preset.sortMode);
            SetOverride(_volume.sortOrder, preset.sortOrder);
            SetOverride(_volume.maxSpanLength, preset.maxSpanLength);

            // Tween float parameters
            TweenFloat(_volume.strength, preset.strength);
            TweenFloat(_volume.thresholdLow, preset.thresholdLow);
            TweenFloat(_volume.thresholdHigh, preset.thresholdHigh);
        }

        public void Randomize()
        {
            if (presetLibrary == null) return;
            var b = presetLibrary.randomBounds;

            var randomPreset = new PixelSortPresetData
            {
                presetName    = "Random",
                enabled       = true,
                strength      = Random.Range(b.strength.x, b.strength.y),
                sortAxis      = (SortAxis)Random.Range(0, 3),
                thresholdMode = (PixelProperty)Random.Range(0, 4),
                thresholdLow  = Random.Range(b.thresholdLow.x, b.thresholdLow.y),
                thresholdHigh = Random.Range(b.thresholdHigh.x, b.thresholdHigh.y),
                sortMode      = (PixelProperty)Random.Range(0, 4),
                sortOrder     = (SortOrder)Random.Range(0, 2),
                maxSpanLength = Random.Range(b.maxSpanLength.x, b.maxSpanLength.y)
            };

            _activePreset = -1;
            ApplyData(randomPreset);
        }

        public string CaptureCurrentState(string name)
        {
            var data = new PixelSortPresetData
            {
                presetName    = name,
                enabled       = _volume.strength.value > 0f,
                strength      = _volume.strength.value,
                sortAxis      = _volume.sortAxis.value,
                thresholdMode = _volume.thresholdMode.value,
                thresholdLow  = _volume.thresholdLow.value,
                thresholdHigh = _volume.thresholdHigh.value,
                sortMode      = _volume.sortMode.value,
                sortOrder     = _volume.sortOrder.value,
                maxSpanLength = _volume.maxSpanLength.value
            };
            return JsonUtility.ToJson(data);
        }

        void TweenFloat(ClampedFloatParameter param, float target)
        {
            param.overrideState = true;
            DOTween.To(() => param.value,
                       x  => param.value = x,
                       target, tweenDuration).SetId(TWEEN_ID);
        }

        void SetOverride<T>(VolumeParameter<T> param, T value)
        {
            param.value = value;
            param.overrideState = true;
        }
    }
}
