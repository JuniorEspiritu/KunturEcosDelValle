using System.IO;
using UnityEditor;
using UnityEngine;

// Diagnóstico de audio (solo editor): creando el archivo
// Logs/audiocheck.request escribe en la consola el estado de la música.
[InitializeOnLoad]
public static class KunturAudioCheck
{
    private const string Request = "Logs/audiocheck.request";
    private const string FixRequest = "Logs/audiofix.request";

    static KunturAudioCheck()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (File.Exists(FixRequest))
        {
            try { File.Delete(FixRequest); } catch { }
            RestoreMusic();
        }
        if (!File.Exists(Request)) return;
        try { File.Delete(Request); } catch { }
        Run();
    }

    // La barra de "Música" de la pausa había quedado casi en cero (1%):
    // por eso no se escuchaba nada en el menú ni en la historia.
    [MenuItem("Kuntur/Restaurar volumen de la música")]
    public static void RestoreMusic()
    {
        EditorUtility.audioMasterMute = false; // el "Mute Audio" de la ventana Game
        PlayerPrefs.SetFloat("Kuntur_VolMusica", 1f);
        PlayerPrefs.SetFloat("Kuntur_VolAmbiente", 1f);
        PlayerPrefs.Save();
        Debug.Log("[Kuntur] Audio: volumen de música y ambiente restaurados al 100%.");
        Run();
    }

    [MenuItem("Kuntur/Revisar música")]
    public static void Run()
    {
        Debug.Log($"[Kuntur] Audio: mute del editor = {EditorUtility.audioMasterMute}, volumen música guardado = {PlayerPrefs.GetFloat("Kuntur_VolMusica", -1f)}, " +
                  $"ambiente = {PlayerPrefs.GetFloat("Kuntur_VolAmbiente", -1f)}, efectos = {PlayerPrefs.GetFloat("Kuntur_VolEfectos", -1f)}, " +
                  $"AudioListener.volume = {AudioListener.volume}, dispositivo = {AudioSettings.GetConfiguration().speakerMode}/{AudioSettings.outputSampleRate}");
        foreach (string name in new[] { "Musica_Menu.ogg", "Musica_Historia.mp3", "Musica_Aventura.mp3", "Musica_Juego.ogg" })
        {
            string path = "Assets/_Project/Art/Audio/" + name;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            AudioImporter imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (clip == null) { Debug.Log($"[Kuntur] Audio: {name} NO CARGA"); continue; }
            Debug.Log($"[Kuntur] Audio: {name} dura {clip.length:0.0}s, canales {clip.channels}, {clip.frequency} Hz, carga {clip.loadType}, estado {clip.loadState}, " +
                      $"import {(imp != null ? imp.defaultSampleSettings.loadType + "/" + imp.defaultSampleSettings.compressionFormat : "?")}");
        }
    }
}
