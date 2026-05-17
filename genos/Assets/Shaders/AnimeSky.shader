Shader "Custom/AnimeSky"
{
    Properties
    {
        [Header(Sky)]
        _TopColor("Top Color", Color) = (0.1, 0.3, 0.7, 1)
        _MiddleColor("Middle Color", Color) = (0.3, 0.6, 1, 1)
        _BottomColor("Bottom Color", Color) = (0.8, 0.9, 1, 1)
        
        [Header(Sun)]
        _SunColor("Sun Color", Color) = (1, 1, 0.8, 1)
        _SunDirection("Sun Direction", Vector) = (0, 1, 0, 0)
        _SunSize("Sun Size", Range(0, 1)) = 0.05
        _SunSoftness("Sun Softness", Range(0, 0.1)) = 0.01

        [Header(Clouds)]
        _CloudColor("Cloud Color", Color) = (1, 1, 1, 1)
        _CloudScale("Cloud Scale", Float) = 5.0
        _CloudSpeed("Cloud Speed", Float) = 0.05
        _CloudThreshold("Cloud Threshold", Range(0, 1)) = 0.5
        _CloudSoftness("Cloud Softness", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float4 _MiddleColor;
                float4 _BottomColor;
                float4 _SunColor;
                float4 _SunDirection;
                float _SunSize;
                float _SunSoftness;
                float4 _CloudColor;
                float _CloudScale;
                float _CloudSpeed;
                float _CloudThreshold;
                float _CloudSoftness;
            CBUFFER_END

            // Simple hash for noise
            float hash(float n) { return frac(sin(n) * 43758.5453123); }

            float noise(float3 x)
            {
                float3 p = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n = p.x + p.y * 57.0 + 113.0 * p.z;
                return lerp(lerp(lerp(hash(n + 0.0), hash(n + 1.0), f.x),
                                 lerp(hash(n + 57.0), hash(n + 58.0), f.x), f.y),
                            lerp(lerp(hash(n + 113.0), hash(n + 114.0), f.x),
                                 lerp(hash(n + 170.0), hash(n + 171.0), f.x), f.y), f.z);
            }

            float fbm(float3 p)
            {
                float f = 0.0;
                f += 0.5000 * noise(p); p = p * 2.02;
                f += 0.2500 * noise(p); p = p * 2.03;
                f += 0.1250 * noise(p); p = p * 2.01;
                f += 0.0625 * noise(p);
                return f / 0.9375;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDir = input.positionOS.xyz;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(input.viewDir);
                float y = viewDir.y;

                // Sky Gradient
                float3 skyColor;
                if (y > 0)
                    skyColor = lerp(_MiddleColor.rgb, _TopColor.rgb, pow(y, 0.5));
                else
                    skyColor = lerp(_MiddleColor.rgb, _BottomColor.rgb, pow(-y, 0.5));

                // Sun
                float3 sunDir = normalize(_SunDirection.xyz);
                float sunDoc = dot(viewDir, sunDir);
                float sunMask = smoothstep(_SunSize - _SunSoftness, _SunSize, sunDoc);
                skyColor = lerp(skyColor, _SunColor.rgb, sunMask);

                // Clouds
                if (y > 0.05)
                {
                    float3 cloudPos = viewDir / max(viewDir.y, 0.01);
                    cloudPos.xz += _Time.y * _CloudSpeed;
                    float c = fbm(cloudPos * _CloudScale * 0.1);
                    float cloudMask = smoothstep(_CloudThreshold, _CloudThreshold + _CloudSoftness, c);
                    cloudMask *= smoothstep(0.05, 0.3, y); // Fade clouds at horizon
                    skyColor = lerp(skyColor, _CloudColor.rgb, cloudMask * _CloudColor.a);
                }

                return float4(skyColor, 1.0);
            }
            ENDHLSL
        }
    }
}
