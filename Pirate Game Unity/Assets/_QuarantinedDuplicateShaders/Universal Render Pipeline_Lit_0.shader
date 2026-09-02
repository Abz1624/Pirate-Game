Shader "Universal Render Pipeline/Lit" {
	Properties {
		[MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
		[MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
		_Cutoff ("Alpha Clipping", Range(0,1)) = 0.5
		_Smoothness ("Smoothness", Range(0,1)) = 0.5
		_SmoothnessTextureChannel ("Smoothness texture channel", Float) = 0
		_Metallic ("Metallic", Range(0,1)) = 0
		_MetallicGlossMap ("Metallic", 2D) = "white" {}
		_SpecularHighlights ("Specular Highlights", Float) = 1
		_EnvironmentReflections ("Environment Reflections", Float) = 1
		_SpecColor ("Specular", Color) = (0.2,0.2,0.2,1)
		_SpecGlossMap ("Specular", 2D) = "white" {}
		_BumpScale ("Scale", Float) = 1
		[Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
		_Parallax ("Height Scale", Range(0.005,0.08)) = 0.005
		_ParallaxMap ("Height Map", 2D) = "black" {}
		_OcclusionStrength ("Strength", Range(0,1)) = 1
		_OcclusionMap ("Occlusion", 2D) = "white" {}
		[HDR] _EmissionColor ("Color", Color) = (0,0,0,1)
		_EmissionMap ("Emission", 2D) = "white" {}
		_DetailMask ("Detail Mask", 2D) = "white" {}
		_DetailAlbedoMapScale ("Scale", Range(0,2)) = 1
		_DetailAlbedoMap ("Detail Albedo x2", 2D) = "linearGrey" {}
		_DetailNormalMapScale ("Scale", Range(0,2)) = 1
		[Normal] _DetailNormalMap ("Normal Map", 2D) = "bump" {}
		_Surface ("__surface", Float) = 0
		_Blend ("__blend", Float) = 0
		_Cull ("__cull", Float) = 2
		[ToggleUI] _AlphaClip ("__clip", Float) = 0
		[HideInInspector] _SrcBlend ("__src", Float) = 1
		[HideInInspector] _DstBlend ("__dst", Float) = 0
		[HideInInspector] _ZWrite ("__zw", Float) = 1
		[ToggleUI] _ReceiveShadows ("Receive Shadows", Float) = 1
		_QueueOffset ("Queue offset", Float) = 0
		[HideInInspector] _MainTex ("BaseMap", 2D) = "white" {}
		[HideInInspector] _Color ("Base Color", Color) = (1,1,1,1)
		[HideInInspector] _GlossMapScale ("Smoothness", Float) = 0.5
		[HideInInspector] _Glossiness ("Smoothness", Float) = 0.5
		[HideInInspector] _GlossyReflections ("EnvironmentReflections", Float) = 1
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
