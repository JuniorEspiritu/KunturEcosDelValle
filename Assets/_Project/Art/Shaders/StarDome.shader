// Domo de estrellas: una esfera gigante alrededor de la cámara con el cielo
// nocturno pintado por dentro.
//
// Tres decisiones que hacen que esto funcione:
//
// 1) Cull Front. Un shader normal dibuja solo las caras que MIRAN a la cámara,
//    y la cámara está DENTRO de la esfera, así que sin esto no se vería nada.
//    Invirtiendo el descarte se dibuja únicamente la cara interior.
//
// 2) Blend One One (aditivo). Las estrellas se SUMAN a lo que ya está pintado,
//    así el fondo negro de la textura no tapa el cielo: donde la textura es
//    negra no aporta nada. Además hace que brillen de verdad.
//
// 3) ZWrite Off y cola Transparent. El domo se dibuja después del terreno y
//    respeta su profundidad, así que las montañas y las casas lo tapan en vez
//    de quedar detrás de un velo de estrellas.
//
// Sin niebla a propósito: el domo está a cientos de metros y la niebla del
// valle lo dejaría de un gris plano.
Shader "Kuntur/StarDome"
{
    Properties
    {
        _MainTex ("Estrellas", 2D) = "black" {}
        _Color ("Tinte (el alfa las va apagando)", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Cull Front
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
                fixed3 stars = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                // El alfa no lo usa la mezcla aditiva, así que se aplica a
                // mano: es el que hace aparecer y desaparecer las estrellas
                // con la hora.
                return fixed4(stars * _Color.a, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
