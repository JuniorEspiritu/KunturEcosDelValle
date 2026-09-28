using System.IO;
using UnityEditor;
using UnityEngine;

// Los paneles del GDD tienen esquinas redondeadas, bordes dorados, un corazón
// para la Salud del Valle, una moneda para el puntaje y una flecha para la
// posición en el mapa. Unity no trae ninguno de esos sprites, así que se
// dibujan por código y se guardan como PNG dentro del proyecto (una sola vez;
// después se reutilizan). Así el HUD se parece al mockup sin depender de
// descargar iconos de ningún lado.
public static class UISpriteFactory
{
    private const string UiDir = "Assets/_Project/Art/UI";
    private const int Size = 64;

    // Panel redondeado con borde. Se usa con Image.type = Sliced para que se
    // estire a cualquier tamaño sin deformar las esquinas.
    public static Sprite RoundedPanel(string name, Color fill, Color border, float cornerRadius = 14f, float borderWidth = 2f)
    {
        return Create(name, (x, y) =>
        {
            float d = RoundedBoxDistance(x, y, cornerRadius);
            if (d > 0.5f) return Color.clear;

            float edgeAlpha = Mathf.Clamp01(0.5f - d);
            if (borderWidth > 0f && d > -borderWidth)
            {
                Color c = border;
                c.a *= edgeAlpha;
                return c;
            }

            Color f = fill;
            f.a *= edgeAlpha;
            return f;
        }, Mathf.CeilToInt(cornerRadius + borderWidth + 1f));
    }

    // Círculo sólido con borde: moneda del puntaje, viñetas de las opciones
    // de diálogo, punto del minimapa.
    public static Sprite Circle(string name, Color fill, Color border, float borderWidth = 3f)
    {
        return Create(name, (x, y) =>
        {
            float cx = x - Size / 2f + 0.5f;
            float cy = y - Size / 2f + 0.5f;
            float radius = Size / 2f - 1f;
            float d = Mathf.Sqrt(cx * cx + cy * cy) - radius;

            if (d > 0.5f) return Color.clear;
            float edgeAlpha = Mathf.Clamp01(0.5f - d);

            if (borderWidth > 0f && d > -borderWidth)
            {
                Color c = border;
                c.a *= edgeAlpha;
                return c;
            }

            Color f = fill;
            f.a *= edgeAlpha;
            return f;
        }, 0);
    }

    // Degradé vertical: opaco abajo (y=0) y transparente arriba. Sirve para
    // que los créditos aparezcan y se esfumen en los bordes de la pantalla.
    public static Sprite VerticalFade(string name, Color color)
    {
        return Create(name, (x, y) =>
        {
            float t = 1f - y / (float)(Size - 1);
            Color c = color;
            c.a *= t * t * (3f - 2f * t);
            return c;
        }, 0);
    }

    // Corazón de la barra "SALUD DEL VALLE" (ecuación implícita clásica).
    public static Sprite Heart(string name, Color color)
    {
        return Create(name, (px, py) =>
        {
            float x = (px - Size / 2f + 0.5f) / (Size * 0.42f);
            // v55b: las filas de la textura van de abajo hacia arriba; antes
            // la cuenta estaba al revés y el corazón salía de cabeza.
            float y = (py + 0.5f - Size / 2f) / (Size * 0.42f) + 0.12f;

            float x2 = x * x;
            float y2 = y * y;
            float a = x2 + y2 - 1f;
            float value = a * a * a - x2 * y * y2;

            if (value > 0f) return Color.clear;
            return color;
        }, 0);
    }

    // Estrella de 5 puntas de la pantalla de resultado.
    public static Sprite Star(string name, Color color)
    {
        return Create(name, (px, py) =>
        {
            float x = (px - Size / 2f + 0.5f) / (Size / 2f);
            float y = (Size / 2f - py - 0.5f) / (Size / 2f);

            float radius = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(x, y); // 0 = hacia arriba, así la punta queda arriba

            float step = Mathf.PI * 2f / 5f;
            float local = Mathf.Repeat(angle, step);
            float edge = Mathf.Abs(local - step * 0.5f) / (step * 0.5f); // 1 en la punta, 0 en el valle
            float limit = Mathf.Lerp(0.40f, 0.95f, edge);

            return radius <= limit ? color : Color.clear;
        }, 0);
    }

