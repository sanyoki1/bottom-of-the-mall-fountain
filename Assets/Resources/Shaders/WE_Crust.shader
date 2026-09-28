// The fountain crust: a tiled procedural coin texture (R = coin mask, G = coin shading,
// B = grime noise, A = sparkle seeds) tinted by the current stratum. Vertex colour darkens
// fresh craters. Coins get a twinkle so the pile always looks like there's treasure in it.
Shader "WE/Crust"
{
    Properties
    {
        _MainTex ("Coin Texture", 2D) = "white" {}
        _BaseColor ("Crust Colour", Color) = (0.5,0.5,0.4,1)
        _SpeckColor ("Coin Colour", Color) = (0.85,0.6,0.35,1)
        _CoinAmount ("Coin Amount", Range(0,1)) = 0.7
        _Scale ("World Scale", Float) = 0.22
        _Sparkle ("Sparkle", Range(0,1)) = 0.5
        _Concrete ("Concrete", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _BaseColor;
        fixed4 _SpeckColor;
        half _CoinAmount;
        half _Scale;
        half _Sparkle;
        half _Concrete;

        struct Input
        {
            float3 worldPos;
            float4 vcol;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcol = v.color;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.worldPos.xz * _Scale;
            fixed4 t = tex2D(_MainTex, uv);
            fixed4 t2 = tex2D(_MainTex, uv * 0.37 + 0.31);
            half grime = lerp(0.62, 1.08, t.b * 0.6 + t2.b * 0.4);
            half coin = saturate(t.r * _CoinAmount * 1.4);
            fixed3 crust = _BaseColor.rgb * grime;
            fixed3 coins = _SpeckColor.rgb * (0.55 + 0.75 * t.g);
            fixed3 albedo = lerp(crust, coins, coin);

            // a few seeded coins twinkle over time
            half phase = frac(t.a * 7.31 + _Time.y * (0.35 + t.a * 0.4));
            half tw = saturate(1 - abs(phase - 0.5) * 9) * step(0.82, t.a) * coin * _Sparkle;

            fixed3 concrete = fixed3(0.74, 0.73, 0.70) * lerp(0.9, 1.05, t2.b);
            albedo = lerp(albedo, concrete, _Concrete);
            albedo *= IN.vcol.rgb;

            o.Albedo = albedo;
            o.Metallic = coin * 0.35 * (1 - _Concrete);
            o.Smoothness = lerp(0.12, 0.55, coin) * (1 - _Concrete * 0.7);
            o.Emission = _SpeckColor.rgb * tw * 2.2 * (1 - _Concrete);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack Off
}
