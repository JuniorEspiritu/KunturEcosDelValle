using TMPro;
using UnityEngine;

// v55b: el parque pasa a ser la PLAZA CONSTITUCIÓN de Huancayo, como en la
// foto: la catedral blanca de dos torres con cúpulas mirando a la plaza, la
// pileta al centro, faroles, bancas, jardines con setos y las letras
// "HUANCAYO" hechas de arbusto sobre el pasto, iluminadas desde abajo.
public static partial class KunturSceneBuilder
{
    // La catedral va en la manzana del norte (entre el Jr. Loreto y el Jr.
    // Ayacucho), con la fachada mirando al sur, a la plaza.
    private static readonly Vector3 CathedralCenter = new Vector3(17.4f, 0f, -2f);
    private const float CathedralWidth = 16f;   // en X
    private const float CathedralDepth = 14f;   // en Z

    private static void ReserveCathedralLot()
    {
        Occupy(CathedralCenter + new Vector3(0f, 0f, -0.8f), CathedralWidth + 3f, CathedralDepth + 4f);
    }

    private static void BuildPlazaConstitucion(Transform plaza)
    {
        Vector3 c = ParkCenter;

        // Setos bajos que enmarcan los cuatro jardines.
        Color hedge = HexColor("#3e7a2c");
        float qx = ParkSizeX / 4f + 0.55f;
        float qz = ParkSizeZ / 4f + 0.55f;
        float bedX = ParkSizeX / 2f - 2.2f;
        float bedZ = ParkSizeZ / 2f - 2.2f;
        foreach (int sx in new[] { -1, 1 })
        foreach (int sz in new[] { -1, 1 })
        {
            Vector3 q = c + new Vector3(sx * qx, 0f, sz * qz);
            // solo el borde que da a los caminos del centro (no tapa las bancas)
            PrimitiveObject(plaza, "Seto", PrimitiveType.Cube, new Vector3(q.x, 0.3f, c.z + sz * 1.45f), new Vector3(bedX - 1.2f, 0.4f, 0.35f), hedge);
            PrimitiveObject(plaza, "Seto", PrimitiveType.Cube, new Vector3(c.x + sx * 1.45f, 0.3f, q.z + sz * 0.9f), new Vector3(0.35f, 0.4f, bedZ - 2.4f), hedge);
        }

        // Arbolitos podados en bola, como los de la plaza.
        Mesh ball = LowPolyMeshFactory.Icosphere(1);
        foreach (int sx in new[] { -1, 1 })
        {
            Vector3 t = c + new Vector3(sx * 2.5f, 0f, -ParkSizeZ / 2f + 2.2f);
            PrimitiveObject(plaza, "Topiario_Maceta", PrimitiveType.Cylinder, t + Vector3.up * 0.3f, new Vector3(0.7f, 0.3f, 0.7f), HexColor("#b9ad97"));
            PrimitiveObject(plaza, "Topiario_Tronco", PrimitiveType.Cylinder, t + Vector3.up * 0.9f, new Vector3(0.12f, 0.4f, 0.12f), HexColor("#6b4a2e"));
            MeshObject(plaza, "Topiario_Copa", ball, t + Vector3.up * 1.7f, Vector3.one * 1.3f, HexColor("#4f8f35"));
        }

        BuildHuancayoLetters(plaza, c + new Vector3(0f, 0f, -ParkSizeZ / 2f + 0.75f));
        BuildCathedral(plaza.parent);

        // Banderas del Perú a los lados de la catedral.
        foreach (int sx in new[] { -1, 1 })
        {
            Vector3 pole = new Vector3(CathedralCenter.x + sx * (CathedralWidth / 2f + 1.6f), 0f, CathedralCenter.z - CathedralDepth / 2f - 2.2f);
            PrimitiveObject(plaza, "Asta", PrimitiveType.Cylinder, pole + Vector3.up * 4f, new Vector3(0.1f, 4f, 0.1f), HexColor("#d9d9d9"));
            for (int b = 0; b < 3; b++)
                PrimitiveObject(plaza, "Bandera", PrimitiveType.Cube, pole + new Vector3(-sx * (0.25f + b * 0.35f), 7.3f, 0f),
                    new Vector3(0.35f, 0.7f, 0.03f), b == 1 ? Color.white : HexColor("#d91023"));
        }
    }

