Shader "Custom/GrayscalePRTShader"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture", 2D) = "white" {}
        [MainColor] _Color("Color", Color) = (1, 1, 1, 1)
        _Intensity("Intensity", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Color;
                float _Intensity;
            CBUFFER_END

            Varyings vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                // Calculate luminosity from the grayscale texture
                float luma = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                // Invert the luminosity so white parts are transparent (mask = 0) and black parts are opaque (mask = 1)
                float mask = 1.0 - luma;

                half4 col;
                col.rgb = _Color.rgb * mask * _Intensity * input.color.rgb;
                col.a = _Color.a * mask * input.color.a;

                return col;
            }
            ENDHLSL
        }
    }
}
