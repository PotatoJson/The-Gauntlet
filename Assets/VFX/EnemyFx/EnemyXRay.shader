// Draws an enemy as a glowing silhouette ONLY where a wall (or anything else) hides it.
// Used by the "EnemyHint" renderer feature (RenderObjects on the Enemy layer, depth test Greater), switched on by
// LastEnemiesHint when the player has been struggling to find the last few enemies.
Shader "Hellbent/EnemyXRay"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.25, 0.1, 1)
        _RimPower ("Rim Power", Range(0.5, 6)) = 1.5
        _PulseSpeed ("Pulse Speed", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "XRay"
            ZTest Greater
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _RimPower;
                half _PulseSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewDirWS : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(pos.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half rim = pow(1.0h - saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS))), _RimPower);
                half pulse = 0.75h + 0.25h * sin(_Time.y * _PulseSpeed);
                half alpha = saturate(lerp(0.35h, 1.0h, rim)) * pulse;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
