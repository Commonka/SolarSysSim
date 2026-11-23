Shader "SolarSys/Earth"
{
    Properties
    {
        _DayMap   ("Day Map (sRGB)", 2D)   = "white" {}
        _NightMap ("Night Map (sRGB)", 2D) = "black" {}
        _CloudMap ("Cloud Map", 2D)        = "black" {}
        _NormalMap ("Relief Normal Map", 2D) = "bump" {}   // NEW: рельеф
        _BumpStrength ("Relief Strength", Range(0, 2)) = 1.0
        _BumpNear ("Relief Fade Near (units)", Float) = 15
        _BumpFar  ("Relief Fade Far (units)", Float) = 80
        _CloudThreshold ("Cloud Threshold", Range(0, 1)) = 0.35
        _CloudBrightness ("Cloud Brightness", Range(0, 2)) = 1.0
        _SpecPower ("Ocean Specular Power", Range(1, 256)) = 60
        _SpecIntensity ("Ocean Specular", Range(0, 1)) = 0.6
        _AtmoColor ("Atmosphere Color", Color) = (0.35, 0.6, 1.0, 1.0)
        _AtmoIntensity ("Atmosphere Intensity", Range(0, 2)) = 0.5
        _AtmoPower ("Atmosphere Power", Range(1, 8)) = 3.0
        _Ambient ("Ambient", Range(0, 0.1)) = 0.015
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
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _DayMap_ST, _NightMap_ST, _CloudMap_ST, _NormalMap_ST;
                float _BumpStrength, _BumpNear, _BumpFar;
                float _CloudThreshold, _CloudBrightness;
                float _SpecPower, _SpecIntensity;
                float4 _AtmoColor;
                float _AtmoIntensity, _AtmoPower;
                float _Ambient, _LonOffset;
            CBUFFER_END

            float3 _SunPositionWS;

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
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);
                float3 nOS = normalize(i.dirOS);

                // Сферические UV + анти-шов
                float u = atan2(nOS.z, nOS.x) * (0.5 / PI) + _LonOffset;
                float v = asin(clamp(nOS.y, -1.0, 1.0)) / PI + 0.5;
                float u1 = frac(u);
                float u2 = frac(u + 0.5);
                bool useB = fwidth(u2) < fwidth(u1);
                float2 gx = useB ? float2(ddx(u2), ddx(v)) : float2(ddx(u1), ddx(v));
                float2 gy = useB ? float2(ddy(u2), ddy(v)) : float2(ddy(u1), ddy(v));

                float3 albedo = SAMPLE_TEXTURE2D_GRAD(_DayMap, sampler_DayMap, float2(u1, v), gx, gy).rgb;

                // ===== NEW: рельеф по normal map =====
                float3 tsn = SAMPLE_TEXTURE2D_GRAD(_NormalMap, sampler_NormalMap, float2(u1, v), gx, gy).rgb * 2.0 - 1.0;
                float lon = atan2(nOS.z, nOS.x);
                float lat = asin(clamp(nOS.y, -1.0, 1.0));
                float3 T_os = float3(-sin(lon), 0.0, cos(lon));                 // восток
                float3 B_os = float3(-sin(lat)*cos(lon), cos(lat), -sin(lat)*sin(lon)); // север
                float3 T = TransformObjectToWorldDir(T_os);
                float3 B = TransformObjectToWorldDir(B_os);
                float dist = length(i.viewDirWS);
                float fade = (1.0 - smoothstep(_BumpNear, _BumpFar, dist)) * _BumpStrength;
                float3 Nrelief = normalize(T * tsn.x + B * tsn.y + N * saturate(tsn.z));
                N = normalize(lerp(N, Nrelief, saturate(fade)));
                // ===== конец блока рельефа =====

                // Облака
                float t = _Time.y * 0.002;
                float uc = useB ? frac(u + 0.5 + t) : frac(u + t);
                float cloudRaw = SAMPLE_TEXTURE2D_GRAD(_CloudMap, sampler_CloudMap, float2(uc, v), gx, gy).r;
                float cloud = smoothstep(_CloudThreshold, _CloudThreshold + 0.25, cloudRaw);

                // Свет от Солнца
                float3 sunDir = normalize(_SunPositionWS - i.positionWS);
                float ndl = dot(N, sunDir);
                float day = smoothstep(-0.15, 0.25, ndl);

                float3 nightLights = SAMPLE_TEXTURE2D_GRAD(_NightMap, sampler_NightMap, float2(u1, v), gx, gy).rgb;
                float3 surface = albedo * (day + _Ambient) + nightLights * (1.0 - day) * 1.5;

                surface = lerp(surface, float3(1,1,1) * (day * _CloudBrightness + _Ambient), cloud * 0.9);

                float waterMask = saturate((albedo.b - albedo.r) * 2.0 + 2.0 * 0.1);
                float3 H = normalize(sunDir + V);
                surface += pow(saturate(dot(N, H)), _SpecPower) * waterMask * _SpecIntensity * day;

                surface = min(surface, 1.0);

                float rim = pow(1.0 - saturate(dot(N, V)), _AtmoPower);
                float term = exp(-pow(ndl * 3.5, 2.0));
                float3 atmo = _AtmoColor.rgb * rim * _AtmoIntensity * (1.0 + term * 1.5);

                return float4(surface + atmo, 1.0);
            }
            ENDHLSL
        }
    }
}