// The screen pulled into a portal: a still of the last frame, twisted round the middle (most at the centre),
// shrinking into it, with the portal's glow growing there, then fading to black. Driven by PortalTwist.cs.
Shader "Hidden/PortalTwist"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "black" {}
        _Twist ("Twist (radians at the centre)", Float) = 0
        _Pull ("Pull into the centre (1 = none)", Float) = 1
        _Glow ("Glow", Color) = (1, 0.25, 0.15, 1)
        _GlowAmount ("Glow amount", Float) = 0
        _Fade ("Fade to black", Float) = 0
        _Aspect ("Screen aspect", Float) = 1.7778
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off ZTest Always Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Twist, _Pull, _GlowAmount, _Fade, _Aspect;
            float4 _Glow;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                c.x *= _Aspect;
                float r = length(c);

                // Twisted most at the centre, not at all at the corners.
                float falloff = saturate(1.0 - r / 1.1);
                float a = _Twist * falloff * falloff;
                float s = sin(a), k = cos(a);
                c = float2(c.x * k - c.y * s, c.x * s + c.y * k);

                // Pulled in: sampling further out makes the picture shrink towards the middle.
                c /= max(_Pull, 0.02);
                c.x /= _Aspect;
                float2 uv = c + 0.5;
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                float3 col = tex2D(_MainTex, uv).rgb * inside;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                // The screenshot holds the screen's (sRGB) colours but is read as linear; without this it comes out
                // washed out and brighter than the frame it was taken from.
                col = GammaToLinearSpace(col);
                #endif

                // The portal's light, a ring and a hot core at the centre.
                float ring = exp(-pow((r - 0.12) * 14.0, 2.0)) + exp(-r * 9.0);
                col += _Glow.rgb * ring * _GlowAmount;

                col *= 1.0 - _Fade;
                return float4(col, 1.0);
            }
            ENDCG
        }
    }
}
