using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Unity solo trae 6 primitivas (cubo, esfera, cápsula, cilindro, plano, quad)
// y todas tienen sombreado suave. El estilo de las imágenes del GDD es
// low-poly facetado: montañas angulosas, pinos cónicos, rocas irregulares.
// Esta fábrica genera esas mallas por código y las guarda como assets, así la
// escena queda igual cada vez que se reconstruye y el proyecto no depende de
// ningún paquete de la Asset Store.
//
// Todas las mallas se generan "flat shaded": cada triángulo lleva sus propios
// 3 vértices, de modo que RecalculateNormals produce una normal por cara y se
// ven las facetas marcadas, no un degradado suave.
public static class LowPolyMeshFactory
{
    private const string GeneratedDir = "Assets/_Project/Art/Generated";

    // Cono con la base en y=0 y la punta en y=1, radio 1. Se usa escalado
    // para montañas, pinos y copas de árbol.
    public static Mesh Cone(int sides)
    {
        return LoadOrCreate($"Mesh_Cono{sides}", () => BuildCone(sides));
    }

    // Esfera facetada (icosaedro subdividido) de diámetro 1, igual que la
    // esfera primitiva de Unity pero con caras planas visibles.
    public static Mesh Icosphere(int subdivisions)
    {
        return LoadOrCreate($"Mesh_Icosfera{subdivisions}", () => BuildIcosphere(subdivisions, 0f, 0));
    }

    // Roca: la misma icosfera pero con los vértices desplazados de forma
    // irregular. El "variant" cambia la semilla para tener varias rocas
    // distintas sin que cambien entre reconstrucciones de la escena.
    public static Mesh Rock(int variant)
    {
        return LoadOrCreate($"Mesh_Roca{variant}", () => BuildIcosphere(1, 0.22f, variant * 977 + 13));
    }

    // Placa plana de n lados para la señalización vertical: 8 lados para el
    // PARE octogonal, 16 para las señales circulares de velocidad. Diámetro 1
    // en XY y grosor 1 en Z, para escalarla al tamaño real con localScale; la
    // cara mira hacia +Z, igual que un cubo, así se orienta con el mismo yaw
    // que los letreros de calle.
    public static Mesh SignBoard(int sides)
    {
        return LoadOrCreate($"Mesh_Placa{sides}", () => BuildSignBoard(sides));
    }

    // Techo a cuatro aguas: pirámide de base RECTANGULAR (base 1x1 centrada en
    // el origen, altura 1), para escalarla al ancho y al fondo de cada casa.
    // El cono de 4 lados no sirve acá: tiene los vértices sobre los ejes, así
    // que al escalarlo distinto en X y en Z los faldones dejan de caer
    // paralelos a las paredes y el techo sale girado respecto de la casa.
    public static Mesh Pyramid()
    {
        return LoadOrCreate("Mesh_Piramide", BuildPyramid);
    }

