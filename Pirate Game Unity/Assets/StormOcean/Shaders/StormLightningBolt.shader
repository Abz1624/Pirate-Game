Shader "StormOcean/LightningBolt"
{
    Properties
    {
        _BoltColor("Bolt Color", Color) = (0.85,0.92,1.0,1.0)
        _BoltIntensity("Bolt Intensity", Range(0,10)) = 4.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha One

        Pass
        {
            Name "BoltUnlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BoltColor;
                float _BoltIntensity;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float edge = 1.0 - smoothstep(0.0, 1.0, abs(IN.uv.x * 2.0 - 1.0));
                return half4(_BoltColor.rgb * _BoltIntensity, edge);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
