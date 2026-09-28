// Build-mode hologram: flat translucent colour with a bright rim, ignoring vertex alpha (which
// is an emission mask elsewhere). Green = can build here, red = can't.
Shader "WE/Ghost"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 1, 0.5, 0.35)
        _Rim ("Rim", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Rim;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; fixed shade : TEXCOORD2; };

            v2f vert(appdata i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = normalize(WorldSpaceViewDir(i.vertex));
                o.shade = dot(i.color.rgb, fixed3(0.3, 0.59, 0.11));
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half rim = pow(1 - saturate(abs(dot(normalize(i.n), i.v))), 2) * _Rim;
                half3 c = _Color.rgb * (0.55 + 0.45 * i.shade) + _Color.rgb * rim;
                return half4(c, saturate(_Color.a + rim * 0.4));
            }
            ENDCG
        }
    }
    FallBack Off
}
