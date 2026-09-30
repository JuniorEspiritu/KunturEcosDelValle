using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// v53: animales del valle. Tres pastores alemanes por el camino a la chacra
// y un gatito en la puerta de la casa de Kuntur.
public static partial class KunturSceneBuilder
{
    private const string DogPrefabPath = "Assets/RSG_DogsPack/HDRP/Prefabs/P_GermanShepherd.prefab";
    private const string DogAnimDir = "Assets/RSG_DogsPack/HDRP/Animations";
    private const string DogTexDir = "Assets/RSG_DogsPack/HDRP/Textures";
    private const string CatPrefabPath = "Assets/Ladymito/Free_cat/Prefabs/cat.prefab";
    private const string CatModelPath = "Assets/Ladymito/Free_cat/Models/cat.fbx";

    // Por el camino de tierra de la chacra (x = 92), lejos de las casas.
    private static readonly Vector3[] DogHomes =
    {
        new Vector3(EastRoadX, 0f, -22f),
        new Vector3(EastRoadX + 1f, 0f, 38f),
        new Vector3(EastRoadX - 1f, 0f, 64f),
    };

    // Al lado del caminito de la casa, del lado contrario a la puerta (así
    // la E de la puerta y la del gato no se pisan).
    private static readonly Vector3 CatHome = new Vector3(-86.2f, 0f, -107.2f);

    private static void BuildAnimals(Transform parent, int interactableLayer)
    {
        GameObject root = new GameObject("Animales");
        root.transform.SetParent(parent);

        BuildDogs(root.transform);
        BuildCat(root.transform, interactableLayer);
        BuildChacraPeople(root.transform, interactableLayer);
    }

    // ---------------------------------------------------------------
    // v55: gente de la chacra (donde andan los perros)
    // ---------------------------------------------------------------
    // Dos parejas conversando al borde del camino de tierra y un vecino que
    // va y viene. El primero, Don Teófilo, es el dueño de los perros y te pide
    // limpiar el camino a la chacra (su zona propia, ver MissionDirector).
    private static void BuildChacraPeople(Transform parent, int layer)
    {
        GameObject root = new GameObject("Gente_Chacra");
        root.transform.SetParent(parent);
        System.Random rng = new System.Random(9292);

        Vector3 Ground(float x, float z) => new Vector3(x, TerrainHeightAt(x, z), z);

        // Entre el borde del camino (±3.5) y las casas de la otra orilla (±7.5).
        Vector3 a1 = Ground(EastRoadX + 5.2f, -17f);
        Vector3 b1 = Ground(EastRoadX + 6.1f, -16.2f);
        Vector3 a2 = Ground(EastRoadX - 5.3f, 68f);
        Vector3 b2 = Ground(EastRoadX - 6.2f, 68.8f);

        GameObject anselmo = BuildVillager(root.transform, "Chacarero_Teofilo", a1, HexColor("#b03a2e"), rng,
            PeopleDir + "/Prefabs/city/casual_Male_G.prefab");
        GameObject wife = BuildVillager(root.transform, "Chacarera_Juana", b1, HexColor("#1f7a5c"), rng,
            PeopleDir + "/Prefabs/elder/elder_Female_A.prefab");
        LinkVillagers(anselmo, wife, 0f);
        LinkVillagers(wife, anselmo, 3.4f);
        MakeMissionGiver(anselmo, "Don Teófilo", false, "CHACARERO DE AZAPAMPA · DUEÑO DE LOS PERROS", layer, "camino_chacra", "father");

        GameObject kid = BuildVillager(root.transform, "Chacra_Nino", a2, HexColor("#2e5aa8"), rng,
            PeopleDir + "/Prefabs/little_kids/little_boy_B.prefab");
        GameObject neighbor = BuildVillager(root.transform, "Chacra_Vecina", b2, HexColor("#7d4e24"), rng,
            PeopleDir + "/Prefabs/downtown/casual_Female_K.prefab");
        LinkVillagers(kid, neighbor, 1.2f);
        LinkVillagers(neighbor, kid, 4.4f);

        TryBuildAssetPedestrian(root.transform, "Caminante_Chacra",
            Ground(EastRoadX + 2.4f, -60f), Ground(EastRoadX + 2.4f, 20f), rng);
    }

