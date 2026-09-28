using System;
using UnityEngine;

// Ciclo de hora del día del valle: mueve el sol, cambia el color de la luz,
// del ambiente y del cielo, y publica la hora para que el HUD la muestre.
// No es solo decoración: el GDD plantea que la jornada avanza mientras Kuntur
// limpia, y que el clímax ocurre al atardecer (la escena del río al ocaso).
// Que el jugador vea caer la tarde mientras quedan residuos por recoger es lo
// que hace sentir la urgencia del ODS 11 sin escribirla en un cartel.
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [Header("Tiempo")]
    [SerializeField] private float startHour = 8f;              // arranca de mañana
    [SerializeField] private float minutesPerFullDay = 14f;     // cuánto dura un día completo en tiempo real
    [SerializeField] private bool advanceAutomatically = true;

    [Header("Sol")]
    [SerializeField] private Light sun;
    [SerializeField] private float dayIntensity = 1.15f;
    [SerializeField] private float nightIntensity = 0.18f;
    [SerializeField] private float ambientStrength = 0.95f;

    [Header("Colores")]
    [SerializeField] private Color dayColor = new Color(1f, 0.96f, 0.88f);
    [SerializeField] private Color sunsetColor = new Color(1f, 0.55f, 0.28f);
    [SerializeField] private Color nightColor = new Color(0.35f, 0.42f, 0.7f);
    [SerializeField] private Color daySky = new Color(0.53f, 0.76f, 0.96f);
    [SerializeField] private Color sunsetSky = new Color(0.85f, 0.42f, 0.35f);
    [SerializeField] private Color nightSky = new Color(0.08f, 0.09f, 0.19f);

    private float currentHour;
    private int lastReportedMinute = -1;

    public event Action<string> OnTimeChanged; // "18:45"

    public float CurrentHour => currentHour;
    // Lo congela DayManager a las 9 de la noche: el reloj no sigue hasta que
    // Kuntur se va a dormir.
    public bool Paused { get; set; }
    public bool IsNight => currentHour < 6f || currentHour >= 19.5f;

    public string FormattedTime
    {
        get
        {
            int hours = Mathf.FloorToInt(currentHour) % 24;
            int minutes = Mathf.FloorToInt((currentHour - Mathf.Floor(currentHour)) * 60f);
            return $"{hours:00}:{minutes:00}";
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        currentHour = startHour;

        if (sun == null)
        {
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) { sun = light; break; }
            }
        }
    }

    private void Start()
    {
        ApplyTime();
    }

    private void Update()
    {
        if (advanceAutomatically && !Paused && minutesPerFullDay > 0.01f)
        {
            // 24 horas repartidas en los minutos reales configurados.
            currentHour += (24f / (minutesPerFullDay * 60f)) * Time.deltaTime;
            if (currentHour >= 24f) currentHour -= 24f;
        }

        ApplyTime();
    }

    // Para el clímax: saltar directo al atardecer, como la imagen del río al
    // ocaso en el GDD.
    public void SetTimeOfDay(float hour)
    {
        currentHour = Mathf.Repeat(hour, 24f);
        ApplyTime();
    }

    public void JumpToSunset() => SetTimeOfDay(18.4f);

    private void ApplyTime()
    {
        float blend;
        Color lightColor;
        Color skyColor;
        float intensity;

        if (currentHour >= 6f && currentHour < 9f)
        {
            // Amanecer: de noche a día.
            blend = Mathf.InverseLerp(6f, 9f, currentHour);
            lightColor = Color.Lerp(sunsetColor, dayColor, blend);
            skyColor = Color.Lerp(sunsetSky, daySky, blend);
            intensity = Mathf.Lerp(nightIntensity, dayIntensity, blend);
        }
        else if (currentHour >= 9f && currentHour < 16.5f)
        {
            lightColor = dayColor;
            skyColor = daySky;
            intensity = dayIntensity;
        }
        else if (currentHour >= 16.5f && currentHour < 19.5f)
        {
            // Atardecer.
            blend = Mathf.InverseLerp(16.5f, 19.5f, currentHour);
            lightColor = Color.Lerp(dayColor, sunsetColor, blend);
            skyColor = Color.Lerp(daySky, sunsetSky, blend);
            intensity = Mathf.Lerp(dayIntensity, nightIntensity + 0.25f, blend);
        }
        else if (currentHour >= 19.5f && currentHour < 21f)
        {
            // Cae la noche.
            blend = Mathf.InverseLerp(19.5f, 21f, currentHour);
            lightColor = Color.Lerp(sunsetColor, nightColor, blend);
            skyColor = Color.Lerp(sunsetSky, nightSky, blend);
            intensity = Mathf.Lerp(nightIntensity + 0.25f, nightIntensity, blend);
        }
        else
        {
            lightColor = nightColor;
            skyColor = nightSky;
            intensity = nightIntensity;
        }

        if (sun != null)
        {
            // El sol sale por el este a las 6 y se pone a las 18: 15° por hora
            // partiendo de -90° (horizonte) a las 6 de la mañana.
            float sunAngle = (currentHour - 6f) * 15f;
            sun.transform.rotation = Quaternion.Euler(sunAngle, -28f, 0f);
            sun.color = lightColor;
            sun.intensity = intensity;
        }

        // Luz ambiente de tres tonos (cielo arriba, horizonte, suelo) en vez
        // de un solo gris azulado: las sombras quedan suaves y con color, y
        // el pueblo se ve claro y cálido como en el Figma, no apagado.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(skyColor, Color.white, 0.3f) * ambientStrength;
        RenderSettings.ambientEquatorColor = Color.Lerp(skyColor, lightColor, 0.55f) * ambientStrength * 0.9f;
        RenderSettings.ambientGroundColor = Color.Lerp(skyColor, new Color(0.48f, 0.5f, 0.36f), 0.6f) * ambientStrength * 0.7f;
        RenderSettings.ambientLight = RenderSettings.ambientEquatorColor;
        RenderSettings.fogColor = skyColor;

        Camera main = Camera.main;
        if (main != null && main.clearFlags == CameraClearFlags.SolidColor)
            main.backgroundColor = skyColor;

        // Avisar al HUD solo cuando cambia el minuto mostrado, no cada frame.
        int minuteNow = Mathf.FloorToInt(currentHour * 60f);
        if (minuteNow != lastReportedMinute)
        {
            lastReportedMinute = minuteNow;
            OnTimeChanged?.Invoke(FormattedTime);
        }
    }
}
