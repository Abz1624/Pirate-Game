Shader "Shader Graphs/FlagWindShader" {
	Properties {
		_Amplitude ("Amplitude", Float) = 0.2
		_Frequency ("Frequency", Float) = 1
		_Amount ("Amount", Float) = 0
		_NoiseScale ("NoiseScale", Float) = 5
		_Color ("Color", Vector) = (1,1,1,1)
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Tiling ("Tiling", Vector) = (1,1,0,0)
		_Offset ("Offset", Vector) = (0,0,0,0)
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}

	SubShader {
		Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		LOD 200

		Pass {
			Name "ForwardLit"
			Tags { "LightMode"="UniversalForward" }

			Cull Off
			ZWrite On

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			TEXTURE2D(_Texture); SAMPLER(sampler_Texture);

			CBUFFER_START(UnityPerMaterial)
				float _Amplitude;
				float _Frequency;
				float _Amount;
				float _NoiseScale;
				float4 _Color;
				float4 _Tiling;
				float4 _Offset;
			CBUFFER_END

			struct Attributes {
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings {
				float4 positionHCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				float3 positionWS : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings vert(Attributes IN) {
				Varyings OUT = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

				float weight = saturate(IN.uv.x);
				float t = _Time.y * max(_Amount, 0.0001);
				float wave = sin(_Frequency * 6.2831853 * weight + t) * _Amplitude;
				float noise = sin(IN.positionOS.y * _NoiseScale + t * 1.7) * _Amplitude * 0.35;

				float4 positionOS = IN.positionOS;
				positionOS.z += (wave + noise) * weight;

				VertexPositionInputs vpi = GetVertexPositionInputs(positionOS.xyz);
				OUT.positionHCS = vpi.positionCS;
				OUT.positionWS = vpi.positionWS;
				OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
				OUT.uv = (IN.uv * _Tiling.xy) + _Offset.xy;
				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target {
				UNITY_SETUP_INSTANCE_ID(IN);
				half4 tex = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, IN.uv);
				half4 col = tex * _Color;
				clip(col.a - 0.5);

				Light mainLight = GetMainLight();
				float3 normalWS = normalize(IN.normalWS);
				half ndotl = saturate(dot(normalWS, mainLight.direction)) * 0.6 + 0.4;
				col.rgb *= ndotl * mainLight.color;
				return col;
			}
			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
}
