// The XP box fill with a rolling pixel-art wave on its surface. The wave is snapped to the art's pixel
// grid so it stays crisp, and a second, paler wave sits behind it for depth. Replaces the Image's
// built-in vertical fill: the shader decides what is filled from _Fill.
Shader "UI/XPWaveFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Fill ("Fill", Range(0,1)) = 0.5
        _Splash ("Splash", Range(0,1)) = 0
        _PixelCount ("Art Pixels Across", Float) = 30
        _WaveAmp ("Wave Height", Float) = 0.04
        _UVRect ("Sprite UV Rect (xmin,ymin,xmax,ymax)", Vector) = (0,0,1,1)
        _UnscaledTime ("Unscaled Time", Float) = 0
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
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Fill, _Splash, _PixelCount, _WaveAmp, _UnscaledTime;
            float4 _UVRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 0..1 across the box, even if the sprite sits inside an atlas.
                float2 uv = (i.uv - _UVRect.xy) / max(_UVRect.zw - _UVRect.xy, 0.0001);
                float2 p = (floor(uv * _PixelCount) + 0.5) / _PixelCount; // snap to art pixels
                float t = _UnscaledTime;

                // Waves flatten out when the box is empty or full, and slosh harder right after XP is gained.
                float amp = saturate(sin(_Fill * 3.14159) * 2.0) * _WaveAmp * (1.0 + _Splash * 2.0);
                float front = _Fill + (sin(p.x * 20.0 + t * 3.0) * 0.6 + sin(p.x * 9.0 - t * 2.1 + 1.7) * 0.4) * amp;
                float back  = _Fill + (sin(p.x * 16.0 - t * 2.2 + 0.8) * 0.6 + sin(p.x * 7.0 + t * 1.6) * 0.4) * amp * 1.4 + 0.015;

                fixed4 col = i.color;
                float edge = 1.6 / _PixelCount;

                if (p.y <= front)
                {
                    // bright rim along the surface
                    if (p.y > front - edge) col.rgb = saturate(col.rgb * 1.3 + 0.12);
                    return col;
                }

                if (p.y <= back)
                {
                    // paler wave behind the front one
                    col.rgb = lerp(col.rgb, fixed3(1, 1, 1), 0.22);
                    col.a *= 0.55;
                    return col;
                }

                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}
