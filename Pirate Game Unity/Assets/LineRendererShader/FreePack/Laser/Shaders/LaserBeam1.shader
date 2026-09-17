Shader "Unlit/LaserBeam1"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color("_Tint",Color)=(0,0,0,0)
        _ColorEmission("Emission Color",Color)=(0,0,0,0)
        _Speed("Speed",vector)=(0,0,0,0)
        _Noise("Noise",vector)=(0,0,0,0) // xy are noise speed// z is noise scale // w is noise power
        _NoiseAmount("Noise Amount",float)=0
        _DissolveAmount("Dissolve Amount",float) =0
        _Emission("Emission",float)=1

    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off


        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;

            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;

            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Speed;
            fixed4 _Color;
            fixed4 _Noise;
            fixed4 _ColorEmission;
            float _DissolveAmount;
            float _NoiseAmount;
            float _Threshold;
            float _Emission;


            
            // simple noise
            inline float unity_noise_randomValue (float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233)))*43758.5453);
            }

            inline float unity_noise_interpolate (float a, float b, float t)
            {
                return (1.0-t)*a + (t*b);
            }

            inline float unity_valueNoise (float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                uv = abs(frac(uv) - 0.5);
                float2 c0 = i + float2(0.0, 0.0);
                float2 c1 = i + float2(1.0, 0.0);
                float2 c2 = i + float2(0.0, 1.0);
                float2 c3 = i + float2(1.0, 1.0);
                float r0 = unity_noise_randomValue(c0);
                float r1 = unity_noise_randomValue(c1);
                float r2 = unity_noise_randomValue(c2);
                float r3 = unity_noise_randomValue(c3);

                float bottomOfGrid = unity_noise_interpolate(r0, r1, f.x);
                float topOfGrid = unity_noise_interpolate(r2, r3, f.x);
                float t = unity_noise_interpolate(bottomOfGrid, topOfGrid, f.y);
                return t;
            }

            void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
            {
                float t = 0.0;

                float freq = pow(2.0, float(0));
                float amp = pow(0.5, float(3-0));
                t += unity_valueNoise(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

                freq = pow(2.0, float(1));
                amp = pow(0.5, float(3-1));
                t += unity_valueNoise(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

                freq = pow(2.0, float(2));
                amp = pow(0.5, float(3-2));
                t += unity_valueNoise(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

                Out = t;
            }

            // lerp
            void Unity_Lerp_float2(float2 A, float2 B, float2 T, out float2 Out)
            {
                Out = lerp(A, B, T);
            }

            // power
            void Unity_Power_float2(float2 A, float2 B, out float2 Out)
            {
                Out = pow(A, B);
            }



            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 noiseUVInput;
                noiseUVInput=i.uv.xy+_Time.y*_Noise.xy;
                float outSimpleNoise;
                Unity_SimpleNoise_float(i.uv.xy,_Noise.z,outSimpleNoise);
                float noiseAfterPower;
                noiseAfterPower=pow(outSimpleNoise, _Noise.w);
                float2 lerpOut;
                lerpOut=lerp(i.uv.xy,noiseAfterPower.xx,_NoiseAmount);
                float4 uvInputCol1;
                uvInputCol1=lerpOut.xyxy+_Speed.xyzw*_Time.y;
                fixed4 col1 = tex2D(_MainTex, uvInputCol1);
                float4 outLerp4;
                outLerp4=lerp(col1.xyzw, noiseAfterPower.xxxx*col1.a, _DissolveAmount);
                fixed4 colResult=outLerp4*i.color*_Color*_ColorEmission;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return colResult*_Emission;
            }
            ENDCG
        }
    }
}
