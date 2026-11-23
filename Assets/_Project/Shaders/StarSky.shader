Shader "SolarSys/StarSky"
{
    Properties
    {
        _MainTex ("Sky (equirectangular)", 2D) = "black" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Exposure ("Exposure", Range(0, 4)) = 1.0
        _Power ("Contrast Power", Range(0.5, 3)) = 1.3
        _Rotation ("Rotation XYZ (deg)", Vector) = (60.2, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
                float _Exposure, _Power;
                float4 _Rotation;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            float3 RotateEuler(float3 p, float3 deg)
            {
                float3 e = radians(deg);
                float cx = cos(e.x), sx = sin(e.x);
                float cy = cos(e.y), sy = sin(e.y);
                float cz = cos(e.z), sz = sin(e.z);
                p = float3(p.x, cx * p.y - sx * p.z, sx * p.y + cx * p.z);   // X
                p = float3(cy * p.x + sy * p.z, p.y, -sy * p.x + cy * p.z);  // Y
                p = float3(cz * p.x - sz * p.y, sz * p.x + cz * p.y, p.z);   // Z
                return p;
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = RotateEuler(i.positionOS.xyz, _Rotation.xyz);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float u = atan2(d.z, d.x) * (0.5 / PI) + 0.5;
                float v = asin(clamp(d.y, -1.0, 1.0)) / PI + 0.5;

                // Тот же приём анти-шва, что в Earth.shader
                float dx = ddx(u), dy = ddy(u);
                dx = (abs(dx) > 0.5) ? 0.0 : dx;
                dy = (abs(dy) > 0.5) ? 0.0 : dy;

                float3 col = SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex,
                                float2(frac(u), v), float2(dx, ddx(v)), float2(dy, ddy(v))).rgb;
                col = pow(max(col, 0.0), _Power) * _Tint.rgb * _Exposure;
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}