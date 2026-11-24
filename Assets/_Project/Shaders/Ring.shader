Shader "SolarSys/Ring"
{
    Properties
    {
        _Color ("Tint", Color) = (0.85,0.78,0.62,1)
        _Opacity ("Opacity", Range(0,1)) = 0.95
        _Brightness ("Sunlight", Range(0,3)) = 1.2
        _Ambient ("Ambient", Range(0,0.2)) = 0.03
        _InnerKm ("Inner radius km", Float) = 74490
        _OuterKm ("Outer radius km", Float) = 136774
        _CellsR ("Cells radial", Float) = 1200
        _CellsA ("Cells angular", Float) = 9000

        _PlanetRadiusKm ("Planet radius km", Float) = 58232
        _ShadowSoft ("Shadow softness", Range(0,0.2)) = 0.03

        // Полосы плотности: (внутр. радиус км, внешн. радиус км, плотность 0..1, мягкость края км)
        _B0 ("Band 0", Vector) = (74490, 91983, 0.22, 400)
        _B1 ("Band 1", Vector) = (91983, 117516, 0.95, 400)
        _B2 ("Band 2", Vector) = (117516, 122170, 0.07, 300)
        _B3 ("Band 3", Vector) = (122170, 133500, 0.65, 300)
        _B4 ("Band 4", Vector) = (133680, 136774, 0.65, 300)
        _B5 ("Band 5", Vector) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Opacity, _Brightness, _Ambient;
                float _InnerKm, _OuterKm, _CellsR, _CellsA;
                float _PlanetRadiusKm, _ShadowSoft;
                float4 _B0, _B1, _B2, _B3, _B4, _B5;
            CBUFFER_END

            // Выставляется глобально из SimulationManager (мировые ед., 1 ед. = 1000 км)
            float3 _SunPositionWS;

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings
            {
                float4 positionHCS:SV_POSITION;
                float3 normalWS:TEXCOORD0;
                float2 uv:TEXCOORD1;
                float3 positionWS:TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = i.uv;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float band(float a, float b, float v, float e)
            {
                return smoothstep(a - e, a + e, v) * (1.0 - smoothstep(b - e, b + e, v));
            }

            float Band(float4 b, float km)
            {
                return band(b.x, b.y, km, max(b.w, 1e-3)) * b.z;
            }

            // Оптическая плотность по радиусу (км)
            float Profile(float km)
            {
                float d = Band(_B0, km) + Band(_B1, km) + Band(_B2, km)
                        + Band(_B3, km) + Band(_B4, km) + Band(_B5, km);
                d *= 0.8 + 0.2 * sin(km * 0.004) * sin(km * 0.00131 + 1.7);
                return saturate(d);
            }

            // 0 = полная тень, 1 = свет. P, C, S в мировых единицах, R — радиус планеты в тех же единицах
            float PlanetShadow(float3 P, float3 C, float3 S, float R, float soft)
            {
                float3 L = normalize(S - P);
                float3 oc = C - P;
                float tca = dot(oc, L);
                if (tca <= 0.0) return 1.0;                          // планета не между точкой и Солнцем
                float d = sqrt(max(dot(oc, oc) - tca * tca, 0.0));   // расстояние луча до центра планеты
                return smoothstep(R * (1.0 - soft), R * (1.0 + soft), d);
            }

            float4 frag(Varyings i) : SV_Target
            {
                float u = i.uv.x;          // угол 0..1
                float r = i.uv.y;          // 0 внутр .. 1 внешн
                float km = lerp(_InnerKm, _OuterKm, r);
                float dens = Profile(km);

                float2 g = float2(r * _CellsR, u * _CellsA);
                float2 id = floor(g);
                float2 f = frac(g);

                float h1 = hash21(id);
                float h2 = hash21(id + 17.1);
                float h3 = hash21(id + 31.7);
                float h4 = hash21(id + 5.3);

                float2 c = 0.3 + 0.4 * float2(h2, h3);
                float rad = lerp(0.18, 0.45, h1 * h1);
                float particle = smoothstep(rad, rad * 0.55, length(f - c));
                particle *= step(h4, dens);

                // дальше пикселя ячейка -> усредняем в оптическую плотность
                float px = max(fwidth(g.x), fwidth(g.y));
                float fade = 1.0 - smoothstep(0.35, 1.0, px);
                float cover = lerp(dens * 0.45, particle, fade);

                float alpha = cover * _Opacity;
                alpha *= smoothstep(0.0, 0.01, r) * smoothstep(1.0, 0.99, r);

                // ледяные глыбы: яркость и лёгкий оттенок на частицу
                float shade = lerp(0.55, 1.25, h1);
                float3 alb = _Color.rgb * shade * lerp(float3(1,1,1), float3(1.0,0.92,0.8), h3 * 0.5);
                alb = lerp(_Color.rgb * 0.85, alb, fade);

                // Освещение + тень планеты
                Light sun = GetMainLight();
                float ndl = abs(dot(normalize(i.normalWS), sun.direction));

                float3 C = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float R = _PlanetRadiusKm * 0.001;
                float lit = PlanetShadow(i.positionWS, C, _SunPositionWS, R, _ShadowSoft);

                float3 col = alb * (sun.color * _Brightness * max(ndl, 0.25) * lit + _Ambient);
                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
}