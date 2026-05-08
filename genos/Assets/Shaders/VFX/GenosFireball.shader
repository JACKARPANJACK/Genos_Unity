Shader "Custom/VFX/GenosFireball"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _CoreColor ("Core (Hot)", Color) = (3, 3, 1, 1)
        [HDR] _MidColor ("Plasma (Mid)", Color) = (2, 0.5, 0, 1)
        [HDR] _EdgeColor ("Outer Edge", Color) = (1, 0, 0, 1)
        
        [Header(Textures)]
        _MainTex ("Anime Fire Texture", 2D) = "white" {}
        _NoiseTex ("Liquid Flow Noise", 2D) = "white" {}
        
        [Header(Animation)]
        _MainScroll ("Main Scroll Speed", Vector) = (2, 1, 0, 0)
        _DistortAmount ("Texture Distortion", Range(0, 1)) = 0.3
        
        [Header(Stylization)]
        _Erosion ("Anime Cutoff", Range(0, 1)) = 0.3
        _Smoothness ("Edge Softness", Range(0, 0.2)) = 0.01
        _RimPower ("Rim Spread", Range(0.1, 10)) = 2.0
        
        [Header(Vertex Shape)]
        _TailStretch ("Tail Length", Range(0, 10)) = 2.0
        _TailTaper ("Tail Taper", Range(0, 1)) = 0.5
        _DispAmount ("Boiling Intensity", Float) = 0.1
        _DispSpeed ("Flicker Speed", Float) = 15.0
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
            float _DistortAmount, _Erosion, _Smoothness, _RimPower, _DispAmount, _DispSpeed, _TailStretch, _TailTaper;

            Varyings vert (Attributes input)
            {
                Varyings output;
                float3 pos = input.positionOS.xyz;
                
                // Identify the "back" of the sphere (local Z < 0)
                float zFactor = saturate(-pos.z * 2.0); 
                
                // Stretch the back part into a comet tail
                pos.z -= zFactor * _TailStretch;
                
                // Taper the width (XY) as it goes back
                float taper = lerp(1.0, 1.0 - _TailTaper, zFactor);
                pos.xy *= taper;
                
                // Energetic Flicker
                float time = _Time.y * _DispSpeed;
                float noise = sin(pos.x * 12 + time) * cos(pos.y * 12 + time);
                pos += input.normalOS * noise * _DispAmount;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(pos);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.vertexMask = 1.0 - zFactor * 0.5; // Fade alpha towards tail end
                
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                // Internal plasma flow
                float2 noiseUV = input.uv + _Time.y * _MainScroll.zw; 
                float noise = tex2D(_NoiseTex, noiseUV).r;
                
                float2 mainUV = input.uv + _Time.y * _MainScroll.xy + (noise - 0.5) * _DistortAmount;
                float4 fireTex = tex2D(_MainTex, mainUV);
                
                // Rim for focus
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                float rim = pow(1.0 - saturate(dot(normal, viewDir)), _RimPower);
                
                // Cell-shaded mask
                float mask = saturate(fireTex.r + rim * 0.5);
                float alpha = smoothstep(_Erosion, _Erosion + _Smoothness, mask) * input.vertexMask;
                
                // Three-tone solar ramping
                float3 color;
                if (mask > 0.85) color = _CoreColor.rgb;
                else if (mask > 0.45) color = _MidColor.rgb;
                else color = _EdgeColor.rgb;

                // Add texture detail highlights
                color *= fireTex.g * 1.5;

                return float4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}