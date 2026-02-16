Shader "Hidden/PostProcess/ChromaticDisplacement"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        // =================================================================
        // PASS 0: Extract displacement source to single-channel texture
        // =================================================================
        Pass
        {
            Name "ExtractDisplacement"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_local _ _USE_DEPTH_SOURCE
            #pragma multi_compile_local _ _USE_EXTERNAL_MAP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_DisplacementMap);
            SAMPLER(sampler_DisplacementMap);

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            float _DepthInfluence;

            struct AppData { uint vertexID : SV_VertexID; };
            struct V2F { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V2F Vert(AppData v)
            {
                V2F o;
                o.posCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv    = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            float4 Frag(V2F i) : SV_Target
            {
                float displacement = 0.0;

                #if defined(_USE_EXTERNAL_MAP)
                    displacement = SAMPLE_TEXTURE2D(_DisplacementMap, sampler_DisplacementMap, i.uv).r;
                #elif defined(_USE_DEPTH_SOURCE)
                    float rawDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv).r;
                    float linearDepth = Linear01Depth(rawDepth, _ZBufferParams);
                    displacement = linearDepth * _DepthInfluence;
                #else
                    float3 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv).rgb;
                    displacement = dot(col, float3(0.2126, 0.7152, 0.0722));
                #endif

                return float4(displacement, 0, 0, 1);
            }

            ENDHLSL
        }

        // =================================================================
        // PASS 1: Separable Gaussian Blur — Horizontal
        // =================================================================
        Pass
        {
            Name "GaussianBlurH"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _BlitTexture_TexelSize;
            float  _BlurRadius;

            static const float weights[3] = { 0.227027, 0.316215, 0.070270 };
            static const float offsets[3] = { 0.0, 1.384615, 3.230769 };

            struct AppData { uint vertexID : SV_VertexID; };
            struct V2F { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V2F Vert(AppData v)
            {
                V2F o;
                o.posCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv    = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            float4 Frag(V2F i) : SV_Target
            {
                float2 texelSize = _BlitTexture_TexelSize.xy;
                float result = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv).r * weights[0];

                [unroll]
                for (int s = 1; s < 3; s++)
                {
                    float2 offset = float2(texelSize.x * offsets[s] * _BlurRadius, 0.0);
                    result += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv + offset).r * weights[s];
                    result += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv - offset).r * weights[s];
                }

                return float4(result, 0, 0, 1);
            }

            ENDHLSL
        }

        // =================================================================
        // PASS 2: Separable Gaussian Blur — Vertical
        // =================================================================
        Pass
        {
            Name "GaussianBlurV"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _BlitTexture_TexelSize;
            float  _BlurRadius;

            static const float weights[3] = { 0.227027, 0.316215, 0.070270 };
            static const float offsets[3] = { 0.0, 1.384615, 3.230769 };

            struct AppData { uint vertexID : SV_VertexID; };
            struct V2F { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V2F Vert(AppData v)
            {
                V2F o;
                o.posCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv    = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            float4 Frag(V2F i) : SV_Target
            {
                float2 texelSize = _BlitTexture_TexelSize.xy;
                float result = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv).r * weights[0];

                [unroll]
                for (int s = 1; s < 3; s++)
                {
                    float2 offset = float2(0.0, texelSize.y * offsets[s] * _BlurRadius);
                    result += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv + offset).r * weights[s];
                    result += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv - offset).r * weights[s];
                }

                return float4(result, 0, 0, 1);
            }

            ENDHLSL
        }

        // =================================================================
        // PASS 3: Mask Dilation — Horizontal Max Filter
        // Expands mask outward so the displacement effect can bleed
        // past the original object silhouette
        // =================================================================
        Pass
        {
            Name "DilateMaskH"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _BlitTexture_TexelSize;
            float  _DilateRadius;

            struct AppData { uint vertexID : SV_VertexID; };
            struct V2F { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V2F Vert(AppData v)
            {
                V2F o;
                o.posCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv    = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            float4 Frag(V2F i) : SV_Target
            {
                float maxVal = 0.0;
                float texelX = _BlitTexture_TexelSize.x;

                // Sample across the dilation radius, take the max
                // Using fixed 9-sample spread scaled by radius
                [unroll]
                for (int s = -4; s <= 4; s++)
                {
                    float2 offset = float2(texelX * s * _DilateRadius, 0.0);
                    float sample_val = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv + offset).r;
                    maxVal = max(maxVal, sample_val);
                }

                return float4(maxVal, 0, 0, 1);
            }

            ENDHLSL
        }

        // =================================================================
        // PASS 4: Mask Dilation — Vertical Max Filter
        // =================================================================
        Pass
        {
            Name "DilateMaskV"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _BlitTexture_TexelSize;
            float  _DilateRadius;

            struct AppData { uint vertexID : SV_VertexID; };
            struct V2F { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V2F Vert(AppData v)
            {
                V2F o;
                o.posCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv    = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            float4 Frag(V2F i) : SV_Target
            {
                float maxVal = 0.0;
                float texelY = _BlitTexture_TexelSize.y;

                [unroll]
                for (int s = -4; s <= 4; s++)
                {
                    float2 offset = float2(0.0, texelY * s * _DilateRadius);
                    float sample_val = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv + offset).r;
                    maxVal = max(maxVal, sample_val);
                }

                return float4(maxVal, 0, 0, 1);
            }

            ENDHLSL
        }

        // =================================================================
        // PASS 5: Mask Blur — Horizontal (softens dilated mask edges)
        // Reuses same Gaussian kernel as displacement blur
        // =================================================================
        Pass
        {
            Name "MaskBlurH"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _BlitTexture_TexelSize;
            float  _MaskFeather;

            static const float weights[3] = { 0.227027, 0.316215, 0.070270 };
            static const float offsets[3] = { 0.0, 1.384615, 3.230769 };

            struct AppData { uint vertexID : SV_VertexID; };
            struct V2F { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V2F Vert(AppData v)
            {
                V2F o;
                o.posCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv    = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            float4 Frag(V2F i) : SV_Target
            {
                float2 texelSize = _BlitTexture_TexelSize.xy;
                float result = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv).r * weights[0];

                [unroll]
                for (int s = 1; s < 3; s++)
                {
                    float2 offset = float2(texelSize.x * offsets[s] * _MaskFeather, 0.0);
                    result += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv + offset).r * weights[s];
                    result += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv - offset).r * weights[s];
                }

                return float4(result, 0, 0, 1);
            }

            ENDHLSL
        }

        // =================================================================
        // PASS 6: Mask Blur — Vertical
        // =================================================================
        Pass
        {
            Name "MaskBlurV"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _BlitTexture_TexelSize;
            float  _MaskFeather;

            static const float weights[3] = { 0.227027, 0.316215, 0.070270 };
            static const float offsets[3] = { 0.0, 1.384615, 3.230769 };

            struct AppData { uint vertexID : SV_VertexID; };
            struct V2F { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V2F Vert(AppData v)
            {
                V2F o;
                o.posCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv    = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            float4 Frag(V2F i) : SV_Target
            {
                float2 texelSize = _BlitTexture_TexelSize.xy;
                float result = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv).r * weights[0];

                [unroll]
                for (int s = 1; s < 3; s++)
                {
                    float2 offset = float2(0.0, texelSize.y * offsets[s] * _MaskFeather);
                    result += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv + offset).r * weights[s];
                    result += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv - offset).r * weights[s];
                }

                return float4(result, 0, 0, 1);
            }

            ENDHLSL
        }

        // =================================================================
        // PASS 7: Final Chromatic Displacement + Mask Composite + Palette
        // =================================================================
        Pass
        {
            Name "ChromaticDisplacement"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_local _ _RADIAL_FALLOFF
            #pragma multi_compile_local _ _USE_MASK
            #pragma multi_compile_local _ _CUSTOM_PALETTE
            #pragma multi_compile_local _ _BLEND_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_BlurredDisplacement);
            SAMPLER(sampler_BlurredDisplacement);

            TEXTURE2D(_ObjectMask);
            SAMPLER(sampler_ObjectMask);

            float  _DisplacementAmount;
            float  _ChannelAAmount;
            float  _ChannelBAmount;
            float  _ChannelCAmount;
            float2 _ChannelAAngle;
            float2 _ChannelBAngle;
            float2 _ChannelCAngle;
            float  _DisplacementScale;
            float2 _Center;
            float  _RadialFalloffStart;
            float  _RadialFalloffEnd;
            float  _RadialFalloffPower;
            float4 _BlitTexture_TexelSize;

            // Custom palette colors (HDR)
            float4 _ColorA;
            float4 _ColorB;
            float4 _ColorC;

            struct AppData { uint vertexID : SV_VertexID; };
            struct V2F { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V2F Vert(AppData v)
            {
                V2F o;
                o.posCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv    = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            float SampleDisplacement(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_BlurredDisplacement, sampler_BlurredDisplacement, uv).r;
            }

            float2 ComputeDisplacementGradient(float2 uv)
            {
                float2 texelSize = _BlitTexture_TexelSize.xy * _DisplacementScale;

                float tl = SampleDisplacement(uv + float2(-texelSize.x,  texelSize.y));
                float t  = SampleDisplacement(uv + float2( 0.0,          texelSize.y));
                float tr = SampleDisplacement(uv + float2( texelSize.x,  texelSize.y));
                float l  = SampleDisplacement(uv + float2(-texelSize.x,  0.0));
                float r  = SampleDisplacement(uv + float2( texelSize.x,  0.0));
                float bl = SampleDisplacement(uv + float2(-texelSize.x, -texelSize.y));
                float b  = SampleDisplacement(uv + float2( 0.0,         -texelSize.y));
                float br = SampleDisplacement(uv + float2( texelSize.x, -texelSize.y));

                float gx = (tr + 2.0 * r + br) - (tl + 2.0 * l + bl);
                float gy = (tl + 2.0 * t + tr) - (bl + 2.0 * b + br);

                return float2(gx, gy);
            }

            float2 RotateUV(float2 v, float2 cosSin)
            {
                return float2(
                    v.x * cosSin.x - v.y * cosSin.y,
                    v.x * cosSin.y + v.y * cosSin.x
                );
            }

            // Screen blend: 1 - (1-a)(1-b)
            float3 ScreenBlend(float3 a, float3 b)
            {
                return 1.0 - (1.0 - a) * (1.0 - b);
            }

            float4 Frag(V2F i) : SV_Target
            {
                float2 uv = i.uv;

                // Original scene color (always needed for fallback/composite)
                float4 original = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // Compute displacement
                float2 gradient = ComputeDisplacementGradient(uv);
                float2 baseDisp = gradient * _DisplacementAmount;

                float2 offsetA = RotateUV(baseDisp, _ChannelAAngle) * _ChannelAAmount;
                float2 offsetB = RotateUV(baseDisp, _ChannelBAngle) * _ChannelBAmount;
                float2 offsetC = RotateUV(baseDisp, _ChannelCAngle) * _ChannelCAmount;

                // Radial falloff
                float falloffMask = 1.0;
                #if defined(_RADIAL_FALLOFF)
                    float2 delta = uv - _Center;
                    delta.x *= _BlitTexture_TexelSize.z * _BlitTexture_TexelSize.y;
                    float dist = length(delta);
                    falloffMask = saturate((dist - _RadialFalloffStart) /
                                  max(_RadialFalloffEnd - _RadialFalloffStart, 0.0001));
                    falloffMask = pow(falloffMask, _RadialFalloffPower);
                #endif

                offsetA *= falloffMask;
                offsetB *= falloffMask;
                offsetC *= falloffMask;

                // --- Compute displaced result ---
                float3 displaced;

                #if defined(_CUSTOM_PALETTE)
                    // Custom palette mode: sample luminance at each offset, tint by user color
                    float3 sampleA = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offsetA).rgb;
                    float3 sampleB = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offsetB).rgb;
                    float3 sampleC = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offsetC).rgb;

                    float lumA = dot(sampleA, float3(0.2126, 0.7152, 0.0722));
                    float lumB = dot(sampleB, float3(0.2126, 0.7152, 0.0722));
                    float lumC = dot(sampleC, float3(0.2126, 0.7152, 0.0722));

                    float3 tintedA = lumA * _ColorA.rgb * _ColorA.a;
                    float3 tintedB = lumB * _ColorB.rgb * _ColorB.a;
                    float3 tintedC = lumC * _ColorC.rgb * _ColorC.a;

                    #if defined(_BLEND_SCREEN)
                        displaced = ScreenBlend(ScreenBlend(tintedA, tintedB), tintedC);
                    #else
                        // Additive
                        displaced = tintedA + tintedB + tintedC;
                    #endif
                #else
                    // Standard RGB passthrough mode
                    float cr = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offsetA).r;
                    float cg = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offsetB).g;
                    float cb = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offsetC).b;
                    displaced = float3(cr, cg, cb);
                #endif

                // --- Mask compositing ---
                #if defined(_USE_MASK)
                    float mask = SAMPLE_TEXTURE2D(_ObjectMask, sampler_ObjectMask, uv).r;
                    float3 result = lerp(original.rgb, displaced, mask);
                    return float4(result, original.a);
                #else
                    return float4(displaced, original.a);
                #endif
            }

            ENDHLSL
        }
    }

    Fallback Off
}
