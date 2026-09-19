Shader "Shader Graphs/Tornado"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.1556604, 0.9242384, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.5
        _Emission ("Emission", Float) = 1.68
        _Tiling ("Tiling", Vector) = (1, 1, 0, 0)
        _FresnelPower ("FresnelPower", Float) = 1.25
        _FresnelGlow ("FresnelGlow", Float) = 2
        [HDR] _FresnelColor ("FresnelColor", Color) = (1, 1, 1, 1)
        _WaveSpeed ("WaveSpeed", Float) = 1
        _WaveNumber ("WaveNumber", Float) = 5
        _Reel ("Reel (x=stripe count, y=softness, z=twist)", Vector) = (10, 0.3, 5, 0)
        _TextureSpeed ("TextureSpeed", Vector) = (0, 0, 0, 0)
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Alpha;
                float _Emission;
                float4 _Tiling;
                float _FresnelPower;
                float _FresnelGlow;
                float4 _FresnelColor;
                float _WaveSpeed;
                float _WaveNumber;
                float4 _Reel;
                float4 _TextureSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 p = IN.positionOS.xyz;
                // sway: bottom of cylinder pinned, top moves most
                float h = saturate(p.y * 0.5 + 0.5);
                float phase = p.y * _WaveNumber + _Time.y * _WaveSpeed;
                p.x += sin(phase) * 0.05 * h;
                p.z += cos(phase) * 0.05 * h;

                float3 posWS = TransformObjectToWorld(p);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(posWS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float3 v = normalize(IN.viewDirWS);
                float fres = pow(saturate(1.0 - abs(dot(n, v))), max(_FresnelPower, 0.0001)) * _FresnelGlow;

                float2 uv = IN.uv * _Tiling.xy + _TextureSpeed.xy * _Time.y;
                float stripe = sin((uv.x * _Reel.x + uv.y * _Reel.z) * 6.2831853) * 0.5 + 0.5;
                float soft = max(_Reel.y, 0.001) * 0.5;
                stripe = smoothstep(0.5 - soft, 0.5 + soft, stripe);

                half3 body = _Color.rgb * (1.0 + stripe * _Emission);
                half3 rim = _FresnelColor.rgb * fres;
                half alpha = saturate(_Alpha * lerp(0.5, 1.0, stripe) + saturate(fres) * _Alpha);
                return half4(body + rim, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