    // Letras "HUANCAYO" de arbusto: el texto 3D se apila en capas (cada una un
    // poco más atrás y más oscura) para que tengan grosor, sobre una jardinera
    // baja con luces que las iluminan de noche.
    private static void BuildHuancayoLetters(Transform parent, Vector3 center)
    {
        GameObject root = new GameObject("Letras_HUANCAYO");
        root.transform.SetParent(parent);
        root.transform.position = center;

        PrimitiveObject(root.transform, "Jardinera", PrimitiveType.Cube, center + Vector3.up * 0.16f, new Vector3(ParkSizeX - 1.2f, 0.32f, 1.3f), HexColor("#6aa84f"));
        PrimitiveObject(root.transform, "Jardinera_Borde", PrimitiveType.Cube, center + Vector3.up * 0.1f, new Vector3(ParkSizeX - 1.0f, 0.2f, 1.5f), HexColor("#b9ad97"));

        const int layers = 9;
        for (int i = layers - 1; i >= 0; i--)
        {
            float k = i / (float)(layers - 1);
            Color color = Color.Lerp(HexColor("#5fa83a"), HexColor("#244d17"), k);
            TextMeshPro text = MakeLogoText(root.transform, $"Letras_Capa_{i}", "HUANCAYO", 14f, color,
                center + new Vector3(0f, 0.32f, -0.2f + i * 0.07f), Quaternion.identity, new Vector2(0.5f, 0f), TextAlignmentOptions.Bottom, 1f);
            text.rectTransform.sizeDelta = new Vector2(ParkSizeX - 2.2f, 1.5f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 2f;
            text.fontSizeMax = 16f;
            text.characterSpacing = 6f;
            text.fontStyle = FontStyles.Bold;
        }

        // Una colisión simple: Kuntur no atraviesa las letras.
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.8f, 0f);
        box.size = new Vector3(ParkSizeX - 1.2f, 1.6f, 1.3f);

        // Reflectores del piso que las iluminan de noche.
        for (int i = -1; i <= 1; i++)
        {
            Vector3 spot = center + new Vector3(i * 6f, 0.4f, -1.2f);
            PrimitiveObject(root.transform, "Reflector", PrimitiveType.Cylinder, spot, new Vector3(0.18f, 0.08f, 0.18f), HexColor("#2b2b2b"));
            AddParkLampLight(root.transform, spot + new Vector3(0f, 0.3f, -0.4f));
        }
        SetStaticRecursive(root, true);
    }

