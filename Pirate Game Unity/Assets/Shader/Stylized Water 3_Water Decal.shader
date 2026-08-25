Shader "Stylized Water 3/Water Decal" {
	Properties {
		[Toggle(_ALIGN_TO_WATER)] _ALIGN_TO_WATER ("Align To Water", Float) = 1
		_Offset ("Offset", Float) = 0.05
		[NoScaleOffset] _BaseMap ("Albedo", 2D) = "white" {}
		[NoScaleOffset] [Normal] _BumpMap ("Normals", 2D) = "bump" {}
		_Mask ("Mask", 2D) = "white" {}
		_Alpha_Clip_Threshold ("Alpha Clip Threshold", Range(0, 1)) = 0.5
		_BaseColor ("Color", Vector) = (1,1,1,1)
		_Specular_Color ("Specular Color", Vector) = (1,1,1,1)
		_Smoothness ("Smoothness", Range(0, 1)) = 0
		_Metalicness ("Metalicness", Range(0, 1)) = 0
		[HideInInspector] _WorkflowMode ("_WorkflowMode", Float) = 0
		[HideInInspector] _CastShadows ("_CastShadows", Float) = 0
		[HideInInspector] _ReceiveShadows ("_ReceiveShadows", Float) = 1
		[HideInInspector] _Surface ("_Surface", Float) = 1
		[HideInInspector] _Blend ("_Blend", Float) = 0
		[HideInInspector] _AlphaClip ("_AlphaClip", Float) = 0
		[HideInInspector] _BlendModePreserveSpecular ("_BlendModePreserveSpecular", Float) = 0
		[HideInInspector] _SrcBlend ("_SrcBlend", Float) = 1
		[HideInInspector] _DstBlend ("_DstBlend", Float) = 0
		[HideInInspector] _SrcBlendAlpha ("_SrcBlendAlpha", Float) = 1
		[HideInInspector] _DstBlendAlpha ("_DstBlendAlpha", Float) = 0
		[ToggleUI] [HideInInspector] _ZWrite ("_ZWrite", Float) = 0
		[HideInInspector] _ZWriteControl ("_ZWriteControl", Float) = 2
		[HideInInspector] _ZTest ("_ZTest", Float) = 4
		[HideInInspector] _Cull ("_Cull", Float) = 2
		[HideInInspector] _AlphaToMask ("_AlphaToMask", Float) = 0
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
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
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}