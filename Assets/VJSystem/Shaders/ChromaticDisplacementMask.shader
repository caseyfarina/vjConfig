Shader "Hidden/PostProcess/ChromaticDisplacementMask"
{
    // Renders objects as flat white into an R8 mask texture.
    // Applied automatically by the mask render pass — you don't need
    // to assign this shader to any materials manually.

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "MaskWrite"

            ZWrite On
            ZTest LEqual
            Cull Back
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 posOS : POSITION;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.posCS = TransformObjectToHClip(v.posOS.xyz);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                return float4(1, 0, 0, 1);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
