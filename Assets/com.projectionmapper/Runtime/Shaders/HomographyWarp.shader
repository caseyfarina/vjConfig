Shader "ProjectionMapper/HomographyWarp"
{
    Properties
    {
        _MainTex ("Source Texture", 2D) = "white" {}
        _CropRect ("Source Crop (xy=origin, zw=size)", Vector) = (0, 0, 1, 1)
        _EdgeFeather ("Edge Feather (L, R, B, T)", Vector) = (0, 0, 0, 0)
        _Brightness ("Brightness", Float) = 1.0
        _Gamma ("Gamma", Float) = 1.0
        _AAQuality ("AA Quality (0=None, 1=2x, 2=4x)", Float) = 1.0
        // _InvHomography is set from script as a 4x4 matrix (3x3 packed in upper-left)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "HomographyWarp"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenPos : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            float4x4 _InvHomography;
            float4 _CropRect;     // xy = origin, zw = size
            float4 _EdgeFeather;  // L, R, B, T feather widths in 0-0.5
            float _Brightness;
            float _Gamma;
            float _AAQuality;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = input.uv;
                return output;
            }

            // Apply the inverse homography to get source UVs, then remap
            // through the crop rect. The homography maps screen->unit square,
            // then the crop rect selects which region of the source texture
            // that unit square corresponds to.
            float2 ApplyInvHomography(float2 screenPos)
            {
                float3 p = float3(screenPos.x, screenPos.y, 1.0);
                float3 result;
                result.x = _InvHomography[0][0] * p.x + _InvHomography[0][1] * p.y + _InvHomography[0][2] * p.z;
                result.y = _InvHomography[1][0] * p.x + _InvHomography[1][1] * p.y + _InvHomography[1][2] * p.z;
                result.z = _InvHomography[2][0] * p.x + _InvHomography[2][1] * p.y + _InvHomography[2][2] * p.z;

                // Perspective divide gives us UV in unit square space (0-1)
                float2 uv = result.xy / result.z;

                // Remap through crop rect: unit square -> source texture sub-region
                uv = _CropRect.xy + uv * _CropRect.zw;

                return uv;
            }

            // Compute edge feather alpha for soft edges.
            // uv is in unit-square space (pre-crop), feather widths in that space.
            float ComputeEdgeAlpha(float2 unitUV)
            {
                float alpha = 1.0;

                // Left edge
                if (_EdgeFeather.x > 0.001)
                    alpha = min(alpha, saturate(unitUV.x / _EdgeFeather.x));
                // Right edge
                if (_EdgeFeather.y > 0.001)
                    alpha = min(alpha, saturate((1.0 - unitUV.x) / _EdgeFeather.y));
                // Bottom edge
                if (_EdgeFeather.z > 0.001)
                    alpha = min(alpha, saturate(unitUV.y / _EdgeFeather.z));
                // Top edge
                if (_EdgeFeather.w > 0.001)
                    alpha = min(alpha, saturate((1.0 - unitUV.y) / _EdgeFeather.w));

                return alpha;
            }

            // Get the unit-square UV (pre-crop) for edge feather computation
            float2 GetUnitUV(float2 screenPos)
            {
                float3 p = float3(screenPos.x, screenPos.y, 1.0);
                float3 result;
                result.x = _InvHomography[0][0] * p.x + _InvHomography[0][1] * p.y + _InvHomography[0][2] * p.z;
                result.y = _InvHomography[1][0] * p.x + _InvHomography[1][1] * p.y + _InvHomography[1][2] * p.z;
                result.z = _InvHomography[2][0] * p.x + _InvHomography[2][1] * p.y + _InvHomography[2][2] * p.z;
                return result.xy / result.z;
            }

            // Sample the source texture with proper gradient computation
            // for correct mip selection under the nonlinear warp.
            float4 SampleWithGradients(float2 screenPos)
            {
                float2 uv = ApplyInvHomography(screenPos);

                // Compute analytical gradients of the UV mapping.
                // This tells the GPU how fast UVs are changing per pixel,
                // which is essential for correct anisotropic filtering
                // under the nonlinear perspective warp.
                float2 uvDdx = ApplyInvHomography(screenPos + ddx(screenPos)) - uv;
                float2 uvDdy = ApplyInvHomography(screenPos + ddy(screenPos)) - uv;

                // Reject samples outside the crop region
                float2 cropMin = _CropRect.xy;
                float2 cropMax = _CropRect.xy + _CropRect.zw;
                if (uv.x < cropMin.x || uv.x > cropMax.x ||
                    uv.y < cropMin.y || uv.y > cropMax.y)
                    return float4(0, 0, 0, 0);

                return SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, uv, uvDdx, uvDdy);
            }

            // 4x Rotated Grid Super Sampling (RGSS) for anti-aliasing.
            // Offsets are rotated 26.6° to break up axis-aligned aliasing patterns.
            // This is the standard RGSS pattern used in film/VFX rendering.
            static const float2 RGSS_OFFSETS_4[4] =
            {
                float2(-0.125, -0.375),
                float2( 0.375, -0.125),
                float2(-0.375,  0.125),
                float2( 0.125,  0.375)
            };

            // 2x RGSS subset
            static const float2 RGSS_OFFSETS_2[2] =
            {
                float2(-0.25, -0.25),
                float2( 0.25,  0.25)
            };

            float4 frag(Varyings input) : SV_Target
            {
                float2 screenPos = input.screenPos;
                float4 color;

                // Compute pixel size in screen-UV space for sub-pixel offsets
                float2 pixelSize = float2(ddx(screenPos.x), ddy(screenPos.y));

                int quality = (int)_AAQuality;

                if (quality >= 2)
                {
                    // 4x RGSS
                    color = float4(0, 0, 0, 0);
                    [unroll]
                    for (int i = 0; i < 4; i++)
                    {
                        float2 offset = float2(
                            RGSS_OFFSETS_4[i].x * pixelSize.x,
                            RGSS_OFFSETS_4[i].y * pixelSize.y
                        );
                        color += SampleWithGradients(screenPos + offset);
                    }
                    color *= 0.25;
                }
                else if (quality >= 1)
                {
                    // 2x RGSS
                    color = float4(0, 0, 0, 0);
                    [unroll]
                    for (int i = 0; i < 2; i++)
                    {
                        float2 offset = float2(
                            RGSS_OFFSETS_2[i].x * pixelSize.x,
                            RGSS_OFFSETS_2[i].y * pixelSize.y
                        );
                        color += SampleWithGradients(screenPos + offset);
                    }
                    color *= 0.5;
                }
                else
                {
                    // No AA — single sample with correct gradients
                    color = SampleWithGradients(screenPos);
                }

                // Apply edge feather (soft edges for blending/masking)
                float2 unitUV = GetUnitUV(screenPos);
                float edgeAlpha = ComputeEdgeAlpha(unitUV);
                color.a *= edgeAlpha;

                // Apply brightness and gamma correction
                color.rgb *= _Brightness;
                color.rgb = pow(max(color.rgb, 0.0), 1.0 / max(_Gamma, 0.01));

                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
