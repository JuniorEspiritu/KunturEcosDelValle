using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tráiler completo de 30 s: se dispara con Logs/t30.request (si adentro dice
// "preview", solo saca fotos chicas para revisar encuadres) o con el menú
// Kuntur. Entra a Play desde el menú principal, deja que KunturTrailer30Capture
// grabe todo en Trailer/frames/t30 y al terminar sale de Play.
[InitializeOnLoad]
public static class KunturTrailer30Shots
{
    private const string Request = "Logs/t30.request";
    private const string StateKey = "Kuntur_T30Shots";
    private const string PreviewKey = "Kuntur_T30Preview";

    static KunturTrailer30Shots()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    [MenuItem("Kuntur/Grabar tráiler 30 s")]
    public static void BeginFull() => Begin(false);

    [MenuItem("Kuntur/Grabar tráiler 30 s (prueba rápida)")]
    public static void BeginPreview() => Begin(true);

    private static void Begin(bool preview, bool introOnly = false)
    {
        if (EditorApplication.isPlaying) return;
        Directory.CreateDirectory(KunturTrailer30Capture.OutDir);
        SessionState.SetInt(StateKey, 1);
        SessionState.SetBool(PreviewKey, preview);
        SessionState.SetBool("Kuntur_T30Intro", introOnly);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MenuPrincipal.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(StateKey, 0) == 1)
        {
            SessionState.SetInt(StateKey, 2);
            KunturTrailer30Capture.Preview = SessionState.GetBool(PreviewKey, false);
            KunturTrailer30Capture.IntroOnly = SessionState.GetBool("Kuntur_T30Intro", false);
            new GameObject("KunturTrailer30Capture").AddComponent<KunturTrailer30Capture>();
        }
        if (change == PlayModeStateChange.EnteredEditMode) SessionState.SetInt(StateKey, 0);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Request))
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string mode = "";
            try { mode = File.ReadAllText(Request).Trim().ToLowerInvariant(); } catch { }
            try { File.Delete(Request); } catch { }
            Begin(mode.Contains("preview"), mode.Contains("intro"));
            return;
        }
        if (EditorApplication.isPlaying && SessionState.GetInt(StateKey, 0) == 2 && KunturTrailer30Capture.Done)
        {
            KunturTrailer30Capture.Done = false;
            EditorApplication.isPlaying = false;
        }
    }
}
