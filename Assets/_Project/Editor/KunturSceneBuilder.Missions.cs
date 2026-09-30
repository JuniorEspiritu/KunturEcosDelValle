using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Misiones por nivel, orillas del río, la Bodega Andina y la ruta morada del
// mapa. Va en su propio archivo para no seguir engordando el constructor
// principal; es la misma clase (partial).
public static partial class KunturSceneBuilder
{
    // ---------------------------------------------------------------
    // Vecinos que pueden dar la misión de un nivel
    // ---------------------------------------------------------------

    private static readonly List<DialogueNPC> MissionGivers = new List<DialogueNPC>();
    private static readonly List<string> MissionGiverNames = new List<string>();

    private static readonly List<bool> MissionGiverFemale = new List<bool>();
    private static readonly List<string> MissionGiverRoles = new List<string>();
    // v55: zona propia de algunos vecinos (el chacarero pide limpiar la
    // chacra; la heladera del mirador, el mirador). Vacío = zona al azar.
    private static readonly List<string> MissionGiverZones = new List<string>();
    // v57: cómo habla cada uno ("mother", "father", "young", "teacher",
    // "leader"). Va escrito acá y no adivinado por el nombre: con gente del
    // valle de verdad (Brayan, Julius, Milagros) el nombre ya no lo dice.
    private static readonly List<string> MissionGiverVoices = new List<string>();

    // Quién es cada vecino que puede pedirte ayuda, con el cuerpo que le toca.
    // v57: gente del valle del Mantaro, con nombres y oficios de Huancayo.
    private static readonly (string name, bool female, string prefab, string role, string voice)[] Givers =
    {
        ("Doña Nélida", true, "elder/elder_Female_A.prefab", "VECINA · VENDE EN EL MERCADO MODELO", "mother"),
        ("Gilmer Gonzales", false, "city/casual_Male_G.prefab", "VECINO · AGRICULTOR DE SAÑO", "father"),
        ("Profesora Yeni", true, "downtown/casual_Female_K.prefab", "PROFESORA DEL SANTA ISABEL", "teacher"),
        ("Brayan", false, "downtown/casual_Male_K.prefab", "ESTUDIANTE DE LA UNCP", "young"),
        ("Señora Flor", true, "city/casual_Female_G.prefab", "VECINA · TEJEDORA DE HUALHUAS", "mother"),
        ("Alejandro", false, "worker_Male_constructor_B.prefab", "VECINO · MAESTRO DE OBRA", "father"),
    };

    private static int interactableLayerForGivers
    {
        get
        {
            int layer = LayerMask.NameToLayer(InteractableLayerName);
            return layer == -1 ? 8 : layer;
        }
    }

    // Las muestras de agua van en la orilla oeste, en el talud, apenas por
    // encima del filo del agua (medio metro más allá de WaterEdge).
    private const float RiverSampleX = RiverCenterX - (WaterEdge + 0.4f);

    private class MissionZoneInfo
    {
        public string id;
        public string displayName;
        public GameObject root;
        public bool hasSamples;
    }

    private static readonly List<MissionZoneInfo> MissionZoneList = new List<MissionZoneInfo>();

    // El vecino queda listo para hablar, pero con el collider apagado:
    // MissionDirector lo prende solo cuando le toca dar la misión del nivel.
    private static void MakeMissionGiver(GameObject villager, string name, bool female, string role, int layer, string preferredZone = "", string voice = "")
    {
        if (villager == null) return;

        CapsuleCollider col = villager.GetComponent<CapsuleCollider>();
        if (col == null) col = villager.AddComponent<CapsuleCollider>();
        // Los vecinos del asset tienen el pivote en los pies; los de cápsula,
        // en el centro.
        bool pivotAtFeet = villager.GetComponent<MeshFilter>() == null;
        col.center = pivotAtFeet ? new Vector3(0f, 1f, 0f) : Vector3.zero;
        col.height = 2f;
        col.radius = 0.45f;
        col.enabled = false;
        villager.layer = layer;

        DialogueNPC npc = villager.AddComponent<DialogueNPC>();
        SerializedObject so = new SerializedObject(npc);
        so.FindProperty("npcName").stringValue = name;
        so.FindProperty("npcRole").stringValue = role;
        so.FindProperty("openingLine").stringValue = "¡Kuntur! Te estaba buscando.";
        so.FindProperty("objectiveId").stringValue = "hablar_mision";
        SerializedProperty options = so.FindProperty("options");
        options.arraySize = 3;
        SetDialogueOption(options.GetArrayElementAtIndex(0), "¡Claro que sí!", true, "¡Gracias!");
        SetDialogueOption(options.GetArrayElementAtIndex(1), "Ahora no.", false, "Piénsalo...");
        SetDialogueOption(options.GetArrayElementAtIndex(2), "No me importa.", false, "El valle es de todos.");
        so.ApplyModifiedProperties();

        MissionGivers.Add(npc);
        MissionGiverNames.Add(name);
        MissionGiverFemale.Add(female);
        MissionGiverRoles.Add(role);
        MissionGiverZones.Add(preferredZone ?? "");
        MissionGiverVoices.Add(string.IsNullOrEmpty(voice) ? (female ? "mother" : "father") : voice);
    }

    // Punto de color que solo ve la cámara del mapa (capa "SoloMapa"): así la
    // basura y las muestras aparecen en el minimapa y en el mapa grande.
    private static bool mapLayerReady;
    private static readonly Dictionary<string, Material> MapIconMaterials = new Dictionary<string, Material>();

    private static void AddMapIcon(Transform owner, Vector3 groundPosition, Color color, float size)
    {
        if (!mapLayerReady) { EnsureLayer("SoloMapa"); mapLayerReady = true; }
        int layer = LayerMask.NameToLayer("SoloMapa");
        if (layer < 0) return;

        string key = ColorUtility.ToHtmlStringRGB(color);
        if (!MapIconMaterials.TryGetValue(key, out Material mat) || mat == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            CreateFolderRecursive(ArtDir + "/Materiales");
            string path = ArtDir + $"/Materiales/Icono_Mapa_{key}.mat";
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            MapIconMaterials[key] = mat;
        }

        GameObject icon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        icon.name = "Icono_Mapa";
        Object.DestroyImmediate(icon.GetComponent<Collider>());
        icon.layer = layer;
        icon.transform.SetParent(owner, true);
        icon.transform.position = new Vector3(groundPosition.x, 46f, groundPosition.z);
        Vector3 parentScale = owner.lossyScale;
        icon.transform.localScale = new Vector3(size / parentScale.x, 0.02f, size / parentScale.z);
        MeshRenderer renderer = icon.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        icon.isStatic = false;
    }

    // Altura del terreno en (x, z): el mismo perfil del cauce que arma
    // BuildValleyGround, para posar cosas sobre la orilla sin que floten.
    private static float TerrainHeightAt(float x, float z)
    {
        float d = Mathf.Abs(x - RiverX(z));
        return RiverProfile(d) * RiverDepthFactor(z) + HillHeightAt(x, z);
    }

    // ---------------------------------------------------------------
    // Zonas de limpieza (cada una aparece recién cuando te la encargan)
    // ---------------------------------------------------------------
    // Un montón de basura en un lugar exacto: centro, si se alarga a lo largo
    // de X o de Z (una vereda), cuánto mide y cómo se llama el lugar.
    private struct SpotDef
    {
        public float x, z, halfLen, halfWid;
        public bool alongX;
        public string place;
        public SpotDef(float x, float z, bool alongX, float halfLen, float halfWid, string place)
        { this.x = x; this.z = z; this.alongX = alongX; this.halfLen = halfLen; this.halfWid = halfWid; this.place = place; }
    }

