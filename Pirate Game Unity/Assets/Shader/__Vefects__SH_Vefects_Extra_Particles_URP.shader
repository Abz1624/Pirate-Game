Shader "/_Vefects_/SH_Vefects_Extra_Particles_URP" {
	Properties {
		[HideInInspector] _AlphaCutoff ("Alpha Cutoff ", Range(0, 1)) = 0.5
		[HideInInspector] _EmissionColor ("Emission Color", Vector) = (1,1,1,1)
		_EmissiveIntensity ("Emissive Intensity", Float) = 1
		_TextureLerp ("Texture Lerp", Float) = 0
		_ErosionSmoothness ("Erosion Smoothness", Float) = 1
		_ParticleTexture ("Particle Texture", 2D) = "white" {}
		[Space(33)] [Header(AR)] [Space(13)] _Cull ("Cull", Float) = 2
		_Src ("Src", Float) = 5
		_Dst ("Dst", Float) = 10
		_ZWrite ("ZWrite", Float) = 0
		_ZTest ("ZTest", Float) = 2
		[HideInInspector] _texcoord ("", 2D) = "white" {}
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
		[ToggleOff] [HideInInspector] _ReceiveShadows ("Receive Shadows", Float) = 0
	}
	SubShader{
		Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
		LOD 200

		Pass
		{
			Name "ForwardParticle"
			Tags { "LightMode"="UniversalForward" }
			Cull [_Cull]
			ZWrite [_ZWrite]
			ZTest [_ZTest]
			Blend [_Src] [_Dst]

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

			TEXTURE2D(_ParticleTexture); SAMPLER(sampler_ParticleTexture);

			CBUFFER_START(UnityPerMaterial)
				float4 _ParticleTexture_ST;
				float _EmissiveIntensity;
				float _TextureLerp;
				float _ErosionSmoothness;
				float _AlphaCutoff;
			CBUFFER_END

			Varyings vert (Attributes IN)
			{
				Varyings OUT;
				OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
				OUT.uv = TRANSFORM_TEX(IN.uv, _ParticleTexture);
				OUT.color = IN.color;
				return OUT;
			}

			// Real replacement for a DummyShaderTextExporter stub (baked to flat white). Recovered as a
			// generic soft particle: the particle texture's alpha drives a feathered erosion/dissolve edge
			// (_ErosionSmoothness), its RGB is blended against the per-particle vertex color (_TextureLerp),
			// and the result is boosted by _EmissiveIntensity for glow.
			half4 frag (Varyings IN) : SV_Target
			{
				half4 tex = SAMPLE_TEXTURE2D(_ParticleTexture, sampler_ParticleTexture, IN.uv);

				half edge = saturate(_ErosionSmoothness) > 0.0001
					? smoothstep(0.0, saturate(_ErosionSmoothness), tex.a)
					: step(_AlphaCutoff, tex.a);

				half3 tintedColor = lerp(tex.rgb * IN.color.rgb, IN.color.rgb, saturate(_TextureLerp));
				half3 finalColor = tintedColor * _EmissiveIntensity;
				half alpha = edge * IN.color.a;

				return half4(finalColor, alpha);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
