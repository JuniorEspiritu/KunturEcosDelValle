// Agua de río estilizada, como la del dibujo de referencia: celeste en las
// orillas, azul más hondo al centro, espuma blanca donde toca las piedras y
// brillos que corren río abajo.
//
// No depende de la profundidad de la escena ni de reflejos: el color sale de
// la posición en el ancho del río (u de la malla: 0 una orilla, 1 la otra), y
// el movimiento de un ruido que se desliza a lo largo del cauce. Por eso se
// ve igual de limpio en cualquier computadora.
Shader "Kuntur/StylizedWater"
{
    Properties
    {
        _Shallow ("Color de la orilla", Color) = (0.46, 0.80, 0.93, 1)
        _Deep ("Color del centro", Color) = (0.12, 0.50, 0.80, 1)
        _Foam ("Espuma", Color) = (1, 1, 1, 1)
        _Noise ("Ruido (gris, repetible)", 2D) = "gray" {}
        _FlowSpeed ("Velocidad de la corriente", Float) = 0.35
        _FoamWidth ("Ancho de la espuma", Range(0, 0.5)) = 0.13
        _Streaks ("Brillos de la corriente", Range(0, 1)) = 0.32
        _Sky ("Reflejo del cielo", Color) = (0.80, 0.92, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0

        sampler2D _Noise;
        fixed4 _Shallow;
        fixed4 _Deep;
        fixed4 _Foam;
        fixed4 _Sky;
        float _FlowSpeed;
        float _FoamWidth;
        float _Streaks;

        struct Input
        {
            float2 rawUV;
            float3 worldPos;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.rawUV = v.texcoord.xy;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            // 0 en el centro del río, 1 en las orillas.
            float edge = abs(IN.rawUV.x * 2.0 - 1.0);
            fixed3 col = lerp(_Deep.rgb, _Shallow.rgb, smoothstep(0.1, 0.95, edge));

            // Dos capas de ruido corriendo aguas abajo a distinta velocidad.
            float t = _Time.y * _FlowSpeed;
            float n1 = tex2D(_Noise, float2(IN.rawUV.x * 1.3, IN.rawUV.y * 0.30 - t)).r;
            float n2 = tex2D(_Noise, float2(IN.rawUV.x * 2.9 + 0.37, IN.rawUV.y * 0.85 - t * 1.7)).r;

            float streak = smoothstep(0.56, 0.70, n1 * 0.6 + n2 * 0.5);
            col += streak * _Streaks;

            // Espuma en el borde, con el contorno movido por el ruido para que
            // no parezca una raya pintada con regla.
            float foam = smoothstep(1.0 - _FoamWidth, 1.0, edge + (n2 - 0.5) * 0.18);
            col = lerp(col, _Foam.rgb, foam * 0.85);

            // Mirando de lejos (casi rasante), el agua refleja el cielo.
            float3 toCam = normalize(_WorldSpaceCameraPos - IN.worldPos);
            float rim = pow(1.0 - saturate(toCam.y), 3.0);
            col = lerp(col, _Sky.rgb, rim * 0.35);

            o.Albedo = col * 0.82;
            o.Emission = col * 0.28; // un poco de luz propia: el agua se ve viva también a la sombra
            o.Alpha = 1;
        }
        ENDCG
    }

    Fallback "Diffuse"
}
