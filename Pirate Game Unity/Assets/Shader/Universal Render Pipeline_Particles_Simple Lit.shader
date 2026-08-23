Shader "Universal Render Pipeline/Particles/Simple Lit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _SpecMap ("Specular", 2D) = "white" {}
        _SpecColor ("Specular", Color) = (0.2,0.2,0.2,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _SmoothnessScale ("Scale", Float) = 1
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _Color ("Color", Vector) = (0,0,0,1)
        _EmissionMap ("Emission", 2D) = "white" {}
        _EmissionColor ("Color", Color) = (0,0,0,1)
        _Surface ("__surface", Float) = 0
        _Blend ("__blend", Float) = 0
        _Cull ("__cull", Float) = 2
        [ToggleUI] _AlphaClip ("__clip", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
        [HideInInspector] _MainTex ("BaseMap", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        Cull [_Cull]
        ZWrite [_ZWrite]
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            // Real replacement for a DummyShaderTextExporter stub (baked to flat white). Built-in RP
            // (no URP package here), so this is a plain CGPROGRAM particle shader: base texture x vertex
            // color x tint for the splash sprite, alpha-blended per the material's own Src/Dst blend
            // properties, with a simple normal-mapped specular kick for a bit of wet shine.
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float3 worldNormal : TEXCOORD1;
            };

            sampler2D _BaseMap;
            fixed4 _BaseColor;
            sampler2D _SpecMap;
            fixed4 _SpecColor;
            half _Smoothness;
            sampler2D _BumpMap;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_BaseMap, i.uv);
                fixed4 col = tex * _BaseColor * i.color;

                fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                half ndotl = saturate(dot(normalize(i.worldNormal), lightDir)) * 0.5 + 0.5;
                fixed specMask = tex2D(_SpecMap, i.uv).r;
                col.rgb *= ndotl;
                col.rgb += _SpecColor.rgb * specMask * _Smoothness * col.a;

                return col;
            }
            ENDCG
        }
    }
    FallBack "Transparent/VertexLit"
}
