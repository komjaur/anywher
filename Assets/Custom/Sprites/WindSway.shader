Shader "Custom/Sprites/WindSway"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _WindStrength ("Global wind strength", Range(0,1)) = 1
        _WindDir      ("Wind direction", Vector) = (1,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;

            float     _WindStrength;          // driven at runtime
            float4    _WindDir;               // (x, y) = direction

            // Per-sprite data supplied from C# (see step 3)
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float , _Amp)
                UNITY_DEFINE_INSTANCED_PROP(float , _Freq)
                UNITY_DEFINE_INSTANCED_PROP(float , _Phase)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float amp  = UNITY_ACCESS_INSTANCED_PROP(Props, _Amp);
                float freq = UNITY_ACCESS_INSTANCED_PROP(Props, _Freq);
                float pha  = UNITY_ACCESS_INSTANCED_PROP(Props, _Phase);

                // World-space Y gives each row a slightly different timing
                float sway = sin(_Time.y * freq + v.vertex.y * 0.25 + pha)
                             * amp * _WindStrength;

                v.vertex.xy += normalize(_WindDir.xy) * sway;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = TRANSFORM_TEX(v.uv, _MainTex);
                o.color  = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                return c;
            }
            ENDCG
        }
    }
}
