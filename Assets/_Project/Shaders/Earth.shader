Shader "SolarSys/Earth"
{
    Properties
    {
        _DayMap     ("Day Map (sRGB)", 2D) = "white" {}
        _Brightness ("Sunlight Brightness", Range(0, 3)) = 1.0
        _Ambient    ("Ambient (earthshine)", Range(0, 0.1)) = 0.01
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
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float2 uv          : TEXCOORD1;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _DayMap);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                Light sun = GetMainLight();              // direction = К свету
                float ndl = saturate(dot(N, sun.direction));

                float3 albedo = SAMPLE_TEXTURE2D(_DayMap, sampler_DayMap, i.uv).rgb;
                float3 col = albedo * (sun.color * _Brightness * ndl + _Ambient);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}