#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Solo editor: saca las fotos para la presentación (Canva). La crea
// KunturCanvaShots al entrar a Play. Fotos del mundo en 4K SIN interfaz
// (se renderiza la cámara principal a una textura, así el HUD no sale) y
// algunas capturas de pantalla CON interfaz (menú, HUD, mapa, tutorial).
public class KunturCanvaCapture : MonoBehaviour
{
    public const string OutDir = "Logs/Canva";
    public static bool Done;

    private const int W = 3840;
    private const int H = 2160;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Done = false;
    }

    private void Start() => StartCoroutine(Run());

    private static void Log(string s) => Debug.Log("[Kuntur] Canva: " + s);

    private IEnumerator Run()
    {
        Directory.CreateDirectory(OutDir);
        yield return new WaitForSeconds(3.5f);
        ScreenCapture.CaptureScreenshot($"{OutDir}/ui_01_menu.png", 2);
        yield return new WaitForSeconds(1f);

        var menu = FindAnyObjectByType<MenuController>();
        if (menu != null) menu.ContinueGame();

        float waited = 0f;
        while ((SimpleThirdPersonController.Instance == null || SceneManager.GetActiveScene().name != "Exploracion") && waited < 30f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(8f);
        if (TutorialUI.Showing && TutorialUI.Instance != null) TutorialUI.Instance.Close();

        SetHour(10.2f);
        yield return new WaitForSeconds(1f);

        // Interfaz: HUD con la misión.
        Place(new Vector3(-6f, 0f, 30f), 0f);
        yield return new WaitForSeconds(2f);
        ScreenCapture.CaptureScreenshot($"{OutDir}/ui_02_hud.png", 2);
        yield return new WaitForSeconds(1f);

        // Mapa completo (M).
        var map = FindAnyObjectByType<MapSystem>();
        if (map != null)
        {
            map.ToggleFullMap();
            yield return new WaitForSeconds(1.5f);
            ScreenCapture.CaptureScreenshot($"{OutDir}/ui_03_mapa.png", 2);
            yield return new WaitForSeconds(1f);
            map.ToggleFullMap();
            yield return new WaitForSeconds(0.5f);
        }

        // Pausa.
        var pause = FindAnyObjectByType<PauseMenu>();
        if (pause != null)
        {
            pause.Open();
            yield return new WaitForSecondsRealtime(1.2f);
            ScreenCapture.CaptureScreenshot($"{OutDir}/ui_04_pausa.png", 2);
            yield return new WaitForSecondsRealtime(1f);
            pause.Close();
            yield return new WaitForSeconds(0.5f);
        }

        // Tutorial (página del tuk tuk) delante de la casa.
        Place(new Vector3(-84.6f, 0f, -108.5f), 0f);
        yield return new WaitForSeconds(1.5f);
        if (TutorialUI.Instance != null)
        {
            TutorialUI.Instance.Open();
            yield return new WaitForSecondsRealtime(2f);
            ScreenCapture.CaptureScreenshot($"{OutDir}/ui_05_tutorial.png", 2);
            yield return new WaitForSecondsRealtime(0.8f);
            GoToTutorialPage("tuk");
            yield return new WaitForSecondsRealtime(1f);
            ScreenCapture.CaptureScreenshot($"{OutDir}/ui_06_tutorial_tuktuk.png", 2);
            yield return new WaitForSecondsRealtime(0.8f);
            TutorialUI.Instance.Close();
            yield return new WaitForSeconds(1f);
        }

        // --- Fotos del mundo (sin interfaz) ---

        // Retratos de Kuntur frente a su casa.
        Place(new Vector3(-84.6f, 0f, -108.5f), 15f);
        yield return new WaitForSeconds(2f);
        Photo("p01_kuntur_frente", new Vector3(-83.7f, 1.25f, -105.9f), new Vector3(-84.6f, 1.0f, -108.5f), 45f);
        Photo("p02_kuntur_heroe", new Vector3(-83.2f, 0.45f, -106.2f), new Vector3(-84.6f, 1.25f, -108.6f), 55f);
        Photo("p03_kuntur_casa", new Vector3(-77.8f, 1.7f, -103.2f), new Vector3(-87f, 1.2f, -110f), 55f);
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayGreet();
        yield return new WaitForSeconds(0.9f);
        Photo("p04_kuntur_saludo", new Vector3(-83.9f, 1.3f, -105.6f), new Vector3(-84.6f, 1.05f, -108.5f), 48f);
        yield return new WaitForSeconds(0.6f);
        Photo("p04b_kuntur_saludo", new Vector3(-83.9f, 1.3f, -105.6f), new Vector3(-84.6f, 1.05f, -108.5f), 48f);
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.StopDance();
        yield return new WaitForSeconds(1f);

        // Mirador: Kuntur mirando el valle (día y atardecer).
        Vector3 mir = new Vector3(4f, 0f, -209.2f);
        Place(mir, 10f);
        yield return new WaitForSeconds(2.5f);
        Vector3 k = Kuntur();
        Photo("p05_mirador_espalda", k + new Vector3(-1.7f, 1.35f, -2.4f), k + new Vector3(2.5f, -1.5f, 14f), 55f);
        Photo("p06_mirador_valle", k + new Vector3(-4.5f, 2.2f, -3.5f), new Vector3(4f, 2f, -110f), 60f);
        SetHour(17.25f);
        yield return new WaitForSeconds(1.5f);
        Photo("p07_mirador_atardecer", k + new Vector3(-1.7f, 1.35f, -2.4f), k + new Vector3(2.5f, -1.5f, 14f), 55f);
        Photo("p08_valle_atardecer", k + new Vector3(-4.5f, 2.2f, -3.5f), new Vector3(4f, 2f, -110f), 60f);
        Photo("p09_aereo_atardecer", new Vector3(-70f, 85f, -250f), new Vector3(0f, 0f, -30f), 55f);
        SetHour(10.2f);
        yield return new WaitForSeconds(1.5f);

        // El pueblo desde el aire (con cielo y cerros).
        Photo("p10_aereo_pueblo", new Vector3(-70f, 85f, -250f), new Vector3(0f, 0f, -30f), 55f);
        Photo("p11_aereo_rio", new Vector3(125f, 55f, -95f), new Vector3(20f, 0f, 10f), 55f);
        Photo("p12_aereo_centro", new Vector3(55f, 45f, -80f), new Vector3(5f, 0f, -10f), 50f);

        // Tuk tuk: en el mirador (con el pueblo detrás) y en el centro.
        DrivableVehicle tuk = TukTuk();
        if (tuk != null)
        {
            tuk.PlaceAt(new Vector3(6f, 0f, -210.2f), 90f);
            yield return new WaitForSeconds(0.3f);
            Place(tuk.transform.position + tuk.transform.right * 1.9f, 90f);
            yield return new WaitForSeconds(0.6f);
            if (VehicleSystem.Instance != null) VehicleSystem.Instance.EnterVehicle(tuk);
            yield return new WaitForSeconds(4.5f);
            Transform t = tuk.transform;
            Photo("p13_tuktuk_mirador", t.TransformPoint(new Vector3(3.2f, 1.1f, 2.6f)), t.TransformPoint(new Vector3(0f, 0.95f, 0.2f)), 50f);
            Photo("p14_tuktuk_mirador_lejos", t.TransformPoint(new Vector3(6.5f, 2.6f, 5.5f)), t.TransformPoint(new Vector3(-1f, 0.2f, -6f)), 55f);
            Photo("p15_tuktuk_frente", t.TransformPoint(new Vector3(0.9f, 1.25f, 3.6f)), t.TransformPoint(new Vector3(0f, 1.0f, 0f)), 48f);
            if (VehicleSystem.Instance != null) VehicleSystem.Instance.ForceExit();
            yield return new WaitForSeconds(1.5f);

            tuk.PlaceAt(new Vector3(1.5f, 0f, -38f), 0f);
            yield return new WaitForSeconds(0.3f);
            Place(tuk.transform.position + tuk.transform.right * 1.9f, 0f);
            yield return new WaitForSeconds(0.6f);
            if (VehicleSystem.Instance != null) VehicleSystem.Instance.EnterVehicle(tuk);
            yield return new WaitForSeconds(4.5f);
            VehicleSystem.DebugThrottle = 0.55f;
            yield return new WaitForSeconds(2.2f);
            t = tuk.transform;
            Photo("p16_tuktuk_calle", t.TransformPoint(new Vector3(-3.0f, 1.2f, 4.2f)), t.TransformPoint(new Vector3(0f, 0.9f, 0.3f)), 52f);
            Photo("p17_tuktuk_persecucion", t.TransformPoint(new Vector3(0f, 3.0f, -7.5f)), t.TransformPoint(new Vector3(0f, 1.0f, 5f)), 55f);
            VehicleSystem.DebugThrottle = 0f;
            yield return new WaitForSeconds(0.2f);
            VehicleSystem.DebugThrottle = -1f;
            yield return new WaitForSeconds(1.5f);
            VehicleSystem.DebugThrottle = 0f;
            if (VehicleSystem.Instance != null) VehicleSystem.Instance.ForceExit();
            yield return new WaitForSeconds(1.5f);

            // De vuelta frente a la casa.
            tuk.PlaceAt(new Vector3(-81.9f, 0f, -113.5f), 180f);
        }

        // Misión: recoger basura (junto al montón del estacionamiento de Plaza Vea).
        Vector3 pile = new Vector3(-27f, 0.2f, 40.5f);
        Place(new Vector3(-25.4f, 0f, 38.7f), Yaw(new Vector3(-25.4f, 0f, 38.7f), pile));
        yield return new WaitForSeconds(1.8f);
        Photo("p18_basura_antes", new Vector3(-21.8f, 1.7f, 35.6f), new Vector3(-26f, 0.7f, 39.8f), 50f);
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayPickup(pile, () => { });
        yield return new WaitForSeconds(1.0f);
        Photo("p19_basura_recoge", new Vector3(-21.8f, 1.7f, 35.6f), new Vector3(-26f, 0.7f, 39.8f), 50f);
        yield return new WaitForSeconds(2.5f);

        // Plaza Constitución y catedral, con Kuntur.
        Place(new Vector3(18.6f, 0f, -40.5f), 200f);
        yield return new WaitForSeconds(2f);
        Photo("p20_plaza_constitucion", new Vector3(17.4f, 1.9f, -46.5f), new Vector3(17.4f, 4.5f, -4f), 55f);
        Photo("p21_catedral", new Vector3(8f, 7f, -24f), new Vector3(17.4f, 8f, -2f), 55f);

        // Los puentes sobre el Mantaro.
        Place(new Vector3(-84.9f, 0f, -105.6f), 180f);
        yield return new WaitForSeconds(0.5f);
        Photo("p22_puentes_rio", new Vector3(47f, 7f, -6f), new Vector3(63f, 1f, 18f), 55f);
        Photo("p23_puente_rojo", new Vector3(50f, 3.5f, 14f), new Vector3(64f, 2.5f, 30f), 55f);
        Photo("p24_rio_curva", new Vector3(70f, 16f, 120f), new Vector3(100f, -1f, 190f), 55f);

        // Ciudad viva: Plaza Vea, calle con gente, parque, heladería.
        Photo("p25_plaza_vea", new Vector3(-10f, 3f, 24f), new Vector3(-22f, 3f, 44f), 55f);
        Photo("p26_calle_real", new Vector3(1.5f, 1.9f, -14f), new Vector3(1f, 3f, 20f), 55f);
        Photo("p27_parque_gente", new Vector3(16.4f, 1.6f, -30.7f), new Vector3(13.85f, 0.8f, -29.1f), 55f);
        Photo("p28_heladeria", new Vector3(-80f, 2.2f, -118f), new Vector3(-71.3f, 2f, -111f), 55f);
        Photo("p29_mirador_bajada", new Vector3(0f, 19.5f, -206f), new Vector3(6f, 2f, -110f), 55f);
        Photo("p30_bodega", new Vector3(30f, 3f, -6f), new Vector3(42f, 1.5f, 4f), 55f);

        Log("listo.");
        yield return new WaitForSeconds(1f);
        Done = true;
    }

    private static void SetHour(float h)
    {
        if (DayNightCycle.Instance != null) DayNightCycle.Instance.SetTimeOfDay(h);
    }

    private static float Yaw(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    private static DrivableVehicle TukTuk()
    {
        foreach (DrivableVehicle v in DrivableVehicle.All) if (v != null && v.IsTukTuk) return v;
        return null;
    }

    private static Vector3 Kuntur()
    {
        var p = SimpleThirdPersonController.Instance;
        return p != null ? p.transform.position : Vector3.zero;
    }

    private static float Ground(float x, float z)
    {
        var p = SimpleThirdPersonController.Instance;
        float best = 0f;
        bool found = false;
        foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, 90f, z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (p != null && hit.collider.transform.IsChildOf(p.transform)) continue;
            if (hit.collider.GetComponentInParent<DrivableVehicle>() != null) continue;
            if (!found || hit.point.y > best) { best = hit.point.y; found = true; }
        }
        return best;
    }

    private static void Place(Vector3 pos, float yaw)
    {
        var p = SimpleThirdPersonController.Instance;
        if (p == null) return;
        pos.y = Ground(pos.x, pos.z) + 0.08f;
        p.Teleport(pos, yaw);
    }

    private static void GoToTutorialPage(string contains)
    {
        var ui = TutorialUI.Instance;
        if (ui == null) return;
        var pagesField = typeof(TutorialUI).GetField("pages", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var indexField = typeof(TutorialUI).GetField("index", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (pagesField == null || indexField == null) return;
        var pages = pagesField.GetValue(ui) as TutorialUI.Page[];
        if (pages == null) return;
        for (int i = 0; i < pages.Length; i++)
        {
            int idx = (int)indexField.GetValue(ui);
            if (pages[idx].title != null && pages[idx].title.ToLowerInvariant().Contains(contains)) return;
            if (idx >= pages.Length - 1) return;
            ui.Next();
        }
    }

    // Renderiza la cámara principal desde un punto a 4K, sin la interfaz.
    private static void Photo(string name, Vector3 from, Vector3 to, float fov)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 oldPos = cam.transform.position;
        Quaternion oldRot = cam.transform.rotation;
        float oldFov = cam.fieldOfView;
        RenderTexture oldTarget = cam.targetTexture;

        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        try
        {
            cam.transform.position = from;
            cam.transform.LookAt(to);
            cam.fieldOfView = fov;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes($"{OutDir}/{name}.jpg", tex.EncodeToJPG(94));
            Log("foto " + name);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Kuntur] Canva: foto " + name + " falló: " + e.Message);
        }
        finally
        {
            cam.targetTexture = oldTarget;
            cam.transform.SetPositionAndRotation(oldPos, oldRot);
            cam.fieldOfView = oldFov;
            rt.Release();
            Destroy(rt);
            Destroy(tex);
        }
    }
}
#endif
