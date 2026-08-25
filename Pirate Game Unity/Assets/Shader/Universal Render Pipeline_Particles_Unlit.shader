Shader "Universal Render Pipeline/Particles/Unlit" {
	Properties {
		[MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
		[MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
		_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
		_BaseColorAddSubDiff ("Base Color Add/Sub/Diff", Vector) = (0,0,0,0)
		[ToggleUI] _FlipbookBlending ("Flipbook Blending", Float) = 0
		[ToggleUI] _SoftParticlesEnabled ("Soft Particles Enabled", Float) = 0
		_SoftParticlesNearFadeDistance ("Soft Particles Near Fade", Float) = 0
		_SoftParticlesFarFadeDistance ("Soft Particles Far Fade", Float) = 1
		[ToggleUI] _CameraFadingEnabled ("Camera Fading Enabled", Float) = 0
		_CameraNearFadeDistance ("Camera Near Fade", Float) = 1
		_CameraFarFadeDistance ("Camera Far Fade", Float) = 2
		[ToggleUI] _DistortionEnabled ("Distortion Enabled", Float) = 0
		_DistortionBlend ("Distortion Blend", Range(0,1)) = 0.5
		_DistortionStrength ("Distortion Strength", Float) = 1
		_EmissionColor ("Color", Color) = (0,0,0,1)
		_Surface ("__surface", Float) = 0
		_Blend ("__blend", Float) = 0
		_Cull ("__cull", Float) = 2
		[ToggleUI] _AlphaClip ("__clip", Float) = 0
		[HideInInspector] _SrcBlend ("__src", Float) = 1
		[HideInInspector] _DstBlend ("__dst", Float) = 0
		[HideInInspector] _ZWrite ("__zw", Float) = 1
		_ColorMode ("Color Mode", Float) = 0
		[HideInInspector] _MainTex ("BaseMap", 2D) = "white" {}
		[HideInInspector] _Color ("Base Color", Color) = (1,1,1,1)
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0); // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/InternalErrorShader"
}
