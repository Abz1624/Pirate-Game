Shader "Shader Graphs/HCFX_Particle2ndColor"
{
    Properties
    {
        [NoScaleOffset] Texture2D_F593E37E ("MainTex", 2D) = "white" {}
        [ToggleUI] Boolean_BB495B7B ("Use SoftParticleFactor?", Float) = 1
        _Intensity ("Glow Intensity", Range(0.1, 5)) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 200

        Pass
        {
            Name "ForwardAdd"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
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

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
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
                half4 tex = SAMPLE_TEXTURE2D(Texture2D_F593E37E, samplerTexture2D_F593E37E, IN.uv);
                half3 glow = tex.rgb * IN.color.rgb * _Intensity;
                half alpha = tex.a * IN.color.a;
                return half4(glow * alpha, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
