using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace VJSystem
{
    /// <summary>
    /// Render pass that executes GPU pixel sorting via compute shader.
    /// Uses Unity 6 RenderGraph AddComputePass API.
    ///
    /// Processing:
    ///   1. Copy active color → UAV-enabled temp texture
    ///   2. Dispatch compute shader (horizontal and/or vertical)
    ///   3. Copy result back to active color
    /// </summary>
    public class PixelSortPass : ScriptableRenderPass
    {
        private ComputeShader m_ComputeShader;

        // Kernel indices
        private int m_KernelH;
        private int m_KernelV;

        // Cached property IDs
        private static readonly int s_SourceTexture   = Shader.PropertyToID("_SourceTexture");
        private static readonly int s_OutputTexture   = Shader.PropertyToID("_OutputTexture");
        private static readonly int s_ThresholdLow    = Shader.PropertyToID("_ThresholdLow");
        private static readonly int s_ThresholdHigh   = Shader.PropertyToID("_ThresholdHigh");
        private static readonly int s_SortMode        = Shader.PropertyToID("_SortMode");
        private static readonly int s_ThresholdMode   = Shader.PropertyToID("_ThresholdMode");
        private static readonly int s_SortDirection   = Shader.PropertyToID("_SortDirection");
        private static readonly int s_Width           = Shader.PropertyToID("_Width");
        private static readonly int s_Height          = Shader.PropertyToID("_Height");
        private static readonly int s_Strength        = Shader.PropertyToID("_Strength");
        private static readonly int s_MaxSpanLength   = Shader.PropertyToID("_MaxSpanLength");

        public PixelSortPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public bool Setup(ComputeShader computeShader)
        {
            if (computeShader == null)
                return false;

            m_ComputeShader = computeShader;
            m_KernelH = m_ComputeShader.FindKernel("PixelSortH");
            m_KernelV = m_ComputeShader.FindKernel("PixelSortV");

            var stack = VolumeManager.instance.stack;
            var volume = stack.GetComponent<PixelSortVolume>();
            return volume != null && volume.IsActive();
        }

        // =====================================================================
        // Pass Data
        // =====================================================================

        private class PixelSortPassData
        {
            public TextureHandle source;
            public TextureHandle output;
            public ComputeShader computeShader;
            public int kernelIndex;
            public int width;
            public int height;
            public int dispatchCount;

            // Parameters
            public float thresholdLow;
            public float thresholdHigh;
            public int sortMode;
            public int thresholdMode;
            public int sortDirection;
            public float strength;
            public int maxSpanLength;
        }

        private class CopyPassData
        {
            public TextureHandle source;
        }

        // =====================================================================
        // RenderGraph
        // =====================================================================

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var stack = VolumeManager.instance.stack;
            var volume = stack.GetComponent<PixelSortVolume>();

            if (volume == null || !volume.IsActive() || m_ComputeShader == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc sourceDesc = renderGraph.GetTextureDesc(source);

            int width = sourceDesc.width;
            int height = sourceDesc.height;

            // Create UAV-enabled texture for compute output
            var outputDesc = sourceDesc;
            outputDesc.name = "_PixelSortOutput";
            outputDesc.clearBuffer = false;
            outputDesc.enableRandomWrite = true;

            bool doHorizontal = volume.sortAxis.value == SortAxis.Horizontal
                             || volume.sortAxis.value == SortAxis.Both;
            bool doVertical   = volume.sortAxis.value == SortAxis.Vertical
                             || volume.sortAxis.value == SortAxis.Both;

            TextureHandle currentSource = source;

            // --- Horizontal sort pass ---
            if (doHorizontal)
            {
                TextureHandle hOutput = renderGraph.CreateTexture(outputDesc);

                using (var builder = renderGraph.AddComputePass<PixelSortPassData>(
                    "PixelSort_Horizontal", out var passData))
                {
                    passData.source = currentSource;
                    passData.output = hOutput;
                    passData.computeShader = m_ComputeShader;
                    passData.kernelIndex = m_KernelH;
                    passData.width = width;
                    passData.height = height;
                    passData.dispatchCount = height; // One thread group per row
                    passData.thresholdLow = volume.thresholdLow.value;
                    passData.thresholdHigh = volume.thresholdHigh.value;
                    passData.sortMode = (int)volume.sortMode.value;
                    passData.thresholdMode = (int)volume.thresholdMode.value;
                    passData.sortDirection = (int)volume.sortOrder.value;
                    passData.strength = volume.strength.value;
                    passData.maxSpanLength = volume.maxSpanLength.value;

                    builder.UseTexture(currentSource, AccessFlags.Read);
                    builder.UseTexture(hOutput, AccessFlags.Write);

                    builder.SetRenderFunc(static (PixelSortPassData data, ComputeGraphContext context) =>
                    {
                        SetComputeParams(data, context);
                        context.cmd.DispatchCompute(data.computeShader, data.kernelIndex,
                            data.dispatchCount, 1, 1);
                    });
                }

                currentSource = hOutput;
            }

            // --- Vertical sort pass ---
            if (doVertical)
            {
                outputDesc.name = "_PixelSortOutputV";
                TextureHandle vOutput = renderGraph.CreateTexture(outputDesc);

                using (var builder = renderGraph.AddComputePass<PixelSortPassData>(
                    "PixelSort_Vertical", out var passData))
                {
                    passData.source = currentSource;
                    passData.output = vOutput;
                    passData.computeShader = m_ComputeShader;
                    passData.kernelIndex = m_KernelV;
                    passData.width = width;
                    passData.height = height;
                    passData.dispatchCount = width; // One thread group per column
                    passData.thresholdLow = volume.thresholdLow.value;
                    passData.thresholdHigh = volume.thresholdHigh.value;
                    passData.sortMode = (int)volume.sortMode.value;
                    passData.thresholdMode = (int)volume.thresholdMode.value;
                    passData.sortDirection = (int)volume.sortOrder.value;
                    passData.strength = volume.strength.value;
                    passData.maxSpanLength = volume.maxSpanLength.value;

                    builder.UseTexture(currentSource, AccessFlags.Read);
                    builder.UseTexture(vOutput, AccessFlags.Write);

                    builder.SetRenderFunc(static (PixelSortPassData data, ComputeGraphContext context) =>
                    {
                        SetComputeParams(data, context);
                        context.cmd.DispatchCompute(data.computeShader, data.kernelIndex,
                            data.dispatchCount, 1, 1);
                    });
                }

                currentSource = vOutput;
            }

            // --- Copy result back to active color texture ---
            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>(
                "PixelSort_CopyBack", out var copyData))
            {
                copyData.source = currentSource;

                builder.UseTexture(currentSource);
                builder.SetRenderAttachment(source, 0);

                builder.SetRenderFunc(static (CopyPassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source,
                        new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        /// <summary>
        /// Bind all compute shader parameters.
        /// </summary>
        private static void SetComputeParams(PixelSortPassData data, ComputeGraphContext context)
        {
            var cmd = context.cmd;
            int kernel = data.kernelIndex;

            cmd.SetComputeTextureParam(data.computeShader, kernel, s_SourceTexture, data.source);
            cmd.SetComputeTextureParam(data.computeShader, kernel, s_OutputTexture, data.output);
            cmd.SetComputeFloatParam(data.computeShader, s_ThresholdLow, data.thresholdLow);
            cmd.SetComputeFloatParam(data.computeShader, s_ThresholdHigh, data.thresholdHigh);
            cmd.SetComputeIntParam(data.computeShader, s_SortMode, data.sortMode);
            cmd.SetComputeIntParam(data.computeShader, s_ThresholdMode, data.thresholdMode);
            cmd.SetComputeIntParam(data.computeShader, s_SortDirection, data.sortDirection);
            cmd.SetComputeIntParam(data.computeShader, s_Width, data.width);
            cmd.SetComputeIntParam(data.computeShader, s_Height, data.height);
            cmd.SetComputeFloatParam(data.computeShader, s_Strength, data.strength);
            cmd.SetComputeIntParam(data.computeShader, s_MaxSpanLength, data.maxSpanLength);
        }

        public void Dispose()
        {
            // ComputeShader is an asset, not created by us — don't destroy it
        }
    }
}
