// The glass of the mirror at the bottom of Hell (InkMirror). It shows what the mirror's camera sees, sampled at the
// same place on screen, so the glass is a window onto the knight's reflection standing behind it. Cracks spread out
// from a point as _Crack goes from 0 to 1.
Shader "Hellbent/MirrorGlass"
{
    Properties
    {
        _ReflectionTex ("Reflection", 2D) = "black" {}
        _CrackTex ("Cracks", 2D) = "black" {}
        _Tint ("Reflection Tint", Color) = (0.72, 0.76, 0.92, 1)
        _Sheen ("Sheen", Color) = (0.2, 0.22, 0.32, 1)
        _CrackColor ("Crack", Color) = (0.86, 0.9, 1, 1)
        _Crack ("Crack Spread", Range(0, 1)) = 0
        _CrackCentre ("Crack Centre (UV)", Vector) = (0.5, 0.5, 0, 0)
        _GlassSize ("Glass Size (m)", Vector) = (2, 3, 0, 0)
        _FlipY ("Flip Reflection", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; };

            sampler2D _ReflectionTex;
            sampler2D _CrackTex;
            fixed4 _Tint;
            fixed4 _Sheen;
            fixed4 _CrackColor;
            float _Crack;
            float4 _CrackCentre;
            float4 _GlassSize;
            float _FlipY;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 screen = i.screen.xy / i.screen.w;
                if (_FlipY > 0.5) screen.y = 1 - screen.y;
                half3 color = tex2D(_ReflectionTex, screen).rgb * _Tint.rgb;

                // Two thin bands of cold light across the glass, and a little darker towards the frame.
                float diagonal = i.uv.x * 0.55 + i.uv.y;
                color += _Sheen.rgb * (smoothstep(0.07, 0.0, abs(diagonal - 0.95)) * 0.6 + smoothstep(0.025, 0.0, abs(diagonal - 1.1)) * 0.4);
                float2 edge = min(i.uv, 1 - i.uv);
                color *= saturate(min(edge.x, edge.y) * 14) * 0.35 + 0.65;

                // Cracks, out to a reach that grows with _Crack, measured in metres on the glass.
                float2 metres = (i.uv - _CrackCentre.xy) * _GlassSize.xy;
                float crack = tex2D(_CrackTex, i.uv).a * step(length(metres), _Crack * 2.2);
                color = lerp(color, _CrackColor.rgb, crack);

                return half4(color, 1);
            }
            ENDCG
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