    private static Mesh BuildPyramid()
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        Vector3 apex = new Vector3(0f, 1f, 0f);
        Vector3[] corners =
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
        };

        for (int i = 0; i < 4; i++)
        {
            Vector3 a = corners[i];
            Vector3 b = corners[(i + 1) % 4];
            AddTriangle(verts, tris, apex, a, b);          // faldón
            AddTriangle(verts, tris, Vector3.zero, b, a);  // tapa de abajo
        }

        return Finish(verts, tris, new Vector3(0f, 0.4f, 0f));
    }

    private static Mesh LoadOrCreate(string assetName, System.Func<Mesh> build)
    {
        string path = $"{GeneratedDir}/{assetName}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        EnsureGeneratedFolder();
        Mesh mesh = build();
        mesh.name = assetName;
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    private static void EnsureGeneratedFolder()
    {
        if (AssetDatabase.IsValidFolder(GeneratedDir)) return;

        string[] parts = GeneratedDir.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static Mesh BuildCone(int sides)
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        Vector3 apex = new Vector3(0f, 1f, 0f);
        Vector3 baseCenter = Vector3.zero;

        for (int i = 0; i < sides; i++)
        {
            float a0 = (i / (float)sides) * Mathf.PI * 2f;
            float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

            AddTriangle(verts, tris, apex, p0, p1);         // pared lateral
            AddTriangle(verts, tris, baseCenter, p1, p0);   // tapa inferior
        }

        // El "centro" para orientar las caras hacia afuera está a media altura,
        // no en el origen: así la tapa de abajo también queda bien orientada.
        return Finish(verts, tris, new Vector3(0f, 0.5f, 0f));
    }

    private static Mesh BuildSignBoard(int sides)
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        // Se arranca medio paso girado para que el octógono quede con el lado
        // de arriba horizontal (como una señal de PARE de verdad) y no en punta.
        float offset = Mathf.PI / sides;
        Vector3[] rim = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = offset + (i / (float)sides) * Mathf.PI * 2f;
            rim[i] = new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f);
        }

        Vector3 frontCenter = new Vector3(0f, 0f, 0.5f);
        Vector3 backCenter = new Vector3(0f, 0f, -0.5f);

        for (int i = 0; i < sides; i++)
        {
            Vector3 a = rim[i];
            Vector3 b = rim[(i + 1) % sides];

            Vector3 frontA = a + Vector3.forward * 0.5f;
            Vector3 frontB = b + Vector3.forward * 0.5f;
            Vector3 backA = a - Vector3.forward * 0.5f;
            Vector3 backB = b - Vector3.forward * 0.5f;

            AddTriangle(verts, tris, frontCenter, frontA, frontB);  // cara delantera
            AddTriangle(verts, tris, backCenter, backA, backB);     // cara trasera
            AddTriangle(verts, tris, frontA, backA, backB);         // canto
            AddTriangle(verts, tris, frontA, backB, frontB);
        }

        return Finish(verts, tris, Vector3.zero);
    }

    private static Mesh BuildIcosphere(int subdivisions, float noise, int seed)
    {
        // Icosaedro base (12 vértices). El winding se corrige después en
        // Finish(), así que no importa el orden con el que salgan las caras.
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        List<Vector3> baseVerts = new List<Vector3>
        {
            new Vector3(-1f,  t, 0f), new Vector3( 1f,  t, 0f), new Vector3(-1f, -t, 0f), new Vector3( 1f, -t, 0f),
            new Vector3(0f, -1f,  t), new Vector3(0f,  1f,  t), new Vector3(0f, -1f, -t), new Vector3(0f,  1f, -t),
            new Vector3( t, 0f, -1f), new Vector3( t, 0f,  1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f,  1f),
        };

        int[] baseTris =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        for (int i = 0; i < baseTris.Length; i += 3)
        {
            Vector3 a = baseVerts[baseTris[i]].normalized;
            Vector3 b = baseVerts[baseTris[i + 1]].normalized;
            Vector3 c = baseVerts[baseTris[i + 2]].normalized;
            Subdivide(verts, tris, a, b, c, subdivisions);
        }

        // Diámetro 1 (radio 0.5) para que escale igual que la esfera de Unity,
        // más el desplazamiento irregular de las rocas si se pidió.
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 dir = verts[i].normalized;
            float bump = noise > 0f ? 1f + (Hash(dir, seed) - 0.5f) * 2f * noise : 1f;
            verts[i] = dir * 0.5f * bump;
        }

        return Finish(verts, tris, Vector3.zero);
    }

    private static void Subdivide(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, int depth)
    {
        if (depth <= 0)
        {
            AddTriangle(verts, tris, a, b, c);
            return;
        }

        Vector3 ab = ((a + b) * 0.5f).normalized;
        Vector3 bc = ((b + c) * 0.5f).normalized;
        Vector3 ca = ((c + a) * 0.5f).normalized;

        Subdivide(verts, tris, a, ab, ca, depth - 1);
        Subdivide(verts, tris, ab, b, bc, depth - 1);
        Subdivide(verts, tris, ca, bc, c, depth - 1);
        Subdivide(verts, tris, ab, bc, ca, depth - 1);
    }

    // Ruido determinista (mismo resultado siempre, sin Random) para deformar
    // las rocas: así la escena se ve igual cada vez que se reconstruye.
    private static float Hash(Vector3 p, int seed)
    {
        float v = Mathf.Sin(p.x * 12.9898f + p.y * 78.233f + p.z * 37.719f + seed * 0.137f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    private static void AddTriangle(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
    {
        int index = verts.Count;
        verts.Add(a);
        verts.Add(b);
        verts.Add(c);
        tris.Add(index);
        tris.Add(index + 1);
        tris.Add(index + 2);
    }

    private static Mesh Finish(List<Vector3> verts, List<int> tris, Vector3 center)
    {
        // Corrige el sentido de giro de cada triángulo comparando su normal
        // con la dirección "hacia afuera" desde el centro de la figura. Es más
        // confiable que acertar el orden a mano en cada cara, y todas estas
        // formas son convexas, así que el criterio siempre aplica.
        for (int i = 0; i < tris.Count; i += 3)
        {
            Vector3 a = verts[tris[i]];
            Vector3 b = verts[tris[i + 1]];
            Vector3 c = verts[tris[i + 2]];

            Vector3 faceNormal = Vector3.Cross(b - a, c - a);
            Vector3 outward = (a + b + c) / 3f - center;

            if (Vector3.Dot(faceNormal, outward) < 0f)
            {
                (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); // vértices no compartidos => una normal por cara (facetado)
        mesh.RecalculateBounds();
        return mesh;
    }
}
