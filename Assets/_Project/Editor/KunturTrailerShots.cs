using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tráiler: se dispara con Logs/trailer.request o con el menú Kuntur.
// Entra a Play desde el menú principal y deja que
// KunturTrailerCapture grabe las tomas; al terminar sale de Play.
[InitializeOnLoad]
public static class KunturTrailerShots
{
    private const string Request = "Logs/trailer.request";
    private const string StateKey = "Kuntur_TrailerShots";

    static KunturTrailerShots()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    [MenuItem("Kuntur/Grabar tráiler")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) return;
        Directory.CreateDirectory("Trailer/frames");
        SessionState.SetInt(StateKey, 1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MenuPrincipal.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(StateKey, 0) == 1)
        {
            SessionState.SetInt(StateKey, 2);
            new GameObject("KunturTrailerCapture").AddComponent<KunturTrailerCapture>();
        }
        if (change == PlayModeStateChange.EnteredEditMode) SessionState.SetInt(StateKey, 0);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Request))
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try { File.Delete(Request); } catch { }
            Begin();
            return;
        }
        if (EditorApplication.isPlaying && SessionState.GetInt(StateKey, 0) == 2 && KunturTrailerCapture.Done)
        {
            KunturTrailerCapture.Done = false;
            EditorApplication.isPlaying = false;
        }
    }
}
