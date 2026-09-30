using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// v53: la Calle Real sigue hacia el SUR y sube un cerro, con casas a los dos
// lados. Desde arriba (el mirador) se ve la calle bajando hacia el pueblo,
// con el río y los cerros detrás, como en la foto de referencia.
public static partial class KunturSceneBuilder
{
    private const float HillStartZ = TownSouthZ - 4f;   // -134: el pueblo queda plano
    private const float HillTopZ = -198f;               // arriba empieza la meseta
    // v56: la Calle Real ya no muere arriba: llega a la calle del mirador
    // (TopRoadZ), que corre de este a oeste y baja por el Jr. Puno y el
    // Jr. Cusco de vuelta al pueblo. Todo el cerro es un circuito.
    private const float HillEndZ = -208f;               // hasta aquí llegan las calles de subida
    private const float TopRoadZ = HillEndZ - CrossStreetHalfRoad;   // -211.25: calle del mirador
    // v55: el doble de alto. Con 8.5 m casi no se notaba la bajada; con 16 m
    // la calle se ve caer hacia el pueblo (máx. ~22°, se sube caminando).
    private const float HillHeight = 16f;

    // Altura extra del cerro en (x, z). 0 en todo el pueblo y cerca del río.
    // Hacia los costados se aplana. v56: el cerro es más ancho, para que el
    // Jr. Cusco (x=34) también suba con la misma pendiente que la Real; se
    // aplana al este recién desde x=44 (el río, por esa zona, pasa lejos).
    private static float HillHeightAt(float x, float z)
    {
        if (z > HillStartZ) return 0f;
        float t = Mathf.Clamp01((HillStartZ - z) / (HillStartZ - HillTopZ));
        // Cóncava: suave abajo (sale plana del pueblo) y más empinada arriba
        // (~22°). Así, parado arriba en el mirador, se ve TODA la bajada;
        // con una curva en S la cima tapaba la calle.
        float hz = HillHeight * Mathf.Pow(t, 1.6f);

        float w = x >= 0f
            ? 1f - Mathf.SmoothStep(0f, 1f, (x - 44f) / 24f)
            : 1f - Mathf.SmoothStep(0f, 1f, (-x - 45f) / 55f);
        return hz * Mathf.Clamp01(w);
    }

    private static readonly Color HillAsphalt = new Color(0.235f, 0.251f, 0.271f);

