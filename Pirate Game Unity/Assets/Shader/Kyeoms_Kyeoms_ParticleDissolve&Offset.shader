Shader "Kyeoms/Kyeoms_ParticleDissolve&Offset" {
	Properties {
		[NoScaleOffset] Texture2D_F593E37E ("MainTex", 2D) = "white" {}
		[NoScaleOffset] _MainTex_UV ("MainTex_UV", 2D) = "white" {}
		[ToggleUI] Boolean_2FAC1FE4 ("Use SoftParticleFactor?", Float) = 1
		_SoftParticleFadeDistance ("Soft Particle Fade Distance", Float) = 1.0
		_UVOffsetStrength ("UV Offset Strength", Float) = 0.05
		[HideInInspector] _CastShadows ("_CastShadows", Float) = 0
		[HideInInspector] _Surface ("_Surface", Float) = 1
		[HideInInspector] _Blend ("_Blend", Float) = 0
		[HideInInspector] _AlphaClip ("_AlphaClip", Float) = 0
		[HideInInspector] _SrcBlend ("_SrcBlend", Float) = 5
		[HideInInspector] _DstBlend ("_DstBlend", Float) = 10
		[HideInInspector] _SrcBlendAlpha ("_SrcBlendAlpha", Float) = 1
		[HideInInspector] _DstBlendAlpha ("_DstBlendAlpha", Float) = 0
		[ToggleUI] [HideInInspector] _ZWrite ("_ZWrite", Float) = 0
		[HideInInspector] _ZWriteControl ("_ZWriteControl", Float) = 0
		[HideInInspector] _ZTest ("_ZTest", Float) = 4
		[HideInInspector] _Cull ("_Cull", Float) = 0
		[HideInInspector] _AlphaToMask ("_AlphaToMask", Float) = 0
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
		[HideInInspector] _BUILTIN_Surface ("Float", Float) = 1
		[HideInInspector] _BUILTIN_Blend ("Float", Float) = 0
		[HideInInspector] _BUILTIN_AlphaClip ("Float", Float) = 0
		[HideInInspector] _BUILTIN_SrcBlend ("Float", Float) = 1
		[HideInInspector] _BUILTIN_DstBlend ("Float", Float) = 0
		[HideInInspector] _BUILTIN_ZWrite ("Float", Float) = 0
		[HideInInspector] _BUILTIN_ZWriteControl ("Float", Float) = 0
		[HideInInspector] _BUILTIN_ZTest ("Float", Float) = 4
		[HideInInspector] _BUILTIN_CullMode ("Float", Float) = 0
		[HideInInspector] _BUILTIN_QueueOffset ("Float", Float) = 0
		[HideInInspector] _BUILTIN_QueueControl ("Float", Float) = -1
	}

	SubShader {
		Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		LOD 100

		Pass {
			Name "ForwardUnlit"
			Tags { "LightMode"="UniversalForward" }

			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]
			ZTest LEqual
			Cull [_Cull]

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

			TEXTURE2D(Texture2D_F593E37E); SAMPLER(sampler_Texture2D_F593E37E);
			TEXTURE2D(_MainTex_UV); SAMPLER(sampler_MainTex_UV);

			CBUFFER_START(UnityPerMaterial)
				float Boolean_2FAC1FE4;
				float _SoftParticleFadeDistance;
				float _UVOffsetStrength;
			CBUFFER_END

			struct Attributes {
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings {
				float4 positionHCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				float4 screenPos : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings vert(Attributes IN) {
				Varyings OUT = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

				VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
				OUT.positionHCS = vpi.positionCS;
				OUT.screenPos = vpi.positionNDC;
				OUT.uv = IN.uv;
				OUT.color = IN.color;
				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target {
				UNITY_SETUP_INSTANCE_ID(IN);

				half4 offsetSample = SAMPLE_TEXTURE2D(_MainTex_UV, sampler_MainTex_UV, IN.uv);
				float2 uvOffset = (offsetSample.rg - 0.5) * _UVOffsetStrength;
				float2 finalUV = IN.uv + uvOffset;

				half4 col = SAMPLE_TEXTURE2D(Texture2D_F593E37E, sampler_Texture2D_F593E37E, finalUV);
				col *= IN.color;

				if (Boolean_2FAC1FE4 > 0.5) {
					float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
					float rawDepth = SampleSceneDepth(screenUV);
					float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
					float particleEyeDepth = IN.screenPos.w;
					float fadeDist = max(_SoftParticleFadeDistance, 0.0001);
					float fade = saturate((sceneEyeDepth - particleEyeDepth) / fadeDist);
					col.a *= fade;
				}

				clip(col.a - 0.001);
				return col;
			}
			ENDHLSL
		}
	}
	Fallback "Universal Render Pipeline/Particles/Unlit"
}
