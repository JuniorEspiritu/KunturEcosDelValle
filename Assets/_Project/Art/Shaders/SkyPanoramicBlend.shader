// Cielo panorámico que MEZCLA dos fotos de cielo.
//
// Por qué hace falta un shader propio: Unity solo sabe mostrar UN skybox a la
// vez, y cambiar de material de golpe al llegar la tarde se ve como un corte
// de película. Este toma dos cielos y los funde con _Blend, así el día se
// vuelve atardecer y el atardecer noche sin ningún salto.
//
// La segunda cosa que resuelve es dónde queda el sol. Estas fotos traen el sol
// pintado en un punto fijo de la imagen; si el cielo se pegara tal cual, el
// sol pintado apuntaría a cualquier lado y no coincidiría con la luz que
// ilumina el pueblo ni con las sombras. Por eso el mapeo está corrido por
// _SunAzimuth: el centro de la foto (u = 0.5) cae SIEMPRE en el azimut que se
// le pase, o sea donde de verdad está el sol de la escena.
Shader "Kuntur/SkyPanoramicBlend"
{
    Properties
    {
        _TexA ("Cielo A", 2D) = "grey" {}
        _TexB ("Cielo B", 2D) = "grey" {}
        _Blend ("Mezcla A -> B", Range(0, 1)) = 0
        _TintA ("Tinte A", Color) = (1, 1, 1, 1)
        _TintB ("Tinte B", Color) = (1, 1, 1, 1)
        _Exposure ("Exposición", Range(0, 4)) = 1
        _SunAzimuth ("Azimut del sol (grados)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            sampler2D _TexA;
            sampler2D _TexB;
            float _Blend;
            float _Exposure;
            float _SunAzimuth;
            fixed4 _TintA;
            fixed4 _TintB;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // En un skybox, la posición del vértice ES la dirección hacia
                // la que se está mirando.
                o.dir = v.vertex.xyz;
                return o;
            }

            // Dirección -> coordenada de una foto equirectangular (la típica
            // panorámica 2:1). u da la vuelta al horizonte, v va del suelo
            // (0) al cenit (1).
            float2 Equirect (float3 d)
            {
                d = normalize(d);
                float azimuth = atan2(d.z, d.x);
                float u = 0.5 + (azimuth - radians(_SunAzimuth)) / (2.0 * UNITY_PI);
                u = frac(u + 1.0);
                float v = 1.0 - acos(clamp(d.y, -1.0, 1.0)) / UNITY_PI;
                return float2(u, v);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = Equirect(i.dir);
                fixed3 a = tex2D(_TexA, uv).rgb * _TintA.rgb;
                fixed3 b = tex2D(_TexB, uv).rgb * _TintB.rgb;
                return fixed4(lerp(a, b, _Blend) * _Exposure, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
