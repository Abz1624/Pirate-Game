Shader "Stylized Water 3/Dynamic Effect/Default" {
	Properties {
		[MaterialEnum(Off,0,Front,1,Back,2)] _Cull ("Cull", Float) = 0
		_BaseMap ("Texture (R=Height mask, G=Foam Mask)", 2D) = "white" {}
		[Toggle] _RemapDisplacement ("Remap displacement", Float) = 0
		_AnimationSpeed ("Panning Speed (XY)", Vector) = (0,0,0,0)
		_MaskMap ("Mask", 2D) = "white" {}
		_MaskAnimationSpeed ("Panning Speed (XY)", Vector) = (0,0,0,0)
		[Header(Output)] _HeightScale ("Displacement scale", Float) = 0.1
		_FoamStrength ("Foam strength", Float) = 1
		_NormalStrength ("Normal strength", Float) = 1
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
}