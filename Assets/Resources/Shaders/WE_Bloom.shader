// Small bloom + grade: prefilter (soft threshold), box down/upsample chain, then combine
// with a gentle saturation lift and vignette. Driven by View/BloomFX.cs via OnRenderImage.
Shader "Hidden/WE/Bloom"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _SourceTex;
    half4 _Filter;        // x threshold, y x-knee, z 2*knee, w 0.25/knee
    half _Intensity;
    half _Saturation;
    half _Vignette;
    half _Contrast;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 Sample(float2 uv) { return tex2D(_MainTex, uv).rgb; }

    half3 SampleBox(float2 uv, float delta)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-delta, delta).xxyy;
        half3 s = Sample(uv + o.xy) + Sample(uv + o.zy) + Sample(uv + o.xw) + Sample(uv + o.zw);
        return s * 0.25h;
    }

    half3 Prefilter(half3 c)
    {
        half brightness = max(c.r, max(c.g, c.b));
        half soft = brightness - _Filter.y;
        soft = clamp(soft, 0, _Filter.z);
        soft = soft * soft * _Filter.w;
        half contribution = max(soft, brightness - _Filter.x);
        contribution /= max(brightness, 0.00001);
        return c * contribution;
    }
    ENDCG

    SubShader
    {
        Cull Off ZTest Always ZWrite Off

        Pass // 0 prefilter + first downsample
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(Prefilter(SampleBox(i.uv, 1)), 1); }
            ENDCG
        }

        Pass // 1 downsample
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(SampleBox(i.uv, 1), 1); }
            ENDCG
        }

        Pass // 2 upsample (additive)
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(SampleBox(i.uv, 0.5), 1); }
            ENDCG
        }

        Pass // 3 combine + grade
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target
            {
                half4 src = tex2D(_SourceTex, i.uv);
                half3 c = src.rgb + _Intensity * SampleBox(i.uv, 0.5);
                half lum = dot(c, half3(0.2126, 0.7152, 0.0722));
                c = lerp(lum.xxx, c, _Saturation);
                c = (c - 0.18) * _Contrast + 0.18;
                float2 d = i.uv - 0.5;
                half vig = 1 - _Vignette * dot(d, d) * 1.6;
                c *= vig;
                return half4(max(c, 0), src.a);
            }
            ENDCG
        }
    }
}
