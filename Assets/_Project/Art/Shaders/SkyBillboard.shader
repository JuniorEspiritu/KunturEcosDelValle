// Cartel plano para la luna: un cuadrado que siempre mira a la cámara, lejos,
// en el cielo.
//
// Igual que el domo de estrellas, se suma a lo que ya está pintado (Blend One
// One), así el fondo negro de la textura no tapa el cielo y la luna se ve
// brillando de verdad. La diferencia es Cull Off en vez de Cull Front: un
// cuadrado hay que verlo por su cara de adelante, no por dentro.
Shader "Kuntur/SkyBillboard"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "black" {}
        _Color ("Tinte (el alfa la va apagando)", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Cull Off
        ZWrite Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 c = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                return fixed4(c * _Color.a, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
