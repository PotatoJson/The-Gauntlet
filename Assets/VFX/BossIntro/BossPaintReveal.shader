// The boss before and during his intro: bare grey clay, with his real texture spreading over it from a point
// in the world (his eyes) behind a burning edge. Unlit like his normal material, so the two match when swapped.
Shader "Hellbent/BossPaintReveal"
{
    Properties
    {
        _BaseMap ("Painted Texture", 2D) = "white" {}
        _BaseColor ("Painted Tint", Color) = (1,1,1,1)
        _ClayColor ("Clay", Color) = (0.4,0.38,0.36,1)
        [HDR] _EdgeColor ("Paint Edge", Color) = (4,1.3,0.3,1)
        _RevealCenter ("Reveal Center", Vector) = (0,0,0,0)
        _RevealRadius ("Reveal Radius", Float) = -2
        _EdgeWidth ("Edge Width", Float) = 0.35
        _NoiseScale ("Brush Noise Scale", Float) = 2.2
        _NoiseAmount ("Brush Noise Amount", Float) = 0.9
        _Dissolve ("Dissolve", Range(0, 1)) = 0
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

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; float3 normal : TEXCOORD2; };

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            fixed4 _BaseColor;
            fixed4 _ClayColor;
            half4 _EdgeColor;
            float4 _RevealCenter;
            float _RevealRadius;
            float _EdgeWidth;
            float _NoiseScale;
            float _NoiseAmount;
            float _Dissolve;

            float Hash(float3 p)
            {
                return frac(sin(dot(floor(p), float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            float Noise(float3 p)
            {
                float3 f = smoothstep(0, 1, frac(p));
                return lerp(
                    lerp(lerp(Hash(p), Hash(p + float3(1,0,0)), f.x), lerp(Hash(p + float3(0,1,0)), Hash(p + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(Hash(p + float3(0,0,1)), Hash(p + float3(1,0,1)), f.x), lerp(Hash(p + float3(0,1,1)), Hash(p + float3(1,1,1)), f.x), f.y),
                    f.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // The clay has no texture to show its shape, so it gets a soft light from the upper front.
                float light = 0.4 + 0.6 * saturate(dot(normalize(i.normal), normalize(float3(0.3, 0.8, 0.5))));
                half3 clay = _ClayColor.rgb * light;
                half3 painted = tex2D(_BaseMap, i.uv).rgb * _BaseColor.rgb;

                float brush = Noise(i.world * _NoiseScale) * 0.65 + Noise(i.world * _NoiseScale * 2.7) * 0.35;
                float ahead = _RevealRadius - distance(i.world, _RevealCenter.xyz) - brush * _NoiseAmount;

                // A boss left unfinished wears this through the fight, so it has to burn away on death too.
                float burn = Noise(i.world * 3.1) - _Dissolve * 1.05;
                clip(burn);

                half3 color = ahead > 0 ? painted : clay;
                if (ahead > 0 && ahead < _EdgeWidth) color = lerp(_EdgeColor.rgb, painted, ahead / _EdgeWidth);
                if (_Dissolve > 0 && burn < 0.06) color = _EdgeColor.rgb;
                return half4(color, 1);
            }
            ENDCG
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
