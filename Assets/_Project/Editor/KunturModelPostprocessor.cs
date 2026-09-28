using UnityEditor;
using UnityEngine;

// Ajustes de importación del Kuntur rigueado, aplicados por código.
//
// Puestos a mano en el Inspector se pierden en cuanto alguien reimporta el
// FBX (o lo vuelve a copiar), y entonces el cóndor aparece con una segunda
// cámara pegada, sin animación, o dando un solo paso y quedándose tieso. Acá
// quedan fijos: el importador los vuelve a aplicar cada vez.
public class KunturModelPostprocessor : AssetPostprocessor
{
    public const string RiggedModelPath = "Assets/_Project/Art/Kuntur/Kuntur_Rig.fbx";
    public const string WalkClipName = "Kuntur_Caminar";

    // El FBX viejo (la caminata de pasarela que quedó fea). Ya no se usa: se
    // le apaga la animación para que no genere clips que alguien pueda
    // enganchar por error. Se puede borrar del proyecto sin problema.
    private const string RetiredModelPath = "Assets/_Project/Art/Kuntur/Kuntur_Caminando.fbx";

    private bool IsKunturRig => assetPath.Replace('\\', '/') == RiggedModelPath;
    private bool IsRetiredModel => assetPath.Replace('\\', '/') == RetiredModelPath;

    // IMPORTANTE: Unity NO vuelve a importar un modelo solo porque cambie
    // este script. Se queda con los ajustes con los que lo importó la primera
    // vez, para siempre. La única forma de que note el cambio es subirle esta
    // versión. Si se toca cualquier ajuste de OnPreprocessModel de abajo y no
    // se sube este número, el cambio no surte efecto y parece que el código
    // no hizo nada.
    // (Se queda en 3 a propósito: subirla reimporta TODOS los modelos del
    // proyecto. Los FBX del Kuntur nuevo los reimporta a mano, una sola vez,
    // KunturSceneBuilder.ForceKuntur2Import.)
    public override uint GetVersion() => 3;

    // ---------------------------------------------------------------
    // v53: el Kuntur nuevo, rigueado en Mixamo (carpeta Art/Kuntur2).
    // ---------------------------------------------------------------
    // Cada animación vino en su propio FBX, todas sobre el MISMO esqueleto
    // (mixamorig:*). Se importan como Generic: así los clips de un archivo se
    // pueden reproducir sobre el modelo de otro sin ningún retargeting, tal
    // cual los hizo Mixamo. El modelo que se ve en el juego es el de
    // "estarquieto"; el resto solo aporta su clip.
    public const string Kuntur2Dir = "Assets/_Project/Art/Kuntur2";
    public const string Kuntur2ModelPath = Kuntur2Dir + "/Kuntur2_estarquieto.fbx";

    // nombre del archivo -> (clip, en bucle, primer y último frame a 30 fps; -1 = entero)
    private static readonly (string file, string clip, bool loop, int first, int last)[] Kuntur2Clips =
    {
        ("estarquieto", "K2_Quieto", true, -1, -1),
        ("caminar", "K2_Caminar", true, -1, -1),
        ("correr", "K2_Correr", true, -1, -1),
        ("saltar", "K2_Saltar", false, -1, -1),
        ("movimiento", "K2_QuietoLargo", false, -1, -1),
        ("triste", "K2_Triste", false, -1, -1),
        ("hablar1", "K2_Hablar", true, -1, -1),
        ("hablar2", "K2_HablarSi", false, -1, -1),
        // recojer dura 9.6 s pero el gesto útil (bajar, agarrar, subir) va
        // de 0.3 a 5.2 s; lo demás es estar parado.
        ("recojer", "K2_Recoger", false, 9, 156),
        // recojer2: arrodillarse, llenar el frasco y pararse (0.2 a 4.8 s).
        ("recojer2", "K2_Muestra", false, 6, 144),
        ("nadar1", "K2_Flotar", true, -1, -1),
        ("nadar2", "K2_Nadar", true, -1, -1),
        // Misión cumplida: baila en bucle hasta que se aprieta CONTINUAR.
        ("baile", "K2_Baile", true, -1, -1),
        // v55b: saluda con el ala mientras se muestra el tutorial.
        ("saludo", "K2_Saludo", true, -1, -1),
        // v56: subirse al tuk tuk (una vez) y manejarlo (en bucle).
        ("subircarro", "K2_SubirCarro", false, -1, -1),
        ("conducir", "K2_Conducir", true, -1, -1),
    };

