using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;

namespace VJSystem
{
    /// <summary>
    /// Controls the ChromaticDisplacementVolume override via the Volume bridge pattern.
    /// Tweens displacement parameters; enums, bools, colors are set instantly.
    /// </summary>
    public class ChromaticDisplacementSystem : MonoBehaviour, IPostFXSystem
    {
        [SerializeField] Volume globalVolume;
        [SerializeField] ChromaticPresetLibrary presetLibrary;
        [SerializeField] float tweenDuration = 0.25f;

        ChromaticDisplacementVolume _volume;
        int _activePreset = -1;

        const string TWEEN_ID = "ChromaticTween";

        public string EffectTypeName => "Chromatic";
        public int ActivePresetIndex => _activePreset;

        public string ActivePresetName =>
            _activePreset >= 0 && _activePreset < presetLibrary.presets.Length
                ? presetLibrary.presets[_activePreset]?.presetName ?? "None"
                : "None";

        void Awake()
        {
            if (globalVolume == null || !globalVolume.profile.TryGet(out _volume))
            {
                Debug.LogError("[VJSystem] ChromaticDisplacementSystem: No ChromaticDisplacementVolume override found on Volume.");
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

        public void ApplyData(ChromaticDisplacementPresetData preset)
        {
            DOTween.Kill(TWEEN_ID);

            if (!preset.enabled)
            {
                // Tween displacementAmount to 0 — Volume's IsActive() will return false
                TweenFloat(_volume.displacementAmount, 0f);
                return;
            }

            // Instant-set enums, bools, colors, LayerMask, Vector2
            SetOverride(_volume.displacementSource, preset.displacementSource);
            SetOverride(_volume.colorMode, preset.colorMode);
            SetOverride(_volume.channelBlendMode, preset.channelBlendMode);
            SetOverride(_volume.useObjectMask, preset.useObjectMask);
            SetOverride(_volume.maskLayer, preset.maskLayer);
            SetOverride(_volume.useRadialFalloff, preset.useRadialFalloff);
            SetOverride(_volume.center, preset.center);
            SetOverride(_volume.colorA, preset.colorA);
            SetOverride(_volume.colorB, preset.colorB);
            SetOverride(_volume.colorC, preset.colorC);

            // Instant-set channel amounts and angles (these change character, not intensity)
            SetOverride(_volume.channelAAmount, preset.channelAAmount);
            SetOverride(_volume.channelAAngle, preset.channelAAngle);
            SetOverride(_volume.channelBAmount, preset.channelBAmount);
            SetOverride(_volume.channelBAngle, preset.channelBAngle);
            SetOverride(_volume.channelCAmount, preset.channelCAmount);
            SetOverride(_volume.channelCAngle, preset.channelCAngle);

            // Tween smooth parameters
            TweenFloat(_volume.displacementAmount, preset.displacementAmount);
            TweenFloat(_volume.displacementScale, preset.displacementScale);
            TweenFloat(_volume.blurRadius, preset.blurRadius);
            TweenFloat(_volume.depthInfluence, preset.depthInfluence);
            TweenFloat(_volume.maskDilation, preset.maskDilation);
            TweenFloat(_volume.maskFeather, preset.maskFeather);
            TweenFloat(_volume.falloffStart, preset.falloffStart);
            TweenFloat(_volume.falloffEnd, preset.falloffEnd);
            TweenFloat(_volume.falloffPower, preset.falloffPower);
        }

        public void Randomize()
        {
            if (presetLibrary == null) return;
            var b = presetLibrary.randomBounds;

            var randomPreset = new ChromaticDisplacementPresetData
            {
                presetName         = "Random",
                enabled            = true,
                displacementAmount = Random.Range(b.displacementAmount.x, b.displacementAmount.y),
                displacementSource = DisplacementSource.Luminance,
                displacementScale  = Random.Range(b.displacementScale.x,  b.displacementScale.y),
                depthInfluence     = Random.Range(b.depthInfluence.x,     b.depthInfluence.y),
                blurRadius         = Random.Range(b.blurRadius.x,         b.blurRadius.y),
                channelAAmount     = Random.Range(b.channelAmount.x,      b.channelAmount.y),
                channelBAmount     = Random.Range(b.channelAmount.x,      b.channelAmount.y),
                channelCAmount     = Random.Range(b.channelAmount.x,      b.channelAmount.y),
                channelAAngle      = Random.Range(b.channelAngle.x,       b.channelAngle.y),
                channelBAngle      = Random.Range(b.channelAngle.x,       b.channelAngle.y),
                channelCAngle      = Random.Range(b.channelAngle.x,       b.channelAngle.y),
                colorMode          = ColorMode.RGB,
                colorA             = Color.red,
                colorB             = Color.green,
                colorC             = Color.blue,
                channelBlendMode   = Random.value > 0.7f ? ChannelBlendMode.Screen : ChannelBlendMode.Additive,
                useObjectMask      = false,
                useRadialFalloff   = Random.value > 0.5f,
                center             = new Vector2(0.5f, 0.5f),
                falloffStart       = Random.Range(b.radialFalloffStart.x, b.radialFalloffStart.y),
                falloffEnd         = Random.Range(b.radialFalloffEnd.x,   b.radialFalloffEnd.y),
                falloffPower       = Random.Range(0.5f, 3f)
            };

            _activePreset = -1;
            ApplyData(randomPreset);
        }

        public string CaptureCurrentState(string name)
        {
            var data = new ChromaticDisplacementPresetData
            {
                presetName         = name,
                enabled            = _volume.displacementAmount.value > 0f,
                displacementAmount = _volume.displacementAmount.value,
                displacementSource = _volume.displacementSource.value,
                displacementScale  = _volume.displacementScale.value,
                depthInfluence     = _volume.depthInfluence.value,
                blurRadius         = _volume.blurRadius.value,
                channelAAmount     = _volume.channelAAmount.value,
                channelAAngle      = _volume.channelAAngle.value,
                channelBAmount     = _volume.channelBAmount.value,
                channelBAngle      = _volume.channelBAngle.value,
                channelCAmount     = _volume.channelCAmount.value,
                channelCAngle      = _volume.channelCAngle.value,
                colorMode          = _volume.colorMode.value,
                colorA             = _volume.colorA.value,
                colorB             = _volume.colorB.value,
                colorC             = _volume.colorC.value,
                channelBlendMode   = _volume.channelBlendMode.value,
                useObjectMask      = _volume.useObjectMask.value,
                maskLayer          = _volume.maskLayer.value,
                maskDilation       = _volume.maskDilation.value,
                maskFeather        = _volume.maskFeather.value,
                useRadialFalloff   = _volume.useRadialFalloff.value,
                center             = _volume.center.value,
                falloffStart       = _volume.falloffStart.value,
                falloffEnd         = _volume.falloffEnd.value,
                falloffPower       = _volume.falloffPower.value
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
