Shader "VFX/AnimeRadialActionLines"
{
    Properties
    {
        [HDR] _MainColor ("Action Line Color", Color) = (0,0,0,1)
        _Speed ("Animation Speed", Float) = 20.0
        _Density ("Line Density", Float) = 150.0
        _Thickness ("Line Thickness (Chance)", Range(0, 1)) = 0.3
        _InnerRadius ("Inner Mask Radius", Range(0, 1)) = 0.2
        _OuterRadius ("Outer Mask Radius", Range(0, 2)) = 1.0
        _EdgeFade ("Edge Mask Softness", Float) = 0.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always // Renders over everything

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
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            float4 _MainColor;
            float _Speed;
            float _Density;
            float _Thickness;
            float _InnerRadius;
            float _OuterRadius;
            float _EdgeFade;

            // Pseudo-random 1D value based on 2D input
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Center UV from -0.5 to +0.5 for polar rotation
                o.uv = v.uv - 0.5;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Convert UV to polar coordinates
                float dist = length(i.uv) * 2.0; // Length from center, 0 to 1+
                float angle = atan2(i.uv.y, i.uv.x); // Angle from -PI to PI

                // Map angle to distinct "rays" by boxing values based on Density
                float angleNormalized = (angle + 3.14159265) / 6.2831853; 
                float rayId = floor(angleNormalized * _Density);

                // Add jumping variation based on time to animate the spikes
                float timeTick = floor(_Time.y * _Speed);
                float noise = rand(float2(rayId, timeTick));

                // Determine if we draw a line (binary)
                float lineMask = step(noise, _Thickness);

                // Fade out the inner core so the center is clear (focus on subject)
                float innerMask = smoothstep(_InnerRadius, _InnerRadius + _EdgeFade, dist);

                // Fade out the outer edge of the screen
                float outerMask = 1.0 - smoothstep(_OuterRadius - _EdgeFade, _OuterRadius, dist);

                float finalAlpha = lineMask * innerMask * outerMask * _MainColor.a * i.color.a;

                return float4(_MainColor.rgb * i.color.rgb, finalAlpha);
            }
            ENDCG
        }
    }
}
