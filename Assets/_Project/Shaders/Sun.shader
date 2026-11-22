Shader "SolarSys/Sun"
{
    Properties
    {
        _CoolColor ("Cool Color", Color) = (0.55, 0.12, 0.02, 1)
        _MidColor  ("Mid Color",  Color) = (1.0, 0.45, 0.05, 1)
        _HotColor  ("Hot Color",  Color) = (1.0, 0.90, 0.55, 1)
        _NoiseScale ("Noise Scale", Float) = 4.0
        _Speed ("Speed", Float) = 0.35
        _Warp ("Domain Warp", Float) = 1.2
        _Contrast ("Contrast", Range(0.5, 3)) = 1.4
        _Granulation ("Granulation", Range(0, 1)) = 0.5
        _CoronaIntensity ("Corona Intensity", Range(0, 5)) = 2.2
        _CoronaPower ("Corona Power", Range(1, 10)) = 2.5
        _Emission ("Emission", Range(0.5, 5)) = 1.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings {
                float4 positionHCS:SV_POSITION;
                float3 normalWS:TEXCOORD0;
                float3 viewDirWS:TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _CoolColor, _MidColor, _HotColor;
                float _NoiseScale, _Speed, _Warp, _Contrast, _Granulation;
                float _CoronaIntensity, _CoronaPower, _Emission;
            CBUFFER_END

            float3 mod289(float3 x){return x-floor(x*(1.0/289.0))*289.0;}
            float4 mod289(float4 x){return x-floor(x*(1.0/289.0))*289.0;}
            float4 permute(float4 x){return mod289(((x*34.0)+1.0)*x);}
            float4 taylorInvSqrt(float4 r){return 1.79284291400159-0.85373472095314*r;}
            float snoise(float3 v){
                const float2 C=float2(1.0/6.0,1.0/3.0); const float4 D=float4(0,0.5,1,2);
                float3 i=floor(v+dot(v,C.yyy)); float3 x0=v-i+dot(i,C.xxx);
                float3 g=step(x0.yzx,x0.xyz); float3 l=1.0-g;
                float3 i1=min(g.xyz,l.zxy); float3 i2=max(g.xyz,l.zxy);
                float3 x1=x0-i1+C.xxx; float3 x2=x0-i2+C.yyy; float3 x3=x0-D.yyy;
                i=mod289(i);
                float4 p=permute(permute(permute(i.z+float4(0,i1.z,i2.z,1))+i.y+float4(0,i1.y,i2.y,1))+i.x+float4(0,i1.x,i2.x,1));
                float n_=0.142857142857; float3 ns=n_*D.wyz-D.xzx;
                float4 j=p-49.0*floor(p*ns.z*ns.z);
                float4 x_=floor(j*ns.z); float4 y_=floor(j-7.0*x_);
                float4 x=x_*ns.x+ns.yyyy; float4 y=y_*ns.x+ns.yyyy; float4 h=1.0-abs(x)-abs(y);
                float4 b0=float4(x.xy,y.xy); float4 b1=float4(x.zw,y.zw);
                float4 s0=floor(b0)*2.0+1.0; float4 s1=floor(b1)*2.0+1.0;
                float4 sh=-step(h,float4(0,0,0,0));
                float4 a0=b0.xzyw+s0.xzyw*sh.xxyy; float4 a1=b1.xzyw+s1.xzyw*sh.zzww;
                float3 p0=float3(a0.xy,h.x); float3 p1=float3(a0.zw,h.y);
                float3 p2=float3(a1.xy,h.z); float3 p3=float3(a1.zw,h.w);
                float4 norm=taylorInvSqrt(float4(dot(p0,p0),dot(p1,p1),dot(p2,p2),dot(p3,p3)));
                p0*=norm.x; p1*=norm.y; p2*=norm.z; p3*=norm.w;
                float4 m=max(0.6-float4(dot(x0,x0),dot(x1,x1),dot(x2,x2),dot(x3,x3)),0.0); m=m*m;
                return 42.0*dot(m*m,float4(dot(p0,x0),dot(p1,x1),dot(p2,x2),dot(p3,x3)));
            }
            float fbm(float3 p, float t){
                float v=0.0, a=0.5;
                for(int i=0;i<4;i++){
                    v += a*snoise(p + t*(0.3+0.25*i)*float3(1.0,0.7,0.4));
                    p *= 2.02; a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes input){
                Varyings o;
                VertexPositionInputs vi = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionHCS = vi.positionCS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewDirWS = _WorldSpaceCameraPos.xyz - vi.positionWS;
                return o;
            }

            float4 frag(Varyings input):SV_Target{
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                float t = _Time.y * _Speed;
                float3 p = n * _NoiseScale;

                float3 q = float3(fbm(p, t), fbm(p+float3(5.2,1.3,0), t*0.8), 0);
                float3 r = float3(fbm(p+_Warp*q, t*0.6), fbm(p+_Warp*q+float3(8.3,2.8,0), t*0.7), 0);
                float f = fbm(p + _Warp*r, t);

                float h = saturate(f*0.5+0.5);
                h = pow(h, _Contrast);
                h = saturate(h + _Granulation*0.5*fbm(p*6.0, t*2.0));

                float3 col = lerp(_CoolColor.rgb, _MidColor.rgb, smoothstep(0.15,0.55,h));
                col = lerp(col, _HotColor.rgb, smoothstep(0.55,0.95,h));

                float ndv = saturate(dot(n,v));
                col *= lerp(0.5, 1.0, pow(ndv, 0.45));

                // --- КОРОНА: дымка + лучи ---
                float fres  = pow(1.0-ndv, _CoronaPower);   // узкий яркий край
                float broad = pow(1.0-ndv, 0.9);            // широкая мягкая дымка
                // угловой шум вокруг оси взгляда -> радиальные лучи
                float3 ref = abs(v.y) < 0.99 ? float3(0,1,0) : float3(1,0,0);
                float3 t1 = normalize(cross(v, ref));
                float3 t2 = cross(v, t1);
                float ang = atan2(dot(n,t2), dot(n,t1));
                float rn = saturate(fbm(float3(ang*2.5, 7.0, 0), t*1.2)*0.5+0.5);
                float streak = pow(rn, 3.0);
                float mask = fres*(0.45 + 1.6*streak) + broad*0.55;
                float3 corona = lerp(_MidColor.rgb, _HotColor.rgb, 0.45) * mask * _CoronaIntensity;

                float pulse = 0.92 + 0.12*sin(t*3.0) + 0.06*sin(t*7.3);
                float3 final = col * _Emission * pulse + corona;
                return float4(final, 1.0);
            }
            ENDHLSL
        }
    }
}