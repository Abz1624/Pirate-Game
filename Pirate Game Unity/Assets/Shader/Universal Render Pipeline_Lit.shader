Shader "Universal Render Pipeline/Lit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Clipping", Range(0,1)) = 0.5
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _SmoothnessTextureChannel ("Smoothness texture channel", Float) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        _MetallicGlossMap ("Metallic", 2D) = "white" {}
        _SpecularHighlights ("Specular Highlights", Float) = 1
        _EnvironmentReflections ("Environment Reflections", Float) = 1
        _SpecColor ("Specular", Color) = (0.2,0.2,0.2,1)
        _SpecGlossMap ("Specular", 2D) = "white" {}
        _BumpScale ("Scale", Float) = 1
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _Parallax ("Height Scale", Range(0.005,0.08)) = 0.005
        _ParallaxMap ("Height Map", 2D) = "black" {}
        _OcclusionStrength ("Strength", Range(0,1)) = 1
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        [HDR] _EmissionColor ("Color", Color) = (0,0,0,1)
        _EmissionMap ("Emission", 2D) = "white" {}
        _DetailMask ("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale ("Scale", Range(0,2)) = 1
        _DetailAlbedoMap ("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale ("Scale", Range(0,2)) = 1
        [Normal] _DetailNormalMap ("Normal Map", 2D) = "bump" {}
        _Surface ("__surface", Float) = 0
        _Blend ("__blend", Float) = 0
        _Cull ("__cull", Float) = 2
        [ToggleUI] _AlphaClip ("__clip", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
        [ToggleUI] _ReceiveShadows ("Receive Shadows", Float) = 1
        _QueueOffset ("Queue offset", Float) = 0
        [HideInInspector] _MainTex ("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color ("Base Color", Color) = (1,1,1,1)
        [HideInInspector] _GlossMapScale ("Smoothness", Float) = 0.5
        [HideInInspector] _Glossiness ("Smoothness", Float) = 0.5
        [HideInInspector] _GlossyReflections ("EnvironmentReflections", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        Cull [_Cull]

        CGPROGRAM
        // Real replacement for a DummyShaderTextExporter stub (baked to flat white). This project uses the
        // Built-in Render Pipeline (no URP package installed), so this is written as a standard Surface
        // Shader rather than URP HLSL - it reproduces the real Universal Render Pipeline/Lit property set
        // so every material already using it (100+ in this project) keeps its assigned textures/colors and
        // renders a normal PBR-lit surface instead of solid white.
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _BaseMap;
        fixed4 _BaseColor;
        sampler2D _MetallicGlossMap;
        half _Metallic;
        half _Smoothness;
        sampler2D _BumpMap;
        half _BumpScale;
        sampler2D _OcclusionMap;
        half _OcclusionStrength;
        sampler2D _EmissionMap;
        fixed4 _EmissionColor;
        fixed _Cutoff;
        fixed _AlphaClip;

        struct Input
        {
            float2 uv_BaseMap;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 tex = tex2D(_BaseMap, IN.uv_BaseMap);
            fixed4 albedo = tex * _BaseColor;

            fixed metallicTex = tex2D(_MetallicGlossMap, IN.uv_BaseMap).r;
            fixed occlusion = tex2D(_OcclusionMap, IN.uv_BaseMap).g;
            fixed3 emission = tex2D(_EmissionMap, IN.uv_BaseMap).rgb * _EmissionColor.rgb;
            fixed3 normalTex = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_BaseMap), _BumpScale);

            o.Albedo = albedo.rgb;
            o.Normal = normalTex;
            o.Metallic = metallicTex * _Metallic;
            o.Smoothness = _Smoothness;
            o.Occlusion = lerp(1.0, occlusion, _OcclusionStrength);
            o.Emission = emission;
            o.Alpha = (_AlphaClip > 0.5) ? albedo.a : 1.0;
            if (_AlphaClip > 0.5) clip(albedo.a - _Cutoff);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
