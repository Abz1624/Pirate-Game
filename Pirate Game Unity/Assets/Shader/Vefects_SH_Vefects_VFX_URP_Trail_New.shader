Shader "Vefects/SH_Vefects_VFX_URP_Trail_New" {
	Properties {
		[HideInInspector] _EmissionColor ("Emission Color", Vector) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff ("Alpha Cutoff ", Range(0, 1)) = 0.5
		_EmissiveIntensity ("Emissive Intensity", Float) = 1
		_AlphaAffectsOpacity ("Alpha Affects Opacity", Float) = 1
		_OverallSpeed ("Overall Speed", Float) = 1
		[Space(33)] [Header(Color)] [Space(13)] _Color ("Color", 2D) = "white" {}
		_ColorUVScale ("Color UV Scale", Vector) = (1,1,0,0)
		_ColorPanSpeed ("Color Pan Speed", Vector) = (0,0,0,0)
		_Color01 ("Color 01", Vector) = (1,1,1,0)
		_Color02 ("Color 02", Vector) = (1,1,1,0)
		_ColorSmoothstep ("Color Smoothstep", Float) = 0
		_ColorSmoothstepSmoothness ("Color Smoothstep Smoothness", Float) = 1
		[Space(33)] [Header(Distortion)] [Space(13)] _Distortion ("Distortion", 2D) = "white" {}
		_DistortionUVScale ("Distortion UV Scale", Vector) = (1,1,0,0)
		_DistortionPanSpeed ("Distortion Pan Speed", Vector) = (0,0,0,0)
		_DistortionAmount ("Distortion Amount", Float) = 0.1
		[Space(33)] [Header(Erosion)] [Space(13)] _Erosion ("Erosion", 2D) = "white" {}
		_ErosionUVScale ("Erosion UV Scale", Vector) = (1,1,0,0)
		_ErosionPanSpeed ("Erosion Pan Speed", Vector) = (0,0,0,0)
		_ErosionSmoothstep ("Erosion Smoothstep", Float) = 0
		_ErosionSmoothstepSmoothness ("Erosion Smoothstep Smoothness", Float) = 1
		[Space(33)] [Header(Mask)] [Space(13)] _Mask ("Mask", 2D) = "white" {}
		_MaskUVScale ("Mask UV Scale", Vector) = (1,1,0,0)
		_MaskPanSpeed ("Mask Pan Speed", Vector) = (0,0,0,0)
		_MaskDistortionIntensity ("Mask Distortion Intensity", Float) = 1
		_MaskSmoothstep ("Mask Smoothstep", Float) = 0
		_MaskSmoothstepSmoothness ("Mask Smoothstep Smoothness", Float) = 1
		[Space(33)] [Header(AR)] [Space(13)] _Cull ("Cull", Float) = 2
		_Src ("Src", Float) = 5
		_Dst ("Dst", Float) = 10
		_ZWrite ("ZWrite", Float) = 0
		_ZTest ("ZTest", Float) = 2
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
		[ToggleOff] [HideInInspector] _ReceiveShadows ("Receive Shadows", Float) = 1
	}
	SubShader{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
		LOD 200
		Cull [_Cull]
		ZWrite [_ZWrite]
		ZTest [_ZTest]
		Blend [_Src] [_Dst]

		Pass
		{
			CGPROGRAM
			// Real replacement for a DummyShaderTextExporter stub (baked to flat white). Built-in RP (no URP
			// package in this project), written as a classic Vefects-style layered fire/energy trail: a
			// Color texture drives a two-stop gradient (Color01->Color02) via smoothstep, a Distortion
			// texture offsets UVs for a flickering/flowing look, an Erosion texture creates a burning dissolve
			// edge, and a Mask texture further shapes opacity - matching the real property set the material
			// already has values for (Emissive Intensity, pan speeds, UV scales, smoothstep controls, etc).
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR;
			};

			sampler2D _Color;
			float4 _ColorUVScale;
			float4 _ColorPanSpeed;
			float4 _Color01;
			float4 _Color02;
			float _ColorSmoothstep;
			float _ColorSmoothstepSmoothness;

			sampler2D _Distortion;
			float4 _DistortionUVScale;
			float4 _DistortionPanSpeed;
			float _DistortionAmount;

			sampler2D _Erosion;
			float4 _ErosionUVScale;
			float4 _ErosionPanSpeed;
			float _ErosionSmoothstep;
			float _ErosionSmoothstepSmoothness;

			sampler2D _Mask;
			float4 _MaskUVScale;
			float4 _MaskPanSpeed;
			float _MaskDistortionIntensity;
			float _MaskSmoothstep;
			float _MaskSmoothstepSmoothness;

			float _EmissiveIntensity;
			float _AlphaAffectsOpacity;
			float _OverallSpeed;

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.color = v.color;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float t = _Time.y * _OverallSpeed;

				float2 distUV = i.uv * _DistortionUVScale.xy + _DistortionPanSpeed.xy * t;
				float2 distortion = (tex2D(_Distortion, distUV).rg - 0.5) * 2.0 * _DistortionAmount;

				float2 colorUV = i.uv * _ColorUVScale.xy + _ColorPanSpeed.xy * t + distortion;
				float colorSample = tex2D(_Color, colorUV).r;
				float colorLerp = smoothstep(_ColorSmoothstep, _ColorSmoothstep + max(_ColorSmoothstepSmoothness, 0.0001), colorSample);
				fixed3 gradient = lerp(_Color01.rgb, _Color02.rgb, colorLerp);

				float2 erosionUV = i.uv * _ErosionUVScale.xy + _ErosionPanSpeed.xy * t + distortion;
				float erosionSample = tex2D(_Erosion, erosionUV).r;
				float erosionMask = smoothstep(_ErosionSmoothstep, _ErosionSmoothstep + max(_ErosionSmoothstepSmoothness, 0.0001), erosionSample);

				float2 maskUV = i.uv * _MaskUVScale.xy + _MaskPanSpeed.xy * t + distortion * _MaskDistortionIntensity;
				float maskSample = tex2D(_Mask, maskUV).r;
				float maskMask = smoothstep(_MaskSmoothstep, _MaskSmoothstep + max(_MaskSmoothstepSmoothness, 0.0001), maskSample);

				fixed4 colorTex = tex2D(_Color, colorUV);
				float texAlpha = lerp(1.0, colorTex.a, saturate(_AlphaAffectsOpacity));

				fixed3 finalColor = gradient * _EmissiveIntensity * i.color.rgb;
				float finalAlpha = erosionMask * maskMask * texAlpha * i.color.a;

				return fixed4(finalColor, finalAlpha);
			}
			ENDCG
		}
	}
	Fallback Off
}
