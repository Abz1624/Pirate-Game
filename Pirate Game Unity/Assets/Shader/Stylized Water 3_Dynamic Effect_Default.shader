Shader "Stylized Water 3/Dynamic Effect/Default"
{
    Properties
    {
        [MaterialEnum(Off,0,Front,1,Back,2)] _Cull ("Cull", Float) = 0
        _BaseMap ("Texture (R=Height mask, G=Foam Mask)", 2D) = "white" {}
        [Toggle] _RemapDisplacement ("Remap displacement", Float) = 0
        _AnimationSpeed ("Panning Speed (XY)", Vector) = (0,0,0,0)
        _MaskMap ("Mask", 2D) = "white" {}
        _MaskAnimationSpeed ("Panning Speed (XY)", Vector) = (0,0,0,0)
        [Header(Output)] _HeightScale ("Displacement scale", Float) = 0.1
        _FoamStrength ("Foam strength", Float) = 1
        _NormalStrength ("Normal strength", Float) = 1
        [HDR] _FoamColor ("Foam Color (visual fallback tint)", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 200

        Pass
        {
            Name "FoamVisible"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _MaskMap_ST;
                float _RemapDisplacement;
                float4 _AnimationSpeed;
                float4 _MaskAnimationSpeed;
                float _HeightScale;
                float _FoamStrength;
                float _NormalStrength;
                float4 _FoamColor;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            // The texture actually assigned in this project (Splash_AlbedoAlpha) is a real albedo+alpha sprite,
            // not the packed height/foam-mask data buffer the original Stylized Water 3 pipeline expects. So we
            // read it as a normal albedo (RGB) + shape (A) foam texture: RGB gives visible surface detail
            // (tinted by _FoamColor), and the alpha channel supplies the real splash/foam silhouette instead of
            // a flat, fully-opaque white blob.
            half4 frag (Varyings IN) : SV_Target
            {
                float2 baseUV = IN.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _AnimationSpeed.xy * _Time.y;
                float2 maskUV = IN.uv * _MaskMap_ST.xy + _MaskMap_ST.zw + _MaskAnimationSpeed.xy * _Time.y;

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
                half4 maskTex = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, maskUV);

                half3 albedo = baseTex.rgb * _FoamColor.rgb;
                half shape = baseTex.a * maskTex.r;
                // _FoamStrength is authored per-material and can legitimately be 0 (e.g. the ripple splash
                // material), which must NOT zero out visibility entirely - only ever boost the shape's alpha.
                half alphaBoost = max(_FoamStrength, 1.0);
                half alpha = saturate(shape * alphaBoost) * IN.color.a;

                return half4(albedo, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
