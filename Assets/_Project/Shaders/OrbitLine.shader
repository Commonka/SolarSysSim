Shader "SolarSys/OrbitLine"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Dashes ("Dashes per orbit", Float) = 180
        _Duty ("Dash length (0..1)", Range(0.05, 0.95)) = 0.45
        _Soft ("Edge softness", Range(0.05, 1)) = 0.6
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

            struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Dashes, _Duty, _Soft;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color * _Color;
                o.uv = i.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                // поперёк линии: мягкий круглый профиль
                float across = 1.0 - abs(i.uv.y * 2.0 - 1.0);          // 0 край .. 1 центр
                float w = smoothstep(0.0, _Soft, across);

                // вдоль линии: штрихи со скруглёнными концами
                float t = frac(i.uv.x * _Dashes);
                float cap = 0.5 * (1.0 - _Duty);                       // зона скругления
                float d = smoothstep(0.0, max(cap, 0.02), t)
                        * smoothstep(0.0, max(cap, 0.02), _Duty - t);
                d = saturate(d * 3.0);

                float4 c = i.color;
                c.a *= w * d;
                return c;
            }
            ENDHLSL
        }
    }
}