    private static void BuildHillStreet(Transform parent)
    {
        GameObject root = new GameObject("Subida_Mirador");
        root.transform.SetParent(parent);

        // Filas cada 1.5 m, las MISMAS del terreno (ver BuildValleyGround):
        // así la pista calza exacto sobre el suelo y no aparecen dientes.
        var rows = new List<float>();
        for (int k = 0; ; k++)
        {
            float z = -TerrainHalf + k * 1.5f;
            if (z > TownSouthZ + 0.01f) break;
            if (z >= HillEndZ - 0.01f) rows.Add(z);
        }
        if (rows.Count < 2) return;

        Color asphalt = HexColor("#3c4045");
        Color yellow = HexColor("#f2c230");
        Color walk = HexColor("#c9c3b8");
        Color curb = HexColor("#9c968c");

        // Las tres calles que suben: la Real (al centro) y, v56, el Jr. Puno
        // y el Jr. Cusco a los costados.
        foreach ((float sx, float half, string label) in new[]
        {
            (MainStreetX, MainStreetHalf, ""), (PunoX, CrossStreetHalfRoad, "Puno_"), (CuscoX, CrossStreetHalfRoad, "Cusco_"),
        })
        {
            BuildHillStrip(root.transform, label + "Pista", rows, sx - half, sx + half, 0.03f, asphalt, true);
            BuildHillStrip(root.transform, label + "Linea_A", rows, sx - 0.35f, sx - 0.15f, 0.045f, yellow, false);
            BuildHillStrip(root.transform, label + "Linea_B", rows, sx + 0.15f, sx + 0.35f, 0.045f, yellow, false);
            foreach (float side in new[] { -1f, 1f })
            {
                float a = sx + side * half, b = sx + side * (half + SidewalkWidth);
                BuildHillStrip(root.transform, label + (side < 0 ? "Vereda_Oeste" : "Vereda_Este"), rows, Mathf.Min(a, b), Mathf.Max(a, b), 0.12f, walk, true);
                float c0 = sx + side * half, c1 = sx + side * (half + 0.18f);
                BuildHillStrip(root.transform, label + (side < 0 ? "Sardinel_Oeste" : "Sardinel_Este"), rows, Mathf.Min(c0, c1), Mathf.Max(c0, c1), 0.16f, curb, false);
            }
        }

        BuildTopRoad(root.transform, asphalt, yellow, walk, curb);

        // Casas a los dos lados de cada subida, con la fachada a la calle.
        System.Random rng = new System.Random(5306);
        Color[] walls =
        {
            HexColor("#e9d8b4"), HexColor("#d98c5f"), HexColor("#f2e6cf"), HexColor("#c9b28f"),
            HexColor("#e7b77a"), HexColor("#bfcfd9"), HexColor("#d7a6a0"),
        };
        int index = 0;
        foreach ((float sx, float half) in new[] { (MainStreetX, MainStreetHalf), (PunoX, CrossStreetHalfRoad), (CuscoX, CrossStreetHalfRoad) })
        {
            foreach (float side in new[] { -1f, 1f })
            {
                for (float z = HillStartZ - 8f; z >= HillTopZ + 2f; z -= NextFloat(rng, 10f, 12.5f))
                {
                    float width = NextFloat(rng, 6.2f, 7.4f);
                    float depth = NextFloat(rng, 6.5f, 8f);
                    float x = sx + side * (half + SidewalkWidth + 0.8f + width / 2f);
                    // En la pendiente la casa se apoya en el lado de ARRIBA y abajo
                    // lleva un basamento de piedra hasta el suelo (como las casas
                    // en ladera de verdad). Si no, flotaba o quedaba enterrada.
                    float hUp = Mathf.Max(HillHeightAt(x - width / 2f, z - depth / 2f), HillHeightAt(x + width / 2f, z - depth / 2f));
                    float hDown = Mathf.Min(HillHeightAt(x - width / 2f, z + depth / 2f), HillHeightAt(x + width / 2f, z + depth / 2f));
                    Vector3 pos = new Vector3(x, hUp - 0.25f, z);
                    string houseName = $"Casa_Subida_{index}";
                    BuildHouse(root.transform, houseName, pos,
                        NextFloat(rng, 3f, 5.2f), width, depth, walls[index % walls.Length], rng);
                    if (root.transform.Find(houseName) != null && hUp - hDown > 0.2f)
                    {
                        float baseH = hUp - hDown + 0.7f;
                        PrimitiveObject(root.transform, "Basamento", PrimitiveType.Cube,
                            new Vector3(x, hDown - 0.45f + baseH / 2f, z), new Vector3(width + 0.1f, baseH, depth + 0.1f), HexColor("#8e8578"));
                        // Escalones de la vereda a la puerta (la puerta mira a la calle).
                        float stepX = sx + side * (half + SidewalkWidth + 0.45f);
                        for (int st = 0; st < 3; st++)
                            PrimitiveObject(root.transform, "Escalon", PrimitiveType.Cube,
                                new Vector3(stepX, HillHeightAt(stepX, z) + 0.08f + st * 0.16f, z),
                                new Vector3(0.9f - st * 0.2f, 0.16f, 1.6f), HexColor("#b9b5ac"));
                    }
                    index++;
                }
            }
        }

        // Árboles en los huecos entre las casas de una subida y la siguiente
        // (y por fuera), y postes de luz en las veredas.
        for (int i = 0; i < 26; i++)
        {
            float x;
            switch (i % 4)
            {
                case 0: x = NextFloat(rng, 16.4f, 18.6f); break;     // entre la Real y el Cusco
                case 1: x = NextFloat(rng, -26f, -16.5f); break;     // entre el Puno y la Real
                case 2: x = NextFloat(rng, 50f, 60f); break;         // pasando el Cusco
                default: x = NextFloat(rng, -70f, -58f); break;      // pasando el Puno
            }
            float z = NextFloat(rng, HillTopZ, HillStartZ - 4f);
            BuildRoundTree(root.transform, $"Arbol_Subida_{i}", new Vector3(x, HillHeightAt(x, z) - 0.1f, z), rng);
        }
        foreach ((float sx, float half) in new[] { (MainStreetX, MainStreetHalf), (PunoX, CrossStreetHalfRoad), (CuscoX, CrossStreetHalfRoad) })
        {
            for (float z = HillStartZ - 6f; z >= HillEndZ + 4f; z -= 18f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = sx + side * (half + SidewalkWidth - 0.45f);
                    BuildStreetLamp(root.transform, new Vector3(x, HillHeightAt(x, z) + 0.12f, z), -side);
                }
            }
        }

