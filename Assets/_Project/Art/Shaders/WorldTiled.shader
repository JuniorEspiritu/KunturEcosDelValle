// Material mate con la textura pegada al MUNDO (no a la malla): el asfalto, la
// vereda y el pasto se repiten siempre al mismo tamaño en metros, sea una
// cuadra de 8 m o una avenida de 200 m, sin tener que calcular UVs por pieza.
//
// Es Lambert (difuso puro): nada de brillos de plástico ni reflejos grises al
// mirar de costado, que era lo que hacía ver el pueblo "brilloso". Así queda
// el aspecto claro y suave del Figma.
//
// _VertexColor = 1 multiplica por el color de los vértices: el suelo del valle
// lo usa para pintar pasto, arena y orilla en una sola malla.
Shader "Kuntur/WorldTiled"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Color ("Tinte", Color) = (1, 1, 1, 1)
        _TileSize ("Metros por repetición", Float) = 4
        _Detail ("Textura de detalle (gris)", 2D) = "gray" {}
        _DetailSize ("Metros por repetición del detalle", Float) = 23
        _DetailStrength ("Fuerza del detalle", Range(0, 1)) = 0
        _VertexColor ("Usar color de vértice", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _Detail;
        fixed4 _Color;
        float _TileSize;
        float _DetailSize;
        float _DetailStrength;
        float _VertexColor;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            float4 vcolor;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcolor = v.color;
        }

        // Proyección desde los tres ejes, mezclada según hacia dónde mira la
        // cara: el techo de la vereda toma la textura desde arriba y el canto
        // del sardinel desde el costado, sin estirarse.
        fixed3 Triplanar (sampler2D tex, float3 p, float3 n, float size)
        {
            float3 w = pow(abs(n), 4);
            w /= (w.x + w.y + w.z + 1e-5);
            fixed3 cx = tex2D(tex, p.zy / size).rgb;
            fixed3 cy = tex2D(tex, p.xz / size).rgb;
            fixed3 cz = tex2D(tex, p.xy / size).rgb;
            return cx * w.x + cy * w.y + cz * w.z;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 n = normalize(IN.worldNormal);
            fixed3 c = Triplanar(_MainTex, IN.worldPos, n, _TileSize) * _Color.rgb;
            fixed3 d = Triplanar(_Detail, IN.worldPos, n, _DetailSize);
            c *= lerp(fixed3(1, 1, 1), d * 2.0, _DetailStrength);
            c *= lerp(fixed3(1, 1, 1), IN.vcolor.rgb, _VertexColor);
            o.Albedo = c;
            o.Alpha = 1;
        }
        ENDCG
    }

    Fallback "Diffuse"
}
