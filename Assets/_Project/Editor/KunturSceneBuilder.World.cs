using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// El valle en grande: suelo con el cauce del río curvo tallado, texturas de
// pista/vereda/pasto, semáforos que funcionan y el tránsito por carriles.
public static partial class KunturSceneBuilder
{
    // ---------------------------------------------------------------
    // El río Mantaro: recto frente al pueblo (ahí están el puente, las
    // muestras y la basura), y serpenteando aguas arriba y aguas abajo
    // hasta perderse al pie de los cerros.
    // ---------------------------------------------------------------

    private const float TerrainHalf = 340f;
    private const float RiverStartZ = -292f;
    private const float RiverEndZ = 292f;
    private const float RiverStraightFromZ = -60f;
    private const float RiverStraightToZ = 70f;

    // Eje del río (X) para cada Z. Las curvas arrancan con pendiente cero, así
    // el tramo recto empalma sin quiebre. Las dos curvas se abren hacia el
    // este: al oeste está el pueblo y un meandro le pasaría por encima.
    private static float RiverX(float z)
    {
        if (z > RiverStraightToZ)
        {
            float t = z - RiverStraightToZ;
            return RiverCenterX + 24f * (1f - Mathf.Cos(t / 40f));
        }
        if (z < RiverStraightFromZ)
        {
            float t = RiverStraightFromZ - z;
            return RiverCenterX + 20f * (1f - Mathf.Cos(t / 38f));
        }
        return RiverCenterX;
    }

    // Hacia dónde corre el río en ese punto (derivada dX/dZ).
    private static float RiverSlope(float z)
    {
        return (RiverX(z + 0.5f) - RiverX(z - 0.5f));
    }

