using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Los días de Kuntur. Cada día empieza a las 8 de la mañana en la puerta de
// su casa (en una esquina del pueblo) y termina a las 9 de la noche:
//   - A las 9 el reloj se detiene, el tiempo de la misión se congela y un
//     cartel dice que ya es hora de descansar. La ruta morada lleva a casa.
//   - En la puerta de la casa, "E Descansar": la pantalla se oscurece, pasa
//     la noche (el reloj corre de 21:00 a 08:00) y aparece "AVANZAR".
//   - Al avanzar se guarda la partida y Kuntur sale de nuevo por su puerta:
//     "¡Es un nuevo día!".
// Así la jornada tiene un ritmo (salir, ayudar, volver a casa) y el progreso
// queda guardado día por día.
public class DayManager : MonoBehaviour
{
    public static DayManager Instance { get; private set; }

    [Header("Casa de Kuntur")]
    [SerializeField] private SimpleThirdPersonController player;
    [SerializeField] private Transform homeSpawn;       // en la vereda, frente a la puerta
    [SerializeField] private float endHour = 21f;       // 9 de la noche
    [SerializeField] private float morningHour = 8f;
    [SerializeField] private float earliestRestHour = 18f;

    [Header("Transición de la noche")]
    [SerializeField] private CanvasGroup nightOverlay;
    [SerializeField] private TMP_Text nightTitle;
    [SerializeField] private TMP_Text nightSubtitle;
    [SerializeField] private TMP_Text nightClock;
    [SerializeField] private GameObject advanceButton;
    [SerializeField] private TMP_Text savedText;
    [SerializeField] private float nightSeconds = 12f;
    [SerializeField] private NightSkyAnimator nightSky;

    [Header("HUD")]
    [SerializeField] private TMP_Text dayLabel;

    public bool DayOver { get; private set; }
    public bool Sleeping { get; private set; }
    public int Day => SaveSystem.Current.day;
    public Vector3 HomePosition => homeSpawn != null ? homeSpawn.position : Vector3.zero;
    public bool CanRest => DayOver || (DayNightCycle.Instance != null && DayNightCycle.Instance.CurrentHour >= earliestRestHour);

    private bool waitingAdvance;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        // Cada vez que se entra a la escena se lee lo guardado en disco: así
        // "Reintentar" vuelve al último guardado y no a lo de la partida perdida.
        SaveSystem.Reload();
        SimpleThirdPersonController.Frozen = false;

        if (nightOverlay != null)
        {
            nightOverlay.alpha = 0f;
            nightOverlay.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        SaveData save = SaveSystem.Current;
        if (ValleyHealthManager.Instance != null) ValleyHealthManager.Instance.SetHealth(save.health);
        if (DayNightCycle.Instance != null) DayNightCycle.Instance.SetTimeOfDay(morningHour);
        PlacePlayerAtHome();
        UpdateDayLabel();

        StartCoroutine(Greeting(save.day == 1 && save.missionsDone == 0));
    }

    private IEnumerator Greeting(bool firstDay)
    {
        yield return new WaitForSeconds(0.8f);
        // v56: con el tutorial en pantalla el saludo espera a que termine
        // (antes aparecía por detrás del cuadro del tutorial).
        if (TutorialUI.Pending || TutorialUI.Showing)
        {
            while (TutorialUI.Pending || TutorialUI.Showing) yield return null;
            yield return new WaitForSeconds(0.8f);
        }
        if (MissionDirector.Instance == null) yield break;
        if (firstDay)
            MissionDirector.Instance.ShowBanner("¡BUENOS DÍAS, KUNTUR!",
                "Día 1. El valle te necesita: abre el mapa con M y busca el \"!\" de quien pide ayuda.", -1, 6f);
        else
            MissionDirector.Instance.ShowBanner($"¡ES UN NUEVO DÍA! · DÍA {Day}", "Hora de continuar. El valle cuenta contigo.", -1, 6f);
    }

