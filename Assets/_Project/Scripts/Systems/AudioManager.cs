using UnityEngine;

// Único punto de sonido del juego: la música de fondo y los efectos cortos.
//
// Los efectos NO se reproducen desde el objeto que los dispara. Un residuo se
// destruye al recogerlo, y si el AudioSource vive en él, el sonido se corta a
// la mitad. Acá se reproducen desde un objeto que no se destruye nunca.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Música")]
    [SerializeField] private AudioClip music;
    [SerializeField] private float musicVolume = 0.62f;
    [SerializeField] private float fadeInDuration = 1.5f;
    // Tema que entra al aceptar una misión (reemplaza a la música del paseo).
    [SerializeField] private AudioClip missionMusic;
    [SerializeField] private AudioClip sleepMusic;

    [Header("Efectos")]
    [SerializeField] private AudioClip trashPickup;
    [SerializeField] private AudioClip waterSample;
    [SerializeField] private AudioClip victory;
    [SerializeField] private AudioClip defeat;
    [SerializeField] private float sfxVolume = 0.95f;

    private AudioSource musicSource;
    private AudioSource sfxSource;
    private float fadeTimer;
    private float duckedVolume = -1f; // >= 0 cuando la música está bajada a propósito

    private void OnEnable() => AudioVolumeSettings.OnChanged += ApplyMusicVolume;
    private void OnDisable() => AudioVolumeSettings.OnChanged -= ApplyMusicVolume;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Dos fuentes separadas: la música tiene loop y la de efectos no.
        // Una sola fuente no puede ser las dos cosas a la vez.
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = music;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = 0f;
        musicSource.spatialBlend = 0f; // 2D: la música no viene de ningún lado del mapa

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
    }

    private void Start()
    {
        if (music == null) return;
        musicSource.Play();
    }

    private void Update()
    {
        // La música entra subiendo de a poco. Arrancar a volumen pleno junto
        // con la escena suena a golpe.
        if (music == null || fadeTimer >= fadeInDuration) return;

        fadeTimer += Time.unscaledDeltaTime;
        ApplyMusicVolume();
    }

    // Un solo lugar decide el volumen de la música: el volumen de diseño, por
    // lo que lleve del fundido de entrada, por el ajuste del jugador. Si cada
    // parte lo escribiera por su cuenta, la última en correr pisaría a las
    // otras y el control de la pausa no haría nada.
    private void ApplyMusicVolume()
    {
        if (musicSource == null) return;

        float target = duckedVolume >= 0f ? duckedVolume : musicVolume;
        float fade = fadeInDuration <= 0f ? 1f : Mathf.Clamp01(fadeTimer / fadeInDuration);
        musicSource.volume = target * fade * AudioVolumeSettings.Music;
    }

    // PlayOneShot y no Play: así dos residuos recogidos casi a la vez suenan
    // los dos, en vez de que el segundo corte al primero.
    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale * AudioVolumeSettings.Sfx);
    }

    public void PlayTrashPickup() => PlaySfx(trashPickup);
    public void PlayWaterSample() => PlaySfx(waterSample);
    public void PlayVictory() => PlaySfx(victory);
    public void PlayDefeat() => PlaySfx(defeat);

    public void PlayMissionMusic()
    {
        if (missionMusic == null || musicSource == null || musicSource.clip == missionMusic) return;
        musicSource.clip = missionMusic;
        musicSource.Play();
        fadeTimer = 0f; // entra con fundido, no de golpe
        ApplyMusicVolume();
    }

    // Música suave mientras Kuntur duerme.
    public void PlaySleepMusic()
    {
        if (sleepMusic == null || musicSource == null || musicSource.clip == sleepMusic) return;
        musicSource.clip = sleepMusic;
        musicSource.Play();
        fadeTimer = 0f;
        duckedVolume = -1f;
        ApplyMusicVolume();
    }

    // Vuelve a la música del paseo al terminar la misión.
    public void PlayGameMusic()
    {
        if (music == null || musicSource == null || musicSource.clip == music) return;
        musicSource.clip = music;
        musicSource.Play();
        fadeTimer = 0f;
        duckedVolume = -1f;
        ApplyMusicVolume();
    }

    // Baja la música para que un jingle de final se escuche por encima.
    public void DuckMusic(float target = 0.12f)
    {
        duckedVolume = target;
        fadeTimer = fadeInDuration; // corta el fundido de entrada si seguía corriendo
        ApplyMusicVolume();
    }
}
