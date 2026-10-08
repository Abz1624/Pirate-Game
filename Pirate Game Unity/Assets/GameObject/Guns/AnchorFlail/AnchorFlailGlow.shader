Shader "RoguePirates/AnchorFlailGlow"
{
    // Additive fresnel halo shell. Mesh is pushed out along its normals so the glow
    // hugs the silhouette of the anchor / chain links. One pass, no textures — mobile safe.
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (1, 0.3, 0.85, 1)
        _Width ("Shell Width", Range(0, 0.25)) = 0.05
        _Power ("Fresnel Power", Range(0.5, 8)) = 1.6
        _Intensity ("Intensity", Range(0, 6)) = 1.4
        _Core ("Core Fill", Range(0, 1)) = 0.12
        _PulseSpeed ("Pulse Speed", Float) = 4
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                float _Width;
                half _Power;
                half _Intensity;
                half _Core;
                half _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz) + normalWS * _Width;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = normalWS;
                o.viewWS = GetWorldSpaceViewDir(posWS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half ndv = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half rim = pow(1.0h - ndv, _Power);
                half glow = saturate(rim + _Core);
                half pulse = 1.0h + _PulseAmount * sin(_Time.y * _PulseSpeed);
                return half4(_GlowColor.rgb * glow * _Intensity * pulse, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
