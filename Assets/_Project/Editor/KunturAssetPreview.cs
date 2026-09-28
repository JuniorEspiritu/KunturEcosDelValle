using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Diagnóstico: saca fotos de los prefabs importados desde tres lados (desde
// +X, desde +Z y en diagonal desde arriba) y las guarda en Logs/Previews.
// Sirve para saber hacia dónde mira cada modelo (la trompa del carro, la
// fachada del edificio) antes de ponerlo en el pueblo, sin adivinar.
[InitializeOnLoad]
public static class KunturAssetPreview
{
    private const string Version = "prev-3";
    private const string Key = "Kuntur_AssetPreview";

    // v53: los vehículos, el perro y el gato nuevos (para saber hacia dónde
    // miran antes de ponerlos a andar por el pueblo).
    private static readonly string[] Prefabs =
    {
        "Assets/ARCADE - FREE Racing Car/Prefabs (Meshes Only)/Free Racing Car.prefab",
        "Assets/Polyeler/Simple Retro Car/Prefabs/Simple Retro Car.prefab",
        "Assets/Ukraine_only_truck/Prefabs_With_Colliders/cars_colliiders/car/car_1/car_1_blue.prefab",
        "Assets/Ukraine_only_truck/Prefabs_With_Colliders/cars_colliiders/van/van_1/van_1_black.prefab",
        "Assets/Ukraine_only_truck/Prefabs_With_Colliders/cars_colliiders/bus/bus_1/bus_1_yellow.prefab",
        "Assets/Ukraine_only_truck/Prefabs_With_Colliders/cars_colliiders/truck/truck_1/truck_1_blue.prefab",
        "Assets/Ukraine_only_truck/Prefabs_With_Colliders/cars_colliiders/truck/truck_3/truck_3_red.prefab",
        "Assets/Ukraine_only_truck/Prefabs_With_Colliders/cars_colliiders/truck/truck_7/truck_6_green.prefab",
        "Assets/RSG_DogsPack/HDRP/Prefabs/P_GermanShepherd.prefab",
        "Assets/Ladymito/Free_cat/Prefabs/cat.prefab",
    };

    static KunturAssetPreview()
    {
        if (EditorPrefs.GetString(Key, "") == Version) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorPrefs.SetString(Key, Version);
            Run();
        };
    }

    [MenuItem("Kuntur/Fotos de assets")]
    public static void Run()
    {
        string outDir = "Logs/Previews";
        Directory.CreateDirectory(outDir);
        int done = 0;

        foreach (string path in Prefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("[Kuntur] Preview: no existe " + path); continue; }

            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
                bool any = false;
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { bounds = r.bounds; any = true; }
                    else bounds.Encapsulate(r.bounds);
                }

                GameObject lightGO = new GameObject("Luz");
                SceneManager.MoveGameObjectToScene(lightGO, scene);
                Light light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                lightGO.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

                GameObject camGO = new GameObject("Cam");
                SceneManager.MoveGameObjectToScene(camGO, scene);
                Camera cam = camGO.AddComponent<Camera>();
                cam.scene = scene;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
                cam.orthographic = true;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 5000f;

                float extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                cam.orthographicSize = extent * 1.15f;
                float distance = extent * 6f + 5f;

                // Vista 1: desde +X mirando a -X (el +Z del modelo queda a la IZQUIERDA de la foto).
                // Vista 2: desde +Z mirando a -Z (se ve la cara +Z de frente).
                // Vista 3: diagonal desde arriba (+X,+Y,+Z).
                Vector3[] dirs = { Vector3.right, Vector3.forward, new Vector3(1f, 0.8f, 1f).normalized };
                string[] tags = { "desdeX", "desdeZ", "diag" };

                RenderTexture rt = new RenderTexture(384, 384, 24);
                Texture2D tex = new Texture2D(384 * 3, 384, TextureFormat.RGB24, false);
                for (int v = 0; v < 3; v++)
                {
                    camGO.transform.position = bounds.center + dirs[v] * distance;
                    camGO.transform.LookAt(bounds.center);
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 384, 384), 384 * v, 0);
                }
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;

                File.WriteAllBytes($"{outDir}/{Path.GetFileNameWithoutExtension(path)}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
                done++;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Kuntur] Preview falló en {path}: {e.Message}");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        Debug.Log($"[Kuntur] Fotos de assets: {done} en {outDir}");
    }
}
