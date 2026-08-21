Shader "Shader Graphs/MapBorder" {
	Properties {
		_Speed ("Speed", Vector) = (0,0,0,0)
		[HDR] _Color ("Color", Vector) = (0,0,0,0)
		[NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}

	SubShader {
		Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		LOD 200

		Pass {
			Name "ForwardUnlit"
			Tags { "LightMode"="UniversalForward" }

			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			ZTest LEqual
			Cull Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _Speed;
				float4 _Color;
				float4 _MainTex_ST;
			CBUFFER_END

			struct Attributes {
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings {
				float4 positionHCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings vert(Attributes IN) {
				Varyings OUT = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

				VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
				OUT.positionHCS = vpi.positionCS;
				OUT.uv = (IN.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw + (_Speed.xy * _Time.y);
				OUT.color = IN.color;
				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target {
				UNITY_SETUP_INSTANCE_ID(IN);
				half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
				half4 col = tex * _Color * IN.color;
				clip(col.a - 0.001);
				return col;
			}
			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
}
