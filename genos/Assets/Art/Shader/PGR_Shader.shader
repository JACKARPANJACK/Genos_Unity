Shader "Custom/PGR_Shader"
{
    Properties
    {
        [Header(Base Settings)]
        _MainTex ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color Tint", Color) = (1,1,1,1)

        [Header(Ambient Occlusion)]
        [Toggle(_USE_AO)] _UseAO ("Use AO Map", Float) = 0
        _AOMap ("AO Map", 2D) = "white" {}
        _AOUVScale ("AO UV Scale (X,Y)", Vector) = (1, 1, 0, 0)
        _AOIntensity ("AO Intensity", Range(0, 1)) = 1

        [Header(Normal Map)]
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 5)) = 1.0

        [Header(Lighting)]
        [Toggle(_USE_ILM)] _UseILM ("Use ILM Map (R:Shadow, G:Emission, B:Spec, A:Ramp)", Float) = 0
        _ILMMap ("ILM Map", 2D) = "white" {}
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = 0.0
        _ShadowSoftness ("Shadow Softness", Range(0, 0.5)) = 0.05
        _ShadowColor ("Shadow Tint", Color) = (0.5, 0.5, 0.6, 1)

        [Header(Face SDF)]
        [Toggle(_USE_FACESDF)] _UseFaceSDF ("Use Face SDF", Float) = 0
        _FaceSDFMap ("Face SDF (R:Threshold)", 2D) = "white" {}
        _FaceForward ("Face Forward (X,Y,Z)", Vector) = (0, 0, 1, 0)
        _FaceRight ("Face Right (X,Y,Z)", Vector) = (1, 0, 0, 0)

        [Header(Anime Eyes (Skin Feature))]
        [Toggle(_ANIME_EYES)] _AnimeEyes ("Enable Anime Eye Highlights", Float) = 0
        [Toggle(_EYE_FROM_EMISSION)] _EyeFromEmission ("Sample From Emission Map", Float) = 0
        _EyeHighlightTex ("Eye Highlight Texture (RGB or Alpha)", 2D) = "black" {}
        [HDR] _EyeHighlightColor ("Eye Highlight Tint", Color) = (1,1,1,1)
        _EyeTracking ("View Tracking (Parallax Offset)", Range(-0.5, 0.5)) = 0.05
        _EyeParallaxX ("Pan Offset X", Range(-1, 1)) = 0
        _EyeParallaxY ("Pan Offset Y", Range(-1, 1)) = 0

        [Header(Toggleable Highlights)]
        [Toggle(_USE_SPECULAR)] _UseSpecular ("Enable Specular Highlights", Float) = 0
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularSmoothness ("Highlight Size (Smoothness)", Range(0.01, 1)) = 0.5
        _SpecularStep ("Highlight Softness", Range(0.001, 0.5)) = 0.01

        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(1, 20)) = 5
        _RimIntensity ("Rim Intensity", Range(0, 5)) = 1

        [Header(Emission and Animation)]
        [Toggle(_USE_EMISSION)] _UseEmission ("Enable Emission", Float) = 1
        [HDR] _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionMap ("Emission Map (RGB or R Mask)", 2D) = "black" {}
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 1
        [Enum(TextureRGB,0, MaskR,1, MaskG,2, MaskB,3, MaskRGBMax,4)] _EmissionMode ("Emission Mode", Float) = 4
        _EmissionThreshold ("Emission Threshold", Range(0, 1)) = 0.1
        _EmissionSoftness ("Emission Softness", Range(0.001, 0.5)) = 0.05
        _ScrollX ("Scroll Speed X", Range(-5, 5)) = 0
        _ScrollY ("Scroll Speed Y", Range(-5, 5)) = 0

        [Header(Outline (Object Space))]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02

        [KeywordEnum(Skin, Hair, Cloth, Metal)] _ShadingMode("Shading Mode", Float) = 0
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            float _OutlineWidth;
            float4 _OutlineColor;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);

                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float2 normalCS = mul(GetWorldToHClipMatrix(), float4(normalWS, 0.0)).xy;
                o.positionCS.xy += normalize(normalCS) * _OutlineWidth * o.positionCS.w;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
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
            
            // -------------------------------------
            // Universal Render Pipeline keywords (REQUIRED for lighting/shadows to not break)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            // -------------------------------------

            #pragma shader_feature _SHADINGMODE_SKIN _SHADINGMODE_HAIR _SHADINGMODE_CLOTH _SHADINGMODE_METAL
            #pragma shader_feature_local _USE_SPECULAR
            #pragma shader_feature_local _USE_EMISSION
            #pragma shader_feature_local _USE_ILM
            #pragma shader_feature_local _USE_FACESDF
            #pragma shader_feature_local _USE_AO
            #pragma shader_feature_local _ANIME_EYES
            #pragma shader_feature_local _EYE_FROM_EMISSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_AOMap);          SAMPLER(sampler_AOMap);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap);     SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_ILMMap);          SAMPLER(sampler_ILMMap);
            TEXTURE2D(_FaceSDFMap);      SAMPLER(sampler_FaceSDFMap);
            TEXTURE2D(_EyeHighlightTex); SAMPLER(sampler_EyeHighlightTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _RimColor;
                float4 _EmissionColor;
                float4 _SpecularColor;
                float4 _EyeHighlightColor;

                float4 _AOUVScale;
                float _AOIntensity;

                float4 _FaceForward;
                float4 _FaceRight;

                float _ShadowThreshold;
                float _ShadowSoftness;
                float _RimPower;
                float _RimIntensity;

                float _ScrollX;
                float _ScrollY;
                float _BumpScale;
                float _SpecularSmoothness;
                float _SpecularStep;

                float _EmissionIntensity;
                float _EmissionMode;

                // ✅ ADD THESE (THIS IS WHAT FIXES YOUR ERROR)
                float _EmissionThreshold;
                float _EmissionSoftness;

                float _EyeTracking;
                float _EyeParallaxX;
                float _EyeParallaxY;

            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float3 positionWS : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vertexInput.positionCS;
                o.positionWS = vertexInput.positionWS;
                o.uv = v.uv;

                VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.normalWS = normalInput.normalWS;
                o.tangentWS = normalInput.tangentWS;
                o.bitangentWS = normalInput.bitangentWS;

                o.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Grab precise shadow and environment data from URP
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 viewDir = normalize(i.viewDirWS);
                
                // Sample Global Illumination (Ambient Light)
                half3 ambient = SampleSH(normalize(i.normalWS));

                half4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                
                #if defined(_USE_AO)
                    float2 aoUV = i.uv * _AOUVScale.xy;
                    half4 aoTex = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, aoUV);
                    // Standard Multiply AO: Darkens the base texture realistically based on intensity
                    baseTex.rgb = lerp(baseTex.rgb, baseTex.rgb * aoTex.rgb, _AOIntensity);
                #endif

                half4 baseCol = baseTex * _BaseColor;

                half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv);
                half3 normalTS = UnpackNormalScale(normalSample, _BumpScale);
                half3x3 tbn = half3x3(normalize(i.tangentWS), normalize(i.bitangentWS), normalize(i.normalWS));
                half3 normal = normalize(mul(normalTS, tbn));

                float NdotL = dot(normal, mainLight.direction);
                float shadow = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, NdotL);
                
                // Multiply our stylized dot shadow against Unity's physical Cast Shadows so other objects can block light
                shadow = min(shadow, mainLight.shadowAttenuation);

                #if defined(_USE_ILM)
                    half4 ilmTex = SAMPLE_TEXTURE2D(_ILMMap, sampler_MainTex, i.uv);
                    // R: Shadow modification
                    shadow = smoothstep((_ShadowThreshold + ilmTex.r) - _ShadowSoftness, (_ShadowThreshold + ilmTex.r) + _ShadowSoftness, NdotL);
                #endif

                #if defined(_USE_FACESDF)
                    half4 sdfTex = SAMPLE_TEXTURE2D(_FaceSDFMap, sampler_MainTex, i.uv);
                    
                    // Transform light to local space or use object space vectors
                    // Note: Here we're assuming _FaceForward and _FaceRight are defined in world space for simplicity
                    // Alternatively, we get local light dir mapping to U,V of the SDF.
                    float3 fwd = normalize((mul(unity_ObjectToWorld, float4(normalize(_FaceForward.xyz), 0))).xyz);
                    float3 rgt = normalize((mul(unity_ObjectToWorld, float4(normalize(_FaceRight.xyz), 0))).xyz);
                    
                    float lightFwd = dot(mainLight.direction, fwd);
                    float lightRgt = dot(mainLight.direction, rgt);
                    
                    // Compare light angle with SDF threshold
                    float sdfThreshold = sdfTex.r;
                    
                    // Simple wrap
                    float angle = atan2(lightRgt, lightFwd) / 3.14159;
                    float shadowSide = sign(lightRgt);
                    
                    // If the angle falls in the SDF shaded region
                    shadow = (abs(angle) < sdfThreshold) ? 1.0 : 0.0;
                    shadow = smoothstep(sdfThreshold - _ShadowSoftness, sdfThreshold + _ShadowSoftness, abs(lightRgt));
                #endif

                float3 diffuse = lerp(_ShadowColor.rgb * baseCol.rgb, baseCol.rgb, shadow);
                
                // Subtly inject ambient environment light into pitch dark areas so it fits neatly into Unity scenes
                diffuse += ambient * baseCol.rgb * 0.5 * lerp(1.0, 0.2, shadow);

                float3 specular = 0;
                #if defined(_USE_SPECULAR)
                    float3 halfDirSpec = normalize(mainLight.direction + viewDir);
                    float NdotH = saturate(dot(normal, halfDirSpec));
                    float specExponent = exp2(10.0 * _SpecularSmoothness + 1.0);
                    float rawSpec = pow(NdotH, specExponent);
                    
                    // Base the standard specular mask on the raw NdotL light data
                    // This prevents ILM "forced shadows" from accidentally hiding the shiny spots
                    float specMask = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, NdotL);
                    
                    #if defined(_USE_ILM)
                        // Only let the ILM Blue map reduce specular to ~30% minimum, 
                        // so pure black maps don't completely destroy the highlight.
                        specMask *= lerp(0.3, 1.0, ilmTex.b);
                    #endif
                    
                    float specIntensity = smoothstep(0.5 - _SpecularStep, 0.5 + _SpecularStep, rawSpec) * specMask;
                    specular = specIntensity * _SpecularColor.rgb;
                #endif

                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _RimPower);
                float3 rim = fresnel * _RimColor.rgb * _RimIntensity * shadow;

                #ifdef _SHADINGMODE_HAIR
                    float3 halfDir = normalize(mainLight.direction + viewDir);

                    // The issue with the patches is that the 3D mesh's physical normals are too spiky/jagged.
                    // Anisotropic shading shatters if the mesh isn't mathematically smoothed in Blender/Maya.
                    // We fix this by crushing the Y-axis of the normal to 10% before calculating light, 
                    // creating a fake "smooth cylinder" normal mathematically. This guarantees an unbroken horizontal ring.
                    float3 ringNormal = normalize(float3(normal.x, normal.y * 0.1, normal.z));

                    // Calculate a smoothed Blinn-Phong highlight on this fake cylindrical normal. 
                    float baseSpec = saturate(dot(ringNormal, halfDir));

                    // Now we inject a tiny bit of the REAL normal and Normal Map back in. 
                    // This creates the sharp, jagged anime "teeth" along the band, without breaking it.
                    float strandShift = (normal.y * 0.05) + (normalTS.x * 0.05);
                    float NdotH1 = baseSpec + strandShift;
                    float NdotH2 = baseSpec + strandShift - 0.04; // Drop the second band slightly lower

                    float hairExponent = exp2(10.0 * _SpecularSmoothness + 1.0);
                    float rawSpec1 = pow(saturate(NdotH1), hairExponent);
                    float rawSpec2 = pow(saturate(NdotH2), max(1.0, hairExponent * 0.6));

                    float hairSpecMask = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, NdotL);

                    #if defined(_USE_ILM)
                        // In PGR/WuWa, the ILM Blue map carefully clips out the hair roots and deep cracks
                        hairSpecMask *= lerp(0.0, 1.0, ilmTex.b);
                    #endif

                    float spec1 = smoothstep(0.5 - _SpecularStep, 0.5 + _SpecularStep, rawSpec1);
                    float spec2 = smoothstep(0.5 - _SpecularStep, 0.5 + _SpecularStep, rawSpec2) * 0.5; // Dimmer secondary band

                    float totalSpec = saturate(spec1 + spec2) * hairSpecMask;

                    diffuse += totalSpec * _SpecularColor.rgb;
                #endif

                #ifdef _SHADINGMODE_METAL
                    // 1. Metals absorb a lot of diffuse light
                    diffuse *= 0.6;
                    
                    // 2. Tint existing specular highlights with the base color (crucial for colored metals like gold/copper)
                    specular *= (baseCol.rgb * 1.5 + 0.5);
                    
                    // 3. Create a stylized, harsh reflection band based on view angle for NPR metal look
                    float VdotN = 1.0 - saturate(dot(normal, viewDir)); // Inverted for rim measurement
                    float metalBand = smoothstep(0.6, 0.65, VdotN) * smoothstep(0.85, 0.8, VdotN);
                    diffuse += metalBand * baseCol.rgb * _RimColor.rgb * shadow * 2.0;
                #endif

                float3 emission = 0;
                float ilmEmissionMask = 0.0;

                #if defined(_USE_ILM)
                    ilmEmissionMask = ilmTex.g;
                #endif

                #if defined(_USE_EMISSION)
                    float2 scrollingUV = i.uv + float2(_ScrollX, _ScrollY) * _Time.y;
                    half4 emissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, scrollingUV);

                    half rawMask = 0.0;
                    half3 emissionSource = half3(1,1,1); // Default to white so _EmissionColor completely overrides it

                    // Extract the correct map channel based on selected _EmissionMode
                    if (_EmissionMode < 0.5) // Texture RGB
                    {
                        rawMask = max(emissionTex.r, max(emissionTex.g, emissionTex.b));
                        emissionSource = emissionTex.rgb; // Only preserve actual texture colors in RGB mode
                    }
                    else if (_EmissionMode < 1.5) // R Channel Mask
                    {
                        rawMask = emissionTex.r;
                    }
                    else if (_EmissionMode < 2.5) // G Channel Mask
                    {
                        rawMask = emissionTex.g;
                    }
                    else if (_EmissionMode < 3.5) // B Channel Mask (For the Blue Emission Map)
                    {
                        rawMask = emissionTex.b;
                    }
                    else // Max RGB Mask
                    {
                        rawMask = max(emissionTex.r, max(emissionTex.g, emissionTex.b));
                    }

                    half emissionMask = smoothstep(_EmissionThreshold, _EmissionThreshold + _EmissionSoftness, rawMask);

                    // Calculate main texture emission base
                    emission = emissionSource * _EmissionColor.rgb * _EmissionIntensity;

                    #if defined(_USE_ILM)
                        // Additively blend ILM emission
                        emission += (ilmEmissionMask * _EmissionColor.rgb * _EmissionIntensity);
                    #endif
                    
                    // STRICT MASKING: Ensure black areas on the emissive channel perfectly mask ALL emission operations
                    // Only the colored parts (defined by the Emission Mode mask) can emit light.
                    emission *= emissionMask;
                #else
                    // If no main EMISSION map is toggled or bound, ensure ILM emission mask can still produce emission if desired
                    #if defined(_USE_ILM)
                        emission = ilmEmissionMask * _EmissionColor.rgb * _EmissionIntensity;
                    #endif
                #endif

                #if defined(_ANIME_EYES)
                    // Anime Eye Highlights (Parallax / Camera Tracking)
                    // Transform view dir to object space to track strictly with the face/head orientation
                    float3 viewDirOS = TransformWorldToObjectDir(viewDir);
                    
                    // Offset UVs based on view direction so the highlight subtly follows the camera.
                    // Most anime heads point forward along local +Z or -Z, so X and Y correspond to eye axes.
                    float2 eyeOffset = float2(viewDirOS.x, viewDirOS.y) * _EyeTracking;
                    float2 eyeUV = i.uv + eyeOffset + float2(_EyeParallaxX, _EyeParallaxY);
                    
                    float eyeMask = 0.0;
                    float3 eyeHighlightRGB = 0.0;

                    #if defined(_EYE_FROM_EMISSION)
                        // Sample the emission map using the parallax UV
                        half4 eyeEmissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, eyeUV);
                        half eyeRawMask = 0.0;
                        half3 eyeEmissionSource = half3(1,1,1);

                        // Properly apply the same channel extraction logic
                        if (_EmissionMode < 0.5) 
                        {
                            eyeRawMask = max(eyeEmissionTex.r, max(eyeEmissionTex.g, eyeEmissionTex.b));
                            eyeEmissionSource = eyeEmissionTex.rgb;
                        }
                        else if (_EmissionMode < 1.5) 
                        {
                            eyeRawMask = eyeEmissionTex.r;
                        }
                        else if (_EmissionMode < 2.5) 
                        {
                            eyeRawMask = eyeEmissionTex.g;
                        }
                        else if (_EmissionMode < 3.5) 
                        {
                            eyeRawMask = eyeEmissionTex.b;
                        }
                        else 
                        {
                            eyeRawMask = max(eyeEmissionTex.r, max(eyeEmissionTex.g, eyeEmissionTex.b));
                        }

                        eyeMask = smoothstep(_EmissionThreshold, _EmissionThreshold + _EmissionSoftness, eyeRawMask);
                        
                        // Combine _EmissionColor and _EyeHighlightColor for ultimate control
                        eyeHighlightRGB = eyeEmissionSource * _EmissionColor.rgb * _EmissionIntensity * _EyeHighlightColor.rgb * _EyeHighlightColor.a;
                    #else
                        half4 eyeHighlightSample = SAMPLE_TEXTURE2D(_EyeHighlightTex, sampler_MainTex, eyeUV);
                        // Support maps that encode the shape in the alpha channel or just have a black background
                        eyeMask = max(eyeHighlightSample.a, max(eyeHighlightSample.r, max(eyeHighlightSample.g, eyeHighlightSample.b)));
                        eyeHighlightRGB = eyeHighlightSample.rgb * _EyeHighlightColor.rgb * _EyeHighlightColor.a;
                    #endif
                    
                    // Eye highlights are typically unlit/emissive, so we add them to the emission pass at the very end
                    emission += eyeHighlightRGB * eyeMask;
                #endif

                float3 finalRGB = diffuse + specular + rim + emission;
                return half4(finalRGB, baseCol.a);
                }
                ENDHLSL
                }

                // Pass to cast shadows
                Pass
                {
                Name "ShadowCaster"
                Tags { "LightMode" = "ShadowCaster" }

                ZWrite On
                ZTest LEqual
                ColorMask 0
                Cull Back

                HLSLPROGRAM
                #pragma vertex ShadowPassVertex
                #pragma fragment ShadowPassFragment

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

                struct Attributes
                {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                };

                struct Varyings
                {
                float4 positionCS   : SV_POSITION;
                };

                float3 _LightDirection;

                Varyings ShadowPassVertex(Attributes input)
                {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
                }

                half4 ShadowPassFragment(Varyings input) : SV_TARGET
                {
                return 0;
                }
                ENDHLSL
                }

                // Pass to write depth for SSAO and other depth-based effects
                Pass
                {
                Name "DepthOnly"
                Tags { "LightMode" = "DepthOnly" }

                ZWrite On
                ColorMask 0
                Cull Back

                HLSLPROGRAM
                #pragma vertex DepthOnlyVertex
                #pragma fragment DepthOnlyFragment

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

                struct Attributes
                {
                float4 positionOS   : POSITION;
                };

                struct Varyings
                {
                float4 positionCS   : SV_POSITION;
                };

                Varyings DepthOnlyVertex(Attributes input)
                {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
                }

                half4 DepthOnlyFragment(Varyings input) : SV_TARGET
                {
                return 0;
                }
                ENDHLSL
                }
                }
                }