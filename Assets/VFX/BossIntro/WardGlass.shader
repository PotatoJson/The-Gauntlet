// The fire wards round Brutus's arena (WardRing): panes of glowing glass between iron posts. Additive, so they read
// as light rather than a wall; brighter at the top and bottom edges, with a slow shimmer rising through them. Cracks
// spread out from where he struck it as _Crack goes from 0 to 1, and _Flash whitens the whole pane for a moment.
Shader "Hellbent/WardGlass"
{
    Properties
    {
        _Color ("Glow", Color) = (1, 0.42, 0.12, 1)
        _Strength ("Strength", Range(0, 2)) = 0.55
        _CrackTex ("Cracks", 2D) = "black" {}
        _CrackColor ("Crack", Color) = (1, 0.9, 0.6, 1)
        _Crack ("Crack Spread", Range(0, 1)) = 0
        _CrackCentre ("Crack Centre (UV)", Vector) = (0.5, 0.5, 0, 0)
        _Flash ("Flash", Range(0, 1)) = 0
        _Rise ("Rise", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _Color;
            float _Strength;
            sampler2D _CrackTex;
            fixed4 _CrackColor;
            float _Crack;
            float4 _CrackCentre;
            float _Flash;
            float _Rise;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // Only what has risen so far shows: the pane grows up out of the floor.
                clip(_Rise - i.uv.y);

                float edge = smoothstep(0.25, 0.0, i.uv.y) + smoothstep(0.75, 1.0, i.uv.y) * 0.7;
                float shimmer = 0.5 + 0.5 * sin(i.uv.y * 18.0 - _Time.y * 2.6 + sin(i.uv.x * 9.0 + _Time.y) * 1.4);
                float glow = _Strength * (0.35 + edge + shimmer * 0.25);

                // Cracks out to a reach that grows with _Crack.
                float reach = length((i.uv - _CrackCentre.xy) * float2(2.0, 1.0));
                float crack = tex2D(_CrackTex, i.uv).a * step(reach, _Crack * 1.6);

                half3 colour = _Color.rgb * glow + _CrackColor.rgb * crack * 1.6 + _Flash * 0.8;
                half alpha = saturate(glow * 0.8 + crack + _Flash);
                return half4(colour, alpha);
            }
            ENDCG
        }
    }
}