    // ---------------------------------------------------------------
    // Perros
    // ---------------------------------------------------------------
    private static void BuildDogs(Transform parent)
    {
        GameObject prefab = LoadPrefab(DogPrefabPath);
        if (prefab == null) return;

        // Los clips de reposo del paquete vienen sin bucle: se les activa
        // (si no, el perro respira una vez y queda congelado).
        EnsureClipLoops(DogAnimDir + "/A_Idle_Breathing.fbx");
        EnsureClipLoops(DogAnimDir + "/A_Idle_Playing.fbx");

        AnimationClip idle = FirstClip(DogAnimDir + "/A_Idle_Breathing.fbx");
        AnimationClip walk = FirstClip(DogAnimDir + "/A_Walk.fbx");
        AnimationClip play = FirstClip(DogAnimDir + "/A_Idle_Playing.fbx");
        RuntimeAnimatorController controller = BuildAnimalController("Perro", idle, walk, play, play);

        Material fur = GetDogMaterial(false);
        Material furCutout = GetDogMaterial(true);
        AudioClip pant = LoadAudio("SFX_PerroJadeo");
        AudioClip bark = LoadAudio("SFX_PerroLadrido");

        for (int i = 0; i < DogHomes.Length; i++)
        {
            Vector3 home = DogHomes[i];
            GameObject dog = new GameObject($"Perro_{i + 1}");
            dog.transform.SetParent(parent);
            dog.transform.position = home;
            dog.transform.rotation = Quaternion.Euler(0f, i * 120f + 30f, 0f);

            GameObject model = PlaceModel(prefab, dog.transform, "Modelo", home, dog.transform.rotation, 1.08f, out _); // v55: más grandes
            RemoveColliders(model);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    bool cut = mats[m] != null && mats[m].name.Contains("Transparent");
                    mats[m] = cut ? furCutout : fur;
                }
                r.sharedMaterials = mats;
            }

            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.transform.GetChild(0).gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            AnimalWander wander = dog.AddComponent<AnimalWander>();
            SerializedObject so = new SerializedObject(wander);
            so.FindProperty("species").enumValueIndex = (int)AnimalWander.Species.Perro;
            so.FindProperty("home").vector3Value = home;
            so.FindProperty("wanderRadius").floatValue = 5.5f;
            so.FindProperty("walkSpeed").floatValue = 1.25f;
            so.FindProperty("animator").objectReferenceValue = animator;
            SerializedProperty voices = so.FindProperty("voiceClips");
            voices.arraySize = bark != null ? 1 : 0;
            if (bark != null) voices.GetArrayElementAtIndex(0).objectReferenceValue = bark;
            so.FindProperty("nearClip").objectReferenceValue = pant;
            so.ApplyModifiedProperties();

