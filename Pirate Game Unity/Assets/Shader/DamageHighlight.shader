Shader "Custom/DamageHighlight"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        [HDR] _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount (0-1, driven by hit script)", Range(0,1)) = 0
        _AutoPulse ("Auto Pulse (demo, no script needed)", Range(0,1)) = 1
        _PulseSpeed ("Pulse Speed", Range(0,20)) = 6

        [HDR] _RimColor ("Highlight Rim Color", Color) = (1,0.15,0.1,1)
        _RimPower ("Rim Power", Range(0.1,8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0,5)) = 1.5

        _Glossiness ("Smoothness", Range(0,1)) = 0.3
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _BaseMap;
        fixed4 _BaseColor;
        fixed4 _FlashColor;
        float _FlashAmount;
        float _AutoPulse;
        float _PulseSpeed;
        fixed4 _RimColor;
        float _RimPower;
        float _RimIntensity;
        half _Glossiness;
        half _Metallic;

        struct Input
        {
            float2 uv_BaseMap;
            float3 viewDir;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 tex = tex2D(_BaseMap, IN.uv_BaseMap);
            fixed3 albedo = tex.rgb * _BaseColor.rgb;

            float pulse = _AutoPulse * (sin(_Time.y * _PulseSpeed) * 0.5 + 0.5);
            float flash = saturate(_FlashAmount + pulse * 0.5);
            fixed3 baseColor = lerp(albedo, _FlashColor.rgb, flash);

            float3 worldNormal = WorldNormalVector(IN, o.Normal);
            float rim = pow(saturate(1.0 - dot(normalize(IN.viewDir), worldNormal)), _RimPower) * _RimIntensity;

            o.Albedo = baseColor;
            o.Emission = _RimColor.rgb * rim;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = tex.a * _BaseColor.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
