using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace VJSystem
{
    /// <summary>
    /// Render pass for the Chromatic Displacement post-processing effect.
    ///
    /// Full processing chain:
    ///   [If mask enabled]
    ///     → Render masked objects to R8 mask texture
    ///     → Dilate mask H + V (separable max filter)
    ///     → Feather/blur mask H + V (Gaussian)
    ///   [Always]
    ///     → Extract displacement source → R8
    ///     → Blur displacement H + V (iterative Gaussian)
    ///     → Final displacement + palette + mask composite
    /// </summary>
    public class ChromaticDisplacementPass : ScriptableRenderPass
    {
        private const string k_ShaderName     = "Hidden/PostProcess/ChromaticDisplacement";
        private const string k_MaskShaderName = "Hidden/PostProcess/ChromaticDisplacementMask";

        // Pass indices matching shader pass order
        private const int PASS_EXTRACT     = 0;
        private const int PASS_BLUR_H      = 1;
        private const int PASS_BLUR_V      = 2;
        private const int PASS_DILATE_H    = 3;
        private const int PASS_DILATE_V    = 4;
        private const int PASS_MASKBLUR_H  = 5;
        private const int PASS_MASKBLUR_V  = 6;
        private const int PASS_DISPLACE    = 7;

        private Material m_Material;
        private Material m_MaskMaterial;

        // Shader tag IDs for mask rendering (matches URP conventions)
        private static readonly List<ShaderTagId> s_ShaderTagIds = new()
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly")
        };

        // Cached property IDs
        private static readonly int s_DisplacementAmount  = Shader.PropertyToID("_DisplacementAmount");
        private static readonly int s_ChannelAAmount      = Shader.PropertyToID("_ChannelAAmount");
        private static readonly int s_ChannelBAmount      = Shader.PropertyToID("_ChannelBAmount");
        private static readonly int s_ChannelCAmount      = Shader.PropertyToID("_ChannelCAmount");
        private static readonly int s_ChannelAAngle       = Shader.PropertyToID("_ChannelAAngle");
        private static readonly int s_ChannelBAngle       = Shader.PropertyToID("_ChannelBAngle");
        private static readonly int s_ChannelCAngle       = Shader.PropertyToID("_ChannelCAngle");
        private static readonly int s_DisplacementScale   = Shader.PropertyToID("_DisplacementScale");
        private static readonly int s_DepthInfluence      = Shader.PropertyToID("_DepthInfluence");
        private static readonly int s_DisplacementMap     = Shader.PropertyToID("_DisplacementMap");
        private static readonly int s_BlurRadius          = Shader.PropertyToID("_BlurRadius");
        private static readonly int s_BlurredDisplacement = Shader.PropertyToID("_BlurredDisplacement");
        private static readonly int s_ObjectMask          = Shader.PropertyToID("_ObjectMask");
        private static readonly int s_DilateRadius        = Shader.PropertyToID("_DilateRadius");
        private static readonly int s_MaskFeather         = Shader.PropertyToID("_MaskFeather");
        private static readonly int s_Center              = Shader.PropertyToID("_Center");
        private static readonly int s_RadialFalloffStart  = Shader.PropertyToID("_RadialFalloffStart");
        private static readonly int s_RadialFalloffEnd    = Shader.PropertyToID("_RadialFalloffEnd");
        private static readonly int s_RadialFalloffPower  = Shader.PropertyToID("_RadialFalloffPower");
        private static readonly int s_ColorA              = Shader.PropertyToID("_ColorA");
        private static readonly int s_ColorB              = Shader.PropertyToID("_ColorB");
        private static readonly int s_ColorC              = Shader.PropertyToID("_ColorC");

        // Keywords
        private const string k_UseDepthSource = "_USE_DEPTH_SOURCE";
        private const string k_UseExternalMap = "_USE_EXTERNAL_MAP";
        private const string k_RadialFalloff  = "_RADIAL_FALLOFF";
        private const string k_UseMask        = "_USE_MASK";
        private const string k_CustomPalette  = "_CUSTOM_PALETTE";
        private const string k_BlendScreen    = "_BLEND_SCREEN";

        public ChromaticDisplacementPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public bool Setup()
        {
            if (m_Material == null)
            {
                var shader = Shader.Find(k_ShaderName);
                if (shader == null)
                {
                    Debug.LogError($"[ChromaticDisplacement] Shader '{k_ShaderName}' not found.");
                    return false;
                }
                m_Material = CoreUtils.CreateEngineMaterial(shader);
            }

            if (m_MaskMaterial == null)
            {
                var maskShader = Shader.Find(k_MaskShaderName);
                if (maskShader == null)
                {
                    Debug.LogError($"[ChromaticDisplacement] Mask shader '{k_MaskShaderName}' not found.");
                    return false;
                }
                m_MaskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
            }

            var stack = VolumeManager.instance.stack;
            var volume = stack.GetComponent<ChromaticDisplacementVolume>();
            return volume != null && volume.IsActive();
        }

        private void UpdateMaterialProperties(ChromaticDisplacementVolume volume)
        {
            m_Material.SetFloat(s_DisplacementAmount, volume.displacementAmount.value);
            m_Material.SetFloat(s_DisplacementScale, volume.displacementScale.value);
            m_Material.SetFloat(s_BlurRadius, volume.blurRadius.value);

            m_Material.SetFloat(s_ChannelAAmount, volume.channelAAmount.value);
            m_Material.SetFloat(s_ChannelBAmount, volume.channelBAmount.value);
            m_Material.SetFloat(s_ChannelCAmount, volume.channelCAmount.value);

            float aRad = volume.channelAAngle.value * Mathf.Deg2Rad;
            float bRad = volume.channelBAngle.value * Mathf.Deg2Rad;
            float cRad = volume.channelCAngle.value * Mathf.Deg2Rad;

            m_Material.SetVector(s_ChannelAAngle, new Vector2(Mathf.Cos(aRad), Mathf.Sin(aRad)));
            m_Material.SetVector(s_ChannelBAngle, new Vector2(Mathf.Cos(bRad), Mathf.Sin(bRad)));
            m_Material.SetVector(s_ChannelCAngle, new Vector2(Mathf.Cos(cRad), Mathf.Sin(cRad)));

            // Displacement source
            m_Material.DisableKeyword(k_UseDepthSource);
            m_Material.DisableKeyword(k_UseExternalMap);

            switch (volume.displacementSource.value)
            {
                case DisplacementSource.Depth:
                    m_Material.EnableKeyword(k_UseDepthSource);
                    m_Material.SetFloat(s_DepthInfluence, volume.depthInfluence.value);
                    break;
                case DisplacementSource.ExternalMap:
                    if (volume.displacementMap.value != null)
                    {
                        m_Material.EnableKeyword(k_UseExternalMap);
                        m_Material.SetTexture(s_DisplacementMap, volume.displacementMap.value);
                    }
                    break;
            }

            // Palette
            if (volume.colorMode.value == ColorMode.CustomPalette)
            {
                m_Material.EnableKeyword(k_CustomPalette);
                m_Material.SetColor(s_ColorA, volume.colorA.value);
                m_Material.SetColor(s_ColorB, volume.colorB.value);
                m_Material.SetColor(s_ColorC, volume.colorC.value);

                if (volume.channelBlendMode.value == ChannelBlendMode.Screen)
                    m_Material.EnableKeyword(k_BlendScreen);
                else
                    m_Material.DisableKeyword(k_BlendScreen);
            }
            else
            {
                m_Material.DisableKeyword(k_CustomPalette);
                m_Material.DisableKeyword(k_BlendScreen);
            }

            // Object mask
            if (volume.useObjectMask.value)
            {
                m_Material.EnableKeyword(k_UseMask);
                m_Material.SetFloat(s_DilateRadius, volume.maskDilation.value);
                m_Material.SetFloat(s_MaskFeather, volume.maskFeather.value);
            }
            else
            {
                m_Material.DisableKeyword(k_UseMask);
            }

            // Radial falloff
            if (volume.useRadialFalloff.value)
            {
                m_Material.EnableKeyword(k_RadialFalloff);
                m_Material.SetVector(s_Center, volume.center.value);
                m_Material.SetFloat(s_RadialFalloffStart, volume.falloffStart.value);
                m_Material.SetFloat(s_RadialFalloffEnd, volume.falloffEnd.value);
                m_Material.SetFloat(s_RadialFalloffPower, volume.falloffPower.value);
            }
            else
            {
                m_Material.DisableKeyword(k_RadialFalloff);
            }
        }

        private int GetBlurIterations(float blurRadius)
        {
            if (blurRadius <= 0.01f) return 0;
            if (blurRadius <= 3f) return 1;
            if (blurRadius <= 8f) return 2;
            return 3;
        }

        // =====================================================================
        // Pass Data classes
        // =====================================================================

        private class BlitPassData
        {
            public TextureHandle source;
            public Material material;
            public int passIndex;
        }

        private class MaskRenderPassData
        {
            public RendererListHandle rendererListHandle;
        }

        private class FinalPassData
        {
            public TextureHandle source;
            public TextureHandle blurredDisplacement;
            public TextureHandle objectMask;
            public Material material;
            public bool useMask;
        }

        private class CopyPassData
        {
            public TextureHandle source;
        }

        // =====================================================================
        // RenderGraph recording
        // =====================================================================

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var stack = VolumeManager.instance.stack;
            var volume = stack.GetComponent<ChromaticDisplacementVolume>();

            if (volume == null || !volume.IsActive() || m_Material == null)
                return;

            UpdateMaterialProperties(volume);

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc sourceDesc = renderGraph.GetTextureDesc(source);

            // R8 descriptor for single-channel intermediate textures
            var r8Desc = new TextureDesc(sourceDesc.width, sourceDesc.height)
            {
                colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm,
                clearBuffer = false,
                filterMode = FilterMode.Bilinear,
                name = "_ChromDispR8Temp"
            };

            // ==============================================================
            // OBJECT MASK pipeline (conditional)
            // ==============================================================
            bool useMask = volume.useObjectMask.value && volume.maskLayer.value != 0;
            TextureHandle finalMask = TextureHandle.nullHandle;

            if (useMask)
            {
                // --- Render masked objects to R8 ---
                var maskDesc = r8Desc;
                maskDesc.name = "_ObjectMaskRaw";
                maskDesc.clearBuffer = true;
                maskDesc.clearColor = Color.clear;
                TextureHandle rawMask = renderGraph.CreateTexture(maskDesc);

                // Get frame data needed for renderer list creation
                // Unity 6 API: CreateDrawingSettings requires RenderingData, CameraData, AND LightData
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                SortingCriteria sortFlags = cameraData.defaultOpaqueSortFlags;
                DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(
                    s_ShaderTagIds, renderingData, cameraData, lightData, sortFlags);
                drawSettings.overrideMaterial = m_MaskMaterial;
                drawSettings.overrideMaterialPassIndex = 0;

                FilteringSettings filterSettings = new FilteringSettings(
                    RenderQueueRange.opaque, volume.maskLayer.value);

                RendererListParams rendererListParams = new RendererListParams(
                    renderingData.cullResults, drawSettings, filterSettings);

                RendererListHandle rendererListHandle = renderGraph.CreateRendererList(rendererListParams);

                using (var builder = renderGraph.AddRasterRenderPass<MaskRenderPassData>(
                    "ChromDisp_MaskRender", out var passData))
                {
                    passData.rendererListHandle = rendererListHandle;

                    builder.UseRendererList(rendererListHandle);
                    builder.SetRenderAttachment(rawMask, 0);

                    // Attach scene depth for proper occlusion
                    if (resourceData.activeDepthTexture.IsValid())
                        builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

                    builder.SetRenderFunc(static (MaskRenderPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.rendererListHandle);
                    });
                }

                // --- Dilate mask ---
                TextureHandle currentMask = rawMask;

                if (volume.maskDilation.value > 0.01f)
                {
                    currentMask = AddBlitPass(renderGraph, currentMask, r8Desc,
                        "_MaskDilateH", "ChromDisp_DilateH", PASS_DILATE_H);
                    currentMask = AddBlitPass(renderGraph, currentMask, r8Desc,
                        "_MaskDilateV", "ChromDisp_DilateV", PASS_DILATE_V);
                }

                // --- Feather mask edges ---
                if (volume.maskFeather.value > 0.01f)
                {
                    currentMask = AddBlitPass(renderGraph, currentMask, r8Desc,
                        "_MaskFeatherH", "ChromDisp_MaskBlurH", PASS_MASKBLUR_H);
                    currentMask = AddBlitPass(renderGraph, currentMask, r8Desc,
                        "_MaskFeatherV", "ChromDisp_MaskBlurV", PASS_MASKBLUR_V);
                }

                finalMask = currentMask;
            }

            // ==============================================================
            // DISPLACEMENT pipeline
            // ==============================================================

            // --- Extract displacement source ---
            r8Desc.name = "_DispExtracted";
            r8Desc.clearBuffer = false;
            TextureHandle extractedDisp = AddBlitPass(renderGraph, source, r8Desc,
                "_DispExtracted", "ChromDisp_Extract", PASS_EXTRACT);

            // --- Iterative Gaussian blur ---
            int iterations = GetBlurIterations(volume.blurRadius.value);
            TextureHandle currentDisp = extractedDisp;

            for (int i = 0; i < iterations; i++)
            {
                currentDisp = AddBlitPass(renderGraph, currentDisp, r8Desc,
                    $"_DispBlurH_{i}", $"ChromDisp_BlurH_{i}", PASS_BLUR_H);
                currentDisp = AddBlitPass(renderGraph, currentDisp, r8Desc,
                    $"_DispBlurV_{i}", $"ChromDisp_BlurV_{i}", PASS_BLUR_V);
            }

            // ==============================================================
            // FINAL COMPOSITE
            // ==============================================================

            var finalDesc = renderGraph.GetTextureDesc(source);
            finalDesc.name = "_ChromaticDisplacementOutput";
            finalDesc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(finalDesc);

            using (var builder = renderGraph.AddRasterRenderPass<FinalPassData>(
                "ChromDisp_Final", out var finalPassData))
            {
                finalPassData.source = source;
                finalPassData.blurredDisplacement = currentDisp;
                finalPassData.objectMask = finalMask;
                finalPassData.material = m_Material;
                finalPassData.useMask = useMask;

                builder.UseTexture(source);
                builder.UseTexture(currentDisp);

                if (useMask && finalMask.IsValid())
                    builder.UseTexture(finalMask);

                builder.SetRenderAttachment(destination, 0);

                builder.SetRenderFunc(static (FinalPassData data, RasterGraphContext context) =>
                {
                    data.material.SetTexture(s_BlurredDisplacement, data.blurredDisplacement);

                    if (data.useMask && data.objectMask.IsValid())
                        data.material.SetTexture(s_ObjectMask, data.objectMask);

                    Blitter.BlitTexture(context.cmd, data.source,
                        new Vector4(1, 1, 0, 0), data.material, PASS_DISPLACE);
                });
            }

            // --- Copy result back to active color texture ---
            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>(
                "ChromDisp_CopyBack", out var copyData))
            {
                copyData.source = destination;

                builder.UseTexture(destination);
                builder.SetRenderAttachment(source, 0);

                builder.SetRenderFunc(static (CopyPassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source,
                        new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        /// <summary>
        /// Helper to add a single full-screen blit pass (used for extract, blur, dilate, feather).
        /// Creates a new texture and blits source into it through the specified shader pass.
        /// </summary>
        private TextureHandle AddBlitPass(
            RenderGraph renderGraph,
            TextureHandle source,
            TextureDesc destDesc,
            string textureName,
            string passName,
            int shaderPass)
        {
            destDesc.name = textureName;
            TextureHandle destination = renderGraph.CreateTexture(destDesc);

            using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(passName, out var passData))
            {
                passData.source = source;
                passData.material = m_Material;
                passData.passIndex = shaderPass;

                builder.UseTexture(source);
                builder.SetRenderAttachment(destination, 0);

                builder.SetRenderFunc(static (BlitPassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source,
                        new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                });
            }

            return destination;
        }

        public void Dispose()
        {
            CoreUtils.Destroy(m_Material);
            CoreUtils.Destroy(m_MaskMaterial);
            m_Material = null;
            m_MaskMaterial = null;
        }
    }
}