            AddMapIcon(dog.transform, home, HexColor("#c8a26a"), 1.4f);
            SetStaticRecursive(dog, false);
        }
    }

    private static Material GetDogMaterial(bool cutout)
    {
        string dir = ArtDir + "/Materiales";
        CreateFolderRecursive(dir);
        string path = dir + (cutout ? "/Perro_Pelaje_Recorte.mat" : "/Perro_Pelaje.mat");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(FindBestShader());
            AssetDatabase.CreateAsset(mat, path);
        }

        // El paquete trae materiales de HDRP (se ven rosados en este
        // proyecto): se arman de nuevo con sus mismas texturas.
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(DogTexDir + "/T_GermanShepherd_B.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(DogTexDir + "/T_GermanShepherd_N.png");
        mat.mainTexture = albedo;
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
        if (normal != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.15f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

        if (cutout && mat.HasProperty("_Mode"))
        {
            // Standard en modo "Cutout": pelos/pestañas con transparencia recortada.
            mat.SetFloat("_Mode", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = 2450;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------------------------------------------------------
    // Gato
    // ---------------------------------------------------------------
    private static void BuildCat(Transform parent, int interactableLayer)
    {
        GameObject prefab = LoadPrefab(CatPrefabPath);
        if (prefab == null) return;

        AnimationClip idle = NamedClip(CatModelPath, "idle");
        AnimationClip walk = NamedClip(CatModelPath, "walk");
        AnimationClip sit = NamedClip(CatModelPath, "sit");
        AnimationClip meow = NamedClip(CatModelPath, "miau");
        RuntimeAnimatorController controller = BuildAnimalController("Gato", idle, walk, sit, meow);

        GameObject cat = new GameObject("Gato_Casa");
        cat.transform.SetParent(parent);
        cat.transform.position = CatHome;
        cat.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        cat.layer = interactableLayer;

        GameObject model = PlaceModel(prefab, cat.transform, "Modelo", CatHome, cat.transform.rotation, 0.56f, out _); // v55: más grande
        RemoveColliders(model);

        Animator animator = model.GetComponentInChildren<Animator>();
        if (animator == null) animator = model.transform.GetChild(0).gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        // Trigger en la capa de interactuables: así aparece "E Saludar al gatito".
        SphereCollider col = cat.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.75f;
        col.center = new Vector3(0f, 0.35f, 0f);

        AnimalWander wander = cat.AddComponent<AnimalWander>();
        SerializedObject so = new SerializedObject(wander);
        so.FindProperty("species").enumValueIndex = (int)AnimalWander.Species.Gato;
        so.FindProperty("home").vector3Value = CatHome;
        so.FindProperty("wanderRadius").floatValue = 1.6f;   // ahí nomás, sin irse lejos
        so.FindProperty("walkSpeed").floatValue = 0.45f;
        so.FindProperty("idleTime").vector2Value = new Vector2(4f, 10f);
        so.FindProperty("extraChance").floatValue = 0.25f;
        so.FindProperty("animator").objectReferenceValue = animator;
        SerializedProperty voices = so.FindProperty("voiceClips");
        string[] meows = { "SFX_Gato1", "SFX_Gato2", "SFX_Gato3" };
        voices.arraySize = 0;
        foreach (string m in meows)
        {
            AudioClip clip = LoadAudio(m);
            if (clip == null) continue;
            voices.arraySize++;
            voices.GetArrayElementAtIndex(voices.arraySize - 1).objectReferenceValue = clip;
        }
        so.FindProperty("volume").floatValue = 0.8f;
        so.ApplyModifiedProperties();

        SetStaticRecursive(cat, false);
    }

    // ---------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------
    // Quieto / Caminar / Extra (jugar o sentarse) / Voz (ladrar o maullar).
    private static RuntimeAnimatorController BuildAnimalController(string name, AnimationClip idle, AnimationClip walk,
        AnimationClip extra, AnimationClip voice)
    {
        if (idle == null && walk == null) return null;

        string dir = ArtDir + "/Generated";
        CreateFolderRecursive(dir);
        string path = $"{dir}/{name}_Animator.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState idleState = machine.AddState("Idle");
        idleState.motion = idle != null ? idle : walk;
        machine.defaultState = idleState;
        machine.AddState("Walk").motion = walk != null ? walk : idle;
        machine.AddState("Extra").motion = extra != null ? extra : idleState.motion;
        machine.AddState("Voz").motion = voice != null ? voice : idleState.motion;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimationClip FirstClip(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && !clip.empty) return clip;
        return null;
    }

    private static AnimationClip NamedClip(string path, string contains)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && clip.name.EndsWith("|" + contains)) return clip;
        return null;
    }

    private static void EnsureClipLoops(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        bool changed = importer.clipAnimations == null || importer.clipAnimations.Length == 0;
        foreach (ModelImporterClipAnimation c in clips)
        {
            if (c.loopTime) continue;
            c.loopTime = true;
            changed = true;
        }
        if (!changed) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
}
