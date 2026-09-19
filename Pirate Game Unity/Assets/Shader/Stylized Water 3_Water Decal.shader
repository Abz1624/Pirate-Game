Shader "Stylized Water 3/Water Decal"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        [HDR] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _Mask ("Mask", 2D) = "white" {}
        _Alpha_Clip_Threshold ("Alpha Clip Threshold", Range(0, 1)) = 0.5
        _Specular_Color ("Specular Color", Color) = (0, 0, 0, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0
        _Metalicness ("Metalicness", Range(0, 1)) = 0
        [ToggleUI] _ALIGN_TO_WATER ("Raise by Offset", Float) = 1
        _Offset ("Height Offset", Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _ALIGN_TO_WATER;
                float _Offset;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                posWS.y += _Offset * _ALIGN_TO_WATER;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                return half4(tex.rgb * _BaseColor.rgb, tex.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
