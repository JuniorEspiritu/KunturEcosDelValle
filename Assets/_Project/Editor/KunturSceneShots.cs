using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Diagnóstico: abre la escena del juego aparte (sin tocar la que está
// abierta en el editor) y saca fotos desde varios puntos del pueblo a
// Logs/Shots. Sirve para revisar cómo quedaron los assets sin darle Play.
[InitializeOnLoad]
public static class KunturSceneShots
{
    private const string Version = "shots-23-v56b";
    private const string Key = "Kuntur_SceneShots";

    static KunturSceneShots()
    {
        if (EditorPrefs.GetString(Key, "") == Version) return;
        // Doble espera: así corre DESPUÉS de que el constructor automático
        // rehaga la escena (que también espera un delayCall).
        EditorApplication.delayCall += WaitAndRun;
    }

    // v56: espera a que el constructor automático termine de rehacer la
    // escena (si no, las fotos salían de la escena vieja).
    private static int waits;
    private static void WaitAndRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        bool sceneReady = EditorPrefs.GetString(KunturAutoBuilder.VersionKey, "") == KunturAutoBuilder.SceneVersion;
        if ((EditorApplication.isCompiling || EditorApplication.isUpdating || !sceneReady) && waits++ < 4000)
        {
            EditorApplication.delayCall += WaitAndRun;
            return;
        }
        // Una vuelta más: el constructor marca la versión y construye en la
        // misma llamada, así que para acá ya terminó.
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            EditorPrefs.SetString(Key, Version);
            Run();
        };
    }

    private static readonly (string name, Vector3 from, Vector3 to)[] Views =
    {
        ("01_calle_real_norte", new Vector3(1.5f, 1.9f, -14f), new Vector3(1f, 3f, 20f)),
        ("02_calle_real_sur", new Vector3(-1.5f, 1.9f, 40f), new Vector3(0f, 3f, 0f)),
        ("03_centro_aereo", new Vector3(45f, 55f, -55f), new Vector3(0f, 0f, 5f)),
        ("04_giraldez", new Vector3(-16f, 2.2f, 27f), new Vector3(10f, 2f, 31f)),
        ("05_rio_puente", new Vector3(50f, 3.5f, 8f), new Vector3(62f, -1f, 40f)),
        ("06_parque", new Vector3(6f, 4.5f, -18f), new Vector3(18f, 0f, -32f)),
        ("07_otra_orilla", new Vector3(66f, 6f, 30f), new Vector3(82f, 0f, 30f)),
        ("08_plaza_vea", new Vector3(-10f, 3f, 24f), new Vector3(-22f, 3f, 44f)),
        ("09_rio_cerca", new Vector3(52f, 1.5f, 12f), new Vector3(60f, -1.4f, 0f)),
        ("10_aereo_todo", new Vector3(20f, 260f, -250f), new Vector3(10f, 0f, -10f)),
        ("11_rio_curva", new Vector3(70f, 16f, 120f), new Vector3(100f, -1f, 190f)),
        ("12_semaforo", new Vector3(-9f, 2.2f, 17f), new Vector3(2f, 3f, 30f)),
        ("13_personas", new Vector3(-1.5f, 1.8f, -2f), new Vector3(-6f, 1.2f, 4f)),
        ("14_bodega", new Vector3(41f, 2.2f, 17f), new Vector3(44.4f, 2f, 5f)),
        ("15_rio_orilla", new Vector3(49f, 2.6f, -20f), new Vector3(64f, -1.2f, 0f)),
        ("16_camino_chacra", new Vector3(92f, 2.2f, 0f), new Vector3(90f, 0f, 40f)),
        ("17_casa_kuntur", new Vector3(-80f, 3f, -104f), new Vector3(-90f, 1.5f, -111f)),
        ("18_parque_gente", new Vector3(16.4f, 1.6f, -30.7f), new Vector3(13.85f, 0.8f, -29.1f)),
        ("20_parque_bancas", new Vector3(16.5f, 1.9f, -33.2f), new Vector3(13.85f, 0.7f, -29.1f)),
        ("21_parque_bancas2", new Vector3(17.45f, 1.9f, -29.8f), new Vector3(17.45f, 0.7f, -33.9f)),
        // Retratos de Kuntur para usarlos de referencia al rehacer el personaje.
        ("26_kuntur_frente", new Vector3(-84.6f, 1.15f, -105.5f), new Vector3(-84.6f, 0.95f, -108.5f)),
        ("27_kuntur_costado", new Vector3(-81.6f, 1.15f, -108.5f), new Vector3(-84.6f, 0.95f, -108.5f)),
        ("28_kuntur_espalda", new Vector3(-84.6f, 1.15f, -111.5f), new Vector3(-84.6f, 0.95f, -108.5f)),
        ("22_puente_rojo", new Vector3(50f, 3.5f, 14f), new Vector3(64f, 2.5f, 30f)),
        // v53: el río ancho y el puente largo, desde arriba de la orilla.
        ("29_rio_ancho", new Vector3(44f, 9f, 62f), new Vector3(66f, -1f, 24f)),
        // v53: desde el mirador, la calle bajando hacia el pueblo.
        ("30_mirador_bajada", new Vector3(0f, 17.9f, -203f), new Vector3(6f, 0.5f, -110f)),
        ("31_subida_desde_abajo", new Vector3(0f, 2.2f, -126f), new Vector3(0f, 12f, -200f)),
        // v55: heladerías, gente de la chacra y la subida vista de costado.
        ("36_heladeria_arequipa", new Vector3(-80f, 2.2f, -118f), new Vector3(-71.3f, 2f, -111f)),
        ("37_heladeria_mirador", new Vector3(0f, 18f, -194f), new Vector3(9.6f, 17.5f, -202f)),
        ("38_gente_chacra", new Vector3(88f, 2f, -10f), new Vector3(96f, 1f, -18f)),
        ("39_subida_costado", new Vector3(-45f, 14f, -150f), new Vector3(0f, 7f, -172f)),
        // v55b: Plaza Constitución con la catedral y las letras HUANCAYO.
        ("40_plaza_constitucion", new Vector3(17.4f, 1.9f, -46f), new Vector3(17.4f, 5f, -2f)),
        ("41_catedral", new Vector3(8f, 7f, -24f), new Vector3(17.4f, 8f, -2f)),
        ("42_cebras_arequipa", new Vector3(-74f, 3f, -92f), new Vector3(-80f, 0f, -100f)),
        ("32_perros_chacra", new Vector3(86f, 2.4f, 30f), new Vector3(92f, 0.4f, 38f)),
        ("34_cerro_aereo", new Vector3(-60f, 70f, -175f), new Vector3(0f, 0f, -175f)),
        ("35_cerro_lado", new Vector3(-70f, 8f, -175f), new Vector3(0f, 5f, -175f)),
        ("33_gato_casa", new Vector3(-84.2f, 1.1f, -105.6f), new Vector3(-86.2f, 0.2f, -107.2f)),
        ("23_monton_plaza", new Vector3(-22f, 2.2f, 35.5f), new Vector3(-27f, 0.2f, 40.5f)),
        ("24_monton_parque", new Vector3(7f, 1.8f, -24f), new Vector3(11.25f, 0.3f, -27.3f)),
        ("25_monton_calle", new Vector3(2f, 1.8f, -52f), new Vector3(5.4f, 0.2f, -58.5f)),
        ("19_calle_gente", new Vector3(2f, 2f, -40f), new Vector3(0f, 1.5f, -60f)),
        // v56: tuk tuk, calles cerradas, calle del mirador, puente peatonal y casas nuevas.
        ("50_tuktuk", new Vector3(-86.5f, 2.3f, -119f), new Vector3(-81.9f, 0.9f, -113.5f)),
        ("51_tuktuk_costado", new Vector3(-76.5f, 1.8f, -113.5f), new Vector3(-81.9f, 0.9f, -113.5f)),
        ("52_calle_mirador", new Vector3(-6f, 21f, -196f), new Vector3(14f, 16f, -212f)),
        ("53_mirador_aereo", new Vector3(-5f, 70f, -140f), new Vector3(-4f, 12f, -190f)),
        ("54_puente_peatonal", new Vector3(42f, 7f, -58f), new Vector3(66f, 0f, -72f)),
        ("55_casas_jiron", new Vector3(-21f, 14f, -33f), new Vector3(-21f, 0f, -12f)),
        ("56_circunvalacion_norte", new Vector3(-24f, 22f, 92f), new Vector3(-24f, 0f, 116f)),
        ("57_aereo_pueblo", new Vector3(-20f, 230f, -150f), new Vector3(-20f, 0f, -25f)),
        ("58_bodega", new Vector3(30f, 3f, -6f), new Vector3(42f, 1.5f, 4f)),
        ("59_circunvalacion_sur", new Vector3(-60f, 18f, -104f), new Vector3(-40f, 0f, -125f)),
    };

    [MenuItem("Kuntur/Fotos de la escena")]
    public static void Run()
    {
        string scenePath = "Assets/_Project/Scenes/Exploracion.unity";
        if (!File.Exists(scenePath)) { Debug.LogWarning("[Kuntur] Fotos: no existe la escena."); return; }

        Directory.CreateDirectory("Logs/Shots");
        Scene scene = EditorSceneManager.OpenPreviewScene(scenePath);
        try
        {
            // En el editor los Animator no corren: sin esto las personas
            // saldrían en pose de T. Se "posa" cada una con su primer clip.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                {
                    if (animator.runtimeAnimatorController == null) continue;
                    AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                    if (clips.Length > 0) clips[clips.Length - 1].SampleAnimation(animator.gameObject, 0.35f);
                }
            }

            // El agua del río arma su malla al arrancar: acá se la fuerza.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb.GetType().Name != "RiverWater") continue;
                    var method = mb.GetType().GetMethod("BuildGrid", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    method?.Invoke(mb, null);
                }
            }

            GameObject camGO = new GameObject("FotoCam");
            SceneManager.MoveGameObjectToScene(camGO, scene);
            Camera cam = camGO.AddComponent<Camera>();
            cam.scene = scene;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 900f;
            cam.depthTextureMode = DepthTextureMode.Depth;
            int soloMapa = LayerMask.NameToLayer("SoloMapa");
            if (soloMapa >= 0) cam.cullingMask &= ~(1 << soloMapa);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.75f, 0.88f);

            RenderTexture rt = new RenderTexture(1280, 720, 24);
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            foreach (var view in Views)
            {
                camGO.transform.position = view.from;
                camGO.transform.LookAt(view.to);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"Logs/Shots/{view.name}.jpg", tex.EncodeToJPG(85));
            }
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log($"[Kuntur] Fotos de la escena listas en Logs/Shots ({Views.Length}).");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Kuntur] Fotos de la escena fallaron: " + e);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
