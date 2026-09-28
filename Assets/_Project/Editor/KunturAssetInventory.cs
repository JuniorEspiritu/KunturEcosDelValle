using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Herramienta de diagnóstico: mide los assets importados (tamaño real en
// metros, shaders, animaciones) y lo escribe en Logs/KunturInventario.txt.
// Sirve para colocar cada modelo a la escala correcta sin adivinar.
// Corre sola UNA vez por versión, o a mano desde Kuntur > Inventario de assets.
[InitializeOnLoad]
public static class KunturAssetInventory
{
    private const string Version = "inv-3";
    private const string Key = "Kuntur_AssetInventory";

    private static readonly string[] Folders =
    {
        "Assets/DenysAlmaral/CityPeople/Prefabs",
        "Assets/_ApocalypticVehiclesPack",
        "Assets/Tarbo-CITY-TrafficLights/Prefabs",
        "Assets/ModularLowpolyStreetsFree/Prefabs",
        "Assets/POLYGON city pack/Prefabs",
        "Assets/Underwater-Effect/Prefabs",
        "Assets/WaterWorks",
    };

    static KunturAssetInventory()
    {
        if (EditorPrefs.GetString(Key, "") == Version) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorPrefs.SetString(Key, Version);
            Run();
        };
    }

    [MenuItem("Kuntur/Inventario de assets")]
    public static void Run()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("pipeline: " + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ? "BUILT-IN" : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name));

        foreach (string folder in Folders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) { sb.AppendLine("## (no existe) " + folder); continue; }
            sb.AppendLine("## " + folder);

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                sb.AppendLine(Describe(path, prefab));
            }
        }

        sb.AppendLine("## Animaciones CityPeople");
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets/DenysAlmaral/CityPeople/Animations" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) continue;
            IEnumerable<string> states = controller.layers.SelectMany(l => l.stateMachine.states)
                .Select(s => s.state.name + "=" + (s.state.motion != null ? s.state.motion.name : "null"));
            IEnumerable<string> parameters = controller.parameters.Select(p => p.name + ":" + p.type);
            sb.AppendLine($"{path} | estados: {string.Join(", ", states)} | params: {string.Join(", ", parameters)}");
        }
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/DenysAlmaral/CityPeople/Animations" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    sb.AppendLine($"clip {clip.name} ({clip.length:0.00}s, loop={clip.isLooping}, humanoid={clip.humanMotion}) <- {path}");
            }
        }

        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/KunturInventario.txt", sb.ToString());
        Debug.Log("[Kuntur] Inventario de assets escrito en Logs/KunturInventario.txt");
    }

    private static string Describe(string path, GameObject prefab)
    {
        // Se abre el prefab aparte (no en la escena), así no ensucia la escena abierta.
        GameObject instance = PrefabUtility.LoadPrefabContents(path);
        try
        {
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            Bounds? bounds = null;
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
                bounds = Add(bounds, filter.sharedMesh, filter.transform);
            foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                bounds = Add(bounds, skin.sharedMesh, skin.transform);

            HashSet<string> shaders = new HashSet<string>();
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    shaders.Add(m == null ? "NULL" : (m.shader == null ? "sin-shader" : m.shader.name + (m.shader.isSupported ? "" : "(NO SOPORTADO)")) + (m != null ? "[" + m.name + "]" : ""));

            Animator animator = instance.GetComponentInChildren<Animator>(true);
            string anim = animator == null ? "" :
                $" anim={(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "sin-controller")} avatar={(animator.avatar != null ? animator.avatar.name + (animator.avatar.isHuman ? "(humano)" : "") : "no")}";

            string lights = instance.GetComponentsInChildren<Light>(true).Length > 0 ? $" luces={instance.GetComponentsInChildren<Light>(true).Length}" : "";
            string colliders = $" colliders={instance.GetComponentsInChildren<Collider>(true).Length}";
            string children = string.Join(",", instance.GetComponentsInChildren<Transform>(true).Skip(1).Take(14).Select(t => t.name));
            string size = bounds.HasValue ? $"tam=({bounds.Value.size.x:0.00},{bounds.Value.size.y:0.00},{bounds.Value.size.z:0.00}) centro=({bounds.Value.center.x:0.00},{bounds.Value.center.y:0.00},{bounds.Value.center.z:0.00})" : "tam=?";
            return $"{Path.GetFileNameWithoutExtension(path)} | {size} | escalaRaiz={instance.transform.localScale}{anim}{lights}{colliders} | shaders: {string.Join("; ", shaders)} | hijos: {children}";
        }
        catch (System.Exception e)
        {
            return $"{path} | ERROR {e.Message}";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    private static Bounds? Add(Bounds? result, Mesh mesh, Transform t)
    {
        if (mesh == null) return result;
        Bounds b = mesh.bounds;
        Matrix4x4 m = t.localToWorldMatrix;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x,
                (i & 2) == 0 ? b.min.y : b.max.y,
                (i & 4) == 0 ? b.min.z : b.max.z);
            Vector3 w = m.MultiplyPoint3x4(corner);
            if (result.HasValue) { Bounds r = result.Value; r.Encapsulate(w); result = r; }
            else result = new Bounds(w, Vector3.zero);
        }
        return result;
    }
}
