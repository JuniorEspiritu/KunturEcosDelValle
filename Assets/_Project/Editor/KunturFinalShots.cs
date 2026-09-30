using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Final del tráiler: se dispara con Logs/final.request (si adentro dice
// "preview", solo saca fotos chicas para revisar encuadres) o con el menú
// Kuntur. Entra a Play desde el menú principal, deja que KunturFinalCapture
// grabe todo en Trailer/frames/final y al terminar sale de Play.
[InitializeOnLoad]
public static class KunturFinalShots
{
    private const string Request = "Logs/final.request";
    private const string StateKey = "Kuntur_FinalShots";
    private const string PreviewKey = "Kuntur_FinalShotsPreview";

    static KunturFinalShots()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    [MenuItem("Kuntur/Grabar final del tráiler")]
    public static void BeginFull() => Begin(false);

    [MenuItem("Kuntur/Grabar final del tráiler (prueba rápida)")]
    public static void BeginPreview() => Begin(true);

    private static void Begin(bool preview)
    {
        if (EditorApplication.isPlaying) return;
        Directory.CreateDirectory(KunturFinalCapture.OutDir);
        SessionState.SetInt(StateKey, 1);
        SessionState.SetBool(PreviewKey, preview);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MenuPrincipal.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(StateKey, 0) == 1)
        {
            SessionState.SetInt(StateKey, 2);
            KunturFinalCapture.Preview = SessionState.GetBool(PreviewKey, false);
            new GameObject("KunturFinalCapture").AddComponent<KunturFinalCapture>();
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
            Begin(mode.Contains("preview"));
            return;
        }
        if (EditorApplication.isPlaying && SessionState.GetInt(StateKey, 0) == 2 && KunturFinalCapture.Done)
        {
            KunturFinalCapture.Done = false;
            EditorApplication.isPlaying = false;
        }
    }
}
