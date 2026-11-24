Shader "SolarSys/Asteroids"
{
    Properties { _Tint ("Tint", Color) = (0.55,0.5,0.45,1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Ast { float3 P; float a; float3 Q; float e; float phase0; float periodDays; float radiusKm; float seed; };
            StructuredBuffer<Ast> _Asteroids;

            float4 _Tint;
            float3 _SunRel;       // Солнце относительно камеры, ед. сцены
            float _SimDays, _SizeExag, _MinPixels, _FarAlpha, _Brightness;

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; uint id : SV_InstanceID; };
            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 sunVS : TEXCOORD1;
                float3 albedo : TEXCOORD2;
                float detail : TEXCOORD3;
                float alpha : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Ast s = _Asteroids[v.id];

                // Уравнение Кеплера: M -> E (Ньютон)
                float M = 6.2831853 * frac(s.phase0 + _SimDays / s.periodDays);
                float E = M + s.e * sin(M);
                [unroll] for (int k = 0; k < 5; k++)
                    E -= (E - s.e * sin(E) - M) / (1.0 - s.e * cos(E));

                float3 rv = s.P * (s.a * (cos(E) - s.e)) + s.Q * (s.a * sqrt(1.0 - s.e * s.e) * sin(E));
                float3 posRel = _SunRel + rv;                       // камера в нуле

                // Размер: реальный, но не меньше _MinPixels
                float dist = max(length(posRel), 1e-3);
                float pxWorld = dist * 2.0 / (unity_CameraProjection._m11 * _ScreenParams.y);
                float realR = s.radiusKm * _SizeExag * 0.001;
                float minR = 0.5 * _MinPixels * pxWorld;
                float R = max(realR, minR);
                float ratio = realR / minR;

                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 wp = posRel + (right * v.uv.x + up * v.uv.y) * R;

                Varyings o;
                o.pos = TransformWorldToHClip(wp);
                o.uv = v.uv;
                o.sunVS = mul((float3x3)UNITY_MATRIX_V, -normalize(rv));   // от астероида к Солнцу
                o.detail = saturate(ratio);
                o.alpha = max(saturate(ratio * ratio), _FarAlpha);
                o.albedo = _Tint.rgb * lerp(0.4, 1.1, s.seed)
                         * lerp(float3(1,1,1), float3(1.1,0.95,0.8), frac(s.seed * 7.31));
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float rr = length(i.uv);
                if (rr > 1.0) discard;

                float3 n = float3(i.uv, sqrt(saturate(1.0 - rr * rr)));    // нормаль сферы в view space
                float lam = saturate(dot(n, i.sunVS));
                float avg = 0.5 * (1.0 + i.sunVS.z);                       // средняя освещённость диска (фаза)
                float lit = lerp(avg, lam, i.detail);

                float edge = lerp(0.5, 0.05, i.detail);
                float a = smoothstep(1.0, 1.0 - edge, rr) * i.alpha;
                float3 col = i.albedo * (lit * _Brightness + 0.01);
                return float4(col, a);
            }
            ENDHLSL
        }
    }
}