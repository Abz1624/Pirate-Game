Shader "RoguePirates/InkPotToon"
{
    // Soft cel-shaded toon lit for the Ink Pot (clay, cork, ink, tentacle). Hand-painted base map, 2-tone shade,
    // stepped glossy highlight, rim sheen, darker inside colour for backfaces (cracked pot interior).
    // One pass, main light + SH ambient, vertex colour multiply (particle meshes). Mobile safe.
    Properties
    {
        _BaseMap ("Base (hand-painted)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade multiplier", Color) = (0.7, 0.58, 0.55, 1)
        _ShadeThreshold ("Shade threshold", Range(-1, 1)) = 0.05
        _ShadeSoftness ("Shade softness", Range(0.001, 0.5)) = 0.07
        _GlossColor ("Gloss colour", Color) = (1, 1, 1, 1)
        _GlossSize ("Gloss size", Range(0, 1)) = 0.12
        _GlossSoftness ("Gloss softness", Range(0.001, 0.2)) = 0.012
        _GlossIntensity ("Gloss intensity", Range(0, 2)) = 0.6
        _RimColor ("Rim / sheen", Color) = (1, 0.92, 0.8, 1)
        _RimPower ("Rim power", Range(0.5, 8)) = 3
        _RimIntensity ("Rim intensity", Range(0, 2)) = 0.2
        _BackfaceColor ("Inside colour", Color) = (0.35, 0.22, 0.15, 1)
        _Ambient ("Ambient", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Toon"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadeColor;
                half _ShadeThreshold;
                half _ShadeSoftness;
                half4 _GlossColor;
                half _GlossSize;
                half _GlossSoftness;
                half _GlossIntensity;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half4 _BackfaceColor;
                half _Ambient;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb * i.color.rgb;
                if (!IS_FRONT_VFACE(face, true, false))
                    return half4(albedo * _BackfaceColor.rgb, 1);

                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light l = GetMainLight();

                half ndl = dot(n, l.direction);
                half lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl);
                half3 col = lerp(albedo * _ShadeColor.rgb, albedo, lit);
                col *= lerp(half3(1, 1, 1), l.color, 0.5);
                col += albedo * SampleSH(n) * _Ambient;

                // stepped glossy highlight
                float3 h = normalize(l.direction + v);
                half nh = saturate(dot(n, h));
                half edge = 1.0 - _GlossSize * 0.25;
                half gloss = smoothstep(edge - _GlossSoftness, edge + _GlossSoftness, nh) * (0.35 + 0.65 * lit);
                col = lerp(col, _GlossColor.rgb, saturate(gloss * _GlossIntensity));

                // rim sheen
                half rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimIntensity;
                col += _RimColor.rgb * rim;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
