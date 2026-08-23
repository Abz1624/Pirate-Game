Shader "Universal Render Pipeline/Nature/SpeedTree8_PBRLit" {
	Properties {
		[NoScaleOffset] _MainTex ("Base Map", 2D) = "white" {}
		_Color ("Color Tint", Vector) = (1,1,1,1)
		[ToggleUI] _HueVariationKwToggle ("Enable Hue Variation", Float) = 1
		[ToggleUI] _OldHueVarBehavior ("Use Old Hue Variation Behavior", Float) = 0
		_HueVariationColor ("Hue Variation Color", Vector) = (1,0.5,0,0.2)
		[NoScaleOffset] [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
		[ToggleUI] EFFECT_EXTRA_TEX ("Enable Extra Map", Float) = 1
		[NoScaleOffset] _ExtraTex ("Smoothness (R), Metallic (G), AO (B)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0, 1)) = 0.5
		_Metallic ("Metallic", Range(0, 1)) = 0
		[ToggleUI] _SubsurfaceKwToggle ("Enable Subsurface", Float) = 0
		_SubsurfaceIndirect ("Indirect Subsurface Intensity", Range(0, 1)) = 0.25
		_SubsurfaceColor ("Subsurface Color", Vector) = (1,1,1,1)
		[NoScaleOffset] _SubsurfaceTex ("Subsurface Map", 2D) = "black" {}
		_AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0.33
		[ToggleUI] _WindQuality ("Wind Enabled", Float) = 1
		[KeywordEnum(None, Fastest, Fast, Better, Best, Palm)] _WINDQUALITY ("Wind Quality", Float) = 4
		[Toggle(EFFECT_BILLBOARD)] EFFECT_BILLBOARD ("Is Billboard", Float) = 0
		[KeywordEnum(Flip, Mirror, None)] BACKFACE_NORMAL_MODE ("Backface Normal Mode", Float) = 1
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
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
			float4 _Color;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy) * _Color;
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}