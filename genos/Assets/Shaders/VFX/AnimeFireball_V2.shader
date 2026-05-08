Shader "Custom/VFX/AnimeFireball_V2"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _CoreColor ("Core Color", Color) = (5, 5, 2, 1)
        [HDR] _MidColor ("Mid Color", Color) = (2, 0.5, 0, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (1, 0, 0, 1)
        
        [Header(Textures)]
        _MainTex ("Fire Texture", 2D) = "white" {}
        _NoiseTex ("Distortion Noise", 2D) = "white" {}
        
        [Header(Animation)]
        _ScrollSpeed ("Scroll Speed (XY)", Vector) = (2, 1, 0, 0)
        _DistortAmount ("Distortion Intensity", Range(0, 1)) = 0.2
        
        [Header(Stylization)]
        _Erosion ("Anime Cutoff", Range(0, 1)) = 0.3
        _Smoothness ("Edge Softness", Range(0, 0.5)) = 0.02
        _RimPower ("Rim Spread", Range(0.1, 10)) = 2.0
        
        [Header(Vertex Shape)]
        _TailStretch ("Tail Length", Float) = 3.0
        _TailTaper ("Tail Sharpness", Range(0, 1)) = 0.5
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
                float3 viewDirWS : TEXCOORD2;
                float vertexMask : TEXCOORD3;
            };

            sampler2D _MainTex, _NoiseTex;
            float4 _MainTex_ST;
            float4 _CoreColor, _MidColor, _EdgeColor, _ScrollSpeed;
            float _DistortAmount, _Erosion, _Smoothness, _RimPower, _DispAmount, _DispSpeed, _TailStretch, _TailTaper;

            Varyings vert (Attributes input)
            {
                Varyings output;
                float3 pos = input.positionOS.xyz;
                
                // Identify back part (local Z is forward in many meshes)
                // Assuming standard sphere where -Z is back
                float zFactor = saturate(-pos.z * 2.0); 
                
                // Stretch the back part into a tail
                pos.z -= zFactor * _TailStretch;
                
                // Taper the width (XY) as it goes back
                float taper = lerp(1.0, 1.0 - _TailTaper, zFactor);
                pos.xy *= taper;
                
                // Boiling Flicker
                float time = _Time.y * _DispSpeed;
                float noise = sin(pos.x * 12 + time) * cos(pos.y * 12 + time);
                pos += input.normalOS * noise * _DispAmount;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(pos);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.vertexMask = 1.0 - zFactor * 0.5;
                
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                float2 noiseUV = input.uv + _Time.y * 0.2;
                float noise = tex2D(_NoiseTex, noiseUV).r;
                
                float2 mainUV = input.uv + _Time.y * _ScrollSpeed.xy + (noise - 0.5) * _DistortAmount;
                float4 fireTex = tex2D(_MainTex, mainUV);
                
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                float rim = pow(1.0 - saturate(dot(normal, viewDir)), _RimPower);
                
                float mask = saturate(fireTex.r + rim * 0.5);
                float alpha = smoothstep(_Erosion, _Erosion + _Smoothness, mask) * input.vertexMask;
                
                float3 color;
                if (mask > 0.85) color = _CoreColor.rgb;
                else if (mask > 0.5) color = _MidColor.rgb;
                else color = _EdgeColor.rgb;

                // Add texture detail variation
                color *= lerp(0.8, 1.2, fireTex.g);

                return float4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}