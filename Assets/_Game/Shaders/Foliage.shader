// Foliage / bark shader for URP: PBR lighting, alpha-clipped double-sided cards, wind sway + leaf flutter,
// subsurface-like translucency, per-instance hue variation and baked vertex AO.
// Vertex color (when _UseVertexColor = 1): R = wind weight, G = phase offset, B = ambient occlusion.
Shader "LastWorld/Foliage"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo (A = alpha)", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.4
        _Smoothness("Smoothness", Range(0,1)) = 0.2
        _Translucency("Translucency", Range(0,3)) = 0.8
        _TranslucencyColor("Translucency Color", Color) = (0.75,0.9,0.35,1)
        _WindStrength("Wind Strength", Float) = 0.3
        _WindSpeed("Wind Speed", Float) = 1.2
        _WindFlutter("Leaf Flutter", Float) = 0.04
        _WindHeight("Height Weight (no vertex color)", Float) = 0.5
        _UseVertexColor("Use Vertex Color", Float) = 1
        _VertexAO("Vertex AO Strength", Range(0,1)) = 1
        _ColorVariation("Hue Variation", Range(0,1)) = 0.2
        _BackfaceFlip("Flip Normals On Backfaces (0 for baked spherical normals)", Float) = 1
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _Cutoff;
            half _Smoothness;
            half _Translucency;
            half4 _TranslucencyColor;
            float _WindStrength;
            float _WindSpeed;
            float _WindFlutter;
            float _WindHeight;
            float _UseVertexColor;
            half _VertexAO;
            half _ColorVariation;
            float _BackfaceFlip;
        CBUFFER_END

        // xyz = wind direction, w = gust strength (set globally by WindController)
        float4 _LW_Wind;

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        float3 ApplyWind(float3 positionWS, float3 positionOS, float4 color)
        {
            float weight = _UseVertexColor > 0.5 ? color.r : saturate(positionOS.y * _WindHeight);
            float phase = color.g * 6.2831 + dot(positionWS.xz, float2(0.07, 0.05));
            float3 dir = normalize(_LW_Wind.xyz + float3(1e-4, 0, 0));
            float gust = _LW_Wind.w;
            float t = _Time.y * _WindSpeed;
            float sway = sin(t + phase) * 0.6 + sin(t * 2.3 + phase * 1.7) * 0.3 + 0.4;
            float3 offset = dir * sway * _WindStrength * gust * weight * weight;
            offset += float3(sin(t * 6.1 + positionWS.x * 3.0), sin(t * 7.3 + positionWS.z * 3.0), cos(t * 5.7 + positionWS.y * 3.0))
                      * _WindFlutter * gust * weight;
            return positionWS + offset;
        }

        struct AttributesSimple
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct VaryingsSimple
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        void ClipAlpha(float2 uv)
        {
            #if defined(_ALPHATEST_ON)
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a - _Cutoff);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half2 variationAO : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 positionWS = ApplyWind(TransformObjectToWorld(v.positionOS.xyz), v.positionOS.xyz, v.color);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = n.normalWS;
                o.tangentWS = float4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);

                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                float variation = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
                float ao = _UseVertexColor > 0.5 ? lerp(1.0, v.color.b, _VertexAO) : 1.0;
                o.variationAO = half2(variation, ao);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
                #endif
                half3 tint = lerp(half3(0.82, 0.9, 0.7), half3(1.12, 1.05, 0.82), i.variationAO.x);
                albedo.rgb *= lerp(half3(1, 1, 1), tint, _ColorVariation);

                float sgn = lerp(1.0, IS_FRONT_VFACE(face, 1.0, -1.0), _BackfaceFlip);
                float3 normalWS = normalize(i.normalWS) * sgn;
                float3 tangentWS = normalize(i.tangentWS.xyz);
                float3 bitangentWS = cross(normalWS, tangentWS) * i.tangentWS.w;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, half3x3(tangentWS, bitangentWS, normalWS)));

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = normalWS;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.bakedGI = SampleSH(normalWS);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo.rgb;
                s.alpha = 1;
                s.smoothness = _Smoothness;
                s.normalTS = normalTS;
                s.occlusion = i.variationAO.y;
                half4 color = UniversalFragmentPBR(input, s);

                // Light passing through leaves when looking toward the sun.
                Light mainLight = GetMainLight(input.shadowCoord);
                half back = pow(saturate(dot(input.viewDirectionWS, -mainLight.direction)), 4.0);
                half wrap = saturate(dot(-normalWS, mainLight.direction)) * 0.5;
                color.rgb += albedo.rgb * _TranslucencyColor.rgb * mainLight.color * mainLight.shadowAttenuation
                             * (back + wrap) * _Translucency * i.variationAO.y;

                color.rgb = MixFog(color.rgb, i.fogFactor);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            VaryingsSimple vertShadow(AttributesSimple v)
            {
                VaryingsSimple o = (VaryingsSimple)0;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = ApplyWind(TransformObjectToWorld(v.positionOS.xyz), v.positionOS.xyz, v.color);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                float3 lightDir = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 fragShadow(VaryingsSimple i) : SV_Target
            {
                ClipAlpha(i.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            VaryingsSimple vertDepth(AttributesSimple v)
            {
                VaryingsSimple o = (VaryingsSimple)0;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = ApplyWind(TransformObjectToWorld(v.positionOS.xyz), v.positionOS.xyz, v.color);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 fragDepth(VaryingsSimple i) : SV_Target
            {
                ClipAlpha(i.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            VaryingsSimple vertDN(AttributesSimple v)
            {
                VaryingsSimple o = (VaryingsSimple)0;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = ApplyWind(TransformObjectToWorld(v.positionOS.xyz), v.positionOS.xyz, v.color);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 fragDN(VaryingsSimple i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                ClipAlpha(i.uv);
                float3 n = normalize(i.normalWS) * lerp(1.0, IS_FRONT_VFACE(face, 1.0, -1.0), _BackfaceFlip);
                return half4(n, 0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
