Shader "Shader Graphs/EnemySailShader" {
	Properties {
		[NoScaleOffset] _BaseTexture ("BaseTexture", 2D) = "white" {}
		[NoScaleOffset] _DamageTexture_1 ("DamageTexture_1", 2D) = "white" {}
		[NoScaleOffset] _DamageTexture_2 ("DamageTexture_2", 2D) = "white" {}
		[NoScaleOffset] _DamageTexture_3 ("DamageTexture_3", 2D) = "white" {}
		_Damage ("Damage", Range(0, 1)) = 0
		_WindAmplitude ("WindAmplitude", Float) = 0.03
		_WindFrequency ("WindFrequency", Float) = 0.7
		_RippleAmpitude ("RippleAmpitude", Float) = 0.005
		_RippleFrequency ("RippleFrequency", Float) = 6
		_PhaseSpread ("PhaseSpread", Float) = 1.5
		_PinRadius ("PinRadius", Float) = 0.4
		_TopFadeStart ("TopFadeStart", Float) = 0.85
		_NoiseAmplitude ("NoiseAmplitude", Float) = 0.0005
		_NoiseSpeed ("NoiseSpeed", Float) = 0.4
		_NoiseScale ("NoiseScale", Float) = 3
		[HDR] _EmissionColor ("EmissionColor", Vector) = (0,0,0,1)
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