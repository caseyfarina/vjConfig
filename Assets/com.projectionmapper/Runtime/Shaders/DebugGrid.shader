Shader "ProjectionMapper/DebugGrid"
{
    Properties
    {
        _MainTex ("Source Texture", 2D) = "white" {}
        _GridIndex ("Grid Index", Float) = 0
        _GridTotal ("Grid Total", Float) = 1
        _GridCols ("Grid Columns", Float) = 1
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
            Name "DebugGrid"

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
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _GridIndex;
            float _GridTotal;
            float _GridCols;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // Draw a thin border to distinguish grid cells
                float2 uv = input.uv;
                float borderWidth = 0.005;
                if (uv.x < borderWidth || uv.x > (1.0 - borderWidth) ||
                    uv.y < borderWidth || uv.y > (1.0 - borderWidth))
                {
                    // Cycle through colors for different surfaces
                    int idx = (int)_GridIndex;
                    float3 borderColors[6] = {
                        float3(1, 0.3, 0.3),
                        float3(0.3, 1, 0.3),
                        float3(0.3, 0.3, 1),
                        float3(1, 1, 0.3),
                        float3(1, 0.3, 1),
                        float3(0.3, 1, 1)
                    };
                    color.rgb = borderColors[idx % 6];
                    color.a = 1.0;
                }

                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
