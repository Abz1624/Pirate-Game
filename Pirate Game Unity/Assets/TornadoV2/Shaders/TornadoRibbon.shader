Shader "Pirate/TornadoRibbon"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.1, 1.05, 0.95, 1)
        _Speed ("Speed (rev/sec)", Float) = 0.35
        _Phase ("Phase", Float) = 0
        _Length ("Trail Length (fraction of loop)", Range(0.05, 1)) = 0.6
        _Power ("Trail Falloff", Float) = 1.2
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Speed; float _Phase; float _Length; float _Power; float _Alpha;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }
            half4 frag(Varyings IN) : SV_Target
            {
                float head = frac(_Time.y * _Speed + _Phase);
                float k = frac(head - IN.uv.x);
                float tail = pow(saturate(1.0 - k / _Length), _Power);
                float across = 1.0 - pow(abs(IN.uv.y * 2.0 - 1.0), 2.0);
                return half4(_Color.rgb, saturate(tail * across * _Alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
