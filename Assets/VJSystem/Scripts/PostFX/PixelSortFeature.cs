using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace VJSystem
{
    /// <summary>
    /// Renderer Feature for GPU Pixel Sorting.
    ///
    /// Setup:
    /// 1. Add this Renderer Feature to your URP Renderer Asset
    /// 2. Assign the PixelSort compute shader in the inspector
    /// 3. Add a Volume with the "Pixel Sort" override
    /// 4. Increase Strength to see the effect
    ///
    /// Ordering:
    /// - Place above Chromatic Displacement in the feature list to sort before displacement
    /// - Place below to sort after displacement
    /// </summary>
    [DisallowMultipleRendererFeature("Pixel Sort")]
    [Tooltip("GPU pixel sorting effect. Sorts pixels along rows/columns within " +
             "threshold-defined spans based on luminance, hue, saturation, or brightness.")]
    public class PixelSortFeature : ScriptableRendererFeature
    {
        [Header("Compute Shader")]
        [Tooltip("Reference to the PixelSort compute shader asset.")]
        [SerializeField]
        private ComputeShader m_ComputeShader;

        [Header("Injection Settings")]
        [Tooltip("When in the render pipeline this effect should execute.")]
        [SerializeField]
        private RenderPassEvent m_RenderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        private PixelSortPass m_Pass;

        public override void Create()
        {
            m_Pass = new PixelSortPass
            {
                renderPassEvent = m_RenderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview)
                return;

            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogWarning("[PixelSort] Compute shaders not supported on this platform.");
                return;
            }

            if (m_ComputeShader == null)
            {
                Debug.LogWarning("[PixelSort] Compute shader not assigned in the Pixel Sort renderer feature.");
                return;
            }

            if (m_Pass.Setup(m_ComputeShader))
            {
                renderer.EnqueuePass(m_Pass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
        }
    }
}