    private const float SpotSidewalk = CrossStreetHalfRoad + SidewalkWidth / 2f; // centro de la vereda de un jirón
    // v53: en cada montón se recogen BOLSAS de basura, no basuritas sueltas.
    // Con la animación de agacharse de Mixamo, recoger 20 botellitas era
    // agacharse 20 veces seguidas y se veía como un bug; una bolsa por gesto
    // se entiende al toque. 8 por montón alcanzan para las misiones largas.
    private const int PickupsPerSpot = 8;

    // Montones de la orilla del río (las piedras de la orilla los esquivan).
    private static readonly SpotDef[] RiverSpots =
    {
        new SpotDef(RiverCenterX - (WaterEdge + 1.2f), -30f, false, 5f, 0.5f, "en la orilla del río Mantaro, al sur del puente"),
        new SpotDef(RiverCenterX - (WaterEdge + 1.2f), 50f, false, 5f, 0.5f, "en la orilla del río Mantaro, al norte del puente"),
        new SpotDef(RiverCenterX + (WaterEdge + 1.2f), 8f, false, 5f, 0.5f, "en la orilla del río, del lado de la chacra"),
    };

    private static void BuildMissionZones(Transform mechanics, int layer)
    {
        int index = 0;
        System.Random rng = new System.Random(20260922);

        MissionZoneInfo NewZone(string id, string display, string rootName, Vector3 center, bool samples)
        {
            GameObject root = new GameObject(rootName);
            root.transform.SetParent(mechanics);
            root.transform.position = center;
            var info = new MissionZoneInfo { id = id, displayName = display, root = root, hasSamples = samples };
            MissionZoneList.Add(info);
            return info;
        }

        void Spots(MissionZoneInfo zone, System.Func<float, float, float> groundY, params SpotDef[] defs)
        {
            for (int k = 0; k < defs.Length; k++)
                BuildCleanupSpot(zone, k, defs[k], groundY, layer, rng, ref index);
        }

        float Flat(float x, float z) => 0f;
        float Deck(float x, float z) => 0.1f;

        MissionZoneInfo plaza = NewZone("plaza_vea", "afuera del Plaza Vea", "Zona_PlazaVea", new Vector3(-22f, 0f, 39f), false);
        Spots(plaza, Flat,
            new SpotDef(-27f, 40.5f, true, 4.5f, 1.8f, "en el estacionamiento del Plaza Vea"),
            new SpotDef(-13.5f, 40.5f, true, 3f, 1.6f, "en la entrada del Plaza Vea"),
            new SpotDef(-21f, AvenueZ + 5.4f, true, 5f, 0.85f, "en la vereda del Plaza Vea, frente a la Av. Giráldez"));

        MissionZoneInfo park = NewZone("parque", "en la Plaza Constitución", "Zona_Parque", ParkCenter, false);
        Vector3 pc = ParkCenter;
        Spots(park, (x, z) => 0.1f,
            new SpotDef(pc.x - 6.2f, pc.z + 4.2f, true, 2.4f, 1.5f, "en el pasto de la Plaza Constitución, junto a la pileta"),
            new SpotDef(pc.x + 6.2f, pc.z - 4.4f, true, 2.4f, 1.5f, "en la esquina sur de la Plaza Constitución"),
            new SpotDef(pc.x + 6.4f, pc.z + 4.4f, true, 2.4f, 1.5f, "al ladito de las bancas de la Plaza Constitución"));

        MissionZoneInfo chacra = NewZone("camino_chacra", "en el camino a la chacra", "Zona_CaminoChacra", new Vector3(EastRoadX, 0f, 10f), false);
        Spots(chacra, Flat,
            new SpotDef(84.5f, 42f, true, 2.6f, 2.6f, "en el descampado, apenas cruzas el puente"),
            new SpotDef(EastRoadX - 4.7f, -30f, false, 5f, 1f, "al borde del camino a la chacra, por el sur"),
            new SpotDef(EastRoadX + 4.7f, 55f, false, 5f, 1f, "al borde del camino a la chacra, por el norte"));

        MissionZoneInfo rio = NewZone("rio", "en la orilla del río Mantaro", "Zona_Rio", new Vector3(RiverCenterX, 0f, 10f), true);
        Spots(rio, TerrainHeightAt, RiverSpots);
        int sample = 0;
        foreach (float z in new[] { -40f, -10f, 20f, 45f })
            BuildWaterSample(rio.root.transform, sample++, new Vector3(RiverSampleX, TerrainHeightAt(RiverSampleX, z), z), layer);

        MissionZoneInfo real = NewZone("calle_real", "en la Calle Real", "Zona_CalleReal", new Vector3(0f, 0f, -10f), false);
        Spots(real, Flat,
            new SpotDef(5.4f, -58.5f, false, 4.5f, 0.85f, "en la Calle Real, entre el Jr. Ancash y el Jr. Junín"),
            new SpotDef(-5.4f, -2f, false, 4.5f, 0.85f, "en la Calle Real, entre el Jr. Loreto y el Jr. Ayacucho"),
            new SpotDef(5.4f, 72f, false, 4.5f, 0.85f, "en la Calle Real, entre la Av. Ferrocarril y el Jr. Amazonas"));

        MissionZoneInfo arequipa = NewZone("arequipa", "en el Jr. Arequipa", "Zona_Arequipa", new Vector3(ArequipaX, 0f, -10f), false);
        Spots(arequipa, Flat,
            new SpotDef(ArequipaX + SpotSidewalk, -31.5f, false, 4.5f, 0.85f, "en el Jr. Arequipa, entre el Jr. Junín y el Jr. Loreto"),
            new SpotDef(ArequipaX - SpotSidewalk, 44f, false, 4.5f, 0.85f, "en el Jr. Arequipa, entre la Av. Giráldez y la Av. Ferrocarril"),
            new SpotDef(ArequipaX + SpotSidewalk, -86f, false, 4.5f, 0.85f, "en el Jr. Arequipa, entre el Jr. Huánuco y el Jr. Ancash"));

        MissionZoneInfo puente = NewZone("puente", "en el puente de la Giráldez", "Zona_Puente", new Vector3(RiverCenterX, 0f, AvenueZ), false);
        Spots(puente, (x, z) => x > BridgeStartX + 1.5f && x < BridgeEndX - 1.5f ? 0.1f : 0f,
            new SpotDef(RiverCenterX, AvenueZ + 3.7f, true, 12f, 0.35f, "en el mismo puente de la Av. Giráldez"),
            new SpotDef(44.5f, AvenueZ - 5.4f, true, 3f, 0.85f, "a la entrada del puente, del lado del pueblo"),
            new SpotDef(84.5f, AvenueZ + 5.4f, true, 2.6f, 0.85f, "a la salida del puente, del lado de la chacra"));

        MissionZoneInfo amazonas = NewZone("amazonas", "en el Jr. Amazonas", "Zona_Amazonas", new Vector3(-20f, 0f, 86f), false);
        Spots(amazonas, Flat,
            new SpotDef(-61f, 86f + SpotSidewalk, true, 5f, 0.85f, "en el Jr. Amazonas, entre el Jr. Arequipa y el Jr. Puno"),
            new SpotDef(-21f, 86f - SpotSidewalk, true, 5f, 0.85f, "en el Jr. Amazonas, entre el Jr. Puno y la Calle Real"),
            new SpotDef(17f, 86f + SpotSidewalk, true, 5f, 0.85f, "en el Jr. Amazonas, entre la Calle Real y el Jr. Cusco"));

        // v56: la bodega da al Jr. Cusco (su puerta mira al oeste).
        MissionZoneInfo bodega = NewZone("bodega", "por la bodega de Doña Rosa", "Zona_Bodega", new Vector3(39f, 0f, 4f), false);
        Spots(bodega, Flat,
            new SpotDef(CuscoX + SpotSidewalk, -4.5f, false, 2.8f, 0.85f, "al costado de la bodega de Doña Rosa, en el Jr. Cusco"),
            new SpotDef(CuscoX + SpotSidewalk, 6.4f, false, 2.2f, 0.85f, "en la misma puerta de la bodega de Doña Rosa"),
            new SpotDef(CuscoX + SpotSidewalk, 22.5f, false, 2.8f, 0.85f, "en la esquina del Jr. Cusco con el Jr. Ayacucho"));

        // v55: la subida al mirador (la calle en bajada del sur). La gente sube
        // a ver el valle, se compra un helado y deja la basura ahí nomás.
        float hillWalk = MainStreetHalf + SidewalkWidth / 2f;
        MissionZoneInfo mirador = NewZone("mirador", "en la subida al mirador", "Zona_Mirador",
            new Vector3(0f, HillHeightAt(0f, -170f), -170f), false);
        Spots(mirador, (x, z) => HillHeightAt(x, z) + 0.12f,
            new SpotDef(hillWalk, -160f, false, 4.5f, 0.85f, "en la vereda de la subida al mirador"),
            new SpotDef(-hillWalk, -182f, false, 4.5f, 0.85f, "a media subida del mirador, frente a las casas del cerro"),
            new SpotDef(9f, TopRoadZ - CrossStreetHalfRoad - SidewalkWidth / 2f, true, 4f, 0.85f, "arriba en el mirador, junto a las bancas"));
    }

