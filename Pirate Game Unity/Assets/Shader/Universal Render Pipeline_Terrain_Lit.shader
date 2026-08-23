Shader "Universal Render Pipeline/Terrain/Lit" {
	Properties {
		[ToggleUI] [HideInInspector] _EnableHeightBlend ("EnableHeightBlend", Float) = 0
		_HeightTransition ("Height Transition", Range(0, 1)) = 0
		[HideInInspector] [PerRendererData] _NumLayersCount ("Total Layer Count", Float) = 1
		[HideInInspector] _Control ("Control (RGBA)", 2D) = "red" {}
		[HideInInspector] _Splat3 ("Layer 3 (A)", 2D) = "grey" {}
		[HideInInspector] _Splat2 ("Layer 2 (B)", 2D) = "grey" {}
		[HideInInspector] _Splat1 ("Layer 1 (G)", 2D) = "grey" {}
		[HideInInspector] _Splat0 ("Layer 0 (R)", 2D) = "grey" {}
		[HideInInspector] _Normal3 ("Normal 3 (A)", 2D) = "bump" {}
		[HideInInspector] _Normal2 ("Normal 2 (B)", 2D) = "bump" {}
		[HideInInspector] _Normal1 ("Normal 1 (G)", 2D) = "bump" {}
		[HideInInspector] _Normal0 ("Normal 0 (R)", 2D) = "bump" {}
		[HideInInspector] _Mask3 ("Mask 3 (A)", 2D) = "grey" {}
		[HideInInspector] _Mask2 ("Mask 2 (B)", 2D) = "grey" {}
		[HideInInspector] _Mask1 ("Mask 1 (G)", 2D) = "grey" {}
		[HideInInspector] _Mask0 ("Mask 0 (R)", 2D) = "grey" {}
		[HideInInspector] [Gamma] _Metallic0 ("Metallic 0", Range(0, 1)) = 0
		[HideInInspector] [Gamma] _Metallic1 ("Metallic 1", Range(0, 1)) = 0
		[HideInInspector] [Gamma] _Metallic2 ("Metallic 2", Range(0, 1)) = 0
		[HideInInspector] [Gamma] _Metallic3 ("Metallic 3", Range(0, 1)) = 0
		[HideInInspector] _Smoothness0 ("Smoothness 0", Range(0, 1)) = 0.5
		[HideInInspector] _Smoothness1 ("Smoothness 1", Range(0, 1)) = 0.5
		[HideInInspector] _Smoothness2 ("Smoothness 2", Range(0, 1)) = 0.5
		[HideInInspector] _Smoothness3 ("Smoothness 3", Range(0, 1)) = 0.5
		[HideInInspector] _MainTex ("BaseMap (RGB)", 2D) = "grey" {}
		[HideInInspector] _BaseColor ("Main Color", Vector) = (1,1,1,1)
		[HideInInspector] _TerrainHolesTexture ("Holes Map (RGB)", 2D) = "white" {}
		[ToggleUI] _EnableInstancedPerPixelNormal ("Enable Instanced per-pixel normal", Float) = 1
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy);
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Universal Render Pipeline/FallbackError"
	//CustomEditor "UnityEditor.Rendering.Universal.TerrainLitShaderGUI"
}