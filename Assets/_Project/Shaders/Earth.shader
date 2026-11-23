Shader "SolarSys/Earth"
{
    Properties
    {
        _DayMap     ("Day Map (sRGB)", 2D) = "white" {}
        _Brightness ("Sunlight Brightness", Range(0, 3)) = 1.0
        _Ambient    ("Ambient (earthshine)", Range(0, 0.1)) = 0.01
        _LonOffset  ("Longitude Offset (turns)", Range(-0.5, 0.5)) = 0.0
        [Toggle] _FlipU ("Flip U (mirror)", Float) = 0
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

            TEXTURE2D(_DayMap);
            SAMPLER(sampler_DayMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _DayMap_ST;
                float _Brightness;
                float _Ambient;
                float _LonOffset;
                float _FlipU;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 dirOS       : TEXCOORD0; // направление от центра сферы (объектные координаты)
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dirOS = i.positionOS.xyz;      // сфера с центром в нуле
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 nOS = normalize(i.dirOS);                       // идеальная нормаль сферы
                float3 N = normalize(TransformObjectToWorldNormal(nOS));

                // Долгота / широта по каждому пикселю
                float u = atan2(nOS.z, nOS.x) * (0.5 / PI);
                if (_FlipU > 0.5) u = -u;
                u += _LonOffset;
                float v = asin(clamp(nOS.y, -1.0, 1.0)) / PI + 0.5;

                // Две версии u с разными швами; производные берём от той, где нет скачка
                float u1 = frac(u);
                float u2 = frac(u + 0.5);
                bool useB = fwidth(u2) < fwidth(u1);
                float2 gx = useB ? float2(ddx(u2), ddx(v)) : float2(ddx(u1), ddx(v));
                float2 gy = useB ? float2(ddy(u2), ddy(v)) : float2(ddy(u1), ddy(v));

                float3 albedo = SAMPLE_TEXTURE2D_GRAD(_DayMap, sampler_DayMap, float2(u1, v), gx, gy).rgb;

                Light sun = GetMainLight();
                float ndl = saturate(dot(N, sun.direction));
                float3 col = albedo * (sun.color * _Brightness * ndl + _Ambient);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}