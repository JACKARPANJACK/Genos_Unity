Shader "Custom/VFX/AnimeFireball"
{
    Properties
    {
        _MainTex ("Main Fire Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 0.5, 0, 1)
        _InnerColor ("Core Color", Color) = (1, 1, 0.5, 1)
        _ScrollSpeed ("Scroll Speed", Vector) = (0.5, 0.5, 0.2, 0.2)
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Float) = 1.0
        _Distortion ("Distortion Amount", Range(0, 1)) = 0.2
        _RimPower ("Rim Power", Range(0, 10)) = 2.0
        _RimStrength ("Rim Strength", Float) = 1.0
        _DispAmount ("Displacement Amount", Float) = 0.1
        _DispSpeed ("Displacement Speed", Float) = 5.0
        _Erosion ("Erosion", Range(0, 1)) = 0.5
        _Smoothing ("Smoothing", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float3 viewDirWS : TEXCOORD3;
            };

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            float4 _MainTex_ST;
            float4 _BaseColor;
            float4 _InnerColor;
            float4 _ScrollSpeed;
            float _NoiseScale;
            float _Distortion;
            float _RimPower;
            float _RimStrength;
            float _DispAmount;
            float _DispSpeed;
            float _Erosion;
            float _Smoothing;

            Varyings vert (Attributes input)
            {
                Varyings output;
                
                float time = _Time.y * _DispSpeed;
                float noise = sin(input.positionOS.x * 5 + time) * cos(input.positionOS.y * 5 + time) * sin(input.positionOS.z * 5 + time);
                input.positionOS.xyz += input.normalOS * noise * _DispAmount;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                float2 noiseUV = input.uv * _NoiseScale + _Time.y * _ScrollSpeed.zw;
                float noise = tex2D(_NoiseTex, noiseUV).r;
                
                float2 distortedUV = input.uv + (noise - 0.5) * _Distortion + _Time.y * _ScrollSpeed.xy;
                float4 fireCol = tex2D(_MainTex, distortedUV);
                
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                float fresnel = 1.0 - saturate(dot(normal, viewDir));
                fresnel = pow(fresnel, _RimPower) * _RimStrength;
                
                float alpha = smoothstep(_Erosion, _Erosion + _Smoothing, fireCol.r);
                
                float3 finalColor = lerp(_BaseColor.rgb, _InnerColor.rgb, fireCol.r + fresnel);
                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