    // ---------------------------------------------------------------
    // Basura de verdad (asset WasteOvergrowth) en montones
    // ---------------------------------------------------------------
    private const string WasteDir = "Assets/URP_WasteOvergrowth_SA/Prefabs";

    private static readonly (string path, int kind, string name, float size, int weight)[] WasteItems =
    {
        ("Bottles/Mesh_WatterBottle_L", 0, "botella", 0.46f, 3),
        ("Bottles/Prefab_BeerBottle", 0, "botella", 0.42f, 2),
        ("Bottles/Prefab_SodaBottle_L", 0, "botella de gaseosa", 0.5f, 3),
        ("Bottles/Prefab_WaterBottle_S", 0, "botellita", 0.36f, 2),
        ("Bottles/Prefab_CoffeeCup", 0, "vaso", 0.3f, 2),
        ("Bottles/Prefab_SodaCup", 0, "vaso", 0.34f, 2),
        ("Bottles/Crushed/Prefab_SodaBottle_L_Crushed", 0, "botella aplastada", 0.46f, 3),
        ("Bottles/Crushed/Prefab_WaterBottle_L_Crushed", 0, "botella aplastada", 0.42f, 2),
        ("Bottles/Crushed/Prefab_CoffeeCup_Crushed", 0, "vaso aplastado", 0.3f, 2),
        ("Bottles/Prefab_SodaCan_1", 1, "lata", 0.28f, 3),
        ("Bottles/Prefab_SodaCan_2", 1, "lata", 0.28f, 3),
        ("Bottles/Crushed/Prefab_SodaCan_Crushed1", 1, "lata aplastada", 0.28f, 3),
        ("Bottles/Crushed/Prefab_SodaCan_Crushed2", 1, "lata aplastada", 0.28f, 3),
        ("Paper/Prefab_NewsPaper_flat", 2, "periódico", 0.5f, 2),
        ("Paper/Prefab_Newspaper_folded", 2, "periódico", 0.45f, 2),
        ("Paper/Prefab_Paper_flat", 2, "papel", 0.4f, 2),
        ("Paper/Prefab_Paper_folded", 2, "papel", 0.36f, 2),
        ("Paper/Grouped/Prefab_PaperGroup01", 2, "papeles", 0.55f, 2),
        ("FooDContainer/Prefab_BurgerBox", 2, "caja de hamburguesa", 0.4f, 2),
        ("FooDContainer/Prefab_BurgerBox_Cracked", 2, "caja de hamburguesa", 0.4f, 1),
        ("FooDContainer/Prefab_Pizzabox", 2, "caja de pizza", 0.6f, 1),
        ("FooDContainer/Prefab_SaiceCup", 2, "tapercito", 0.3f, 2),
        ("Trashbag/Prefab_TrashBag", 2, "bolsa de basura", 0.8f, 1),
        ("Trashbag/Prefab_Trashbag_2", 2, "bolsa de basura", 0.75f, 1),
        ("Trashbag/Prefab_Bag_G", 2, "bolsa", 0.7f, 1),
        ("Trashbag/Prefab_Trashbag_Spilled", 2, "bolsa rota", 0.9f, 1),
    };

    private static readonly string[] WastePiles =
    {
        "TrashPile/Prefab_Pile1", "TrashPile/Prefab_Pile2", "TrashPile/Prefab_Pile3",
        "TrashGroup/Prefab_TrashGroup_1", "TrashGroup/Prefab_TrashGroup_3", "TrashGroup/Prefab_TrashGroup_5",
        "TrashGroup/Prefab_TrashGroup_7", "TrashGroup/Prefab_TrashGroup_9", "TrashGroup/Prefab_TrashGroup_11",
        "TrashGroup/Prefab_TrashGroup_13",
    };

    private static bool WasteAvailable => AssetDatabase.LoadAssetAtPath<GameObject>(WasteDir + "/Bottles/Prefab_SodaCan_1.prefab") != null;

    private static void BuildCleanupSpot(MissionZoneInfo zone, int k, SpotDef def, System.Func<float, float, float> groundY,
        int layer, System.Random rng, ref int index)
    {
        GameObject spotGO = new GameObject($"Punto_{k + 1}");
        spotGO.transform.SetParent(zone.root.transform);
        spotGO.transform.position = new Vector3(def.x, groundY(def.x, def.z), def.z);
        CleanupSpot spot = spotGO.AddComponent<CleanupSpot>();
        SerializedObject so = new SerializedObject(spot);
        so.FindProperty("placeName").stringValue = def.place;
        so.ApplyModifiedProperties();

        bool waste = WasteAvailable;
        float pileWidth = Mathf.Min(2f, def.halfWid * 2.2f + 0.4f);

        // El montón de fondo (decorado, no se recoge): se va cuando termina la misión.
        Vector3 center = spotGO.transform.position;
        if (waste)
        {
            GameObject pilePrefab = LoadPrefab($"{WasteDir}/{WastePiles[rng.Next(WastePiles.Length)]}.prefab");
            if (pilePrefab != null)
            {
                GameObject pile = PlaceModel(pilePrefab, spotGO.transform, "Monton", center, Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f), 0f, out Vector3 size);
                float maxXZ = Mathf.Max(size.x, size.z, 0.01f);
                pile.transform.localScale *= pileWidth / maxXZ;
                RemoveColliders(pile);
            }
        }
        else
        {
            Mesh ico = LowPolyMeshFactory.Icosphere(1);
            for (int b = 0; b < 4; b++)
            {
                float sz = NextFloat(rng, 0.5f, 0.8f);
                MeshObject(spotGO.transform, "Bolsa_Monton", ico,
                    center + new Vector3(NextFloat(rng, -0.5f, 0.5f), sz * 0.3f, NextFloat(rng, -0.4f, 0.4f)),
                    new Vector3(sz, sz * 0.7f, sz * 0.85f), b % 2 == 0 ? HexColor("#2f3338") : HexColor("#4a4640"));
            }
        }

