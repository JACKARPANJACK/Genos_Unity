Shader "Custom/AnimeEnvironment"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        
        _ShadowColor("Shadow Color", Color) = (0.5, 0.5, 0.7, 1)
        _ToonThreshold("Toon Threshold", Range(0, 1)) = 0.5
        _ToonSmoothness("Toon Smoothness", Range(0, 0.1)) = 0.02

        [Header(Specular)]
        _SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularSize("Specular Size", Range(0, 1)) = 0.1
        _SpecularSmoothness("Specular Smoothness", Range(0, 0.1)) = 0.01

        [Header(Rim Lighting)]
        _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower("Rim Power", Range(0.1, 10)) = 3
        _RimThreshold("Rim Threshold", Range(0, 1)) = 0.5
    [Header(Painterly)]
    _PainterlyMap("Painterly Map (Overlay)", 2D) = "white" {}
    _PainterlyStrength("Painterly Strength", Range(0, 1)) = 0.2
    _PainterlyScale("Painterly Scale", Float) = 1.0
    }

    SubShader
    {
    Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
    LOD 100

    Pass
    {
        Name "ForwardLit"
        Tags { "LightMode" = "UniversalForward" }

        HLSLPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fwdbase
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
        #pragma multi_compile _ _SHADOWS_SOFT

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            float3 viewDirWS : TEXCOORD1;
            float2 uv : TEXCOORD3;
            float3 positionWS : TEXCOORD4;
            float4 screenPos : TEXCOORD5;
        };

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_PainterlyMap);
        SAMPLER(sampler_PainterlyMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _ShadowColor;
            float _ToonThreshold;
            float _ToonSmoothness;
            float4 _SpecularColor;
            float _SpecularSize;
            float _SpecularSmoothness;
            float4 _RimColor;
            float _RimPower;
            float _RimThreshold;
            float _PainterlyStrength;
            float _PainterlyScale;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.screenPos = ComputeScreenPos(output.positionCS);
            return output;
        }

        float4 frag(Varyings input) : SV_Target
        {
            float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            float3 normal = normalize(input.normalWS);
            float3 viewDir = normalize(input.viewDirWS);

            // Get Main Light
            #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light light = GetMainLight(shadowCoord);
            #else
                Light light = GetMainLight();
            #endif

            // Painterly Overlay (Screen Space)
            float2 screenUV = input.screenPos.xy / input.screenPos.w;
            float4 painterly = SAMPLE_TEXTURE2D(_PainterlyMap, sampler_PainterlyMap, screenUV * _PainterlyScale);
            float3 painterlyColor = painterly.rgb;

            // Toon Diffuse
            float NdotL = dot(normal, light.direction);
                
            // Jitter the threshold with painterly noise
            float jitteredThreshold = _ToonThreshold + (painterlyColor.r - 0.5) * _PainterlyStrength;
            float lightIntensity = smoothstep(jitteredThreshold - _ToonSmoothness, jitteredThreshold + _ToonSmoothness, NdotL * light.shadowAttenuation);
                
            float3 diffuse = lerp(_ShadowColor.rgb * texColor.rgb, texColor.rgb, lightIntensity);
                
            // Blend in painterly texture to the diffuse color
            diffuse *= lerp(float3(1,1,1), painterlyColor, _PainterlyStrength * 0.5);

            // Toon Specular
            float3 halfDir = normalize(light.direction + viewDir);
            float NdotH = dot(normal, halfDir);
            float specularIntensity = smoothstep(_SpecularSize - _SpecularSmoothness, _SpecularSize + _SpecularSmoothness, NdotH);
            float3 specular = specularIntensity * _SpecularColor.rgb * lightIntensity;

            // Rim Lighting
            float rim = 1.0 - saturate(dot(normal, viewDir));
            rim = pow(rim, _RimPower);
            float rimIntensity = smoothstep(_RimThreshold - 0.01, _RimThreshold + 0.01, rim);
            float3 rimColor = rimIntensity * _RimColor.rgb * lightIntensity;

            float3 finalColor = diffuse + specular + rimColor;

            return float4(finalColor, texColor.a);
        }
        ENDHLSL
    }

        // Add ShadowCaster pass for shadows
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
