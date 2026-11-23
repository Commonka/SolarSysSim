Shader "SolarSys/LensFlare"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Shape ("Shape (0 disc, 1 ring, 2 hexagon, 3 glow)", Float) = 0
        _Intensity ("Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Shape, _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

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
                float v = 0;
                if (_Shape < 0.5)        v = smoothstep(1.0, 0.8, r) * 0.6;                       // диск
                else if (_Shape < 1.5)   v = smoothstep(0.3, 0.0, abs(r - 0.8)) * 0.9 + smoothstep(1.0, 0.0, r) * 0.08; // кольцо
                else if (_Shape < 2.5)                                                            // шестиугольник диафрагмы
                {
                    float a = atan2(p.y, p.x);
                    float k = cos(3.14159265 / 6.0) / cos(fmod(abs(a), 3.14159265 / 3.0) - 3.14159265 / 6.0);
                    float h = r / k;
                    v = smoothstep(1.0, 0.7, h) * (0.35 + 0.65 * smoothstep(0.5, 1.0, h));
                }
                else                     v = pow(saturate(1.0 - r), 2.5);                         // мягкое пятно
                v *= smoothstep(1.0, 0.95, r);
                return float4(_Color.rgb * v * _Intensity, 1.0);
            }
            ENDHLSL
        }
    }
}