Shader "Shader Graphs/SandShader" {
	Properties {
		_BaseColor ("BaseColor", Vector) = (0.9056604,0.7888306,0.4912781,1)
		_DarkLineColor ("DarkLineColor", Vector) = (0.9150943,0.6850769,0.366901,1)
		_HighlightColor ("HighlightColor", Vector) = (0.9811321,0.9086897,0.5599858,1)
		_RingDensity ("RingDensity", Float) = 8
		_LineWidth ("LineWidth", Float) = 0.12
		_LineSoftness ("LineSoftness", Float) = 0.05
		_DistotionScale ("DistotionScale", Float) = 3
		_DistortionAmount ("DistortionAmount", Float) = 0.15
		_FineNoiseScale ("FineNoiseScale", Float) = 35
		_FineNoiseAmount ("FineNoiseAmount", Float) = 0.08
		_NormalStrength ("NormalStrength", Float) = 0.04
		_Smoothness ("Smoothness", Float) = 0.25
		_CenterFadeSoftness ("CenterFadeSoftness", Float) = 0.35
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