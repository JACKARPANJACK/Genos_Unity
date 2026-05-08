Shader "Custom/VFX/SunFireball"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _CoreColor ("Core Color", Color) = (5, 5, 2, 1)
        [HDR] _MidColor ("Mid Color", Color) = (2, 0.5, 0, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (1, 0, 0, 1)
        
        [Header(Textures)]
        _MainTex ("Plasma Pattern", 2D) = "white" {}
        _NoiseTex ("Flow Noise", 2D) = "white" {}
        
        [Header(Animation)]
        _MainScroll ("Main Scroll", Vector) = (2, 1, 0, 0)
        _DistortAmount ("Distortion", Range(0, 1)) = 0.3
        
        [Header(Stylization)]
        _Erosion ("Anime Cutoff", Range(0, 1)) = 0.3
        _Smoothness ("Edge Softness", Range(0, 0.2)) = 0.01
        _RimPower ("Rim Intensity", Range(0.1, 10)) = 2.0
        
        [Header(Vertex FX)]
        _DispAmount ("Jaggedness", Float) = 0.1
        _DispSpeed ("Wobble Speed", Float) = 15.0
        _TailStretch ("Tail Length", Float) = 3.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Blend One One // Additive
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
                float vertexMask : TEXCOORD4;
            };

            sampler2D _MainTex, _NoiseTex;
            float4 _MainTex_ST;
            float4 _CoreColor, _MidColor, _EdgeColor, _MainScroll;
            float _DistortAmount, _Erosion, _Smoothness, _RimPower, _DispAmount, _DispSpeed, _TailStretch;

            Varyings vert (Attributes input)
            {
                Varyings output;
                float3 pos = input.positionOS.xyz;
                
                // Comet Teardrop Logic: Stretch the back, keep front sharp
                float zFactor = saturate(-pos.z * 2.0); // Assuming standard sphere, -z is back
                pos.z -= zFactor * _TailStretch;
                
                // Taper width towards tail
                float taper = lerp(1.0, 0.3, zFactor);
                pos.xy *= taper;
                
                // Energetic Flicker
                float time = _Time.y * _DispSpeed;
                float noise = sin(pos.x * 20 + time) * cos(pos.y * 20 + time);
                pos += input.normalOS * noise * _DispAmount * (1.0 + zFactor);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(pos);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.vertexMask = 1.0 - zFactor;
                
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                // Internal plasma flow
                float2 noiseUV = input.uv + _Time.y * 0.2;
                float noise = tex2D(_NoiseTex, noiseUV).r;
                
                float2 mainUV = input.uv + _Time.y * _MainScroll.xy + (noise - 0.5) * _DistortAmount;
                float4 fireTex = tex2D(_MainTex, mainUV);
                
                // Rim glow
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                float rim = pow(1.0 - saturate(dot(normal, viewDir)), _RimPower);
                
                // Masking
                float mask = saturate(fireTex.r + rim * 0.5);
                float alpha = smoothstep(_Erosion, _Erosion + _Smoothness, mask) * input.vertexMask;
                
                // Three-tone cell shaded ramp
                float3 color;
                if (mask > 0.8) color = _CoreColor.rgb;
                else if (mask > 0.4) color = _MidColor.rgb;
                else color = _EdgeColor.rgb;

                return float4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}