Shader "SolarSys/OrbitLine"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
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
            struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; };
            struct Varyings { float4 positionHCS:SV_POSITION; float4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial) float4 _Color; CBUFFER_END
            Varyings vert(Attributes i){
                Varyings o;
                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color * _Color;
                return o;
            }
            float4 frag(Varyings i):SV_Target { return i.color; }
            ENDHLSL
        }
    }
}