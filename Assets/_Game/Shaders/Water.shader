// Ocean / lake shader for URP: Gerstner waves, scrolling detail normals, depth-based absorption,
// screen-space refraction, environment reflection with Fresnel, sun glint and shoreline foam.
// Requires Depth Texture and Opaque Texture enabled on the URP asset.
Shader "LastWorld/Water"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.10, 0.45, 0.45, 1)
        _DeepColor("Deep Color", Color) = (0.01, 0.07, 0.13, 1)
        _Absorption("Absorption (per meter)", Vector) = (0.45, 0.12, 0.08, 0)
        _DepthDistance("Color Depth Distance", Float) = 6
        [Normal] _NormalMap("Detail Normal", 2D) = "bump" {}
        _NormalScale("Normal Tiling", Float) = 0.08
        _NormalSpeed("Normal Speed", Float) = 0.03
        _NormalStrength("Normal Strength", Float) = 0.6
        _RefractionStrength("Refraction", Float) = 0.03
        _Smoothness("Smoothness", Range(0,1)) = 0.95
        _SpecPower("Sun Glint Power", Float) = 600
        _SpecIntensity("Sun Glint Intensity", Float) = 4
        _FoamMap("Foam Noise", 2D) = "white" {}
        _FoamColor("Foam Color", Color) = (0.95, 0.97, 1, 1)
        _FoamDistance("Shore Foam Distance", Float) = 1.6
        _WaveSpeed("Wave Speed", Float) = 1
        _WaveA("Wave A (dir, steepness, wavelength)", Vector) = (1, 0.3, 0.12, 38)
        _WaveB("Wave B", Vector) = (0.4, 1, 0.10, 21)
        _WaveC("Wave C", Vector) = (-0.6, 0.7, 0.08, 12)
        _WaveD("Wave D", Vector) = (0.9, -0.5, 0.05, 7)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-100" "RenderPipeline"="UniversalPipeline" }
        ZWrite On
        Blend Off
        Cull Back

        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float4 _Absorption;
                float _DepthDistance;
                float4 _NormalMap_ST;
                float _NormalScale;
                float _NormalSpeed;
                float _NormalStrength;
                float _RefractionStrength;
                half _Smoothness;
                float _SpecPower;
                float _SpecIntensity;
                float4 _FoamMap_ST;
                half4 _FoamColor;
                float _FoamDistance;
                float _WaveSpeed;
                float4 _WaveA;
                float4 _WaveB;
                float4 _WaveC;
                float4 _WaveD;
            CBUFFER_END

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamMap); SAMPLER(sampler_FoamMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            float3 Gerstner(float4 wave, float3 p, inout float3 tangent, inout float3 binormal)
            {
                float steepness = wave.z;
                float k = 2.0 * PI / wave.w;
                float c = sqrt(9.8 / k);
                float2 d = normalize(wave.xy);
                float f = k * (dot(d, p.xz) - c * _Time.y * _WaveSpeed);
                float a = steepness / k;
                float s = sin(f), co = cos(f);
                tangent += float3(-d.x * d.x * steepness * s, d.x * steepness * co, -d.x * d.y * steepness * s);
                binormal += float3(-d.x * d.y * steepness * s, d.y * steepness * co, -d.y * d.y * steepness * s);
                return float3(d.x * a * co, a * s, d.y * a * co);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);
                float3 offset = Gerstner(_WaveA, p, tangent, binormal);
                offset += Gerstner(_WaveB, p, tangent, binormal);
                offset += Gerstner(_WaveC, p, tangent, binormal);
                offset += Gerstner(_WaveD, p, tangent, binormal);
                p += offset;
                o.positionWS = p;
                o.normalWS = normalize(cross(binormal, tangent));
                o.positionCS = TransformWorldToHClip(p);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _NormalSpeed;
                float2 uv1 = i.positionWS.xz * _NormalScale + t * float2(0.8, 0.3);
                float2 uv2 = i.positionWS.xz * _NormalScale * 0.57 - t * float2(0.25, 0.9);
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1), _NormalStrength);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2), _NormalStrength);
                half3 nd = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                float3 normalWS = normalize(normalize(i.normalWS) + float3(nd.x, 0, nd.y));

                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float surfaceDepth = i.screenPos.w;
                float depth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams) - surfaceDepth;

                float2 refrUV = screenUV + nd.xy * _RefractionStrength * saturate(depth * 0.25);
                float refrDepth = LinearEyeDepth(SampleSceneDepth(refrUV), _ZBufferParams) - surfaceDepth;
                if (refrDepth < 0) { refrUV = screenUV; refrDepth = depth; }
                half3 sceneColor = SampleSceneColor(refrUV);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 ambient = SampleSH(float3(0, 1, 0));
                half3 light = mainLight.color * mainLight.shadowAttenuation * saturate(mainLight.direction.y) * 0.7 + ambient;

                half3 absorb = exp(-refrDepth * _Absorption.rgb);
                half3 body = lerp(_DeepColor.rgb, _ShallowColor.rgb, exp(-refrDepth / _DepthDistance)) * light;
                half3 refracted = sceneColor * absorb + body * (1 - absorb);

                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(normalWS, viewDir)), 5.0);
                half3 reflection = GlossyEnvironmentReflection(reflect(-viewDir, normalWS), 1.0 - _Smoothness, 1.0);
                float3 h = normalize(mainLight.direction + viewDir);
                half3 glint = pow(saturate(dot(normalWS, h)), _SpecPower) * _SpecIntensity * mainLight.color * mainLight.shadowAttenuation;

                half3 color = lerp(refracted, reflection, saturate(fresnel)) + glint;

                float foamNoise = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, i.positionWS.xz * 0.12 + float2(_Time.y * 0.015, 0)).r;
                float foamNoise2 = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, i.positionWS.xz * 0.05 - float2(0, _Time.y * 0.01)).r;
                float shore = 1.0 - saturate(depth / _FoamDistance);
                float foam = smoothstep(0.45, 0.6, foamNoise * foamNoise2 * 1.6 * shore + shore * shore * 0.5);
                color = lerp(color, _FoamColor.rgb * light, foam);

                // Soften the exact contact line with the shore.
                color = lerp(SampleSceneColor(screenUV), color, saturate(depth * 3.0));
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