    // Catedral de Huancayo (simplificada con piezas): cuerpo blanco, dos torres
    // con campanario, cúpula y cruz, frontón, pilastras, puerta en arco y una
    // cúpula central detrás.
    private static void BuildCathedral(Transform parent)
    {
        Vector3 c = CathedralCenter;
        const float W = CathedralWidth, D = CathedralDepth, H = 11f;
        float front = c.z - D / 2f;

        GameObject root = new GameObject("Catedral_Huancayo");
        root.transform.SetParent(parent);
        root.transform.position = c;
        Transform t = root.transform;

        Color wall = HexColor("#f2ead9");
        Color trim = HexColor("#dcd0b6");
        Color stone = HexColor("#cbc2ae");
        Color dome = HexColor("#ece2cc");
        Color dark = HexColor("#3b3530");
        Color wood = HexColor("#6b4a2e");

        // Escalinata y plataforma.
        PrimitiveObject(t, "Plataforma", PrimitiveType.Cube, new Vector3(c.x, 0.3f, c.z), new Vector3(W + 1.2f, 0.6f, D + 1.2f), stone);
        for (int i = 0; i < 3; i++)
            PrimitiveObject(t, "Escalon", PrimitiveType.Cube, new Vector3(c.x, 0.1f + i * 0.2f, front - 1.6f + i * 0.5f),
                new Vector3(W * 0.62f - i * 0.8f, 0.2f, 1.2f), stone);

        // Cuerpo (nave).
        PrimitiveObject(t, "Nave", PrimitiveType.Cube, new Vector3(c.x, 0.6f + H / 2f, c.z + 0.6f), new Vector3(W - 1.2f, H, D - 1.2f), wall);
        PrimitiveObject(t, "Cornisa", PrimitiveType.Cube, new Vector3(c.x, 0.6f + H, front + 0.9f), new Vector3(W - 6.4f, 0.45f, 0.8f), trim);

        // Frontón triangular sobre la puerta.
        Mesh gable = BuildGableMesh(0.6f, W - 7.6f, 2.8f);
        gable.name = "Fronton_Catedral";
        SaveGeneratedMesh(gable);
        MeshObject(t, "Fronton", gable, new Vector3(c.x, 0.6f + H + 0.2f, front + 0.9f), Vector3.one, wall, Quaternion.Euler(0f, 90f, 0f));
        float half = (W - 7.6f) / 2f;
        float slope = Mathf.Atan2(2.8f, half) * Mathf.Rad2Deg;
        foreach (int s in new[] { -1, 1 })
            PrimitiveObject(t, "Fronton_Borde", PrimitiveType.Cube,
                new Vector3(c.x + s * half / 2f, 0.6f + H + 0.2f + 1.4f, front + 0.85f),
                new Vector3(Mathf.Sqrt(half * half + 2.8f * 2.8f) + 0.3f, 0.3f, 0.9f), trim, Quaternion.Euler(0f, 0f, -s * slope));
        PrimitiveObject(t, "Cruz_Fronton_V", PrimitiveType.Cube, new Vector3(c.x, 0.6f + H + 3.6f, front + 0.9f), new Vector3(0.14f, 1.1f, 0.14f), trim);
        PrimitiveObject(t, "Cruz_Fronton_H", PrimitiveType.Cube, new Vector3(c.x, 0.6f + H + 3.8f, front + 0.9f), new Vector3(0.6f, 0.14f, 0.14f), trim);

        // Pilastras, puerta principal en arco, ventana redonda.
        foreach (float px in new[] { -3.6f, -1.6f, 1.6f, 3.6f })
            PrimitiveObject(t, "Pilastra", PrimitiveType.Cube, new Vector3(c.x + px, 0.6f + (H - 0.6f) / 2f, front + 0.55f), new Vector3(0.5f, H - 0.6f, 0.3f), trim);
        PrimitiveObject(t, "Puerta", PrimitiveType.Cube, new Vector3(c.x, 0.6f + 1.9f, front + 0.55f), new Vector3(2.4f, 3.8f, 0.2f), wood);
        PrimitiveObject(t, "Puerta_Arco", PrimitiveType.Cylinder, new Vector3(c.x, 0.6f + 3.8f, front + 0.55f), new Vector3(2.4f, 0.1f, 2.4f), wood, Quaternion.Euler(90f, 0f, 0f));
        PrimitiveObject(t, "Marco_Arco", PrimitiveType.Cylinder, new Vector3(c.x, 0.6f + 3.8f, front + 0.5f), new Vector3(2.9f, 0.08f, 2.9f), trim, Quaternion.Euler(90f, 0f, 0f));
        PrimitiveObject(t, "Rosetón", PrimitiveType.Cylinder, new Vector3(c.x, 0.6f + 7.4f, front + 0.5f), new Vector3(1.5f, 0.06f, 1.5f), HexColor("#6f8fb0"), Quaternion.Euler(90f, 0f, 0f));
        PrimitiveObject(t, "Rosetón_Marco", PrimitiveType.Cylinder, new Vector3(c.x, 0.6f + 7.4f, front + 0.54f), new Vector3(1.9f, 0.04f, 1.9f), trim, Quaternion.Euler(90f, 0f, 0f));
        foreach (int s in new[] { -1, 1 })
        {
            PrimitiveObject(t, "Hornacina", PrimitiveType.Cube, new Vector3(c.x + s * 2.6f, 0.6f + 6.2f, front + 0.5f), new Vector3(0.9f, 1.6f, 0.1f), dark);
            PrimitiveObject(t, "Ventana_Lateral", PrimitiveType.Cube, new Vector3(c.x + s * (W / 2f - 0.55f), 0.6f + 6f, c.z + 2f), new Vector3(0.1f, 2.4f, 1.2f), dark);
        }

        // Torres con campanario y cúpula.
        foreach (int s in new[] { -1, 1 })
        {
            float tx = c.x + s * (W / 2f - 1.8f);
            float tz = front + 1.6f;
            PrimitiveObject(t, "Torre", PrimitiveType.Cube, new Vector3(tx, 0.6f + H / 2f, tz), new Vector3(3.6f, H, 3.6f), wall);
            PrimitiveObject(t, "Torre_Cornisa", PrimitiveType.Cube, new Vector3(tx, 0.6f + H + 0.2f, tz), new Vector3(4.1f, 0.4f, 4.1f), trim);
            PrimitiveObject(t, "Puerta_Torre", PrimitiveType.Cube, new Vector3(tx, 0.6f + 1.4f, tz - 1.81f), new Vector3(1.2f, 2.8f, 0.1f), wood);
            PrimitiveObject(t, "Ventana_Torre", PrimitiveType.Cube, new Vector3(tx, 0.6f + 6.4f, tz - 1.81f), new Vector3(0.8f, 1.6f, 0.1f), dark);
            // Campanario.
            float by = 0.6f + H + 0.4f;
            PrimitiveObject(t, "Campanario", PrimitiveType.Cube, new Vector3(tx, by + 2.1f, tz), new Vector3(3.1f, 4.2f, 3.1f), wall);
            foreach (Vector3 face in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                Vector3 p = new Vector3(tx, by + 2.3f, tz) + face * 1.56f;
                Vector3 size = face.x != 0f ? new Vector3(0.05f, 2.2f, 1.2f) : new Vector3(1.2f, 2.2f, 0.05f);
                PrimitiveObject(t, "Arco_Campana", PrimitiveType.Cube, p, size, dark);
            }
            PrimitiveObject(t, "Campana", PrimitiveType.Sphere, new Vector3(tx, by + 2.2f, tz - 1.2f), new Vector3(0.6f, 0.7f, 0.6f), HexColor("#b08d3c"));
            PrimitiveObject(t, "Campanario_Cornisa", PrimitiveType.Cube, new Vector3(tx, by + 4.35f, tz), new Vector3(3.5f, 0.35f, 3.5f), trim);
            PrimitiveObject(t, "Tambor", PrimitiveType.Cylinder, new Vector3(tx, by + 4.9f, tz), new Vector3(2.4f, 0.4f, 2.4f), wall);
            PrimitiveObject(t, "Cupula_Torre", PrimitiveType.Sphere, new Vector3(tx, by + 5.4f, tz), new Vector3(2.5f, 2.3f, 2.5f), dome);
            PrimitiveObject(t, "Linterna", PrimitiveType.Cylinder, new Vector3(tx, by + 6.9f, tz), new Vector3(0.6f, 0.4f, 0.6f), wall);
            PrimitiveObject(t, "Linterna_Cupula", PrimitiveType.Sphere, new Vector3(tx, by + 7.4f, tz), new Vector3(0.7f, 0.6f, 0.7f), dome);
            PrimitiveObject(t, "Cruz_V", PrimitiveType.Cube, new Vector3(tx, by + 8.2f, tz), new Vector3(0.12f, 1f, 0.12f), HexColor("#8a7a5a"));
            PrimitiveObject(t, "Cruz_H", PrimitiveType.Cube, new Vector3(tx, by + 8.4f, tz), new Vector3(0.5f, 0.12f, 0.12f), HexColor("#8a7a5a"));
        }

        // Cúpula central, detrás del frontón.
        Vector3 cd = new Vector3(c.x, 0.6f + H, c.z + 2.6f);
        PrimitiveObject(t, "Tambor_Central", PrimitiveType.Cylinder, cd + Vector3.up * 1.1f, new Vector3(4.6f, 1.1f, 4.6f), wall);
        PrimitiveObject(t, "Cupula_Central", PrimitiveType.Sphere, cd + Vector3.up * 2.4f, new Vector3(4.8f, 4.2f, 4.8f), dome);
        PrimitiveObject(t, "Linterna_Central", PrimitiveType.Cylinder, cd + Vector3.up * 4.9f, new Vector3(0.9f, 0.5f, 0.9f), wall);
        PrimitiveObject(t, "Cruz_Central_V", PrimitiveType.Cube, cd + Vector3.up * 6f, new Vector3(0.14f, 1.2f, 0.14f), HexColor("#8a7a5a"));
        PrimitiveObject(t, "Cruz_Central_H", PrimitiveType.Cube, cd + Vector3.up * 6.2f, new Vector3(0.6f, 0.14f, 0.14f), HexColor("#8a7a5a"));

        // Luces cálidas que la iluminan de noche.
        foreach (float lx in new[] { -5f, 0f, 5f })
            AddParkLampLight(t, new Vector3(c.x + lx, 1.2f, front - 3f));

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, (H + 0.6f) / 2f, 0f);
        box.size = new Vector3(W, H + 0.6f, D);

        AddMapIcon(root.transform, c, HexColor("#f2ead9"), 9f);
        SetStaticRecursive(root, true);
    }
}
