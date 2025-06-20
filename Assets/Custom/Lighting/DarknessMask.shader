Shader "Custom/Lighting/DarknessMask"
{
    Properties
    {
        _MainTex   ("Mask (uses alpha)", 2D) = "white" {}
        [PowerSlider(3)]
        _Strength  ("Darkness Strength", Range(0,1)) = 1
        _Tint      ("Tint Colour", Color) = (0,0,0,1)
        _Gamma     ("Edge Gamma", Float) = 1
        _Ambient   ("Ambient Light Floor", Range(0,1)) = 0   // NEW
        _Cutoff    ("Alpha Cut-off", Range(0,1)) = 0

        [Header(Blend Settings)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src",  Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst",  Float) = 10
        [Enum(UnityEngine.Rendering.BlendOp  )] _BlendOp  ("Op",   Float) = 0
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
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f     { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 scr:TEXCOORD1; };

            sampler2D _MainTex;
            float4    _MainTex_ST;

            float  _Strength, _Gamma, _Cutoff, _Ambient;
            float4 _Tint;

            // 8×8 Bayer ordered-dither (values 0-63)/64
            static const float2x4 bayer[2] = {
                float2x4( 0,48,12,60, 3,51,15,63),
                float2x4(32,16,44,28,35,19,47,31)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                o.scr = o.pos.xy;               // screen-space for dither
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float  a = tex2D(_MainTex, i.uv).a;

                if (a < _Cutoff) discard;

                // ordered dither – makes the ramp look smoother
                int2 px = int2(i.scr) & 7;                // 0-7 range
                float dither = bayer[px.y >> 2][px.x];    // 0-63
                a -= dither * (1.0/64.0);                 // subtle step
                a = saturate(a);

                // strength / gamma
                a = pow(a * _Strength, _Gamma);

                // lift by ambient floor (ambient 0 = old behaviour)
                a *= 1.0 - _Ambient;

                return fixed4(_Tint.rgb * a, a);          // pre-mult tint
            }
            ENDCG
        }
    }
    FallBack Off
}
