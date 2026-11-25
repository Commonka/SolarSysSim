Shader "SolarSys/Planet"
{
    Properties
    {
        _MainMap ("Day Map (sRGB)", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _BumpStrength ("Relief Strength", Range(0, 3)) = 1.0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _LonOffset ("Longitude Offset", Range(-0.5, 0.5)) = 0.0
        _Ambient ("Ambient", Range(0, 0.2)) = 0.02
        _RimColor ("Rim / Atmo Color", Color) = (0.4, 0.6, 1.0, 1)
        _RimPower ("Rim Power", Range(0, 8)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        ZWrite On
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainMap, _NormalMap;
            fixed4 _Tint, _RimColor;
            float _BumpStrength, _LonOffset, _Ambient, _RimPower, _RimIntensity;
            float3 _SunPositionWS;   // глобально ставит SimulationManager (относительно камеры)

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 n : TEXCOORD1;
                float3 t : TEXCOORD2;
                float3 b : TEXCOORD3;
                float3 world : TEXCOORD4;
                float3 view : TEXCOORD5;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uv.x = frac(o.uv.x + _LonOffset);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.t = UnityObjectToWorldDir(v.tangent.xyz);
                o.b = cross(o.n, o.t) * v.tangent.w;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.world = wp;
                o.view = normalize(_WorldSpaceCameraPos - wp);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.n);

                // рельеф по normal map (если назначена)
                float3 tn = UnpackNormal(tex2D(_NormalMap, i.uv));
                tn = normalize(float3(tn.xy * _BumpStrength, tn.z));
                N = normalize(tn.x * normalize(i.t) + tn.y * normalize(i.b) + tn.z * N);

                float3 L = normalize(_SunPositionWS - i.world);
                float ndl = saturate(dot(N, L));

                float3 day = tex2D(_MainMap, i.uv).rgb * _Tint.rgb;
                float3 col = day * (ndl + _Ambient);

                // тонкий ободок-«атмосфера»
                float rim = pow(1.0 - saturate(dot(N, normalize(i.view))), _RimPower);
                col += _RimColor.rgb * rim * _RimIntensity * ndl;

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}