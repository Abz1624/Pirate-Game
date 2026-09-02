Shader "TextMeshPro/Distance Field" {
	Properties {
		_FaceTex ("Face Texture", 2D) = "white" {}
		_FaceColor ("Face Color", Color) = (1,1,1,1)
		_FaceDilate ("Face Dilate", Range(-1,1)) = 0
		_OutlineColor ("Outline Color", Color) = (0,0,0,1)
		_OutlineTex ("Outline Texture", 2D) = "white" {}
		_OutlineWidth ("Outline Thickness", Range(0,1)) = 0
		_OutlineSoftness ("Outline Softness", Range(0,1)) = 0
		_Bevel ("Bevel", Range(0,1)) = 0.5
		_BevelOffset ("Bevel Offset", Range(-0.5,0.5)) = 0
		_BevelWidth ("Bevel Width", Range(-0.5,0.5)) = 0
		_BevelClamp ("Bevel Clamp", Range(0,1)) = 0
		_BevelRoundness ("Bevel Roundness", Range(0,1)) = 0
		_LightAngle ("Light Angle", Range(0, 6.2831853)) = 3.1416
		_SpecularColor ("Specular", Color) = (1,1,1,1)
		_SpecularPower ("Specular power", Range(0,4)) = 2
		_Reflectivity ("Reflectivity Power", Range(5.0,15.0)) = 10
		_Diffuse ("Diffuse shading", Range(0,1)) = 0.5
		_Ambient ("Ambient Shading", Range(1,0)) = 0.5
		_BumpMap ("Normal map", 2D) = "bump" {}
		_BumpOutline ("Bump Outline", Range(0,1)) = 0
		_BumpFace ("Bump Face", Range(0,1)) = 0
		_ReflectFaceColor ("Face Color", Color) = (0,0,0,1)
		_ReflectOutlineColor ("Outline Color", Color) = (0,0,0,1)
		_Cube ("Reflection Cubemap", Cube) = "black" {}
		_EnvMatrixRotation ("Environment Matrix Rotation", Vector) = (0,0,0,0)
		_UnderlayColor ("Border Color", Color) = (0,0,0,0.5)
		_UnderlayOffsetX ("Underlay OffsetX", Range(-1,1)) = 0
		_UnderlayOffsetY ("Underlay OffsetY", Range(-1,1)) = 0
		_UnderlayDilate ("Underlay Dilate", Range(-1,1)) = 0
		_UnderlaySoftness ("Underlay Softness", Range(0,1)) = 0
		_WeightNormal ("Weight Normal", Float) = 0
		_WeightBold ("Weight Bold", Float) = 0.5
		_ShaderFlags ("Flags", Float) = 0
		_ScaleRatioA ("Scale RatioA", Float) = 1
		_ScaleRatioB ("Scale RatioB", Float) = 1
		_ScaleRatioC ("Scale RatioC", Float) = 1
		_MainTex ("Font Atlas", 2D) = "white" {}
		_TextureWidth ("Texture Width", Float) = 512
		_TextureHeight ("Texture Height", Float) = 512
		_GradientScale ("Gradient Scale", Float) = 5
		_ScaleX ("Scale X", Float) = 1
		_ScaleY ("Scale Y", Float) = 1
		_PerspectiveFilter ("Perspective Correction", Range(0,1)) = 0.875
		_Sharpness ("Sharpness", Range(-1,1)) = 0
		_VertexOffsetX ("Vertex OffsetX", Float) = 0
		_VertexOffsetY ("Vertex OffsetY", Float) = 0
		_ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
		_MaskSoftnessX ("Mask SoftnessX", Float) = 0
		_MaskSoftnessY ("Mask SoftnessY", Float) = 0
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_CullMode ("Cull Mode", Float) = 0
		_ColorMask ("Color Mask", Float) = 15
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