        // Abajo, donde empieza la subida: letreros que dicen a dónde lleva.
        BuildStreetSign(root.transform, new Vector3(MainStreetHalf + 1.2f, 0.12f, TownSouthZ + 1.5f), "SUBIDA AL MIRADOR", 0f);
        BuildStreetSign(root.transform, new Vector3(-MainStreetHalf - 1.2f, 0.12f, TownSouthZ + 1.5f), "HELADERÍA · MIRADOR", 0f);

        BuildMiradorTop(root.transform, rng);

        // Arriba, el mirador: desde aquí se ve bajar la calle hacia el valle.
        float topZ = HillEndZ + 3f;
        BuildStreetSign(root.transform, new Vector3(MainStreetHalf + 1.2f, HillHeightAt(MainStreetHalf + 1.2f, topZ) + 0.12f, topZ),
            "MIRADOR DEL VALLE", 180f);
    }

    // v56: la calle de arriba. Une la Real con el Puno (a la izquierda) y el
    // Cusco (a la derecha): lo que antes era un final con baranda ahora
    // sigue, y se puede bajar por el otro lado.
    private static void BuildTopRoad(Transform parent, Color asphalt, Color yellow, Color walk, Color curb)
    {
        GameObject top = new GameObject("Calle_Mirador");
        top.transform.SetParent(parent);
        float west = PunoX - CrossStreetHalfRoad, east = CuscoX + CrossStreetHalfRoad;
        float outerWest = west - SidewalkWidth, outerEast = east + SidewalkWidth;
        float roadS = TopRoadZ - CrossStreetHalfRoad, roadN = TopRoadZ + CrossStreetHalfRoad;

        BuildHillStripX(top.transform, "Pista", west, east, roadS, roadN, 0.03f, asphalt, true);
        // Doble línea amarilla, cortada en los cruces.
        foreach ((float a, float b) in new[] { (PunoX + CrossStreetHalfRoad + 1f, MainStreetX - MainStreetHalf - 1f), (MainStreetX + MainStreetHalf + 1f, CuscoX - CrossStreetHalfRoad - 1f) })
        {
            BuildHillStripX(top.transform, "Linea_A", a, b, TopRoadZ - 0.35f, TopRoadZ - 0.15f, 0.045f, yellow, false);
            BuildHillStripX(top.transform, "Linea_B", a, b, TopRoadZ + 0.15f, TopRoadZ + 0.35f, 0.045f, yellow, false);
        }

        // Vereda de afuera (la del sur, la del mirador): corrida de punta a punta.
        BuildHillStripX(top.transform, "Vereda_Sur", outerWest, outerEast, roadS - SidewalkWidth, roadS, 0.12f, walk, true);
        BuildHillStripX(top.transform, "Sardinel_Sur", outerWest, outerEast, roadS - 0.18f, roadS, 0.16f, curb, false);
        // Vereda de adentro (norte): entre las calles que bajan.
        foreach ((float a, float b) in new[] { (PunoX + CrossStreetHalfRoad, MainStreetX - MainStreetHalf), (MainStreetX + MainStreetHalf, CuscoX - CrossStreetHalfRoad) })
        {
            BuildHillStripX(top.transform, "Vereda_Norte", a, b, roadN, roadN + SidewalkWidth, 0.12f, walk, true);
            BuildHillStripX(top.transform, "Sardinel_Norte", a, b, roadN, roadN + 0.18f, 0.16f, curb, false);
        }

        // Postes de luz a lo largo de la vereda del mirador.
        for (float x = PunoX + 8f; x <= CuscoX - 6f; x += 16f)
        {
            if (Mathf.Abs(x) < 6f) continue;
            float z = roadS - SidewalkWidth + 0.45f;
            BuildStreetLamp(top.transform, new Vector3(x, HillHeightAt(x, z) + 0.12f, z), Vector3.forward, false);
        }
    }

    // Franja a lo largo de X (la calle de arriba), siguiendo el suelo.
    private static void BuildHillStripX(Transform parent, string name, float x0, float x1, float z0, float z1,
        float lift, Color color, bool collider)
    {
        var cols = new List<float>();
        for (float x = x0; x < x1 - 0.01f; x += 1.5f) cols.Add(x);
        cols.Add(x1);
        if (cols.Count < 2) return;

        var vertices = new Vector3[cols.Count * 2];
        var uvs = new Vector2[cols.Count * 2];
        var tris = new int[(cols.Count - 1) * 6];
        for (int i = 0; i < cols.Count; i++)
        {
            float x = cols[i];
            vertices[i * 2] = new Vector3(x, HillHeightAt(x, z0) + lift, z0);
            vertices[i * 2 + 1] = new Vector3(x, HillHeightAt(x, z1) + lift, z1);
            uvs[i * 2] = new Vector2(x * 0.25f, 0f);
            uvs[i * 2 + 1] = new Vector2(x * 0.25f, 1f);
            if (i == cols.Count - 1) continue;
            int t = i * 6, v = i * 2;
            // Cara hacia el cielo: (x crece, z0 -> z1).
            tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
            tris[t + 3] = v + 1; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
        }

        Mesh mesh = new Mesh { name = "Mirador_" + name + "_" + Mathf.RoundToInt(x0) + "_" + Mathf.RoundToInt(z0 * 10f) };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        SaveGeneratedMesh(mesh);

        GameObject go = MeshObject(parent, name, mesh, Vector3.zero, Vector3.one, color);
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    // v55: arriba de la subida hay a qué ir: la heladería del mirador, bancas
    // mirando al pueblo y vecinos disfrutando la vista (la heladera, Doña
    // Rosario, te encarga limpiar la subida: ver la zona "mirador").
    private static void BuildMiradorTop(Transform parent, System.Random rng)
    {
        Vector3 shop = new Vector3(MainStreetHalf + SidewalkWidth + 0.5f + 2.3f, HillHeightAt(9.6f, -202f), -202f);
        BuildIceCreamShop(parent, "Heladeria_Mirador", shop, -1f, "HELADERÍA DEL MIRADOR", "Doña Rosario");

        // v56: las bancas se pasaron a la vereda de la calle del mirador (la
        // calle ahora sigue), mirando al norte, al pueblo, calle abajo. Detrás,
        // una baranda de madera a lo largo de toda la vereda.
        float benchZ = TopRoadZ - CrossStreetHalfRoad - SidewalkWidth + 0.75f;
        foreach (float x in new[] { -31f, -19f, -2.6f, 2.6f, 16f, 25f })
        {
            PlaceProp(parent, "Assets/ModularLowpolyStreetsFree/Prefabs/Other/Bench_1.prefab", "Banca_Mirador",
                new Vector3(x, HillHeightAt(x, benchZ) + 0.12f, benchZ), 0f, 0.9f);
        }

        float railZ = TopRoadZ - CrossStreetHalfRoad - SidewalkWidth - 0.15f;
        float rx0 = PunoX - CrossStreetHalfRoad - SidewalkWidth, rx1 = CuscoX + CrossStreetHalfRoad + SidewalkWidth;
        float railY = HillHeightAt(0f, railZ);
        PrimitiveObject(parent, "Baranda_Mirador", PrimitiveType.Cube,
            new Vector3((rx0 + rx1) / 2f, railY + 0.95f, railZ), new Vector3(rx1 - rx0, 0.12f, 0.12f), HexColor("#7a5a3a"));
        PrimitiveObject(parent, "Baranda_Mirador_Baja", PrimitiveType.Cube,
            new Vector3((rx0 + rx1) / 2f, railY + 0.5f, railZ), new Vector3(rx1 - rx0, 0.1f, 0.1f), HexColor("#7a5a3a"));
        for (float x = rx0; x <= rx1 + 0.01f; x += 2.5f)
            PrimitiveObject(parent, "Baranda_Poste", PrimitiveType.Cube,
                new Vector3(x, HillHeightAt(x, railZ) + 0.5f, railZ), new Vector3(0.14f, 1f, 0.14f), HexColor("#6a4a2e"));
        GameObject wall = new GameObject("Baranda_Collider");
        wall.transform.SetParent(parent);
        wall.transform.position = new Vector3((rx0 + rx1) / 2f, railY + 0.6f, railZ);
        BoxCollider box = wall.AddComponent<BoxCollider>();
        box.size = new Vector3(rx1 - rx0, 1.2f, 0.3f);
        wall.isStatic = true;
        Occupy(new Vector3((rx0 + rx1) / 2f, 0f, railZ), rx1 - rx0, 1f);

        int layer = interactableLayerForGivers;
        Vector3 a = new Vector3(-8.2f, HillHeightAt(-8.2f, -202.5f), -202.5f);
        Vector3 b = new Vector3(-9.1f, HillHeightAt(-9.1f, -201.8f), -201.8f);
        GameObject rosario = BuildVillager(parent, "Heladera_Rosario", a, HexColor("#ff5c93"), rng,
            PeopleDir + "/Prefabs/downtown/casual_Female_K.prefab");
        GameObject friendGO = BuildVillager(parent, "Mirador_Vecino", b, HexColor("#2e5aa8"), rng,
            PeopleDir + "/Prefabs/downtown/casual_Male_K.prefab");
        if (rosario != null && friendGO != null)
        {
            LinkVillagers(rosario, friendGO, 0f);
            LinkVillagers(friendGO, rosario, 3.4f);
            MakeMissionGiver(rosario, "Doña Rosario", true, "HELADERA DEL MIRADOR", layer, "mirador", "mother");
        }

        // Alguien que sube y baja la calle a pie.
        GameObject hiker = BuildAssetPedestrian(parent, "Caminante_Subida",
            new Vector3(-(MainStreetHalf + SidewalkWidth / 2f), HillHeightAt(-5.4f, -138f) + 0.12f, -138f),
            new Vector3(-(MainStreetHalf + SidewalkWidth / 2f), HillHeightAt(-5.4f, -196f) + 0.12f, -196f), rng);
        if (hiker != null)
        {
            SerializedObject so = new SerializedObject(hiker.GetComponent<PedestrianWalker>());
            so.FindProperty("followSlope").boolValue = true;
            so.ApplyModifiedProperties();
        }
    }

    // Una franja de la calle (pista, línea, vereda) que sigue la pendiente.
    private static void BuildHillStrip(Transform parent, string name, List<float> rows, float x0, float x1,
        float lift, Color color, bool collider)
    {
        var vertices = new Vector3[rows.Count * 2];
        var uvs = new Vector2[rows.Count * 2];
        var tris = new int[(rows.Count - 1) * 6];
        for (int i = 0; i < rows.Count; i++)
        {
            float z = rows[i];
            vertices[i * 2] = new Vector3(x0, HillHeightAt(x0, z) + lift, z);
            vertices[i * 2 + 1] = new Vector3(x1, HillHeightAt(x1, z) + lift, z);
            uvs[i * 2] = new Vector2(0f, z * 0.25f);
            uvs[i * 2 + 1] = new Vector2(1f, z * 0.25f);
            if (i == rows.Count - 1) continue;
            int t = i * 6, v = i * 2;
            // Visto desde arriba, en sentido horario (cara hacia el cielo).
            tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
            tris[t + 3] = v + 1; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
        }

        Mesh mesh = new Mesh { name = "Subida_" + name };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        SaveGeneratedMesh(mesh);

        GameObject go = MeshObject(parent, name, mesh, Vector3.zero, Vector3.one, color);
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    // Las mallas hechas por código se guardan como asset: si no, la escena
    // guardada las pierde y la calle aparece invisible al volver a abrirla.
    private static void SaveGeneratedMesh(Mesh mesh)
    {
        string dir = ArtDir + "/Generated/Mallas";
        CreateFolderRecursive(dir);
        string path = $"{dir}/{mesh.name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
    }
}
