using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// v53: el Kuntur nuevo, rigueado en Mixamo, con sus 12 animaciones.
//
// Las animaciones de Mixamo vinieron en gris (Mixamo no guarda la textura),
// así que el color se le devuelve acá: la textura original de Tripo (la del
// GLB con el chullo, el poncho y el collar) se pinta sobre la malla, que es
// la misma que usó Mixamo, con las mismas UV.
public static partial class KunturSceneBuilder
{
    private const float Kuntur2Height = 1.6f;

    // Velocidades del jugador (SimpleThirdPersonController). El blend tree
    // usa los mismos m/s, así la mezcla caminar/correr casa con lo que se ve.
    private const float K2WalkSpeed = 4f;
    private const float K2RunSpeed = 8.5f;
    private const float K2SwimSpeed = 1.8f;

    private static KunturVisualParts? TryBuildKuntur2(Transform visual, Transform player)
    {
        string modelPath = KunturModelPostprocessor.Kuntur2ModelPath;
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (source == null || source.GetComponentInChildren<SkinnedMeshRenderer>() == null) return null;

        // Si Unity lo importó antes de que existieran estos ajustes (Humanoid,
        // o sin clips), se le obliga a reimportar TODA la carpeta.
        ForceKuntur2Import();
        source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (source == null) return null;

        RuntimeAnimatorController controller = BuildKuntur2Controller();
        if (controller == null)
        {
            Debug.LogWarning("[Kuntur] Encontré el Kuntur de Mixamo pero no sus animaciones; uso el cóndor anterior.");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, visual);
        if (instance == null) instance = Object.Instantiate(source, visual, false);
        instance.name = "Modelo_Kuntur";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity; // Mixamo ya lo deja mirando hacia +Z
        instance.transform.localScale = Vector3.one;

        foreach (Collider col in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
        foreach (Camera stray in instance.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(stray.gameObject);
        foreach (Light stray in instance.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(stray.gameObject);

        NormalizeKunturModel(instance, visual);
        ApplyKuntur2Material(instance);

        foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            // La cadera se mueve bastante en recoger/nadar: con los límites
            // del bind pose Unity lo ocultaba al mirar de cerca.
            skin.updateWhenOffscreen = true;
            skin.quality = SkinQuality.Bone4;
        }

        Animator animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false; // al personaje lo mueve el CharacterController
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        KunturMixamoAnimator driver = instance.AddComponent<KunturMixamoAnimator>();
        SerializedObject so = new SerializedObject(driver);
        so.FindProperty("player").objectReferenceValue = player.GetComponent<SimpleThirdPersonController>();
        so.FindProperty("hips").objectReferenceValue = FindChildByName(instance.transform, "mixamorig:Hips");
        so.ApplyModifiedProperties();

        // Nadar más lento que caminar: el río se cruza en ~12 s.
        SimpleThirdPersonController controllerScript = player.GetComponent<SimpleThirdPersonController>();
        if (controllerScript != null)
        {
            SerializedObject pso = new SerializedObject(controllerScript);
            pso.FindProperty("swimSpeed").floatValue = K2SwimSpeed;
            pso.FindProperty("swimSprintSpeed").floatValue = K2SwimSpeed * 1.45f;
            pso.ApplyModifiedProperties();
        }

        Debug.Log("[Kuntur] Kuntur de Mixamo listo: 13 animaciones y textura original.");
        return new KunturVisualParts { root = visual, rigged = true };
    }

    private static void ForceKuntur2Import()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { KunturModelPostprocessor.Kuntur2Dir });
        var pending = new System.Collections.Generic.List<string>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            string file = System.IO.Path.GetFileNameWithoutExtension(path).Replace("Kuntur2_", "");
            string clipName = KunturModelPostprocessor.Kuntur2ClipName(file);
            bool clipOk = clipName == null || HasClip(path, clipName);
            if (importer.animationType == ModelImporterAnimationType.Generic && clipOk) continue;

            pending.Add(path);
        }

