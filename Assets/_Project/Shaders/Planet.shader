Shader "SolarSys/Planet"
{
    Properties
    {
        _DayMap ("Surface / Bands Map", 2D) = "white" {}
        _NormalMap ("Relief Normal", 2D) = "bump" {}
        _CloudMap ("Cloud Mask", 2D) = "black" {}
        _NightMap ("Night Lights", 2D) = "black" {}

        _Brightness ("Sunlight", Range(0,3)) = 1.0
        _Ambient ("Ambient", Range(0,0.1)) = 0.01
        _ReliefStrength ("Relief", Range(0,2)) = 0.0

        _CloudCoverage ("Cloud Coverage", Range(0,1)) = 1.0
        _CloudOpacity ("Cloud Opacity", Range(0,1)) = 0.9
        _CloudSpeed ("Cloud Drift", Float) = 0.0005
        _NightIntensity ("Night Lights", Range(0,3)) = 0.0

        _AtmoColor ("Atmosphere Color", Color) = (0.5,0.6,1,1)
        _AtmoIntensity ("Atmosphere", Range(0,3)) = 0.0
        _AtmoPower ("Atmo Falloff", Range(1,8)) = 3.0
        _SpecStrength ("Specular", Range(0,2)) = 0.0
        _SpecPower ("Shininess", Range(8,256)) = 64

        [Toggle] _GasGiant ("Gas Giant Mode", Float) = 0
        _BandSpeed ("Band Drift", Float) = 0.002
        _BandWarp ("Band Turbulence", Range(0,0.1)) = 0.02

        _LonOffset ("Lon Offset", Range(-0.5,0.5)) = 0
        [Toggle] _FlipU ("Flip U", Float) = 0
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DayMap); SAMPLER(sampler_DayMap);
            TEXTURE2D(_NormalMap); TEXTURE2D(_CloudMap); TEXTURE2D(_NightMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _DayMap_ST, _AtmoColor;
                float _Brightness,_Ambient,_ReliefStrength;
                float _CloudCoverage,_CloudOpacity,_CloudSpeed,_NightIntensity;
                float _AtmoIntensity,_AtmoPower,_SpecStrength,_SpecPower;
                float _GasGiant,_BandSpeed,_BandWarp,_LonOffset,_FlipU;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 dirOS:TEXCOORD0; float3 positionWS:TEXCOORD1; };

            Varyings vert(Attributes i){
                Varyings o;
                VertexPositionInputs vi = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionHCS = vi.positionCS; o.positionWS = vi.positionWS;
                o.dirOS = i.positionOS.xyz; return o;
            }

            float hash21(float2 p){ p = frac(p*float2(123.34,456.21)); p += dot(p,p+45.32); return frac(p.x*p.y); }
            float vnoise(float2 p){
                float2 i = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),
                            lerp(hash21(i+float2(0,1)),hash21(i+float2(1,1)),f.x), f.y);
            }
            float fbm2(float2 p){ float v=0,a=0.5; for(int k=0;k<3;k++){ v+=a*vnoise(p); p*=2.03; a*=0.5; } return v; }

            float4 frag(Varyings i):SV_Target
            {
                float3 nOS = normalize(i.dirOS);
                float u = atan2(nOS.z, nOS.x)*(0.5/PI);
                if(_FlipU>0.5) u=-u;
                u += _LonOffset;
                float v = asin(clamp(nOS.y,-1,1))/PI + 0.5;

                // Gas giants: дифференциальное вращение + турбулентность полос
                if(_GasGiant>0.5){
                    float t = _Time.y*_BandSpeed;
                    u += t*(0.6+0.4*cos(v*6.2831));
                    v += (fbm2(float2(u*6.0, v*12.0)+t*0.2)-0.5)*_BandWarp;
                    u += (fbm2(float2(v*20.0, u*3.0)-t*0.1)-0.5)*_BandWarp*0.5;
                }

                // бесшовные производные
                float u1=frac(u), u2=frac(u+0.5);
                bool useB = fwidth(u2)<fwidth(u1);
                float2 gx = useB?float2(ddx(u2),ddx(v)):float2(ddx(u1),ddx(v));
                float2 gy = useB?float2(ddy(u2),ddy(v)):float2(ddy(u1),ddy(v));
                float2 uv = float2(u1,v);

                float3 day   = SAMPLE_TEXTURE2D_GRAD(_DayMap,sampler_DayMap,uv,gx,gy).rgb;
                float3 night = SAMPLE_TEXTURE2D_GRAD(_NightMap,sampler_DayMap,uv,gx,gy).rgb;
                float  cl    = SAMPLE_TEXTURE2D_GRAD(_CloudMap,sampler_DayMap,uv+float2(_Time.y*_CloudSpeed,0),gx,gy).r;
                float3 nrm   = SAMPLE_TEXTURE2D_GRAD(_NormalMap,sampler_DayMap,uv,gx,gy).rgb;

                float c = smoothstep(_CloudCoverage,_CloudCoverage+0.25,cl)*_CloudOpacity;

                float3 nm = nrm*2-1; nm.xy *= _ReliefStrength;
                float3 up = abs(nOS.y)<0.999?float3(0,1,0):float3(1,0,0);
                float3 T = normalize(cross(nOS,up)); float3 B = cross(T,nOS);
                float3 Np = normalize(T*nm.x + B*nm.y + nOS*nm.z);
                float3 Nw = normalize(TransformObjectToWorldNormal(Np));
                float3 Ngw= normalize(TransformObjectToWorldNormal(nOS));
                float3 V  = normalize(GetWorldSpaceViewDir(i.positionWS));

                Light sun = GetMainLight();
                float ndl = dot(Nw,sun.direction);
                float3 surface = lerp(day,float3(1,1,1),c);
                float3 col = surface*(sun.color*_Brightness*saturate(ndl)+_Ambient);

                float nightF = 1-smoothstep(-0.15,0.05,ndl);
                col += night*_NightIntensity*nightF*(1-c*0.8);

                float ocean = saturate((day.b-day.r)*2.5)*(1-c);
                float3 H = normalize(sun.direction+V);
                float dayF = smoothstep(-0.08,0.12,ndl);
                col += sun.color*pow(saturate(dot(Nw,H)),_SpecPower)*ocean*_SpecStrength*dayF;

                float fres = pow(1-saturate(dot(Ngw,V)),_AtmoPower);
                float atmoDay = smoothstep(-0.25,0.35,dot(Ngw,sun.direction));
                col += _AtmoColor.rgb*fres*_AtmoIntensity*(0.15+0.85*atmoDay);
                return float4(col,1);
            }
            ENDHLSL
        }
    }
}