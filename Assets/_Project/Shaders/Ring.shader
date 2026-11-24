Shader "SolarSys/Ring"
{
    Properties
    {
        _RingMap ("Ring Radial Strip", 2D) = "white" {}
        [Toggle] _UseTex ("Use Texture", Float) = 0
        _Color ("Tint", Color) = (0.85,0.78,0.6,1)
        _Opacity ("Opacity", Range(0,1)) = 0.9
        _Brightness ("Sunlight", Range(0,3)) = 1.0
        _Ambient ("Ambient", Range(0,0.2)) = 0.05
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
            TEXTURE2D(_RingMap); SAMPLER(sampler_RingMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _RingMap_ST, _Color;
                float _UseTex,_Opacity,_Brightness,_Ambient;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; };
            Varyings vert(Attributes i){
                Varyings o;
                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = i.uv; return o;
            }
            float hash11(float p){ p=frac(p*0.1031); p*=p+33.33; return frac(p*p); }
            float4 frag(Varyings i):SV_Target
            {
                float r = i.uv.y; // 0 inner .. 1 outer
                float alpha;
                float3 alb;
                if(_UseTex>0.5){
                    float4 t = SAMPLE_TEXTURE2D(_RingMap,sampler_RingMap,float2(r,0.5));
                    alb = t.rgb*_Color.rgb; alpha = t.a;
                } else {
                    float bands = 0.55+0.45*sin(r*90.0);
                    float gaps  = smoothstep(0.25,0.6,hash11(floor(r*40.0)));
                    alpha = bands*gaps;
                    alb = _Color.rgb*(0.7+0.3*bands);
                }
                alpha *= smoothstep(0.0,0.04,r)*smoothstep(1.0,0.96,r)*_Opacity;
                Light sun = GetMainLight();
                float ndl = abs(dot(normalize(i.normalWS),sun.direction)); // двустороннее
                float3 col = alb*(sun.color*_Brightness*ndl+_Ambient);
                return float4(col,alpha);
            }
            ENDHLSL
        }
    }
}