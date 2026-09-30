#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Solo editor: graba el TRÁILER COMPLETO de 30 segundos, cuadro por cuadro
// (30 fps fijos con Time.captureFramerate), siguiendo el guion del documento:
// cortes rápidos de 1 a 2 segundos (estilo arcade / GTA) y un cierre de noche
// de 7.5 s con KUNTUR subiendo detrás de los cerros y "PRÓXIMAMENTE".
//
// Las tomas se graban en el orden que más conviene (todas las del tuk tuk
// juntas, el atardecer al final); el montaje las pone en el orden del guion.
// Cada toma queda en Trailer/frames/t30/<toma>/f0000.jpg.
//
// Modo Preview: mismo recorrido pero solo guarda unas pocas fotos chicas por
// toma, para revisar encuadres rápido.
public class KunturTrailer30Capture : MonoBehaviour
{
    public const string OutDir = "Trailer/frames/t30";
    public static bool Done;
    public static bool Preview;
    // Solo la toma corta del intro: Kuntur caminando en el mirador del cerro.
    public static bool IntroOnly;

    // ---- Textos del cierre ----
    private const string TitleText = "KUNTUR";
    private const string SubtitleText = "ecos del valle";
    private const string SoonText = "PRÓXIMAMENTE";
    private const string FontPath = "Assets/_Project/Art/Fuentes/JosefinSans-SemiBold SDF.asset";
    private const float TitleWiden = 1.38f;   // letras más anchas

    private const int Fps = 30;
    private const int PreviewEvery = 10;
    private int W => Preview ? 960 : 2560;
    private int H => Preview ? 540 : 1440;

    private RenderTexture rt;
    private Texture2D tex;
    private Coroutine walker;
    private GameObject giverMarker;

    private TextMeshPro title;
    private TextMeshPro subtitle;
    private TextMeshPro soon;
    private Transform overlayRoot;

