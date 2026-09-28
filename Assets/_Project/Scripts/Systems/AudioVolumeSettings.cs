using System;
using UnityEngine;

// Volumen por CATEGORÍA, separado del volumen general de Ajustes del menú.
//
// Hace falta porque "bajar el sonido" no es una sola cosa: uno quiere dejar el
// ambiente del río y los grillos y apagar la música, o al revés. Con un solo
// control eso no se puede.
//
// No es un MonoBehaviour: es un ajuste del jugador, no algo que viva en un
// objeto de una escena. Así sobrevive a los cambios de escena sin tener que
// arrastrar un GameObject de una a otra.
public static class AudioVolumeSettings
{
    private const string MusicKey = "Kuntur_VolMusica";
    private const string AmbienceKey = "Kuntur_VolAmbiente";
    private const string SfxKey = "Kuntur_VolEfectos";

    // Avisa a todas las fuentes que ya están sonando para que se actualicen
    // en el momento, sin esperar a que se vuelvan a crear.
    public static event Action OnChanged;

    private static float music = -1f;
    private static float ambience = -1f;
    private static float sfx = -1f;

    public static float Music
    {
        get { EnsureLoaded(); return music; }
        set { EnsureLoaded(); music = Mathf.Clamp01(value); Save(MusicKey, music); }
    }

    public static float Ambience
    {
        get { EnsureLoaded(); return ambience; }
        set { EnsureLoaded(); ambience = Mathf.Clamp01(value); Save(AmbienceKey, ambience); }
    }

    public static float Sfx
    {
        get { EnsureLoaded(); return sfx; }
        set { EnsureLoaded(); sfx = Mathf.Clamp01(value); Save(SfxKey, sfx); }
    }

    // Carga perezosa: el primero que pregunte por un volumen lo lee del disco.
    // Evita depender de que algún objeto haya corrido su Awake antes.
    private static void EnsureLoaded()
    {
        if (music >= 0f) return;
        music = PlayerPrefs.GetFloat(MusicKey, 1f);
        ambience = PlayerPrefs.GetFloat(AmbienceKey, 1f);
        sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
    }

    private static void Save(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
}
