Shader "Custom/Lighting/AdditiveLight"
{
    Properties
    {
        _MainTex ("Lightmap (RGB)", 2D) = "white" {}
        [HDR]_Tint ("Global Tint", Color) = (1,1,1,1)
        [PowerSlider(4)] _Intensity ("Intensity", Float) = 1
        _Gamma      ("Gamma Curve", Float) = 1
        _Cutoff     ("Alpha Cut-off", Range(0,1)) = 0
        _UseAlphaAsMask ("Multiply RGB by Alpha", Float) = 0

        /* NEW */
        _ToneMap  ("ACES Tone-map Strength", Range(0,1)) = .5
        _SatBoost ("Saturation After Tone-map", Range(0,2)) = 1

        [Header(Blend Settings)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src",  Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst",  Float) = 1
        [Enum(UnityEngine.Rendering.BlendOp  )] _BlendOp  ("Op",   Float) = 3
    }

    SubShader
    {
        Tags{ "Queue"="Transparent" "RenderType"="Transparent"
              "IgnoreProjector"="True" "PreviewType"="Plane" }

        Blend   [_SrcBlend] [_DstBlend]
        BlendOp [_BlendOp]

        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f     { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            sampler2D _MainTex;
            float4    _MainTex_ST;

            float4 _Tint;  float _Intensity,_Gamma,_Cutoff,_UseAlphaAsMask;
            float  _ToneMap,_SatBoost;

            /* ---------- helpers ---------- */
            float3 ApplyACES(float3 x)
            {   // ACES approximation by Narkowicz 2015
                const float a=2.51, b=0.03, c=2.43, d=0.59, e=0.14;
                return saturate((x*(a*x+b))/(x*(c*x+d)+e));
            }

            float3 BoostSaturation(float3 c, float boost)
            {
                float l = dot(c, float3(0.2126,0.7152,0.0722));
                return lerp(float3(l,l,l), c, boost);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
                if (tex.a < _Cutoff) discard;

                half3 rgb = tex.rgb;

                // optional edge mask
                rgb *= lerp(1.0, tex.a, _UseAlphaAsMask);

                // artist controls
                rgb = pow(rgb * _Intensity, _Gamma) * _Tint.rgb;

                // ACES tone-map (strength 0..1)
                rgb = lerp(rgb, ApplyACES(rgb), _ToneMap);

                // saturation restore
                rgb = BoostSaturation(rgb, _SatBoost);

                return half4(rgb, 1);   // additive: alpha ignored
            }
            ENDCG
        }
    }
    FallBack Off
}