        if (pending.Count > 0)
        {
            // ForceUpdate: sin eso Unity ve que el archivo no cambió y no lo
            // vuelve a pasar por KunturModelPostprocessor.
            Debug.Log($"[Kuntur] Reimportando {pending.Count} FBX del Kuntur de Mixamo (tarda un par de minutos)...");
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in pending)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();
        }

        // Texturas: se ajustan acá (y no con un postprocesador de texturas,
        // que obligaría a Unity a reimportar todas las imágenes del proyecto).
        foreach (string tex in new[] { "Kuntur2_Color.jpg", "Kuntur2_Normal.png" })
        {
            string path = KunturModelPostprocessor.Kuntur2Dir + "/" + tex;
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            bool isNormal = tex.Contains("Normal");
            TextureImporterType type = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType == type && ti.maxTextureSize == 4096) continue;
            ti.textureType = type;
            ti.sRGBTexture = !isNormal;
            ti.maxTextureSize = 4096; // es el protagonista: se le ve de cerca todo el juego
            ti.SaveAndReimport();
        }
    }

    private static bool HasClip(string path, string clipName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip c && c.name == clipName && !c.empty) return true;
        return false;
    }

    private static AnimationClip K2Clip(string file)
    {
        string path = $"{KunturModelPostprocessor.Kuntur2Dir}/Kuntur2_{file}.fbx";
        string name = KunturModelPostprocessor.Kuntur2ClipName(file);
        AnimationClip fallback = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__") || clip.empty) continue;
            if (clip.name == name) return clip;
            fallback = clip;
        }
        if (fallback == null) Debug.LogWarning($"[Kuntur] Falta la animación '{file}' (Kuntur2_{file}.fbx).");
        return fallback;
    }

    // ---------------------------------------------------------------
    // Material: la textura original sobre la malla de Mixamo
    // ---------------------------------------------------------------
    private static void ApplyKuntur2Material(GameObject instance)
    {
        string dir = KunturModelPostprocessor.Kuntur2Dir;
        Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Kuntur2_Color.jpg");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Kuntur2_Normal.png");
        if (color == null)
        {
            Debug.LogWarning("[Kuntur] No encontré Kuntur2_Color.jpg: el Kuntur nuevo se va a ver gris.");
            return;
        }

        string path = dir + "/Kuntur2_Material.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(FindBestShader());
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != FindBestShader())
        {
            material.shader = FindBestShader();
        }

        material.mainTexture = color;
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", color);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);

        if (normal != null && material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normal);
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
        }

        // Plumas y lana: nada de brillo de plástico.
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.18f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = renderer.sharedMaterials;
            if (mats == null || mats.Length == 0) mats = new Material[1];
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            renderer.sharedMaterials = mats;
        }
    }

    // ---------------------------------------------------------------
    // Animator: un estado por animación; KunturMixamoAnimator elige cuál
    // ---------------------------------------------------------------
    private static RuntimeAnimatorController BuildKuntur2Controller()
    {
        AnimationClip idle = K2Clip("estarquieto");
        AnimationClip walk = K2Clip("caminar");
        AnimationClip run = K2Clip("correr");
        if (idle == null || walk == null || run == null) return null;

        string dir = ArtDir + "/Generated";
        CreateFolderRecursive(dir);
        string path = dir + "/Kuntur2_Animator.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        // Quieto -> caminar -> correr según la velocidad real (m/s). Los
        // pasos de Mixamo son de un cóndor de 1.6 m, más cortos que lo que
        // avanza el jugador: se aceleran un poco (timeScale) para que los
        // pies no patinen tanto sin que se vea frenético.
        AnimatorState locomotion = controller.CreateBlendTreeInController(KunturMixamoAnimator.StateLocomotion, out BlendTree loco);
        loco.blendType = BlendTreeType.Simple1D;
        loco.blendParameter = "Speed";
        loco.useAutomaticThresholds = false;
        loco.AddChild(idle, 0f);
        loco.AddChild(walk, K2WalkSpeed);
        loco.AddChild(run, K2RunSpeed);
        ChildMotion[] lc = loco.children;
        lc[1].timeScale = 2.5f;
        lc[2].timeScale = 2.1f;
        loco.children = lc;
        machine.defaultState = locomotion;

        // En el río: flotar quieto (nadar1) o nadar hacia la otra orilla (nadar2).
        AnimatorState swim = controller.CreateBlendTreeInController(KunturMixamoAnimator.StateSwim, out BlendTree sw);
        sw.blendType = BlendTreeType.Simple1D;
        sw.blendParameter = "Speed";
        sw.useAutomaticThresholds = false;
        AnimationClip floatClip = K2Clip("nadar1");
        AnimationClip swimClip = K2Clip("nadar2");
        if (floatClip != null) sw.AddChild(floatClip, 0f);
        if (swimClip != null) sw.AddChild(swimClip, K2SwimSpeed);
        if (swimClip != null)
        {
            ChildMotion[] sc = sw.children;
            sc[sc.Length - 1].timeScale = 2f;
            sw.children = sc;
        }

        AddK2State(machine, KunturMixamoAnimator.StateJump, K2Clip("saltar"), 1.1f);
        AddK2State(machine, KunturMixamoAnimator.StateTalk, K2Clip("hablar1"), 1f);
        AddK2State(machine, KunturMixamoAnimator.StateTalkYes, K2Clip("hablar2"), 1f);
        AddK2State(machine, KunturMixamoAnimator.StatePickup, K2Clip("recojer"), 1.8f);
        AddK2State(machine, KunturMixamoAnimator.StateSample, K2Clip("recojer2"), 1.3f);
        AddK2State(machine, KunturMixamoAnimator.StateSad, K2Clip("triste"), 1f);
        AddK2State(machine, KunturMixamoAnimator.StateDance, K2Clip("baile"), 1f);
        AddK2State(machine, KunturMixamoAnimator.StateGreet, K2Clip("saludo"), 1f);

        // v56: subir al vehículo (acelerada si dura más de 2.2 s, para no
        // esperar) y manejar sentado.
        AnimationClip enterCar = K2Clip("subircarro");
        if (enterCar != null)
            AddK2State(machine, KunturMixamoAnimator.StateEnterCar, enterCar,
                Mathf.Max(1f, enterCar.length / KunturMixamoAnimator.MaxEnterCarSeconds));
        AddK2State(machine, KunturMixamoAnimator.StateDrive, K2Clip("conducir"), 1f);

        // El "movimiento" de los 15 s vuelve solo a quedarse quieto.
        AnimatorState idleLong = AddK2State(machine, KunturMixamoAnimator.StateIdleVariant, K2Clip("movimiento"), 1f);
        if (idleLong != null)
        {
            AnimatorStateTransition back = idleLong.AddTransition(locomotion);
            back.hasExitTime = true;
            back.exitTime = 0.92f;
            back.duration = 0.25f;
            back.hasFixedDuration = true;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimatorState AddK2State(AnimatorStateMachine machine, string name, AnimationClip clip, float speed)
    {
        if (clip == null) return null;
        AnimatorState state = machine.AddState(name);
        state.motion = clip;
        state.speed = speed;
        state.writeDefaultValues = true;
        return state;
    }
}
