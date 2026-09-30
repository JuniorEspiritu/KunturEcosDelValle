using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Todo lo que usa los assets importados de la Asset Store: personas (City
// People), autos/combis/buses, semáforos, edificios, parque y mobiliario,
// y el agua del río. Va en un archivo aparte para que el constructor principal
// no siga creciendo, y cada pieza tiene su plan B: si falta un asset, se
// arma lo de antes con primitivas y la escena igual se construye.
public static partial class KunturSceneBuilder
{
    // ---------------------------------------------------------------
    // Herramientas comunes
    // ---------------------------------------------------------------

    private static readonly HashSet<string> MissingAssetsLogged = new HashSet<string>();

    private static GameObject LoadPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null && MissingAssetsLogged.Add(path))
            Debug.LogWarning($"[Kuntur] No encontré el asset {path}; uso el reemplazo de siempre.");
        return prefab;
    }

    // Caja real del modelo en coordenadas de mundo, contando mallas normales Y
    // mallas con esqueleto (las personas). Renderer.bounds no sirve acá: en un
    // objeto recién creado en el editor muchas veces viene vacío.
    private static Bounds? MeasureWorldBounds(GameObject root)
    {
        Bounds result = new Bounds();
        bool started = false;

        void AddMesh(Mesh mesh, Transform t)
        {
            if (mesh == null) return;
            Bounds b = mesh.bounds;
            Matrix4x4 m = t.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                Vector3 w = m.MultiplyPoint3x4(corner);
                if (!started) { result = new Bounds(w, Vector3.zero); started = true; }
                else result.Encapsulate(w);
            }
        }

        foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshRenderer r = f.GetComponent<MeshRenderer>();
            if (r == null) continue; // una malla sin renderer (collider, LOD suelto) no se ve
            AddMesh(f.sharedMesh, f.transform);
        }
        foreach (SkinnedMeshRenderer s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            AddMesh(s.sharedMesh, s.transform);

        return started ? result : (Bounds?)null;
    }

    // Coloca un prefab dentro de un "envoltorio" propio: el envoltorio queda
    // con su pivote en el piso, al centro del modelo, sin importar dónde puso
    // el pivote el autor del asset (los edificios de POLYGON lo traen en una
    // esquina, los autos con escala 14 en la raíz). Así todo se ubica, gira y
    // escala igual: por el envoltorio, sin tocar el prefab.
    //
    // targetHeight > 0 escala el modelo a esa altura; 0 lo deja a su tamaño.
    private static GameObject PlaceModel(GameObject prefab, Transform parent, string name,
        Vector3 groundPosition, Quaternion rotation, float targetHeight, out Vector3 size)
    {
        GameObject wrapper = new GameObject(name);
        wrapper.transform.SetParent(parent, false);
        wrapper.transform.position = Vector3.zero;
        wrapper.transform.rotation = Quaternion.identity;
        wrapper.transform.localScale = Vector3.one;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        FixPipelineMaterials(instance);

        Bounds bounds = MeasureWorldBounds(instance) ?? new Bounds(Vector3.up * 0.5f, Vector3.one);
        instance.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

        float scale = targetHeight > 0.01f && bounds.size.y > 0.001f ? targetHeight / bounds.size.y : 1f;
        wrapper.transform.localScale = Vector3.one * scale;
        wrapper.transform.position = groundPosition;
        wrapper.transform.rotation = rotation;

        size = bounds.size * scale;
        return wrapper;
    }

    private static void SetStaticRecursive(GameObject go, bool isStatic)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = isStatic;
    }

    private static void RemoveColliders(GameObject go)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        // Sin colliders, un Rigidbody con gravedad se cae por el piso al dar
        // Play: los camiones de "Ukraine only truck" traen uno de 12 t y por
        // eso solo se veían los faros flotando en la pista.
        foreach (Joint j in go.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(j);
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
    }

    private static void RemoveLights(GameObject go)
    {
        foreach (Light l in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
    }

    // ---------------------------------------------------------------
    // Personas (City People)
    // ---------------------------------------------------------------

    private const string PeopleDir = "Assets/DenysAlmaral/CityPeople";

    // Los 8 modelos "de calle". La niña con prótesis va aparte porque trae su
    // propio controlador de animación.
    private static readonly string[] PeoplePrefabs =
    {
        PeopleDir + "/Prefabs/city/casual_Female_G.prefab",
        PeopleDir + "/Prefabs/city/casual_Male_G.prefab",
        PeopleDir + "/Prefabs/downtown/casual_Female_K.prefab",
        PeopleDir + "/Prefabs/downtown/casual_Male_K.prefab",
        PeopleDir + "/Prefabs/elder/elder_Female_A.prefab",
        PeopleDir + "/Prefabs/professions/Doctor_Male_B.prefab",
        PeopleDir + "/Prefabs/little_kids/little_boy_B.prefab",
        PeopleDir + "/Prefabs/professions/police_Female_A.prefab",
    };

    private const float PeopleScale = 1.45f;

    private static List<Material> peoplePalettes;
    private static int personCounter;

    // Las paletas son lo que evita que se vean repetidos: el mismo modelo con
    // otra paleta tiene otra ropa, otro pelo y otro tono de piel. Vienen en
    // URP, así que se pasan a Built-in una vez.
    private static List<Material> GetPeoplePalettes()
    {
        if (peoplePalettes != null) return peoplePalettes;
        peoplePalettes = new List<Material>();

        foreach (string guid in AssetDatabase.FindAssets("people_pal t:Material", new[] { PeopleDir + "/Materials" }))
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (source == null || !source.name.StartsWith("people_pal")) continue;
            peoplePalettes.Add(ConvertToBuiltIn(source, null, null));
        }

        peoplePalettes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return peoplePalettes;
    }

    private static bool IsFemaleModel(string path)
    {
        string file = Path.GetFileNameWithoutExtension(path);
        return file.Contains("Female") || file.Contains("girl");
    }

    private static AnimationClip LoadPeopleClip(string fileName)
    {
        string path = $"{PeopleDir}/Animations/{fileName}.fbx";
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is AnimationClip clip && !clip.name.StartsWith("__preview")) return clip;
        }
        return null;
    }

    private static readonly Dictionary<string, RuntimeAnimatorController> PeopleControllers = new Dictionary<string, RuntimeAnimatorController>();

    // Controlador propio para cada tipo de persona. El que trae el asset
    // cambia de animación al azar cada tantos segundos (baila, trota...),
    // que no sirve para alguien que camina por la vereda.
    //   walker = true : dos estados, Quieto y Camina, con el parámetro "Walking".
    //   walker = false: un solo estado quieto (vecinos conversando).
    private static RuntimeAnimatorController GetPeopleController(bool female, bool walker, int variant)
    {
        string g = female ? "f" : "m";
        string[] walks = { $"locom_{g}_basicWalk_30f", $"locom_{g}_slowWalk_40f", $"locom_{g}_phoneWalking_40f" };
        string[] idles = female
            ? new[] { "idle_f_1_150f", "idle_f_2_190f", "idle_phoneTalking_180f" }
            : new[] { "idle_m_1_200f", "idle_m_2_220f", "idle_phoneTalking_180f", "idle_selfcheck_1_300f" };

        string walkName = walks[variant % walks.Length];
        string idleName = idles[variant % idles.Length];
        string key = walker ? $"Camina_{walkName}_{idleName}" : $"Quieto_{idleName}";
        if (PeopleControllers.TryGetValue(key, out RuntimeAnimatorController cached) && cached != null) return cached;

        AnimationClip idle = LoadPeopleClip(idleName);
        AnimationClip walk = walker ? LoadPeopleClip(walkName) : null;
        if (idle == null || (walker && walk == null)) return null;

        string dir = ArtDir + "/Animaciones/Personas";
        CreateFolderRecursive(dir);
        string path = $"{dir}/{key}.controller";
        // Se rehace siempre: si quedara uno viejo con otros estados, el
        // peatón podría quedarse trabado en una animación que ya no existe.
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idleState = sm.AddState("Quieto");
        idleState.motion = idle;
        sm.defaultState = idleState;

        if (walker)
        {
            controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
            AnimatorState walkState = sm.AddState("Camina");
            walkState.motion = walk;

            AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.18f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "Walking");

            AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.22f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Walking");

            // Arranca caminando: si arrancara quieto, todos los peatones
            // darían el primer paso sincronizados al darle Play.
            sm.defaultState = walkState;
        }

        EditorUtility.SetDirty(controller);
        PeopleControllers[key] = controller;
        return controller;
    }

    // Velocidad de marcha que le va a cada animación, para que los pies no
    // "patinen" sobre la vereda.
    private static float WalkSpeedForVariant(int variant)
    {
        switch (variant % 3)
        {
            case 1: return 0.85f;  // paso lento
            case 2: return 1.0f;   // caminando con el celular
            default: return 1.3f;  // paso normal
        }
    }

    // Crea una persona de City People con pies en groundPosition, mirando al
    // +Z del padre. Devuelve el objeto del modelo (con su Animator) o null si
    // el asset no está.
    private static GameObject BuildPersonModel(Transform parent, string name, Vector3 localFeet, bool walker, out Animator animator, out bool female, out int variant, string forcedPrefab = null)
    {
        animator = null;
        female = false;
        variant = 0;

        int index = personCounter++;
        string prefabPath = forcedPrefab ?? PeoplePrefabs[index % PeoplePrefabs.Length];
        GameObject prefab = LoadPrefab(prefabPath);
        if (prefab == null) return null;

        female = IsFemaleModel(prefabPath);
        variant = (index / PeoplePrefabs.Length + index) % 4;

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        model.name = name;
        model.transform.localPosition = localFeet;
        model.transform.localRotation = Quaternion.identity;
        // Un poco más grandes que el tamaño del asset: al lado de Kuntur (un
        // cóndor de tamaño "personaje") se veían como niños.
        model.transform.localScale = Vector3.one * PeopleScale;
        FixPipelineMaterials(model);

        // Paleta distinta por persona: salta de 7 en 7 entre las 24, así dos
        // personas seguidas nunca comparten ropa aunque sean el mismo modelo.
        List<Material> palettes = GetPeoplePalettes();
        if (palettes.Count > 0)
        {
            Material palette = palettes[(index * 7 + 3) % palettes.Count];
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].name.StartsWith("people_pal")) { mats[i] = palette; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            RuntimeAnimatorController controller = forcedPrefab != null && forcedPrefab.Contains("prostheticLeg")
                ? animator.runtimeAnimatorController   // trae el suyo (caminar con prótesis, conversar)
                : GetPeopleController(female, walker, variant);
            if (controller != null) animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;          // lo mueve PedestrianWalker, no la animación
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        // El script de demo del asset reproduce animaciones al azar y agrega
        // un collider; acá sobra.
        foreach (MonoBehaviour mb in model.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb != null && mb.GetType().Name == "CityPeople") Object.DestroyImmediate(mb);
        }

        RemoveColliders(model);
        SetStaticRecursive(model, false);
        return model;
    }

    // Peatón de City People: raíz vacía con PedestrianWalker (que la mueve)
    // y el modelo adentro, con los pies en el suelo.
    private static bool TryBuildAssetPedestrian(Transform parent, string name, Vector3 a, Vector3 b, System.Random rng)
    {
        return BuildAssetPedestrian(parent, name, a, b, rng) != null;
    }

    private static GameObject BuildAssetPedestrian(Transform parent, string name, Vector3 a, Vector3 b, System.Random rng, string forcedPrefab = null)
    {
        if (LoadPrefab(PeoplePrefabs[0]) == null) return null;

        GameObject walker = new GameObject(name);
        walker.transform.SetParent(parent);
        walker.transform.position = a;

        GameObject model = BuildPersonModel(walker.transform, "Cuerpo", Vector3.zero, true, out Animator animator, out _, out int variant, forcedPrefab);
        if (model == null) { Object.DestroyImmediate(walker); return null; }

        PedestrianWalker legs = walker.AddComponent<PedestrianWalker>();
        SerializedObject so = new SerializedObject(legs);
        so.FindProperty("pointA").vector3Value = a;
        so.FindProperty("pointB").vector3Value = b;
        so.FindProperty("speed").floatValue = WalkSpeedForVariant(variant) * (float)(0.95 + rng.NextDouble() * 0.1);
        so.FindProperty("pauseAtEnds").floatValue = (float)(1.2 + rng.NextDouble() * 2.5);
        so.FindProperty("startProgress").floatValue = (float)rng.NextDouble();
        so.FindProperty("bodyAnimator").objectReferenceValue = animator;
        so.ApplyModifiedProperties();

        AddPersonBlocker(walker);
        SetStaticRecursive(walker, false);
        return walker;
    }

    // v55: las personas ya no se traspasan. Cápsula sólida en un hijo (el
    // collider de la raíz lo usa MakeMissionGiver para hablar y se apaga
    // cuando no le toca) con un Rigidbody cinemático: se mueve con la persona
    // sin caerse ni empujarla. Va en la capa "Ignore Raycast" para que los
    // animales, que se apoyan en el suelo con un rayo, no se suban encima.
    private static void AddPersonBlocker(GameObject person)
    {
        if (person == null || person.transform.Find("Colision") != null) return;
        GameObject blocker = new GameObject("Colision");
        blocker.transform.SetParent(person.transform, false);
        blocker.layer = 2; // Ignore Raycast
        CapsuleCollider col = blocker.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f, 0f);
        col.height = 1.75f;
        col.radius = 0.32f;
        Rigidbody rb = blocker.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
        blocker.isStatic = false;
    }

    // Vecino conversando: persona quieta con su globito de diálogo encima.
    // La primera pareja la forma la niña con prótesis, que trae su propia
    // animación de conversar: el pueblo también es de ella.
    private static int villagerCounter;

    private static GameObject TryBuildAssetVillager(Transform parent, string name, Vector3 position, string forcedPrefab = null)
    {
        if (LoadPrefab(PeoplePrefabs[0]) == null) return null;

        GameObject villager = new GameObject(name);
        villager.transform.SetParent(parent);
        villager.transform.position = position;

        // La niña con prótesis conversa con la primera vecina (no da misiones).
        string forced = villagerCounter++ == 1 ? PeopleDir + "/Prefabs/disabilities/prostheticLeg_girl.prefab" : forcedPrefab;
        if (forced != null && LoadPrefab(forced) == null) forced = null;

        GameObject model = BuildPersonModel(villager.transform, "Cuerpo", Vector3.zero, false, out _, out _, out _, forced);
        if (model == null) { Object.DestroyImmediate(villager); return null; }

        float height = (MeasureWorldBounds(model)?.size.y) ?? 1.7f;
        GameObject bubble = PrimitiveObject(villager.transform, "Globo", PrimitiveType.Sphere,
            position + new Vector3(0.35f, height + 0.45f, 0f), new Vector3(0.5f, 0.36f, 0.5f), UIPalette_CreamSoft());
        bubble.isStatic = false;

        AddRandomAnimStart(villager);
        AddPersonBlocker(villager);
        SetStaticRecursive(villager, false);
        return villager;
    }

    private static void AddRandomAnimStart(GameObject go)
    {
        if (go.GetComponent<AnimatorRandomStart>() == null) go.AddComponent<AnimatorRandomStart>();
    }

    // Cambia el cuerpo de cápsula de un NPC de misión (Yamile, Doña Rosa) por
    // una persona de verdad. La cápsula se queda: tiene el collider y el
    // diálogo, solo deja de verse.
    private static void SwapNpcBody(GameObject npc, string prefabPath)
    {
        if (LoadPrefab(prefabPath) == null) return;

        GameObject model = BuildPersonModel(npc.transform, "Cuerpo", Vector3.zero, false, out _, out _, out _, prefabPath);
        if (model == null) return;

        // La cápsula mide 2 con el centro a 1: los pies están en y = -1 local.
        Vector3 parentScale = npc.transform.lossyScale;
        model.transform.localPosition = new Vector3(0f, -1f, 0f);
        model.transform.localScale = new Vector3(PeopleScale / parentScale.x, PeopleScale / parentScale.y, PeopleScale / parentScale.z);

        MeshRenderer capsuleRenderer = npc.GetComponent<MeshRenderer>();
        if (capsuleRenderer != null) capsuleRenderer.enabled = false;

        foreach (string part in new[] { "Poncho", "Cabeza", "Sombrero_Copa", "Sombrero_Ala", "Brazo_Izq", "Brazo_Der" })
        {
            Transform t = npc.transform.Find(part);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        AddRandomAnimStart(npc);
    }

    // ---------------------------------------------------------------
    // Vehículos (v53: ARCADE Free Racing Car, Simple Retro Car y los
    // camiones/bus/van de "Ukraine only truck"). Los carros abollados del
    // Apocalyptic Vehicles Pack ya no se usan.
    // ---------------------------------------------------------------

    private const string UkraineCars = "Assets/Ukraine_only_truck/Prefabs_With_Colliders/cars_colliiders";
    private const string ArcadeCars = "Assets/ARCADE - FREE Racing Car/Prefabs (Meshes Only)";

    // (prefab, largo real en metros). Todos miran al +Z (ver Logs/Previews).
    private static readonly (string path, float length)[] CarPool =
    {
        (ArcadeCars + "/Free Racing Car.prefab", 4.6f),
        (ArcadeCars + "/Free Racing Car Blue Variant.prefab", 4.6f),
        (ArcadeCars + "/Free Racing Car Gray Variant.prefab", 4.6f),
        (ArcadeCars + "/Free Racing Car Purple Variant.prefab", 4.6f),
        (ArcadeCars + "/Free Racing Car Red Variant.prefab", 4.6f),
        ("Assets/Polyeler/Simple Retro Car/Prefabs/Simple Retro Car.prefab", 4.4f),
        (UkraineCars + "/car/car_1/car_1_blue.prefab", 4.2f),
    };

    private static readonly (string path, float length)[] VanPool =
    {
        (UkraineCars + "/van/van_1/van_1_black.prefab", 4.5f),
    };

    private static readonly (string path, float length)[] BigPool =
    {
        (UkraineCars + "/bus/bus_1/bus_1_yellow.prefab", 8.2f),
        (UkraineCars + "/truck/truck_2/truck_2_red.prefab", 7.8f),
        (UkraineCars + "/truck/truck_3/truck_3_orange.prefab", 8.0f),
        (UkraineCars + "/truck/truck_4/truck_4_yellow.prefab", 7.8f),
        (UkraineCars + "/truck/truck_5/truck_5_green.prefab", 8.0f),
        (UkraineCars + "/truck/truck_6/truck_6_white.prefab", 8.0f),
        (UkraineCars + "/truck/truck_7/truck_6_green.prefab", 8.2f),
    };

    // Cada vehículo que circula lleva varios modelos adentro (apagados menos
    // uno): al llegar al final de la calle desaparece y vuelve a salir desde
    // su punto de partida con OTRO modelo (ver CarPatrol).
    private const int VariantsPerVehicle = 5;

    private enum VehicleKind { Car, Combi, Bus, Police }

    // moving = false: auto estacionado (sin recorrido ni motor).
    private static bool TryBuildAssetVehicle(Transform parent, string name, VehicleKind kind,
        Vector3 from, Vector3 to, float speed, string routeText, bool moving, float yaw = 0f)
    {
        return BuildAssetVehicle(parent, name, kind, from, to, speed, routeText, moving, yaw) != null;
    }

    // Qué tipo de modelo sale en cada aparición: sobre todo autos, a veces
    // una van y de vez en cuando un camión o el bus.
    private static (string path, float length) PickVehicleModel(VehicleKind kind, System.Random rng)
    {
        int roll = rng.Next(100);
        int carChance = kind == VehicleKind.Bus ? 40 : kind == VehicleKind.Combi ? 45 : 75;
        int vanChance = kind == VehicleKind.Combi ? 40 : 10;
        if (roll < carChance) return CarPool[rng.Next(CarPool.Length)];
        if (roll < carChance + vanChance) return VanPool[rng.Next(VanPool.Length)];
        return BigPool[rng.Next(BigPool.Length)];
    }

    private static GameObject BuildAssetVehicle(Transform parent, string name, VehicleKind kind,
        Vector3 from, Vector3 to, float speed, string routeText, bool moving, float yaw = 0f)
    {
        // Semilla por nombre Y posición: los autos estacionados se llaman
        // todos igual, y con solo el nombre salían todos del mismo modelo.
        System.Random rng = new System.Random(StableHash(name + from.ToString("F1")));
        if (LoadPrefab(CarPool[0].path) == null && LoadPrefab(CarPool[5].path) == null) return null;

        GameObject vehicle = new GameObject(name);
        vehicle.transform.SetParent(parent);
        vehicle.transform.position = from;

        var variants = new List<Transform>();
        var halfLengths = new List<float>();
        int wanted = moving ? VariantsPerVehicle : 1;
        for (int attempt = 0; attempt < wanted * 3 && variants.Count < wanted; attempt++)
        {
            // La primera variante siempre es un auto: así nadie arranca en camión.
            var model = variants.Count == 0 ? CarPool[rng.Next(CarPool.Length)] : PickVehicleModel(kind, rng);
            Transform variant = BuildVehicleVariant(vehicle.transform, $"Modelo_{variants.Count + 1}", model.path, model.length, from, out float halfLength);
            if (variant == null) continue;
            variants.Add(variant);
            halfLengths.Add(halfLength);
            variant.gameObject.SetActive(variants.Count == 1);
        }

        if (variants.Count == 0)
        {
            Object.DestroyImmediate(vehicle);
            return null;
        }

        SetStaticRecursive(vehicle, false);

        if (moving)
        {
            AttachVehicleAI(vehicle, from, to, speed, 1.6f, halfLengths[0], 1f);
            CarPatrol patrol = vehicle.GetComponent<CarPatrol>();
            SerializedObject so = new SerializedObject(patrol);
            so.FindProperty("oneWay").boolValue = true;
            SerializedProperty vp = so.FindProperty("variants");
            SerializedProperty hp = so.FindProperty("variantHalfLengths");
            vp.arraySize = variants.Count;
            hp.arraySize = variants.Count;
            for (int i = 0; i < variants.Count; i++)
            {
                vp.GetArrayElementAtIndex(i).objectReferenceValue = variants[i].gameObject;
                hp.GetArrayElementAtIndex(i).floatValue = halfLengths[i];
            }
            so.ApplyModifiedProperties();
        }
        else
        {
            vehicle.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            SetStaticRecursive(vehicle, true);
        }

        return vehicle;
    }

    // Un modelo de vehículo, escalado a su largo real, con su collider de caja
    // y los faros/stops que se prenden de noche.
    private static Transform BuildVehicleVariant(Transform vehicle, string name, string prefabPath, float length, Vector3 from, out float halfLength)
    {
        halfLength = 2.2f;
        GameObject prefab = LoadPrefab(prefabPath);
        if (prefab == null) return null;

        GameObject holder = new GameObject(name);
        holder.transform.SetParent(vehicle, false);
        holder.transform.position = from;

        GameObject body = PlaceModel(prefab, holder.transform, "Carroceria", from, Quaternion.identity, 0f, out Vector3 size);
        RemoveColliders(body);
        if (size.z > 0.01f)
        {
            float scale = length / size.z;
            body.transform.localScale *= scale;
            size *= scale; // el pivote del envoltorio está en el suelo: escala sin despegarse
        }

        BoxCollider box = holder.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, size.y / 2f, 0f);
        box.size = new Vector3(size.x * 0.92f, size.y, size.z * 0.96f);

        halfLength = size.z / 2f;
        bool tall = size.y > 2.4f;
        AddVehicleLights(holder.transform, from, size.x * 0.3f, size.y * (tall ? 0.22f : 0.4f),
            halfLength + 0.01f, halfLength + 0.01f);
        return holder.transform;
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            int h = 23;
            foreach (char c in text) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }

    // ---------------------------------------------------------------
    // Semáforos (Tarbo City Traffic Lights)
    // ---------------------------------------------------------------

    private const string TrafficLightPrefab =
        "Assets/Tarbo-CITY-TrafficLights/Prefabs/Props/Road/TB_CITY_Prop_TrafficLight_4Set_Yellow.prefab";

    // Semáforo peruano de esquina: poste en la vereda con cabezales de
    // carcasa amarilla (rojo arriba, ámbar, verde abajo) mirando a las cuatro
    // bocacalles, a unos 5 m de alto. Es el que se ve en los cruces del centro
    // de Huancayo, en vez del brazo largo sobre la pista de las avenidas de
    // EE. UU.
    private static bool TryBuildPeruTrafficLight(Transform parent, Vector3 basePosition)
    {
        if (IsOnRoadway(basePosition)) return true; // en la pista no se pone, y no hace falta plan B
        GameObject prefab = LoadPrefab(TrafficLightPrefab);
        if (prefab == null) return false;

        GameObject light = PlaceModel(prefab, parent, "Semaforo", basePosition, Quaternion.identity, 5.2f, out _);
        SetStaticRecursive(light, true);

        // Base de concreto pintada a franjas, como las de Lima y Huancayo.
        PrimitiveObject(light.transform, "Base_Franjas", PrimitiveType.Cylinder,
            basePosition + Vector3.up * 0.35f, new Vector3(0.34f, 0.35f, 0.34f), HexColor("#f1c40f"));
        PrimitiveObject(light.transform, "Base_Franja_Negra", PrimitiveType.Cylinder,
            basePosition + Vector3.up * 0.35f, new Vector3(0.345f, 0.09f, 0.345f), HexColor("#1f1f1f"));
        return true;
    }

    private static void BuildPeruTrafficLights(Transform parent)
    {
        GameObject root = new GameObject("Semaforos");
        root.transform.SetParent(parent);

        // Los tres cruces de la Av. Giráldez: esquina sureste y noroeste de
        // cada uno (la noreste ya tiene el letrero con los nombres).
        (float x, float half)[] crossings = { (MainStreetX, MainStreetHalf), (CuscoX, CrossStreetHalfRoad), (PunoX, CrossStreetHalfRoad) };
        foreach (var c in crossings)
        {
            float dx = c.half + 1.9f;
            float dz = AvenueHalf + 2.5f;
            Vector3 se = new Vector3(c.x + dx, 0f, AvenueZ - dz);
            Vector3 nw = new Vector3(c.x - dx, 0f, AvenueZ + dz);

            if (!TryBuildPeruTrafficLight(root.transform, se)) BuildTrafficLight(root.transform, se, -1f);
            if (!TryBuildPeruTrafficLight(root.transform, nw)) BuildTrafficLight(root.transform, nw, 1f);
        }
    }

    // ---------------------------------------------------------------
    // Edificios (POLYGON city pack)
    // ---------------------------------------------------------------

    private const string CityPackDir = "Assets/POLYGON city pack/Prefabs";

    // facadeZ = true si la fachada del modelo mira a su +Z; si no, mira a +X
    // (se comprobó con las fotos de KunturAssetPreview).
    private static readonly (string file, bool facadeZ)[] CityBuildings =
    {
        ("Build_G-Left_Prefab", false), ("Building_F_prefab", false), ("Building_I_1_prefab", false),
        ("Building_M_prefab", false), ("Build_G-middle_Prefab", false), ("Building_T_prefab", false),
        ("Building_E_prefab", false), ("Building_N_Prefab", false), ("Shop_A_prefab", true),
        ("Building_W_prefab", false), ("Build_G-right_Prefab", false), ("building_X_prefab", false),
        ("Building_I_2_Prefab", false), ("Building_R_Prefab", false), ("Building_Q_prefab", false),
        ("Building_D_prefab", false), ("Building_I_3_prefab", false), ("Building_Y_prefab", false),
        ("Building_O_PREFAB", false), ("Police_station_prefab", false),
    };

    private static int buildingCounter;

    // Se llama al empezar cada construcción: con los contadores en cero, la
    // escena sale idéntica cada vez (mismos autos, mismas personas).
    private static void ResetAssetCounters()
    {
        personCounter = 0;
        buildingCounter = 0;
        villagerCounter = 0;
        MissionGivers.Clear();
        MissionGiverNames.Clear();
        MissionGiverFemale.Clear();
        SitControllers.Clear();
        walkingGiverCounter = 0;
        pedestrianGiverTick = 0;
        MissionGiverRoles.Clear();
        MissionGiverZones.Clear();
        MissionGiverVoices.Clear();
        MissionZoneList.Clear();
        PeopleControllers.Clear();
        peoplePalettes = null;
    }

    // Frentes de edificios del centro: las dos veredas de la Calle Real de
    // punta a punta, y los lados de Jr. Cusco y Jr. Puno que miran al centro.
    // El resto del pueblo sigue con casas de adobe: el centro de Huancayo es
    // así, edificios de 3-5 pisos en las calles principales y casas bajas
    // apenas uno se aleja.
    private static void BuildCityBuildings(Transform parent, System.Random rng)
    {
        GameObject root = new GameObject("Edificios_Centro");
        root.transform.SetParent(parent);

        // edgeX = filo de la vereda; outward = hacia dónde crece el edificio
        // (lejos de la calle); maxDepth = fondo disponible hasta la manzana
        // de atrás.
        (float edgeX, float outward, float maxDepth, float fromZ, float toZ)[] frontages =
        {
            (MainStreetX + 7.25f, 1f, 10.2f, TownSouthZ + 4f, TownNorthZ - 4f),   // Calle Real, vereda este
            (MainStreetX - 7.25f, -1f, 13.8f, TownSouthZ + 4f, TownNorthZ - 4f),  // Calle Real, vereda oeste
            (CuscoX - CrossStreetHalf - 0.25f, -1f, 10.2f, -45f, 58f),      // Jr. Cusco, lado del centro
            (PunoX + CrossStreetHalf + 0.25f, 1f, 13.8f, -45f, 58f),        // Jr. Puno, lado del centro
        };

        // Tamaño de cada modelo (sin girar), medido una sola vez.
        var sizes = new Dictionary<string, Vector3>();
        foreach (var spec in CityBuildings)
        {
            GameObject prefab = LoadPrefab($"{CityPackDir}/Buildings/{spec.file}.prefab");
            if (prefab == null) continue;
            GameObject probe = PlaceModel(prefab, root.transform, "Medida", new Vector3(0f, -500f, 0f), Quaternion.identity, 0f, out Vector3 size);
            Object.DestroyImmediate(probe);
            sizes[spec.file] = size;
        }

        int placed = 0;
        foreach (var f in frontages)
        {
            float cursor = f.fromZ;
            int guard = 0;
            while (cursor < f.toZ && guard++ < 500)
            {
                // Algunos lotes quedan para casas de adobe, como en el centro
                // de verdad, donde entre edificio y edificio sobrevive la casona.
                if (rng.Next(100) < 12) { cursor += NextFloat(rng, 7f, 9f); continue; }

                // Se prueba el que toca en la lista y, si no entra en ese
                // hueco (la cuadra se acaba o hay algo), el siguiente. Antes
                // un edificio grande que no entraba se "comía" la cuadra
                // entera esperando su turno.
                bool built = false;
                for (int attempt = 0; attempt < CityBuildings.Length && !built; attempt++)
                {
                    var spec = CityBuildings[(buildingCounter + attempt) % CityBuildings.Length];
                    if (!sizes.TryGetValue(spec.file, out Vector3 size)) continue;

                    float depth = spec.facadeZ ? size.z : size.x;
                    float frontage = spec.facadeZ ? size.x : size.z;
                    float scale = depth > f.maxDepth ? f.maxDepth / depth : 1f;
                    if (scale < 0.72f || size.y * scale > 17.5f) continue;

                    depth *= scale;
                    frontage *= scale;
                    Vector3 center = new Vector3(f.edgeX + f.outward * depth / 2f, 0f, cursor + frontage / 2f);
                    float halfX = depth / 2f + 0.15f;
                    float halfZ = frontage / 2f + 0.15f;
                    if (!IsAreaFree(center, halfX, halfZ)) continue;

                    // La fachada mira a la calle. Solo giro en Y: FromToRotation
                    // no sirve para vueltas de 180° (puede dejarlo de cabeza).
                    Vector3 facadeDir = new Vector3(-f.outward, 0f, 0f);
                    Vector3 localFacade = spec.facadeZ ? Vector3.forward : Vector3.right;
                    Quaternion rotation = Quaternion.Euler(0f, Vector3.SignedAngle(localFacade, facadeDir, Vector3.up), 0f);

                    GameObject prefab = LoadPrefab($"{CityPackDir}/Buildings/{spec.file}.prefab");
                    GameObject building = PlaceModel(prefab, root.transform, $"Edificio_{spec.file}_{placed}", center, rotation, 0f, out _);
                    building.transform.localScale *= scale;
                    building.transform.position = center;

                    Occupy(center, halfX * 2f, halfZ * 2f);
                    SetStaticRecursive(building, true);
                    RemoveLights(building);

                    placed++;
                    buildingCounter += attempt + 1;
                    cursor += frontage + NextFloat(rng, 0.2f, 0.8f);
                    built = true;
                }

                if (!built) cursor += 1.2f;
            }
        }

        Debug.Log($"[Kuntur] Edificios del centro: {placed}.");
    }

    // ---------------------------------------------------------------
    // Parque y mobiliario urbano
    // ---------------------------------------------------------------

    // Manzana entre la Calle Real y el Jr. Cusco, del Jr. Junín al Jr. Loreto.
    // Medida para caber justo entre las cuatro veredas (x 7–27.9, z -38.9–-24.1).
    private static readonly Vector3 ParkCenter = new Vector3(17.45f, 0f, -31.5f);
    private const float ParkSizeX = 19.8f;
    private const float ParkSizeZ = 14.4f;

    private static GameObject PlaceProp(Transform parent, string path, string name, Vector3 position, float yaw, float targetHeight, bool keepColliders = true)
    {
        GameObject prefab = LoadPrefab(path);
        if (prefab == null) return null;
        GameObject prop = PlaceModel(prefab, parent, name, position, Quaternion.Euler(0f, yaw, 0f), targetHeight, out _);
        if (!keepColliders) RemoveColliders(prop);
        RemoveLights(prop);
        SetStaticRecursive(prop, true);
        return prop;
    }

    // Parque Huamanmarca: un parque de barrio con pasto, caminos en cruz,
    // árboles, bancas, faroles, flores y una pileta al centro. Se aparta ANTES
    // de los edificios para que no le construyan encima.
    private static void ReserveParkLot()
    {
        Occupy(ParkCenter, ParkSizeX, ParkSizeZ);
    }

    private static void BuildPark(Transform parent)
    {
        GameObject park = new GameObject("Plaza_Constitucion");
        park.transform.SetParent(parent);
        Vector3 c = ParkCenter;

        // Pasto, sardinel y caminos.
        PrimitiveObject(park.transform, "Pasto_Parque", PrimitiveType.Cube,
            c + Vector3.up * 0.05f, new Vector3(ParkSizeX, 0.1f, ParkSizeZ), HexColor("#5e9a45"));
        PrimitiveObject(park.transform, "Camino_NS", PrimitiveType.Cube,
            c + Vector3.up * 0.11f, new Vector3(2.2f, 0.02f, ParkSizeZ), HexColor("#cdbf9f"));
        PrimitiveObject(park.transform, "Camino_EO", PrimitiveType.Cube,
            c + Vector3.up * 0.11f, new Vector3(ParkSizeX, 0.02f, 2.2f), HexColor("#cdbf9f"));

        // Pileta: la plaza de pueblo sin pileta no es plaza.
        PrimitiveObject(park.transform, "Pileta_Borde", PrimitiveType.Cylinder,
            c + Vector3.up * 0.35f, new Vector3(4.2f, 0.35f, 4.2f), HexColor("#b8b1a4"));
        GameObject water = PrimitiveObject(park.transform, "Pileta_Agua", PrimitiveType.Cylinder,
            c + Vector3.up * 0.62f, new Vector3(3.7f, 0.05f, 3.7f), HexColor("#4f9cc9"));
        water.isStatic = false;
        PrimitiveObject(park.transform, "Pileta_Columna", PrimitiveType.Cylinder,
            c + Vector3.up * 1.2f, new Vector3(0.45f, 0.9f, 0.45f), HexColor("#a39b8e"));
        PrimitiveObject(park.transform, "Pileta_Plato", PrimitiveType.Cylinder,
            c + Vector3.up * 2.05f, new Vector3(1.5f, 0.08f, 1.5f), HexColor("#a39b8e"));

        // Árboles en las cuatro esquinas del pasto (dos modelos distintos).
        float ax = ParkSizeX / 2f - 2.6f;
        float az = ParkSizeZ / 2f - 2.4f;
        Vector3[] treeSpots =
        {
            c + new Vector3(-ax, 0f, -az), c + new Vector3(ax, 0f, -az),
            c + new Vector3(-ax, 0f, az), c + new Vector3(ax, 0f, az),
            c + new Vector3(-ax * 0.45f, 0f, az + 0.6f), c + new Vector3(ax * 0.45f, 0f, -az - 0.6f),
        };
        for (int i = 0; i < treeSpots.Length; i++)
        {
            string path = i % 2 == 0
                ? "Assets/ModularLowpolyStreetsFree/Prefabs/Other/Tree1.prefab"
                : CityPackDir + "/Props/Tree prefab.prefab";
            GameObject tree = PlaceProp(park.transform, path, $"Arbol_Parque_{i}", treeSpots[i] + Vector3.up * 0.1f, i * 47f, i % 2 == 0 ? 5.6f : 6.5f);
            if (tree == null) BuildRoundTree(park.transform, $"Arbol_Parque_{i}", treeSpots[i], new System.Random(i));
        }

        // Bancas mirando a la pileta, a los lados de los caminos.
        Vector3[] benchSpots =
        {
            c + new Vector3(-3.6f, 0.1f, 2.4f), c + new Vector3(3.6f, 0.1f, 2.4f),
            c + new Vector3(-3.6f, 0.1f, -2.4f), c + new Vector3(3.6f, 0.1f, -2.4f),
        };
        foreach (Vector3 spot in benchSpots)
        {
            Vector3 toCenter = c - spot; toCenter.y = 0f;
            float yaw = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
            // El modelo mira a su +Z: con este giro el asiento da a la pileta
            // (antes quedaban de espaldas).
            PlaceProp(park.transform, "Assets/ModularLowpolyStreetsFree/Prefabs/Other/Bench_1.prefab", "Banca_Parque", spot, yaw, 0.9f);
        }

        // Flores y setos en los cuadrantes de pasto.
        string[] flowers = { "Flower 1 prefab A", "Flower 1 prefab C", "Flower 1 prefab E", "Flower 1 prefab G", "flower 2 prefab", "Bush 2 prefab" };
        System.Random rng = new System.Random(9911);
        for (int i = 0; i < 18; i++)
        {
            float x = NextFloat(rng, 1.8f, ParkSizeX / 2f - 0.8f) * (rng.Next(2) == 0 ? -1f : 1f);
            float z = NextFloat(rng, 1.8f, ParkSizeZ / 2f - 0.8f) * (rng.Next(2) == 0 ? -1f : 1f);
            Vector3 p = c + new Vector3(x, 0.1f, z);
            if (Vector2.Distance(new Vector2(x, z), Vector2.zero) < 3.2f) continue;
            GameObject flower = PlaceProp(park.transform, $"{CityPackDir}/Props/{flowers[i % flowers.Length]}.prefab",
                "Flor_Parque", p, NextFloat(rng, 0f, 360f), 0f, false);
            if (flower != null && flowers[i % flowers.Length].StartsWith("Flower")) flower.transform.localScale *= 2.2f;
        }

        // Faroles en las cuatro bocas de los caminos (con la luz del
        // presupuesto de siempre, no la del asset).
        Vector3[] lampSpots =
        {
            c + new Vector3(-1.8f, 0f, ParkSizeZ / 2f - 0.6f), c + new Vector3(ParkSizeX / 2f - 0.6f, 0f, -4.2f),
            c + new Vector3(ParkSizeX / 2f - 0.6f, 0f, 1.8f), c + new Vector3(-ParkSizeX / 2f + 0.6f, 0f, -1.8f),
        };
        foreach (Vector3 spot in lampSpots)
        {
            GameObject lamp = PlaceProp(park.transform, $"{CityPackDir}/Lamps/street lamp 2 prefab.prefab", "Farol_Parque", spot + Vector3.up * 0.1f, 0f, 3.8f);
            if (lamp != null) AddParkLampLight(lamp.transform, spot + Vector3.up * 3.7f);
        }

        PlaceProp(park.transform, $"{CityPackDir}/Props/trashcan prefab.prefab", "Tacho_Parque", c + new Vector3(1.6f, 0.1f, 3.4f), 0f, 1.0f);
        PlaceProp(park.transform, $"{CityPackDir}/Props/trashcan prefab.prefab", "Tacho_Parque", c + new Vector3(-1.6f, 0.1f, -3.4f), 180f, 1.0f);

        // Nombre del parque en un letrero de esquina.
        BuildStreetSign(park.transform, c + new Vector3(-ParkSizeX / 2f + 0.4f, 0f, ParkSizeZ / 2f - 0.4f), "PLAZA CONSTITUCIÓN", 0f);

        // v55b: el parque ahora es la Plaza Constitución (con la catedral).
        BuildPlazaConstitucion(park.transform);
    }

    private static void AddParkLampLight(Transform lamp, Vector3 position)
    {
        GameObject lightGO = new GameObject("Luz");
        lightGO.transform.SetParent(lamp);
        lightGO.transform.position = position;
        Light pointLight = lightGO.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.range = 12f;
        pointLight.intensity = 2.2f;
        pointLight.color = HexColor("#ffdca0");
        pointLight.shadows = LightShadows.None;
        pointLight.enabled = false;
        lightGO.AddComponent<BudgetedLight>();
        lightGO.isStatic = false;
    }

    // Mobiliario de vereda: paraderos de combi en la avenida, bancas, tachos,
    // hidrantes y un carrito de ambulante frente al Plaza Vea. Cada cosa se
    // pone solo si el sitio está en la vereda y libre.
    private static void BuildStreetFurniture(Transform parent)
    {
        GameObject root = new GameObject("Mobiliario_Urbano");
        root.transform.SetParent(parent);

        void Put(string path, string name, Vector3 pos, float yaw, float height)
        {
            if (IsOnRoadway(pos) || OverlapsPlaced(pos, 0.5f)) return;
            PlaceProp(root.transform, path, name, pos, yaw, height);
        }

        string props = CityPackDir + "/Props";

        // Paraderos en la Av. Giráldez (la fachada del paradero mira a la pista).
        Put($"{props}/Bus stop prefab.prefab", "Paradero", new Vector3(-24f, 0f, AvenueZ - 6.2f), 90f, 0f);
        Put($"{props}/Bus stop prefab.prefab", "Paradero", new Vector3(20f, 0f, AvenueZ + 6.2f), -90f, 0f);
        Put($"{props}/Bus stop pole prefabe.prefab", "Paradero_Poste", new Vector3(-2f + 40f, 0f, AvenueZ - 5.9f), 0f, 0f);

        // Carrito de ambulante (emoliente, raspadilla...) frente al Plaza Vea.
        Put($"{props}/StreetSellerStand prefab.prefab", "Carrito_Ambulante", new Vector3(-12f, 0f, 36.4f), 180f, 0f);
        Put($"{props}/StreetSellerStand prefab.prefab", "Carrito_Ambulante", new Vector3(8.2f, 0f, 24.6f), 0f, 0f);

        // Tachos e hidrantes a lo largo de la Calle Real.
        float[] zs = { -64f, -40f, -8f, 22f, 50f, 68f };
        for (int i = 0; i < zs.Length; i++)
        {
            float x = i % 2 == 0 ? 5.6f : -5.6f;
            Put($"{props}/trashcan prefab.prefab", "Tacho_Vereda", new Vector3(x, 0f, zs[i]), 0f, 1.0f);
            Put($"{props}/Hydrant prefab.prefab", "Hidrante", new Vector3(-x, 0f, zs[i] + 5f), 0f, 0.8f);
        }

        // Bancas en la vereda de la avenida.
        // Bancas a lo largo de la vereda (paralelas a la pista) y mirando a la
        // calle, pegadas al lado de las casas para no estorbar el paso.
        string bench = "Assets/ModularLowpolyStreetsFree/Prefabs/Other/Bench_1.prefab";
        Put(bench, "Banca_Vereda", new Vector3(-36f, 0f, AvenueZ + 6.5f), 180f, 0.9f);
        Put(bench, "Banca_Vereda", new Vector3(12f, 0f, AvenueZ - 6.5f), 0f, 0.9f);

        // Teléfono público y buzón, de los que todavía quedan en el centro.
        Put($"{props}/phone booth prefab.prefab", "Telefono_Publico", new Vector3(5.8f, 0f, 9.5f), -90f, 0f);
        Put($"{props}/Mail_box prefab.prefab", "Buzon", new Vector3(-5.8f, 0f, -26f), 90f, 0f);
    }

    // ---------------------------------------------------------------
    // Agua del río (Underwater Effect, "Brackish Water")
    // ---------------------------------------------------------------

    private const string RiverWaterSource = "Assets/Underwater-Effect/Materials/Water Materials/BrackishWaterMaterial.mat";

    // El agua del asset, sobre la misma malla del río que ya calza con el
    // cauce: el plano del asset mide 150 x 150 y habría que recortarlo; la
    // malla propia ya tiene exactamente el ancho y el largo del Mantaro.
    // "Brackish" es el agua turbia marrón verdosa: la del Mantaro de verdad.
    private static Material GetRiverWaterMaterial()
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(RiverWaterSource);
        if (source == null || source.shader == null || !source.shader.isSupported)
        {
            if (MissingAssetsLogged.Add(RiverWaterSource))
                Debug.LogWarning("[Kuntur] El agua del asset no está disponible; el río queda con el agua de antes.");
            return null;
        }

        string dir = ArtDir + "/Materiales";
        CreateFolderRecursive(dir);
        string path = dir + "/Rio_Mantaro_Agua.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(source);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = source.shader;
            mat.CopyPropertiesFromMaterial(source);
        }

        // La malla va de 0 a 1 en UV a lo largo de 200 m: sin repetir la
        // textura, se vería estirada como un chicle.
        // La malla del río pone la V en "anchos de río" recorridos, así que
        // la misma repetición sirve en rectas y curvas.
        Vector2 tiling = new Vector2(1.5f, 1.5f);
        foreach (string tex in new[] { "_MainTex", "_NormalMap", "_SurfaceNoise", "_FoamTex" })
            if (mat.HasProperty(tex)) mat.SetTextureScale(tex, tiling);

        // Olas de río, no de mar: bajitas y corriendo aguas abajo (-Z).
        if (mat.HasProperty("_Wave1")) mat.SetVector("_Wave1", new Vector4(0f, -1f, 0.06f, 5f));
        if (mat.HasProperty("_Wave2")) mat.SetVector("_Wave2", new Vector4(0.3f, -1f, 0.04f, 3f));
        if (mat.HasProperty("_Wave3")) mat.SetVector("_Wave3", new Vector4(-0.3f, -1f, 0.03f, 2.2f));
        if (mat.HasProperty("_WaveSpeed")) mat.SetFloat("_WaveSpeed", 1f);
        if (mat.HasProperty("_DepthMaxDistance")) mat.SetFloat("_DepthMaxDistance", 1.2f);
        if (mat.HasProperty("_Transparency")) mat.SetFloat("_Transparency", 0.85f);

        // Colores del Mantaro: el "Brackish" original es casi naranja y, sin
        // brillo, se leía como barro seco. Acá queda pardo verdoso (el río
        // baja cargado de sedimento), con el cielo reflejado cuando se lo mira
        // de costado y un brillo de sol que dice "esto es agua".
        void Col(string prop, Color c) { if (mat.HasProperty(prop)) mat.SetColor(prop, c); }
        // Verde agua con un dejo pardo: se reconoce como agua de río al
        // primer vistazo (el pardo puro parecía cemento) sin volverlo una
        // piscina azul.
        Col("_MainColor", new Color(0.34f, 0.50f, 0.47f, 1f));
        Col("_DeepColor", new Color(0.20f, 0.34f, 0.33f, 0.85f));
        Col("_ShoreColor", new Color(0.58f, 0.62f, 0.50f, 1f));
        Col("_HorizonColor", new Color(0.72f, 0.84f, 0.90f, 1f));
        Col("_CrestColor", new Color(0.78f, 0.75f, 0.64f, 1f));
        Col("_FoamColor", new Color(0.88f, 0.85f, 0.76f, 0.55f));
        if (mat.HasProperty("_Specular")) mat.SetFloat("_Specular", 0.75f);
        if (mat.HasProperty("_ReflectionStrength")) mat.SetFloat("_ReflectionStrength", 0.65f);
        if (mat.HasProperty("_FresnelIntensity")) mat.SetFloat("_FresnelIntensity", 1.6f);
        if (mat.HasProperty("_FresnelRamp")) mat.SetFloat("_FresnelRamp", 4f);
        if (mat.HasProperty("_CrestModifier")) mat.SetFloat("_CrestModifier", 3f);
        if (mat.HasProperty("_FoamDirection")) mat.SetVector("_FoamDirection", new Vector4(0f, -0.08f, 0f, 0f));

        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------------------------------------------------------
    // Faroles (POLYGON street lamp) — solo la forma; la luz sigue siendo la
    // del presupuesto de luces.
    // ---------------------------------------------------------------

    private const string StreetLampPrefab = CityPackDir + "/Lamps/street_lamp 1 prefab.prefab";

    // Devuelve la posición del foco si pudo poner el modelo, o null.
    private static Vector3? TryBuildAssetLampPost(Transform lamp, Vector3 basePosition, Vector3 dir)
    {
        GameObject prefab = LoadPrefab(StreetLampPrefab);
        if (prefab == null) return null;

        // El brazo del modelo apunta a su -Z: se gira para que apunte a la pista.
        Quaternion rotation = Quaternion.Euler(0f, Vector3.SignedAngle(Vector3.back, dir, Vector3.up), 0f);
        GameObject model = PlaceModel(prefab, lamp, "Poste_Modelo", basePosition, rotation, 6.9f, out Vector3 size);
        RemoveLights(model);

        // PlaceModel deja el CENTRO de la caja en basePosition, pero la caja
        // incluye el brazo: se corre medio largo hacia la pista para que el
        // poste quede plantado en basePosition y el brazo salga sobre la calle.
        model.transform.position = basePosition + dir * (size.z * 0.5f - 0.15f);
        SetStaticRecursive(model, true);

        // El foco queda en la punta del brazo, un poco bajo el tope.
        return basePosition + dir * (size.z - 0.35f) + Vector3.up * (size.y - 0.3f);
    }
}
