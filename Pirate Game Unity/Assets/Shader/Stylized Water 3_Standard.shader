Shader "Stylized Water 3/Standard" {
	Properties {
		[Toggle] _ZWrite ("Depth writing", Float) = 0
		[Toggle] _ZClip ("Camera frustum clipping", Float) = 1
		[Enum(Both,0,Back,1,Front,2)] _Cull ("Render faces", Float) = 2
		[MaterialEnum(Performance, 0,Appearance, 1)] _ShadingMode ("Shading mode", Float) = 1
		[MaterialEnum(Mesh UV,0,World XZ projected ,1)] _WorldSpaceUV ("UV Coordinates", Float) = 1
		_Direction ("Animation direction", Vector) = (0,-1,0,0)
		_Speed ("Animation Speed", Float) = 1
		_SlopeAngleThreshold ("Angle° threshold", Range(0, 90)) = 15
		_SlopeAngleFalloff ("Angle° falloff", Range(15, 90)) = 25
		_SlopeStretching ("Slope UV stretch", Range(0, 1)) = 0.5
		_SlopeSpeed ("Slope speed multiplier", Float) = 2
		_SlopeFoam ("River slope foam", Range(0, 3)) = 1
		[HDR] _BaseColor ("Deep", Vector) = (0,0.44,0.62,1)
		[HDR] _ShallowColor ("Shallow", Vector) = (0.1,0.9,0.89,0.02)
		[PowerSlider(3)] _ColorAbsorption ("Color Absorption", Range(0, 1)) = 0
		_WaveTint ("Wave tint", Range(-0.1, 0.1)) = 0
		[HDR] _HorizonColor ("Horizon", Vector) = (0.84,1,1,0.15)
		_HorizonDistance ("Horizon Distance", Range(0.01, 32)) = 8
		[Toggle] _VertexColorTransparency ("Vertex color (G) transparency", Float) = 0
		[MaterialEnum(Depth Texture,0,Vertex Color (G),1)] _FogSource ("Fog source", Float) = 0
		_DepthVertical ("View Depth", Range(0.01, 16)) = 4
		_DepthHorizontal ("Vertical Height Depth", Range(0.01, 8)) = 1
		_EdgeFade ("Edge Fade", Float) = 0.1
		_ShadowStrength ("Shadow Strength", Range(0, 1)) = 1
		_TranslucencyStrength ("Translucency Strength", Range(0, 3)) = 1
		_TranslucencyStrengthDirect ("Translucency Strength (Direct)", Range(0, 0.5)) = 0.05
		_TranslucencyExp ("Translucency Exponent", Range(1, 32)) = 4
		_TranslucencyCurvatureMask ("Translucency Curvature mask", Range(0, 1)) = 0.75
		_TranslucencyReflectionMask ("Translucency Reflection mask", Range(0, 1)) = 1
		_CausticsBrightness ("Brightness", Float) = 2
		_CausticsChromance ("Chromance", Range(0, 1)) = 1
		_CausticsTiling ("Tiling", Float) = 0.5
		_CausticsSpeed ("Speed multiplier", Float) = 0.1
		_CausticsDistortion ("Distortion", Range(0, 1)) = 0.15
		[SingleLineTexture] [NoScaleOffset] _CausticsTex ("Texture", 2D) = "black" {}
		[Toggle] _EnableDirectionalCaustics ("Directional Caustics", Float) = 0
		_UnderwaterSurfaceSmoothness ("Underwater Surface Smoothness", Range(0, 1)) = 0.8
		_UnderwaterRefractionOffset ("Underwater Refraction Offset", Range(0, 1)) = 0.2
		_RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.1
		_RefractionChromaticAberration ("Refraction Chromatic Aberration)", Range(0, 1)) = 1
		[MaterialEnum(Depth Texture,0,Vertex Color (R),1,Depth Texture and Vertex Color,2)] _IntersectionSource ("Intersection source", Float) = 0
		[SingleLineTexture] [NoScaleOffset] _IntersectionNoise ("Intersection noise", 2D) = "white" {}
		_IntersectionColor ("Color", Vector) = (1,1,1,1)
		_IntersectionLength ("Distance", Range(0.01, 10)) = 3
		_IntersectionFalloff ("Falloff", Range(0.01, 1)) = 0.5
		[Toggle] _IntersectionSharp ("Sharp", Float) = 1
		_IntersectionClipping ("Cutoff", Range(0.01, 1)) = 0.5
		_IntersectionTiling ("Noise Tiling", Float) = 0.2
		_IntersectionSpeed ("Speed multiplier", Float) = 0.1
		_IntersectionRippleDist ("Ripple frequency", Float) = 32
		_IntersectionRippleStrength ("Ripple Strength", Range(0, 1)) = 0.5
		_IntersectionRippleSpeed ("Ripple Speed", Float) = 2
		_IntersectionDistortion ("Distortion", Range(0, 1)) = 0.2
		[SingleLineTexture] [NoScaleOffset] _FoamTex ("Foam Mask", 2D) = "black" {}
		_FoamColor ("Color", Vector) = (1,1,1,1)
		_FoamSpeed ("Speed multiplier", Float) = 0.1
		_FoamSubSpeed ("Speed multiplier (sub-layer)", Float) = -0.25
		_FoamBaseAmount ("Base amount", Range(0, 1)) = 0
		_FoamClipping ("Clipping", Range(0, 0.999)) = 0
		_FoamStrength ("Strength", Float) = 1
		[MinMaxSlider(0, 3)] _FoamCrestMinMaxHeight ("Wave crest min/max height", Vector) = (1,2,0,0)
		[MinMaxSlider(10, 500)] _DistanceFoamFadeDist ("Distance foam blend (Start/End)", Vector) = (100,350,0,0)
		_DistanceFoamTiling ("Distance foam: Tiling multiplier", Float) = 0.2
		_FoamBubblesSpread ("Foam bubbles spread", Range(0, 2)) = 1
		_FoamBubblesStrength ("Foam ubbles", Range(0, 1)) = 0.1
		_FoamTiling ("Tiling", Vector) = (0.1,0.1,0,0)
		_FoamSubTiling ("Tiling (sub-layer)", Float) = 0.5
		_FoamDistortion ("Distortion", Range(0, 3)) = 0
		[Toggle] _VertexColorFoam ("Vertex color (A) foam", Float) = 0
		[SingleLineTexture] [NoScaleOffset] _FoamTexDynamic ("Foam (Dynamic)", 2D) = "black" {}
		_FoamTilingDynamic ("Tiling (Dynamic)", Float) = 0.1
		_FoamSubTilingDynamic ("Tiling (sub-layer)", Float) = 2
		_FoamSpeedDynamic ("Speed multiplier", Float) = 0.1
		_FoamSubSpeedDynamic ("Speed multiplier (sub-layer)", Float) = -0.1
		[SingleLineTexture] [NoScaleOffset] [Normal] _BumpMap ("Normals", 2D) = "bump" {}
		[SingleLineTexture] [NoScaleOffset] [Normal] _BumpMapSlope ("Normals (River slopes)", 2D) = "bump" {}
		_NormalTiling ("Tiling", Vector) = (0.5,0.5,0,0)
		_NormalSubTiling ("Tiling (sub-layer)", Float) = 0.5
		_NormalStrength ("Strength", Range(0, 1)) = 0.135
		_NormalSpeed ("Speed multiplier", Float) = 1
		_NormalSubSpeed ("Speed multiplier (sub-layer)", Float) = -0.5
		[SingleLineTexture] [NoScaleOffset] [Normal] _BumpMapLarge ("Normals (Distance)", 2D) = "bump" {}
		_DistanceNormalsFadeDist ("Distance normals blend (Start/End)", Vector) = (100,300,0,0)
		_DistanceNormalsTiling ("Distance normals: Tiling multiplier", Float) = 0.15
		_SparkleIntensity ("Sparkle Intensity", Range(0, 10)) = 0
		_SparkleSize ("Sparkle Size", Range(0, 1)) = 0.28
		[PowerSlider(0.1)] _SunReflectionSize ("Sun Size", Range(0, 1)) = 0.5
		_SunReflectionStrength ("Sun Strength", Float) = 10
		_SunReflectionDistortion ("Sun Distortion", Range(0, 2)) = 0.49
		_PointSpotLightReflectionStrength ("Point/spot light strength", Float) = 10
		[PowerSlider(0.1)] _PointSpotLightReflectionSize ("Point/spot light size", Range(0, 1)) = 0
		_PointSpotLightReflectionDistortion ("Point/spot light distortion", Range(0, 1)) = 0.5
		_ReflectionStrength ("Strength", Range(0, 1)) = 1
		_ReflectionDistortion ("Distortion", Range(0, 1)) = 0.05
		_ReflectionBlur ("Probe Blur Factor", Range(0, 1)) = 0
		_ReflectionFresnel ("Curvature mask", Range(0.01, 20)) = 5
		_ReflectionLighting ("Lighting influence", Range(0, 1)) = 0
		_PlanarReflection ("Planar Reflections", 2D) = "" {}
		[Toggle] _ScreenSpaceReflectionsEnabled ("Screen-space Reflections", Float) = 0
		_PlanarReflectionsEnabled ("Planar Enabled", Float) = 0
		[WaveProfile] _WaveProfile ("Wave Profile", 2D) = "black" {}
		_WaveSpeed ("Speed", Float) = 2
		_WaveFrequency ("Frequency", Float) = 1
		_WaveHeight ("Height Scale", Range(0, 1)) = 0.15
		[Toggle] _VertexColorWaveFlattening ("Vertex color (B) wave flattening", Float) = 0
		_WaveNormalStr ("Normal Strength", Range(0, 32)) = 0.1
		_WaveFadeDistance ("Wave fade distance (Start/End)", Vector) = (150,500,0,0)
		_WaveSteepness ("Steepness", Range(0, 5)) = 0.1
		_WaveMaxLayers ("Maximum Layers", Range(1, 64)) = 64
		_WaveDirection ("Direction", Vector) = (1,1,1,1)
		[ToggleOff(_UNLIT)] _LightingOn ("Enable lighting", Float) = 1
		[ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows ("Recieve Shadows", Float) = 1
		[Toggle(_FLAT_SHADING)] _FlatShadingOn ("Flat shading", Float) = 0
		[Toggle(_TRANSLUCENCY)] _TranslucencyOn ("Enable translucency shading", Float) = 1
		[Toggle(_REFRACTION)] _RefractionOn ("Refraction", Float) = 1
		[Toggle(_RIVER)] _RiverModeOn ("River Mode", Float) = 0
		[Toggle(_CAUSTICS)] _CausticsOn ("Caustics ON", Float) = 1
		[ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularReflectionsOn ("Specular Reflections", Float) = 1
		[ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflectionsOn ("Environment Reflections", Float) = 1
		[Toggle(_NORMALMAP)] _NormalMapOn ("Normal maps", Float) = 1
		[Toggle(_INTERSECTION_FOAM)] _IntersectionFoamOn ("Enable intersection foam", Float) = 1
		[Toggle(_DISTANCE_NORMALS)] _DistanceNormalsOn ("Distance normal map", Float) = 0
		[Toggle] _FoamOn ("Surface Foam", Float) = 1
		[Toggle] _FoamDistanceOn ("Surface Foam Distance", Float) = 0
		[Toggle(_DISABLE_DEPTH_TEX)] _DisableDepthTexture ("Disable depth texture", Float) = 0
		[Toggle(_WAVES)] _WavesOn ("Waves", Float) = 0
		[Toggle] _ReceiveDynamicEffectsHeight ("Receive Dynamic Effects Height", Float) = 1
		[Toggle] _ReceiveDynamicEffectsFoam ("Receive Dynamic Effects Foam", Float) = 1
		[Toggle] _ReceiveDynamicEffectsNormal ("Receive Dynamic Effects Normals", Float) = 1
		[HideInInspector] _BaseMap ("Albedo", 2D) = "white" {}
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}

	SubShader{
		Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
		LOD 200

		Pass
		{
			Name "ForwardWater"
			Tags { "LightMode"="UniversalForward" }
			Cull [_Cull]
			ZWrite [_ZWrite]
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float4 screenPos : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				float3 normalWS : TEXCOORD2;
				float4 color : TEXCOORD3;
			};

			TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
			TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _Direction;
				float _Speed;
				float4 _BaseColor;
				float4 _ShallowColor;
				float _ColorAbsorption;
				float _DepthVertical;
				float _EdgeFade;

				float4 _NormalTiling;
				float _NormalSubTiling;
				float _NormalStrength;
				float _NormalSpeed;
				float _NormalSubSpeed;

				float4 _FoamColor;
				float4 _FoamTiling;
				float _FoamSubTiling;
				float _FoamSpeed;
				float _FoamSubSpeed;
				float _FoamStrength;
				float _FoamClipping;
				float _FoamBaseAmount;

				float _SunReflectionSize;
				float _SunReflectionStrength;
				float _SunReflectionDistortion;
				float _ReflectionStrength;
				float _ReflectionFresnel;
				float4 _HorizonColor;
			CBUFFER_END

			Varyings vert (Attributes IN)
			{
				Varyings OUT;
				OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
				OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
				OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
				OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
				OUT.color = IN.color;
				return OUT;
			}

			// Real replacement for a DummyShaderTextExporter stub (baked to flat white). Approximates the
			// Stylized Water 3 "Standard" surface: depth-based deep/shallow color gradient (via the camera
			// depth texture), a dual-panning normal map for surface motion, a Blinn-Phong sun highlight,
			// fresnel-tinted horizon reflection, and a foam texture blended in near shorelines. This is a
			// hand-authored stand-in, not the original Shader Graph, so it won't have every feature (caustics,
			// refraction, planar reflections, wave displacement) of the real asset — but it renders real water
			// instead of solid white.
			half4 frag (Varyings IN) : SV_Target
			{
				float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
				float rawDepth = SampleSceneDepth(screenUV);
				float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
				float waterEyeDepth = IN.screenPos.w;
				float depthDiff = max(sceneEyeDepth - waterEyeDepth, 0.0);
				float depthFactor = saturate(depthDiff / max(_DepthVertical, 0.001));
				depthFactor = pow(depthFactor, lerp(1.0, 3.0, saturate(_ColorAbsorption)));

				float3 normalWS = normalize(IN.normalWS);
				float3 tangentWS = normalize(cross(normalWS, float3(0,0,1)));
				float3 bitangentWS = normalize(cross(normalWS, tangentWS));

				float2 panDir = _Direction.xy;
				float2 uvA = IN.positionWS.xz * _NormalTiling.xy + panDir * _NormalSpeed * _Time.y;
				float2 uvB = IN.positionWS.xz * _NormalTiling.xy * _NormalSubTiling + panDir * _NormalSubSpeed * _Time.y;

				half3 nA = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvA), _NormalStrength);
				half3 nB = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvB), _NormalStrength);
				half3 normalTS = normalize(half3(nA.xy + nB.xy, nA.z * nB.z));

				float3 bumpedNormalWS = normalize(
					tangentWS * normalTS.x +
					bitangentWS * normalTS.y +
					normalWS * normalTS.z);

				float3 viewDirWS = normalize(GetCameraPositionWS() - IN.positionWS);

				half3 waterColor = lerp(_ShallowColor.rgb, _BaseColor.rgb, depthFactor);
				half waterAlpha = lerp(_ShallowColor.a, _BaseColor.a, depthFactor);

				Light mainLight = GetMainLight();
				half ndotl = saturate(dot(bumpedNormalWS, mainLight.direction));
				// Wider 0.4-1.0 range (was 0.55-1.0) so wave-driven normal variation reads as visible
				// light/shadow banding on the surface instead of a near-flat wash.
				half lightMul = saturate(0.4 + 0.6 * ndotl);
				half3 lightColorClamped = saturate(mainLight.color); // avoid HDR light intensity stacking with HDR water color
				half3 diffuse = waterColor * lightMul * lightColorClamped;

				float3 halfDir = normalize(mainLight.direction + viewDirWS);
				half spec = pow(saturate(dot(bumpedNormalWS, halfDir)), lerp(256.0, 4.0, saturate(_SunReflectionSize)));
				half3 sunHighlight = lightColorClamped * spec * _SunReflectionStrength;

				// _ReflectionFresnel is authored (e.g. 0.1) for the original curvature-mask technique, not a
				// plain pow() exponent - used directly, tiny values push fresnel to ~1 across almost the whole
				// visible plane at typical low camera angles, drowning out depth/wave detail under a flat wash.
				// Floor the exponent so the highlight stays a grazing-angle rim instead of a full-surface tint,
				// and only lightly blend it in - per design priority this is a rim accent, not the main read.
				half fresnelExp = max(_ReflectionFresnel * 20.0, 4.0);
				half fresnel = pow(1.0 - saturate(dot(viewDirWS, bumpedNormalWS)), fresnelExp);

				// The horizon color is an intentionally bright HDR tint (for a glinting skyline rim) but with
				// no tonemapping in this scene it was clipping to flat white; normalize its peak to 1 so it
				// still reads as a bright cyan accent instead of blown-out white.
				half3 horizonRGB = _HorizonColor.rgb;
				half horizonPeak = max(horizonRGB.r, max(horizonRGB.g, horizonRGB.b));
				horizonRGB = horizonPeak > 1.0 ? horizonRGB / horizonPeak : horizonRGB;
				half reflectionWeight = saturate(_ReflectionStrength) * fresnel * 0.4; // de-emphasized per design priority
				half3 withReflection = lerp(diffuse, horizonRGB, reflectionWeight);

				float2 foamUvA = IN.positionWS.xz * _FoamTiling.xy + panDir * _FoamSpeed * _Time.y;
				float2 foamUvB = IN.positionWS.xz * _FoamTiling.xy * _FoamSubTiling + panDir * _FoamSubSpeed * _Time.y;
				half foamA = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUvA).r;
				half foamB = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUvB).r;
				half foamNoise = saturate(foamA * 0.6 + foamB * 0.4);

				// Foam should hug the actual coastline (where depthDiff -> 0), not the whole shallow-water
				// zone out to _DepthVertical - the previous version used overall shore proximity, so any
				// broad shallow reef (tightly packed islands, big depthVertical) got whited out entirely.
				half foamBandWidth = max(_DepthVertical * 0.15, 0.5);
				half shoreEdge = 1.0 - saturate(depthDiff / foamBandWidth);
				half foamMask = saturate((shoreEdge + _FoamBaseAmount * 0.3 - _FoamClipping) * _FoamStrength);
				half foamAmount = saturate(foamNoise * foamMask);

				half3 finalColor = lerp(withReflection, _FoamColor.rgb, foamAmount) + sunHighlight;
				// Soft-knee highlight compression: keeps HDR accents (shallow glow, foam, rim) looking bright
				// without hard-clipping to flat white when a few bright terms stack on one pixel.
				finalColor = finalColor / (1.0 + max(0.0, finalColor - 1.0));
				half finalAlpha = saturate(waterAlpha + foamAmount + spec * 0.5);

				return half4(finalColor, finalAlpha);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
