using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VJSystem
{
    /// <summary>
    /// Volume component for Chromatic Displacement post-processing effect.
    /// </summary>
    [VolumeComponentMenu("Post-processing/Chromatic Displacement")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class ChromaticDisplacementVolume : VolumeComponent, IPostProcessComponent
    {
        // =====================================================================
        // Displacement
        // =====================================================================

        [Header("Displacement")]
        [Tooltip("Master displacement strength. Controls overall intensity of channel separation.")]
        public ClampedFloatParameter displacementAmount = new(0f, 0f, 0.1f);

        [Tooltip("Source for computing displacement gradients.")]
        public DisplacementSourceParameter displacementSource = new(DisplacementSource.Luminance);

        [Tooltip("Scale of the Sobel gradient sampling kernel. Higher = broader displacement response.")]
        public ClampedFloatParameter displacementScale = new(1f, 0.1f, 10f);

        [Tooltip("External displacement map (grayscale). Used when Source = External Map.")]
        public TextureParameter displacementMap = new(null);

        [Tooltip("Depth influence multiplier when using Depth source.")]
        public ClampedFloatParameter depthInfluence = new(1f, 0f, 5f);

        // =====================================================================
        // Pre-Blur
        // =====================================================================

        [Header("Pre-Blur")]
        [Tooltip("Gaussian blur radius on displacement source before gradient computation. " +
                 "0 = sharp detail. 2 = subtle. 8+ = broad flowing displacement.")]
        public ClampedFloatParameter blurRadius = new(2f, 0f, 16f);

        // =====================================================================
        // Channel Controls (A, B, C — maps to R, G, B in default mode)
        // =====================================================================

        [Header("Channel A (Red in default mode)")]
        [Tooltip("Channel A displacement multiplier.")]
        public ClampedFloatParameter channelAAmount = new(1f, -3f, 3f);

        [Tooltip("Channel A displacement angle offset in degrees.")]
        public ClampedFloatParameter channelAAngle = new(0f, -180f, 180f);

        [Header("Channel B (Green in default mode)")]
        [Tooltip("Channel B displacement multiplier.")]
        public ClampedFloatParameter channelBAmount = new(0f, -3f, 3f);

        [Tooltip("Channel B displacement angle offset in degrees.")]
        public ClampedFloatParameter channelBAngle = new(120f, -180f, 180f);

        [Header("Channel C (Blue in default mode)")]
        [Tooltip("Channel C displacement multiplier.")]
        public ClampedFloatParameter channelCAmount = new(-1f, -3f, 3f);

        [Tooltip("Channel C displacement angle offset in degrees.")]
        public ClampedFloatParameter channelCAngle = new(240f, -180f, 180f);

        // =====================================================================
        // Custom Palette
        // =====================================================================

        [Header("Color Palette")]
        [Tooltip("Color mode. RGB = standard R/G/B channel split. Custom = user-defined palette colors.")]
        public ColorModeParameter colorMode = new(ColorMode.RGB);

        [Tooltip("Color for Channel A. Alpha controls intensity.")]
        public ColorParameter colorA = new(new Color(1f, 0f, 0f, 1f), true, true, true);

        [Tooltip("Color for Channel B. Alpha controls intensity.")]
        public ColorParameter colorB = new(new Color(0f, 1f, 0f, 1f), true, true, true);

        [Tooltip("Color for Channel C. Alpha controls intensity.")]
        public ColorParameter colorC = new(new Color(0f, 0f, 1f, 1f), true, true, true);

        [Tooltip("How the three palette channels are combined. Additive can clip to white. Screen prevents clipping.")]
        public ChannelBlendModeParameter channelBlendMode = new(ChannelBlendMode.Additive);

        // =====================================================================
        // Object Mask
        // =====================================================================

        [Header("Object Mask")]
        [Tooltip("Enable to restrict the effect to objects on a specific layer. " +
                 "The mask is automatically dilated so the effect bleeds past object silhouettes.")]
        public BoolParameter useObjectMask = new(false);

        [Tooltip("Layer mask for objects to include in the effect.")]
        public LayerMaskParameter maskLayer = new(0);

        [Tooltip("Dilation radius in pixels. Expands the mask beyond object edges so displacement " +
                 "can bleed outward. Should be at least as large as your maximum displacement.")]
        public ClampedFloatParameter maskDilation = new(8f, 0f, 32f);

        [Tooltip("Feather/softness of the dilated mask edges. Higher = softer falloff.")]
        public ClampedFloatParameter maskFeather = new(4f, 0f, 16f);

        // =====================================================================
        // Radial Falloff
        // =====================================================================

        [Header("Radial Falloff")]
        [Tooltip("Limit the effect based on distance from a center point.")]
        public BoolParameter useRadialFalloff = new(false);

        [Tooltip("Center point in UV space (0-1).")]
        public Vector2Parameter center = new(new Vector2(0.5f, 0.5f));

        [Tooltip("Inner radius where falloff begins.")]
        public ClampedFloatParameter falloffStart = new(0.0f, 0f, 2f);

        [Tooltip("Outer radius where effect reaches full strength.")]
        public ClampedFloatParameter falloffEnd = new(0.7f, 0f, 2f);

        [Tooltip("Falloff curve power. 1 = linear, <1 = fast start, >1 = slow start.")]
        public ClampedFloatParameter falloffPower = new(1f, 0.1f, 5f);

        // =====================================================================
        // IPostProcessComponent
        // =====================================================================

        public bool IsActive() => displacementAmount.value > 0f && active;

        [System.Obsolete]
        public bool IsTileCompatible() => false;
    }

    // =========================================================================
    // Custom Enums & Parameter Types
    // =========================================================================

    public enum DisplacementSource
    {
        Luminance = 0,
        Depth = 1,
        ExternalMap = 2
    }

    public enum ColorMode
    {
        RGB = 0,
        CustomPalette = 1
    }

    public enum ChannelBlendMode
    {
        Additive = 0,
        Screen = 1
    }

    [System.Serializable]
    public sealed class DisplacementSourceParameter : VolumeParameter<DisplacementSource>
    {
        public DisplacementSourceParameter(DisplacementSource value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    [System.Serializable]
    public sealed class ColorModeParameter : VolumeParameter<ColorMode>
    {
        public ColorModeParameter(ColorMode value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    [System.Serializable]
    public sealed class ChannelBlendModeParameter : VolumeParameter<ChannelBlendMode>
    {
        public ChannelBlendModeParameter(ChannelBlendMode value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    [System.Serializable]
    public sealed class LayerMaskParameter : VolumeParameter<LayerMask>
    {
        public LayerMaskParameter(LayerMask value, bool overrideState = false)
            : base(value, overrideState) { }
    }
}
