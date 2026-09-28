using UnityEngine;

// Grillos: aparecen cuando cae la noche y se van al amanecer, siguiendo la
// hora real del DayNightCycle. No se prenden de golpe a las 19:30 - suben
// mientras el sol se va, igual que en el campo.
//
// El volumen base lo pone quien lo coloca en la escena: los que están junto al
// río van más fuerte, porque es donde de verdad hay agua, pasto y bichos.
[RequireComponent(typeof(AudioSource))]
public class NightAmbience : MonoBehaviour
{
    [SerializeField] private float maxVolume = 0.3f;

    [Header("Franja horaria")]
    [SerializeField] private float fadeInStart = 18.5f;  // empieza a oírse
    [SerializeField] private float fullVolumeAt = 20.5f; // ya a tope
    [SerializeField] private float fadeOutStart = 4.8f;  // empieza a irse
    [SerializeField] private float silentAt = 6.3f;      // ya no se oye

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
    }

    private void Start()
    {
        // Suena siempre, y lo que cambia es el volumen. Arrancar y parar el
        // clip cada vez que cruza el umbral se oye como un corte; además, al
        // volver a arrancar empezaría desde el principio del loop.
        if (source.clip != null) source.Play();
    }

    private void Update()
    {
        if (DayNightCycle.Instance == null) return;

        float hour = DayNightCycle.Instance.CurrentHour;
        float target = maxVolume * NightFactor(hour) * AudioVolumeSettings.Ambience;
        source.volume = Mathf.Lerp(source.volume, target, Time.deltaTime * 0.8f);
    }

    // 0 de día, 1 de noche, con subida y bajada suaves. Se parte en dos casos
    // porque la noche cruza la medianoche: de 18:30 a 24 y de 0 a 6:30.
    private float NightFactor(float hour)
    {
        if (hour >= fadeInStart) return Mathf.InverseLerp(fadeInStart, fullVolumeAt, hour);
        if (hour <= fadeOutStart) return 1f;
        if (hour <= silentAt) return 1f - Mathf.InverseLerp(fadeOutStart, silentAt, hour);
        return 0f;
    }

    // Lo usa el generador de escena para subirle el volumen a los que quedan
    // cerca del río.
    public void SetMaxVolume(float value) => maxVolume = value;
}
