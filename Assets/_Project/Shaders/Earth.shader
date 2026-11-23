Shader "SolarSys/Earth"
{
    Properties
    {
        _DayMap   ("Day Map (sRGB)", 2D)   = "white" {}
        _NightMap ("Night Map (sRGB)", 2D) = "black" {}
        _CloudMap ("Cloud Map", 2D)        = "white" {}
        _SpecPower ("Ocean Specular Power", Range(1, 256)) = 60
        _SpecIntensity ("Ocean Specular", Range(0, 1)) = 0.6
        _AtmoColor ("Atmosphere Color", Color) = (0.35, 0.6, 1.0, 1.0)
        _AtmoIntensity ("Atmosphere Intensity", Range(0, 2)) = 0.8
        _AtmoPower ("Atmosphere Power", Range(1, 8)) = 2.5
        _CloudBrightness ("Cloud Brightness", Range(0, 3)) = 1.2
        _Ambient ("Ambient", Range(0, 0.1)) = 0.015
        _RotationSpeed ("Rotation (deg/day)", Float) = 360
        _LonOffset ("Longitude Offset", Range(-0.5, 0.5)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DayMap);   SAMPLER(sampler_DayMap);
            TEXTURE2D(_NightMap); SAMPLER(sampler_NightMap);
            TEXTURE2D(_CloudMap); SAMPLER(sampler_CloudMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _DayMap_ST, _NightMap_ST, _CloudMap_ST;
                float _SpecPower, _SpecIntensity;
                float4 _AtmoColor;
                float _AtmoIntensity, _AtmoPower;
                float _CloudBrightness, _Ambient;
                float _RotationSpeed, _LonOffset;
            CBUFFER_END

            float3 _SunPositionWS;
            float  _SimDays; // опционально, если захочешь анимировать вращение от симуляции

            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings {
                float4 positionHCS:SV_POSITION;
                float3 dirOS:TEXCOORD0;
                float3 positionWS:TEXCOORD1;
                float3 normalWS:TEXCOORD2;
                float3 viewDirWS:TEXCOORD3;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs vi = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionHCS = vi.positionCS;
                o.positionWS  = vi.positionWS;
                o.dirOS = i.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(normalize(i.positionOS.xyz));
                o.viewDirWS = _WorldSpaceCameraPos.xyz - vi.positionWS;
                return o;
            }

            float4 frag(Varyings i):SV_Target
            {
                float3 nOS = normalize(i.dirOS);
                float3 N   = normalize(i.normalWS);
                float3 V   = normalize(i.viewDirWS);

                // Сферические UV с анти-шовной техникой (как было)
                float u = atan2(nOS.z, nOS.x) * (0.5 / PI) + _LonOffset;
                float v = asin(clamp(nOS.y, -1.0, 1.0)) / PI + 0.5;
                float u1 = frac(u);
                float u2 = frac(u + 0.5);
                bool useB = fwidth(u2) < fwidth(u1);
                float2 gx = useB ? float2(ddx(u2), ddx(v)) : float2(ddx(u1), ddx(v));
                float2 gy = useB ? float2(ddy(u2), ddy(v)) : float2(ddy(u1), ddy(v));

                float3 albedo = SAMPLE_TEXTURE2D_GRAD(_DayMap, sampler_DayMap, float2(u1, v), gx, gy).rgb;

                // Облака с независимым UV (вращаются чуть быстрее поверхности)
                float uCloud = frac(u + _Time.y * 0.002);
                float cloud = SAMPLE_TEXTURE2D_GRAD(_CloudMap, sampler_CloudMap,
                                    float2(uCloud, v), gx, gy).r;

                // Освещение от реального Солнца (per-pixel)
                float3 sunDir = normalize(_SunPositionWS - i.positionWS);
                float ndl = dot(N, sunDir);

                // Мягкий терминатор (атмосфера Земли "размывает" границу)
                float day = smoothstep(-0.15, 0.25, ndl);

                // Ночная сторона — ночные огни (emission)
                float3 nightLights = SAMPLE_TEXTURE2D_GRAD(_NightMap, sampler_NightMap,
                                       float2(u1, v), gx, gy).rgb;
                float3 night = nightLights * (1.0 - day) * 1.8;

                // Поверхность: day*albedo + ambient + nightLights
                float3 surface = albedo * (day + _Ambient) + night;

                // Облака: на дневной стороне светлые, на ночной — тёмные силуэты
                float3 clouds = float3(cloud, cloud, cloud);
                float cloudLit = day * _CloudBrightness + _Ambient;
                surface = lerp(surface, clouds * cloudLit, cloud * 0.85);

                // Specular океана: маска по "синеве" daymap + fresnel
                float waterMask = saturate((albedo.b - albedo.r) * 2.0 + 0.2);
                float3 H = normalize(sunDir + V);
                float spec = pow(saturate(dot(N, H)), _SpecPower) * waterMask * _SpecIntensity * day;
                surface += spec;

                // Атмосферный ободок (Fresnel) — голубая дымка на краю диска
                float rim = 1.0 - saturate(dot(N, V));
                rim = pow(rim, _AtmoPower);
                float3 atmo = _AtmoColor.rgb * rim * _AtmoIntensity;
                // Атмосфера ярче на терминаторе (закатное кольцо)
                float term = exp(-pow((ndl - 0.0) * 3.5, 2.0));
                atmo *= (1.0 + term * 1.5);

                float3 final = surface + atmo;
                return float4(final, 1.0);
            }
            ENDHLSL
        }
    }
}