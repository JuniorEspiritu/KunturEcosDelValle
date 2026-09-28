using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// v56: el tuk tuk de Kuntur, calles que no terminan en el pasto (todas
// empalman y los autos dan la vuelta a la manzana), casas pegadas a las
// veredas llenando los huecos de pasto, y un puente peatonal sobre el río.
public static partial class KunturSceneBuilder
{
    // La Av. Giráldez, del otro lado del puente, llega en T al camino de la chacra.
    private const float GiraldezEastEndX = EastRoadX - 3.5f;
    // Puente peatonal: v56c, cerca del puente de la Giráldez, siguiendo la
    // línea del Jr. Ayacucho hasta la otra orilla.
    private const float FootbridgeZ = 14f;

    private static bool IsRing(float z) => Mathf.Approximately(z, SouthRingZ) || Mathf.Approximately(z, NorthRingZ);

    // ---------------------------------------------------------------
    // Veredas
    // ---------------------------------------------------------------

    // Vereda con los cortes que se le indiquen (para las de "afuera" del
    // pueblo, que solo se cortan donde una calle sigue de largo).
    private static void BuildSidewalkCustom(Transform parent, string name, float fixedCoord, float roadCenter,
        float from, float to, bool alongZ, List<Vector2> gaps, Color surface, Color curb)
    {
        int piece = 0;
        BuildSidewalkPieces(gaps, from, to, (a, b) =>
        {
            if (alongZ)
                BuildSidewalk(parent, $"{name}_{piece++}", new Vector3(fixedCoord, 0f, (a + b) / 2f),
                    new Vector3(SidewalkWidth, 0.12f, b - a), surface, curb, true, roadCenter);
            else
                BuildSidewalk(parent, $"{name}_{piece++}", new Vector3((a + b) / 2f, 0f, fixedCoord),
                    new Vector3(b - a, 0.12f, SidewalkWidth), surface, curb, false, roadCenter);
        });
    }

    private static void BuildRingSidewalks(Transform parent, int index, float z, Color surface, Color curb)
    {
        bool south = z < 0f;
        float inner = south ? 1f : -1f;   // hacia el pueblo
        float innerZ = z + inner * (CrossStreetHalfRoad + SidewalkWidth / 2f);
        float outerZ = z - inner * (CrossStreetHalfRoad + SidewalkWidth / 2f);

        BuildSidewalkAlongX(parent, $"Vereda_Circunvalacion{index}_Adentro", innerZ, z, CrossStreetFromX, CrossStreetToX, surface, curb);

        // Por fuera: de esquina a esquina (tapando las esquinas de afuera).
        // En la del sur siguen de largo la Real, el Puno y el Cusco (suben
        // al mirador); en la del norte no sigue ninguna.
        var gaps = new List<Vector2>();
        if (south)
        {
            gaps.Add(new Vector2(MainStreetX - MainStreetHalf - 0.2f, MainStreetX + MainStreetHalf + 0.2f));
            gaps.Add(new Vector2(PunoX - CrossStreetHalfRoad - 0.2f, PunoX + CrossStreetHalfRoad + 0.2f));
            gaps.Add(new Vector2(CuscoX - CrossStreetHalfRoad - 0.2f, CuscoX + CrossStreetHalfRoad + 0.2f));
        }
        BuildSidewalkCustom(parent, $"Vereda_Circunvalacion{index}_Afuera", outerZ, z,
            CrossStreetFromX - SidewalkWidth, CrossStreetToX + SidewalkWidth, false, gaps, surface, curb);
    }

