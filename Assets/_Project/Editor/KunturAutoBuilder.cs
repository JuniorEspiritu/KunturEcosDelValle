using UnityEditor;
using UnityEngine;

// Reconstruye la escena automáticamente cuando llegan cambios nuevos del
// generador, sin tener que acordarse de ir al menú Kuntur > Construir Escena
// Completa. Solo corre UNA vez por versión: guarda la versión ya construida en
// EditorPrefs y, si coincide, no vuelve a tocar nada, así el trabajo hecho a
// mano en el Editor no se pisa en cada recompilación.
//
// Para forzar otra reconstrucción basta con subir SceneVersion, o usar el
// menú Kuntur > Reconstruir Ahora.
[InitializeOnLoad]
public static class KunturAutoBuilder
{
    // Subir este número en cada entrega con cambios de escena.
    public const string SceneVersion = "v58b-mirador-alto-casa-kuntur";
    public const string VersionKey = "Kuntur_EscenaConstruida";

    // Reconstrucción automática ENCENDIDA otra vez.
    //
    // Estuvo apagada mientras Unity crasheaba al abrir por el driver gráfico
    // (DirectX 12 + Intel); eso ya se arregló forzando DirectX 11 en los
    // ProjectSettings. Apagada, el efecto secundario era peor: llegaban
    // cambios nuevos del generador, compilaban bien, y la escena seguía
    // siendo la de antes hasta que alguien se acordaba de ir al menú. Se veía
    // exactamente igual que si el código no hubiera cambiado nada.
    //
    // Sigue corriendo UNA sola vez por versión (ver SceneVersion), así que no
    // pisa el trabajo hecho a mano en cada recompilación.
    private const bool EnableAutoBuild = true;

    static KunturAutoBuilder()
    {
        if (EnableAutoBuild) EditorApplication.delayCall += TryBuild;
    }

    private static void TryBuild()
    {
        // Nunca en medio de una compilación, un import o el modo Play.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryBuild;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetString(VersionKey, string.Empty) == SceneVersion) return;

        // La versión se marca ANTES de construir. Si algo falla a mitad de
        // camino, el editor no se queda reintentando lo mismo en cada recarga
        // (que es como un proyecto termina sin poder abrirse).
        EditorPrefs.SetString(VersionKey, SceneVersion);

        Debug.Log($"[Kuntur] Reconstruyendo la escena con los cambios nuevos ({SceneVersion})...");

        try
        {
            // Sin el cartel de "Listo": es modal y deja el editor esperando.
            KunturSceneBuilder.SuppressDialog = true;
            KunturSceneBuilder.BuildAll();
            Debug.Log("[Kuntur] Escena reconstruida.");
        }
        catch (System.Exception error)
        {
            Debug.LogError($"[Kuntur] La escena no se pudo reconstruir: {error}");
        }
        finally
        {
            KunturSceneBuilder.SuppressDialog = false;
        }
    }

    [MenuItem("Kuntur/Reconstruir Ahora (forzar)")]
    private static void ForceRebuild()
    {
        EditorPrefs.DeleteKey(VersionKey);
        EditorPrefs.SetString(VersionKey, SceneVersion);
        KunturSceneBuilder.BuildAll();
    }
}
