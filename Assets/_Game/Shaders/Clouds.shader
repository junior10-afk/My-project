// Procedural cloud layer drawn on a camera-centred dome. Lit by the main light (sun or moon) with
// a cheap self-shadowing step toward the light, coverage/speed driven from material or WindController.
Shader "LastWorld/Clouds"
{
    Properties
    {
        _Coverage("Coverage", Range(0,1)) = 0.52
        _Softness("Softness", Range(0.01,0.5)) = 0.2
        _CloudScale("Scale", Float) = 0.9
        _CloudSpeed("Speed", Float) = 0.006
        _Density("Density", Range(0,1)) = 0.9
        _ShadowColor("Shadow Tint", Color) = (0.55, 0.6, 0.7, 1)
        _HorizonFade("Horizon Fade", Range(0.01,0.5)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-400" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Coverage;
                half _Softness;
                float _CloudScale;
                float _CloudSpeed;
                half _Density;
                half4 _ShadowColor;
                half _HorizonFade;
            CBUFFER_END

            float4 _LW_Wind;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + 1);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 6; k++)
                {
                    s += a * ValueNoise(p);
                    p = p * 2.03 + 17.1;
                    a *= 0.5;
                }
                return s;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 dir = normalize(i.positionWS - _WorldSpaceCameraPos);
                if (dir.y <= 0.0) return 0;

                float2 drift = normalize(_LW_Wind.xz + float2(1e-4, 0)) * _Time.y * _CloudSpeed;
                float2 uv = dir.xz / (dir.y + 0.15) * _CloudScale + drift;
                float n = Fbm(uv);
                float cover = smoothstep(_Coverage, _Coverage + _Softness, n);

                Light mainLight = GetMainLight();
                float3 L = mainLight.direction;
                float n2 = Fbm(uv + L.xz * 0.06);
                float shade = saturate(1.0 - (n2 - n) * 4.0 - cover * 0.35);

                half3 ambient = SampleSH(float3(0, 1, 0));
                half3 sunLit = mainLight.color * saturate(L.y + 0.25);
                half3 col = lerp(_ShadowColor.rgb * (ambient + sunLit * 0.25), ambient + sunLit, shade);

                // Silver lining when looking toward the light
                half forward = pow(saturate(dot(dir, L)), 8.0);
                col += mainLight.color * forward * (1.0 - cover) * 1.5;

                float alpha = cover * _Density * smoothstep(0.0, _HorizonFade, dir.y);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
