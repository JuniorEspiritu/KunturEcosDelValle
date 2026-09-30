#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Solo editor: graba el FINAL del tráiler, cuadro por cuadro (30 fps fijos
// con Time.captureFramerate, así sale fluido aunque el editor vaya lento).
//
// No es la cámara del jugador: son tomas "de cine", como la cámara
// cinemática de GTA V. Lentes largos desde lejos, una toma alta como de
// helicóptero, otra a ras del piso con el teleobjetivo; Kuntur vuelve a su
// casa al atardecer, acaricia al gatito, y la cámara se aleja del pueblo
// mientras cae la noche, se prenden las luces de las casas y las letras de
// KUNTUR suben por DETRÁS del paisaje (los cerros las tapan mientras suben),
// como el final del tráiler de Alba: A Wildlife Adventure.
//
// Modo Preview: mismo recorrido, pero solo guarda una foto chica cada medio
// segundo. Sirve para revisar encuadres en un par de minutos antes de
// grabar el final completo.
//
// Salida: Trailer/frames/final/<toma>/f0000.jpg (Trailer/frames no se sube
// a GitHub). La crea KunturFinalShots al entrar a Play.
public class KunturFinalCapture : MonoBehaviour
{
    public const string OutDir = "Trailer/frames/final";
    public static bool Done;
    public static bool Preview;

    // ---- Textos del cierre (cambiar acá) ----
    private const string TitleText = "KUNTUR";
    private const string SubtitleText = "ecos del valle";
    private const string LineOne = "Un juego sobre el río Mantaro y la gente del valle";
    private const string LineTwo = "Próximamente en PC  ·  2026";
    private const string CreditText = "Desarrollo de Videojuegos  ·  Universidad Continental  ·  Huancayo, Perú";
    private const string FontPath = "Assets/_Project/Art/Fuentes/JosefinSans-SemiBold SDF.asset";

    private const int Fps = 30;
    private const int PreviewEvery = 15;
    private int W => Preview ? 960 : 2560;
    private int H => Preview ? 540 : 1440;

    private RenderTexture rt;
    private Texture2D tex;
    private Coroutine walker;
    private GameObject giverMarker;