    // ---------------------------------------------------------------
    // Puente peatonal
    // ---------------------------------------------------------------
    private static void BuildFootbridge(Transform parent)
    {
        GameObject root = new GameObject("Puente_Peatonal");
        root.transform.SetParent(parent);

        float riverX = RiverX(FootbridgeZ);
        float x0 = riverX - RiverBankHalf - 1f;   // un poco antes del borde de la orilla
        float x1 = riverX + RiverBankHalf + 1f;
        const float width = 2.6f;
        const float rise = 1.9f;                  // arco suave: sube al centro y baja
        const int segments = 16;
        float len = x1 - x0;

        Color wood = HexColor("#9a6b43");
        Color woodDark = HexColor("#6d4a2e");
        Color rail = HexColor("#f4f1ea");
        Color accent = HexColor("#2f6db5");

        float Y(float x) => 0.12f + rise * Mathf.Sin(Mathf.PI * Mathf.Clamp01((x - x0) / len));

        for (int i = 0; i < segments; i++)
        {
            float a = x0 + len * i / segments;
            float b = x0 + len * (i + 1) / segments;
            Vector3 pa = new Vector3(a, Y(a), FootbridgeZ);
            Vector3 pb = new Vector3(b, Y(b), FootbridgeZ);
            Vector3 mid = (pa + pb) * 0.5f;
            Vector3 dir = pb - pa;
            Quaternion rot = Quaternion.LookRotation(dir.normalized) * Quaternion.Euler(0f, -90f, 0f);

            // Tablero con collider: se camina encima.
            GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "Tablero";
            deck.transform.SetParent(root.transform);
            deck.transform.position = mid - Vector3.up * 0.1f;
            deck.transform.rotation = rot;
            deck.transform.localScale = new Vector3(dir.magnitude + 0.05f, 0.2f, width);
            SetColor(deck, i % 2 == 0 ? wood : HexColor("#a8774d"));
            deck.isStatic = true;

            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 edge = new Vector3(0f, 0f, side * (width / 2f - 0.06f));
                // Pasamanos blanco y franja azul, como los puentes colgantes del valle.
                PrimitiveObject(root.transform, "Pasamanos", PrimitiveType.Cube, mid + edge + Vector3.up * 1.0f,
                    new Vector3(dir.magnitude + 0.05f, 0.08f, 0.08f), rail, rot);
                PrimitiveObject(root.transform, "Franja", PrimitiveType.Cube, mid + edge + Vector3.up * 0.5f,
                    new Vector3(dir.magnitude + 0.05f, 0.06f, 0.06f), accent, rot);
                PrimitiveObject(root.transform, "Poste", PrimitiveType.Cube, pa + edge + Vector3.up * 0.5f,
                    new Vector3(0.1f, 1.05f, 0.1f), rail);

                // Pared invisible: nadie se cae al río.
                GameObject wall = new GameObject("Borde");
                wall.transform.SetParent(root.transform);
                wall.transform.position = mid + edge + Vector3.up * 0.7f;
                wall.transform.rotation = rot;
                BoxCollider col = wall.AddComponent<BoxCollider>();
                col.size = new Vector3(dir.magnitude + 0.1f, 1.4f, 0.12f);
                wall.isStatic = true;
            }
        }

        // Pilares en el agua y en cada orilla.
        foreach (float px in new[] { x0 + 1.2f, riverX - 4.5f, riverX + 4.5f, x1 - 1.2f })
        {
            float top = Y(px);
            float bottom = RiverBedY - 0.2f;
            PrimitiveObject(root.transform, "Pilar", PrimitiveType.Cube,
                new Vector3(px, (top + bottom) / 2f - 0.15f, FootbridgeZ), new Vector3(0.45f, top - bottom, width + 0.3f), woodDark);
        }

        // Caminitos a cada lado: del Jr. Cusco al puente y del puente al
        // camino de la chacra. Se apartan antes de construir las casas.
        float pathFrom = CuscoX + CrossStreetHalfRoad + SidewalkWidth;
        PrimitiveObject(root.transform, "Caminito_Oeste", PrimitiveType.Cube,
            new Vector3((pathFrom + x0) / 2f, 0.07f, FootbridgeZ), new Vector3(x0 - pathFrom + 0.3f, 0.1f, width + 0.4f), HexColor("#c9bfae"));
        PrimitiveObject(root.transform, "Caminito_Este", PrimitiveType.Cube,
            new Vector3((x1 + EastRoadX - 3.5f) / 2f, 0.07f, FootbridgeZ), new Vector3(EastRoadX - 3.5f - x1 + 0.3f, 0.1f, width + 0.4f), HexColor("#b09166"));
        Occupy(new Vector3((pathFrom + x0) / 2f, 0f, FootbridgeZ), x0 - pathFrom + 1f, width + 2f);
        Occupy(new Vector3((x1 + EastRoadX) / 2f, 0f, FootbridgeZ), EastRoadX - x1, width + 2f);

