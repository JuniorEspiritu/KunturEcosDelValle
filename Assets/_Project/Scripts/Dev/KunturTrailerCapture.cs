#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Solo editor: graba las tomas del tráiler cuadro por cuadro (30 fps fijos
// con Time.captureFramerate, así sale fluido aunque el editor vaya lento).
// Cada toma queda en Trailer/frames/<toma>/f0000.jpg; después ffmpeg las une
// con fundidos, títulos y música. La crea KunturTrailerShots al entrar a Play.
public class KunturTrailerCapture : MonoBehaviour
{
    public const string OutDir = "Trailer/frames";
    public static bool Done;

    private const int Fps = 30;
    private const int W = 2560;
    private const int H = 1440;

    private RenderTexture rt;
    private Texture2D tex;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Done = false;
    }

    private void Start() => StartCoroutine(Run());

    private void OnDestroy()
    {
        Time.captureFramerate = 0;
        SimpleThirdPersonController.DebugMove = Vector2.zero;
        VehicleSystem.DebugThrottle = 0f;
        VehicleSystem.DebugSteer = 0f;
        if (rt != null) rt.Release();
    }

    private static void Log(string s) => Debug.Log("[Kuntur] Tráiler: " + s);

    private IEnumerator Run()
    {
        Directory.CreateDirectory(OutDir);
        rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        tex = new Texture2D(W, H, TextureFormat.RGB24, false);

        yield return new WaitForSecondsRealtime(3f);
        var menu = FindAnyObjectByType<MenuController>();
        if (menu != null) menu.ContinueGame();

        float waited = 0f;
        while ((SimpleThirdPersonController.Instance == null || SceneManager.GetActiveScene().name != "Exploracion") && waited < 30f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(8f);
        if (TutorialUI.Showing && TutorialUI.Instance != null) TutorialUI.Instance.Close();
        if (MissionDirector.Instance != null) MissionDirector.Instance.HideBanner();

        Time.captureFramerate = Fps;
        SetHour(10.2f);
        yield return Wait(1f);

        // 1) El valle desde el aire, bajando hacia el pueblo.
        Park();
        yield return Record("s01_valle", 4.0f, t => (
            Vector3.Lerp(new Vector3(-95f, 125f, -310f), new Vector3(-55f, 72f, -225f), Ease(t)),
            Vector3.Lerp(new Vector3(0f, 0f, -60f), new Vector3(0f, 0f, -25f), Ease(t))), 55f);

        // 2) Kuntur frente a su casa: acercamiento y saludo.
        Place(new Vector3(-84.6f, 0f, -108.5f), 15f);
        yield return Wait(1.5f);
        Vector3 k = Kuntur();
        yield return Record("s02_kuntur", 3.2f, t => (
            Vector3.Lerp(k + new Vector3(1.9f, 0.55f, 3.6f), k + new Vector3(0.8f, 0.85f, 2.3f), Ease(t)),
            k + new Vector3(0f, 1.0f, 0f)), 48f,
            frame => { if (frame == 18 && KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayGreet(); });
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.StopDance();

        // 3) Kuntur caminando por la vereda del Jr. Arequipa.
        Place(new Vector3(-84.2f, 0f, -113.5f), 0f);
        yield return Wait(0.8f);
        SimpleThirdPersonController.DebugMove = new Vector2(0f, 1f);
        yield return Wait(0.6f);
        yield return Record("s03_camina", 3.4f, t =>
        {
            Vector3 p = Kuntur();
            return (p + new Vector3(0.9f - 0.6f * t, 1.15f, 3.1f), p + new Vector3(0f, 0.95f, 0f));
        }, 50f);
        SimpleThirdPersonController.DebugMove = Vector2.zero;

        // 4) y 5) El tuk tuk por la Calle Real.
        DrivableVehicle tuk = TukTuk();
        if (tuk != null)
        {
            tuk.PlaceAt(new Vector3(1.5f, 0f, -74f), 0f);
            yield return Wait(0.3f);
            Place(tuk.transform.position + tuk.transform.right * 1.9f, 0f);
            yield return Wait(0.5f);
            if (VehicleSystem.Instance != null) VehicleSystem.Instance.EnterVehicle(tuk);
            yield return Wait(4.5f);
            VehicleSystem.DebugThrottle = 0.8f;
            yield return Wait(0.8f);
            Transform tr = tuk.transform;
            yield return Record("s04_tuktuk", 3.4f, t => (
                tr.TransformPoint(Vector3.Lerp(new Vector3(-2.6f, 1.2f, 4.2f), new Vector3(-3.4f, 1.1f, 0.6f), Ease(t))),
                tr.TransformPoint(new Vector3(0f, 0.95f, 0.2f))), 52f);
            yield return Record("s05_tuktuk_atras", 3.0f, t => (
                tr.TransformPoint(Vector3.Lerp(new Vector3(0f, 2.4f, -6.0f), new Vector3(0f, 3.4f, -8.0f), Ease(t))),
                tr.TransformPoint(new Vector3(0f, 1.0f, 6f))), 55f);
            VehicleSystem.DebugThrottle = -1f;
            yield return Wait(1.5f);
            VehicleSystem.DebugThrottle = 0f;
            if (VehicleSystem.Instance != null) VehicleSystem.Instance.ForceExit();
            yield return Wait(1.5f);
            tuk.PlaceAt(new Vector3(-81.9f, 0f, -113.5f), 180f);
        }

        // 6) Plaza Constitución: la cámara sube mientras Kuntur camina.
        Place(new Vector3(18.4f, 0f, -35.5f), 180f);
        yield return Wait(0.8f);
        SimpleThirdPersonController.DebugMove = new Vector2(0f, 1f);
        yield return Record("s06_plaza", 3.2f, t => (
            Vector3.Lerp(new Vector3(16.6f, 1.4f, -47.5f), new Vector3(16.8f, 5.5f, -46.5f), Ease(t)),
            Vector3.Lerp(new Vector3(17.6f, 2.2f, -30f), new Vector3(17.4f, 5f, -6f), Ease(t))), 55f);
        SimpleThirdPersonController.DebugMove = Vector2.zero;

        // 7) El Mantaro y sus puentes.
        Park();
        yield return Record("s07_rio", 3.2f, t => (
            Vector3.Lerp(new Vector3(43f, 9f, -14f), new Vector3(50f, 7f, 0f), Ease(t)),
            new Vector3(63f, 1f, 18f)), 55f);

        // 8) Misión: recoger basura.
        Vector3 pile = new Vector3(-27f, 0.2f, 40.5f);
        Vector3 stand = new Vector3(-25.4f, 0f, 38.7f);
        Place(stand, Yaw(stand, pile));
        yield return Wait(1.5f);
        yield return Record("s08_basura", 3.2f, t => (
            Vector3.Lerp(new Vector3(-20.8f, 1.9f, 34.4f), new Vector3(-22.3f, 1.5f, 36.1f), Ease(t)),
            new Vector3(-26f, 0.7f, 39.8f)), 50f,
            frame => { if (frame == 10 && KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayPickup(pile, () => { }); });
        yield return Wait(2f);

        // 9) Mirador al atardecer: Kuntur mira su valle.
        SetHour(17.25f);
        Vector3 mir = new Vector3(4f, 0f, -209.2f);
        Place(mir, 10f);
        yield return Wait(2f);
        k = Kuntur();
        yield return Record("s09_mirador", 4.0f, t => (
            Vector3.Lerp(k + new Vector3(-2.6f, 1.7f, -3.6f), k + new Vector3(-1.5f, 1.35f, -2.2f), Ease(t)),
            k + new Vector3(2.5f, -1.5f, 14f)), 55f);

        // 10) Cierre: el valle al atardecer (va con el título).
        Park();
        yield return Record("s10_final", 4.5f, t => (
            Vector3.Lerp(new Vector3(-60f, 80f, -245f), new Vector3(-85f, 95f, -265f), Ease(t)),
            new Vector3(0f, 0f, -30f)), 55f);

        Time.captureFramerate = 0;
        Log("listo.");
        yield return new WaitForSecondsRealtime(1f);
        Done = true;
    }

    private static IEnumerator Wait(float seconds)
    {
        // Con captureFramerate el tiempo del juego avanza 1/30 por cuadro.
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    private static float Ease(float t) => t * t * (3f - 2f * t);

    private IEnumerator Record(string shot, float seconds, Func<float, (Vector3 pos, Vector3 look)> pose, float fov, Action<int> onFrame = null)
    {
        string dir = $"{OutDir}/{shot}";
        Directory.CreateDirectory(dir);
        foreach (string old in Directory.GetFiles(dir, "*.jpg")) File.Delete(old);
        int frames = Mathf.RoundToInt(seconds * Fps);
        for (int i = 0; i < frames; i++)
        {
            onFrame?.Invoke(i);
            yield return new WaitForEndOfFrame();
            var p = pose(frames > 1 ? i / (float)(frames - 1) : 0f);
            Render(p.pos, p.look, fov, $"{dir}/f{i:0000}.jpg");
            yield return null;
        }
        Log($"toma {shot} ({frames} cuadros)");
    }

    private void Render(Vector3 from, Vector3 to, float fov, string path)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 oldPos = cam.transform.position;
        Quaternion oldRot = cam.transform.rotation;
        float oldFov = cam.fieldOfView;
        RenderTexture oldTarget = cam.targetTexture;
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
            File.WriteAllBytes(path, tex.EncodeToJPG(92));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Kuntur] Tráiler: cuadro falló: " + e.Message);
        }
        finally
        {
            cam.targetTexture = oldTarget;
            cam.transform.SetPositionAndRotation(oldPos, oldRot);
            cam.fieldOfView = oldFov;
        }
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

    // Kuntur fuera de cuadro (dentro del patio de su casa) para las tomas aéreas.
    private static void Park() => Place(new Vector3(-84.9f, 0f, -105.6f), 180f);

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
}
#endif