        // Los residuos que sí se recogen, regados alrededor del montón.
        for (int i = 0; i < PickupsPerSpot; i++)
        {
            Vector3 p = center;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float a = NextFloat(rng, -def.halfLen, def.halfLen);
                float b = NextFloat(rng, -def.halfWid, def.halfWid);
                float x = def.alongX ? def.x + a : def.x + b;
                float z = def.alongX ? def.z + b : def.z + a;
                bool nearPile = Mathf.Abs(x - def.x) < pileWidth * 0.45f && Mathf.Abs(z - def.z) < pileWidth * 0.45f;
                if (nearPile && attempt < 7) continue;
                p = new Vector3(x, groundY(x, z), z);
                if (!OverlapsPlaced(p, 0.45f) || attempt == 7) break;
            }

            if (waste) BuildBagPickup(spotGO.transform, index++, p, layer, zone.id, rng);
            else BuildTrashItem(spotGO.transform, index++, p, layer, zone.id);
        }
    }

    // Bolsas del asset WasteOvergrowth. Cada una lleva un tipo de residuo
    // adentro (botellas, latas o papeles) para que el inventario siga
    // mostrando lo que se clasifica.
    private static readonly (string path, float size)[] BagItems =
    {
        ("Trashbag/Prefab_TrashBag", 0.85f),
        ("Trashbag/Prefab_Trashbag_2", 0.8f),
        ("Trashbag/Prefab_Bag_G", 0.75f),
        ("Trashbag/Prefab_Trashbag_Spilled", 0.95f),
    };

    private static readonly string[] BagNames = { "bolsa con botellas", "bolsa con latas", "bolsa de papeles" };

    // v53: cartel de misión cumplida mientras Kuntur baila. Va abajo al
    // centro (la parte de arriba queda libre para ver el baile) y tiene el
    // botón CONTINUAR, que es lo que corta el baile y trae la siguiente misión.
    private static VictoryDanceUI BuildVictoryDancePanel(Transform canvasRoot, Sprite starFilled, Sprite starEmpty)
    {
        GameObject host = new GameObject("BaileVictoria", typeof(RectTransform));
        host.transform.SetParent(canvasRoot, false);
        StretchFull((RectTransform)host.transform);

        Image card = MakeRoundedPanel(host.transform, "Panel_BaileVictoria", UIPalette_PanelDark());
        SetRect(card.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(620f, 170f));

        TextMeshProUGUI title = MakeText(card.transform, "Text_Titulo", "¡MISIÓN CUMPLIDA!", 32f, UIPalette_Gold(),
            TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -12f), new Vector2(-40f, 42f));
        title.enableAutoSizing = true;
        title.fontSizeMin = 20f;
        title.fontSizeMax = 32f;

        Image[] starImages = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            Image star = MakeIcon(card.transform, $"Estrella_{i + 1}", starEmpty, Color.white);
            SetRect(star.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2((i - 1) * 40f, -56f), new Vector2(34f, 34f));
            starImages[i] = star;
        }

        TextMeshProUGUI body = MakeText(card.transform, "Text_Cuerpo", "", 17f, UIPalette_Cream(), TextAlignmentOptions.Center);
        SetRect(body.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -94f), new Vector2(-40f, 26f));
        body.enableAutoSizing = true;
        body.fontSizeMin = 12f;
        body.fontSizeMax = 17f;

        Image button = MakeRoundedPanel(card.transform, "Boton_Continuar", UIPalette_Amber());
        SetRect(button.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 14f), new Vector2(220f, 42f));
        Button continueButton = button.gameObject.AddComponent<Button>();
        continueButton.targetGraphic = button;
        TextMeshProUGUI label = MakeText(button.transform, "Text", "CONTINUAR", 20f, HexColor("#2a1a05"),
            TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(label.rectTransform);
        TextMeshProUGUI hint = MakeText(card.transform, "Text_Ayuda", "Enter / E", 12f, UIPalette_TextMuted(), TextAlignmentOptions.Center);
        SetRect(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f),
            new Vector2(122f, 35f), new Vector2(90f, 18f));

        VictoryDanceUI ui = host.AddComponent<VictoryDanceUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("panelRoot").objectReferenceValue = card.gameObject;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("bodyText").objectReferenceValue = body;
        SerializedProperty starsProp = so.FindProperty("stars");
        starsProp.arraySize = 3;
        for (int i = 0; i < 3; i++) starsProp.GetArrayElementAtIndex(i).objectReferenceValue = starImages[i];
        so.FindProperty("starFilled").objectReferenceValue = starFilled;
        so.FindProperty("starEmpty").objectReferenceValue = starEmpty;
        so.FindProperty("continueButton").objectReferenceValue = continueButton;
        so.ApplyModifiedProperties();

        card.gameObject.SetActive(false);
        return ui;
    }

    private static void BuildBagPickup(Transform parent, int index, Vector3 pos, int layer, string objectiveId, System.Random rng)
    {
        var bag = BagItems[rng.Next(BagItems.Length)];
        GameObject prefab = LoadPrefab($"{WasteDir}/{bag.path}.prefab");
        if (prefab == null) { BuildWastePickup(parent, index, pos, layer, objectiveId, rng); return; }

        int kind = rng.Next(3);
        GameObject trash = new GameObject($"Bolsa_{index + 1}");
        trash.transform.SetParent(parent);
        trash.transform.position = pos;
        trash.layer = layer;

        GameObject model = PlaceModel(prefab, trash.transform, "Modelo", pos, Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f), 0f, out Vector3 size);
        float maxDim = Mathf.Max(size.x, size.y, size.z, 0.01f);
        model.transform.localScale *= bag.size / maxDim;
        RemoveColliders(model);
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        CapsuleCollider col = trash.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.radius = 0.7f;
        col.height = 1.4f;
        col.center = new Vector3(0f, 0.45f, 0f);

        TrashPickup pickup = trash.AddComponent<TrashPickup>();
        SerializedObject pso = new SerializedObject(pickup);
        pso.FindProperty("trashType").enumValueIndex = kind;
        // Una bolsa vale lo que varias basuritas juntas.
        pso.FindProperty("scoreValue").intValue = 25;
        pso.FindProperty("healthContribution").floatValue = 1.5f;
        pso.FindProperty("objectiveId").stringValue = objectiveId;
        pso.FindProperty("itemName").stringValue = BagNames[kind];
        pso.ApplyModifiedProperties();

        CollectibleGlow glow = trash.AddComponent<CollectibleGlow>();
        SerializedObject gso = new SerializedObject(glow);
        gso.FindProperty("glowColor").colorValue = UIPalette_Green();
        gso.FindProperty("rotateSpeed").floatValue = 0f;
        gso.FindProperty("bobAmplitude").floatValue = 0f;
        gso.FindProperty("glowIntensity").floatValue = 0.55f;
        gso.ApplyModifiedProperties();

        AddMapIcon(trash.transform, pos, UIPalette_Green(), 2f);
        SetStaticRecursive(trash, false);
    }

    private static void BuildWastePickup(Transform parent, int index, Vector3 pos, int layer, string objectiveId, System.Random rng)
    {
        int total = 0;
        foreach (var w in WasteItems) total += w.weight;
        int roll = rng.Next(total);
        var item = WasteItems[0];
        foreach (var w in WasteItems) { if (roll < w.weight) { item = w; break; } roll -= w.weight; }

        GameObject prefab = LoadPrefab($"{WasteDir}/{item.path}.prefab");
        if (prefab == null) { BuildTrashItem(parent, index, pos, layer, objectiveId); return; }

        GameObject trash = new GameObject($"Basura_{index + 1}_{item.name}");
        trash.transform.SetParent(parent);
        trash.transform.position = pos;
        trash.layer = layer;

        GameObject model = PlaceModel(prefab, trash.transform, "Modelo", pos, Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f), 0f, out Vector3 size);
        float maxDim = Mathf.Max(size.x, size.y, size.z, 0.01f);
        model.transform.localScale *= item.size / maxDim;
        RemoveColliders(model);
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        // Trigger generoso: se recoge con solo pasar cerca, sin atinarle exacto.
        CapsuleCollider col = trash.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.radius = 0.55f;
        col.height = 1.2f;
        col.center = new Vector3(0f, 0.35f, 0f);

        TrashPickup pickup = trash.AddComponent<TrashPickup>();
        SerializedObject pso = new SerializedObject(pickup);
        pso.FindProperty("trashType").enumValueIndex = item.kind;
        pso.FindProperty("healthContribution").floatValue = 0.6f;
        pso.FindProperty("objectiveId").stringValue = objectiveId;
        pso.FindProperty("itemName").stringValue = item.name;
        pso.ApplyModifiedProperties();

        CollectibleGlow glow = trash.AddComponent<CollectibleGlow>();
        SerializedObject gso = new SerializedObject(glow);
        gso.FindProperty("glowColor").colorValue = UIPalette_Green();
        gso.FindProperty("rotateSpeed").floatValue = 0f;     // basura tirada, no un ítem flotando
        gso.FindProperty("bobAmplitude").floatValue = 0.015f;
        gso.FindProperty("glowIntensity").floatValue = 0.55f;
        gso.ApplyModifiedProperties();

        AddMapIcon(trash.transform, pos, UIPalette_Green(), 1.8f);
        SetStaticRecursive(trash, false);
    }

    // ---------------------------------------------------------------
    // Orillas del río como el dibujo: piedras redondeadas en fila donde el
    // agua toca la pendiente y juncos más arriba, en el pasto.
    // ---------------------------------------------------------------
    private static void BuildRiverEdge(Transform river)
    {
        GameObject root = new GameObject("Orillas_Piedras");
        root.transform.SetParent(river);

        System.Random rng = new System.Random(60606);
        Color[] rockColors =
        {
            HexColor("#8f8b83"), HexColor("#7b776f"), HexColor("#a39d92"), HexColor("#6e6b66"), HexColor("#98928a"),
        };
        Color[] reedColors = { HexColor("#5f8338"), HexColor("#6f9443"), HexColor("#557a31") };

        int count = 0;
        for (float z = -285f; z <= 285f; z += 1.5f)
        {
            if (Mathf.Abs(z - 30f) < 6f) continue; // el puente
            float rx = RiverX(z);

            for (int side = -1; side <= 1; side += 2)
            {
                if (side < 0 && z > -4f && z < 8f) continue; // la rampa para bajar al río

                float zz = z + NextFloat(rng, -0.6f, 0.6f);
                float d = NextFloat(rng, WaterEdge - 0.2f, WaterEdge + 0.8f);
                float x = rx + side * d;
                if (IsNearRiverCollectible(x, zz)) continue;

                float size = NextFloat(rng, 0.55f, 1.15f);
                float y = TerrainHeightAt(x, zz);
                MeshObject(root.transform, $"Piedra_{count++}", LowPolyMeshFactory.Rock(rng.Next(4)),
                    new Vector3(x, y + size * 0.12f, zz),
                    new Vector3(size * NextFloat(rng, 1f, 1.35f), size * NextFloat(rng, 0.6f, 0.85f), size),
                    rockColors[rng.Next(rockColors.Length)],
                    Quaternion.Euler(NextFloat(rng, -8f, 8f), NextFloat(rng, 0f, 360f), NextFloat(rng, -8f, 8f)));

                // Una piedra chica de vez en cuando, metida en el agua.
                if (rng.Next(4) == 0)
                {
                    float sx = rx + side * NextFloat(rng, WaterEdge - 0.9f, WaterEdge - 0.4f);
                    float small = NextFloat(rng, 0.3f, 0.55f);
                    MeshObject(root.transform, $"Piedra_{count++}", LowPolyMeshFactory.Rock(rng.Next(4)),
                        new Vector3(sx, RiverWaterY - small * 0.15f, zz + NextFloat(rng, -0.8f, 0.8f)),
                        new Vector3(small * 1.2f, small * 0.7f, small), rockColors[rng.Next(rockColors.Length)],
                        Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f));
                }

                // Juncos arriba, en el pasto.
                if (count % 5 == 0)
                {
                    float jd = NextFloat(rng, RiverBankHalf + 0.4f, RiverBankHalf + 3f);
                    float jx = rx + side * jd;
                    Vector3 jp = new Vector3(jx, 0f, zz);
                    if (side < 0 && jx < RiverBankWestX - 2f && IsOnStreetOrRiver(jp)) continue;
                    if (Mathf.Abs(zz - 30f) < 8f) continue;
                    BuildGrassTuft(root.transform, new Vector3(jx, TerrainHeightAt(jx, zz), zz), rng,
                        reedColors[rng.Next(reedColors.Length)], NextFloat(rng, 0.9f, 1.3f));
                }
            }
        }

        foreach (Collider c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
    }

    // ---------------------------------------------------------------
    // Agua estilizada: celeste en las orillas, azul al centro, espuma blanca
    // ---------------------------------------------------------------
    private static Material GetStylizedWaterMaterial()
    {
        Shader shader = Shader.Find("Kuntur/StylizedWater");
        if (shader == null || !shader.isSupported) return null;

        string dir = ArtDir + "/Materiales";
        CreateFolderRecursive(dir);
        string path = dir + "/Rio_Estilizado.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_Shallow", HexColor("#6fd0ee"));
        mat.SetColor("_Deep", HexColor("#1f7fc9"));
        mat.SetColor("_Foam", Color.white);
        mat.SetColor("_Sky", HexColor("#cfeaff"));
        mat.SetFloat("_FlowSpeed", 0.35f);
        mat.SetFloat("_FoamWidth", 0.14f);
        mat.SetFloat("_Streaks", 0.3f);
        Texture2D noise = GetWaterNoiseTexture();
        if (noise != null) mat.SetTexture("_Noise", noise);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Ruido suave que se repite sin costuras (valor en una grilla periódica),
    // para los brillos que corren río abajo.
    private static Texture2D GetWaterNoiseTexture()
    {
        string dir = ArtDir + "/Texturas";
        CreateFolderRecursive(dir);
        string path = dir + "/Ruido_Agua.png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int size = 256;
        System.Random rng = new System.Random(4242);
        float[][] lattices = new float[4][];
        int[] periods = { 4, 8, 16, 32 };
        for (int o = 0; o < 4; o++)
        {
            lattices[o] = new float[periods[o] * periods[o]];
            for (int i = 0; i < lattices[o].Length; i++) lattices[o][i] = (float)rng.NextDouble();
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = 0f, amp = 0.5f, total = 0f;
                for (int o = 0; o < 4; o++)
                {
                    int p = periods[o];
                    float fx = x / (float)size * p, fy = y / (float)size * p;
                    int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                    float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
                    float L(int a, int b) => lattices[o][((b % p + p) % p) * p + ((a % p + p) % p)];
                    float top = Mathf.Lerp(L(x0, y0), L(x0 + 1, y0), tx);
                    float bot = Mathf.Lerp(L(x0, y0 + 1), L(x0 + 1, y0 + 1), tx);
                    v += Mathf.Lerp(top, bot, ty) * amp;
                    total += amp;
                    amp *= 0.5f;
                }
                v /= total;
                tex.SetPixel(x, y, new Color(v, v, v));
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------------------------------------------------------
    // Bodega Andina de Doña Rosa, como la foto: paredes azules, marcos y
    // toldos amarillos, friso rojo y techo de tejas a dos aguas.
    // ---------------------------------------------------------------
    private static void BuildBodegaAndina(Transform parent)
    {
        Vector3 c = new Vector3(44.4f, 0f, 3.9f);
        const float sx = 8f, sz = 7f, h = 3.5f;
        float front = c.z + sz / 2f; // la fachada mira al Jr. de enfrente (+Z)

        GameObject bodega = new GameObject("Bodega_Andina");
        bodega.transform.SetParent(parent);
        bodega.transform.position = c;

        Color blue = HexColor("#2f6db5");
        Color yellow = HexColor("#f2c230");
        Color red = HexColor("#c0392b");
        Color wood = HexColor("#5a3a24");
        Color tile = HexColor("#b5462f");
        Color tileDark = HexColor("#8e3322");
        Transform t = bodega.transform;

        // Muros y zócalo rojo.
        PrimitiveObject(t, "Muros", PrimitiveType.Cube, c + new Vector3(0f, h / 2f, 0f), new Vector3(sx, h, sz), blue);
        PrimitiveObject(t, "Zocalo", PrimitiveType.Cube, c + new Vector3(0f, 0.22f, 0f), new Vector3(sx + 0.12f, 0.44f, sz + 0.12f), red);
        // Friso rojo arriba.
        PrimitiveObject(t, "Friso", PrimitiveType.Cube, c + new Vector3(0f, h - 0.18f, 0f), new Vector3(sx + 0.2f, 0.36f, sz + 0.2f), red);
        // Pilastras amarillas en las esquinas.
        for (int ix = -1; ix <= 1; ix += 2)
            for (int iz = -1; iz <= 1; iz += 2)
                PrimitiveObject(t, "Pilastra", PrimitiveType.Cube,
                    c + new Vector3(ix * (sx / 2f), h / 2f - 0.1f, iz * (sz / 2f)), new Vector3(0.4f, h - 0.2f, 0.4f), yellow);

        // Puerta con marco rojo.
        Vector3 door = new Vector3(c.x, 0f, front);
        PrimitiveObject(t, "Puerta", PrimitiveType.Cube, door + new Vector3(0f, 1.15f, 0.03f), new Vector3(1.3f, 2.3f, 0.08f), wood);
        PrimitiveObject(t, "Marco_Puerta_Izq", PrimitiveType.Cube, door + new Vector3(-0.75f, 1.2f, 0.06f), new Vector3(0.2f, 2.4f, 0.12f), red);
        PrimitiveObject(t, "Marco_Puerta_Der", PrimitiveType.Cube, door + new Vector3(0.75f, 1.2f, 0.06f), new Vector3(0.2f, 2.4f, 0.12f), red);
        PrimitiveObject(t, "Marco_Puerta_Sup", PrimitiveType.Cube, door + new Vector3(0f, 2.4f, 0.06f), new Vector3(1.7f, 0.2f, 0.12f), red);

        // Dos ventanas con marco amarillo, mercadería adentro y toldo inclinado.
        Color[] goods = { HexColor("#e74c3c"), HexColor("#f1c40f"), HexColor("#27ae60"), HexColor("#ecf0f1"), HexColor("#e67e22") };
        int g = 0;
        foreach (float wx in new[] { -2.55f, 2.55f })
        {
            Vector3 w = new Vector3(c.x + wx, 1.55f, front);
            PrimitiveObject(t, "Ventana_Fondo", PrimitiveType.Cube, w + new Vector3(0f, 0f, 0.03f), new Vector3(1.6f, 1.2f, 0.06f), HexColor("#3b2a20"));
            PrimitiveObject(t, "Ventana_Marco_Sup", PrimitiveType.Cube, w + new Vector3(0f, 0.66f, 0.07f), new Vector3(1.9f, 0.14f, 0.12f), yellow);
            PrimitiveObject(t, "Ventana_Marco_Inf", PrimitiveType.Cube, w + new Vector3(0f, -0.66f, 0.1f), new Vector3(1.9f, 0.14f, 0.2f), yellow);
            PrimitiveObject(t, "Ventana_Marco_Izq", PrimitiveType.Cube, w + new Vector3(-0.88f, 0f, 0.07f), new Vector3(0.14f, 1.4f, 0.12f), yellow);
            PrimitiveObject(t, "Ventana_Marco_Der", PrimitiveType.Cube, w + new Vector3(0.88f, 0f, 0.07f), new Vector3(0.14f, 1.4f, 0.12f), yellow);
            PrimitiveObject(t, "Ventana_Cruz", PrimitiveType.Cube, w + new Vector3(0f, 0f, 0.07f), new Vector3(0.08f, 1.2f, 0.08f), yellow);
            // Productos en el escaparate: gaseosas, galletas, papel higiénico.
            for (int k = 0; k < 5; k++)
            {
                float px = -0.6f + k * 0.3f;
                PrimitiveObject(t, "Producto", PrimitiveType.Cube, w + new Vector3(px, -0.42f, 0.09f),
                    new Vector3(0.2f, 0.28f + (k % 2) * 0.08f, 0.06f), goods[g++ % goods.Length]);
                PrimitiveObject(t, "Producto", PrimitiveType.Cube, w + new Vector3(px + 0.1f, 0.1f, 0.09f),
                    new Vector3(0.18f, 0.22f, 0.06f), goods[(g + 2) % goods.Length]);
            }
            // Toldo amarillo con borde rojo.
            PrimitiveObject(t, "Toldo", PrimitiveType.Cube, w + new Vector3(0f, 0.98f, 0.42f), new Vector3(2.1f, 0.06f, 0.95f), yellow,
                Quaternion.Euler(28f, 0f, 0f));
            PrimitiveObject(t, "Toldo_Borde", PrimitiveType.Cube, w + new Vector3(0f, 0.72f, 0.86f), new Vector3(2.1f, 0.14f, 0.04f), red);
        }

        // Letrero de madera sobre la puerta: "BODEGA ANDINA".
        Vector3 board = new Vector3(c.x, 2.92f, front + 0.12f);
        PrimitiveObject(t, "Letrero", PrimitiveType.Cube, board, new Vector3(4.2f, 0.62f, 0.1f), wood);
        PrimitiveObject(t, "Letrero_Borde", PrimitiveType.Cube, board + new Vector3(0f, 0f, -0.02f), new Vector3(4.4f, 0.74f, 0.08f), yellow);
        TextMeshPro label = MakeLogoText(t, "Letrero_Texto", "BODEGA ANDINA", 4.2f, HexColor("#fff2c6"),
            board + new Vector3(0f, 0f, 0.07f), ReadableFrom(Quaternion.identity), new Vector2(0.5f, 0.5f),
            TextAlignmentOptions.Center, 0.7f);
        label.rectTransform.sizeDelta = new Vector2(4f, 0.6f);

        // Techo de tejas a dos aguas (cumbrera a lo largo de X).
        const float slope = 25f;
        float rise = (sz / 2f) * Mathf.Tan(slope * Mathf.Deg2Rad);
        float half = sz / 2f + 0.45f;                    // alero
        float panel = half / Mathf.Cos(slope * Mathf.Deg2Rad);
        float ridgeY = h + rise;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 center = new Vector3(c.x, ridgeY - half * Mathf.Tan(slope * Mathf.Deg2Rad) / 2f + 0.08f, c.z + side * half / 2f);
            PrimitiveObject(t, side > 0 ? "Techo_Frente" : "Techo_Atras", PrimitiveType.Cube, center,
                new Vector3(sx + 0.9f, 0.14f, panel), tile, Quaternion.Euler(side * slope, 0f, 0f));
            // Hileras de tejas: franjas más oscuras paralelas a la cumbrera.
            for (int r = 1; r <= 3; r++)
            {
                float f = r / 4f;
                Vector3 p = new Vector3(c.x, ridgeY - half * f * Mathf.Tan(slope * Mathf.Deg2Rad) + 0.17f, c.z + side * half * f);
                PrimitiveObject(t, "Tejas", PrimitiveType.Cube, p, new Vector3(sx + 0.9f, 0.05f, 0.12f), tileDark,
                    Quaternion.Euler(side * slope, 0f, 0f));
            }
        }
        PrimitiveObject(t, "Cumbrera", PrimitiveType.Cylinder, new Vector3(c.x, ridgeY + 0.16f, c.z),
            new Vector3(0.28f, (sx + 0.9f) / 2f, 0.28f), tileDark, Quaternion.Euler(0f, 0f, 90f));

        // Hastiales: los triángulos azules de los costados bajo el techo.
        Mesh gable = BuildGableMesh(sx, sz, rise);
        GameObject gableGO = MeshObject(t, "Hastiales", gable, new Vector3(c.x, h, c.z), Vector3.one, blue);
        gableGO.isStatic = true;

        // Un solo collider para toda la casa: no se atraviesa la pared.
        BoxCollider box = bodega.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, h / 2f, 0f);
        box.size = new Vector3(sx, h, sz);

        // v56: el jirón de enfrente ya no llega hasta aquí: la bodega se gira
        // para abrir su puerta al Jr. Cusco (mira al oeste).
        bodega.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        SetStaticRecursive(bodega, true);
        Occupy(c, sz + 0.6f, sx + 0.6f);
    }

    private static Mesh BuildGableMesh(float sx, float sz, float rise)
    {
        float hx = sx / 2f, hz = sz / 2f;
        Vector3[] v =
        {
            new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(-hx, rise, 0f),
            new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(hx, rise, 0f),
        };
        // Las dos caras de cada triángulo, cada una con sus propios vértices:
        // si se comparten, las normales se anulan y el hastial sale negro.
        var verts = new List<Vector3>();
        var tris = new List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }
        Tri(v[0], v[1], v[2]); Tri(v[2], v[1], v[0]);
        Tri(v[3], v[5], v[4]); Tri(v[4], v[5], v[3]);
        Mesh mesh = new Mesh { name = "Hastial_Bodega" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------------------------------------------------------
    // MissionDirector + cartel de misión + "!" + ruta morada del mapa
    // ---------------------------------------------------------------
    private static void BuildMissionSystem(Transform canvasRoot, Camera mapCamera, Camera playerCamera,
        GameObject managersGO, ClimaxDirector director)
    {
        if (managersGO == null) return;

        // Cartel de misión (v55): una franja negra con el texto y nada más,
        // debajo del reloj de la misión y lejos de los paneles de los costados.
        // BannerFx la hace entrar y salir.
        Image banner = MakePanel(canvasRoot, "Panel_Mision", new Color(0f, 0f, 0f, 0.72f));
        SetRect(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -100f), new Vector2(560f, 92f));
        CanvasGroup bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;
        bannerGroup.interactable = false;
        banner.raycastTarget = false;
        // Bordes que se esfuman a los lados: sin cajas duras.
        Sprite sideFade = UISpriteFactory.HorizontalFade("UI_DegradeHorizontal", Color.white);
        foreach (int side in new[] { -1, 1 })
        {
            Image wing = MakeIcon(banner.transform, side < 0 ? "Ala_Izq" : "Ala_Der", sideFade, new Color(0f, 0f, 0f, 0.72f));
            SetRect(wing.rectTransform, new Vector2(side < 0 ? 0f : 1f, 0f), new Vector2(side < 0 ? 0f : 1f, 1f),
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(120f, 0f));
            if (side < 0) wing.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }

        TextMeshProUGUI title = MakeText(banner.transform, "Text_TituloMision", "NIVEL 1", 26f, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -10f), new Vector2(-40f, 36f));
        title.enableAutoSizing = true;
        title.fontSizeMin = 16f;
        title.fontSizeMax = 26f;
        title.characterSpacing = 6f;
        title.textWrappingMode = TextWrappingModes.NoWrap;

        Image bannerLine = MakePanel(banner.transform, "Linea_Dorada", UIPalette_Gold());
        SetRect(bannerLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -50f), new Vector2(220f, 2f));
        bannerLine.raycastTarget = false;

        TextMeshProUGUI body = MakeText(banner.transform, "Text_CuerpoMision", "", 16f, new Color(1f, 1f, 1f, 0.88f),
            TextAlignmentOptions.Center);
        SetRect(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 8f), new Vector2(-40f, 34f));
        body.enableAutoSizing = true;
        body.fontSizeMin = 12f;
        body.fontSizeMax = 16f;

        BannerFx bannerFx = banner.gameObject.AddComponent<BannerFx>();
        SerializedObject bannerFxSo = new SerializedObject(bannerFx);
        bannerFxSo.FindProperty("group").objectReferenceValue = bannerGroup;
        bannerFxSo.FindProperty("band").objectReferenceValue = banner.rectTransform;
        bannerFxSo.FindProperty("title").objectReferenceValue = title;
        bannerFxSo.FindProperty("body").objectReferenceValue = body;
        bannerFxSo.FindProperty("line").objectReferenceValue = bannerLine.rectTransform;
        bannerFxSo.ApplyModifiedProperties();

        // Tres estrellitas a los lados del título al cumplir una misión.
        Sprite starFilledSprite = UISpriteFactory.Star("UI_EstrellaLlena", UIPalette_Gold());
        Sprite starEmptySprite = UISpriteFactory.Star("UI_EstrellaVacia", new Color(1f, 1f, 1f, 0.22f));
        Image[] bannerStars = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            Image star = MakeIcon(banner.transform, $"Estrella_{i + 1}", starEmptySprite, Color.white);
            SetRect(star.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 30f, -16f), new Vector2(24f, 24f));
            star.gameObject.SetActive(false);
            bannerStars[i] = star;
        }
        banner.gameObject.SetActive(false);

        // "!" dorado que flota sobre el vecino que te busca.
        GameObject marker = new GameObject("Marcador_Mision");
        TextMeshPro mark = marker.AddComponent<TextMeshPro>();
        mark.text = "!";
        mark.fontSize = 14f;
        mark.fontStyle = FontStyles.Bold;
        mark.color = UIPalette_Gold();
        mark.alignment = TextAlignmentOptions.Center;
        mark.outlineWidth = 0.25f;
        mark.outlineColor = new Color32(90, 50, 0, 255);
        if (defaultFont != null) mark.font = defaultFont;
        mark.rectTransform.sizeDelta = new Vector2(2f, 2f);
        marker.SetActive(false);

        MissionDirector missions = managersGO.AddComponent<MissionDirector>();
        SerializedObject so = new SerializedObject(missions);
        SerializedProperty zones = so.FindProperty("zones");
        zones.arraySize = MissionZoneList.Count;
        for (int i = 0; i < MissionZoneList.Count; i++)
        {
            SerializedProperty z = zones.GetArrayElementAtIndex(i);
            z.FindPropertyRelative("id").stringValue = MissionZoneList[i].id;
            z.FindPropertyRelative("displayName").stringValue = MissionZoneList[i].displayName;
            z.FindPropertyRelative("root").objectReferenceValue = MissionZoneList[i].root;
            z.FindPropertyRelative("center").objectReferenceValue = MissionZoneList[i].root.transform;
            z.FindPropertyRelative("hasWaterSamples").boolValue = MissionZoneList[i].hasSamples;
        }
        SerializedProperty givers = so.FindProperty("giverCandidates");
        SerializedProperty names = so.FindProperty("giverNames");
        SerializedProperty females = so.FindProperty("giverFemale");
        SerializedProperty roles = so.FindProperty("giverRoles");
        SerializedProperty giverZones = so.FindProperty("giverZones");
        giverZones.arraySize = MissionGivers.Count;
        for (int i = 0; i < MissionGivers.Count; i++)
            giverZones.GetArrayElementAtIndex(i).stringValue = i < MissionGiverZones.Count ? MissionGiverZones[i] : "";
        SerializedProperty giverVoices = so.FindProperty("giverVoices");
        giverVoices.arraySize = MissionGivers.Count;
        for (int i = 0; i < MissionGivers.Count; i++)
            giverVoices.GetArrayElementAtIndex(i).stringValue = i < MissionGiverVoices.Count ? MissionGiverVoices[i] : "";
        givers.arraySize = MissionGivers.Count;
        names.arraySize = MissionGivers.Count;
        females.arraySize = MissionGivers.Count;
        roles.arraySize = MissionGivers.Count;
        for (int i = 0; i < MissionGivers.Count; i++)
        {
            givers.GetArrayElementAtIndex(i).objectReferenceValue = MissionGivers[i];
            names.GetArrayElementAtIndex(i).stringValue = MissionGiverNames[i];
            females.GetArrayElementAtIndex(i).boolValue = i < MissionGiverFemale.Count && MissionGiverFemale[i];
            roles.GetArrayElementAtIndex(i).stringValue = i < MissionGiverRoles.Count ? MissionGiverRoles[i] : "";
        }
        GameObject rosaGO = GameObject.Find("NPC_DonaRosa");
        so.FindProperty("rosa").objectReferenceValue = rosaGO != null ? rosaGO.GetComponent<DialogueNPC>() : null;
        SerializedProperty starsProp = so.FindProperty("bannerStars");
        starsProp.arraySize = bannerStars.Length;
        for (int i = 0; i < bannerStars.Length; i++) starsProp.GetArrayElementAtIndex(i).objectReferenceValue = bannerStars[i];
        so.FindProperty("starFilled").objectReferenceValue = starFilledSprite;
        so.FindProperty("starEmpty").objectReferenceValue = starEmptySprite;
        GameObject missionsTitleGO = GameObject.Find("Text_TituloMisiones");
        so.FindProperty("missionsTitle").objectReferenceValue = missionsTitleGO != null ? missionsTitleGO.GetComponent<TMP_Text>() : null;
        so.FindProperty("countdown").objectReferenceValue = managersGO.GetComponent<CountdownManager>();
        so.FindProperty("giverMarker").objectReferenceValue = marker.transform;
        so.FindProperty("bannerPanel").objectReferenceValue = banner.gameObject;
        so.FindProperty("bannerTitle").objectReferenceValue = title;
        so.FindProperty("bannerBody").objectReferenceValue = body;
        so.FindProperty("climax").objectReferenceValue = director;
        so.FindProperty("victoryDance").objectReferenceValue =
            BuildVictoryDancePanel(canvasRoot, starFilledSprite, starEmptySprite);
        so.ApplyModifiedProperties();

        // ---- Ruta morada (solo la ve la cámara del mapa) ----
        EnsureLayer("SoloMapa");
        int mapLayer = LayerMask.NameToLayer("SoloMapa");

        GameObject routeGO = new GameObject("Ruta_Mapa");
        routeGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        if (mapLayer >= 0) routeGO.layer = mapLayer;
        LineRenderer line = routeGO.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.widthMultiplier = 2f;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        Color purple = HexColor("#a45cff");
        line.startColor = purple;
        line.endColor = purple;
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            string dir = ArtDir + "/Materiales";
            CreateFolderRecursive(dir);
            string path = dir + "/Ruta_Morada.mat";
            Material lineMat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (lineMat == null)
            {
                lineMat = new Material(spriteShader);
                AssetDatabase.CreateAsset(lineMat, path);
            }
            lineMat.color = Color.white;
            EditorUtility.SetDirty(lineMat);
            line.sharedMaterial = lineMat;
        }

        if (playerCamera != null && mapLayer >= 0) playerCamera.cullingMask &= ~(1 << mapLayer);

        RouteGuide guide = routeGO.AddComponent<RouteGuide>();
        SerializedObject guideSo = new SerializedObject(guide);
        var roads = new List<(Vector2 a, Vector2 b)>
        {
        };
        // v56: la misma red de calles que usa el tuk tuk para llegar solo,
        // más el puente peatonal (a pie sí se puede cruzar por ahí).
        foreach (RoadNetwork.Segment seg in RoadSegments())
            roads.Add((seg.a, seg.b));
        roads.Add((new Vector2(CuscoX, FootbridgeZ), new Vector2(EastRoadX, FootbridgeZ)));

        SerializedProperty roadsProp = guideSo.FindProperty("roads");
        roadsProp.arraySize = roads.Count;
        for (int i = 0; i < roads.Count; i++)
        {
            SerializedProperty r = roadsProp.GetArrayElementAtIndex(i);
            r.FindPropertyRelative("a").vector2Value = roads[i].a;
            r.FindPropertyRelative("b").vector2Value = roads[i].b;
        }
        GameObject playerGO = GameObject.Find("Player");
        guideSo.FindProperty("player").objectReferenceValue = playerGO != null ? playerGO.transform : null;
        guideSo.FindProperty("line").objectReferenceValue = line;
        guideSo.FindProperty("mapCamera").objectReferenceValue = mapCamera;
        guideSo.FindProperty("lineHeight").floatValue = 40f;
        guideSo.ApplyModifiedProperties();
    }
}
