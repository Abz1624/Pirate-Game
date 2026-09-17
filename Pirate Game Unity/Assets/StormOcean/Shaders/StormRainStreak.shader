Shader "StormOcean/RainStreak"
{
    Properties
    {
        _RainColor("Rain Color", Color) = (0.72,0.8,0.86,0.35)
        _RainOpacity("Rain Opacity", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RainUnlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RainColor;
                float _RainOpacity;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv * 2.0 - 1.0;
                float streak = 1.0 - smoothstep(0.0, 1.0, abs(uv.x));
                float lenFade = smoothstep(0.0, 0.18, uv.y + 1.0) * smoothstep(0.0, 0.18, 1.0 - uv.y);
                float alpha = streak * lenFade * _RainOpacity * IN.color.a * _RainColor.a;
                return half4(_RainColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
