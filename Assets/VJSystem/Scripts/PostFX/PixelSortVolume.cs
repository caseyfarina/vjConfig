using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VJSystem
{
    [VolumeComponentMenu("Post-processing/Pixel Sort")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class PixelSortVolume : VolumeComponent, IPostProcessComponent
    {
        // =====================================================================
        // Core
        // =====================================================================

        [Header("Pixel Sort")]
        [Tooltip("Overall effect strength. 0 = no sorting, 1 = fully sorted.")]
        public ClampedFloatParameter strength = new(0f, 0f, 1f);

        [Tooltip("Sort direction along the image.")]
        public SortAxisParameter sortAxis = new(SortAxis.Horizontal);

        // =====================================================================
        // Threshold — defines which pixels participate in sorting
        // =====================================================================

        [Header("Threshold")]
        [Tooltip("Property used to determine if a pixel is sortable.")]
        public PixelPropertyParameter thresholdMode = new(PixelProperty.Luminance);

        [Tooltip("Lower threshold. Pixels below this value are not sorted (they act as span boundaries).")]
        public ClampedFloatParameter thresholdLow = new(0.1f, 0f, 1f);

        [Tooltip("Upper threshold. Pixels above this value are not sorted.")]
        public ClampedFloatParameter thresholdHigh = new(0.9f, 0f, 1f);

        // =====================================================================
        // Sort Criteria — what property determines pixel ordering within spans
        // =====================================================================

        [Header("Sort Criteria")]
        [Tooltip("Property used to order pixels within sortable spans.")]
        public PixelPropertyParameter sortMode = new(PixelProperty.Luminance);

        [Tooltip("Sort order within spans.")]
        public SortOrderParameter sortOrder = new(SortOrder.Ascending);

        // =====================================================================
        // Span Control
        // =====================================================================

        [Header("Span Control")]
        [Tooltip("Maximum length of a sort span in pixels. 0 = unlimited. " +
                 "Lower values create shorter sorted streaks for a more controlled look.")]
        public ClampedIntParameter maxSpanLength = new(0, 0, 1920);

        // =====================================================================
        // IPostProcessComponent
        // =====================================================================

        public bool IsActive() => strength.value > 0f && active;

        [System.Obsolete]
        public bool IsTileCompatible() => false;
    }

    // =========================================================================
    // Enums & Parameter Types
    // =========================================================================

    public enum SortAxis
    {
        Horizontal = 0,
        Vertical = 1,
        Both = 2
    }

    public enum PixelProperty
    {
        Luminance = 0,
        Hue = 1,
        Saturation = 2,
        Brightness = 3
    }

    public enum SortOrder
    {
        Ascending = 0,  // Dark to light
        Descending = 1  // Light to dark
    }

    [System.Serializable]
    public sealed class SortAxisParameter : VolumeParameter<SortAxis>
    {
        public SortAxisParameter(SortAxis value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    [System.Serializable]
    public sealed class PixelPropertyParameter : VolumeParameter<PixelProperty>
    {
        public PixelPropertyParameter(PixelProperty value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    [System.Serializable]
    public sealed class SortOrderParameter : VolumeParameter<SortOrder>
    {
        public SortOrderParameter(SortOrder value, bool overrideState = false)
            : base(value, overrideState) { }
    }
}
