// Shallow fountain water: transparent, tinted, glossy, with two scrolling ripple layers that
// perturb the normal. Opaque things below (coins, crust) stay visible through it.
Shader "WE/Water"
{
    Properties
    {
        _Color ("Tint", Color) = (0.45, 0.75, 0.85, 1)
        _MainTex ("Ripples", 2D) = "white" {}
        _Alpha ("Alpha", Range(0,1)) = 0.16
        _Speed ("Scroll Speed", Float) = 0.05
        _Bump ("Ripple Strength", Float) = 0.35
        _Glossiness ("Smoothness", Range(0,1)) = 0.93
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        fixed4 _Color;
        half _Alpha, _Speed, _Bump, _Glossiness;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 viewDir;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
        }

        float H(float2 uv)
        {
            float2 a = uv * 0.45 + float2(_Time.y * _Speed, _Time.y * _Speed * 0.6);
            float2 b = uv * 0.7 + float2(-_Time.y * _Speed * 0.8, _Time.y * _Speed * 0.35);
            return tex2D(_MainTex, a).r * 0.6 + tex2D(_MainTex, b).r * 0.4;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.worldPos.xz;
            float e = 0.05;
            float h = H(uv);
            float hx = H(uv + float2(e, 0));
            float hz = H(uv + float2(0, e));
            float3 n = normalize(float3((h - hx) * _Bump * 8, 1, (h - hz) * _Bump * 8));
            o.Normal = normalize(float3(n.x, n.z, n.y));
            float fres = pow(1 - saturate(dot(normalize(IN.viewDir), float3(0, 0, 1))), 3);
            o.Albedo = _Color.rgb * (0.85 + h * 0.3);
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            o.Emission = _Color.rgb * 0.05;
            o.Alpha = saturate(_Alpha + fres * 0.5 + h * 0.06);
        }
        ENDCG
    }
    FallBack Off
}
