using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace VJSystem
{
    /// <summary>
    /// Renderer Feature that adds the Chromatic Displacement post-processing pass to URP.
    ///
    /// Setup:
    /// 1. Add this Renderer Feature to your URP Renderer Asset
    /// 2. Add a Volume with the "Chromatic Displacement" override
    /// 3. Increase Displacement Amount to see the effect
    /// 4. (Optional) Enable Object Mask and assign a layer to restrict the effect
    /// 5. (Optional) Switch Color Mode to Custom Palette for artistic color control
    /// </summary>
    [DisallowMultipleRendererFeature("Chromatic Displacement")]
    [Tooltip("Chromatic Displacement effect that separates color channels based on " +
             "scene luminance, depth, or an external map. Supports selective object masking " +
             "and custom color palettes.")]
    public class ChromaticDisplacementFeature : ScriptableRendererFeature
    {
        [Header("Injection Settings")]
        [Tooltip("When in the render pipeline this effect should execute.")]
        [SerializeField]
        private RenderPassEvent m_RenderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        private ChromaticDisplacementPass m_Pass;

        public override void Create()
        {
            m_Pass = new ChromaticDisplacementPass
            {
                renderPassEvent = m_RenderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview)
                return;

            if (m_Pass.Setup())
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
