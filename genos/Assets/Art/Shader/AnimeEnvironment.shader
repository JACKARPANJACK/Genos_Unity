Shader "Custom/AnimeEnvironment"
{
    Properties
    {
        [Header(Base Settings)]
        _MainTex ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color Tint", Color) = (1,1,1,1)

        [Header(Automated Ramp Lighting)]
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = 0.0
        _ShadowSoftness ("Shadow Softness", Range(0, 0.5)) = 0.05
        _ShadowColor ("Shadow Tint", Color) = (0.5, 0.5, 0.6, 1)

        [Header(Environment Settings)]
        _AmbientIntensity ("Ambient Intensity", Range(0, 2)) = 0.5
        _LightIntensity ("Light Intensity", Range(0, 5)) = 1.0

        [Header(Outline)]
        [Toggle(_USE_OUTLINE)] _UseOutline ("Enable Outline", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _USE_OUTLINE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            float _OutlineWidth;
            float4 _OutlineColor;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                #if defined(_USE_OUTLINE)
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float2 normalCS = mul(GetWorldToHClipMatrix(), float4(normalWS, 0.0)).xy;
                o.positionCS.xy += normalize(normalCS) * _OutlineWidth * o.positionCS.w;
                #endif
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                #if !defined(_USE_OUTLINE)
                clip(-1.0);
                #endif
                return _OutlineColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float _ShadowThreshold;
                float _ShadowSoftness;
                float _AmbientIntensity;
                float _LightIntensity;
                float _OutlineWidth;
                float4 _OutlineColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vertexInput.positionCS;
                o.positionWS = vertexInput.positionWS;
                o.uv = v.uv;

                VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS, float4(1,0,0,1));
                o.normalWS = normalInput.normalWS;
                
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Environment and Shadow Data
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                
                half3 ambient = SampleSH(normalize(i.normalWS));

                half4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 baseCol = baseTex * _BaseColor;

                float3 normal = normalize(i.normalWS);
                
                // Automate ramp using smoothstep
                float NdotL = dot(normal, mainLight.direction);
                float shadow = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, NdotL);
                
                // Incorporate physical cast shadows
                shadow = min(shadow, mainLight.shadowAttenuation);

                // Lighting application
                float3 diffuse = lerp(_ShadowColor.rgb * baseCol.rgb, baseCol.rgb * mainLight.color * _LightIntensity, shadow);
                
                // Subtly inject ambient light
                diffuse += ambient * baseCol.rgb * _AmbientIntensity * lerp(1.0, 0.2, shadow);

                return half4(diffuse, baseCol.a);
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}