    public static string Kuntur2ClipName(string file)
    {
        foreach (var c in Kuntur2Clips) if (c.file == file) return c.clip;
        return null;
    }

    private string Kuntur2File
    {
        get
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Kuntur2Dir + "/Kuntur2_") || !path.ToLowerInvariant().EndsWith(".fbx")) return null;
            return System.IO.Path.GetFileNameWithoutExtension(path).Substring("Kuntur2_".Length);
        }
    }

    private void PreprocessKuntur2(string file)
    {
        ModelImporter importer = (ModelImporter)assetImporter;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        // Sin nodo de movimiento: la cadera se anima tal cual y
        // KunturMixamoAnimator le quita el avance de las caminatas.
        importer.motionNodeName = "";
        // El material lo pone el generador de escena con la textura sacada
        // del GLB de Tripo (el FBX apunta a archivos que no están acá).
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importNormals = ModelImporterNormals.Import;
        // 500 mil vértices: comprimir la malla aliviana el proyecto sin que
        // se note a la distancia de la cámara.
        importer.meshCompression = ModelImporterMeshCompression.Low;
        importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
    }

    private void PreprocessKuntur2Clips(string file)
    {
        ModelImporter importer = (ModelImporter)assetImporter;
        (string file, string clip, bool loop, int first, int last) def = default;
        bool found = false;
        foreach (var c in Kuntur2Clips) if (c.file == file) { def = c; found = true; }
        if (!found) return;

        ModelImporterClipAnimation[] source = importer.defaultClipAnimations;
        if (source == null || source.Length == 0) return;

        // Mixamo trae un solo take ("mixamo.com"): ese es el clip.
        ModelImporterClipAnimation clip = source[0];
        clip.name = def.clip;
        clip.loopTime = def.loop;
        clip.loopPose = false;
        if (def.first >= 0) clip.firstFrame = def.first;
        if (def.last >= 0) clip.lastFrame = Mathf.Min(def.last, clip.lastFrame);
        clip.lockRootRotation = false;
        clip.lockRootHeightY = false;
        clip.lockRootPositionXZ = false;
        importer.clipAnimations = new[] { clip };
    }

    private void OnPreprocessModel()
    {
        string k2 = Kuntur2File;
        if (k2 != null) { PreprocessKuntur2(k2); return; }

        if (IsRetiredModel)
        {
            ModelImporter retired = (ModelImporter)assetImporter;
            retired.importAnimation = false;
            retired.animationType = ModelImporterAnimationType.None;
            retired.importCameras = false;
            retired.importLights = false;
            return;
        }

        if (!IsKunturRig) return;

        ModelImporter importer = (ModelImporter)assetImporter;

        // El FBX salió de Blender y trae una cámara y una luz adentro. Si se
        // importan, cada Kuntur que se instancia mete una segunda cámara y una
        // luz extra en la escena, y el juego se ve desde el lugar equivocado.
        importer.importCameras = false;
        importer.importLights = false;

        // Generic, y OJO con esto: tiene que ser Generic y no None.
        //
        // Con None, Unity descarta el esqueleto y trae la malla como objeto
        // estático: se pierden los 28 huesos, no hay nada que animar y el
        // cóndor queda tieso y sin textura. Con Generic conserva el skinning.
        //
        // El Animator que Unity engancha junto con el Avatar lo quita después
        // el generador de escena (ver SetupKunturRig): un Animator sin clip
        // le impone su propia pose al esqueleto y pelea con la animación por
        // código. Y NO se declara nodo de movimiento: extraer la raíz
        // "Armature" es justo lo que rompe el skinning cuando no hay
        // animación que separar.
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false; // el modelo no trae clips

        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }

    private void OnPreprocessAnimation()
    {
        string k2 = Kuntur2File;
        if (k2 != null) { PreprocessKuntur2Clips(k2); return; }

        if (!IsKunturRig) return;

        ModelImporter importer = (ModelImporter)assetImporter;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].name = clips.Length == 1 ? WalkClipName : $"{WalkClipName}_{i + 1}";
            // Sin esto el cóndor da UN paso y se queda congelado: el clip dura
            // 1.4 s y sin loop no se repite.
            clips[i].loopTime = true;
        }

        importer.clipAnimations = clips;
    }
}