    // Lugares.
    private static readonly Vector3 ParkCenter = new Vector3(17.45f, 0f, -31.5f);
    private static readonly Vector3 PlazaPile = new Vector3(11.25f, 0f, -27.3f);
    private static readonly Vector3 StreetPile = new Vector3(5.4f, 0f, -58.5f);
    private const float EastRoadX = 92f;
    private const float FootbridgeZ = 14f;
    private static readonly Vector3 CatSpot = new Vector3(-86.35f, 0f, -107.55f);
    private static readonly Vector3 PetSpot = new Vector3(-85.55f, 0f, -106.95f);

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
        SimpleThirdPersonController.DebugSprint = false;
        VehicleSystem.DebugThrottle = 0f;
        VehicleSystem.DebugSteer = 0f;
        if (rt != null) rt.Release();
        if (title != null) Destroy(title.gameObject);
        if (overlayRoot != null) Destroy(overlayRoot.gameObject);
    }

    private static void Log(string s) => Debug.Log("[Kuntur] T30: " + s);

    // ---------------------------------------------------------------
    // El guion
    // ---------------------------------------------------------------
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
            MissionDirector.Instance.PauseForNight();
        }
        if (IntroOnly)
        {
            Time.captureFramerate = Fps;
            SetHour(9.2f);
            yield return Wait(1f);
            yield return MiradorIntroShot();
            Time.captureFramerate = 0;
            Log("listo.");
            yield return new WaitForSecondsRealtime(1f);
            Done = true;
            yield break;
        }
        giverMarker = FindGiverMarker();
        StaggerNightLights();
        BuildTitles();

        Time.captureFramerate = Fps;
        SetHour(10.4f);
        yield return Wait(1f);

        // ============ A pie, de día ============

        // 02 - Bajada: por la vereda de la subida de la Calle Real.
        Place(new Vector3(-5.4f, 0f, -178f), 0f);
        yield return Wait(0.3f);
        Walk(new Vector3(-5.4f, 0f, -120f));
        yield return Wait(0.6f);
        yield return Record("02_bajada", 1.0f, (t, k) => (
            k + new Vector3(1.3f, 3.7f - 0.3f * t, -4.8f), k + new Vector3(0f, 0.6f, 3.5f)), 48f);
        StopWalk();

        // 03 - Chacra: corriendo por el camino de tierra, plano lateral bajo.
        Place(new Vector3(EastRoadX + 2.2f, 0f, -50f), 0f);
        yield return Wait(0.3f);
        SimpleThirdPersonController.DebugSprint = true;
        Walk(new Vector3(EastRoadX + 2.2f, 0f, 20f));
        yield return Wait(0.8f);
        yield return Record("03_chacra", 1.0f, (t, k) => (
            k + new Vector3(5.6f, 0.75f, 1.2f), k + new Vector3(0f, 0.9f, 0.9f)), 38f);
        StopWalk();
        SimpleThirdPersonController.DebugSprint = false;

        // 05 - El perrito, de cerca.
        Park();
        AnimalWander dog = FindDog(new Vector3(EastRoadX, 0f, -22f));
        Vector3 dogSpot = new Vector3(EastRoadX + 3.6f, 0f, -20f);
        if (dog != null) Pose(dog, dogSpot, 270f, "Extra");
        yield return Wait(0.5f);
        Vector3 dp = dogSpot; dp.y = Ground(dp.x, dp.z);
        yield return Record("05_perro", 1.0f, (t, k) => (
            dp + new Vector3(-1.75f + 0.25f * t, 0.42f, 0.35f), dp + new Vector3(0f, 0.32f, 0f)), 42f);

        // 13 se graba al atardecer; el perro vuelve a su paseo.
        if (dog != null) dog.enabled = true;

        // 08 - Puente peatonal, desde lejos y de costado.
        float bridgeX = BridgeCenterX();
        Place(new Vector3(bridgeX - 9.5f, 0f, FootbridgeZ), 90f);
        yield return Wait(0.3f);
        Walk(new Vector3(bridgeX + 14f, 0f, FootbridgeZ));
        yield return Wait(1.6f);
        yield return Record("08_puente", 1.0f, (t, k) => (
            new Vector3(bridgeX - 1.5f, 3.4f, FootbridgeZ - 16f), k + Vector3.up * 1.0f), 15f);
        StopWalk();

        // 04 - La zona sucia: montón de basura en la Plaza Constitución.
        Park();
        List<TrashPickup> plazaBags = ShowPile("Zona_Parque", PlazaPile);
        yield return Wait(0.4f);
        yield return Record("04_zona_sucia", 1.0f, (t, k) => (
            Vector3.Lerp(new Vector3(8.4f, 2.9f, -34.4f), new Vector3(8.8f, 2.75f, -33.7f), t),
            PlazaPile + new Vector3(0f, 0.3f, 0f)), 40f);

        // 11 - Limpieza: recoge una bolsa y celebra.
        Vector3 stand = PlazaPile + new Vector3(-2.3f, 0f, -1.4f);
        Place(stand, Yaw(stand, PlazaPile));
        yield return Wait(0.6f);
        TrashPickup bag = Nearest(plazaBags, stand);
        Vector3 f = Flat(PlazaPile - stand).normalized;
        Vector3 r = Vector3.Cross(Vector3.up, f);
        yield return Record("11_limpieza", 2.0f, (t, k) => (
            k + f * 2.7f + r * 1.9f + Vector3.up * (1.35f - 0.15f * t), k + Vector3.up * 0.8f + f * 0.5f), 42f,
            frame =>
            {
                if (frame == 3 && KunturMixamoAnimator.Instance != null && bag != null)
                    KunturMixamoAnimator.Instance.PlayPickup(bag.transform.position, () => Shrink(bag));
                if (frame == 3) Cue("recoger", 3);
                if (frame == 34)
                {
                    if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayDance();
                    foreach (TrashPickup b in plazaBags) Shrink(b);
                    Cue("victoria", 34);
                }
            });
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.StopDance();
        HideAll(plazaBags);

        // 07 - Baile: plano medio centrado.
        Place(new Vector3(ParkCenter.x, 0f, ParkCenter.z - 9.0f), 180f);
        yield return Wait(0.2f);
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayDance();
        yield return Wait(0.9f);
        if (SimpleThirdPersonController.Instance != null)
            SimpleThirdPersonController.Instance.FaceTowards(Kuntur() + new Vector3(0.4f, 0f, -3.6f));
        yield return Record("07_baile", 1.0f, (t, k) => (
            k + new Vector3(0.4f - 0.3f * t, 1.35f, -3.6f + 0.3f * t), k + Vector3.up * 1.15f), 44f);
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.StopDance();

        // 09 - Doña Rosa.
        yield return TalkShot("09_rosa", FindNpc("Doña Rosa", "NPC_DonaRosa"));

        // 15 - Un vecino preocupado (Brayan).
        yield return TalkShot("15_dialogo", FindNpc("Brayan", null));

        // 14 - El problema: un montón de basura en la esquina.
        List<TrashPickup> streetBags = ShowPile("Zona_CalleReal", StreetPile);
        Place(StreetPile + new Vector3(0f, 0f, -4.2f), 0f);
        yield return Wait(0.6f);
        yield return Record("14_problema", 1.0f, (t, k) => (
            Vector3.Lerp(new Vector3(1.2f, 1.6f, -66.4f), new Vector3(1.7f, 1.45f, -65.4f), t),
            StreetPile + new Vector3(0f, 0.8f, -1.8f)), 42f);

        // 06 - El mapa de limpieza (la cámara del mapa, desde arriba).
        Park();
        yield return Wait(0.2f);
        yield return RecordMap("06_mapa", 1.0f);

        // ============ En el tuk tuk ============
        DrivableVehicle tuk = TukTuk();
        if (tuk != null)
        {
            // 10 - Se sube a la moto.
            tuk.PlaceAt(new Vector3(2.3f, 0f, -8f), 0f);
            yield return Wait(0.3f);
            Place(tuk.transform.position - tuk.transform.right * 1.9f + tuk.transform.forward * 0.2f, 90f);
            yield return Wait(0.5f);
            Transform tr = tuk.transform;
            yield return Record("10_moto", 1.0f, (t, k) => (
                tr.TransformPoint(Vector3.Lerp(new Vector3(-3.0f, 1.3f, 3.4f), new Vector3(-2.6f, 1.2f, 3.0f), t)),
                tr.TransformPoint(new Vector3(-0.6f, 1.0f, 0f))), 44f,
                frame =>
                {
                    if (frame == 1 && VehicleSystem.Instance != null) VehicleSystem.Instance.EnterVehicle(tuk);
                    if (frame == 1) Cue("arranque", 1);
                });
            yield return Wait(4.5f);
            yield return ExitTuk();

            // 01 - Llegada: baja en mototaxi desde el cerro hacia la ciudad.
            yield return RideTuk(tuk, new Vector3(2.3f, 0f, -168f), 0f, 0.9f, 1.3f);
            yield return Record("01_llegada", 2.0f, (t, k) => (
                tr.TransformPoint(Vector3.Lerp(new Vector3(0.5f, 2.6f, -7.2f), new Vector3(0.3f, 2.1f, -6.0f), t)),
                tr.TransformPoint(new Vector3(0f, 1.0f, 8f))), 52f);
            yield return ExitTuk();

            // 16 - Rescate: pasa la mototaxi y la basura de la esquina desaparece.
            yield return RideTuk(tuk, new Vector3(2.3f, 0f, -86f), 0f, 0.95f, 2.0f);
            yield return Record("16_rescate", 2.0f, (t, k) => (
                new Vector3(-5.8f, 1.15f, -52.2f), Vector3.Lerp(StreetPile + Vector3.up * 0.7f, tr.position + Vector3.up * 1.0f, 0.25f)), 44f,
                frame =>
                {
                    foreach (TrashPickup b in streetBags)
                        if (b != null && b.gameObject.activeInHierarchy && tr.position.z > b.transform.position.z - 2.5f)
                        {
                            Shrink(b);
                            Cue("recoger", frame);
                        }
                    if (frame == 2) { tuk.Horn(); Cue("claxon", frame); }
                });
            HideAll(streetBags);
            yield return ExitTuk();

            // 12 - Atardecer: por la Calle Real, junto a la Plaza Constitución.
            SetHour(17.75f);
            yield return RideTuk(tuk, new Vector3(2.3f, 0f, -70f), 0f, 0.85f, 1.2f);
            yield return Record("12_atardecer", 2.0f, (t, k) => (
                tr.TransformPoint(new Vector3(-1.7f, 1.35f, -5.4f)),
                tr.TransformPoint(new Vector3(0.6f, 1.3f, 10f))), 48f);
            yield return ExitTuk();
            ParkTukTukAway();
        }

        // ============ Atardecer ============

        // 13 - Vuelta por la chacra, con el perrito, mirando al sol.
        SetHour(17.85f);
        if (dog != null) Pose(dog, new Vector3(EastRoadX - 9.5f, 0f, FootbridgeZ + 1.4f), 90f, "Idle");
        Place(new Vector3(EastRoadX - 4.5f, 0f, FootbridgeZ), 270f);
        yield return Wait(0.3f);
        Walk(new Vector3(EastRoadX - 30f, 0f, FootbridgeZ));
        yield return Wait(0.6f);
        yield return Record("13_camino", 1.0f, (t, k) => (
            k + new Vector3(4.4f, 1.3f, 1.9f), k + new Vector3(-9f, 1.3f, -0.4f)), 50f);
        StopWalk();
        if (dog != null) dog.enabled = true;

        // 17 - En casa: acaricia al gatito.
        yield return CatShot();

        // 18 - Cierre: la cámara se aleja, cae la noche, KUNTUR y PRÓXIMAMENTE.
        yield return FinaleShot();

        Time.captureFramerate = 0;
        Log("listo.");
        yield return new WaitForSecondsRealtime(1f);
        Done = true;
    }

    // Intro (3 s): Kuntur llega a la baranda del mirador y se ve la ciudad.
    private static readonly Vector3 MiradorCenter = new Vector3(-160f, 0f, -138f);

    private IEnumerator MiradorIntroShot()
    {
        Vector3 view = Flat(Vector3.zero - MiradorCenter).normalized;
        Vector3 across = Vector3.Cross(Vector3.up, view);
        // Se busca la baranda de verdad (sus postes) y Kuntur se para 0.9 m
        // antes, así nunca la atraviesa.
        Vector3 railMid = Vector3.zero; int posts = 0;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (t.name != "Baranda_Poste") continue;
            if (Flat(t.position - MiradorCenter).magnitude > 40f) continue;
            railMid += t.position; posts++;
        }
        if (posts > 0)
        {
            railMid /= posts;
            view = Flat(Vector3.zero - railMid).normalized;
            across = Vector3.Cross(Vector3.up, view);
        }
        else railMid = MiradorCenter + view * 10.4f;
        Vector3 end = Flat(railMid) - view * 0.9f;
        Vector3 start = end - view * 6.5f;
        Log($"baranda: {posts} postes, centro {railMid}");
        SimpleThirdPersonController.DebugSprint = false;
        // Si el jugador dejó la cámara en primera persona, el cuerpo de Kuntur
        // no se dibuja y camina hacia donde mira la cámara: se fuerza "Cerca".
        var player = SimpleThirdPersonController.Instance;
        MethodInfo setMode = typeof(SimpleThirdPersonController).GetProperty("CurrentCameraMode")?.GetSetMethod(true);
        object oldMode = player != null ? (object)player.CurrentCameraMode : null;
        if (player != null && setMode != null) setMode.Invoke(player, new object[] { SimpleThirdPersonController.CameraMode.Cerca });
        Place(start, Yaw(start, end));
        yield return Wait(0.6f);
        Walk(end);
        yield return Wait(0.35f);
        // 3 s: tercera persona detrás de Kuntur mientras llega a la baranda;
        // al llegar, la cámara entra a sus ojos (primera persona) y queda la
        // ciudad entera delante.
        yield return Record("intro_mirador", 3.0f, (t, k) =>
        {
            float e = Smooth01(Mathf.InverseLerp(0.55f, 0.86f, t));
            Vector3 third = k - view * 3.3f + across * 0.55f + Vector3.up * 1.75f;
            Vector3 first = k + view * 0.25f + Vector3.up * 1.5f;
            Vector3 lookThird = k + view * 8f + Vector3.up * 1.2f;
            Vector3 lookFirst = k + view * 60f + Vector3.up * -16f;
            return (Vector3.Lerp(third, first, e), Vector3.Lerp(lookThird, lookFirst, Smooth01(Mathf.InverseLerp(0.45f, 0.9f, t))));
        }, 52f, frame =>
        {
            // Cuando la cámara ya casi está en la cabeza, el cuerpo se oculta.
            if (frame == 64 && player != null && setMode != null)
                setMode.Invoke(player, new object[] { SimpleThirdPersonController.CameraMode.PrimeraPersona });
        });
        StopWalk();
        if (player != null && setMode != null && oldMode != null) setMode.Invoke(player, new[] { oldMode });
    }

    // ---------------------------------------------------------------
    // Tomas con más lógica
    // ---------------------------------------------------------------
    private IEnumerator TalkShot(string shot, DialogueNPC npc)
    {
        if (npc == null) { Log($"no encontré al vecino de {shot}"); yield break; }
        Vector3 n = npc.transform.position; n.y = Ground(n.x, n.z);
        Vector3 fwd = Flat(npc.transform.forward).sqrMagnitude > 0.01f ? Flat(npc.transform.forward).normalized : Vector3.forward;
        // Si conversa con un compañero, Kuntur se para del otro lado (si no,
        // el compañero queda entre la cámara y el vecino).
        VillagerIdle idle = npc.GetComponent<VillagerIdle>();
        FieldInfo lf = typeof(VillagerIdle).GetField("lookAtTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        Transform partner = idle != null && lf != null ? lf.GetValue(idle) as Transform : null;
        if (partner != null)
        {
            Vector3 away = Flat(n - partner.position);
            if (away.sqrMagnitude > 0.01f) fwd = Quaternion.Euler(0f, 35f, 0f) * away.normalized;
        }
        Vector3 stand = n + fwd * 1.9f;
        Place(stand, Yaw(stand, n));
        npc.SetTalking(true);
        yield return Wait(0.8f);
        float head = HeadHeight(npc.gameObject, n.y);
        yield return Record(shot, 1.0f, (t, k) =>
        {
            Vector3 dir = Flat(n - k).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 pos = k + Vector3.up * 1.74f + side * (0.74f - 0.06f * t) - dir * (1.25f - 0.12f * t);
            return (pos, new Vector3(n.x, head, n.z));
        }, 36f, frame => { if (frame == 0) Cue("dialogo", 0); });
        npc.SetTalking(false);
    }

    private IEnumerator CatShot()
    {
        SetHour(17.9f);
        AnimalWander cat = FindCat();
        Animator catAnim = null;
        if (cat != null)
        {
            Pose(cat, CatSpot, Yaw(CatSpot, PetSpot), "Extra");
            catAnim = cat.GetComponentInChildren<Animator>();
        }
        Place(PetSpot, Yaw(PetSpot, CatSpot));
        yield return Wait(0.5f);
        Vector3 mid = (CatSpot + PetSpot) * 0.5f;
        mid.y = Ground(mid.x, mid.z);
        yield return Record("17_gato", 2.5f, (t, k) => (
            Vector3.Lerp(mid + new Vector3(2.7f, 1.0f, -2.4f), mid + new Vector3(2.2f, 0.85f, -1.9f), t),
            mid + Vector3.up * 0.45f), 38f,
            frame =>
            {
                if (frame == 4 && KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayPickup(CatSpot, () => { });
                if (frame == 30) { PlayAnim(catAnim, "Voz"); Cue("miau", frame); }
            });
    }

    // Una sola toma continua: desde arriba de la casa hasta ver el pueblo
    // entero, cae la noche, KUNTUR sube desde atrás de los cerros y al final
    // aparece PRÓXIMAMENTE.
    private const float FinaleSeconds = 7.5f;
    private const float FinaleFov = 46f;
    private static readonly Vector3 StartCam = new Vector3(-79.2f, 4.6f, -117.8f);
    private static readonly Vector3 FarCam = new Vector3(-48f, 74f, -236f);
    private static readonly Vector3 FarLook = new Vector3(6f, 42f, 150f);

    private IEnumerator FinaleShot()
    {
        SetHour(17.95f);
        yield return Wait(0.2f);
        if (title != null) title.gameObject.SetActive(true);
        Vector3 kLook = Kuntur() + Vector3.up * 0.8f;

        yield return Record("18_final", FinaleSeconds, (t, k) =>
        {
            float s = t * FinaleSeconds;
            SetHour(Mathf.Lerp(17.95f, 20.6f, Smooth01(Mathf.InverseLerp(0.3f, 5.0f, s))));

            float pull = Smooth01(Mathf.InverseLerp(0f, 3.6f, s));
            float drift = Mathf.InverseLerp(3.6f, FinaleSeconds, s);
            Vector3 pos = new Vector3(
                Mathf.Lerp(StartCam.x, FarCam.x, pull),
                Mathf.Lerp(StartCam.y, FarCam.y, EaseOut(pull)),
                Mathf.Lerp(StartCam.z, FarCam.z, pull));
            Vector3 back = Flat(FarCam - FarLook).normalized;
            pos += back * (drift * 6f) + Vector3.up * (drift * 1.5f);
            Vector3 look = Vector3.Lerp(kLook, FarLook, Smooth01(Mathf.InverseLerp(0.2f, 3.2f, s)));

            AnimateTitles(s, pos, look);
            return (pos, look);
        }, FinaleFov);
    }

    // ---------------------------------------------------------------
    // Letras del cierre
    // ---------------------------------------------------------------
    private const float TitleHeight = 58f;
    private const float TitleDistance = 640f;
    private const float TitleRaiseDeg = 13.5f;
    private const float TitleRise = 200f;
    private Vector3 titleEnd;

    private void BuildTitles()
    {
        TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        title = MakeWorldText("T30_Titulo", TitleText, font, FontStyles.Bold, 12f);
        subtitle = MakeWorldText("T30_Subtitulo", SubtitleText, font, FontStyles.Normal, 34f);
        subtitle.transform.SetParent(title.transform, false);

        ScaleToHeight(title, TitleHeight);
        // Letras más anchas: se estira el título a lo ancho (el subtítulo,
        // que es hijo, se compensa para no deformarse).
        Vector3 ts = title.transform.localScale;
        title.transform.localScale = new Vector3(ts.x * TitleWiden, ts.y, ts.z);
        float titleScaleY = title.transform.localScale.y;
        ScaleToHeight(subtitle, TitleHeight * 0.26f);
        Vector3 ss = subtitle.transform.localScale / titleScaleY;
        subtitle.transform.localScale = new Vector3(ss.x / TitleWiden, ss.y, ss.z);
        subtitle.transform.localPosition = new Vector3(0f, -(TitleHeight * 0.72f) / titleScaleY, 0f);

        Quaternion finalRot = Quaternion.LookRotation(FarLook - FarCam);
        Vector3 up = finalRot * Quaternion.Euler(-TitleRaiseDeg, 0f, 0f) * Vector3.forward;
        float flat = Flat(up).magnitude;
        titleEnd = FarCam + up * (TitleDistance / Mathf.Max(0.2f, flat));
        title.transform.position = titleEnd - Vector3.up * TitleRise;
        title.transform.rotation = Quaternion.LookRotation(Flat(titleEnd - FarCam));
        SetAlpha(title, 0f);
        SetAlpha(subtitle, 0f);
        title.gameObject.SetActive(false);

        // PRÓXIMAMENTE: pegado a la cámara, debajo del centro.
        overlayRoot = new GameObject("T30_Textos").transform;
        soon = MakeWorldText("T30_Proximamente", SoonText, font, FontStyles.Bold, 22f);
        soon.transform.SetParent(overlayRoot, false);
        SetAlpha(soon, 0f);
        const float d = 1.5f;
        float half = Mathf.Tan(FinaleFov * 0.5f * Mathf.Deg2Rad) * d;
        ScaleToHeight(soon, half * 0.105f);
        soon.transform.localPosition = new Vector3(0f, -half * 0.42f, d);
        overlayRoot.gameObject.SetActive(false);
    }

    private void AnimateTitles(float s, Vector3 camPos, Vector3 camLook)
    {
        if (title == null) return;
        float rise = EaseOutCubic(Mathf.InverseLerp(2.5f, 4.3f, s));
        title.transform.position = titleEnd - Vector3.up * (TitleRise * (1f - rise));
        SetAlpha(title, Smooth01(Mathf.InverseLerp(2.5f, 3.1f, s)));
        SetAlpha(subtitle, Smooth01(Mathf.InverseLerp(3.9f, 4.6f, s)));

        overlayRoot.gameObject.SetActive(s > 4.6f);
        overlayRoot.SetPositionAndRotation(camPos, Quaternion.LookRotation(camLook - camPos));
        SetAlpha(soon, Smooth01(Mathf.InverseLerp(4.7f, 5.5f, s)));
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
        tmp.transform.localScale = Vector3.one * (meters / h);
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
        string dir = PrepareDir(shot);
        int frames = Mathf.RoundToInt(seconds * Fps);
        // Dos cuadros de calentamiento sin guardar: al saltar la cámara a un
        // lugar nuevo, el primer render a veces sale con árboles sin textura.
        var p0 = pose(0f, Kuntur());
        for (int w = 0; w < 2; w++)
        {
            yield return new WaitForEndOfFrame();
            Render(Camera.main, p0.pos, p0.look, fov, null);
        }
        Time.captureFramerate = Fps;
        for (int i = 0; i < frames; i++)
        {
            onFrame?.Invoke(i);
            yield return new WaitForEndOfFrame();
            var p = pose(frames > 1 ? i / (float)(frames - 1) : 0f, Kuntur());
            if (ShouldSave(i, frames)) Render(Camera.main, p.pos, p.look, fov, $"{dir}/f{i:0000}.jpg");
            yield return null;
        }
        Log($"toma {shot} ({frames} cuadros)");
    }

    // El mapa: la cámara del minimapa, desde arriba, con un zoom lento.
    private IEnumerator RecordMap(string shot, float seconds)
    {
        string dir = PrepareDir(shot);
        MapSystem map = FindAnyObjectByType<MapSystem>();
        FieldInfo fi = typeof(MapSystem).GetField("mapCamera", BindingFlags.NonPublic | BindingFlags.Instance);
        Camera cam = map != null && fi != null ? fi.GetValue(map) as Camera : null;
        if (cam == null) { Log("sin cámara del mapa"); yield break; }
        int frames = Mathf.RoundToInt(seconds * Fps);
        for (int i = 0; i < frames; i++)
        {
            yield return new WaitForEndOfFrame();
            float t = frames > 1 ? i / (float)(frames - 1) : 0f;
            if (ShouldSave(i, frames))
            {
                float oldSize = cam.orthographicSize;
                Vector3 oldPos = cam.transform.position;
                Quaternion oldRot = cam.transform.rotation;
                RenderTexture oldTarget = cam.targetTexture;
                float oldAspect = cam.aspect;
                try
                {
                    cam.orthographicSize = Mathf.Lerp(78f, 66f, Smooth01(t));
                    cam.transform.SetPositionAndRotation(new Vector3(-12f, 220f, -8f), Quaternion.Euler(90f, 0f, 0f));
                    cam.targetTexture = rt;
                    cam.aspect = W / (float)H;
                    cam.Render();
                    Save($"{dir}/f{i:0000}.jpg");
                }
                finally
                {
                    cam.targetTexture = oldTarget;
                    cam.aspect = oldAspect;
                    cam.ResetAspect();
                    cam.orthographicSize = oldSize;
                    cam.transform.SetPositionAndRotation(oldPos, oldRot);
                }
            }
            yield return null;
        }
        Log($"toma {shot} ({frames} cuadros)");
    }

    private string PrepareDir(string shot)
    {
        string dir = $"{OutDir}/{(Preview ? "preview_" : "")}{shot}";
        Directory.CreateDirectory(dir);
        foreach (string old in Directory.GetFiles(dir, "*.jpg")) File.Delete(old);
        return dir;
    }

    private static bool ShouldSave(int i, int frames) => !Preview || i % PreviewEvery == 0 || i == frames - 1;

    private void Render(Camera cam, Vector3 from, Vector3 to, float fov, string path)
    {
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
            if (path != null) Save(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Kuntur] T30: cuadro falló: " + e.Message);
        }
        finally
        {
            cam.targetTexture = oldTarget;
            cam.transform.SetPositionAndRotation(oldPos, oldRot);
            cam.fieldOfView = oldFov;
            cam.farClipPlane = oldFar;
        }
    }

    private void Save(string path)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToJPG(Preview ? 85 : 93));
    }

    // Marca en el log el cuadro de un efecto de sonido (para el montaje).
    private static void Cue(string what, int frame) => Log($"sonido {what} cuadro {frame}");

    // ---------------------------------------------------------------
    // Tuk tuk
    // ---------------------------------------------------------------
    private IEnumerator RideTuk(DrivableVehicle tuk, Vector3 pos, float yaw, float throttle, float warmup)
    {
        tuk.PlaceAt(pos, yaw);
        yield return Wait(0.3f);
        Place(tuk.transform.position - tuk.transform.right * 1.9f, yaw);
        yield return Wait(0.4f);
        if (VehicleSystem.Instance != null) VehicleSystem.Instance.EnterVehicle(tuk);
        yield return Wait(4.5f);
        VehicleSystem.DebugThrottle = throttle;
        yield return Wait(warmup);
    }

    private IEnumerator ExitTuk()
    {
        VehicleSystem.DebugThrottle = 0f;
        if (VehicleSystem.Driving)
        {
            VehicleSystem.DebugThrottle = -1f;
            yield return Wait(1.2f);
            VehicleSystem.DebugThrottle = 0f;
            if (VehicleSystem.Instance != null) VehicleSystem.Instance.ForceExit();
        }
        yield return Wait(1.5f);
    }

    private static DrivableVehicle TukTuk()
    {
        foreach (DrivableVehicle v in DrivableVehicle.All) if (v != null && v.IsTukTuk) return v;
        return null;
    }

    private static void ParkTukTukAway()
    {
        DrivableVehicle t = TukTuk();
        if (t != null) t.PlaceAt(new Vector3(-71.5f, 0f, -121.8f), 90f);
    }

    // ---------------------------------------------------------------
    // Basura
    // ---------------------------------------------------------------
    // Prende el montón de basura de una zona que queda más cerca del punto.
    private static List<TrashPickup> ShowPile(string zoneRoot, Vector3 near)
    {
        var bags = new List<TrashPickup>();
        GameObject root = FindInactive(zoneRoot);
        if (root == null) { Log($"sin zona {zoneRoot}"); return bags; }
        root.SetActive(true);
        CleanupSpot best = null;
        float bestD = float.MaxValue;
        foreach (CleanupSpot s in root.GetComponentsInChildren<CleanupSpot>(true))
        {
            float d = Vector3.Distance(Flat(s.transform.position), Flat(near));
            if (d < bestD) { bestD = d; best = s; }
        }
        foreach (CleanupSpot s in root.GetComponentsInChildren<CleanupSpot>(true)) s.gameObject.SetActive(s == best);
        foreach (WaterSampleKit k in root.GetComponentsInChildren<WaterSampleKit>(true)) k.gameObject.SetActive(false);
        Transform pool = best != null ? best.transform : root.transform;
        foreach (TrashPickup b in pool.GetComponentsInChildren<TrashPickup>(true))
        {
            b.ResetPickup("");
            bags.Add(b);
        }
        return bags;
    }

    private void Shrink(TrashPickup b)
    {
        if (b == null || !b.gameObject.activeInHierarchy) return;
        StartCoroutine(ShrinkRoutine(b.transform));
    }

    private static IEnumerator ShrinkRoutine(Transform t)
    {
        Vector3 s0 = t.localScale;
        Vector3 p0 = t.position;
        for (float e = 0f; e < 0.3f; e += Time.deltaTime)
        {
            if (t == null) yield break;
            float p = e / 0.3f;
            t.localScale = Vector3.Lerp(s0, Vector3.zero, p);
            t.position = p0 + Vector3.up * (p * 0.5f);
            yield return null;
        }
        if (t != null) { t.gameObject.SetActive(false); t.localScale = s0; t.position = p0; }
    }

    private static void HideAll(List<TrashPickup> bags)
    {
        foreach (TrashPickup b in bags) if (b != null) b.gameObject.SetActive(false);
    }

    private static TrashPickup Nearest(List<TrashPickup> bags, Vector3 p)
    {
        TrashPickup best = null;
        float bd = float.MaxValue;
        foreach (TrashPickup b in bags)
        {
            if (b == null) continue;
            float d = Vector3.Distance(Flat(b.transform.position), Flat(p));
            if (d < bd) { bd = d; best = b; }
        }
        return best;
    }

    // ---------------------------------------------------------------
    // Kuntur caminando solo
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
    private static void StaggerNightLights()
    {
        FieldInfo onHour = typeof(NightLight).GetField("onHour", BindingFlags.NonPublic | BindingFlags.Instance);
        if (onHour == null) return;
        var rng = new System.Random(2026);
        foreach (NightLight light in FindObjectsByType<NightLight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            onHour.SetValue(light, 18.6f + (float)rng.NextDouble() * 1.2f);
    }

    private static GameObject FindGiverMarker()
    {
        if (MissionDirector.Instance == null) return null;
        FieldInfo f = typeof(MissionDirector).GetField("giverMarker", BindingFlags.NonPublic | BindingFlags.Instance);
        Transform t = f != null ? f.GetValue(MissionDirector.Instance) as Transform : null;
        return t != null ? t.gameObject : null;
    }

    private static GameObject FindInactive(string name)
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
        return null;
    }

    private static DialogueNPC FindNpc(string npcName, string objectName)
    {
        if (!string.IsNullOrEmpty(objectName))
        {
            GameObject go = FindInactive(objectName);
            if (go != null && go.GetComponent<DialogueNPC>() != null) return go.GetComponent<DialogueNPC>();
        }
        foreach (DialogueNPC n in FindObjectsByType<DialogueNPC>(FindObjectsSortMode.None))
            if (n != null && n.NpcName == npcName) return n;
        return null;
    }

    private static float HeadHeight(GameObject go, float groundY)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return groundY + 1.55f;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return Mathf.Clamp(b.max.y - 0.18f, groundY + 1.2f, groundY + 1.9f);
    }

    private static float BridgeCenterX()
    {
        GameObject bridge = FindInactive("Puente_Peatonal");
        if (bridge == null) return 64f;
        Renderer[] rs = bridge.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 64f;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b.center.x;
    }

    private static AnimalWander FindCat()
    {
        foreach (AnimalWander a in FindObjectsByType<AnimalWander>(FindObjectsSortMode.None))
            if (a != null && a.name.Contains("Gato")) return a;
        return null;
    }

    private static AnimalWander FindDog(Vector3 near)
    {
        AnimalWander best = null;
        float bd = float.MaxValue;
        foreach (AnimalWander a in FindObjectsByType<AnimalWander>(FindObjectsSortMode.None))
        {
            if (a == null || !a.name.Contains("Perro")) continue;
            float d = Vector3.Distance(a.transform.position, near);
            if (d < bd) { bd = d; best = a; }
        }
        return best;
    }

    // Deja a un animal quieto en un lugar, mirando hacia donde se le pide.
    private static void Pose(AnimalWander a, Vector3 pos, float yaw, string state)
    {
        a.enabled = false;
        pos.y = Ground(pos.x, pos.z);
        a.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        PlayAnim(a.GetComponentInChildren<Animator>(), state);
    }

    private static void PlayAnim(Animator anim, string state)
    {
        if (anim == null) return;
        if (anim.HasState(0, Animator.StringToHash(state))) anim.CrossFadeInFixedTime(state, 0.2f, 0);
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    private static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
    private static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }
    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private static float Yaw(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    private static void SetHour(float h)
    {
        if (DayNightCycle.Instance != null) DayNightCycle.Instance.SetTimeOfDay(h);
    }

    private static Vector3 Kuntur()
    {
        var p = SimpleThirdPersonController.Instance;
        return p != null ? p.transform.position : Vector3.zero;
    }

    private static void Park() => Place(new Vector3(-84.9f, 0f, -105.6f), 180f);

    private static float Ground(float x, float z)
    {
        var p = SimpleThirdPersonController.Instance;
        float best = 0f;
        bool found = false;
        foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, 120f, z), Vector3.down, 240f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (p != null && hit.collider.transform.IsChildOf(p.transform)) continue;
            if (hit.collider.GetComponentInParent<DrivableVehicle>() != null) continue;
            if (hit.collider.GetComponentInParent<AnimalWander>() != null) continue;
            if (hit.collider.GetComponentInParent<DialogueNPC>() != null) continue;
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