    // Flecha del jugador en el mapa: apunta hacia arriba (+Y de la UI) y se
    // rota según hacia dónde mira Kuntur.
    public static Sprite Arrow(string name, Color fill, Color outline)
    {
        return Create(name, (px, py) =>
        {
            float x = (px - Size / 2f + 0.5f) / (Size / 2f); // -1..1
            // Las filas de la textura van de abajo hacia arriba (fila 0 = abajo):
            // antes la cuenta estaba al revés y la flecha salía apuntando abajo,
            // o sea al lado contrario de hacia donde camina Kuntur.
            float y = (py + 0.5f - Size / 2f) / (Size / 2f); // -1..1 (arriba positivo)

            // Triángulo con la punta arriba y una muesca abajo, para que se
            // lea la dirección de un vistazo.
            bool insideTriangle = y < 0.9f && y > -0.9f && Mathf.Abs(x) < (0.9f - y) * 0.55f;
            bool insideNotch = y < -0.25f && Mathf.Abs(x) < (y + 0.9f) * 0.5f;
            bool inside = insideTriangle && !insideNotch;

            if (!inside) return Color.clear;

            bool nearEdge = Mathf.Abs(x) > (0.9f - y) * 0.55f - 0.12f || y > 0.78f;
            return nearEdge ? outline : fill;
        }, 0);
    }

    // v55: palomita (✓) de los objetivos cumplidos. La fuente no trae el
    // carácter, así que se dibuja: distancia a dos segmentos.
    public static Sprite Check(string name, Color color)
    {
        return Create(name, (px, py) =>
        {
            Vector2 p = new Vector2(px + 0.5f, py + 0.5f) / Size;
            Vector2 a = new Vector2(0.18f, 0.52f), b = new Vector2(0.42f, 0.26f), c = new Vector2(0.84f, 0.76f);
            float d = Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, b, c)) * Size;
            float alpha = Mathf.Clamp01(5.5f - d);
            Color col = color;
            col.a *= alpha;
            return col;
        }, 0);
    }

    // Rayo de la barra de ENERGÍA.
    public static Sprite Bolt(string name, Color color)
    {
        Vector2[] poly =
        {
            new Vector2(0.58f, 0.96f), new Vector2(0.20f, 0.44f), new Vector2(0.46f, 0.44f),
            new Vector2(0.38f, 0.04f), new Vector2(0.80f, 0.58f), new Vector2(0.54f, 0.58f),
        };
        return Create(name, (px, py) =>
        {
            Vector2 p = new Vector2(px + 0.5f, py + 0.5f) / Size;
            return InsidePolygon(p, poly) ? color : Color.clear;
        }, 0);
    }

    // Brillo redondo que se desvanece hacia afuera (partículas del menú,
    // halo del sol, destello de la barra).
    public static Sprite SoftGlow(string name, Color color)
    {
        return Create(name, (px, py) =>
        {
            float x = (px + 0.5f) / Size * 2f - 1f;
            float y = (py + 0.5f) / Size * 2f - 1f;
            float r = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y));
            Color c = color;
            c.a *= r * r;
            return c;
        }, 0);
    }

    // Degradé horizontal: opaco a la izquierda, transparente a la derecha
    // (la línea que subraya la opción elegida del menú, estilo Horizon).
    public static Sprite HorizontalFade(string name, Color color)
    {
        return Create(name, (px, py) =>
        {
            float t = 1f - px / (float)(Size - 1);
            Color c = color;
            c.a *= t;
            return c;
        }, 0);
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    private static bool InsidePolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    private static float RoundedBoxDistance(int px, int py, float radius)
    {
        float halfSize = Size / 2f;
        float dx = Mathf.Abs(px - halfSize + 0.5f) - (halfSize - radius);
        float dy = Mathf.Abs(py - halfSize + 0.5f) - (halfSize - radius);
        float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
        float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
        return outside + inside - radius; // negativo adentro, 0 justo en el borde
    }

    private static Sprite Create(string name, System.Func<int, int, Color> shade, int nineSliceBorder)
    {
        string path = $"{UiDir}/{name}.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        EnsureFolder();

        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                pixels[y * Size + x] = shade(x, y);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            if (nineSliceBorder > 0)
            {
                // Con 9-slice las esquinas redondeadas no se estiran al
                // cambiar el tamaño del panel.
                importer.spriteBorder = new Vector4(nineSliceBorder, nineSliceBorder, nineSliceBorder, nineSliceBorder);
            }
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(UiDir)) return;

        string[] parts = UiDir.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
