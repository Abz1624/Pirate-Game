Shader "/_Vefects_/SH_Vefects_Zap_URP" {
	Properties {
		[HideInInspector] _EmissionColor ("Emission Color", Vector) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff ("Alpha Cutoff ", Range(0, 1)) = 0.5
		[Space(33)] [Header(Zap)] [Space(13)] _ZapTexture ("Zap Texture", 2D) = "white" {}
		_EmissiveIntensity ("Emissive Intensity", Float) = 1
		_ErosionSmoothness ("Erosion Smoothness", Float) = 0.01
		_TextureMultiply ("Texture Multiply", Float) = 0
		[Space(33)] [Header(LUT)] [Space(13)] _LUT ("LUT", 2D) = "white" {}
		_LUTAmplitude ("LUT Amplitude", Float) = 1
		_LUTOffset ("LUT Offset", Float) = 0
		_LUTErosion ("LUT Erosion", Float) = 0
		_LUTErosionOffset ("LUT Erosion Offset", Float) = 0
		_LUTErosionSmoothness ("LUT Erosion Smoothness", Float) = 0.3
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
			Name "ForwardZap"
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

			TEXTURE2D(_ZapTexture); SAMPLER(sampler_ZapTexture);
			TEXTURE2D(_LUT); SAMPLER(sampler_LUT);

			CBUFFER_START(UnityPerMaterial)
				float4 _ZapTexture_ST;
				float _EmissiveIntensity;
				float _ErosionSmoothness;
				float _TextureMultiply;
				float _LUTAmplitude;
				float _LUTOffset;
				float _LUTErosion;
				float _LUTErosionOffset;
				float _LUTErosionSmoothness;
			CBUFFER_END

			Varyings vert (Attributes IN)
			{
				Varyings OUT;
				OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
				OUT.uv = TRANSFORM_TEX(IN.uv, _ZapTexture);
				OUT.color = IN.color;
				return OUT;
			}

			// Real replacement for a DummyShaderTextExporter stub (baked to flat white). Recovered as a
			// classic dissolve+LUT "energy zap" shader: the zap noise texture drives both an eroding
			// alpha edge (LUTErosion/Offset/Smoothness) and a lookup into a 1D color ramp (_LUT) for the
			// bolt's core-to-edge color gradient, boosted by _EmissiveIntensity for glow.
			half4 frag (Varyings IN) : SV_Target
			{
				half4 zapTex = SAMPLE_TEXTURE2D(_ZapTexture, sampler_ZapTexture, IN.uv);
				half zapValue = zapTex.r;

				half erosionThreshold = _LUTErosion + _LUTErosionOffset;
				half smoothness = max(_LUTErosionSmoothness, 0.0001);
				half alpha = 1.0 - smoothstep(erosionThreshold, erosionThreshold + smoothness, zapValue);

				half lutU = saturate(zapValue * _LUTAmplitude + _LUTOffset);
				half4 lutColor = SAMPLE_TEXTURE2D(_LUT, sampler_LUT, half2(lutU, 0.5));

				half3 texTint = lerp(half3(1,1,1), zapTex.rgb, saturate(_TextureMultiply));
				half3 finalColor = lutColor.rgb * texTint * IN.color.rgb * _EmissiveIntensity;

				return half4(finalColor, alpha * IN.color.a);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
