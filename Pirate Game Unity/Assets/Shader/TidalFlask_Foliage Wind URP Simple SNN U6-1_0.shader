Shader "TidalFlask/Foliage Wind URP Simple SNN U6-1" {
	Properties {
		[HideInInspector] _AlphaCutoff ("Alpha Cutoff ", Range(0, 1)) = 0.5
		[HideInInspector] _EmissionColor ("Emission Color", Vector) = (1,1,1,1)
		_BaseTexture ("Base Texture", 2D) = "white" {}
		_AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0.5
		_ShadowRecievedStrength ("Shadow Recieved Strength", Range(0, 1)) = 1
		_GroundFadeColor ("Ground Fade Color", Vector) = (0.3137255,0.3960785,0.1137255,1)
		_GroundFadeHeight ("Ground Fade Height", Range(-5, 0)) = -5
		_GroundFadeContrast ("Ground Fade Contrast", Range(0, 2)) = 1
		_WindStrength ("Wind Strength", Range(0, 1)) = 0.3
		_WindScale ("Wind Scale", Range(0, 1)) = 0.4
		_WindSpeed ("Wind Speed", Vector) = (2,1,0,0)
		[HideInInspector] _texcoord ("", 2D) = "white" {}
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
		[ToggleOff] [HideInInspector] _ReceiveShadows ("Receive Shadows", Float) = 1
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
}