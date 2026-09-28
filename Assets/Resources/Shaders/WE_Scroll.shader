// Scrolling surface for conveyor belts and the acid river: texture scrolls along U, can glow.
Shader "WE/Scroll"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Speed ("Scroll Speed", Float) = 0.5
        _Glow ("Glow", Float) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.3
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
        half _Speed;
        half _Glow;
        half _Glossiness;

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
            float2 uv = IN.uv_MainTex + float2(_Time.y * _Speed, 0);
            fixed3 c = tex2D(_MainTex, uv).rgb * _Color.rgb * IN.vcol.rgb;
            o.Albedo = c;
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            o.Emission = c * _Glow;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack Off
}
