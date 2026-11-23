Shader "SolarSys/Atmosphere"
{
    Properties
    {
        _PlanetRadius ("Planet Radius (units)", Float) = 6.371
        _AtmoRadius ("Atmosphere Radius (units)", Float) = 6.451
        _HeightR ("Rayleigh Scale Height", Float) = 0.010
        _HeightM ("Mie Scale Height", Float) = 0.0025
        _SunIntensity ("Sun Intensity", Float) = 8
        _RayleighScale ("Rayleigh Scale", Float) = 1
        _MieScale ("Mie Scale", Float) = 0.4
        _MieG ("Mie Anisotropy", Range(0, 0.99)) = 0.76
        _Extinction ("Extinction", Range(0, 1)) = 0.8
        _SampleScale ("Sample Scale", Float) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _PlanetRadius, _AtmoRadius, _HeightR, _HeightM;
                float _SunIntensity, _RayleighScale, _MieScale, _MieG;
                float _Extinction, _SampleScale;
            CBUFFER_END

            float3 _SunPositionWS;

            #define SAMPLES 24
            #define LSAMPLES 6

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            // Пересечение луча со сферой с центром в начале координат.
            // oc = начало луча относительно центра. Считаем через перпендикуляр,
            // а не через oc*oc - R*R: на больших расстояниях float иначе разваливается.
            float2 RaySphere(float3 oc, float3 D, float R)
            {
                float tm = -dot(oc, D);
                float3 perp = oc + D * tm;
                float d2 = R * R - dot(perp, perp);
                if (d2 <= 0.0) return float2(1.0, -1.0);   // промах
                float s = sqrt(d2);
                return float2(tm - s, tm + s);
            }

            float Asinh2(float x) { return sign(x) * log(abs(x) + sqrt(x * x + 1.0)); }

            float4 frag(Varyings i) : SV_Target
            {
                float3 O = _WorldSpaceCameraPos;
                float3 D = normalize(i.positionWS - O);
                float3 C = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float3 oc = O - C;

                float2 hitA = RaySphere(oc, D, _AtmoRadius);
                if (hitA.y <= max(hitA.x, 0.0)) return 0;

                float tStart = max(hitA.x, 0.0);
                float tEnd = hitA.y;

                // Планета обрывает луч
                float2 hitP = RaySphere(oc, D, _PlanetRadius);
                if (hitP.y > hitP.x && hitP.x > 0.0) tEnd = min(tEnd, hitP.x);

                // Другие объекты (Луна и т.д.) обрывают луч по буферу глубины
                float2 uv = i.positionCS.xy / _ScaledScreenParams.xy;
                float rawD = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool hasSurf = rawD > 1e-6;
                #else
                    bool hasSurf = rawD < 1.0 - 1e-6;
                #endif
                if (hasSurf)
                {
                    float3 wp = ComputeWorldSpacePosition(uv, rawD, UNITY_MATRIX_I_VP);
                    tEnd = min(tEnd, length(wp - O));
                }
                if (tEnd <= tStart) return 0;

                // Сэмплы сгущаются у точки касания (там плотность максимальна): s = s0*sinh(w)
                float tm = -dot(oc, D);
                float3 perp = oc + D * tm;
                float s0 = max(_SampleScale, 1e-3);
                float wa = Asinh2((tStart - tm) / s0);
                float wb = Asinh2((tEnd - tm) / s0);
                float dw = (wb - wa) / SAMPLES;

                float3 L = normalize(_SunPositionWS - C);
                float3 betaR = float3(5.5, 13.0, 22.4) * _RayleighScale;   // 1/единицу (1 ед. = 1000 км)
                float betaM = 21.0 * _MieScale;

                float odR = 0.0, odM = 0.0;
                float3 sumR = 0.0, sumM = 0.0;

                [loop]
                for (int k = 0; k < SAMPLES; k++)
                {
                    float w = wa + (k + 0.5) * dw;
                    float ew = exp(w);
                    float s = 0.5 * (ew - 1.0 / ew) * s0;
                    float ds = 0.5 * (ew + 1.0 / ew) * s0 * dw;
                    float3 rel = perp + D * s;                  // позиция относительно центра планеты

                    float h = max(length(rel) - _PlanetRadius, 0.0);
                    float dR = exp(-h / _HeightR) * ds;
                    float dM = exp(-h / _HeightM) * ds;
                    odR += dR;
                    odM += dM;

                    // Точка в тени планеты света не получает
                    float2 hl = RaySphere(rel, L, _PlanetRadius);
                    if (hl.x > 0.0 && hl.y > hl.x) continue;

                    float lt = max(RaySphere(rel, L, _AtmoRadius).y, 0.0);
                    float lds = lt / LSAMPLES;
                    float lR = 0.0, lM = 0.0;
                    for (int j = 0; j < LSAMPLES; j++)
                    {
                        float3 q = rel + L * ((j + 0.5) * lds);
                        float hq = max(length(q) - _PlanetRadius, 0.0);
                        lR += exp(-hq / _HeightR) * lds;
                        lM += exp(-hq / _HeightM) * lds;
                    }
                    float3 att = exp(-(betaR * (odR + lR) + betaM * 1.1 * (odM + lM)));
                    sumR += att * dR;
                    sumM += att * dM;
                }

                float mu = dot(D, L);
                float g = _MieG;
                float phaseR = 3.0 / (16.0 * PI) * (1.0 + mu * mu);
                float phaseM = 3.0 / (8.0 * PI) * ((1.0 - g * g) * (1.0 + mu * mu))
                             / ((2.0 + g * g) * pow(max(1.0 + g * g - 2.0 * g * mu, 1e-3), 1.5));

                float3 col = (sumR * betaR * phaseR + sumM * betaM * phaseM) * _SunIntensity;

                // Атмосфера ещё и приглушает то, что за ней (звёзды у лимба, поверхность)
                float T = exp(-(betaR.g * odR + betaM * 1.1 * odM));
                return float4(col, saturate(1.0 - T) * _Extinction);
            }
            ENDHLSL
        }
    }
}