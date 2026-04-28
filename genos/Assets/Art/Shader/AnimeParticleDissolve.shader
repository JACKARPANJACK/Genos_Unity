Shader "Custom/AnimeParticleDissolve"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture (A for Alpha)", 2D) = "white" {}
        [HDR] _BaseColor ("Outer Color", Color) = (1, 0.4, 0, 1)
        [HDR] _InnerColor ("Inner Color", Color) = (1, 1, 0.7, 1)
        
        _AlphaThreshold ("Base Clip Threshold", Range(0, 1)) = 0.1
        _EdgeSoftness ("Edge Softness", Range(0, 0.2)) = 0.01
        
        _ColorStep ("Inner Step", Range(0, 1)) = 0.5
        
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _InnerColor;
                float _AlphaThreshold;
                float _ColorStep;
                float _EdgeSoftness;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                
                // Robust Intensity detection:
                // Since we set textures to 'Alpha from Grayscale', the shape is in the Alpha channel.
                // However, the RGB might still contain the grayscale if sRGB was checked.
                float intensity = tex.a;
                
                // Particle Life Progress (driven by vertex alpha)
                float progress = 1.0 - i.color.a; 
                
                // Map life 1.0 -> 0.0 into a clipping threshold 0.0 -> 1.0
                float threshold = lerp(_AlphaThreshold, 1.05, progress);
                
                // Final Alpha clipping
                float alpha = smoothstep(threshold - _EdgeSoftness, threshold + _EdgeSoftness, intensity);
                
                // Anime Color Ramp
                float toon = smoothstep(_ColorStep - _EdgeSoftness, _ColorStep + _EdgeSoftness, intensity);
                float3 col = lerp(_BaseColor.rgb, _InnerColor.rgb, toon);
                col *= i.color.rgb; // Tint by particle color
                
                // Mandatory for transparency to work correctly with clipping
                if (alpha < 0.001) discard;
                
                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
}