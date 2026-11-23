Shader "SolarSys/SunGlare"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.95, 0.85, 1)
        _Intensity ("Intensity", Float) = 5
        _Disk ("Disk Radius (uv)", Float) = 0.04
        _Fade ("Fade", Float) = 1
        _DiskGain ("Disk Gain", Float) = 0.9
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity, _Disk, _Fade, _DiskGain;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), f.x),
                            lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), f.x), f.y);
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float x = r / max(_Disk, 1e-4);      // расстояние в радиусах диска Солнца
                float ang = atan2(p.y, p.x);
                float t = _Time.y;

                float disc = smoothstep(1.2, 0.85, x);            // раскалённый белый диск
                float glow = 1.0 / (1.0 + x * x * 0.35);          // ближнее свечение
                float halo = exp(-r * 5.0);                       // широкая мягкая дымка

                // Лучи: два набора с разной частотой, медленно «дышат»
                // Лучи: шум по окружности, разная длина и яркость, мягкие края
                float2 c = float2(cos(ang), sin(ang));
                float n1 = vnoise(c * 5.0  + t * 0.02);
                float n2 = vnoise(c * 12.0 - t * 0.03 + 7.3);
                float n3 = vnoise(c * 28.0 + 3.1);
                float s = n1 * 0.55 + n2 * 0.3 + n3 * 0.15;
                s = pow(smoothstep(0.3, 0.95, s), 1.6);

                // Яркие лучи тянутся дальше, слабые гаснут быстро
                float len = exp(-r * lerp(9.0, 3.0, s));
                float rays = s * len * smoothstep(0.6, 2.5, x);   // начинаются от края диска, не из центра
                // Горизонтальный анаморфный штрих
                float streak = exp(-abs(p.y) * 60.0) * exp(-abs(p.x) * 3.5);

                float v = disc * _DiskGain + glow * 0.7 + halo * 0.25 + rays * 0.5 + streak * 0.35;
                v *= smoothstep(1.0, 0.7, r);                      // обрезка по краю квада

                // Центр белый, края тёплые
                float3 warm = lerp(float3(1.0, 0.97, 0.9), float3(1.0, 0.55, 0.2), saturate(r * 1.6));
                return float4(warm * _Color.rgb * v * _Intensity * _Fade, 1.0);
            }
            ENDHLSL
        }
    }
}