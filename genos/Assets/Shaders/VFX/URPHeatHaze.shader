Shader "Custom/VFX/URPHeatHaze"
{
    Properties
    {
        [MainTexture] _MainTex ("Main Texture / Mask (RGBA)", 2D) = "white" {}
        [MainColor] _Color ("Color", Color) = (1, 1, 1, 1)
        _DistortTex ("Distortion Texture", 2D) = "white" {}
        _DistortAmount ("Distortion Amount", Range(0, 0.5)) = 0.05
        _ScrollSpeed ("Scroll Speed", Vector) = (0.5, 0.5, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "HeatHaze"
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uvMain : TEXCOORD0;
                float2 uvDistort : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float4 color : COLOR;
            };

            sampler2D _DistortTex;
            float4 _DistortTex_ST;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _DistortAmount;
            float4 _ScrollSpeed;

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uvMain = TRANSFORM_TEX(input.uv, _MainTex);
                output.uvDistort = TRANSFORM_TEX(input.uv, _DistortTex);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.color = input.color;
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                // Sample main texture/mask and evaluate final transparent color
                float4 mainCol = tex2D(_MainTex, input.uvMain) * _Color * input.color;

                float finalAlpha = mainCol.a;

                // Sample distortion noise with its proper tiling/offset
                float2 noiseUV = input.uvDistort + _Time.y * _ScrollSpeed.xy;
                float3 noise = tex2D(_DistortTex, noiseUV).rgb;

                // Calculate distortion offset
                float2 offset = (noise.rg - 0.5) * _DistortAmount;

                // Fix aspect ratio so distortion does not look stretched horizontally
                offset.x *= _ScreenParams.y / _ScreenParams.x;

                // Apply the exact alpha mask to offset so edges do not distort abruptly
                offset *= finalAlpha;

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float3 sceneCol = SampleSceneColor(screenUV + offset);

                // Because we are using Blend SrcAlpha OneMinusSrcAlpha:
                // Unity will automatically do: Result = (Output) * Alpha + UnrenderedBackground * (1 - Alpha)
                // If we output the Distorted Scene multiplied by our texture's color:
                // - High alpha: Full distorted scene, fully tinted/multiplied by the texture color.
                // - Low alpha: Gracefully blends back into the undistorted standard background.
                float3 finalColor = sceneCol * mainCol.rgb;

                return float4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}