// A pulsing glow for things the player can use (chests, shrines): a rim of light around the object's silhouette and a
// thin outline just outside it. Drawn on a copy of the object's mesh, on top of its own materials, so nothing about the
// object itself changes. URP, unlit, additive.
Shader "Hellbent/InteractableGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.78, 0.3, 1)
        _RimPower ("Rim Sharpness", Float) = 2.5
        _Strength ("Strength", Float) = 1.6
        _OutlineWidth ("Outline Width (world units)", Float) = 0.04
        _PulseSpeed ("Pulse Speed", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _RimPower;
            float _Strength;
            float _OutlineWidth;
            float _PulseSpeed;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        half Pulse() { return 0.6h + 0.4h * sin(_Time.y * _PulseSpeed); }
        ENDHLSL

        // The rim: brightest where the surface turns away from the camera.
        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(position.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half rim = 1.0h - saturate(dot(normalize(input.normalWS), normalize(input.viewWS)));
                rim = pow(rim, _RimPower) * _Strength + 0.1h; // a faint wash over the whole object too
                return half4(_Color.rgb * rim * Pulse(), 1.0h);
            }
            ENDHLSL
        }

        // The outline: the mesh pushed out a little and drawn inside-out, so only a thin shell shows around the edge.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                return TransformWorldToHClip(positionWS + normalWS * _OutlineWidth);
            }

            half4 Frag() : SV_Target
            {
                return half4(_Color.rgb * Pulse() * 1.4h, 1.0h);
            }
            ENDHLSL
        }
    }
}
