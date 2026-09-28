// Vertex-colour lit surface. Vertex alpha is an emission mask: 0 = matte paint, 1 = full glow
// (neon, screens, lasers). _EmissionBoost pushes glowing parts past 1.0 so bloom picks them up.
Shader "WE/Lit"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0
        _EmissionBoost ("Emission Boost", Float) = 3
        _Flash ("Flash", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        half _EmissionBoost;
        half _Flash;

        struct Input
        {
            float2 uv_MainTex;
            float4 vcol;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcol = v.color;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 c = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb * IN.vcol.rgb;
            o.Albedo = c;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = c * IN.vcol.a * _EmissionBoost + _Flash * 0.6;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack Off
}