    // 1 donde hay río, 0 pasando sus extremos: el cauce se va haciendo menos
    // hondo al llegar a los cerros en vez de cortarse de golpe.
    private static float RiverDepthFactor(float z)
    {
        float a = Mathf.Abs(z);
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RiverEndZ - 6f, RiverEndZ + 16f, a));
    }

    // Distancia lateral (en X) al eje del río.
    private static float RiverDistance(Vector3 p) => Mathf.Abs(p.x - RiverX(p.z));

    // ---------------------------------------------------------------
    // Texturas de la calle (de los assets ModularLowpolyStreets) y del pasto
    // ---------------------------------------------------------------

    private const string TextureDir = ArtDir + "/Texturas";

    private static readonly Dictionary<string, Material> SurfaceMaterials = new Dictionary<string, Material>();

    private static Material GetWorldTiledMaterial(string name, string texturePath, Color tint, float tileSize,
        float detailStrength = 0f, bool vertexColor = false)
    {
        if (SurfaceMaterials.TryGetValue(name, out Material cached) && cached != null) return cached;

        Shader shader = Shader.Find("Kuntur/WorldTiled");
        if (shader == null) return null;

        Texture2D texture = texturePath != null ? LoadTiledTexture(texturePath) : null;
        Texture2D detail = LoadTiledTexture(TextureDir + "/Detalle_Pasto.png");

        string dir = ArtDir + "/Materiales";
        CreateFolderRecursive(dir);
        string path = $"{dir}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetTexture("_MainTex", texture);
        mat.SetColor("_Color", tint);
        mat.SetFloat("_TileSize", tileSize);
        mat.SetTexture("_Detail", detail);
        mat.SetFloat("_DetailStrength", detailStrength);
        mat.SetFloat("_VertexColor", vertexColor ? 1f : 0f);
        EditorUtility.SetDirty(mat);

        SurfaceMaterials[name] = mat;
        return mat;
    }

    private static Texture2D LoadTiledTexture(string path)
    {
        if (!System.IO.File.Exists(path)) return null;
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && (importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled || importer.anisoLevel < 4))
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            // Anisotrópico: sin esto el asfalto se ve borroso a pocos metros,
            // porque la pista se mira casi siempre de costado.
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Los colores con que se arman pista, vereda y sardinel reciben su
    // textura real. Así no hubo que tocar las decenas de lugares que los
    // crean: todos piden "el color del asfalto" y ahora les llega el asfalto.
    private static Material TryGetSurfaceMaterial(string colorKey)
    {
        switch (colorKey)
        {
            case "3C4045FF":
                return GetWorldTiledMaterial("Sup_Asfalto", TextureDir + "/Asfalto.png", new Color(0.93f, 0.93f, 0.95f), 3.5f);
            case "B9B5ACFF":
                return GetWorldTiledMaterial("Sup_Vereda", TextureDir + "/Vereda.png", new Color(1f, 0.99f, 0.96f), 2.6f);
            case "D7D2C7FF":
                return GetWorldTiledMaterial("Sup_Sardinel", TextureDir + "/Sardinel.png", new Color(1.05f, 1.05f, 1.02f), 1.2f);
            default:
                return null;
        }
    }

    // ---------------------------------------------------------------
    // Suelo del valle: UNA malla con el cauce tallado siguiendo las curvas.
    // ---------------------------------------------------------------
    // Antes el suelo eran dos cubos con el río recto en medio; un río curvo
    // no se puede hacer con cubos. La malla tiene más detalle cerca del río
    // (un vértice por metro, para que la orilla se vea suave) y poco lejos,
    // donde el suelo es plano. Va en varios pedazos para que la cámara no
    // tenga que dibujarla entera cuando solo se ve una parte.
    private static void BuildValleyGround(Transform parent)
    {
        const float rowStep = 1.5f;
        const int leftCols = 60;
        const int bandCols = 41;     // de -20 a +20 m del eje, cada 1 m
        const int rightCols = 60;
        const int cols = leftCols + bandCols + rightCols;
        const int rowsPerChunk = 76;

        int totalRows = Mathf.FloorToInt(2f * TerrainHalf / rowStep) + 1;

        Color grassDark = HexColor("#679a47");
        Color grassLight = HexColor("#93c466");
        Color mud = HexColor("#6b5a44");
        Color wetSand = HexColor("#b59c78");
        Color bank = HexColor("#a98a66");

        Material material = GetWorldTiledMaterial("Sup_Suelo_Valle", null, Color.white, 4f, 0.28f, true);
        if (material == null) material = GetMaterial(HexColor("#7cae57"));

        int chunkIndex = 0;
        for (int startRow = 0; startRow < totalRows - 1; startRow += rowsPerChunk - 1)
        {
            int rowCount = Mathf.Min(rowsPerChunk, totalRows - startRow);
            var vertices = new Vector3[rowCount * cols];
            var colors = new Color[rowCount * cols];
            var uvs = new Vector2[rowCount * cols];

            for (int r = 0; r < rowCount; r++)
            {
                float z = -TerrainHalf + (startRow + r) * rowStep;
                float rx = RiverX(z);
                float depth = RiverDepthFactor(z);

                for (int c = 0; c < cols; c++)
                {
                    float x;
                    if (c < leftCols) x = Mathf.Lerp(-TerrainHalf, rx - 20f, c / (float)leftCols);
                    else if (c < leftCols + bandCols) x = rx - 20f + (c - leftCols);
                    else x = Mathf.Lerp(rx + 20f, TerrainHalf, (c - leftCols - bandCols + 1) / (float)rightCols);

                    float d = Mathf.Abs(x - rx);
                    // v53: más el cerro de la subida al mirador (0 en el pueblo).
                    float y = RiverProfile(d) * depth + HillHeightAt(x, z);

                    // Pasto con manchas suaves de otro tono (en vez de los
                    // discos planos de antes, que se veían cuadrados).
                    float n1 = Mathf.PerlinNoise(x * 0.018f + 100f, z * 0.018f + 100f);
                    float n2 = Mathf.PerlinNoise(x * 0.07f + 13f, z * 0.07f + 57f);
                    Color grass = Color.Lerp(grassDark, grassLight, Mathf.Clamp01(n1 * 0.8f + n2 * 0.35f - 0.05f));

                    // Orilla como la del dibujo de referencia: el agua llega
                    // hasta la pendiente, una franja de piedra/arena húmeda
                    // donde van las rocas, y enseguida el pasto baja al agua.
                    Color color;
                    // (Medidas relativas al filo del agua, WaterEdge: así
                    // siguen valiendo con el río ancho.)
                    float e = WaterEdge;
                    if (depth < 0.02f) color = grass;
                    else if (d < e - 0.2f) color = mud;
                    else if (d < e + 0.5f) color = Color.Lerp(wetSand, bank, (d - (e - 0.2f)) / 0.7f);
                    else if (d < e + 1.3f) color = Color.Lerp(bank, grassDark, (d - (e + 0.5f)) / 0.8f);
                    else if (d < e + 3.8f) color = Color.Lerp(grassDark, grass, (d - (e + 1.3f)) / 2.5f);
                    else color = grass;
                    if (depth < 1f && d < e + 3.8f) color = Color.Lerp(grass, color, depth);

                    int i = r * cols + c;
                    vertices[i] = new Vector3(x, y, z);
                    colors[i] = color;
                    uvs[i] = new Vector2(x, z);
                }
            }

            var triangles = new int[(rowCount - 1) * (cols - 1) * 6];
            int t = 0;
            for (int r = 0; r < rowCount - 1; r++)
            {
                for (int c = 0; c < cols - 1; c++)
                {
                    int a = r * cols + c;
                    int b = (r + 1) * cols + c;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                    triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
                }
            }

            Mesh mesh = new Mesh { name = $"Suelo_Valle_{chunkIndex}" };
            if (vertices.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // La malla se guarda como asset: si viviera solo en la escena, se
            // perdería al cerrar Unity y el suelo desaparecería.
            string dir = ArtDir + "/Generated";
            CreateFolderRecursive(dir);
            string meshPath = $"{dir}/Suelo_Valle_{chunkIndex}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            GameObject chunk = new GameObject($"Suelo_Valle_{chunkIndex}");
            chunk.transform.SetParent(parent);
            chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = chunk.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            chunk.AddComponent<MeshCollider>().sharedMesh = mesh;
            chunk.isStatic = true;
            chunkIndex++;
        }
    }

    // Paredes invisibles en el filo del agua, siguiendo las curvas: el río no
    // se cruza a pie, se cruza por el puente (z 25–35, donde se cortan).
    private static void BuildCurvedWaterBarriers(Transform parent)
    {
        GameObject root = new GameObject("Barreras_Rio");
        root.transform.SetParent(parent);
        const float step = 4f;
        float edge = WaterWidth / 2f + 0.2f;

        for (float z = RiverStartZ; z < RiverEndZ; z += step)
        {
            float z0 = z, z1 = z + step;
            if (z1 > 25f && z0 < 35f) continue; // el puente
            float zm = (z0 + z1) / 2f;
            float slope = RiverSlope(zm);
            float yaw = Mathf.Atan(slope) * Mathf.Rad2Deg;
            float length = step * Mathf.Sqrt(1f + slope * slope) + 0.3f;

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject barrier = new GameObject($"Barrera_{(side < 0 ? "O" : "E")}_{zm:0}");
                barrier.transform.SetParent(root.transform);
                barrier.transform.position = new Vector3(RiverX(zm) + side * edge, RiverBedY / 2f - 0.15f, zm);
                barrier.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                BoxCollider box = barrier.AddComponent<BoxCollider>();
                box.size = new Vector3(0.4f, Mathf.Abs(RiverBedY) + 0.6f, length);
                barrier.isStatic = true;
            }
        }
    }

    // Eje del agua para RiverWater, en coordenadas locales del objeto agua.
    private static Vector3[] RiverWaterPath(Vector3 waterOrigin)
    {
        var points = new List<Vector3>();
        for (float z = RiverStartZ; z <= RiverEndZ + 0.01f; z += 1.5f)
            points.Add(new Vector3(RiverX(z) - waterOrigin.x, 0f, z - waterOrigin.z));
        return points.ToArray();
    }

    // ---------------------------------------------------------------
    // Semáforos que funcionan
    // ---------------------------------------------------------------

    private static Material signalLensMaterial;

    private static Material GetSignalLensMaterial()
    {
        if (signalLensMaterial != null) return signalLensMaterial;

        string dir = ArtDir + "/Materiales";
        CreateFolderRecursive(dir);
        string path = dir + "/Semaforo_Luz.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = new Color(0.09f, 0.09f, 0.09f);
        // La emisión tiene que estar prendida en el material para que
        // TrafficSignal pueda encender cada luz por separado.
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.black);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetFloat("_Glossiness", 0.6f);
        EditorUtility.SetDirty(mat);
        signalLensMaterial = mat;
        return mat;
    }

    // Cabezal peruano: carcasa amarilla con placa negra de fondo, tres luces
    // (rojo arriba, ámbar, verde abajo) con viseras. "facing" = hacia dónde
    // mira (hacia los carros que se acercan).
    private static void BuildSignalHead(Transform parent, Vector3 center, Vector3 facing, List<Renderer> reds, List<Renderer> yellows, List<Renderer> greens)
    {
        Quaternion rot = Quaternion.LookRotation(facing, Vector3.up);

        PrimitiveObject(parent, "Placa_Fondo", PrimitiveType.Cube, center - facing * 0.19f,
            new Vector3(0.66f, 1.42f, 0.04f), HexColor("#151515"), rot);
        PrimitiveObject(parent, "Carcasa", PrimitiveType.Cube, center,
            new Vector3(0.42f, 1.22f, 0.34f), HexColor("#f2c230"), rot);

        Material lensMat = GetSignalLensMaterial();
        float[] heights = { 0.39f, 0f, -0.39f };
        for (int i = 0; i < 3; i++)
        {
            Vector3 lensPos = center + Vector3.up * heights[i] + facing * 0.18f;
            GameObject lens = PrimitiveObject(parent, $"Luz_{i}", PrimitiveType.Cylinder, lensPos,
                new Vector3(0.27f, 0.02f, 0.27f), Color.black, rot * Quaternion.Euler(90f, 0f, 0f));
            Renderer renderer = lens.GetComponent<Renderer>();
            renderer.sharedMaterial = lensMat;
            lens.isStatic = false;
            (i == 0 ? reds : i == 1 ? yellows : greens).Add(renderer);

            // Visera sobre cada luz: da sombra y es lo que hace que se lea como
            // semáforo de verdad y no como tres pelotitas.
            PrimitiveObject(parent, $"Visera_{i}", PrimitiveType.Cube, lensPos + Vector3.up * 0.16f + facing * 0.08f,
                new Vector3(0.32f, 0.025f, 0.18f), HexColor("#d9a91e"), rot);
        }
    }

    private const string SignalPolePrefab =
        "Assets/Tarbo-CITY-TrafficLights/Prefabs/Props/Road/TB_CITY_Prop_TrafficLight_Base_I_A.prefab";

    // Un cruce con semáforo: un poste en cada esquina, cada uno con el
    // cabezal mirando a los carros que llegan por su derecha (en el Perú el
    // semáforo que te toca está en la esquina de tu derecha, antes de cruzar).
    private static void BuildSignalizedIntersection(Transform parent, string name, float x, float z,
        float halfNS, float halfEW, float phaseOffset)
    {
        GameObject root = new GameObject($"Semaforo_{name}");
        root.transform.SetParent(parent);
        root.transform.position = new Vector3(x, 0f, z);

        var nsRed = new List<Renderer>(); var nsYellow = new List<Renderer>(); var nsGreen = new List<Renderer>();
        var ewRed = new List<Renderer>(); var ewYellow = new List<Renderer>(); var ewGreen = new List<Renderer>();

        float ox = halfNS + 1.9f;
        float oz = halfEW + 1.9f;
        // (esquina, hacia dónde mira el cabezal, ¿es de la calle norte-sur?)
        var corners = new (Vector3 pos, Vector3 facing, bool ns)[]
        {
            (new Vector3(x + ox, 0f, z - oz), Vector3.back, true),            // SE: los que suben (norte)
            (new Vector3(x - ox, 0f, z + oz), Vector3.forward, true),         // NO: los que bajan (sur)
            (new Vector3(x - ox, 0f, z - oz), Vector3.left, false),           // SO: los que van al este
            (new Vector3(x + ox + 1.8f, 0f, z + oz), Vector3.right, false),   // NE: los que van al oeste (corrido: ahí está el letrero)
        };

        GameObject polePrefab = LoadPrefab(SignalPolePrefab);
        foreach (var corner in corners)
        {
            if (IsOnRoadway(corner.pos)) continue;

            GameObject pole = new GameObject("Poste_Semaforo");
            pole.transform.SetParent(root.transform);
            const float poleHeight = 4.6f;
            if (polePrefab != null)
            {
                GameObject model = PlaceModel(polePrefab, pole.transform, "Poste", corner.pos, Quaternion.identity, poleHeight, out _);
                SetStaticRecursive(model, true);
            }
            else
            {
                PrimitiveObject(pole.transform, "Poste", PrimitiveType.Cylinder, corner.pos + Vector3.up * poleHeight / 2f,
                    new Vector3(0.16f, poleHeight / 2f, 0.16f), HexColor("#2b2d30"));
            }

            // Base a franjas amarillas y negras, como las de Huancayo.
            PrimitiveObject(pole.transform, "Base_Amarilla", PrimitiveType.Cylinder, corner.pos + Vector3.up * 0.4f,
                new Vector3(0.3f, 0.4f, 0.3f), HexColor("#f1c40f"));
            PrimitiveObject(pole.transform, "Base_Negra", PrimitiveType.Cylinder, corner.pos + Vector3.up * 0.4f,
                new Vector3(0.305f, 0.1f, 0.305f), HexColor("#1f1f1f"));

            Vector3 headCenter = corner.pos + Vector3.up * (poleHeight - 0.85f) + corner.facing * 0.32f;
            if (corner.ns) BuildSignalHead(pole.transform, headCenter, corner.facing, nsRed, nsYellow, nsGreen);
            else BuildSignalHead(pole.transform, headCenter, corner.facing, ewRed, ewYellow, ewGreen);
        }

        TrafficSignal signal = root.AddComponent<TrafficSignal>();
        SerializedObject so = new SerializedObject(signal);
        so.FindProperty("halfSizeX").floatValue = halfNS;
        so.FindProperty("halfSizeZ").floatValue = halfEW;
        so.FindProperty("phaseOffset").floatValue = phaseOffset;
        SetRendererArray(so.FindProperty("northSouthRed"), nsRed);
        SetRendererArray(so.FindProperty("northSouthYellow"), nsYellow);
        SetRendererArray(so.FindProperty("northSouthGreen"), nsGreen);
        SetRendererArray(so.FindProperty("eastWestRed"), ewRed);
        SetRendererArray(so.FindProperty("eastWestYellow"), ewYellow);
        SetRendererArray(so.FindProperty("eastWestGreen"), ewGreen);
        so.ApplyModifiedProperties();
    }

    private static void SetRendererArray(SerializedProperty prop, List<Renderer> list)
    {
        prop.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
    }

    // Cruces con semáforo: toda la Av. Giráldez y los cruces más movidos de
    // la Calle Real. En esos cruces NO va la señal de PARE (se contradirían).
    private static readonly float[] SignalizedRealCrossZ = { -45f, -18f, 58f };

    private static void BuildTrafficSignals(Transform parent)
    {
        GameObject root = new GameObject("Semaforos");
        root.transform.SetParent(parent);

        float offset = 0f;
        foreach ((float x, float half, string name) in new[] {
            (ArequipaX, CrossStreetHalfRoad, "Arequipa_Giraldez"), (PunoX, CrossStreetHalfRoad, "Puno_Giraldez"),
            (MainStreetX, MainStreetHalf, "Real_Giraldez"), (CuscoX, CrossStreetHalfRoad, "Cusco_Giraldez") })
        {
            BuildSignalizedIntersection(root.transform, name, x, AvenueZ, half, AvenueHalf, offset);
            offset += 5.5f; // "ola verde" suave: no cambian todos a la vez
        }

        foreach (float z in SignalizedRealCrossZ)
        {
            BuildSignalizedIntersection(root.transform, $"Real_{z:0}", MainStreetX, z, MainStreetHalf, CrossStreetHalfRoad, offset);
            offset += 4f;
        }
    }

    // ---------------------------------------------------------------
    // Tránsito por carriles
    // ---------------------------------------------------------------

    private static void ConfigureLane(GameObject vehicle, float laneOffset, float progress)
    {
        if (vehicle == null) return;
        CarPatrol patrol = vehicle.GetComponent<CarPatrol>();
        if (patrol == null) return;
        SerializedObject so = new SerializedObject(patrol);
        so.FindProperty("laneOffset").floatValue = laneOffset;
        so.FindProperty("startProgress").floatValue = progress;
        so.ApplyModifiedProperties();
    }

    // Cada vehículo va y viene por UNA calle, por su carril derecho, y da la
    // vuelta en U al final. Los que comparten calle arrancan repartidos en el
    // circuito para no salir amontonados.
    private static void BuildTraffic(Transform parent)
    {
        GameObject root = new GameObject("Transito");
        root.transform.SetParent(parent);

        const float wide = 2.0f;    // carril de la Calle Real y la avenida (pista de 8)
        const float narrow = 1.6f;  // carril de los jirones (pista de 6.5)

        // v53: cada vehículo va en UN solo sentido (uno sí y otro no, en
        // sentidos contrarios); al llegar al final desaparece y vuelve a salir
        // desde su punto de partida con otro modelo (ver CarPatrol).
        int count = 0;
        void Add(string name, VehicleKind kind, Vector3 a, Vector3 b, float speed, float lane, float progress, string route = null)
        {
            bool reverse = (count++ % 2) == 1;
            GameObject v = BuildAssetVehicle(root.transform, name, kind, reverse ? b : a, reverse ? a : b, speed, null, true);
            ConfigureLane(v, lane, progress);
        }

        Vector3 realA = new Vector3(MainStreetX, 0f, TownSouthZ + 4f), realB = new Vector3(MainStreetX, 0f, TownNorthZ - 4f);
        Add("Auto_Real_1", VehicleKind.Car, realA, realB, 10f, wide, 0.02f);
        Add("Auto_Real_2", VehicleKind.Car, realA, realB, 9.5f, wide, 0.3f);
        Add("Auto_Real_3", VehicleKind.Car, realA, realB, 10f, wide, 0.62f);
        Add("Patrullero", VehicleKind.Police, realA, realB, 8f, wide, 0.84f);
        Add("Combi_Real", VehicleKind.Combi, realA, realB, 8.5f, wide, 0.46f, "EL TAMBO - CHILCA");

        // La avenida entera, CRUZANDO el puente hasta la otra orilla: antes
        // daban la vuelta justo antes del río.
        Vector3 girA = new Vector3(-126f, 0f, AvenueZ), girB = new Vector3(126f, 0f, AvenueZ);
        Add("Bus_Giraldez", VehicleKind.Bus, girA, girB, 7f, wide, 0.05f, "LÍNEA 25");
        Add("Combi_Giraldez", VehicleKind.Combi, girA, girB, 8.5f, wide, 0.32f, "CENTRO - SAN CARLOS");
        Add("Auto_Giraldez_1", VehicleKind.Car, girA, girB, 10f, wide, 0.55f);
        Add("Auto_Giraldez_2", VehicleKind.Car, girA, girB, 9.5f, wide, 0.8f);

        Add("Auto_Giraldez_3", VehicleKind.Car, girA, girB, 9f, wide, 0.68f);

        foreach ((float x, string name, float p) in new[] { (PunoX, "Puno", 0.1f), (CuscoX, "Cusco", 0.55f), (ArequipaX, "Arequipa", 0.3f) })
        {
            Vector3 a = new Vector3(x, 0f, TownSouthZ + 12f), b = new Vector3(x, 0f, TownNorthZ - 12f);
            Add($"Auto_{name}", VehicleKind.Car, a, b, 8.5f, narrow, p);
            if (name != "Arequipa")
                Add($"Combi_{name}", VehicleKind.Combi, a, b, 8f, narrow, p + 0.5f, name == "Puno" ? "PILCOMAYO - HUANCAYO" : "SAPALLANGA - CENTRO");
        }

        float fromX = CrossStreetFromX + 2f, toX = CrossStreetToX - 3f;
        Add("Bus_Ferrocarril", VehicleKind.Bus, new Vector3(fromX, 0f, 58f), new Vector3(toX, 0f, 58f), 6.5f, narrow, 0.15f, "LÍNEA 7");
        Add("Auto_Ferrocarril", VehicleKind.Car, new Vector3(fromX, 0f, 58f), new Vector3(toX, 0f, 58f), 8f, narrow, 0.65f);
        Add("Auto_Loreto", VehicleKind.Car, new Vector3(fromX, 0f, -18f), new Vector3(toX, 0f, -18f), 8f, narrow, 0.4f);
        Add("Auto_Junin", VehicleKind.Car, new Vector3(fromX, 0f, -45f), new Vector3(toX, 0f, -45f), 8f, narrow, 0.85f);
    }
}