    // Letras del final.
    private TextMeshPro title;
    private TextMeshPro subtitle;
    private TextMeshPro lineOne;
    private TextMeshPro lineTwo;
    private TextMeshPro credit;
    private Transform overlayRoot;

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
        if (rt != null) rt.Release();
        if (title != null) Destroy(title.gameObject);
        if (overlayRoot != null) Destroy(overlayRoot.gameObject);
    }

    private static void Log(string s) => Debug.Log("[Kuntur] Final: " + s);

    // ---------------------------------------------------------------
    // El guion
    // ---------------------------------------------------------------
    // Kuntur baja por la vereda oeste del Jr. Arequipa (x = -84.65) hacia el
    // sur, cruza el Jr. Huánuco (z = -100) y llega a su casa (puerta en
    // x = -88.6, z = -111). El gatito espera en el jardincito de adelante.
    private const float SidewalkX = -84.55f;
    private static readonly Vector3 CatSpot = new Vector3(-86.35f, 0f, -107.55f);
    private static readonly Vector3 PetSpot = new Vector3(-85.55f, 0f, -106.95f);

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
        if (MissionDirector.Instance != null)
        {
            MissionDirector.Instance.HideBanner();
            MissionDirector.Instance.PauseForNight(); // que ningún reloj de misión corra mientras se graba
        }
        giverMarker = FindGiverMarker();
        StaggerNightLights();
        BuildTitles();
        ParkTukTukAway();

        Time.captureFramerate = Fps;

        // ---------------- 1) Toma alta de costado, teleobjetivo ----------------
        SetHour(17.62f);
        Place(new Vector3(SidewalkX, 0f, -50f), 180f);
        yield return Wait(0.6f);
        Walk(new Vector3(SidewalkX, 0f, -120f));
        yield return Wait(0.4f);
        yield return Record("f1_costado", 4.0f, (t, k) => (
            Vector3.Lerp(new Vector3(-76.9f, 2.2f, -33f), new Vector3(-77.1f, 2.5f, -36.5f), t),
            k + Vector3.up * 1.0f), 14f);

        // ---------------- 2) Toma de helicóptero, desde atrás ----------------
        SetHour(17.68f);
        Place(new Vector3(SidewalkX, 0f, -68f), 180f);
        yield return Wait(0.3f);
        Walk(new Vector3(SidewalkX, 0f, -120f));
        yield return Wait(0.3f);
        yield return Record("f2_helicoptero", 4.0f, (t, k) => (
            Vector3.Lerp(new Vector3(-70.5f, 13f, -56f), new Vector3(-72f, 11.2f, -64f), Ease(t)),
            k + Vector3.up * 0.9f), 22f);

        // ---------------- 3) A ras del piso, de frente, teleobjetivo ----------------
        SetHour(17.74f);
        Place(new Vector3(SidewalkX, 0f, -84f), 180f);
        yield return Wait(0.3f);
        Walk(new Vector3(SidewalkX, 0f, -120f));
        yield return Wait(0.3f);
        yield return Record("f3_frente", 4.0f, (t, k) => (
            new Vector3(-81.2f, 1.5f, -128.5f),
            k + Vector3.up * 1.05f), 10f);
        StopWalk();

        // ---------------- 4) Llega a casa, acaricia al gatito, la cámara se aleja ----------------
        yield return FinaleShot();

        Time.captureFramerate = 0;
        Log("listo.");
        yield return new WaitForSecondsRealtime(1f);
        Done = true;
    }

    // Una sola toma continua: el gatito, Kuntur que llega y lo acaricia, y la
    // cámara que se va hacia atrás y arriba hasta ver todo el pueblo desde el
    // sur, mientras oscurece y suben las letras.
    private const float FinaleSeconds = 21f;
    private const float FinaleFov = 46f;
    private static readonly Vector3 CloseCam = new Vector3(-80.6f, 2.3f, -113.8f);
    private static readonly Vector3 FarCam = new Vector3(-48f, 74f, -236f);
    private static readonly Vector3 FarLook = new Vector3(6f, 42f, 150f);

    private IEnumerator FinaleShot()
    {
        SetHour(17.8f);

        // El gatito, quieto y sentado en el jardín (su paseo se apaga).
        AnimalWander cat = FindCat();
        Animator catAnim = null;
        if (cat != null)
        {
            cat.enabled = false;
            Vector3 c = CatSpot;
            c.y = Ground(c.x, c.z);
            cat.transform.position = c;
            cat.transform.rotation = Quaternion.LookRotation(Flat(PetSpot - CatSpot));
            catAnim = cat.GetComponentInChildren<Animator>();
            PlayCat(catAnim, "Extra");
        }

        Place(new Vector3(SidewalkX, 0f, -102.8f), 180f);
        yield return Wait(0.4f);
        Walk(PetSpot);

        Vector3 catLook = CatSpot + Vector3.up * 0.55f;
        bool petted = false;
        bool meowed = false;
        if (title != null) title.gameObject.SetActive(true);

        yield return Record("f4_final", FinaleSeconds, (t, k) =>
        {
            float s = t * FinaleSeconds; // segundos dentro de la toma

            // El cielo: atardecer -> anochecer.
            SetHour(Mathf.Lerp(17.8f, 20.6f, Smooth01(Mathf.InverseLerp(1.5f, FinaleSeconds, s))));

            // La cámara: quieta con el gatito, después se va (7 s) y sigue
            // alejándose despacito mientras suben las letras.
            float pull = Smooth01(Mathf.InverseLerp(4.6f, 12.0f, s));
            float drift = Mathf.InverseLerp(12.0f, FinaleSeconds, s);
            Vector3 lookClose = Vector3.Lerp(k + Vector3.up * 0.85f, catLook, 0.45f);
            Vector3 pos = new Vector3(
                Mathf.Lerp(CloseCam.x, FarCam.x, pull),
                Mathf.Lerp(CloseCam.y, FarCam.y, EaseOut(pull)),   // sube primero: no roza techos ni el cerro
                Mathf.Lerp(CloseCam.z, FarCam.z, pull));
            Vector3 back = Flat(FarCam - FarLook).normalized;
            pos += back * (drift * 14f) + Vector3.up * (drift * 3f);
            Vector3 look = Vector3.Lerp(lookClose, FarLook, Smooth01(Mathf.InverseLerp(4.6f, 11.0f, s)));

            // Letras: suben desde atrás del paisaje y después los textos.
            AnimateTitles(s, pos, look);
            return (pos, look);
        }, FinaleFov, frame =>
        {
            float s = frame / (float)Fps;
            SimpleThirdPersonController p = SimpleThirdPersonController.Instance;
            // Llegó: se agacha a acariciarlo (la misma animación de recoger).
            if (!petted && p != null && (Vector3.Distance(Flat(p.transform.position), Flat(PetSpot)) < 0.55f || s > 2.4f))
            {
                petted = true;
                StopWalk();
                if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayPickup(CatSpot, () => { });
            }
            if (petted && !meowed && s > 2.6f)
            {
                meowed = true;
                PlayCat(catAnim, "Voz");
                Log($"miau en el segundo {s:0.00}");
            }
            if (meowed && s > 4.4f && s < 4.5f) PlayCat(catAnim, "Extra");
        });

        StopWalk();
        if (cat != null) cat.enabled = true;
    }

    // ---------------------------------------------------------------
    // Letras del final
    // ---------------------------------------------------------------
    private const float TitleHeight = 58f;     // metros de alto de la palabra
    private const float TitleDistance = 640f;  // desde la cámara final, en el piso
    private const float TitleRaiseDeg = 13.5f; // cuánto arriba del centro termina
    private const float TitleRise = 200f;      // cuánto sube (empieza escondida detrás del paisaje)
    private Vector3 titleEnd;

    private void BuildTitles()
    {
        TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        title = MakeWorldText("Final_Titulo", TitleText, font, FontStyles.Bold, 10f);
        subtitle = MakeWorldText("Final_Subtitulo", SubtitleText, font, FontStyles.Normal, 34f);
        subtitle.transform.SetParent(title.transform, false);

        // Tamaños en metros: se mide el texto armado y se escala a lo pedido.
        ScaleToHeight(title, TitleHeight);
        float titleScale = title.transform.localScale.y;
        ScaleToHeight(subtitle, TitleHeight * 0.26f);
        subtitle.transform.localScale /= titleScale; // es hija del título
        subtitle.transform.localPosition = new Vector3(0f, -(TitleHeight * 0.72f) / titleScale, 0f);

        // Dónde termina: centrada en la vista final, un poco arriba del
        // centro, a 640 m (detrás del pueblo y de la primera fila de cerros).
        Quaternion finalRot = Quaternion.LookRotation(FarLook - FarCam);
        Vector3 up = finalRot * Quaternion.Euler(-TitleRaiseDeg, 0f, 0f) * Vector3.forward;
        float flat = Flat(up).magnitude;
        titleEnd = FarCam + up * (TitleDistance / Mathf.Max(0.2f, flat));
        title.transform.position = titleEnd - Vector3.up * TitleRise;
        title.transform.rotation = Quaternion.LookRotation(Flat(titleEnd - FarCam));
        SetAlpha(title, 1f);
        SetAlpha(subtitle, 0f);
        title.gameObject.SetActive(false); // aparece recién en la toma final

        // Textos de abajo: van pegados a la cámara (delante de todo).
        overlayRoot = new GameObject("Final_Textos").transform;
        lineOne = MakeWorldText("Final_Linea1", LineOne, font, FontStyles.Normal, 2f);
        lineTwo = MakeWorldText("Final_Linea2", LineTwo, font, FontStyles.Normal, 2f);
        credit = MakeWorldText("Final_Credito", CreditText, font, FontStyles.Normal, 3f);
        foreach (TextMeshPro t in new[] { lineOne, lineTwo, credit })
        {
            t.transform.SetParent(overlayRoot, false);
            SetAlpha(t, 0f);
        }
        // A 1.5 m de la cámara, medidos con el alto de la toma final.
        const float d = 1.5f;
        float half = Mathf.Tan(FinaleFov * 0.5f * Mathf.Deg2Rad) * d;
        ScaleToHeight(lineOne, half * 0.052f);
        ScaleToHeight(lineTwo, half * 0.052f);
        ScaleToHeight(credit, half * 0.028f);
        lineOne.transform.localPosition = new Vector3(0f, -half * 0.40f, d);
        lineTwo.transform.localPosition = new Vector3(0f, -half * 0.50f, d);
        credit.transform.localPosition = new Vector3(0f, -half * 0.90f, d);
        overlayRoot.gameObject.SetActive(false);
    }

    private void AnimateTitles(float s, Vector3 camPos, Vector3 camLook)
    {
        if (title == null) return;
        float rise = EaseOutCubic(Mathf.InverseLerp(10.2f, 14.2f, s));
        title.transform.position = titleEnd - Vector3.up * (TitleRise * (1f - rise));
        // Invisible hasta que empieza a subir: entre los cerros hay huecos por
        // donde se asomaba antes de tiempo.
        SetAlpha(title, Smooth01(Mathf.InverseLerp(10.2f, 11.2f, s)));
        SetAlpha(subtitle, Smooth01(Mathf.InverseLerp(13.4f, 14.9f, s)));

        // Los textos de abajo van con la cámara.
        overlayRoot.gameObject.SetActive(s > 15f);
        overlayRoot.SetPositionAndRotation(camPos, Quaternion.LookRotation(camLook - camPos));
        SetAlpha(lineOne, Smooth01(Mathf.InverseLerp(15.3f, 16.5f, s)));
        SetAlpha(lineTwo, Smooth01(Mathf.InverseLerp(15.8f, 17.0f, s)));
        SetAlpha(credit, Smooth01(Mathf.InverseLerp(16.8f, 17.8f, s)) * 0.75f);
    }

    private static TextMeshPro MakeWorldText(string name, string text, TMP_FontAsset font, FontStyles style, float spacing)
    {
        GameObject go = new GameObject(name);
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontStyle = style;
        tmp.fontSize = 10f;
        tmp.characterSpacing = spacing;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.color = Color.white;
        tmp.rectTransform.sizeDelta = new Vector2(200f, 20f);
        // Sombra suave debajo de las letras, como en el Alba: se leen sobre
        // el cielo claro y sobre los cerros oscuros por igual.
        tmp.fontSharedMaterial = new Material(tmp.fontSharedMaterial);
        tmp.fontSharedMaterial.EnableKeyword("UNDERLAY_ON");
        tmp.fontSharedMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.62f));
        tmp.fontSharedMaterial.SetFloat("_UnderlayOffsetX", 0.2f);
        tmp.fontSharedMaterial.SetFloat("_UnderlayOffsetY", -0.25f);
        tmp.fontSharedMaterial.SetFloat("_UnderlaySoftness", 0.65f);
        tmp.fontSharedMaterial.SetFloat("_UnderlayDilate", 0.25f);
        tmp.ForceMeshUpdate();
        return tmp;
    }

    private static void ScaleToHeight(TextMeshPro tmp, float meters)
    {
        tmp.ForceMeshUpdate();
        float h = tmp.textBounds.size.y;
        if (h < 0.0001f) h = 1f;
        float k = meters / h;
        tmp.transform.localScale = Vector3.one * k;
    }

    private static void SetAlpha(TextMeshPro tmp, float a)
    {
        if (tmp == null) return;
        Color c = tmp.color;
        c.a = a;
        tmp.color = c;
    }

    // ---------------------------------------------------------------
    // Grabación
    // ---------------------------------------------------------------
    private IEnumerator Record(string shot, float seconds, Func<float, Vector3, (Vector3 pos, Vector3 look)> pose, float fov, Action<int> onFrame = null)
    {
        string dir = $"{OutDir}/{(Preview ? "preview_" : "")}{shot}";
        Directory.CreateDirectory(dir);
        foreach (string old in Directory.GetFiles(dir, "*.jpg")) File.Delete(old);
        int frames = Mathf.RoundToInt(seconds * Fps);
        for (int i = 0; i < frames; i++)
        {
            onFrame?.Invoke(i);
            yield return new WaitForEndOfFrame();
            var p = pose(frames > 1 ? i / (float)(frames - 1) : 0f, Kuntur());
            bool save = !Preview || i % PreviewEvery == 0 || i == frames - 1;
            if (save) Render(p.pos, p.look, fov, $"{dir}/f{i:0000}.jpg");
            yield return null;
        }
        Log($"toma {shot} ({frames} cuadros)");
    }

    private void Render(Vector3 from, Vector3 to, float fov, string path)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (giverMarker != null && giverMarker.activeSelf) giverMarker.SetActive(false);

        Vector3 oldPos = cam.transform.position;
        Quaternion oldRot = cam.transform.rotation;
        float oldFov = cam.fieldOfView;
        float oldFar = cam.farClipPlane;
        RenderTexture oldTarget = cam.targetTexture;
        try
        {
            cam.transform.position = from;
            cam.transform.LookAt(to);
            cam.fieldOfView = fov;
            cam.farClipPlane = Mathf.Max(oldFar, 2500f);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToJPG(Preview ? 85 : 93));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Kuntur] Final: cuadro falló: " + e.Message);
        }
        finally
        {
            cam.targetTexture = oldTarget;
            cam.transform.SetPositionAndRotation(oldPos, oldRot);
            cam.fieldOfView = oldFov;
            cam.farClipPlane = oldFar;
        }
    }

    // ---------------------------------------------------------------
    // Kuntur caminando solo (sin teclado)
    // ---------------------------------------------------------------
    private void Walk(params Vector3[] points)
    {
        StopWalk();
        walker = StartCoroutine(WalkRoutine(points));
    }

    private IEnumerator WalkRoutine(Vector3[] points)
    {
        int i = 0;
        while (i < points.Length)
        {
            SimpleThirdPersonController p = SimpleThirdPersonController.Instance;
            if (p == null) break;
            Vector3 to = Flat(points[i] - p.transform.position);
            if (to.magnitude < 0.35f) { i++; continue; }
            Quaternion want = Quaternion.LookRotation(to.normalized);
            p.transform.rotation = Quaternion.RotateTowards(p.transform.rotation, want, 220f * Time.deltaTime);
            SimpleThirdPersonController.DebugMove = new Vector2(0f, 1f);
            yield return null;
        }
        SimpleThirdPersonController.DebugMove = Vector2.zero;
        walker = null;
    }

    private void StopWalk()
    {
        if (walker != null) StopCoroutine(walker);
        walker = null;
        SimpleThirdPersonController.DebugMove = Vector2.zero;
    }

    // ---------------------------------------------------------------
    // Ayudas
    // ---------------------------------------------------------------
    // Las ventanas y faroles no se prenden todos de golpe a las 6: cada uno
    // a su hora (entre 18:36 y 19:48), como un pueblo de verdad.
    private static void StaggerNightLights()
    {
        FieldInfo onHour = typeof(NightLight).GetField("onHour", BindingFlags.NonPublic | BindingFlags.Instance);
        if (onHour == null) return;
        var rng = new System.Random(2026);
        foreach (NightLight light in FindObjectsByType<NightLight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            onHour.SetValue(light, 18.6f + (float)rng.NextDouble() * 1.2f);
    }

    // El tuk tuk de Kuntur se estaciona frente a su casa, justo donde van
    // las cámaras del final: se lo lleva a la esquina de la circunvalación.
    private static void ParkTukTukAway()
    {
        foreach (DrivableVehicle v in DrivableVehicle.All)
            if (v != null && v.IsTukTuk) { v.PlaceAt(new Vector3(-71.5f, 0f, -121.8f), 90f); return; }
    }

    private static GameObject FindGiverMarker()
    {
        if (MissionDirector.Instance == null) return null;
        FieldInfo f = typeof(MissionDirector).GetField("giverMarker", BindingFlags.NonPublic | BindingFlags.Instance);
        Transform t = f != null ? f.GetValue(MissionDirector.Instance) as Transform : null;
        return t != null ? t.gameObject : null;
    }

    private static AnimalWander FindCat()
    {
        foreach (AnimalWander a in FindObjectsByType<AnimalWander>(FindObjectsSortMode.None))
            if (a != null && a.name.Contains("Gato")) return a;
        return null;
    }

    private static void PlayCat(Animator anim, string state)
    {
        if (anim == null) return;
        if (anim.HasState(0, Animator.StringToHash(state))) anim.CrossFadeInFixedTime(state, 0.2f, 0);
    }

    private static IEnumerator Wait(float seconds)
    {
        // Con captureFramerate el tiempo del juego avanza 1/30 por cuadro.
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    private static float Ease(float t) => t * t * (3f - 2f * t);
    private static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
    private static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }
    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private static void SetHour(float h)
    {
        if (DayNightCycle.Instance != null) DayNightCycle.Instance.SetTimeOfDay(h);
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
            if (hit.collider.GetComponentInParent<AnimalWander>() != null) continue;
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
