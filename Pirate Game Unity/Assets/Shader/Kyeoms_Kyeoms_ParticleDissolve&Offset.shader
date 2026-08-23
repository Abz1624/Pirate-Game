Shader "Kyeoms/Kyeoms_ParticleDissolve&Offset"
{
    Properties
    {
        [NoScaleOffset] Texture2D_F593E37E ("MainTex", 2D) = "white" {}
        [NoScaleOffset] _MainTex_UV ("MainTex_UV", 2D) = "white" {}
        [ToggleUI] Boolean_2FAC1FE4 ("Use SoftParticleFactor?", Float) = 1
        _DissolveSoftness ("Dissolve Edge Softness", Range(0.01, 0.5)) = 0.12
        [HDR] _EdgeColor ("Dissolve Edge Glow Color", Color) = (1.6, 0.9, 0.5, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 200

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

            TEXTURE2D(Texture2D_F593E37E); SAMPLER(samplerTexture2D_F593E37E);
            TEXTURE2D(_MainTex_UV); SAMPLER(sampler_MainTex_UV);

            CBUFFER_START(UnityPerMaterial)
                float _DissolveSoftness;
                float4 _EdgeColor;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 baseTex = SAMPLE_TEXTURE2D(Texture2D_F593E37E, samplerTexture2D_F593E37E, IN.uv);
                half4 offsetTex = SAMPLE_TEXTURE2D(_MainTex_UV, sampler_MainTex_UV, IN.uv);

                half dissolveMask = offsetTex.r;
                half threshold = 1.0 - IN.color.a;
                half edge = smoothstep(threshold, threshold + _DissolveSoftness, dissolveMask);
                half edgeGlow = 1.0 - smoothstep(threshold, threshold + _DissolveSoftness * 2.0, dissolveMask);

                half3 albedo = baseTex.rgb * IN.color.rgb;
                half3 finalColor = lerp(albedo, _EdgeColor.rgb, edgeGlow * edge);
                half alpha = baseTex.a * edge * IN.color.a;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