        // Faroles en las dos entradas.
        BuildStreetLamp(root.transform, new Vector3(x0 - 1.4f, 0f, FootbridgeZ + width / 2f + 0.5f), Vector3.back, false);
        BuildStreetLamp(root.transform, new Vector3(x1 + 1.4f, 0f, FootbridgeZ - width / 2f - 0.5f), Vector3.forward, false);
        BuildStreetSign(root.transform, new Vector3(pathFrom + 1.5f, 0f, FootbridgeZ + width / 2f + 1.3f), "PUENTE PEATONAL", 90f);
    }

    // ---------------------------------------------------------------
    // Casas pegadas a la vereda de los jirones, y relleno de los huecos
    // ---------------------------------------------------------------
    private static void BuildStreetFacingHouses(Transform parent, Color[] walls, System.Random rng)
    {
        int index = 0;
        var streets = new List<(float z, float half, float fromX, float toX)>();
        foreach (float z in CrossStreetsZ)
            streets.Add((z, CrossStreetHalfRoad, CrossStreetFromX - SidewalkWidth - 6f, CrossStreetToX + SidewalkWidth + 6f));
        streets.Add((AvenueZ, AvenueHalf, CrossStreetFromX, BridgeStartX - 2f));

        foreach (var st in streets)
        {
            float edge = st.half + SidewalkWidth;
            foreach (float side in new[] { 1f, -1f })
            {
                // Paso corto y tres tamaños: si la casa grande no entra en el
                // hueco, entra una mediana o una chica. Así no quedan lotes
                // de pasto vacíos entre casa y casa.
                for (float x = st.fromX; x <= st.toX; x += 1.25f)
                {
                    foreach ((float f0, float f1, float d0, float d1) in HouseSizes)
                    {
                        float frontage = NextFloat(rng, f0, f1);   // de cara a la calle (en X)
                        float depth = NextFloat(rng, d0, d1);      // hacia adentro de la manzana (en Z)
                        // Pegada a la vereda: el filo de la vereda + medio metro.
                        float z = st.z + side * (edge + 0.55f + depth / 2f);
                        if (BuildHouseFacingZ(parent, $"Casa_Jiron_{index}", new Vector3(x + frontage / 2f, 0f, z),
                                NextFloat(rng, 3.2f, 5.6f), frontage, depth, walls[index % walls.Length], rng, -side))
                        {
                            index++;
                            x += frontage - 1.25f;
                            break;
                        }
                    }
                }
            }
        }
    }

    // Casa con la fachada hacia +Z (facing = 1) o -Z (facing = -1). Se arma
    // mirando en X y se gira 90°: así sale igual de detallada que las demás.
    private static bool BuildHouseFacingZ(Transform parent, string name, Vector3 groundPosition, float height,
        float frontage, float depth, Color wallColor, System.Random rng, float facing)
    {
        float halfX = frontage / 2f + 0.3f;
        float halfZ = depth / 2f + 0.3f;
        if (!IsAreaFree(groundPosition, halfX, halfZ)) return false;
        if (IsOnReservedSpot(groundPosition)) return false;
        Occupy(groundPosition, halfX * 2f, halfZ * 2f);

        GameObject pivot = new GameObject("_giro");
        // Armada en el origen mirando a +X (ancho en X = fondo, fondo en Z = frente)...
        GameObject house = BuildHouseBody(pivot.transform, name, Vector3.zero, height, depth, frontage, wallColor, rng, 1f);
        // ...y girada para que la fachada mire a la calle.
        pivot.transform.SetPositionAndRotation(groundPosition, Quaternion.Euler(0f, facing > 0f ? -90f : 90f, 0f));
        house.transform.SetParent(parent, true);
        Object.DestroyImmediate(pivot);
        return true;
    }

    // Lo que queda de pasto ADENTRO de las manzanas: casas más chicas, de
    // cara a la calle más cercana.
    // Tamaños de casa que se prueban, de la más grande a la más chica
    // (frente mín/máx, fondo mín/máx).
    private static readonly (float f0, float f1, float d0, float d1)[] HouseSizes =
    {
        (6.8f, 7.6f, 7f, 8.2f), (5.8f, 6.6f, 6f, 7f), (4.8f, 5.6f, 5f, 6f),
    };

    private static void FillEmptyLots(Transform parent, Color[] walls, System.Random rng)
    {
        int index = 0;
        for (float z = SouthRingZ + 7f; z <= NorthRingZ - 7f; z += 2f)
        {
            for (float x = ArequipaX + 7f; x <= CuscoX - 7f; x += 2f)
            {
                Vector3 p = new Vector3(x, 0f, z);
                if (IsOnReservedSpot(p)) continue;
                foreach ((float f0, float f1, float d0, float d1) in HouseSizes)
                {
                    float width = NextFloat(rng, d0, d1);   // fondo (en X: la fachada mira a la calle más cercana)
                    float depth = NextFloat(rng, f0, f1);   // frente (en Z)
                    if (!IsAreaFree(p, width / 2f + 0.3f, depth / 2f + 0.3f)) continue;
                    BuildHouse(parent, $"Casa_Lote_{index}", p, NextFloat(rng, 3.2f, 5.4f), width, depth, walls[index % walls.Length], rng);
                    index++;
                    break;
                }
            }
        }
        Debug.Log($"[Kuntur] Casas nuevas en los lotes vacíos: {index}.");
    }

    // ---------------------------------------------------------------
    // Red de calles (la usa el tuk tuk para llegar solo, y la ruta morada)
    // ---------------------------------------------------------------
    private static List<RoadNetwork.Segment> RoadSegments()
    {
        var list = new List<RoadNetwork.Segment>();
        void Add(float ax, float az, float bx, float bz) =>
            list.Add(new RoadNetwork.Segment { a = new Vector2(ax, az), b = new Vector2(bx, bz) });

        Add(MainStreetX, TopRoadZ, MainStreetX, NorthRingZ);          // Calle Real (con la subida)
        Add(PunoX, TopRoadZ, PunoX, NorthRingZ);                      // Jr. Puno (con la subida)
        Add(CuscoX, TopRoadZ, CuscoX, NorthRingZ);                    // Jr. Cusco (con la subida)
        Add(ArequipaX, SouthRingZ, ArequipaX, NorthRingZ);            // Jr. Arequipa
        foreach (float z in CrossStreetsZ) Add(ArequipaX, z, CuscoX, z);   // jirones y circunvalaciones
        Add(ArequipaX, AvenueZ, EastRoadX, AvenueZ);                  // Av. Giráldez con el puente
        Add(EastRoadX, EastRoadFromZ, EastRoadX, EastRoadToZ);        // camino de la chacra
        Add(PunoX, TopRoadZ, CuscoX, TopRoadZ);                       // calle del mirador
        return list;
    }

    private static void BuildRoadNetwork(Transform parent)
    {
        GameObject go = new GameObject("Red_De_Calles");
        go.transform.SetParent(parent);
        RoadNetwork net = go.AddComponent<RoadNetwork>();
        List<RoadNetwork.Segment> segs = RoadSegments();
        SerializedObject so = new SerializedObject(net);
        SerializedProperty arr = so.FindProperty("segments");
        arr.arraySize = segs.Count;
        for (int i = 0; i < segs.Count; i++)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("a").vector2Value = segs[i].a;
            e.FindPropertyRelative("b").vector2Value = segs[i].b;
        }
        so.ApplyModifiedProperties();
    }

    // ---------------------------------------------------------------
    // El tuk tuk de Kuntur
    // ---------------------------------------------------------------
    private const string TukTukDir = "Assets/XR_Interactive/Low_Poly_Auto_Rickshaws/Prefab/";
    // Estacionado en el Jr. Arequipa, junto a la vereda de la casa de Kuntur
    // (carril del lado de la casa, mirando al sur como ese carril).
    private static readonly Vector3 TukTukSpot = new Vector3(ArequipaX - 1.9f, 0f, -113.5f);
    private const float TukTukLength = 2.9f;

    private static void BuildTukTuk(Transform parent)
    {
        GameObject prefab = null;
        foreach (string color in new[] { "Red", "Blue", "Yellow", "Purple", "Pink" })
        {
            prefab = LoadPrefab(TukTukDir + $"Low_Poly_Auto_Rickshaw_{color}.prefab");
            if (prefab != null) break;
        }
        if (prefab == null)
        {
            Debug.LogWarning("[Kuntur] No encontré el asset del tuk tuk (Low Poly Auto Rickshaw Pack): Kuntur se queda sin su mototaxi.");
            return;
        }

        GameObject root = new GameObject("TukTuk_Kuntur");
        root.transform.SetParent(parent);
        root.transform.position = TukTukSpot;

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        if (model == null) model = Object.Instantiate(prefab, root.transform);
        model.name = "Modelo_TukTuk";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        foreach (Camera c in model.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
        foreach (Light l in model.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        RemoveColliders(model);
        FixPipelineMaterials(model);

        // A su tamaño real (~2.9 m de largo), apoyado en el suelo y centrado.
        Bounds b = LocalRendererBounds(model, root.transform);
        if (b.size.z > 0.01f)
        {
            model.transform.localScale *= TukTukLength / b.size.z;
            b = LocalRendererBounds(model, root.transform);
        }
        model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
        b = LocalRendererBounds(model, root.transform);

        // Asientos: los puntos vienen de las piezas del modelo (el asiento del
        // conductor va detrás del manubrio; el de pasajeros, atrás).
        Transform seat = new GameObject("Asiento_Conductor").transform;
        seat.SetParent(root.transform, false);
        seat.position = model.transform.TransformPoint(new Vector3(0f, 0.78f, 0.38f));
        Transform back = new GameObject("Asiento_Atras").transform;
        back.SetParent(root.transform, false);
        back.position = model.transform.TransformPoint(new Vector3(0f, 0.82f, -0.55f));

        var wheels = new List<Transform>();
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            if (t.name.ToLowerInvariant().Contains("wheel")) wheels.Add(t);

        // Una caja para chocar con Kuntur cuando camina (manejando se ignora).
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = new Vector3(b.size.x * 0.9f, b.size.y * 0.95f, b.size.z * 0.95f);

        DrivableVehicle vehicle = root.AddComponent<DrivableVehicle>();
        SerializedObject so = new SerializedObject(vehicle);
        so.FindProperty("displayName").stringValue = "tuk tuk";
        so.FindProperty("isTukTuk").boolValue = true;
        so.FindProperty("seat").objectReferenceValue = seat;
        so.FindProperty("backSeat").objectReferenceValue = back;
        SerializedProperty wp = so.FindProperty("wheels");
        wp.arraySize = wheels.Count;
        for (int i = 0; i < wheels.Count; i++) wp.GetArrayElementAtIndex(i).objectReferenceValue = wheels[i];
        so.FindProperty("wheelRadius").floatValue = 0.288f * model.transform.localScale.y;
        so.FindProperty("maxSpeed").floatValue = 16f;
        so.FindProperty("turnRate").floatValue = 120f;   // v56c: dobla más cerrado
        so.FindProperty("acceleration").floatValue = 7f;
        so.FindProperty("engineClip").objectReferenceValue = LoadAudio("SFX_TukTuk_Motor");
        so.FindProperty("startClip").objectReferenceValue = LoadAudio("SFX_TukTuk_Arranque");
        so.FindProperty("hornClip").objectReferenceValue = LoadAudio("SFX_TukTuk_Claxon");
        so.FindProperty("crashClip").objectReferenceValue = LoadAudio("SFX_TukTuk_Choque");
        so.ApplyModifiedProperties();

        root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        SetStaticRecursive(root, false);
    }

    private static Bounds LocalRendererBounds(GameObject go, Transform space)
    {
        bool any = false;
        Bounds result = new Bounds();
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Bounds lb = r.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                else result.Encapsulate(p);
            }
        }
        return result;
    }

    // ---------------------------------------------------------------
    // Todos los vehículos se pueden manejar
    // ---------------------------------------------------------------
    private static void MakeVehiclesDrivable(Transform world)
    {
        AudioClip start = LoadAudio("SFX_TukTuk_Arranque");
        AudioClip engine = LoadAudio("SFX_Motor");
        AudioClip horn = LoadAudio("SFX_Claxon");

        var targets = new List<GameObject>();
        foreach (CarPatrol cp in world.GetComponentsInChildren<CarPatrol>(true)) targets.Add(cp.gameObject);
        foreach (Transform t in world.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Auto_Estacionado") && !targets.Contains(t.gameObject)) targets.Add(t.gameObject);

        foreach (GameObject v in targets)
        {
            if (v.GetComponent<DrivableVehicle>() != null) continue;
            // Estáticos no se pueden mover (Unity los junta en un solo bloque).
            SetStaticRecursive(v, false);
            DrivableVehicle d = v.AddComponent<DrivableVehicle>();
            SerializedObject so = new SerializedObject(d);
            string n = v.name.ToLowerInvariant();
            float half = ActiveHalfLength(v);
            string label = n.Contains("patrullero") ? "patrullero"
                : half > 3.2f ? (n.Contains("bus") ? "bus" : "camión")
                : half > 2.15f ? "combi" : "auto";
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("maxSpeed").floatValue = half > 3.2f ? 13f : 19f;
            so.FindProperty("turnRate").floatValue = half > 3.2f ? 48f : 68f;
            so.FindProperty("acceleration").floatValue = half > 3.2f ? 4.5f : 7f;
            so.FindProperty("engineClip").objectReferenceValue = engine;
            so.FindProperty("startClip").objectReferenceValue = start;
            so.FindProperty("hornClip").objectReferenceValue = horn;
            so.ApplyModifiedProperties();
        }
    }

    private static float ActiveHalfLength(GameObject vehicle)
    {
        foreach (Transform child in vehicle.transform)
        {
            if (!child.gameObject.activeSelf) continue;
            BoxCollider box = child.GetComponent<BoxCollider>();
            if (box != null) return box.size.z / 0.96f / 2f;
        }
        return 2.2f;
    }

    // ---------------------------------------------------------------
    // Tránsito: vueltas a la manzana
    // ---------------------------------------------------------------
    // Carril de cada calle: la Real y la avenida son más anchas.
    private static float LaneFor(Vector2 a, Vector2 b)
    {
        bool alongZ = Mathf.Abs(a.x - b.x) < 0.01f;
        if (alongZ && Mathf.Abs(a.x - MainStreetX) < 0.01f) return 2.0f;
        if (!alongZ && Mathf.Abs(a.y - AvenueZ) < 0.01f) return 2.0f;
        return 1.6f;
    }

    // Circuito cerrado por las esquinas dadas (ejes de las calles), en el
    // carril derecho, con las curvas redondeadas, las vueltas en U en
    // redondo y la altura del suelo (la cuesta del mirador).
    private static Vector3[] BuildCircuit(params Vector2[] corners)
    {
        int n = corners.Length;
        var pts = new List<Vector3>();
        for (int i = 0; i < n; i++)
        {
            Vector2 prev = corners[(i - 1 + n) % n];
            Vector2 c = corners[i];
            Vector2 next = corners[(i + 1) % n];
            Vector2 dIn = (c - prev).normalized;
            Vector2 dOut = (next - c).normalized;
            Vector2 rIn = new Vector2(dIn.y, -dIn.x);
            Vector2 rOut = new Vector2(dOut.y, -dOut.x);
            float oIn = LaneFor(prev, c);
            float oOut = LaneFor(c, next);

            // Tramo recto desde la esquina anterior (subdividido para seguir el cerro).
            if (pts.Count > 0)
            {
                Vector3 last = pts[pts.Count - 1];
                Vector2 from = new Vector2(last.x, last.z);
                Vector2 entry = c + rIn * oIn - dIn * 5f;
                float len = Vector2.Distance(from, entry);
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / 4f));
                for (int s = 1; s < steps; s++) pts.Add(Ground(Vector2.Lerp(from, entry, s / (float)steps)));
            }

            float dot = Vector2.Dot(dIn, dOut);
            if (dot < -0.9f)
            {
                // Vuelta en U alrededor del final de la calle.
                for (int k = 0; k <= 6; k++)
                {
                    float t = Mathf.PI * k / 6f;
                    pts.Add(Ground(c + rIn * (oIn * Mathf.Cos(t)) + dIn * (oIn * Mathf.Sin(t))));
                }
                continue;
            }
            if (dot > 0.9f)
            {
                pts.Add(Ground(c + rIn * oIn));
                continue;
            }

            // Esquina: el punto donde se cruzan los dos carriles, redondeado.
            Vector2 p = c + rIn * oIn + rOut * oOut;
            float cross = dIn.x * dOut.y - dIn.y * dOut.x;
            float r = cross < 0f ? 3.2f : 5.5f;   // a la derecha se dobla más cerrado
            Vector2 center = p - dIn * r + dOut * r;
            for (int k = 0; k <= 4; k++)
            {
                float t = Mathf.PI * 0.5f * k / 4f;
                pts.Add(Ground(center + (-dOut * Mathf.Cos(t) + dIn * Mathf.Sin(t)) * r));
            }
        }

        // Cierre: del último punto al primero, subdividido.
        Vector3 end = pts[pts.Count - 1], first = pts[0];
        Vector2 e2 = new Vector2(end.x, end.z), f2 = new Vector2(first.x, first.z);
        int closeSteps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(e2, f2) / 4f));
        for (int s = 1; s < closeSteps; s++) pts.Add(Ground(Vector2.Lerp(e2, f2, s / (float)closeSteps)));
        return pts.ToArray();
    }

    private static Vector3 Ground(Vector2 p)
    {
        float h = HillHeightAt(p.x, p.y);
        return new Vector3(p.x, h > 0.001f ? h + 0.03f : 0f, p.y);
    }

    private static void BuildCircuitTraffic(Transform parent)
    {
        GameObject root = new GameObject("Transito");
        root.transform.SetParent(parent);

        const float R = MainStreetX, P = PunoX, C = CuscoX, A = ArequipaX;
        const float SR = SouthRingZ, NR = NorthRingZ, T = TopRoadZ;

        // Vuelta a la manzana del Plaza Vea (Giráldez, Real, Ferrocarril, Puno).
        Vector3[] plazaVea = BuildCircuit(new Vector2(P, 58f), new Vector2(R, 58f), new Vector2(R, AvenueZ), new Vector2(P, AvenueZ));
        // Vuelta a la Plaza Constitución (Real, Loreto, Cusco, Junín).
        Vector3[] constitucion = BuildCircuit(new Vector2(R, -18f), new Vector2(C, -18f), new Vector2(C, -45f), new Vector2(R, -45f));
        // La "feria": toda la Calle Real hacia el norte, de circunvalación a circunvalación.
        Vector3[] feria = BuildCircuit(new Vector2(R, NR), new Vector2(C, NR), new Vector2(C, SR), new Vector2(R, SR));
        // La Real hacia el sur (lado oeste del pueblo).
        Vector3[] realSur = BuildCircuit(new Vector2(P, NR), new Vector2(R, NR), new Vector2(R, SR), new Vector2(P, SR));
        // Jr. Arequipa y Jr. Puno.
        Vector3[] arequipa = BuildCircuit(new Vector2(A, NR), new Vector2(P, NR), new Vector2(P, SR), new Vector2(A, SR));
        // Norte: Ferrocarril, Cusco, Amazonas.
        Vector3[] norte = BuildCircuit(new Vector2(R, 86f), new Vector2(C, 86f), new Vector2(C, 58f), new Vector2(R, 58f));
        // Bus y combis: cruzan el puente hasta la chacra y vuelven.
        Vector3[] giraldez = BuildCircuit(new Vector2(A, 58f), new Vector2(P, 58f), new Vector2(P, AvenueZ),
            new Vector2(GiraldezEastEndX - 1.5f, AvenueZ), new Vector2(A, AvenueZ));
        // Mirador: suben por la Real, cruzan arriba y bajan por el Cusco / el Puno.
        Vector3[] miradorEste = BuildCircuit(new Vector2(R, SR), new Vector2(C, SR), new Vector2(C, T), new Vector2(R, T));
        Vector3[] miradorOeste = BuildCircuit(new Vector2(P, SR), new Vector2(R, SR), new Vector2(R, T), new Vector2(P, T));

        int count = 0;
        void Add(string name, VehicleKind kind, Vector3[] circuit, float speed, float progress)
        {
            GameObject v = BuildAssetVehicle(root.transform, name, kind, circuit[0], circuit[1], speed, null, true);
            if (v == null) return;
            CarPatrol patrol = v.GetComponent<CarPatrol>();
            SerializedObject so = new SerializedObject(patrol);
            so.FindProperty("oneWay").boolValue = false;
            so.FindProperty("startProgress").floatValue = progress;
            SerializedProperty cp = so.FindProperty("circuit");
            cp.arraySize = circuit.Length;
            for (int i = 0; i < circuit.Length; i++) cp.GetArrayElementAtIndex(i).vector3Value = circuit[i];

            // Sin reaparecer ya no cambia de modelo: se elige uno de entrada
            // según lo que es (el bus sale de bus, la combi de van).
            SerializedProperty vp = so.FindProperty("variants");
            SerializedProperty hp = so.FindProperty("variantHalfLengths");
            int pick = 0;
            for (int i = 0; i < vp.arraySize; i++)
            {
                float h = hp.GetArrayElementAtIndex(i).floatValue;
                bool fits = kind == VehicleKind.Bus ? h > 3.2f : kind == VehicleKind.Combi ? h > 2.15f && h <= 3.2f : false;
                if (fits) { pick = i; break; }
            }
            if (kind == VehicleKind.Car || kind == VehicleKind.Police)
            {
                // Autos distintos entre sí: el n-ésimo modelo de auto de la lista.
                int wanted = count % Mathf.Max(1, CountCars(hp)), seen = 0;
                for (int i = 0; i < hp.arraySize; i++)
                {
                    if (hp.GetArrayElementAtIndex(i).floatValue > 2.15f) continue;
                    if (seen++ == wanted) { pick = i; break; }
                }
            }
            for (int i = 0; i < vp.arraySize; i++)
            {
                GameObject m = vp.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (m != null) m.SetActive(i == pick);
            }
            if (pick < hp.arraySize) so.FindProperty("vehicleHalfLength").floatValue = hp.GetArrayElementAtIndex(pick).floatValue;
            so.ApplyModifiedProperties();
            count++;
        }

        Add("Auto_PlazaVea_1", VehicleKind.Car, plazaVea, 8f, 0.05f);
        Add("Auto_PlazaVea_2", VehicleKind.Car, plazaVea, 8f, 0.55f);
        Add("Auto_Constitucion_1", VehicleKind.Car, constitucion, 8f, 0.1f);
        Add("Auto_Constitucion_2", VehicleKind.Car, constitucion, 8f, 0.6f);
        Add("Auto_Feria_1", VehicleKind.Car, feria, 10f, 0.02f);
        Add("Auto_Feria_2", VehicleKind.Car, feria, 9.5f, 0.27f);
        Add("Combi_Feria", VehicleKind.Combi, feria, 8.5f, 0.52f);
        Add("Auto_Feria_3", VehicleKind.Car, feria, 10f, 0.77f);
        Add("Auto_RealSur_1", VehicleKind.Car, realSur, 9.5f, 0.15f);
        Add("Patrullero", VehicleKind.Police, realSur, 8f, 0.48f);
        Add("Auto_RealSur_2", VehicleKind.Car, realSur, 9.5f, 0.8f);
        Add("Auto_Arequipa", VehicleKind.Car, arequipa, 8.5f, 0.2f);
        Add("Combi_Arequipa", VehicleKind.Combi, arequipa, 8f, 0.7f);
        Add("Auto_Norte", VehicleKind.Car, norte, 8f, 0.4f);
        Add("Bus_Giraldez", VehicleKind.Bus, giraldez, 7f, 0.05f);
        Add("Combi_Giraldez", VehicleKind.Combi, giraldez, 8.5f, 0.4f);
        Add("Auto_Giraldez", VehicleKind.Car, giraldez, 9.5f, 0.72f);
        Add("Auto_Mirador_Este", VehicleKind.Car, miradorEste, 8f, 0.3f);
        Add("Auto_Mirador_Oeste", VehicleKind.Car, miradorOeste, 8f, 0.75f);
    }

    private static int CountCars(SerializedProperty halfLengths)
    {
        int n = 0;
        for (int i = 0; i < halfLengths.arraySize; i++) if (halfLengths.GetArrayElementAtIndex(i).floatValue <= 2.15f) n++;
        return n;
    }
}
