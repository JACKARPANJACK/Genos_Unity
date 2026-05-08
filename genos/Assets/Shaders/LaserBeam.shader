Shader "Custom/LaserBeam"
{
    Properties
    {
        [HDR] _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (1, 0, 0, 1)
        _MainTex ("Mask (RGB)", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseSpeed ("Noise Speed (X, Y)", Vector) = (-2.0, 0.0, 0, 0)
        _NoiseScale ("Noise Power", Range(0, 1)) = 0.5
        _CoreThickness ("Core Thickness", Range(0, 1)) = 0.2
        _AuraThickness ("Aura Thickness", Range(0, 1)) = 0.8
        _EdgeSoftness ("Edge Softness (Low for Anime)", Range(0.001, 1)) = 0.05
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "IgnoreProjector"="True" 
            "PreviewType"="Plane"
        }
        LOD 100

        ZWrite Off
        Blend SrcAlpha One // Additive blending for nice laser glow
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 noiseUv : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            float4 _CoreColor;
            float4 _GlowColor;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            float2 _NoiseSpeed;
            float _NoiseScale;
            float _CoreThickness;
            float _AuraThickness;
            float _EdgeSoftness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // Scrolling noise UV
                o.noiseUv = TRANSFORM_TEX(v.uv, _NoiseTex);
                o.noiseUv += _NoiseSpeed * _Time.y;

                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample textures
                float mask = tex2D(_MainTex, i.uv).r;
                float noise = tex2D(_NoiseTex, i.noiseUv).r;

                // Distance from the center of the beam (Y = 0.5)
                float distFromCenter = abs(i.uv.y - 0.5) * 2.0;

                // Apply noise to distort the apparent thickness (gives a jagged energy look)
                distFromCenter += (noise - 0.5) * _NoiseScale;

                // Anime style has sharp, distinct bands of brightness rather than a soft gradient
                float core = smoothstep(_CoreThickness + _EdgeSoftness, _CoreThickness, distFromCenter);
                float aura = smoothstep(_AuraThickness + _EdgeSoftness, _AuraThickness, distFromCenter);

                // Two-tone color mixing
                float4 finalColor = _GlowColor * aura;
                finalColor = lerp(finalColor, _CoreColor, core);
                finalColor.a = max(aura, core);

                // Apply main texture mask (for beam ends) and vertex color
                finalColor *= mask * i.color;

                return finalColor;
            }
            ENDCG
        }
    }
}