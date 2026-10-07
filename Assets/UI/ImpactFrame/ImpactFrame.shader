// Full-screen "impact frame" look for the low-res game RawImage: the scene is reduced to two
// colours by luminance and (optionally) inverted, like the hard black-and-white frames used in
// anime and fighting games. UI/Default-compatible, so it can be swapped onto a RawImage.
Shader "UI/ImpactFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Threshold ("Luminance Threshold", Range(0,1)) = 0.3
        _Softness ("Edge Softness", Range(0.0005,0.3)) = 0.12
        _Invert ("Invert", Range(0,1)) = 1
        _Mix ("Effect Strength", Range(0,1)) = 1
        _DarkColor ("Dark Colour", Color) = (0,0,0,1)
        _LightColor ("Light Colour", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Threshold;
            float _Softness;
            float _Invert;
            float _Mix;
            fixed4 _DarkColor;
            fixed4 _LightColor;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.texcoord);
                float lum = dot(src.rgb, float3(0.299, 0.587, 0.114));
#ifndef UNITY_COLORSPACE_GAMMA
                lum = pow(max(lum, 0.0), 0.4545); // perceptual brightness, so the threshold behaves like what the eye sees
#endif
                float m = smoothstep(_Threshold - _Softness, _Threshold + _Softness, lum);
                m = lerp(m, 1.0 - m, _Invert);
                fixed4 col = lerp(_DarkColor, _LightColor, m);
                col = lerp(src, col, _Mix); // below 1 keeps some of the real image, so the frame is softer
                col.a = i.color.a;
                return col;
            }
            ENDCG
        }
    }
}
