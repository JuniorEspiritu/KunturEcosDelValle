using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Fotos para la presentación: se dispara con Logs/canvashots.request o con el
// menú Kuntur. Entra a Play desde el menú principal y deja que
// KunturCanvaCapture saque todo a Logs/Canva; al terminar sale de Play.
[InitializeOnLoad]
public static class KunturCanvaShots
{
    private const string Request = "Logs/canvashots.request";
    private const string StateKey = "Kuntur_CanvaShots";

    static KunturCanvaShots()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    [MenuItem("Kuntur/Fotos para la presentación")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) return;
        Directory.CreateDirectory("Logs/Canva");
        SessionState.SetInt(StateKey, 1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MenuPrincipal.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(StateKey, 0) == 1)
        {
            SessionState.SetInt(StateKey, 2);
            new GameObject("KunturCanvaCapture").AddComponent<KunturCanvaCapture>();
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
        if (EditorApplication.isPlaying && SessionState.GetInt(StateKey, 0) == 2 && KunturCanvaCapture.Done)
        {
            KunturCanvaCapture.Done = false;
            EditorApplication.isPlaying = false;
        }
    }
}