    private void Update()
    {
        if (waitingAdvance)
        {
            Keyboard k = Keyboard.current;
            if (k != null && (k.enterKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                Advance();
            return;
        }

#if UNITY_EDITOR
        // Atajo de prueba (solo en el editor): F10 adelanta el reloj a las 8:58 PM.
        if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame && DayNightCycle.Instance != null && !DayOver)
            DayNightCycle.Instance.SetTimeOfDay(20.97f);
#endif
        if (Sleeping || DayOver || DayNightCycle.Instance == null) return;
        float hour = DayNightCycle.Instance.CurrentHour;
        if (hour >= endHour && hour < 23.9f) EndOfDay();
    }

    private void EndOfDay()
    {
        DayOver = true;
        DayNightCycle.Instance.SetTimeOfDay(endHour);
        DayNightCycle.Instance.Paused = true;
        if (MissionDirector.Instance != null)
        {
            MissionDirector.Instance.PauseForNight();
            MissionDirector.Instance.ShowBanner("¡YA SON LAS 9 DE LA NOCHE!",
                "Continuemos mañana, es hora de ir a descansar. Sigue la ruta morada hasta tu casa.", -1, 9f);
        }
        if (RouteGuide.Instance != null && homeSpawn != null) RouteGuide.Instance.SetCustomTarget(homeSpawn.position);
    }

    // Lo llama HomeRest (la puerta de la casa).
    public void RequestRest()
    {
        if (Sleeping) return;
        if (!CanRest)
        {
            if (MissionDirector.Instance != null)
                MissionDirector.Instance.ShowBanner("TODAVÍA ES TEMPRANO", "Se descansa desde las 6 de la tarde. ¡Aún hay tiempo para ayudar al valle!", -1, 4f);
            return;
        }
        StartCoroutine(NightRoutine());
    }

    private IEnumerator NightRoutine()
    {
        Sleeping = true;
        SimpleThirdPersonController.Frozen = true;
        if (MissionDirector.Instance != null) MissionDirector.Instance.PauseForNight();
        if (DayNightCycle.Instance != null) DayNightCycle.Instance.Paused = true;
        if (RouteGuide.Instance != null) RouteGuide.Instance.ClearCustomTarget();

        float startHour = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentHour : endHour;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySleepMusic();

        if (nightOverlay != null)
        {
            nightOverlay.gameObject.SetActive(true);
            if (nightSky != null) nightSky.Restart();
            nightOverlay.blocksRaycasts = true;
            if (advanceButton != null) advanceButton.SetActive(false);
            if (savedText != null) savedText.text = "";
            if (nightTitle != null) nightTitle.text = "Buenas noches, Kuntur";
            if (nightSubtitle != null) nightSubtitle.text = "La noche pasa...";
            yield return Fade(nightOverlay, 1f, 1f);
        }

        // La noche pasa: el reloj corre hasta las 8 de la mañana.
        float hoursToPass = (24f - startHour) + morningHour;
        float t = 0f;
        while (t < nightSeconds)
        {
            t += Time.deltaTime;
            float hour = Mathf.Repeat(startHour + hoursToPass * Mathf.SmoothStep(0f, 1f, t / nightSeconds), 24f);
            if (nightClock != null) nightClock.text = FormatHour(hour);
            yield return null;
        }

        // Nuevo día: se guarda.
        SaveData save = SaveSystem.Current;
        save.day++;
        if (ValleyHealthManager.Instance != null) save.health = ValleyHealthManager.Instance.CurrentHealth;
        if (MissionDirector.Instance != null)
        {
            save.stars = MissionDirector.Instance.TotalStars;
            save.missionsDone = MissionDirector.Instance.MissionsDone;
        }
        SaveSystem.SaveCurrent();

        if (nightSky != null) nightSky.ShowDawn();
        SimpleThirdPersonController.AddEnergy(1f); // durmió bien: energía llena
        if (nightTitle != null) nightTitle.text = $"Día {save.day}";
        if (nightSubtitle != null) nightSubtitle.text = "¡Ya amaneció en el valle del Mantaro!";
        if (nightClock != null) nightClock.text = FormatHour(morningHour);
        if (savedText != null) savedText.text = "Progreso guardado";
        if (advanceButton != null) advanceButton.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        waitingAdvance = true;
    }

    // Botón "AVANZAR" (o Enter / E / Espacio).
    public void Advance()
    {
        if (!waitingAdvance) return;
        waitingAdvance = false;
        StartCoroutine(MorningRoutine());
    }

    private IEnumerator MorningRoutine()
    {
        if (DayNightCycle.Instance != null)
        {
            DayNightCycle.Instance.SetTimeOfDay(morningHour);
            DayNightCycle.Instance.Paused = false;
        }
        DayOver = false;
        if (AudioManager.Instance != null)
        {
            if (MissionDirector.Instance != null && MissionDirector.Instance.IsCleaning) AudioManager.Instance.PlayMissionMusic();
            else AudioManager.Instance.PlayGameMusic();
        }
        PlacePlayerAtHome();
        UpdateDayLabel();

        if (nightOverlay != null)
        {
            nightOverlay.blocksRaycasts = false;
            yield return Fade(nightOverlay, 0f, 1.2f);
            nightOverlay.gameObject.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SimpleThirdPersonController.Frozen = false;
        Sleeping = false;

        if (MissionDirector.Instance != null)
        {
            MissionDirector.Instance.ShowBanner($"¡ES UN NUEVO DÍA! · DÍA {Day}", "Hora de continuar. ¡Vamos, Kuntur!", -1, 5f);
            MissionDirector.Instance.OnNewDay();
        }
    }

    private void PlacePlayerAtHome()
    {
        if (player == null || homeSpawn == null) return;
        player.Teleport(homeSpawn.position, homeSpawn.eulerAngles.y);
    }

    private void UpdateDayLabel()
    {
        if (dayLabel != null) dayLabel.text = $"DÍA {Day}";
    }

    private static IEnumerator Fade(CanvasGroup group, float target, float duration)
    {
        float start = group.alpha;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, time / duration));
            yield return null;
        }
        group.alpha = target;
    }

    private static string FormatHour(float hour)
    {
        int h = Mathf.FloorToInt(hour) % 24;
        int m = Mathf.FloorToInt((hour - Mathf.Floor(hour)) * 60f);
        string ampm = h >= 12 ? "PM" : "AM";
        int h12 = h % 12 == 0 ? 12 : h % 12;
        return $"{h12}:{m:00} {ampm}";
    }
}
