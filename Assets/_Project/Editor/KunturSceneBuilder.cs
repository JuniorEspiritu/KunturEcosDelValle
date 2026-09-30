using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Arma automáticamente todo lo que la guía GUIA_IMPLEMENTACION_UNITY.md pide
// hacer a mano en el Editor: Input Actions, layer de interacción, entorno de
// bloqueo, jugador+cámara, managers, mecánicas en el mundo y los Canvas de
// HUD/Diálogo/Resultado con los colores exactos de UIPalette. La idea es que
// esto corra UNA vez (Kuntur > Construir Escena Completa) y deje el juego
// jugable de inmediato; después se puede seguir puliendo a mano desde el
// Editor con normalidad.
public static partial class KunturSceneBuilder
{
    private const string InputDir = "Assets/_Project/Input";
    private const string ArtDir = "Assets/_Project/Art";
    private const string ScenesDir = "Assets/_Project/Scenes";
    private const string InteractableLayerName = "Interactable";

    private static TMP_FontAsset defaultFont;

    public static bool SuppressDialog = false;

    [MenuItem("Kuntur/Construir Escena Completa")]
    public static void BuildAll()
    {
        EnsureFolders();
        EnsureLayer(InteractableLayerName);
        defaultFont = FindDefaultFont();

        InputActionAsset controls = BuildInputActions();

        BuildExplorationScene(controls);
        BuildMainMenuScene();
        BuildIntroScene();
        AddScenesToBuildSettings();

        if (!Application.isBatchMode && !SuppressDialog)
        {
            EditorUtility.DisplayDialog(
                "Kuntur: Ecos del Valle",
                "Escena construida. Abre Assets/_Project/Scenes/MenuPrincipal.unity y dale Play, " +
                "o abre Exploracion.unity directamente para probar el juego ya.",
                "Listo");
        }
        else
        {
            Debug.Log("[KunturSceneBuilder] Escena construida con éxito.");
        }
    }

    // ---------------------------------------------------------------
    // Carpetas / Layer
    // ---------------------------------------------------------------

    private static void EnsureFolders()
    {
        CreateFolderRecursive(InputDir);
        CreateFolderRecursive(ArtDir);
        CreateFolderRecursive(ScenesDir);
        CreateFolderRecursive("Assets/_Project/Editor"); // por si acaso
    }

    private static void CreateFolderRecursive(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void EnsureLayer(string layerName)
    {
        var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManagerAssets.Length == 0) return;
        SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i <= 31; i++)
        {
            SerializedProperty sp = layers.GetArrayElementAtIndex(i);
            if (sp.stringValue == layerName) return;
        }
        for (int i = 8; i <= 31; i++)
        {
            SerializedProperty sp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return;
            }
        }
    }

    // ---------------------------------------------------------------
    // Input Actions (asset PlayerControls con sub-assets de referencia)
    // ---------------------------------------------------------------

    private static InputActionAsset BuildInputActions()
    {
        string path = InputDir + "/PlayerControls.asset";

        // El asset viejo se borra y se rehace de cero. Motivo: una versión
        // anterior quedó con acciones pero SIN algunas InputActionReference
        // (faltaba justo la de InteractPrimary, la tecla E), y eso mataba en
        // silencio toda la interacción del juego. Regenerarlo entero es más
        // seguro que intentar parchear un asset a medias.
        if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(path) != null)
            AssetDatabase.DeleteAsset(path);

        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        bool isNewAsset = asset == null;

        if (isNewAsset)
        {
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            AssetDatabase.CreateAsset(asset, path);
        }

        // FindActionMap/AddAction en vez de crear todo de cero: si el asset
        // ya existía de una corrida anterior (por ejemplo, de antes de que
        // existiera el sistema de interacción), esto agrega solo lo que
        // falte en vez de dejarlo tal cual - así una acción nueva como
        // InteractPrimary/InteractSecondary no se queda nunca sin crear.
        InputActionMap map = asset.FindActionMap("Player");
        if (map == null) map = asset.AddActionMap("Player");

        EnsureAction(map, "Move", InputActionType.Value, move =>
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d"));

        EnsureAction(map, "Look", InputActionType.Value, look => look.AddBinding("<Mouse>/delta"));
        EnsureAction(map, "Sprint", InputActionType.Button, sprint => sprint.AddBinding("<Keyboard>/leftShift"));
        EnsureAction(map, "Jump", InputActionType.Button, jump => jump.AddBinding("<Keyboard>/space"));
        EnsureAction(map, "InteractPrimary", InputActionType.Button, ip => ip.AddBinding("<Keyboard>/e"));
        EnsureAction(map, "InteractSecondary", InputActionType.Button, isec => isec.AddBinding("<Keyboard>/f"));

        // Crea la InputActionReference (sub-asset) de cada acción que todavía
        // no tenga una - necesario tanto para el asset nuevo como para uno
        // viejo al que se le acaban de agregar acciones.
        var existingRefNames = new HashSet<string>();
        foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (obj is InputActionReference existingRef) existingRefNames.Add(existingRef.name);
        }

        foreach (InputAction action in map.actions)
        {
            if (existingRefNames.Contains(action.name)) continue;

            InputActionReference reference = InputActionReference.Create(action);
            reference.name = action.name;
            AssetDatabase.AddObjectToAsset(reference, asset);
        }

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
    }

    private static void EnsureAction(InputActionMap map, string actionName, InputActionType type, System.Action<InputAction> configure)
    {
        if (map.FindAction(actionName) != null) return;
        InputAction action = map.AddAction(actionName, type);
        configure(action);
    }

    private static InputActionReference FindActionRef(InputActionAsset asset, string actionName)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (obj is InputActionReference iar && iar.name == actionName)
                return iar;
        }
        return null;
    }

    // ---------------------------------------------------------------
    // Fuente TMP por defecto
    // ---------------------------------------------------------------

    private static TMP_FontAsset FindDefaultFont()
    {
        try
        {
            string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
            if (guids.Length == 0) guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        catch { }

        try
        {
            if (TMP_Settings.instance != null)
                return TMP_Settings.defaultFontAsset;
        }
        catch { }

        return null;
    }

    // ---------------------------------------------------------------
    // Escena de Exploración
    // ---------------------------------------------------------------

    private static void BuildExplorationScene(InputActionAsset controls)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            // Sombras suaves y no negras: con sombra dura al 100% cada casa
            // dejaba un rectángulo oscuro recortado, muy "de bloques".
            Light sunLight = sun.GetComponent<Light>();
            if (sunLight != null)
            {
                sunLight.shadows = LightShadows.Soft;
                sunLight.shadowStrength = 0.62f;
                sunLight.shadowNormalBias = 0.5f;
            }
        }

        BuildEnvironment();

        Camera playerCamera = BuildPlayerAndCamera(controls);
        BuildManagers();
        BuildMechanics();
        BuildEventSystem();
        BuildHudAndDialogueCanvas(playerCamera, controls);

        // La misma cortina negra del menú: acá sirve para ENTRAR desde negro.
        // Sin esto, el menú se funde a negro y el juego aparece de golpe a
        // plena luz, que es peor que no tener transición.
        BuildSceneFader();

        string path = ScenesDir + "/Exploracion.unity";
        EditorSceneManager.SaveScene(scene, path);
    }

    private static void BuildEnvironment()
    {
        // Importante: se limpia en cada construcción, si no, la segunda vez
        // que se arma la escena el registro seguiría lleno y no se colocaría
        // nada.
        PlacedVolumes.Clear();
        ConvertedMaterials.Clear();
        lampCounter = 0;
        ResetAssetCounters();

        GameObject world = new GameObject("--- MUNDO ---");

        BuildTerrain(world.transform);
        BuildMountainRange(world.transform);
        // Las nubes de esferas se fueron: ahora las trae la foto del cielo.
        BuildRiver(world.transform);
        BuildAmbience(world.transform);
        BuildStreets(world.transform);
        // El lote del botadero y el terreno de los NPC se apartan ANTES de las
        // casas: si no, la casa que cae ahí se construye igual y la basura (o
        // Doña Rosa) queda dentro de una pared.
        Occupy(RosaSpot, 2.5f, 2.5f);
        // El parque también se aparta antes: si no, le construyen edificios encima.
        ReserveParkLot();
        ReserveCathedralLot();
        // El Plaza Vea también va ANTES de las casas: aparta su manzana y las
        // casas se acomodan alrededor en vez de quedar metidas en la tienda.
        BuildPlazaVea(world.transform);
        // v55: heladería frente a la casa de Kuntur (se aparta antes que las casas).
        BuildTownIceCreamShop(world.transform);
        BuildHouses(world.transform);
        // v53: la Calle Real sube al sur hasta el mirador (calle en bajada).
        BuildHillStreet(world.transform);
        BuildPark(world.transform);
        BuildStreetFurniture(world.transform);
        BuildVegetation(world.transform);
        BuildPedestrians(world.transform);
        BuildMorePedestrians(world.transform);
        BuildParkPeople(world.transform);
        // v58: cerro con camino en curvas y mirador, atrás de la casa de Kuntur.
        BuildNorthMirador(world.transform);
        BuildSky(world.transform);
        ConfigureAtmosphere();

        // v56: el tuk tuk de Kuntur, la red de calles para que llegue solo, y
        // todos los vehículos (tránsito y estacionados) se pueden manejar.
        BuildTukTuk(world.transform);
        BuildRoadNetwork(world.transform);
        // (v56c: solo el tuk tuk se maneja; los demás autos siguen su ruta.)
    }

    // ---------------------------------------------------------------
    // Cielo
    // ---------------------------------------------------------------
    // Tres fotos panorámicas de AllSky Free que se funden según la hora, más
    // un domo de estrellas y una luna propios. Ver SkyController.
    // Copias a 4096x2048 de las panorámicas de AllSky Free, guardadas dentro
    // del proyecto. Los originales vienen de 16384x8192 (80 MB cada uno):
    // importarlos así tarda minutos y llena la memoria de video para nada,
    // porque en el cielo no se nota la diferencia.
    private const string DaySkyPath = ArtDir + "/Cielo/Cielo_Dia.png";
    private const string SunsetSkyPath = ArtDir + "/Cielo/Cielo_Atardecer.png";
    private const string NightSkyPath = ArtDir + "/Cielo/Cielo_Noche.png";

    private static void BuildSky(Transform parent)
    {
        GameObject sky = new GameObject("Cielo");
        sky.transform.SetParent(parent);

        // Elegidas MIRÁNDOLAS, no por el nombre: la que se llama "Cold Sunset"
        // es en realidad un cielo diurno lleno de nubes con el sol brillando,
        // justo lo que hacía falta para el día. "Deep Dusk" sí es el atardecer
        // naranja, y la nocturna de dibujo es un azul limpio, perfecto para
        // ponerle estrellas encima. Las dos "cartoon" de día no servían: son
        // un degradado liso, sin una sola nube.
        Texture day = LoadSkyTexture(DaySkyPath);
        Texture sunset = LoadSkyTexture(SunsetSkyPath);
        Texture night = LoadSkyTexture(NightSkyPath);

        Material skyMaterial = GetSkyBlendMaterial(day, sunset, night);
        if (skyMaterial != null) RenderSettings.skybox = skyMaterial;

        // La noche es "Cold Night" de AllSky, que ya trae su luna pintada; por
        // eso ya no se pone una luna aparte (se verían dos lunas). Solo se le
        // suma un velo de estrellas finitas, porque esa foto no trae estrellas.
        Renderer domeRenderer = BuildStarDome(sky.transform);

        SkyController controller = sky.AddComponent<SkyController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("dayTexture").objectReferenceValue = day;
        so.FindProperty("sunsetTexture").objectReferenceValue = sunset;
        so.FindProperty("nightTexture").objectReferenceValue = night;
        so.FindProperty("starDome").objectReferenceValue = domeRenderer;
        so.FindProperty("starDomeTransform").objectReferenceValue = domeRenderer != null ? domeRenderer.transform : null;
        so.ApplyModifiedProperties();
    }

    private static Renderer BuildStarDome(Transform parent)
    {
        Material starMaterial = GetAdditiveSkyMaterial("Cielo_Estrellas", "Kuntur/StarDome",
            ArtDir + "/Cielo/Cielo_Estrellas.png");
        if (starMaterial == null) return null;

        // Por qué las estrellas se veían como manchas enormes: la textura mide
        // 2048 px para dar la vuelta entera (360°), o sea ~5.7 px por grado,
        // mientras que la pantalla muestra ~32 px por grado. Cada pixel de la
        // textura terminaba dibujado como un cuadro de 5-6 pixeles. Repitiendo
        // la textura 3 veces (en los dos ejes, para que sigan redondas) cada
        // estrella queda 3 veces más chica, del tamaño de un puntito real.
        starMaterial.mainTextureScale = new Vector2(3f, 3f);
        EditorUtility.SetDirty(starMaterial);

        // Esfera gigante alrededor del jugador: 700 de radio.
        //
        // Tiene que quedar MÁS LEJOS que la cordillera (la fila de atrás llega
        // a 480 + su falda). El domo se dibuja después del terreno y compara
        // profundidad: si estuviera por delante de una montaña, le pintaría
        // estrellas encima. Y por dentro del plano lejano de la cámara (900),
        // o no se dibujaría nada.
        GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dome.name = "Domo_Estrellas";
        dome.transform.SetParent(parent);
        dome.transform.localScale = Vector3.one * 1400f;
        dome.transform.rotation = Quaternion.Euler(0f, 210f, 0f);
        Object.DestroyImmediate(dome.GetComponent<Collider>());
        dome.isStatic = false;

        Renderer renderer = dome.GetComponent<Renderer>();
        renderer.sharedMaterial = starMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static Material GetSkyBlendMaterial(Texture day, Texture sunset, Texture night)
    {
        Shader shader = Shader.Find("Kuntur/SkyPanoramicBlend");
        if (shader == null)
        {
            Debug.LogWarning("[Kuntur] Falta el shader Kuntur/SkyPanoramicBlend. El cielo queda como estaba.");
            return null;
        }

        string dir = ArtDir + "/Materiales";
        string path = $"{dir}/Cielo_Kuntur.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            CreateFolderRecursive(dir);
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.shader != shader) mat.shader = shader;

        mat.SetTexture("_TexA", day);
        mat.SetTexture("_TexB", day);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_Exposure", 1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material GetAdditiveSkyMaterial(string name, string shaderName, string texturePath)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[Kuntur] Falta el shader {shaderName}. Esa pieza del cielo no se pone.");
            return null;
        }

        Texture2D texture = LoadSkyTexture(texturePath, false);
        if (texture == null) return null;

        string dir = ArtDir + "/Materiales";
        string path = $"{dir}/{name}.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            CreateFolderRecursive(dir);
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.shader != shader) mat.shader = shader;

        mat.SetTexture("_MainTex", texture);
        mat.SetColor("_Color", new Color(1f, 1f, 1f, 0f)); // arranca apagado
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Las panorámicas van SIN mipmaps. En una equirectangular, la columna
    // donde la imagen da la vuelta (u pasa de 1 a 0) hace que la tarjeta crea
    // que ahí hay un salto enorme y elija el mip más borroso: aparece una
    // costura vertical de lado a lado del cielo. Sin mipmaps no hay de dónde
    // elegir y la costura desaparece.
    private static Texture2D LoadSkyTexture(string path, bool panoramic = true)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[Kuntur] Falta la textura de cielo {path}.");
            return null;
        }

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool needsReimport = importer.textureType != TextureImporterType.Default
                                 || importer.mipmapEnabled
                                 || importer.wrapMode != TextureWrapMode.Repeat
                                 || (panoramic && importer.maxTextureSize < 4096);

            if (needsReimport)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                // 4096 de ancho para la panorámica: a 2048 el trozo de cielo
                // que entra en pantalla se ve borroso.
                if (panoramic) importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Niebla suave del color del cielo: es lo que hace que las montañas del
    // fondo se vean lejanas en vez de pegadas al pueblo (las imágenes del GDD
    // tienen ese mismo desvanecido azulado en el horizonte). DayNightCycle
    // luego le cambia el color según la hora.
    private static void ConfigureAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        // Niebla más suave que antes: con la anterior, la cordillera nueva
        // -que está a 320-480 m- se perdía casi entera en el gris.
        RenderSettings.fogDensity = 0.0016f;
        RenderSettings.fogColor = HexColor("#9fd0f0");
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = HexColor("#9fb4c4");
    }

    // ---------------------------------------------------------------
    // Trazado del pueblo, en UN solo lugar
    // ---------------------------------------------------------------
    // Las veredas, las cebras, las señales, los postes y el chequeo de "¿este
    // sitio está libre?" leían todos las mismas medidas copiadas a mano en
    // cinco sitios distintos, y bastaba cambiar una para que la vereda pasara
    // por encima de un jirón. Ahora todo sale de acá.
    private const float MainStreetX = 0f;        // Calle Real (norte-sur)
    private const float MainStreetHalf = 4f;     // media pista
    private const float AvenueZ = 30f;           // Av. Giráldez (este-oeste)
    private const float AvenueHalf = 4f;
    private const float PunoX = -42f;            // Jr. Puno (norte-sur)
    private const float CuscoX = 34f;            // Jr. Cusco (norte-sur)
    private const float ArequipaX = -80f;        // Jr. Arequipa (norte-sur), de la ampliación

    // Hasta dónde llega el pueblo. La Calle Real sale un poco más lejos que
    // los jirones paralelos: es la que sigue hacia los otros distritos.
    private const float TownSouthZ = -130f;
    // v56: el pueblo queda CERRADO por dos calles de circunvalación (norte y
    // sur): ninguna calle termina en el pasto, todas empalman con otra y los
    // autos dan la vuelta a la manzana. La Calle Real termina en la del norte.
    private const float TownNorthZ = 116.5f;
    private const float SideStreetSouthZ = -126.5f;
    private const float SideStreetNorthZ = 116.5f;
    private const float SouthRingZ = -123.25f;   // Jr. Mantaro (circunvalación sur)
    private const float NorthRingZ = 113.25f;    // Av. Huancavelica (circunvalación norte)
    private const float SidewalkWidth = 2.8f;

    // Jirones transversales: pista de 6.5 (±3.25) + vereda a los dos lados.
    // Los dos últimos (Jr. Huánuco al sur y Jr. Amazonas al norte) son de la
    // ampliación del pueblo; van al final para no cambiar el orden de los
    // cinco originales, que otros lugares usan por índice.
    // v56: al final, las dos circunvalaciones (índices 7 y 8).
    private static readonly float[] CrossStreetsZ = { -72f, -45f, -18f, 14f, 58f, -100f, 86f, SouthRingZ, NorthRingZ };
    private const float CrossStreetHalfRoad = 3.25f;
    private const float CrossStreetHalf = 6.1f;  // pista + vereda: el filo donde puede nacer una pared
    // v56: los jirones ya no se pasan de largo (antes terminaban en el pasto
    // detrás del Jr. Arequipa y en la orilla del río): van de esquina a
    // esquina, del Jr. Arequipa al Jr. Cusco.
    private const float CrossStreetFromX = ArequipaX - CrossStreetHalfRoad;
    private const float CrossStreetToX = CuscoX + CrossStreetHalfRoad;

    // Lote baldío donde se junta TODA la basura del nivel, a media cuadra de
    // donde aparece Kuntur. Tenerla desparramada por todo el pueblo hacía que
    // probar la mecánica de recoger fuera una caminata de diez minutos; junta
    // en un lote se ve el problema de un vistazo y se limpia de una pasada,
    // que además es como se ve un botadero informal de verdad.
    // Dónde paran los dos NPC de misión. Están acá arriba porque su terreno
    // hay que reservarlo antes de construir las casas.
    private static readonly Vector3 YamileSpot = new Vector3(-6.5f, 0f, 4f);
    // En la vereda, frente a la puerta de su bodega (ver BuildBodegaAndina).
    // v56: la bodega ahora da al Jr. Cusco (su jirón de enfrente ya no pasa).
    private static readonly Vector3 RosaSpot = new Vector3(38.9f, 0f, 1.4f);

    // Manzana del Plaza Vea: al norte de la Av. Giráldez, entre el Jr. Puno y
    // la Calle Real. Es la manzana entera que queda libre entre las veredas
    // (x de -35.9 a -7, z de 37 a 51.9): el hipermercado va sobre la avenida
    // principal, como el de verdad, con su estacionamiento adelante.
    private static readonly Vector3 PlazaVeaBlockCenter = new Vector3(-21.45f, 0f, 44.45f);
    private const float PlazaVeaBlockSizeX = 28.9f;
    private const float PlazaVeaBlockSizeZ = 14.9f;

    private static readonly Vector3 CleanupLotCenter = new Vector3(-20f, 0f, 1f);
    private const float CleanupLotSizeX = 16f;
    private const float CleanupLotSizeZ = 13f;

    // Camino de tierra del barrio nuevo, al otro lado del río.
    private const float EastRoadX = 92f;
    private const float EastRoadFromZ = -70f;
    private const float EastRoadToZ = 80f;

    // Medidas del cauce, compartidas por el terreno, el agua y las orillas.
    //
    // v53: río MÁS ANCHO y más hondo (se puede nadar). La orilla del pueblo
    // quedó donde estaba (x=52, las calles no se tocan) y el cauce creció
    // hacia el lado de la chacra: el eje pasó de x=60 a x=64. El agua mide
    // ~21 m de ancho (antes 11.6) y casi 2 m de hondo en el centro.
    private const float RiverCenterX = 64f;
    private const float RiverBedY = -3.0f;     // fondo del cauce (hondo: aquí se nada)
    private const float RiverBedHalf = 7.5f;   // fondo plano hasta 7.5 m del eje
    private const float RiverBankHalf = 12f;   // el talud sube hasta el pasto a 12 m del eje
    private const float RiverBankWestX = RiverCenterX - RiverBankHalf;  // 52, donde empieza a bajar la orilla oeste
    private const float RiverBankEastX = RiverCenterX + RiverBankHalf;  // 76
    private const float RiverWaterY = -1.05f;  // superficie del agua
    // El agua llena el cauce hasta donde el talud cruza la superficie.
    private const float WaterWidth = 20.8f;
    private const float WaterEdge = WaterWidth / 2f;

    // El puente de la Av. Giráldez cruza de orilla a orilla (antes 47–73).
    private const float BridgeStartX = 47f;
    private const float BridgeEndX = 81f;

    // Altura del terreno a una distancia d del eje del río (sin contar que
    // el cauce se aplana en los extremos del valle): fondo plano y un talud
    // recto hasta el pasto. Lo usan el suelo, las orillas y las mecánicas.
    private static float RiverProfile(float d)
    {
        if (d < RiverBedHalf) return RiverBedY;
        if (d < RiverBankHalf) return RiverBedY * (1f - (d - RiverBedHalf) / (RiverBankHalf - RiverBedHalf));
        return 0f;
    }

    private static void BuildTerrain(Transform parent)
    {
        // El suelo es UNA malla con el cauce curvo del río ya tallado, pasto
        // con manchas suaves y la orilla de arena (ver BuildValleyGround).
        BuildValleyGround(parent);

        // (La "Bajada_Al_Rio" ya no hace falta: con el río ancho el talud se
        // baja caminando y desde ahí se entra a nadar.)
    }

    // Pared invisible en el filo del agua. Tiene collider pero no se dibuja:
    // el jugador siente el borde del agua sin ver una caja rara.
    private static void BuildWaterBarrier(Transform parent, string name, float x, float fromZ, float toZ)
    {
        GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrier.name = name;
        barrier.transform.SetParent(parent);
        barrier.transform.position = new Vector3(x, RiverBedY / 2f - 0.15f, (fromZ + toZ) / 2f);
        barrier.transform.localScale = new Vector3(0.4f, Mathf.Abs(RiverBedY), toZ - fromZ);

        Object.DestroyImmediate(barrier.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(barrier.GetComponent<MeshFilter>());
        barrier.isStatic = true;
    }

    // Puente sobre el río, en la avenida que cruza el valle: tablero de
    // madera, barandas y pilotes hundidos en el agua.
    private static void BuildBridge(Transform parent, float z)
    {
        GameObject bridge = new GameObject("Puente");
        bridge.transform.SetParent(parent);

        Color wood = HexColor("#8a5f3c");
        Color woodDark = HexColor("#6d4a2e");

        // Tablero: con collider, es por donde se camina. Va de BridgeStartX a
        // BridgeEndX (34 m, antes 26), justo donde termina la pista en cada
        // orilla, y casi a ras del asfalto para que no haya escalón al entrar.
        GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deck.name = "Puente_Tablero";
        deck.transform.SetParent(bridge.transform);
        deck.transform.position = new Vector3((BridgeStartX + BridgeEndX) / 2f, -0.02f, z);
        deck.transform.localScale = new Vector3(BridgeEndX - BridgeStartX, 0.24f, 10.4f);
        SetColor(deck, wood);
        deck.isStatic = true;

        // Puente de acero rojo con cerchas (armadura de triángulos a los dos
        // lados y vigas arriba), como el modelo de referencia. Los carros y el
        // bus pasan por debajo de las vigas de arriba (a 5.6 m).
        SetColor(deck, HexColor("#5b5f66"));
        BuildTrussBridge(bridge.transform, z);

        // Pilotes que bajan hasta el fondo del cauce (tres pares: el río es
        // el doble de ancho).
        for (int i = 0; i < 6; i++)
        {
            float px = RiverCenterX + ((i % 3) - 1) * 6.5f;
            float pz = z + (i < 3 ? -2.6f : 2.6f);
            PrimitiveObject(bridge.transform, $"Pilote_{i}", PrimitiveType.Cylinder,
                new Vector3(px, RiverBedY / 2f, pz), new Vector3(0.6f, Mathf.Abs(RiverBedY) / 2f + 0.2f, 0.6f), HexColor("#7a7f86"));
        }
    }

    private static void BuildTrussBridge(Transform parent, float z)
    {
        Color red = HexColor("#b8352c");
        Color redDark = HexColor("#8f261f");
        const float x0 = BridgeStartX + 0.2f, x1 = BridgeEndX - 0.2f;
        const float h = 5.6f;          // alto de la cercha
        const float halfW = 4.7f;      // las cerchas van a los costados de la pista
        const int panels = 10;         // más largo, más paneles (siguen de ~3.4 m)
        float panel = (x1 - x0) / panels;
        GameObject truss = new GameObject("Cerchas");
        truss.transform.SetParent(parent);

        void Beam(string name, Vector3 a, Vector3 b, float t, Color c)
        {
            Vector3 mid = (a + b) * 0.5f;
            Vector3 dir = b - a;
            PrimitiveObject(truss.transform, name, PrimitiveType.Cube, mid, new Vector3(t, t, dir.magnitude + t * 0.5f), c,
                Quaternion.LookRotation(dir.normalized, Mathf.Abs(dir.normalized.y) > 0.99f ? Vector3.forward : Vector3.up));
        }

        for (int side = -1; side <= 1; side += 2)
        {
            float zz = z + side * halfW;
            // Cordón de abajo (a lo largo de todo el puente) y cordón de arriba.
            Beam("Cordon_Inferior", new Vector3(x0, 0.3f, zz), new Vector3(x1, 0.3f, zz), 0.45f, redDark);
            Beam("Cordon_Superior", new Vector3(x0 + panel, h, zz), new Vector3(x1 - panel, h, zz), 0.42f, red);
            // Diagonales de los extremos (de la base al cordón de arriba).
            Beam("Extremo", new Vector3(x0, 0.3f, zz), new Vector3(x0 + panel, h, zz), 0.42f, red);
            Beam("Extremo", new Vector3(x1, 0.3f, zz), new Vector3(x1 - panel, h, zz), 0.42f, red);
            for (int i = 1; i < panels; i++)
            {
                float x = x0 + i * panel;
                Beam("Montante", new Vector3(x, 0.3f, zz), new Vector3(x, h, zz), 0.28f, red);
                // Diagonales en V: forman los triángulos de la cercha.
                if (i < panels - 1)
                {
                    bool up = i % 2 == 1;
                    Beam("Diagonal", new Vector3(x, up ? 0.3f : h, zz), new Vector3(x + panel, up ? h : 0.3f, zz), 0.26f, red);
                }
            }
            // Baranda por dentro, a la altura de la cintura.
            Beam("Baranda", new Vector3(x0, 1.05f, zz - side * 0.3f), new Vector3(x1, 1.05f, zz - side * 0.3f), 0.1f, redDark);
            Beam("Baranda", new Vector3(x0, 0.6f, zz - side * 0.3f), new Vector3(x1, 0.6f, zz - side * 0.3f), 0.08f, redDark);

            // Pared invisible: nadie se cae al río desde el puente.
            GameObject wall = new GameObject("Borde_Puente");
            wall.transform.SetParent(truss.transform);
            wall.transform.position = new Vector3((x0 + x1) / 2f, 0.8f, zz);
            BoxCollider col = wall.AddComponent<BoxCollider>();
            col.size = new Vector3(x1 - x0, 1.6f, 0.4f);
            wall.isStatic = true;
        }

        // Vigas de arriba que unen las dos cerchas, con cruces de refuerzo.
        for (int i = 1; i < panels; i++)
        {
            float x = x0 + i * panel;
            Beam("Viga_Superior", new Vector3(x, h, z - halfW), new Vector3(x, h, z + halfW), 0.3f, red);
            if (i < panels - 1)
            {
                Beam("Cruz", new Vector3(x, h, z - halfW), new Vector3(x + panel, h, z + halfW), 0.16f, redDark);
                Beam("Cruz", new Vector3(x, h, z + halfW), new Vector3(x + panel, h, z - halfW), 0.16f, redDark);
            }
        }
        // Vigas del piso bajo el tablero (se ven desde el río).
        for (int i = 0; i <= panels; i++)
        {
            float x = x0 + i * panel;
            Beam("Viga_Piso", new Vector3(x, -0.25f, z - halfW), new Vector3(x, -0.25f, z + halfW), 0.3f, redDark);
        }
    }

    // Talud de una orilla: un cubo inclinado con collider que une el nivel del
    // pasto con el fondo del cauce. westSide=true es la orilla del pueblo.
    private static void BuildRiverSlope(Transform parent, string name, float topX, bool westSide)
    {
        const float run = 3.2f; // cuánto avanza en horizontal mientras baja
        float angle = Mathf.Atan2(-RiverBedY, run) * Mathf.Rad2Deg;
        float length = Mathf.Sqrt(run * run + RiverBedY * RiverBedY);

        GameObject slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slope.name = name;
        slope.transform.SetParent(parent);
        slope.transform.position = new Vector3(
            westSide ? topX + run / 2f : topX - run / 2f,
            RiverBedY / 2f,
            0f);
        slope.transform.rotation = Quaternion.Euler(0f, 0f, westSide ? -angle : angle);
        slope.transform.localScale = new Vector3(length + 0.6f, 0.5f, 440f);
        SetColor(slope, HexColor("#a98a66"));
        slope.isStatic = true;
    }

    // Nubes low-poly: grupos de esferas facetadas blancas flotando alto, como
    // las que se ven sobre el pueblo en las imágenes del GDD.
    private static void BuildClouds(Transform parent)
    {
        GameObject clouds = new GameObject("Nubes");
        clouds.transform.SetParent(parent);

        System.Random rng = new System.Random(3344);
        Mesh ico = LowPolyMeshFactory.Icosphere(1);

        for (int i = 0; i < 16; i++)
        {
            Vector3 center = new Vector3(NextFloat(rng, -140f, 160f), NextFloat(rng, 42f, 68f), NextFloat(rng, -140f, 140f));
            int puffs = rng.Next(3, 6);

            for (int p = 0; p < puffs; p++)
            {
                float size = NextFloat(rng, 9f, 17f);
                Vector3 offset = new Vector3(NextFloat(rng, -11f, 11f), NextFloat(rng, -1.6f, 1.6f), NextFloat(rng, -5f, 5f));
                MeshObject(clouds.transform, $"Nube_{i}_{p}", ico, center + offset,
                    new Vector3(size, size * 0.55f, size * 0.8f), HexColor("#f7fbff"),
                    Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f));
            }
        }
    }

    // Cordillera alrededor del valle: dos filas de conos facetados. La fila
    // lejana es más azulada y sin nieve, para dar sensación de profundidad
    // igual que en las imágenes del GDD.
    private const string MountainPrefabPath = "Assets/SuperiorWorlds/Highlands/Mountains/FREE/MountainFREE.prefab";

    private static void BuildMountainRange(Transform parent)
    {
        GameObject mountains = new GameObject("Montanas");
        mountains.transform.SetParent(parent);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MountainPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Kuntur] No encontré {MountainPrefabPath}. Uso los conos de antes.");
            BuildLowPolyMountains(mountains.transform);
            return;
        }

        System.Random rng = new System.Random(90210);
        Vector3 valleyCenter = new Vector3(10f, 0f, 0f);

        // Dos anillos: el cercano cierra el valle y el lejano da profundidad.
        // Se usan menos piezas que con los conos (18 + 14 antes) porque cada
        // montaña del asset trae su malla completa con LODs y ya llena mucho
        // más de horizonte que un cono.
        // Las distancias están medidas contra el pueblo, no elegidas a ojo.
        // Esta montaña es 3 veces más ancha que alta: una de 110 de alto mide
        // 330 de ancho, o sea 165 de radio. Puesta a 250 m, su falda llegaría
        // hasta 85 m del centro, justo encima del barrio del este y del río.
        // A partir de 320 m la falda queda a más de 150 m y el valle entero
        // -calles, casas, río- se ve despejado con la cordillera detrás.
        BuildMountainRing(mountains.transform, prefab, rng, valleyCenter, 11, 320f, 365f, 88f, 125f, -10f);
        BuildMountainRing(mountains.transform, prefab, rng, valleyCenter, 8, 430f, 480f, 130f, 175f, -18f);
    }

    private static void BuildMountainRing(Transform parent, GameObject prefab, System.Random rng,
        Vector3 center, int count, float minDistance, float maxDistance, float minHeight, float maxHeight, float sink)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f + NextFloat(rng, -0.12f, 0.12f);
            float distance = NextFloat(rng, minDistance, maxDistance);
            // v53: las del sur se corren más lejos. Sus faldas llegaban hasta
            // z = -170 y se metían en la calle que sube al mirador.
            float dirX = Mathf.Cos(angle), dirZ = Mathf.Sin(angle);
            if (dirZ < -0.55f && minDistance < 400f) distance += 85f;
            Vector3 position = center + new Vector3(dirX * distance, sink, dirZ * distance);

            GameObject mountain = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            mountain.name = $"Montana_{parent.childCount}";
            mountain.transform.position = position;
            // Giro al azar en Y: es la misma montaña repetida, y si todas
            // miraran igual se notaría el copiar y pegar de inmediato.
            mountain.transform.rotation = Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f);

            ScaleToHeight(mountain, NextFloat(rng, minHeight, maxHeight), NextFloat(rng, 0.85f, 1.25f));
            FixPipelineMaterials(mountain);

            // Las montañas no proyectan sombra: son enormes y están lejísimos,
            // su sombra no se ve pero sí cuesta calcularla.
            foreach (Renderer renderer in mountain.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (Collider collider in mountain.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
        }
    }

    // Escala un objeto del asset hasta que MIDA lo que se le pide de alto, y
    // lo asienta en el suelo.
    //
    // Hace falta porque cada asset viene en su propia escala y no hay forma de
    // saberla de antemano. La montaña de SuperiorWorlds, por ejemplo, mide
    // 1000 x 330 x 1000 metros ya armada (la malla es de 1 x 0.33 x 1 y su
    // hijo viene con escala 1000): puesta tal cual, una sola tapaba el pueblo
    // entero.
    //
    // OJO con cómo se mide: NO se usa Renderer.bounds. Las piezas de una
    // montaña con LODs están desactivadas en el prefab menos una, y una pieza
    // desactivada puede devolver un tamaño vacío; si sale vacío, el escalado
    // se salta y el objeto queda a su tamaño original, que es justo lo que
    // pasó. Acá se mide la MALLA y se la lleva al espacio del objeto con su
    // propia matriz, que no depende de que nada esté activo.
    private static void ScaleToHeight(GameObject instance, float targetHeight, float widthFactor = 1f)
    {
        Bounds? local = MeasureLocalBounds(instance);
        if (local == null || local.Value.size.y < 0.0001f)
        {
            Debug.LogWarning($"[Kuntur] No pude medir {instance.name}; lo dejo como vino.");
            return;
        }

        float groundY = instance.transform.position.y;
        float factor = targetHeight / local.Value.size.y;
        instance.transform.localScale = new Vector3(factor * widthFactor, factor, factor * widthFactor);

        // Asentarlo: después de escalar, la base casi nunca queda donde estaba
        // el pivote.
        Vector3 position = instance.transform.position;
        position.y = groundY - local.Value.min.y * factor;
        instance.transform.position = position;

        LODGroup lods = instance.GetComponent<LODGroup>();
        if (lods != null) lods.RecalculateBounds();
    }

    // Los assets de la Asset Store suelen venir con materiales de URP, y este
    // proyecto es Built-in: bajo Built-in, un material de URP no tiene ningún
    // paso que dibujar y el objeto sale rosado o gris plano. Es lo que les
    // pasaba a los árboles.
    //
    // La conversión NO es automática en Unity: hay que rehacer el material con
    // el shader Standard y volver a colgarle sus texturas, que en URP se
    // llaman distinto (_BaseMap en vez de _MainTex, _BaseColor en vez de
    // _Color).
    private static readonly Dictionary<string, Material> ConvertedMaterials = new Dictionary<string, Material>();

    private static void FixPipelineMaterials(GameObject instance, Color? tint = null, string tintKey = null)
    {
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material converted = ConvertToBuiltIn(materials[i], tint, tintKey);
                if (converted != materials[i]) { materials[i] = converted; changed = true; }
            }

            if (changed) renderer.sharedMaterials = materials;
        }
    }

    private static Material ConvertToBuiltIn(Material source, Color? tint, string tintKey)
    {
        if (source == null || source.shader == null) return source;

        string shaderName = source.shader.name;
        bool fromAnotherPipeline = shaderName.StartsWith("Universal Render Pipeline/")
                                   || shaderName.StartsWith("HDRP/")
                                   || shaderName == "Hidden/InternalErrorShader";
        if (!fromAnotherPipeline) return source;

        Shader standard = Shader.Find("Standard");
        if (standard == null) return source;

        bool tintApplies = tint != null && tintKey != null
                           && (source.name.IndexOf("Environ", System.StringComparison.OrdinalIgnoreCase) >= 0
                               || source.name.IndexOf("Leaf", System.StringComparison.OrdinalIgnoreCase) >= 0
                               || source.name.IndexOf("Foliage", System.StringComparison.OrdinalIgnoreCase) >= 0);

        string key = source.name + (tintApplies ? "_" + tintKey : string.Empty);
        if (ConvertedMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;

        string dir = ArtDir + "/Materiales/Convertidos";
        string path = $"{dir}/{key}_BuiltIn.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            CreateFolderRecursive(dir);
            mat = new Material(standard);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.shader != standard) mat.shader = standard;

        Texture baseMap = null;
        if (source.HasProperty("_BaseMap")) baseMap = source.GetTexture("_BaseMap");
        if (baseMap == null && source.HasProperty("_MainTex")) baseMap = source.GetTexture("_MainTex");
        if (baseMap != null) mat.SetTexture("_MainTex", baseMap);

        Color baseColor = Color.white;
        if (source.HasProperty("_BaseColor")) baseColor = source.GetColor("_BaseColor");
        else if (source.HasProperty("_Color")) baseColor = source.GetColor("_Color");

        // El tinte va SOLO al follaje. Si se lo aplicara a todo, el tronco
        // saldría verde: un árbol lleva dos materiales, hojas y madera, y el
        // color que se quiere variar es el de las hojas.
        bool isFoliage = source.name.IndexOf("Environ", System.StringComparison.OrdinalIgnoreCase) >= 0
                         || source.name.IndexOf("Leaf", System.StringComparison.OrdinalIgnoreCase) >= 0
                         || source.name.IndexOf("Foliage", System.StringComparison.OrdinalIgnoreCase) >= 0;

        mat.color = (tint != null && isFoliage) ? tint.Value : baseColor;

        if (source.HasProperty("_BumpMap"))
        {
            Texture bump = source.GetTexture("_BumpMap");
            if (bump != null)
            {
                mat.SetTexture("_BumpMap", bump);
                mat.EnableKeyword("_NORMALMAP");
            }
        }

        // Mate: el resto del pueblo es low-poly sin brillos, y un árbol
        // lustroso desentonaría.
        mat.SetFloat("_Glossiness", 0.08f);
        mat.SetFloat("_Metallic", 0f);

        // Si el material de URP recortaba por alfa (hojas sueltas), el de
        // Built-in también tiene que hacerlo o saldrían cuadrados opacos.
        if (source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0.5f)
        {
            mat.SetFloat("_Mode", 1f);
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cutoff", 0.4f);
            mat.renderQueue = 2450;
        }

        EditorUtility.SetDirty(mat);
        ConvertedMaterials[key] = mat;
        return mat;
    }

    // Tamaño real del objeto en SU espacio (sin contar la escala de su raíz),
    // sacado de las mallas y no de los renderers.
    private static Bounds? MeasureLocalBounds(GameObject instance)
    {
        Matrix4x4 toLocal = instance.transform.worldToLocalMatrix;
        Bounds result = new Bounds();
        bool started = false;

        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;

            Bounds mesh = filter.sharedMesh.bounds;
            Matrix4x4 matrix = toLocal * filter.transform.localToWorldMatrix;

            // Las 8 esquinas de la caja: girada, la caja original ya no sirve.
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                    (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                    (corner & 4) == 0 ? mesh.min.z : mesh.max.z);

                Vector3 world = matrix.MultiplyPoint3x4(point);
                if (!started) { result = new Bounds(world, Vector3.zero); started = true; }
                else result.Encapsulate(world);
            }
        }

        // Mallas con esqueleto (algunos árboles del asset las traen así para
        // mecerse con el viento): sin esto, esos árboles "no se podían medir"
        // y quedaban del tamaño con que vinieron.
        foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.sharedMesh == null) continue;
            Bounds mesh = skin.sharedMesh.bounds;
            Matrix4x4 matrix = toLocal * skin.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                    (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                    (corner & 4) == 0 ? mesh.min.z : mesh.max.z);
                Vector3 world = matrix.MultiplyPoint3x4(point);
                if (!started) { result = new Bounds(world, Vector3.zero); started = true; }
                else result.Encapsulate(world);
            }
        }

        return started ? result : (Bounds?)null;
    }

    // Respaldo: las montañas de conos de antes, por si el asset no está.
    private static void BuildLowPolyMountains(Transform parent)
    {
        System.Random rng = new System.Random(90210);
        Mesh cone6 = LowPolyMeshFactory.Cone(6);
        Mesh cone8 = LowPolyMeshFactory.Cone(8);
        Vector3 valleyCenter = new Vector3(10f, 0f, 0f);

        for (int i = 0; i < 18; i++)
        {
            float angle = (i / 18f) * Mathf.PI * 2f + NextFloat(rng, -0.08f, 0.08f);
            float distance = NextFloat(rng, 165f, 215f);
            Vector3 pos = valleyCenter + new Vector3(Mathf.Cos(angle) * distance, -2f, Mathf.Sin(angle) * distance);

            float height = NextFloat(rng, 48f, 92f);
            float radius = NextFloat(rng, 38f, 62f);
            Color rock = rng.Next(3) switch
            {
                0 => HexColor("#6f7a72"),
                1 => HexColor("#7a8079"),
                _ => HexColor("#68736e"),
            };

            MeshObject(parent, $"Montana_{i}", i % 2 == 0 ? cone6 : cone8, pos,
                new Vector3(radius, height, radius * NextFloat(rng, 0.85f, 1.15f)), rock,
                Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f));

            if (height > 62f)
            {
                MeshObject(parent, $"Montana_{i}_Nieve", cone6,
                    pos + Vector3.up * (height * 0.68f),
                    new Vector3(radius * 0.33f, height * 0.33f, radius * 0.33f), HexColor("#eef4f7"),
                    Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f));
            }
        }

        for (int i = 0; i < 14; i++)
        {
            float angle = (i / 14f) * Mathf.PI * 2f + 0.22f;
            float distance = NextFloat(rng, 260f, 320f);
            Vector3 pos = valleyCenter + new Vector3(Mathf.Cos(angle) * distance, -4f, Mathf.Sin(angle) * distance);

            MeshObject(parent, $"MontanaLejana_{i}", cone6, pos,
                new Vector3(NextFloat(rng, 55f, 85f), NextFloat(rng, 70f, 120f), NextFloat(rng, 55f, 85f)),
                HexColor("#8fa6bb"), Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f));
        }
    }

    // Río con cauce hundido, orillas de tierra, piedras y juncos: en las
    // imágenes del GDD el río no es una franja azul plana, tiene borde.
    private static void BuildRiver(Transform parent)
    {
        GameObject river = new GameObject("Rio");
        river.transform.SetParent(parent);

        // Agua de verdad: una malla que ondula y "corre" río abajo, no un
        // bloque azul quieto (ver RiverWater). Va por el centro del cauce,
        // dejando arena a los dos lados para poder caminar por la orilla.
        GameObject water = new GameObject("Rio_Agua");
        water.transform.SetParent(river.transform);
        water.transform.position = new Vector3(RiverCenterX, RiverWaterY, 0f);
        Vector3 waterOrigin = water.transform.position;
        water.AddComponent<MeshFilter>();
        MeshRenderer waterRenderer = water.AddComponent<MeshRenderer>();
        // Agua estilizada como el dibujo de referencia: celeste en la orilla,
        // azul al centro y espuma blanca contra las piedras. Si el shader no
        // compiló, el agua del asset; y si tampoco, el azul de siempre.
        Material assetWater = GetStylizedWaterMaterial();
        if (assetWater == null) assetWater = GetRiverWaterMaterial();
        waterRenderer.sharedMaterial = assetWater != null ? assetWater : GetMaterial(HexColor("#3f95cc"));
        waterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        RiverWater riverWater = water.AddComponent<RiverWater>();
        SerializedObject waterSo = new SerializedObject(riverWater);
        waterSo.FindProperty("width").floatValue = WaterWidth;
        waterSo.FindProperty("length").floatValue = 200f;
        // El agua sigue las curvas del cauce, de punta a punta del valle.
        Vector3[] riverPath = RiverWaterPath(waterOrigin);
        SerializedProperty pathProp = waterSo.FindProperty("path");
        pathProp.arraySize = riverPath.Length;
        for (int p = 0; p < riverPath.Length; p++) pathProp.GetArrayElementAtIndex(p).vector3Value = riverPath[p];
        waterSo.ApplyModifiedProperties();

        // v53: ya NO hay paredes invisibles al filo del agua. El río es ancho
        // y hondo, y Kuntur puede meterse a nadar (flota con "nadar1" y nada
        // con "nadar2", ver SimpleThirdPersonController). El puente sigue
        // siendo el camino seco para cruzar.

        // Puente de la Av. Giráldez: la única forma de cruzar al otro lado.
        BuildBridge(river.transform, 30f);

        // Rocas en fila a lo largo de las dos orillas, justo donde el agua
        // toca la pendiente, y juncos arriba (ver BuildRiverEdge).
        BuildRiverEdge(river.transform);
    }

    // Posiciones (X,Z) de la basura y las muestras de agua que van DENTRO del
    // cauce (ver BuildMechanics): las piedras y juncos decorativos del río se
    // arman después de esta lista, así que si no se les avisa dónde están,
    // les caen encima sin querer y las tapan por completo.
    private static readonly Vector2[] RiverCollectiblePoints =
    {
        new Vector2(RiverSampleX, -40f), new Vector2(RiverSampleX, -10f), new Vector2(RiverSampleX, 20f), new Vector2(RiverSampleX, 45f),
    };

    // Las piedras de la orilla no deben caer sobre las muestras de agua ni
    // sobre los montones de basura de la orilla (ver RiverSpots).
    private static bool IsNearRiverCollectible(float x, float z)
    {
        foreach (Vector2 point in RiverCollectiblePoints)
        {
            if (Vector2.Distance(new Vector2(x, z), point) < 2.4f) return true;
        }
        foreach (SpotDef spot in RiverSpots)
        {
            if (Mathf.Abs(x - spot.x) < 2.2f && Mathf.Abs(z - spot.z) < spot.halfLen + 1.5f) return true;
        }
        return false;
    }

    private static void BuildStreets(Transform parent)
    {
        GameObject streets = new GameObject("Calles");
        streets.transform.SetParent(parent);

        Color asphalt = HexColor("#3c4045");
        Color laneYellow = HexColor("#e8c33d");
        Color sidewalkColor = HexColor("#b9b5ac");
        Color curbColor = HexColor("#d7d2c7");

        // --- Calle Norte-Sur ---
        float realLength = TownNorthZ - TownSouthZ;
        float realCenterZ = (TownNorthZ + TownSouthZ) / 2f;
        PrimitiveObject(streets.transform, "Calle_Norte-Sur", PrimitiveType.Cube,
            new Vector3(0f, 0.01f, realCenterZ), new Vector3(8f, 0.02f, realLength), asphalt);

        // Doble línea amarilla al centro, como en la imagen de la calle.
        PrimitiveObject(streets.transform, "Linea_NS_A", PrimitiveType.Cube,
            new Vector3(-0.25f, 0.03f, realCenterZ), new Vector3(0.22f, 0.02f, realLength - 5f), laneYellow);
        PrimitiveObject(streets.transform, "Linea_NS_B", PrimitiveType.Cube,
            new Vector3(0.25f, 0.03f, realCenterZ), new Vector3(0.22f, 0.02f, realLength - 5f), laneYellow);

        // Líneas de borde.
        PrimitiveObject(streets.transform, "Borde_NS_Este", PrimitiveType.Cube,
            new Vector3(3.5f, 0.03f, realCenterZ), new Vector3(0.18f, 0.02f, realLength - 5f), laneYellow);
        PrimitiveObject(streets.transform, "Borde_NS_Oeste", PrimitiveType.Cube,
            new Vector3(-3.5f, 0.03f, realCenterZ), new Vector3(0.18f, 0.02f, realLength - 5f), laneYellow);

        BuildSidewalkAlongZ(streets.transform, "Vereda_Real_Este", 5.4f, MainStreetX, TownSouthZ, TownNorthZ, sidewalkColor, curbColor);
        BuildSidewalkAlongZ(streets.transform, "Vereda_Real_Oeste", -5.4f, MainStreetX, TownSouthZ, TownNorthZ, sidewalkColor, curbColor);

        // --- Av. Giráldez (Este-Oeste) ---
        // OJO: la pista se CORTA en las dos orillas del río. Antes cruzaba de
        // largo y, al hundir el cauce, quedaba una losa de asfalto flotando en
        // el aire sobre el agua (y el jugador se caía por el borde). El único
        // paso al otro lado es el puente.
        // v56: la avenida empieza en el Jr. Arequipa y del otro lado del
        // puente llega hasta el camino de la chacra (en T, sin cortarse en
        // el pasto).
        BuildRoadSegment(streets.transform, "Av_Giraldez_Oeste", CrossStreetFromX, BridgeStartX, 30f, 8f, asphalt, laneYellow);
        BuildRoadSegment(streets.transform, "Av_Giraldez_Este", BridgeEndX, GiraldezEastEndX, 30f, 8f, asphalt, laneYellow);

        BuildSidewalkAlongX(streets.transform, "Vereda_Giraldez_Norte", AvenueZ + 5.4f, AvenueZ, CrossStreetFromX, BridgeStartX, sidewalkColor, curbColor);
        BuildSidewalkAlongX(streets.transform, "Vereda_Giraldez_Sur", AvenueZ - 5.4f, AvenueZ, CrossStreetFromX, BridgeStartX, sidewalkColor, curbColor);
        // La avenida sigue del otro lado del puente, y su vereda también.
        BuildSidewalkAlongX(streets.transform, "Vereda_Giraldez_Norte_Este", AvenueZ + 5.4f, AvenueZ, BridgeEndX, GiraldezEastEndX, sidewalkColor, curbColor);
        BuildSidewalkAlongX(streets.transform, "Vereda_Giraldez_Sur_Este", AvenueZ - 5.4f, AvenueZ, BridgeEndX, GiraldezEastEndX, sidewalkColor, curbColor);

        // --- Jirones transversales (el pueblo deja de ser una sola calle) ---
        // Todos terminan antes de la orilla, por la misma razón. Y ahora cada
        // uno lleva sus dos veredas: un jirón sin vereda, con el pasto pegado
        // al asfalto, es justo lo que hacía que el pueblo se viera armado con
        // bloques en vez de como una calle de verdad.
        for (int i = 0; i < CrossStreetsZ.Length; i++)
        {
            float z = CrossStreetsZ[i];
            BuildRoadSegment(streets.transform, $"Jiron_{i}", CrossStreetFromX, CrossStreetToX, z, 6.5f, asphalt, laneYellow);
            if (IsRing(z))
            {
                // Circunvalación: por dentro, vereda cortada en cada esquina;
                // por fuera, vereda corrida de punta a punta (solo se corta
                // donde una calle sigue de largo, como la subida al mirador).
                BuildRingSidewalks(streets.transform, i, z, sidewalkColor, curbColor);
                continue;
            }
            BuildSidewalkAlongX(streets.transform, $"Vereda_Jiron{i}_Norte", z + CrossStreetHalfRoad + SidewalkWidth / 2f, z,
                CrossStreetFromX, CrossStreetToX, sidewalkColor, curbColor);
            BuildSidewalkAlongX(streets.transform, $"Vereda_Jiron{i}_Sur", z - CrossStreetHalfRoad - SidewalkWidth / 2f, z,
                CrossStreetFromX, CrossStreetToX, sidewalkColor, curbColor);
        }

        // --- Dos calles más paralelas a la Calle Real: con esto el pueblo ya
        // tiene manzanas de verdad y no una sola avenida con casas a los lados.
        // Tres paralelas: Puno, Cusco y el Jr. Arequipa de la ampliación, que
        // cierra el pueblo por el oeste.
        foreach ((float streetX, string nombre) in new[] { (PunoX, "Puno"), (CuscoX, "Cusco"), (ArequipaX, "Arequipa") })
        {
            // v56: Puno y Cusco siguen al sur, cuesta arriba, hasta el mirador
            // (ver KunturSceneBuilder.Hill); el Arequipa cierra en la
            // circunvalación sur.
            float south = streetX == ArequipaX ? SideStreetSouthZ : TownSouthZ;
            BuildRoadSegmentZ(streets.transform, $"Jr_{nombre}", streetX, south, SideStreetNorthZ, 6.5f, asphalt, laneYellow);
            float eastX = streetX + CrossStreetHalfRoad + SidewalkWidth / 2f;
            float westX = streetX - CrossStreetHalfRoad - SidewalkWidth / 2f;
            if (streetX == CuscoX)
            {
                // Lado de afuera (el del río): corrida, solo la corta la Giráldez.
                BuildSidewalkCustom(streets.transform, $"Vereda_{nombre}_Este", eastX, streetX, south, SideStreetNorthZ, true,
                    new List<Vector2> { new Vector2(AvenueZ - AvenueHalf - 0.2f, AvenueZ + AvenueHalf + 0.2f) }, sidewalkColor, curbColor);
                BuildSidewalkAlongZ(streets.transform, $"Vereda_{nombre}_Oeste", westX, streetX, south, SideStreetNorthZ, sidewalkColor, curbColor);
            }
            else if (streetX == ArequipaX)
            {
                BuildSidewalkAlongZ(streets.transform, $"Vereda_{nombre}_Este", eastX, streetX, south, SideStreetNorthZ, sidewalkColor, curbColor);
                // Lado de afuera: ninguna calle sigue al oeste, vereda corrida.
                BuildSidewalkCustom(streets.transform, $"Vereda_{nombre}_Oeste", westX, streetX, south, SideStreetNorthZ, true,
                    new List<Vector2>(), sidewalkColor, curbColor);
            }
            else
            {
                BuildSidewalkAlongZ(streets.transform, $"Vereda_{nombre}_Este", eastX, streetX, south, SideStreetNorthZ, sidewalkColor, curbColor);
                BuildSidewalkAlongZ(streets.transform, $"Vereda_{nombre}_Oeste", westX, streetX, south, SideStreetNorthZ, sidewalkColor, curbColor);
            }
        }

        // --- Camino de tierra del barrio del otro lado del río ---
        BuildDirtRoad(streets.transform, "Camino_Chacra", EastRoadX, EastRoadFromZ, EastRoadToZ, 7f);

        // --- v56: puente peatonal sobre el río (a la altura del Jr. Ancash) ---
        BuildFootbridge(streets.transform);

        // --- Cruces peatonales y líneas de pare en cada esquina ---
        BuildIntersectionMarkings(streets.transform);

        // --- Señalización vertical al estilo peruano ---
        BuildTrafficSigns(streets.transform);

        // Semáforos con brazo sobre la pista (como la imagen de la calle).
        // Van SOBRE la vereda (que llega hasta 6.8), no en el pasto de atrás,
        // y en esquinas distintas para no chocar con los letreros de calle.
        // Semáforos que FUNCIONAN (cambian de color y los carros los
        // respetan), al estilo peruano. Ver BuildTrafficSignals.
        BuildTrafficSignals(streets.transform);

        // Postes de luz a lo largo de la Calle Real, alineados al borde de la
        // vereda (antes varios quedaban en el pasto, fuera de la vereda).
        // Los postes van a MITAD DE CUADRA, no "cada 16 metros": puestos por
        // distancia fija, uno de cada tres caía justo en un cruce -encima del
        // asfalto- o pegado al letrero de la esquina. A mitad de cuadra es
        // además donde están en la calle de verdad.
        float[] realLampZ = { -122f, -86f, -58.5f, -31.5f, -2f, 44f, 72f, 102f };
        for (int i = 0; i < realLampZ.Length; i++)
        {
            float x = i % 2 == 0 ? 5.9f : -5.9f;
            BuildStreetLamp(streets.transform, new Vector3(x, 0f, realLampZ[i]), i % 2 == 0 ? -1f : 1f);
        }

        // Postes repartidos por los jirones. Estos NO llevan luz en tiempo
        // real (allowRealLight = false): dan la silueta y el brillo del foco
        // de noche sin sumarle otras veinte luces a una laptop con gráficos
        // integrados. En la Calle Real no se ponen: esa ya tiene los suyos y
        // terminaban dos postes pegados en la misma esquina.
        foreach (float z in CrossStreetsZ)
        {
            for (float x = -86f; x <= 46f; x += 24f)
            {
                if (Mathf.Abs(x) < 9f) continue; // la esquina de la Calle Real ya tiene el suyo
                if (x < CrossStreetFromX + 3f || x > CrossStreetToX - 3f) continue; // v56: el jirón ya no llega ahí
                BuildStreetLamp(streets.transform, new Vector3(x, 0f, z + CrossStreetHalfRoad + 1.4f), Vector3.back, false);
            }
        }

        BuildStreetLamp(streets.transform, new Vector3(50.5f, 0f, -2f), 1f);   // junto a la bajada al río
        BuildStreetLamp(streets.transform, new Vector3(-10.5f, 0f, 6f), -1f);  // alumbrando el lote del botadero

        // --- Letreros de esquina, como en Huancayo ---
        // Un letrero en CADA cruce, con los nombres de las dos calles que se
        // cruzan ahí. Antes había una decena de letreros sueltos y varios
        // nombraban la calle equivocada para esa esquina (el Jr. Huancavelica
        // estaba plantado en el cruce del Jr. Junín).
        //
        // Van en la esquina NORESTE de cada cruce: ahí no hay señal de PARE
        // (esas van al sureste y al noroeste) ni semáforo, así que no se
        // pisan postes.
        var northSouth = new (float x, float half, string name)[]
        {
            (ArequipaX, CrossStreetHalfRoad, "JR. AREQUIPA"),
            (PunoX, CrossStreetHalfRoad, "JR. PUNO"),
            (MainStreetX, MainStreetHalf, "CALLE REAL"),
            (CuscoX, CrossStreetHalfRoad, "JR. CUSCO"),
        };

        var eastWest = new (float z, float half, string name)[]
        {
            (CrossStreetsZ[0], CrossStreetHalfRoad, "JR. ANCASH"),
            (CrossStreetsZ[1], CrossStreetHalfRoad, "JR. JUNÍN"),
            (CrossStreetsZ[2], CrossStreetHalfRoad, "JR. LORETO"),
            (CrossStreetsZ[3], CrossStreetHalfRoad, "JR. AYACUCHO"),
            (AvenueZ, AvenueHalf, "AV. GIRÁLDEZ"),
            (CrossStreetsZ[4], CrossStreetHalfRoad, "AV. FERROCARRIL"),
            (CrossStreetsZ[5], CrossStreetHalfRoad, "JR. HUÁNUCO"),
            (CrossStreetsZ[6], CrossStreetHalfRoad, "JR. AMAZONAS"),
            (CrossStreetsZ[7], CrossStreetHalfRoad, "JR. MANTARO"),
            (CrossStreetsZ[8], CrossStreetHalfRoad, "AV. HUANCAVELICA"),
        };

        foreach (var ns in northSouth)
        {
            foreach (var ew in eastWest)
            {
                Vector3 corner = new Vector3(ns.x + ns.half + 1.2f, 0f, ew.z + ew.half + 1.2f);
                BuildCornerSign(streets.transform, corner, ew.name, ns.name);
            }
        }

        BuildStreetSign(streets.transform, new Vector3(EastRoadX + 5.5f, 0f, AvenueZ - 5.6f), "CAMINO A LA CHACRA", 0f);

        // --- Tránsito ---
        // Por carriles, con vuelta en U al final de cada calle, respetando
        // semáforos y sin chocarse (ver BuildTraffic y CarPatrol).
        BuildCircuitTraffic(streets.transform);
    }

    // Camino de tierra del barrio de la otra orilla: sin líneas ni sardinel,
    // solo la huella apisonada y las dos rodadas de las combis. En el campo
    // alrededor de Huancayo las calles son exactamente así.
    private static void BuildDirtRoad(Transform parent, string name, float x, float fromZ, float toZ, float width)
    {
        float centerZ = (fromZ + toZ) / 2f;
        float length = toZ - fromZ;

        PrimitiveObject(parent, name, PrimitiveType.Cube,
            new Vector3(x, 0.012f, centerZ), new Vector3(width, 0.02f, length), HexColor("#b09166"));

        for (int side = -1; side <= 1; side += 2)
        {
            PrimitiveObject(parent, $"{name}_Rodada_{side}", PrimitiveType.Cube,
                new Vector3(x + side * 1.5f, 0.026f, centerZ),
                new Vector3(0.9f, 0.02f, length), HexColor("#9b7d55"));
        }
    }

    // Cebras y líneas de pare en todas las esquinas. Sin esto la pista es solo
    // asfalto con rayas amarillas y no se entiende dónde cruza el peatón.
    private static void BuildIntersectionMarkings(Transform parent)
    {
        Color paint = HexColor("#eef2f3");
        float crossOffset = CrossStreetHalfRoad + 2.1f;
        float avenueOffset = AvenueHalf + 2.1f;

        // Calle Real contra cada jirón: cebra de los dos lados del cruce.
        for (int i = 0; i < CrossStreetsZ.Length; i++)
        {
            float z = CrossStreetsZ[i];
            BuildCrosswalk(parent, $"Cebra_Real_J{i}_Sur", new Vector3(MainStreetX, 0f, z - crossOffset), MainStreetHalf * 2f, true, paint);
            BuildCrosswalk(parent, $"Cebra_Real_J{i}_Norte", new Vector3(MainStreetX, 0f, z + crossOffset), MainStreetHalf * 2f, true, paint);
        }

        // La esquina principal (Calle Real con Av. Giráldez) lleva las cuatro.
        BuildCrosswalk(parent, "Cebra_Real_Giraldez_Sur", new Vector3(MainStreetX, 0f, AvenueZ - avenueOffset), MainStreetHalf * 2f, true, paint);
        BuildCrosswalk(parent, "Cebra_Real_Giraldez_Norte", new Vector3(MainStreetX, 0f, AvenueZ + avenueOffset), MainStreetHalf * 2f, true, paint);
        BuildCrosswalk(parent, "Cebra_Giraldez_Oeste", new Vector3(MainStreetX - MainStreetHalf - 2.1f, 0f, AvenueZ), AvenueHalf * 2f, false, paint);
        BuildCrosswalk(parent, "Cebra_Giraldez_Este", new Vector3(MainStreetX + MainStreetHalf + 2.1f, 0f, AvenueZ), AvenueHalf * 2f, false, paint);

        // Jr. Puno y Jr. Cusco donde cruzan la avenida.
        foreach (float streetX in new[] { PunoX, CuscoX, ArequipaX })
        {
            string nombre = streetX == PunoX ? "Puno" : streetX == CuscoX ? "Cusco" : "Arequipa";
            BuildCrosswalk(parent, $"Cebra_{nombre}_Sur", new Vector3(streetX, 0f, AvenueZ - avenueOffset), CrossStreetHalfRoad * 2f, true, paint);
            BuildCrosswalk(parent, $"Cebra_{nombre}_Norte", new Vector3(streetX, 0f, AvenueZ + avenueOffset), CrossStreetHalfRoad * 2f, true, paint);

            // v55b: y en CADA esquina con los jirones: se cruza por la cebra,
            // no por la vereda.
            for (int i = 0; i < CrossStreetsZ.Length; i++)
            {
                float z = CrossStreetsZ[i];
                if (z < SideStreetSouthZ + 4f || z > SideStreetNorthZ - 4f) continue;
                if (streetX < CrossStreetFromX + 4f || streetX > CrossStreetToX - 4f) continue;
                BuildCrosswalk(parent, $"Cebra_{nombre}_J{i}_Sur", new Vector3(streetX, 0f, z - crossOffset), CrossStreetHalfRoad * 2f, true, paint);
                BuildCrosswalk(parent, $"Cebra_{nombre}_J{i}_Norte", new Vector3(streetX, 0f, z + crossOffset), CrossStreetHalfRoad * 2f, true, paint);
                BuildCrosswalk(parent, $"Cebra_J{i}_{nombre}_Oeste", new Vector3(streetX - CrossStreetHalfRoad - 2.1f, 0f, z), CrossStreetHalfRoad * 2f, false, paint);
                BuildCrosswalk(parent, $"Cebra_J{i}_{nombre}_Este", new Vector3(streetX + CrossStreetHalfRoad + 2.1f, 0f, z), CrossStreetHalfRoad * 2f, false, paint);
            }
        }

        // Los jirones cruzando la Calle Real (a los dos lados de la Real).
        for (int i = 0; i < CrossStreetsZ.Length; i++)
        {
            float z = CrossStreetsZ[i];
            BuildCrosswalk(parent, $"Cebra_J{i}_Real_Oeste", new Vector3(MainStreetX - MainStreetHalf - 2.1f, 0f, z), CrossStreetHalfRoad * 2f, false, paint);
            BuildCrosswalk(parent, $"Cebra_J{i}_Real_Este", new Vector3(MainStreetX + MainStreetHalf + 2.1f, 0f, z), CrossStreetHalfRoad * 2f, false, paint);
        }
    }

    // Cruce peatonal. roadAlongZ = la pista que se cruza corre de norte a sur,
    // así que las franjas se alargan en Z (en el sentido en que van los carros)
    // y se repiten a lo ancho de la pista, igual que una cebra de verdad.
    private static void BuildCrosswalk(Transform parent, string name, Vector3 center, float roadWidth, bool roadAlongZ, Color paint)
    {
        int bars = Mathf.Max(4, Mathf.RoundToInt(roadWidth / 1.3f));
        float step = roadWidth / bars;

        for (int i = 0; i < bars; i++)
        {
            float t = -roadWidth / 2f + step * (i + 0.5f);
            Vector3 position = roadAlongZ ? center + new Vector3(t, 0f, 0f) : center + new Vector3(0f, 0f, t);
            Vector3 scale = roadAlongZ
                ? new Vector3(step * 0.55f, 0.02f, 2.9f)
                : new Vector3(2.9f, 0.02f, step * 0.55f);

            PrimitiveObject(parent, $"{name}_Franja{i}", PrimitiveType.Cube, position + Vector3.up * 0.038f, scale, paint);
        }

        // Línea de pare de cada sentido, justo antes de la cebra. En Perú se
        // maneja por la derecha: el carril que sube (+Z) es el del lado +X y
        // su línea de pare queda del lado -Z del cruce.
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 position = roadAlongZ
                ? center + new Vector3(side * roadWidth * 0.25f, 0.038f, -side * 2.3f)
                : center + new Vector3(-side * 2.3f, 0.038f, side * roadWidth * 0.25f);
            Vector3 scale = roadAlongZ
                ? new Vector3(roadWidth * 0.44f, 0.02f, 0.42f)
                : new Vector3(0.42f, 0.02f, roadWidth * 0.44f);

            PrimitiveObject(parent, $"{name}_LineaPare{side}", PrimitiveType.Cube, position, scale, paint);
        }
    }

    private enum TrafficSignKind { Pare, VelocidadMaxima, CrucePeatonal }

    // Señalización vertical como la del MTC: PARE octogonal roja, velocidad
    // máxima en disco blanco con aro rojo, y el rombo amarillo de cruce
    // peatonal. Todas van paradas sobre la vereda y mirando al carro que
    // viene, no de adorno en el pasto.
    private static void BuildTrafficSigns(Transform parent)
    {
        // Un PARE en cada boca de jirón: la Calle Real tiene la preferencia,
        // así que el que para es el que viene por el jirón. La señal va a la
        // DERECHA del que maneja, que es donde se para en la vida real.
        for (int i = 0; i < CrossStreetsZ.Length; i++)
        {
            float z = CrossStreetsZ[i];
            if (System.Array.IndexOf(SignalizedRealCrossZ, z) >= 0) continue; // ahí manda el semáforo
            BuildTrafficSign(parent, new Vector3(5.9f, 0f, z - CrossStreetHalfRoad - 1.4f), TrafficSignKind.Pare, 90f);
            BuildTrafficSign(parent, new Vector3(-5.9f, 0f, z + CrossStreetHalfRoad + 1.4f), TrafficSignKind.Pare, -90f);
        }

        // Velocidad máxima en la Calle Real y en la avenida.
        BuildTrafficSign(parent, new Vector3(5.9f, 0f, -34f), TrafficSignKind.VelocidadMaxima, 180f);
        BuildTrafficSign(parent, new Vector3(-5.9f, 0f, 48f), TrafficSignKind.VelocidadMaxima, 0f);
        BuildTrafficSign(parent, new Vector3(-20f, 0f, AvenueZ - 5.9f), TrafficSignKind.VelocidadMaxima, 90f);

        // Rombo de cruce peatonal antes de las cebras más transitadas. Se
        // separan del semáforo de esa misma esquina para que no queden dos
        // postes pegados uno al otro.
        BuildTrafficSign(parent, new Vector3(CuscoX - 5f, 0f, AvenueZ - 9.5f), TrafficSignKind.CrucePeatonal, 180f);
        BuildTrafficSign(parent, new Vector3(-5.9f, 0f, AvenueZ + 9.5f), TrafficSignKind.CrucePeatonal, 0f);
        BuildTrafficSign(parent, new Vector3(-5.9f, 0f, -18f + CrossStreetHalfRoad + 4.2f), TrafficSignKind.CrucePeatonal, 0f);
    }

    private static void BuildTrafficSign(Transform parent, Vector3 basePosition, TrafficSignKind kind, float yaw)
    {
        GameObject sign = new GameObject("Senal_" + kind);
        sign.transform.SetParent(parent);
        sign.transform.position = basePosition;

        // La placa mira hacia su +Z local, así que el yaw es directamente
        // "hacia dónde mira la señal".
        Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
        Vector3 board = basePosition + Vector3.up * 2.3f;

        PrimitiveObject(sign.transform, "Poste", PrimitiveType.Cylinder,
            basePosition + Vector3.up * 1.15f, new Vector3(0.09f, 1.15f, 0.09f), HexColor("#8b9196"));

        switch (kind)
        {
            case TrafficSignKind.Pare:
                MeshObject(sign.transform, "Placa", LowPolyMeshFactory.SignBoard(8), board,
                    new Vector3(1.15f, 1.15f, 0.07f), HexColor("#b03a2e"), facing);
                MeshObject(sign.transform, "Aro", LowPolyMeshFactory.SignBoard(8),
                    board + facing * new Vector3(0f, 0f, 0.05f),
                    new Vector3(1.02f, 1.02f, 0.03f), HexColor("#f4f6f6"), facing);
                MeshObject(sign.transform, "Fondo", LowPolyMeshFactory.SignBoard(8),
                    board + facing * new Vector3(0f, 0f, 0.08f),
                    new Vector3(0.9f, 0.9f, 0.03f), HexColor("#c0392b"), facing);
                AddSignText(sign.transform, board, facing, "PARE", 1.5f, HexColor("#f8f9f9"), 0.12f);
                break;

            case TrafficSignKind.VelocidadMaxima:
                MeshObject(sign.transform, "Aro", LowPolyMeshFactory.SignBoard(16), board,
                    new Vector3(1.1f, 1.1f, 0.07f), HexColor("#c0392b"), facing);
                MeshObject(sign.transform, "Fondo", LowPolyMeshFactory.SignBoard(16),
                    board + facing * new Vector3(0f, 0f, 0.05f),
                    new Vector3(0.88f, 0.88f, 0.03f), HexColor("#f4f6f6"), facing);
                AddSignText(sign.transform, board, facing, "30", 2.6f, HexColor("#1c2833"), 0.09f);
                break;

            case TrafficSignKind.CrucePeatonal:
                // El rombo es un cubo girado 45° sobre su propia cara.
                PrimitiveObject(sign.transform, "Placa", PrimitiveType.Cube, board,
                    new Vector3(0.84f, 0.84f, 0.06f), HexColor("#f0c419"), facing * Quaternion.Euler(0f, 0f, 45f));
                PrimitiveObject(sign.transform, "Peaton_Cabeza", PrimitiveType.Sphere,
                    board + facing * new Vector3(0f, 0.23f, 0.05f), Vector3.one * 0.13f, HexColor("#1c1c1c"));
                PrimitiveObject(sign.transform, "Peaton_Cuerpo", PrimitiveType.Cube,
                    board + facing * new Vector3(0f, 0.02f, 0.05f), new Vector3(0.13f, 0.28f, 0.04f), HexColor("#1c1c1c"), facing);
                PrimitiveObject(sign.transform, "Peaton_Pierna", PrimitiveType.Cube,
                    board + facing * new Vector3(0.07f, -0.22f, 0.05f), new Vector3(0.09f, 0.26f, 0.04f), HexColor("#1c1c1c"),
                    facing * Quaternion.Euler(0f, 0f, 20f));
                break;
        }
    }

    // Texto de una señal: solo en la cara delantera. Una señal de tránsito por
    // detrás es una plancha gris, no un texto al revés.
    //
    // OJO con la rotación, que es la que dejaba el PARE en espejo: un texto de
    // TextMeshPro se lee de frente desde su -Z local, no desde su +Z. La cara
    // de la señal mira a su +Z, así que el texto hay que darlo vuelta 180°
    // para que su lado legible quede mirando hacia afuera, al que lo lee. Sin
    // ese giro, el texto quedaba mirando hacia la placa y desde la calle se
    // veía al revés.
    private static Quaternion ReadableFrom(Quaternion facing) => facing * Quaternion.Euler(0f, 180f, 0f);

    private static void AddSignText(Transform parent, Vector3 board, Quaternion facing, string text, float fontSize, Color color, float depth)
    {
        GameObject textGO = new GameObject("Texto");
        textGO.transform.SetParent(parent);
        textGO.transform.position = board + facing * new Vector3(0f, 0f, depth);
        textGO.transform.rotation = ReadableFrom(facing);

        TextMeshPro label = textGO.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        if (defaultFont != null) label.font = defaultFont;
        label.rectTransform.sizeDelta = new Vector2(1.3f, 0.7f);
    }

    // Letrero de esquina con el nombre de la calle. Los nombres son de calles
    // reales de Huancayo: el juego habla de la basura de ESE valle, así que el
    // pueblo se llama como el pueblo del jugador.
    //
    // Versión de una sola placa (se usa para el camino de tierra, que no cruza
    // con nada). Las esquinas usan BuildCornerSign, con las dos calles.
    private static void BuildStreetSign(Transform parent, Vector3 basePosition, string streetName, float yaw)
    {
        if (!FindSignSpot(ref basePosition)) return;
        Occupy(basePosition, 2.5f, 2.5f);

        GameObject sign = new GameObject("Letrero_" + streetName);
        sign.transform.SetParent(parent);
        sign.transform.position = basePosition;

        PrimitiveObject(sign.transform, "Poste", PrimitiveType.Cylinder,
            basePosition + Vector3.up * 1.5f, new Vector3(0.1f, 1.5f, 0.1f), HexColor("#7f8c8d"));

        BuildNamePlate(sign.transform, basePosition, 2.85f, streetName, yaw);
    }

    // Letrero de ESQUINA, como los de verdad: un poste con dos placas en cruz,
    // cada una con el nombre de una de las dos calles que se cruzan ahí. Cada
    // placa va paralela a la calle que nombra.
    private static void BuildCornerSign(Transform parent, Vector3 basePosition, string eastWestName, string northSouthName)
    {
        if (!FindSignSpot(ref basePosition)) return;
        Occupy(basePosition, 2.5f, 2.5f);

        GameObject sign = new GameObject($"Esquina_{eastWestName}_{northSouthName}");
        sign.transform.SetParent(parent);
        sign.transform.position = basePosition;

        PrimitiveObject(sign.transform, "Poste", PrimitiveType.Cylinder,
            basePosition + Vector3.up * 1.8f, new Vector3(0.1f, 1.8f, 0.1f), HexColor("#7f8c8d"));

        // Alturas distintas: si las dos placas estuvieran a la misma altura se
        // atravesarían una a la otra en el centro de la cruz.
        BuildNamePlate(sign.transform, basePosition, 3.35f, eastWestName, 0f);   // calle este-oeste
        BuildNamePlate(sign.transform, basePosition, 2.72f, northSouthName, 90f); // calle norte-sur
    }

    // Busca un hueco libre cerca del punto pedido: si ahí hay una casa o el
    // asfalto, se corre un poco en vez de quedar clavado en una pared. Ojo:
    // acá NO se usa IsAreaFree, porque ese da por ocupada toda la calle con
    // sus veredas, y la vereda es justo donde va un letrero.
    private static bool FindSignSpot(ref Vector3 basePosition)
    {
        if (!IsOnRoadway(basePosition) && !OverlapsPlaced(basePosition, 1.6f)) return true;

        Vector3[] nudges =
        {
            new Vector3(2.5f, 0f, 0f), new Vector3(-2.5f, 0f, 0f),
            new Vector3(0f, 0f, 3f), new Vector3(0f, 0f, -3f),
            new Vector3(2.5f, 0f, 3f), new Vector3(-2.5f, 0f, -3f),
            new Vector3(4.5f, 0f, 0f), new Vector3(-4.5f, 0f, 0f),
        };

        foreach (Vector3 nudge in nudges)
        {
            if (IsOnRoadway(basePosition + nudge)) continue;
            if (OverlapsPlaced(basePosition + nudge, 1.6f)) continue;
            basePosition += nudge;
            return true;
        }

        return false; // no hay dónde: mejor sin letrero que dentro de una pared
    }

    // Una placa verde con el nombre por las DOS caras. El texto de cada cara
    // va girado 180° respecto de la dirección a la que mira esa cara (ver
    // ReadableFrom): sin ese giro, las dos caras se leían en espejo.
    private static void BuildNamePlate(Transform sign, Vector3 basePosition, float height, string streetName, float yaw)
    {
        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Placa_" + streetName;
        board.transform.SetParent(sign);
        board.transform.position = basePosition + Vector3.up * height;
        board.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        board.transform.localScale = new Vector3(3.1f, 0.56f, 0.09f);
        // v56c: la placa conserva su collider (está a 2.7 m, no molesta al
        // caminar): así la cámara la esquiva en vez de meterse adentro.
        SetColor(board, HexColor("#1f6f4a"));
        board.isStatic = true;

        for (int face = 0; face < 2; face++)
        {
            Quaternion faceRotation = Quaternion.Euler(0f, yaw + face * 180f, 0f);

            // Colgado del POSTE (sin escala), no de la placa: la placa tiene
            // escala desigual y deformaría las letras.
            GameObject textGO = new GameObject("Texto_" + face);
            textGO.transform.SetParent(sign);
            textGO.transform.position = basePosition + Vector3.up * height + faceRotation * new Vector3(0f, 0f, 0.07f);
            textGO.transform.rotation = ReadableFrom(faceRotation);

            TextMeshPro label = textGO.AddComponent<TextMeshPro>();
            label.text = streetName;
            label.fontSize = 2f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = UIPalette_Cream();
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (defaultFont != null) label.font = defaultFont;
            label.rectTransform.sizeDelta = new Vector2(3f, 0.55f);
        }
    }

    // Carro low-poly que va y viene por la avenida, con faros que se encienden
    // solos al anochecer.
    private static void BuildCar(Transform parent, string name, Color bodyColor, Vector3 from, Vector3 to, float speed)
    {
        if (TryBuildAssetVehicle(parent, name, VehicleKind.Car, from, to, speed, null, true)) return;

        GameObject car = new GameObject(name);
        car.transform.SetParent(parent);
        car.transform.position = from;

        PrimitiveObject(car.transform, "Chasis", PrimitiveType.Cube,
            from + new Vector3(0f, 0.62f, 0f), new Vector3(1.9f, 0.7f, 4.2f), bodyColor);
        PrimitiveObject(car.transform, "Cabina", PrimitiveType.Cube,
            from + new Vector3(0f, 1.22f, -0.25f), new Vector3(1.7f, 0.62f, 2.1f), HexColor("#2c3e50"));

        for (int i = 0; i < 4; i++)
        {
            float wx = i % 2 == 0 ? -0.92f : 0.92f;
            float wz = i < 2 ? 1.35f : -1.35f;
            PrimitiveObject(car.transform, $"Rueda_{i}", PrimitiveType.Cylinder,
                from + new Vector3(wx, 0.33f, wz), new Vector3(0.66f, 0.14f, 0.66f), HexColor("#1c1f22"),
                Quaternion.Euler(0f, 0f, 90f));
        }

        // Faros: piezas que se prenden de noche.
        for (int i = 0; i < 2; i++)
        {
            GameObject headlight = PrimitiveObject(car.transform, $"Faro_{i}", PrimitiveType.Cube,
                from + new Vector3(i == 0 ? -0.6f : 0.6f, 0.7f, 2.12f), new Vector3(0.42f, 0.22f, 0.1f), HexColor("#f5efd0"));
            headlight.isStatic = false;
            AddNightLight(headlight, null, HexColor("#fff3c4"), 2.2f);
        }

        foreach (Transform child in car.transform) child.gameObject.isStatic = false;

        AttachVehicleAI(car, from, to, speed, 1.6f, 2.1f, 1f);
    }

    // Recorrido, freno por Kuntur, motor y claxon: lo mismo para carros,
    // combis y buses. Lo que cambia es el tamaño (cuánto carril barre y dónde
    // está la trompa) y lo grave del motor.
    private static void AttachVehicleAI(GameObject vehicle, Vector3 from, Vector3 to, float speed,
        float laneHalfWidth, float halfLength, float pitchScale)
    {
        CarPatrol patrol = vehicle.AddComponent<CarPatrol>();
        SerializedObject so = new SerializedObject(patrol);
        so.FindProperty("pointA").vector3Value = from;
        so.FindProperty("pointB").vector3Value = to;
        so.FindProperty("speed").floatValue = speed;
        so.FindProperty("laneHalfWidth").floatValue = laneHalfWidth;
        so.FindProperty("vehicleHalfLength").floatValue = halfLength;
        so.FindProperty("pitchScale").floatValue = pitchScale;
        so.FindProperty("engineClip").objectReferenceValue = LoadAudio("SFX_Motor");
        so.FindProperty("hornClip").objectReferenceValue = LoadAudio("SFX_Claxon");
        so.ApplyModifiedProperties();
    }

    // Texto 3D pegado a un vehículo (la ruta de la combi, el número del bus).
    // Se lee de frente, desde adelante del vehículo.
    private static void AddVehicleText(Transform vehicle, Vector3 position, string text, float fontSize, Color color, Vector2 size)
    {
        GameObject textGO = new GameObject("Ruta");
        textGO.transform.SetParent(vehicle);
        textGO.transform.position = position;
        textGO.transform.rotation = ReadableFrom(Quaternion.identity); // el vehículo se arma mirando al +Z

        TextMeshPro label = textGO.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.fontStyle = FontStyles.Bold;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        if (defaultFont != null) label.font = defaultFont;
        label.rectTransform.sizeDelta = size;
    }

    // Combi de las de Huancayo: blanca o crema, con una franja de color a lo
    // largo, ventanas corridas y la ruta escrita arriba del parabrisas.
    private static void BuildCombi(Transform parent, string name, Color bodyColor, Color stripeColor,
        Vector3 from, Vector3 to, float speed, string route)
    {
        if (TryBuildAssetVehicle(parent, name, VehicleKind.Combi, from, to, speed, route, true)) return;

        GameObject combi = new GameObject(name);
        combi.transform.SetParent(parent);
        combi.transform.position = from;

        Color glass = HexColor("#1d2b36");
        Color dark = HexColor("#2b2b2b");

        PrimitiveObject(combi.transform, "Carroceria", PrimitiveType.Cube,
            from + new Vector3(0f, 0.9f, 0f), new Vector3(1.95f, 1.1f, 5f), bodyColor);
        PrimitiveObject(combi.transform, "Techo", PrimitiveType.Cube,
            from + new Vector3(0f, 1.8f, -0.08f), new Vector3(1.92f, 0.72f, 4.8f), bodyColor);

        for (int side = -1; side <= 1; side += 2)
        {
            PrimitiveObject(combi.transform, $"Ventanas_{side}", PrimitiveType.Cube,
                from + new Vector3(side * 0.965f, 1.84f, -0.35f), new Vector3(0.04f, 0.46f, 3.9f), glass);
            PrimitiveObject(combi.transform, $"Franja_{side}", PrimitiveType.Cube,
                from + new Vector3(side * 0.985f, 1.12f, 0f), new Vector3(0.03f, 0.2f, 4.95f), stripeColor);
        }

        PrimitiveObject(combi.transform, "Parabrisas", PrimitiveType.Cube,
            from + new Vector3(0f, 1.75f, 2.335f), new Vector3(1.72f, 0.58f, 0.04f), glass);
        PrimitiveObject(combi.transform, "Luneta", PrimitiveType.Cube,
            from + new Vector3(0f, 1.8f, -2.49f), new Vector3(1.6f, 0.48f, 0.04f), glass);
        PrimitiveObject(combi.transform, "Franja_Frente", PrimitiveType.Cube,
            from + new Vector3(0f, 1.12f, 2.51f), new Vector3(1.9f, 0.2f, 0.03f), stripeColor);
        PrimitiveObject(combi.transform, "Parrilla", PrimitiveType.Cube,
            from + new Vector3(0f, 0.55f, 2.54f), new Vector3(1.95f, 0.22f, 0.12f), dark);
        PrimitiveObject(combi.transform, "Parachoque_Atras", PrimitiveType.Cube,
            from + new Vector3(0f, 0.5f, -2.54f), new Vector3(1.95f, 0.22f, 0.12f), dark);
        PrimitiveObject(combi.transform, "Parrilla_Techo", PrimitiveType.Cube,
            from + new Vector3(0f, 2.2f, -0.3f), new Vector3(1.5f, 0.07f, 3.2f), HexColor("#5b5f63"));

        // Cartel de ruta: plancha blanca arriba del parabrisas.
        PrimitiveObject(combi.transform, "Cartel_Ruta", PrimitiveType.Cube,
            from + new Vector3(0f, 2.08f, 2.3f), new Vector3(1.4f, 0.22f, 0.04f), HexColor("#f7f7f2"));
        AddVehicleText(combi.transform, from + new Vector3(0f, 2.08f, 2.335f), route, 1.05f, HexColor("#b3261e"), new Vector2(1.36f, 0.2f));

        for (int i = 0; i < 4; i++)
        {
            float wx = i % 2 == 0 ? -0.92f : 0.92f;
            float wz = i < 2 ? 1.7f : -1.7f;
            PrimitiveObject(combi.transform, $"Rueda_{i}", PrimitiveType.Cylinder,
                from + new Vector3(wx, 0.36f, wz), new Vector3(0.72f, 0.15f, 0.72f), HexColor("#1c1f22"),
                Quaternion.Euler(0f, 0f, 90f));
        }

        AddVehicleLights(combi.transform, from, 0.72f, 0.72f, 2.53f, 2.53f);

        foreach (Transform child in combi.transform) child.gameObject.isStatic = false;

        // Más angosta que el carril de un bus pero más larga que un carro, y
        // con el motor un poco más agudo, como una combi exigida.
        AttachVehicleAI(combi, from, to, speed, 1.65f, 2.5f, 1.1f);
    }

    // Bus urbano: largo, alto, con ventanas corridas, puerta adelante y el
    // número de línea en un letrero luminoso sobre el parabrisas.
    private static void BuildBus(Transform parent, string name, Color bodyColor, Vector3 from, Vector3 to, float speed, string line)
    {
        if (TryBuildAssetVehicle(parent, name, VehicleKind.Bus, from, to, speed, line, true)) return;

        GameObject bus = new GameObject(name);
        bus.transform.SetParent(parent);
        bus.transform.position = from;

        Color glass = HexColor("#1b2833");

        PrimitiveObject(bus.transform, "Carroceria", PrimitiveType.Cube,
            from + new Vector3(0f, 1.6f, 0f), new Vector3(2.45f, 2.5f, 10f), bodyColor);
        PrimitiveObject(bus.transform, "Techo", PrimitiveType.Cube,
            from + new Vector3(0f, 2.92f, 0f), new Vector3(2.3f, 0.16f, 9.6f), HexColor("#dcdcdc"));

        for (int side = -1; side <= 1; side += 2)
        {
            PrimitiveObject(bus.transform, $"Ventanas_{side}", PrimitiveType.Cube,
                from + new Vector3(side * 1.23f, 2.1f, -0.5f), new Vector3(0.04f, 0.95f, 8.2f), glass);
            PrimitiveObject(bus.transform, $"Franja_{side}", PrimitiveType.Cube,
                from + new Vector3(side * 1.235f, 1.18f, 0f), new Vector3(0.03f, 0.22f, 9.9f), HexColor("#f4f4f2"));
        }

        // Puerta de entrada al lado derecho (en Perú se sube por la derecha).
        PrimitiveObject(bus.transform, "Puerta", PrimitiveType.Cube,
            from + new Vector3(1.24f, 1.4f, 3.9f), new Vector3(0.05f, 2f, 1.1f), HexColor("#2c3e4a"));

        PrimitiveObject(bus.transform, "Parabrisas", PrimitiveType.Cube,
            from + new Vector3(0f, 1.95f, 5.01f), new Vector3(2.2f, 1.25f, 0.05f), glass);
        PrimitiveObject(bus.transform, "Luneta", PrimitiveType.Cube,
            from + new Vector3(0f, 2.1f, -5.01f), new Vector3(2f, 0.8f, 0.05f), glass);
        PrimitiveObject(bus.transform, "Parachoque", PrimitiveType.Cube,
            from + new Vector3(0f, 0.55f, 5.05f), new Vector3(2.45f, 0.28f, 0.14f), HexColor("#2b2b2b"));

        // Letrero de línea: fondo negro y letras ámbar, como los de LED.
        PrimitiveObject(bus.transform, "Letrero_Linea", PrimitiveType.Cube,
            from + new Vector3(0f, 2.68f, 5.02f), new Vector3(1.9f, 0.34f, 0.05f), HexColor("#111111"));
        AddVehicleText(bus.transform, from + new Vector3(0f, 2.68f, 5.06f), line, 2.4f, HexColor("#ffb020"), new Vector2(1.85f, 0.32f));

        for (int i = 0; i < 4; i++)
        {
            float wx = i % 2 == 0 ? -1.16f : 1.16f;
            float wz = i < 2 ? 3.3f : -3.3f;
            PrimitiveObject(bus.transform, $"Rueda_{i}", PrimitiveType.Cylinder,
                from + new Vector3(wx, 0.5f, wz), new Vector3(1f, 0.2f, 1f), HexColor("#1c1f22"),
                Quaternion.Euler(0f, 0f, 90f));
        }

        AddVehicleLights(bus.transform, from, 0.95f, 0.7f, 5.03f, 5.03f);

        foreach (Transform child in bus.transform) child.gameObject.isStatic = false;

        // Motor grave y lento de arrancar: pitchScale bajo.
        AttachVehicleAI(bus, from, to, speed, 1.9f, 5f, 0.72f);
    }

    // Faros adelante (se prenden de noche) y luces rojas atrás.
    private static void AddVehicleLights(Transform vehicle, Vector3 from, float halfSpread, float height, float front, float back)
    {
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -halfSpread : halfSpread;

            GameObject headlight = PrimitiveObject(vehicle, $"Faro_{i}", PrimitiveType.Cube,
                from + new Vector3(x, height, front), new Vector3(0.36f, 0.2f, 0.08f), HexColor("#f5efd0"));
            AddNightLight(headlight, null, HexColor("#fff3c4"), 2.2f);

            GameObject taillight = PrimitiveObject(vehicle, $"Stop_{i}", PrimitiveType.Cube,
                from + new Vector3(x, height, -back), new Vector3(0.3f, 0.18f, 0.08f), HexColor("#b3261e"));
            AddNightLight(taillight, null, HexColor("#ff3b2f"), 1.6f);
        }
    }

    // Auto quieto (estacionamiento del Plaza Vea): mismo carro de la calle,
    // pero sin recorrido ni motor.
    private static void BuildParkedCar(Transform parent, Vector3 center, Color bodyColor, float yaw)
    {
        if (TryBuildAssetVehicle(parent, "Auto_Estacionado", VehicleKind.Car, center, center, 0f, null, false, yaw)) return;

        GameObject car = new GameObject("Auto_Estacionado");
        car.transform.SetParent(parent);
        car.transform.position = center;
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

        PrimitiveObject(car.transform, "Chasis", PrimitiveType.Cube,
            center + rot * new Vector3(0f, 0.62f, 0f), new Vector3(1.9f, 0.7f, 4.2f), bodyColor, rot);
        PrimitiveObject(car.transform, "Cabina", PrimitiveType.Cube,
            center + rot * new Vector3(0f, 1.22f, -0.25f), new Vector3(1.7f, 0.62f, 2.1f), HexColor("#2c3e50"), rot);

        for (int i = 0; i < 4; i++)
        {
            float wx = i % 2 == 0 ? -0.92f : 0.92f;
            float wz = i < 2 ? 1.35f : -1.35f;
            PrimitiveObject(car.transform, $"Rueda_{i}", PrimitiveType.Cylinder,
                center + rot * new Vector3(wx, 0.33f, wz), new Vector3(0.66f, 0.14f, 0.66f), HexColor("#1c1f22"),
                rot * Quaternion.Euler(0f, 0f, 90f));
        }
    }

    // ---------------------------------------------------------------
    // Sonido
    // ---------------------------------------------------------------

    private const string AudioDir = ArtDir + "/Audio";

    // Busca el clip por nombre sin importar la extensión: las músicas largas
    // entran como .ogg y todo lo demás como .wav.
    private static AudioClip LoadAudio(string fileName)
    {
        foreach (string extension in new[] { ".wav", ".ogg", ".mp3" })
        {
            string path = $"{AudioDir}/{fileName}{extension}";
            if (!File.Exists(path)) continue;

            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) return clip;
        }

        Debug.LogWarning($"[Kuntur] Falta el audio {fileName} en {AudioDir}. Esa parte queda en silencio.");
        return null;
    }

    // Fuente de sonido de ambiente, en 3D y dando vueltas. Se usa para el río
    // y los grillos: varias repartidas a lo largo del cauce en vez de una
    // sola, así el sonido acompaña a Kuntur mientras camina por la orilla en
    // lugar de aparecer y desaparecer en un punto.
    private static AudioSource BuildAmbienceSource(Transform parent, string name, Vector3 position,
        AudioClip clip, float volume, float minDistance, float maxDistance, bool playNow)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;

        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = playNow;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f; // el río no se mueve: sin efecto Doppler
        return source;
    }

    // Río sonando a lo largo de todo el cauce, y grillos por el valle que solo
    // se oyen de noche (más fuerte junto al agua, que es donde de verdad hay).
    private static void BuildAmbience(Transform parent)
    {
        GameObject ambience = new GameObject("Ambiente_Sonido");
        ambience.transform.SetParent(parent);

        const float RiverVolume = 0.85f;

        AudioClip river = LoadAudio("Amb_Rio");
        AudioClip crickets = LoadAudio("Amb_Grillos");

        // El cauce va de z=-100 a z=100 sobre x=RiverCenterX. Una fuente cada
        // 26 m con alcance 34 m: se solapan lo justo para que no haya huecos
        // de silencio al caminar por la orilla.
        if (river != null)
        {
            for (float z = RiverStartZ + 8f; z <= RiverEndZ - 8f; z += 26f)
            {
                AudioSource source = BuildAmbienceSource(ambience.transform, $"Rio_Sonido_{z:0}",
                    new Vector3(RiverX(z), 0.6f, z), river, RiverVolume, 7f, 34f, true);

                AmbienceSourceVolume volume = source.gameObject.AddComponent<AmbienceSourceVolume>();
                SerializedObject volumeSo = new SerializedObject(volume);
                volumeSo.FindProperty("baseVolume").floatValue = RiverVolume;
                volumeSo.ApplyModifiedProperties();
            }
        }

        if (crickets == null) return;

        // Grillos del río: más fuerte, porque es donde hay agua y pasto.
        for (float z = -240f; z <= 240f; z += 40f)
        {
            AudioSource source = BuildAmbienceSource(ambience.transform, $"Grillos_Rio_{z:0}",
                new Vector3(RiverX(z) - 4f, 0.8f, z), crickets, 0f, 9f, 48f, false);
            NightAmbience night = source.gameObject.AddComponent<NightAmbience>();
            SerializedObject so = new SerializedObject(night);
            so.FindProperty("maxVolume").floatValue = 0.8f;
            so.ApplyModifiedProperties();
        }

        // Grillos del pueblo: los mismos bichos, pero de lejos.
        Vector3[] townSpots =
        {
            new Vector3(-18f, 0.8f, -30f), new Vector3(6f, 0.8f, 26f),
            new Vector3(-26f, 0.8f, 34f), new Vector3(22f, 0.8f, -12f),
        };

        foreach (Vector3 spot in townSpots)
        {
            AudioSource source = BuildAmbienceSource(ambience.transform, "Grillos_Pueblo", spot,
                crickets, 0f, 8f, 40f, false);
            NightAmbience night = source.gameObject.AddComponent<NightAmbience>();
            SerializedObject so = new SerializedObject(night);
            so.FindProperty("maxVolume").floatValue = 0.45f;
            so.ApplyModifiedProperties();
        }
    }

    // Agrega el componente que enciende algo al anochecer. lightSource puede
    // ser null cuando solo se quiere que la pieza brille (ventanas, faros).
    private static void AddNightLight(GameObject target, Light lightSource, Color glowColor, float intensity)
    {
        NightLight nightLight = target.AddComponent<NightLight>();
        SerializedObject so = new SerializedObject(nightLight);
        so.FindProperty("lightSource").objectReferenceValue = lightSource;
        so.FindProperty("glowRenderer").objectReferenceValue = target.GetComponent<Renderer>();
        so.FindProperty("glowColor").colorValue = glowColor;
        so.FindProperty("glowIntensity").floatValue = intensity;
        so.ApplyModifiedProperties();
    }

    // Tramo de pista horizontal (a lo largo del eje X) entre dos puntos, con
    // su línea central. Se usa para poder cortar las calles en la orilla del
    // río en vez de tirar una plancha de asfalto de punta a punta del mapa.
    private static void BuildRoadSegment(Transform parent, string name, float fromX, float toX, float z, float width, Color asphalt, Color line)
    {
        float length = toX - fromX;
        if (length <= 0.1f) return;

        float centerX = (fromX + toX) / 2f;

        PrimitiveObject(parent, name, PrimitiveType.Cube,
            new Vector3(centerX, 0.01f, z), new Vector3(length, 0.02f, width), asphalt);

        PrimitiveObject(parent, name + "_Linea_A", PrimitiveType.Cube,
            new Vector3(centerX, 0.03f, z - 0.25f), new Vector3(length - 2f, 0.02f, 0.2f), line);
        PrimitiveObject(parent, name + "_Linea_B", PrimitiveType.Cube,
            new Vector3(centerX, 0.03f, z + 0.25f), new Vector3(length - 2f, 0.02f, 0.2f), line);
    }

    // Igual que BuildRoadSegment pero para calles que corren a lo largo del
    // eje Z (paralelas a la Calle Real).
    private static void BuildRoadSegmentZ(Transform parent, string name, float x, float fromZ, float toZ, float width, Color asphalt, Color line)
    {
        float length = toZ - fromZ;
        if (length <= 0.1f) return;

        float centerZ = (fromZ + toZ) / 2f;

        PrimitiveObject(parent, name, PrimitiveType.Cube,
            new Vector3(x, 0.01f, centerZ), new Vector3(width, 0.02f, length), asphalt);

        PrimitiveObject(parent, name + "_Linea_A", PrimitiveType.Cube,
            new Vector3(x - 0.25f, 0.03f, centerZ), new Vector3(0.2f, 0.02f, length - 2f), line);
        PrimitiveObject(parent, name + "_Linea_B", PrimitiveType.Cube,
            new Vector3(x + 0.25f, 0.03f, centerZ), new Vector3(0.2f, 0.02f, length - 2f), line);
    }

    // roadCenter = dónde está el eje de la pista (X si la vereda corre en Z,
    // Z si corre en X). Antes el bordillo se deducía del signo de la posición
    // y del valor fijo 30, así que cualquier vereda que no fuera la de la Av.
    // Giráldez terminaba con el sardinel del lado equivocado.
    private static void BuildSidewalk(Transform parent, string name, Vector3 position, Vector3 scale, Color surface, Color curb, bool alongZ, float roadCenter)
    {
        PrimitiveObject(parent, name, PrimitiveType.Cube, position + Vector3.up * 0.06f, scale, surface);

        // Bordillo: una tira un poco más alta en el lado que da a la pista.
        Vector3 curbPos = alongZ
            ? position + new Vector3(Mathf.Sign(roadCenter - position.x) * (scale.x / 2f), 0.09f, 0f)
            : position + new Vector3(0f, 0.09f, Mathf.Sign(roadCenter - position.z) * (scale.z / 2f));
        Vector3 curbScale = alongZ ? new Vector3(0.35f, 0.18f, scale.z) : new Vector3(scale.x, 0.18f, 0.35f);
        PrimitiveObject(parent, name + "_Bordillo", PrimitiveType.Cube, curbPos, curbScale, curb);
    }

    // Vereda que corre a lo largo de Z y se CORTA en cada cruce. En una calle
    // de verdad la vereda termina en el sardinel y el peatón cruza por la
    // cebra: antes era una sola tira de 170 metros que pasaba por encima de la
    // Av. Giráldez y de los cinco jirones, que es lo que hacía que las
    // esquinas no se leyeran como esquinas.
    private static void BuildSidewalkAlongZ(Transform parent, string name, float x, float roadCenterX,
        float fromZ, float toZ, Color surface, Color curb)
    {
        List<Vector2> gaps = new List<Vector2>
        {
            new Vector2(AvenueZ - AvenueHalf - 0.2f, AvenueZ + AvenueHalf + 0.2f),
        };
        foreach (float z in CrossStreetsZ)
            gaps.Add(new Vector2(z - CrossStreetHalfRoad - 0.2f, z + CrossStreetHalfRoad + 0.2f));

        int piece = 0;
        BuildSidewalkPieces(gaps, fromZ, toZ, (from, to) =>
        {
            BuildSidewalk(parent, $"{name}_{piece++}", new Vector3(x, 0f, (from + to) / 2f),
                new Vector3(SidewalkWidth, 0.12f, to - from), surface, curb, true, roadCenterX);
        });
    }

    // Lo mismo para las veredas que corren a lo largo de X (Av. Giráldez y los
    // jirones): se cortan en la Calle Real, el Jr. Puno y el Jr. Cusco.
    private static void BuildSidewalkAlongX(Transform parent, string name, float z, float roadCenterZ,
        float fromX, float toX, Color surface, Color curb)
    {
        List<Vector2> gaps = new List<Vector2>
        {
            new Vector2(MainStreetX - MainStreetHalf - 0.2f, MainStreetX + MainStreetHalf + 0.2f),
            new Vector2(PunoX - CrossStreetHalfRoad - 0.2f, PunoX + CrossStreetHalfRoad + 0.2f),
            new Vector2(CuscoX - CrossStreetHalfRoad - 0.2f, CuscoX + CrossStreetHalfRoad + 0.2f),
            // v55b: también el Jr. Arequipa (antes la vereda de los jirones
            // pasaba por encima de la pista y la gente "cruzaba por la vereda").
            new Vector2(ArequipaX - CrossStreetHalfRoad - 0.2f, ArequipaX + CrossStreetHalfRoad + 0.2f),
        };

        int piece = 0;
        BuildSidewalkPieces(gaps, fromX, toX, (from, to) =>
        {
            BuildSidewalk(parent, $"{name}_{piece++}", new Vector3((from + to) / 2f, 0f, z),
                new Vector3(to - from, 0.12f, SidewalkWidth), surface, curb, false, roadCenterZ);
        });
    }

    // Recorre el tramo de "from" a "to" saltándose los huecos de los cruces y
    // manda construir cada pedazo suelto.
    private static void BuildSidewalkPieces(List<Vector2> gaps,
        float from, float to, System.Action<float, float> build)
    {
        gaps.Sort((a, b) => a.x.CompareTo(b.x));

        float cursor = from;
        foreach (Vector2 gap in gaps)
        {
            if (gap.y <= cursor) continue;   // el hueco quedó atrás
            if (gap.x >= to) break;          // ya nos pasamos del tramo

            float end = Mathf.Min(gap.x, to);
            if (end - cursor > 1.2f) build(cursor, end);
            cursor = Mathf.Max(cursor, gap.y);
        }

        if (to - cursor > 1.2f) build(cursor, to);
    }

    // side = -1 si el brazo sale hacia -X, +1 si sale hacia +X.
    private static void BuildTrafficLight(Transform parent, Vector3 basePosition, float side)
    {
        if (IsOnRoadway(basePosition)) return; // el poste va en la vereda, no en la pista

        GameObject light = new GameObject("Semaforo");
        light.transform.SetParent(parent);

        Color metal = HexColor("#9aa0a6");
        Color box = HexColor("#2a2d31");

        PrimitiveObject(light.transform, "Poste", PrimitiveType.Cylinder,
            basePosition + Vector3.up * 3.1f, new Vector3(0.16f, 3.1f, 0.16f), metal);

        // Brazo horizontal que cruza sobre la pista.
        PrimitiveObject(light.transform, "Brazo", PrimitiveType.Cube,
            basePosition + new Vector3(side * 2.4f, 6f, 0f), new Vector3(4.8f, 0.16f, 0.16f), metal);

        Vector3 headPos = basePosition + new Vector3(side * 4.4f, 5.2f, 0f);
        PrimitiveObject(light.transform, "Caja", PrimitiveType.Cube,
            headPos, new Vector3(0.5f, 1.4f, 0.42f), box);

        Color[] lightColors = { HexColor("#e74c3c"), HexColor("#f1c40f"), HexColor("#2ecc71") };
        for (int i = 0; i < 3; i++)
        {
            PrimitiveObject(light.transform, "Luz" + i, PrimitiveType.Sphere,
                headPos + new Vector3(0f, 0.42f - i * 0.42f, 0.2f), Vector3.one * 0.26f, lightColors[i]);
        }
    }

    private static int lampCounter;

    private static void BuildStreetLamp(Transform parent, Vector3 basePosition, float side)
    {
        BuildStreetLamp(parent, basePosition, new Vector3(side, 0f, 0f), true);
    }

    private static void BuildStreetLamp(Transform parent, Vector3 basePosition, float side, bool allowRealLight)
    {
        BuildStreetLamp(parent, basePosition, new Vector3(side, 0f, 0f), allowRealLight);
    }

    // armDirection = hacia dónde sale el brazo (siempre hacia la pista). Antes
    // solo podía apuntar en X, así que los postes de los jirones -que corren
    // en la otra dirección- quedaban con el foco apuntando a la vereda.
    private static void BuildStreetLamp(Transform parent, Vector3 basePosition, Vector3 armDirection, bool allowRealLight)
    {
        // Un poste plantado en medio del asfalto se ve pésimo y además no
        // debería existir. Pasa cuando una posición calculada "cada 16 metros"
        // cae justo donde cruza un jirón: en vez de corregir a mano cada caso,
        // el poste simplemente no se pone ahí.
        if (IsOnRoadway(basePosition)) return;

        GameObject lamp = new GameObject("PosteDeLuz");
        lamp.transform.SetParent(parent);

        Vector3 dir = armDirection.sqrMagnitude < 0.001f ? Vector3.left : armDirection.normalized;
        dir.y = 0f;
        // El brazo es un cubo alargado en su X local: se gira para que esa X
        // apunte en la dirección pedida.
        float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
        Quaternion armRotation = Quaternion.Euler(0f, yaw, 0f);

        // Poste del asset POLYGON si está; si no, el de primitivas.
        Vector3? assetBulb = TryBuildAssetLampPost(lamp.transform, basePosition, dir);
        GameObject bulb;
        Vector3 lightPosition;
        if (assetBulb.HasValue)
        {
            // Pantallita que brilla de noche justo bajo la cabeza del farol.
            bulb = PrimitiveObject(lamp.transform, "Lampara", PrimitiveType.Cube,
                assetBulb.Value - Vector3.up * 0.12f, new Vector3(0.5f, 0.06f, 0.28f), HexColor("#dfe6ea"), armRotation);
            lightPosition = assetBulb.Value - Vector3.up * 0.5f + dir * 0.2f;
        }
        else
        {
            Color metal = HexColor("#9aa0a6");
            PrimitiveObject(lamp.transform, "Poste", PrimitiveType.Cylinder,
                basePosition + Vector3.up * 3.4f, new Vector3(0.14f, 3.4f, 0.14f), metal);
            PrimitiveObject(lamp.transform, "Brazo", PrimitiveType.Cube,
                basePosition + dir * 0.9f + Vector3.up * 6.7f, new Vector3(1.8f, 0.12f, 0.12f), metal, armRotation);

            bulb = PrimitiveObject(lamp.transform, "Lampara", PrimitiveType.Cube,
                basePosition + dir * 1.7f + Vector3.up * 6.55f, new Vector3(0.7f, 0.18f, 0.35f), HexColor("#dfe6ea"), armRotation);
            lightPosition = basePosition + dir * 1.9f + Vector3.up * 6.1f;
        }
        bulb.isStatic = false;

        // TODOS los postes llevan su luz real. Antes solo uno de cada tres,
        // porque con una luz por poste eran demasiadas luces en tiempo real
        // para una laptop con gráficos integrados. Ahora cada luz se anota en
        // StreetLightBudget, que de noche prende solo las más cercanas a la
        // cámara: todos los focos brillan (eso lo hace NightLight y no cuesta
        // nada), pero la tarjeta de video solo carga con unas pocas luces a la
        // vez, sin importar cuántos postes haya en el pueblo.
        lampCounter++;

        GameObject lightGO = new GameObject("Luz");
        lightGO.transform.SetParent(lamp.transform);
        // Un poco por debajo del foco y hacia la pista, para que el charco de
        // luz caiga en el asfalto y no en la vereda.
        lightGO.transform.position = lightPosition;

        Light pointLight = lightGO.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.range = 18f;
        pointLight.intensity = 2.6f;
        pointLight.color = HexColor("#ffdca0"); // sodio cálido, como el alumbrado de verdad
        pointLight.shadows = LightShadows.None;  // sombras dinámicas = muerte del rendimiento
        pointLight.renderMode = LightRenderMode.ForcePixel;
        pointLight.enabled = false;
        lightGO.AddComponent<BudgetedLight>();

        // A NightLight solo se le pasa el foco (el brillo). La luz real la
        // maneja el presupuesto; si la manejaran los dos, cada uno la
        // prendería y apagaría por su cuenta y parpadearía.
        AddNightLight(bulb, null, HexColor("#fff0c0"), 2.6f);
    }

    private static void BuildHouses(Transform parent)
    {
        GameObject houses = new GameObject("Casas");
        houses.transform.SetParent(parent);

        // Casas alineadas a ambos lados de la calle principal, como la imagen
        // de la calle del pueblo.
        Color[] walls =
        {
            HexColor("#d8c9a8"), HexColor("#c9a37a"), HexColor("#e0d3b4"),
            HexColor("#b98d63"), HexColor("#d3c2a0"), HexColor("#c08a5e"),
        };

        System.Random rng = new System.Random(7788);

        // "Bodega Andina" de Doña Rosa primero: es parte de una misión, así
        // que se queda con su terreno y las demás casas se acomodan alrededor.
        // Tienda de colores como las del valle (ver BuildBodegaAndina).
        BuildBodegaAndina(houses.transform);
        // La casa de Kuntur, en la esquina sur-oeste: de ahí sale cada mañana.
        BuildKunturHome(houses.transform);

        // Edificios del centro (POLYGON city pack) ANTES de las casas: toman
        // los frentes de la Calle Real y de Puno/Cusco, y las casas de adobe
        // llenan los huecos que quedan y el resto del pueblo.
        BuildCityBuildings(houses.transform, new System.Random(2468));

        // Las casas ya no van en posiciones fijas (así chocaban con los
        // jirones y se perdía media cuadra): se recorre cada frente de calle
        // de punta a punta y se levanta una casa donde haya sitio. Lo que cae
        // sobre una pista u otra casa se descarta solo.
        // Cada frente está justo a la distancia mínima del eje de su calle:
        // filo de vereda (7 en la Calle Real, 6.1 en los jirones) más el medio
        // fondo de la casa. Puestos "a ojo" -como estaban- la fila entera caía
        // dentro del terreno reservado de la calle y no se construía NADA.
        (float x, string nombre)[] frentes =
        {
            (-93f, "Arequipa_Oeste"), (-67f, "Arequipa_Este"),  // Jr. Arequipa (ampliación)
            (-12f, "Real_Oeste"), (12f, "Real_Este"),        // Calle Real
            (-55f, "Puno_Oeste"), (-31f, "Puno_Este"),       // Jr. Puno
            (22.8f, "Cusco_Oeste"), (45f, "Cusco_Este"),     // Jr. Cusco
        };

        foreach ((float x, string nombre) frente in frentes)
        {
            int index = 0;
            // Paso corto a propósito: se intentan muchas posiciones y las que
            // chocan con un jirón o con la casa vecina se descartan solas, así
            // las cuadras quedan llenas en vez de con huecos grandes. Con paso
            // de 7.5 la casa siguiente siempre chocaba con la anterior y se
            // terminaba colocando una cada 15 metros, con un vacío enorme en
            // medio; con paso de 2.5 el empaquetado se ajusta al hueco real
            // que queda en la cuadra y las casas salen casi pared con pared,
            // como en cualquier jirón del centro.
            for (float z = SideStreetSouthZ - 4f; z <= SideStreetNorthZ + 4f; z += 2.5f)
            {
                float offset = (frente.x > 0f ? 2f : 0f); // las dos aceras van alternadas
                // Frente angosto (6 a 7.5 de cara a la calle) y fondo largo
                // (7.5 a 9 hacia adentro del terreno): así es una casa de
                // pueblo, y así entran dos o tres por cuadra en vez de una.
                BuildHouse(houses.transform, $"Casa_{frente.nombre}_{index}",
                    new Vector3(frente.x, 0f, z + offset),
                    NextFloat(rng, 3.2f, 5.4f), NextFloat(rng, 7.5f, 9f), NextFloat(rng, 5.5f, 6.8f),
                    walls[index % walls.Length], rng);
                index++;
            }
        }

        // (El "barrio del fondo" de casas sueltas se reemplazó por las
        // manzanas del Jr. Arequipa: el pueblo ahora es cuadriculado de punta
        // a punta, como el centro de Huancayo.)

        // v56: casas también de cara a los jirones y a las circunvalaciones
        // (pegadas a la vereda), y después se rellenan los huecos de pasto que
        // quedaban vacíos adentro de las manzanas.
        BuildStreetFacingHouses(houses.transform, walls, new System.Random(5656));
        FillEmptyLots(houses.transform, walls, new System.Random(9191));

        BuildEastNeighborhood(houses.transform, walls, rng);
        BuildVillagers(houses.transform);
    }

    // Barrio de la otra orilla. Toda la llanura al este del río era pasto
    // vacío hasta las montañas: ahora tiene dos filas de casas dando al camino
    // de tierra y unas cuantas chacras sueltas más adentro, que es como se ve
    // el campo alrededor de Huancayo desde el puente.
    private static void BuildEastNeighborhood(Transform parent, Color[] walls, System.Random rng)
    {
        int index = 0;

        foreach (float x in new[] { EastRoadX - 11f, EastRoadX + 11f })
        {
            for (float z = EastRoadFromZ + 8f; z <= EastRoadToZ - 8f; z += 2.5f)
            {
                BuildHouse(parent, $"Casa_Chacra_{index}", new Vector3(x, 0f, z),
                    NextFloat(rng, 2.8f, 4.4f), NextFloat(rng, 7f, 8.5f), NextFloat(rng, 5.5f, 6.8f),
                    walls[index % walls.Length], rng);
                index++;
            }
        }

        // Casas sueltas en el campo. El tope de 106 en X no es arbitrario: las
        // montañas del fondo empiezan a apoyarse en el suelo cerca de x=113, y
        // una casa metida dentro del cerro se ve pésimo.
        for (int i = 0; i < 14; i++)
        {
            Vector3 pos = RandomFreeSpot(rng, 72f, 108f, -95f, 95f, 5f);
            if (pos == Vector3.zero) continue;

            BuildHouse(parent, $"Casa_Campo_{i}", pos,
                NextFloat(rng, 2.6f, 3.8f), NextFloat(rng, 6f, 7.5f), NextFloat(rng, 6f, 7.5f),
                walls[i % walls.Length], rng);
        }
    }

    // Vecinos conversando por el pueblo: no dan misión, están ahí para que el
    // valle se sienta habitado. Se paran en parejas, mirándose, y les sale un
    // globito de diálogo por turnos (ver VillagerIdle).
    private static void BuildVillagers(Transform parent)
    {
        GameObject villagers = new GameObject("Vecinos");
        villagers.transform.SetParent(parent);

        // Parejas repartidas: en la vereda, en el barrio del fondo y cerca de
        // la bodega, que es donde la gente se junta a conversar.
        // Todos parados SOBRE la vereda. Con las hileras de casas nuevas, los
        // puntos de antes (x ±9.5) caían justo dentro de una pared: entre el
        // filo de la vereda y la fachada ya no queda pasto donde pararse.
        Vector3[] spots =
        {
            new Vector3(-5.5f, 0f, 6f), new Vector3(-5.5f, 0f, -12f),
            // Los del oeste van en las veredas del Jr. Arequipa (la calle nueva
            // pasa justo por donde antes estaban parados).
            new Vector3(5.5f, 0f, 44f), new Vector3(-75.3f, 0f, -30f),
            new Vector3(-84.9f, 0f, 10f), new Vector3(28.5f, 0f, 18f),
        };

        Color[] ponchos =
        {
            HexColor("#c0392b"), HexColor("#2980b9"), HexColor("#8e44ad"),
            HexColor("#16a085"), HexColor("#d35400"), HexColor("#7f5539"),
        };

        System.Random rng = new System.Random(31415);

        for (int i = 0; i < spots.Length; i++)
        {
            Vector3 a = spots[i];
            // El compañero va al costado, pero cerca: con 1.7 de separación el
            // segundo vecino terminaba parado en la pista.
            Vector3 b = spots[i] + new Vector3(1.0f, 0f, 0.7f);

            // El primero de la pareja es quien puede darte una misión: su cuerpo
            // va de acuerdo a su nombre (Doña Maruja es una señora mayor, el
            // Señor Teodoro un hombre, etc.), nada de nombres al azar.
            var giver = Givers[i % Givers.Length];
            string giverVoice = giver.voice;
            GameObject first = BuildVillager(villagers.transform, $"Vecino_{i}A", a, ponchos[i % ponchos.Length], rng, PeopleDir + "/Prefabs/" + giver.prefab);
            GameObject second = BuildVillager(villagers.transform, $"Vecino_{i}B", b, ponchos[(i + 3) % ponchos.Length], rng);

            LinkVillagers(first, second, 0f);
            LinkVillagers(second, first, 3.4f); // desfasado: hablan por turnos

            // El primero de cada pareja puede ser el vecino que te da la
            // misión de un nivel (ver MissionDirector).
            MakeMissionGiver(first, giver.name, giver.female, giver.role, interactableLayerForGivers, "", giverVoice);
        }
    }

    private static GameObject BuildVillager(Transform parent, string name, Vector3 position, Color ponchoColor, System.Random rng, string forcedPrefab = null)
    {
        GameObject assetVillager = TryBuildAssetVillager(parent, name, position, forcedPrefab);
        if (assetVillager != null) return assetVillager;

        GameObject villager = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        villager.name = name;
        villager.transform.SetParent(parent);
        villager.transform.position = position + Vector3.up;
        villager.transform.localScale = new Vector3(0.78f, 0.92f, 0.78f);
        Object.DestroyImmediate(villager.GetComponent<Collider>());
        SetColor(villager, HexColor("#6d5847"));

        DecorateNPC(villager, ponchoColor, rng.Next(2) == 0 ? HexColor("#8a5a3b") : HexColor("#3f3630"), HexColor("#c58c68"));

        // Globito de diálogo: una esferita blanca sobre la cabeza, suficiente
        // para leer "están conversando" desde lejos.
        GameObject bubble = PrimitiveObject(villager.transform, "Globo", PrimitiveType.Sphere,
            position + new Vector3(0.35f, 2.55f, 0f), new Vector3(0.5f, 0.36f, 0.5f), UIPalette_CreamSoft());
        bubble.isStatic = false;

        foreach (Transform child in villager.transform) child.gameObject.isStatic = false;
        return villager;
    }

    private static void LinkVillagers(GameObject villager, GameObject partner, float talkOffset)
    {
        VillagerIdle idle = villager.AddComponent<VillagerIdle>();
        SerializedObject so = new SerializedObject(idle);
        so.FindProperty("lookAtTarget").objectReferenceValue = partner.transform;
        so.FindProperty("talkOffset").floatValue = talkOffset;
        // Con un modelo animado, el balanceo lo hace la animación: si el
        // script además lo meciera, se vería mareado.
        so.FindProperty("animatedBody").boolValue = villager.GetComponentInChildren<Animator>() != null;

        Transform bubble = villager.transform.Find("Globo");
        if (bubble != null) so.FindProperty("speechBubble").objectReferenceValue = bubble.gameObject;

        so.ApplyModifiedProperties();
    }

    // A qué calle (centro en X) mira una casa según dónde está parada. Las
    // tres calles con casas a los lados son norte-sur, por eso todo se mide
    // en X: la casa que quede más cerca de una de las tres, mira hacia ella.
    private static float NearestStreetCenterX(float x)
    {
        float[] streetCentersX = { MainStreetX, PunoX, CuscoX, EastRoadX };
        float best = streetCentersX[0];
        float bestDist = Mathf.Abs(x - best);
        foreach (float center in streetCentersX)
        {
            float dist = Mathf.Abs(x - center);
            if (dist < bestDist) { bestDist = dist; best = center; }
        }
        return best;
    }

    private static void BuildHouse(Transform parent, string name, Vector3 groundPosition, float height, float width, float depth, Color wallColor, System.Random rng)
    {
        // Si el terreno está pisado por una calle, el río u otra casa, esta
        // casa simplemente no se levanta. Es lo que evita ver casas cruzadas
        // con jirones o metidas una dentro de otra.
        // El rectángulo que se consulta y el que se reserva son EL MISMO: si
        // no coincidieran, dos casas podrían quedar rozándose.
        float halfX = width / 2f + 0.3f;
        float halfZ = depth / 2f + 0.3f;
        if (!IsAreaFree(groundPosition, halfX, halfZ)) return;
        Occupy(groundPosition, halfX * 2f, halfZ * 2f);

        // Hacia qué lado da la fachada. Las calles con casas a los lados
        // (Real, Puno, Cusco y el camino de la otra orilla) corren de norte a
        // sur, así que la casa siempre queda A UN LADO de su calle: la puerta
        // abre en la pared que mira en X. frontSign = +1 si la calle está al
        // este de la casa, -1 si está al oeste.
        float streetCenterX = NearestStreetCenterX(groundPosition.x);
        float frontSign = groundPosition.x < streetCenterX ? 1f : -1f;
        BuildHouseBody(parent, name, groundPosition, height, width, depth, wallColor, rng, frontSign);
    }

    // v56: el cuerpo de la casa, ya con el sitio reservado. La fachada mira
    // en X (frontSign); las casas de cara a un jirón se arman igual y después
    // se giran 90° (ver BuildHouseFacingZ).
    private static GameObject BuildHouseBody(Transform parent, string name, Vector3 groundPosition, float height, float width, float depth,
        Color wallColor, System.Random rng, float frontSign)
    {
        GameObject house = new GameObject(name);
        house.transform.SetParent(parent);
        float front = frontSign * (width / 2f + 0.06f);

        // Dos pisos cuando la casa da la altura. En los pueblos del valle la
        // casa "terminada" tiene dos pisos y la de un piso es la más antigua.
        bool twoFloors = height > 4.1f;
        float floorHeight = twoFloors ? height * 0.52f : height;

        // Zócalo de piedra: casi ninguna pared nace directamente del suelo, y
        // sin él la casa se ve como un cubo apoyado encima del pasto.
        PrimitiveObject(house.transform, "Zocalo", PrimitiveType.Cube,
            groundPosition + Vector3.up * 0.21f,
            new Vector3(width + 0.28f, 0.42f, depth + 0.28f), HexColor("#8d8378"));

        // Cuerpo con collider (sí choca con el jugador, es un edificio).
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Paredes";
        body.transform.SetParent(house.transform);
        body.transform.position = groundPosition + Vector3.up * (height / 2f);
        body.transform.localScale = new Vector3(width, height, depth);
        SetColor(body, wallColor);
        body.isStatic = true;

        // Cornisa entre los dos pisos.
        if (twoFloors)
        {
            PrimitiveObject(house.transform, "Cornisa", PrimitiveType.Cube,
                groundPosition + Vector3.up * floorHeight,
                new Vector3(width + 0.22f, 0.16f, depth + 0.22f), HexColor("#b5a68e"));
        }

        BuildRoof(house.transform, groundPosition, height, width, depth, rng);
        BuildFacade(house.transform, groundPosition, front, frontSign, floorHeight, height, width, depth, twoFloors, rng);
        return house;
    }

    // Tres tipos de techo, que es lo que más cambia la silueta de una calle:
    // a cuatro aguas (teja o calamina), a dos aguas con alero, y la azotea
    // plana con tanque de agua y varillas salidas, que en cualquier pueblo del
    // Perú es la mitad de las casas.
    private static void BuildRoof(Transform parent, Vector3 groundPosition, float height, float width, float depth, System.Random rng)
    {
        int kind = rng.Next(10);
        Color tile = rng.Next(3) == 0 ? HexColor("#9aa1a8") : HexColor("#a8543f");

        if (kind < 4)
        {
            // Cuatro aguas: pirámide de base rectangular, con el alero
            // sobresaliendo un poco de la pared por los cuatro lados.
            float roofHeight = Mathf.Clamp(Mathf.Min(width, depth) * 0.3f, 1.1f, 2.4f);
            MeshObject(parent, "Techo", LowPolyMeshFactory.Pyramid(),
                groundPosition + Vector3.up * height,
                new Vector3(width + 1.1f, roofHeight, depth + 1.1f), tile);
            return;
        }

        if (kind < 7)
        {
            // Dos aguas, con la cumbrera encima y el alero sobresaliendo.
            float slope = 26f;
            float roofLength = depth * 0.62f;
            PrimitiveObject(parent, "Techo_A", PrimitiveType.Cube,
                groundPosition + new Vector3(0f, height + roofLength * 0.22f, -depth * 0.24f),
                new Vector3(width + 1.2f, 0.25f, roofLength), tile, Quaternion.Euler(-slope, 0f, 0f));
            PrimitiveObject(parent, "Techo_B", PrimitiveType.Cube,
                groundPosition + new Vector3(0f, height + roofLength * 0.22f, depth * 0.24f),
                new Vector3(width + 1.2f, 0.25f, roofLength), tile, Quaternion.Euler(slope, 0f, 0f));

            // Hastiales: tapan el triángulo que quedaba hueco a los dos lados.
            float gableHeight = Mathf.Sin(slope * Mathf.Deg2Rad) * roofLength;
            for (int side = -1; side <= 1; side += 2)
            {
                PrimitiveObject(parent, $"Hastial_{side}", PrimitiveType.Cube,
                    groundPosition + new Vector3(side * width / 2f, height + gableHeight * 0.3f, 0f),
                    new Vector3(0.22f, gableHeight * 0.62f, depth * 0.55f), HexColor("#c9b89a"));
            }

            PrimitiveObject(parent, "Cumbrera", PrimitiveType.Cube,
                groundPosition + new Vector3(0f, height + gableHeight * 0.62f, 0f),
                new Vector3(width + 1.3f, 0.18f, 0.42f), HexColor("#8b5a45"));
            return;
        }

        // Azotea: losa plana, parapeto, tanque de agua negro sobre su base y
        // las varillas de la columna esperando el segundo piso que nunca llega.
        PrimitiveObject(parent, "Losa", PrimitiveType.Cube,
            groundPosition + Vector3.up * (height + 0.12f),
            new Vector3(width + 0.4f, 0.24f, depth + 0.4f), HexColor("#b7b0a4"));

        for (int side = -1; side <= 1; side += 2)
        {
            PrimitiveObject(parent, $"Parapeto_X{side}", PrimitiveType.Cube,
                groundPosition + new Vector3(side * width / 2f, height + 0.6f, 0f),
                new Vector3(0.22f, 0.72f, depth + 0.4f), HexColor("#c6bdae"));
            PrimitiveObject(parent, $"Parapeto_Z{side}", PrimitiveType.Cube,
                groundPosition + new Vector3(0f, height + 0.6f, side * depth / 2f),
                new Vector3(width + 0.4f, 0.72f, 0.22f), HexColor("#c6bdae"));
        }

        PrimitiveObject(parent, "Tanque_Base", PrimitiveType.Cube,
            groundPosition + new Vector3(width * 0.22f, height + 0.6f, depth * 0.22f),
            new Vector3(1f, 0.7f, 1f), HexColor("#a39a8c"));
        PrimitiveObject(parent, "Tanque", PrimitiveType.Cylinder,
            groundPosition + new Vector3(width * 0.22f, height + 1.4f, depth * 0.22f),
            new Vector3(1.05f, 0.55f, 1.05f), HexColor("#2f3336"));

        for (int i = 0; i < 4; i++)
        {
            float sx = (i % 2 == 0 ? -1f : 1f) * width * 0.34f;
            float sz = (i < 2 ? -1f : 1f) * depth * 0.34f;
            PrimitiveObject(parent, $"Varilla_{i}", PrimitiveType.Cube,
                groundPosition + new Vector3(sx, height + 1.05f, sz),
                new Vector3(0.08f, 1.6f, 0.08f), HexColor("#7d6a52"));
        }
    }

    // Fachada: puerta con marco y umbral, ventanas con marco y alféizar, y en
    // las casas de dos pisos un balcón. Es el detalle que hace que se lea como
    // una casa y no como un cubo con una mancha pintada.
    private static void BuildFacade(Transform parent, Vector3 groundPosition, float front, float frontSign,
        float floorHeight, float height, float width, float depth, bool twoFloors, System.Random rng)
    {
        Color frameColor = HexColor("#6b5843");
        Color doorColor = rng.Next(3) switch
        {
            0 => HexColor("#6b4630"),
            1 => HexColor("#3f5d52"),
            _ => HexColor("#7a3b30"),
        };

        // Puerta: marco, hoja y escalón de entrada. Va un poco más afuera que
        // el zócalo (que sobresale 0.14 de la pared), si no la base de la
        // puerta queda tapada por el zócalo.
        PrimitiveObject(parent, "Puerta_Marco", PrimitiveType.Cube,
            groundPosition + new Vector3(front + frontSign * 0.1f, 1.12f, 0f),
            new Vector3(0.1f, 2.34f, 1.42f), frameColor);
        PrimitiveObject(parent, "Puerta", PrimitiveType.Cube,
            groundPosition + new Vector3(front + frontSign * 0.16f, 1.05f, 0f),
            new Vector3(0.1f, 2.1f, 1.12f), doorColor);
        PrimitiveObject(parent, "Puerta_Umbral", PrimitiveType.Cube,
            groundPosition + new Vector3(front + frontSign * 0.26f, 0.08f, 0f),
            new Vector3(0.7f, 0.16f, 1.6f), HexColor("#a49a8c"));

        // Ventanas del primer piso, a los dos lados de la puerta.
        float lowerY = twoFloors ? floorHeight * 0.62f : height * 0.62f;
        GameObject window1 = BuildWindow(parent, "Ventana_1",
            groundPosition + new Vector3(front, lowerY, -depth * 0.29f), frontSign, frameColor);
        GameObject window2 = BuildWindow(parent, "Ventana_2",
            groundPosition + new Vector3(front, lowerY, depth * 0.29f), frontSign, frameColor);

        AddNightLight(window1, null, HexColor("#ffd98a"), 1.5f);
        // La segunda ventana se prende un poco más tarde: no todos en la casa
        // se acuestan y se levantan a la misma hora.
        NightLight second = window2.AddComponent<NightLight>();
        SerializedObject secondSo = new SerializedObject(second);
        secondSo.FindProperty("glowRenderer").objectReferenceValue = window2.GetComponent<Renderer>();
        secondSo.FindProperty("glowColor").colorValue = HexColor("#ffcf72");
        secondSo.FindProperty("glowIntensity").floatValue = 1.3f;
        secondSo.FindProperty("onHour").floatValue = 18.6f + (rng.Next(0, 12) * 0.1f);
        secondSo.ApplyModifiedProperties();

        if (!twoFloors) return;

        // Segundo piso: dos ventanas más y un balcón sobre la puerta.
        float upperY = floorHeight + (height - floorHeight) * 0.55f;
        GameObject window3 = BuildWindow(parent, "Ventana_3",
            groundPosition + new Vector3(front, upperY, -depth * 0.29f), frontSign, frameColor);
        AddNightLight(window3, null, HexColor("#ffd98a"), 1.4f);

        GameObject window4 = BuildWindow(parent, "Ventana_4",
            groundPosition + new Vector3(front, upperY, depth * 0.29f), frontSign, frameColor);
        AddNightLight(window4, null, HexColor("#ffcf72"), 1.2f);

        PrimitiveObject(parent, "Balcon_Piso", PrimitiveType.Cube,
            groundPosition + new Vector3(front + frontSign * 0.45f, floorHeight + 0.22f, 0f),
            new Vector3(1f, 0.14f, 2.2f), HexColor("#a49a8c"));
        PrimitiveObject(parent, "Balcon_Baranda", PrimitiveType.Cube,
            groundPosition + new Vector3(front + frontSign * 0.9f, floorHeight + 0.62f, 0f),
            new Vector3(0.1f, 0.7f, 2.2f), frameColor);
        for (int side = -1; side <= 1; side += 2)
        {
            PrimitiveObject(parent, $"Balcon_Lado_{side}", PrimitiveType.Cube,
                groundPosition + new Vector3(front + frontSign * 0.68f, floorHeight + 0.62f, side * 1.05f),
                new Vector3(0.9f, 0.7f, 0.1f), frameColor);
        }
    }

    // Ventana con marco y alféizar. Devuelve el vidrio, que es el que se
    // ilumina de noche (ver NightLight).
    private static GameObject BuildWindow(Transform parent, string name, Vector3 position, float frontSign, Color frameColor)
    {
        PrimitiveObject(parent, name + "_Marco", PrimitiveType.Cube,
            position - Vector3.right * frontSign * 0.02f, new Vector3(0.1f, 1.24f, 1.24f), frameColor);

        GameObject glass = PrimitiveObject(parent, name, PrimitiveType.Cube,
            position + Vector3.right * frontSign * 0.03f, new Vector3(0.1f, 1f, 1f), HexColor("#7fb4d8"));
        glass.isStatic = false;

        // Cruceta: dos hojas, como casi toda ventana de casa.
        PrimitiveObject(parent, name + "_Travesano", PrimitiveType.Cube,
            position + Vector3.right * frontSign * 0.05f, new Vector3(0.09f, 0.08f, 1.02f), frameColor);
        PrimitiveObject(parent, name + "_Alfeizar", PrimitiveType.Cube,
            position + new Vector3(frontSign * 0.12f, -0.66f, 0f), new Vector3(0.34f, 0.12f, 1.4f), HexColor("#a49a8c"));

        return glass;
    }

    // Árboles, arbustos, flores, pasto y rocas repartidos por todo el valle,
    // esquivando calles, veredas, río y casas. La semilla es fija para que la
    // escena se vea igual cada vez que se reconstruye.
    private static void BuildVegetation(Transform parent)
    {
        GameObject vegetation = new GameObject("Vegetacion");
        vegetation.transform.SetParent(parent);

        System.Random rng = new System.Random(20260908);

        // Los árboles ahora son los del asset "3D Tree" y van SOLO a lo largo
        // del río, no repartidos por el pueblo. Es lo que se pidió, y además
        // tiene sentido: en el valle del Mantaro la arboleda crece junto al
        // agua, y el pueblo es de calles secas con casas pegadas.
        BuildRiverTrees(vegetation.transform, rng);

        for (int i = 0; i < 80; i++)
        {
            Vector3 pos = RandomFreeSpot(rng, -110f, 110f, -125f, 115f, 1.8f);
            if (pos == Vector3.zero) continue;
            BuildBush(vegetation.transform, $"Arbusto_{i}", pos, rng);
        }

        for (int i = 0; i < 110; i++)
        {
            Vector3 pos = RandomFreeSpot(rng, -105f, 100f, -120f, 110f);
            if (pos == Vector3.zero) continue;
            BuildFlower(vegetation.transform, $"Flor_{i}", pos, rng);
        }

        for (int i = 0; i < 150; i++)
        {
            Vector3 pos = RandomFreeSpot(rng, -105f, 100f, -120f, 110f);
            if (pos == Vector3.zero) continue;
            BuildGrassTuft(vegetation.transform, pos, rng, HexColor("#69a344"), 1f);
        }

        for (int i = 0; i < 40; i++)
        {
            float size = NextFloat(rng, 0.6f, 2.4f);
            Vector3 pos = RandomFreeSpot(rng, -110f, 120f, -125f, 115f, size * 0.5f + 0.8f);
            if (pos == Vector3.zero) continue;

            MeshObject(vegetation.transform, $"Roca_{i}", LowPolyMeshFactory.Rock(i % 4),
                pos + Vector3.up * size * 0.22f,
                new Vector3(size, size * NextFloat(rng, 0.5f, 0.9f), size * NextFloat(rng, 0.8f, 1.2f)),
                rng.Next(2) == 0 ? HexColor("#9a9088") : HexColor("#8b8179"),
                Quaternion.Euler(NextFloat(rng, -10f, 10f), NextFloat(rng, 0f, 360f), NextFloat(rng, -10f, 10f)));
        }
    }

    // Verdes de árbol de ribera: del más claro (sauce joven) al más oscuro
    // (eucalipto), que son los que crecen a la orilla del Mantaro.
    // OJO: el color MULTIPLICA la textura de las hojas, no la reemplaza. Por
    // eso van todos cerca del blanco: con verdes fuertes el follaje saldría
    // casi negro.
    private static readonly Color[] TreeGreens =
    {
        new Color(0.92f, 1.00f, 0.80f), // verde claro, sauce joven
        new Color(0.78f, 0.94f, 0.70f),
        new Color(1.00f, 0.98f, 0.86f), // el de la textura, casi sin tocar
        new Color(0.68f, 0.86f, 0.66f), // eucalipto, más oscuro
        new Color(0.86f, 0.96f, 0.74f),
    };

    private static readonly string[] TreePrefabPaths =
    {
        "Assets/3D Tree/prefab/Tree_RoundNatural.prefab",
        "Assets/3D Tree/prefab/Tree_RoundNatural2.prefab",
        "Assets/3D Tree/prefab/Tree_RoundNatural3.prefab",
    };

    // Arboleda de las dos orillas. Van en dos hileras irregulares siguiendo el
    // cauce, del lado de AFUERA de los taludes: dentro del cauce estorbarían
    // para bajar por las muestras de agua.
    private static void BuildRiverTrees(Transform parent, System.Random rng)
    {
        List<GameObject> prefabs = new List<GameObject>();
        foreach (string path in TreePrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            // Uno de los tres prefabs del paquete da error al importarse en
            // Unity 6. Si viene null se usa lo que sí haya, en vez de dejar el
            // río pelado.
            if (prefab != null) prefabs.Add(prefab);
            else Debug.LogWarning($"[Kuntur] El árbol {path} no se pudo cargar. Sigo con los demás.");
        }

        if (prefabs.Count == 0)
        {
            Debug.LogWarning("[Kuntur] No hay árboles del asset. Uso los de siempre junto al río.");
            for (int i = 0; i < 40; i++)
            {
                Vector3 fallback = RandomFreeSpot(rng, -95f, 115f, -85f, 85f, 3.2f);
                if (fallback == Vector3.zero) continue;
                BuildRoundTree(parent, $"Arbol_{i}", fallback, rng);
            }
            return;
        }

        GameObject trees = new GameObject("Arboleda_Rio");
        trees.transform.SetParent(parent);

        int planted = 0;
        // Toda la orilla, de punta a punta del río (ahora curvo y largo).
        for (float z = RiverStartZ + 10f; z <= RiverEndZ - 10f; z += NextFloat(rng, 4.5f, 7.5f))
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (rng.Next(100) < 18) continue; // huecos, para que no sea una alameda

                // Entre 16.5 y 23 del eje del río: el cauce y sus taludes
                // llegan hasta 12 (río ancho de la v53), así que desde 16.5 ya
                // se está en tierra firme.
                //
                // Antes este filtro usaba IsOnStreetOrRiver, que da por
                // ocupada la franja del río MÁS un margen, y terminaba
                // descartando casi toda la orilla: de la arboleda entera solo
                // quedaban 8 árboles. Ahora se preguntan únicamente las tres
                // cosas que de verdad estorban.
                float offset = NextFloat(rng, RiverBankHalf + 4.5f, RiverBankHalf + 11f);
                float treeZ = z + NextFloat(rng, -2.2f, 2.2f);
                Vector3 position = new Vector3(RiverX(treeZ) + side * offset, 0f, treeZ);

                if (Mathf.Abs(position.z - AvenueZ) < 11f) continue;      // el puente y la avenida
                if (Mathf.Abs(position.x - EastRoadX) < 7f &&
                    position.z > EastRoadFromZ - 4f && position.z < EastRoadToZ + 4f) continue; // camino de tierra del barrio
                if (IsOnStreetOrRiver(position, 1.5f) && RiverDistance(position) > RiverBankHalf + 3f) continue; // calles del pueblo
                if (OverlapsPlaced(position, 3f)) continue;               // casas y todo lo ya puesto
                Occupy(position, 4f, 4f);

                GameObject prefab = prefabs[rng.Next(prefabs.Count)];
                GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, trees.transform);
                tree.name = $"Arbol_Rio_{planted++}";
                tree.transform.position = position;
                tree.transform.rotation = Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f);

                // Un modelo que no se puede medir es uno cuya malla no cargó
                // (pasa con uno de los prefabs del paquete): se descarta en
                // vez de dejar un árbol invisible o gigante.
                if (MeasureLocalBounds(tree) == null) { Object.DestroyImmediate(tree); planted--; continue; }

                ScaleToHeight(tree, NextFloat(rng, 6.5f, 11f), NextFloat(rng, 0.9f, 1.15f));

                // Verde distinto por árbol: el asset trae un solo material
                // para todos, y una arboleda con el mismo verde exacto se ve
                // pintada con rodillo. Son 5 tonos fijos, no uno por árbol,
                // para no llenar el proyecto de materiales.
                int shade = rng.Next(TreeGreens.Length);
                FixPipelineMaterials(tree, TreeGreens[shade], "verde" + shade);

                foreach (Collider collider in tree.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(collider); // que no estorben al caminar por la orilla
                }
            }
        }

        Debug.Log($"[Kuntur] Arboleda del río: {planted} árboles del asset 3D Tree.");
    }

    private static void BuildPineTree(Transform parent, string name, Vector3 position, System.Random rng)
    {
        GameObject tree = new GameObject(name);
        tree.transform.SetParent(parent);

        float scale = NextFloat(rng, 0.8f, 1.5f);
        Color green = rng.Next(3) switch
        {
            0 => HexColor("#2f6b3a"),
            1 => HexColor("#37793f"),
            _ => HexColor("#285f33"),
        };

        PrimitiveObject(tree.transform, "Tronco", PrimitiveType.Cylinder,
            position + Vector3.up * 0.9f * scale, new Vector3(0.28f * scale, 0.9f * scale, 0.28f * scale), HexColor("#6b4a30"));

        Mesh cone = LowPolyMeshFactory.Cone(8);
        // Tres conos apilados, cada uno más chico: la silueta clásica de pino
        // de las imágenes del GDD.
        MeshObject(tree.transform, "Copa_1", cone, position + Vector3.up * 1.2f * scale,
            new Vector3(1.9f * scale, 2.6f * scale, 1.9f * scale), green);
        MeshObject(tree.transform, "Copa_2", cone, position + Vector3.up * 2.5f * scale,
            new Vector3(1.5f * scale, 2.2f * scale, 1.5f * scale), green);
        MeshObject(tree.transform, "Copa_3", cone, position + Vector3.up * 3.6f * scale,
            new Vector3(1.05f * scale, 1.8f * scale, 1.05f * scale), green);
    }

    private static void BuildRoundTree(Transform parent, string name, Vector3 position, System.Random rng)
    {
        GameObject tree = new GameObject(name);
        tree.transform.SetParent(parent);

        float scale = NextFloat(rng, 0.85f, 1.5f);
        Color green = rng.Next(3) switch
        {
            0 => HexColor("#4f8f43"),
            1 => HexColor("#5ba04c"),
            _ => HexColor("#417a39"),
        };

        PrimitiveObject(tree.transform, "Tronco", PrimitiveType.Cylinder,
            position + Vector3.up * 1.3f * scale, new Vector3(0.34f * scale, 1.3f * scale, 0.34f * scale), HexColor("#6b4a30"));

        // Copa hecha de 2-3 esferas facetadas, que se lee mucho mejor que una
        // sola bola lisa.
        Mesh ico = LowPolyMeshFactory.Icosphere(1);
        MeshObject(tree.transform, "Copa_1", ico, position + Vector3.up * 3.2f * scale,
            Vector3.one * 3.2f * scale, green, Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f));
        MeshObject(tree.transform, "Copa_2", ico, position + new Vector3(0.7f * scale, 2.6f * scale, 0.3f * scale),
            Vector3.one * 2.3f * scale, green, Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f));
        MeshObject(tree.transform, "Copa_3", ico, position + new Vector3(-0.6f * scale, 2.8f * scale, -0.4f * scale),
            Vector3.one * 2.1f * scale, green, Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f));
    }

    private static void BuildBush(Transform parent, string name, Vector3 position, System.Random rng)
    {
        GameObject bush = new GameObject(name);
        bush.transform.SetParent(parent);

        Mesh ico = LowPolyMeshFactory.Icosphere(1);
        Color green = rng.Next(2) == 0 ? HexColor("#3f7a38") : HexColor("#4a8a3f");
        float scale = NextFloat(rng, 0.7f, 1.3f);

        for (int i = 0; i < 3; i++)
        {
            Vector3 offset = new Vector3(NextFloat(rng, -0.5f, 0.5f), 0f, NextFloat(rng, -0.5f, 0.5f)) * scale;
            float size = NextFloat(rng, 0.9f, 1.5f) * scale;
            MeshObject(bush.transform, $"Mata_{i}", ico, position + offset + Vector3.up * size * 0.38f,
                new Vector3(size, size * 0.75f, size), green, Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f));
        }
    }

    private static void BuildFlower(Transform parent, string name, Vector3 position, System.Random rng)
    {
        GameObject flower = new GameObject(name);
        flower.transform.SetParent(parent);

        Color petal = rng.Next(4) switch
        {
            0 => HexColor("#e8574c"),
            1 => HexColor("#f2c14e"),
            2 => HexColor("#f0f0f0"),
            _ => HexColor("#c46bd4"),
        };

        PrimitiveObject(flower.transform, "Tallo", PrimitiveType.Cylinder,
            position + Vector3.up * 0.2f, new Vector3(0.05f, 0.2f, 0.05f), HexColor("#4f8f3e"));
        MeshObject(flower.transform, "Flor", LowPolyMeshFactory.Icosphere(0),
            position + Vector3.up * 0.42f, Vector3.one * 0.28f, petal,
            Quaternion.Euler(0f, NextFloat(rng, 0f, 360f), 0f));
    }

    // Mechón de pasto: tres hojas finas inclinadas en distintas direcciones,
    // igual que los matorrales sueltos de las imágenes.
    private static void BuildGrassTuft(Transform parent, Vector3 position, System.Random rng, Color color, float scale)
    {
        GameObject tuft = new GameObject("Pasto");
        tuft.transform.SetParent(parent);

        for (int i = 0; i < 3; i++)
        {
            float height = NextFloat(rng, 0.35f, 0.75f) * scale;
            float tilt = NextFloat(rng, -25f, 25f);
            Vector3 offset = new Vector3(NextFloat(rng, -0.18f, 0.18f), 0f, NextFloat(rng, -0.18f, 0.18f));

            PrimitiveObject(tuft.transform, $"Hoja_{i}", PrimitiveType.Cube,
                position + offset + Vector3.up * height * 0.5f,
                new Vector3(0.07f, height, 0.07f), color,
                Quaternion.Euler(tilt, NextFloat(rng, 0f, 360f), tilt));
        }
    }

    // ---------------------------------------------------------------
    // Utilidades de mundo (posiciones libres, objetos con malla)
    // ---------------------------------------------------------------

    private static float NextFloat(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    // Devuelve Vector3.zero si no encontró un punto libre en unos cuantos
    // intentos (el llamador simplemente se salta ese elemento).
    private static Vector3 RandomFreeSpot(System.Random rng, float minX, float maxX, float minZ, float maxZ)
    {
        return RandomFreeSpot(rng, minX, maxX, minZ, maxZ, 1.2f);
    }

    // radius = cuánto ocupa de verdad lo que se va a plantar ahí. Un arbusto
    // de 1.5 y un árbol con copa de 3 no pueden usar el mismo criterio: con
    // uno solo, o quedan árboles con media copa sobre la pista, o no se planta
    // nada en media cuadra.
    private static Vector3 RandomFreeSpot(System.Random rng, float minX, float maxX, float minZ, float maxZ, float radius)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 candidate = new Vector3(NextFloat(rng, minX, maxX), 0f, NextFloat(rng, minZ, maxZ));
            if (IsOnStreetOrRiver(candidate, radius)) continue;
            if (IsOnBuilding(candidate)) continue;
            return candidate;
        }
        return Vector3.zero;
    }

    private static bool IsOnStreetOrRiver(Vector3 position)
    {
        return IsOnStreetOrRiver(position, 0f);
    }

    // margin = cuánto se agranda cada corredor. Sirve para lo que NO es un
    // punto: una mancha de pasto de 16 de ancho se tiene que descartar por su
    // radio, no por su centro, o termina pintada encima del asfalto.
    private static bool IsOnStreetOrRiver(Vector3 position, float margin)
    {
        return IsOnStreetOrRiver(position, margin, margin);
    }

    // Versión rectangular: marginX y marginZ por separado. Una casa de frente
    // angosto y fondo largo NO es un cuadrado, y tratarla como tal (usando el
    // lado más largo para los dos ejes) dejaba media cuadra sin construir:
    // la casa "no entraba" por un fondo que en realidad apunta hacia adentro
    // del terreno, no hacia la calle.
    private static bool IsOnStreetOrRiver(Vector3 position, float marginX, float marginZ)
    {
        // Medidas reales: la pista mide 8 de ancho (±4) y la vereda llega
        // hasta 6.8, así que 7 es justo el filo de la vereda. Poner un número
        // más grande "por si acaso" dejaba media cuadra inutilizable y las
        // casas no se llegaban a construir.
        if (Mathf.Abs(position.x - MainStreetX) < 7f + marginX &&
            position.z < TownNorthZ + 2.8f + marginZ && position.z > TopRoadZ - CrossStreetHalf - marginZ) return true;   // Calle Real + veredas (v56: termina en la circunvalación norte)
        if (Mathf.Abs(position.z - AvenueZ) < 7f + marginZ &&
            position.x > CrossStreetFromX - 3f - marginX && position.x < GiraldezEastEndX + marginX) return true; // Av. Giráldez + veredas
        if (RiverDistance(position) < RiverBankHalf + 1f + marginX && Mathf.Abs(position.z) < RiverEndZ + 10f) return true;  // cauce del río (curvo) y sus taludes

        // Jirones transversales: 6.5 de pista + vereda a los dos lados.
        foreach (float z in CrossStreetsZ)
        {
            if (Mathf.Abs(position.z - z) < CrossStreetHalf + marginZ &&
                position.x > CrossStreetFromX - marginX && position.x < CrossStreetToX + marginX) return true;
        }

        // Jr. Puno y Jr. Cusco, las dos calles paralelas a la Calle Real.
        // v56: Puno y Cusco siguen cuesta arriba hasta el mirador.
        foreach (float streetX in new[] { PunoX, CuscoX, ArequipaX })
        {
            float southZ = streetX == ArequipaX ? SideStreetSouthZ - 2f : TopRoadZ - CrossStreetHalf;
            if (Mathf.Abs(position.x - streetX) < CrossStreetHalf + marginX &&
                position.z > southZ - marginZ && position.z < SideStreetNorthZ + 2f + marginZ) return true;
        }

        // v56: la calle de arriba del mirador y el puente peatonal.
        if (Mathf.Abs(position.z - TopRoadZ) < CrossStreetHalf + marginZ &&
            position.x > PunoX - CrossStreetHalf - marginX && position.x < CuscoX + CrossStreetHalf + marginX) return true;
        if (Mathf.Abs(position.z - FootbridgeZ) < 1.9f + marginZ &&
            position.x > CrossStreetToX - marginX && position.x < EastRoadX + marginX) return true;

        // Camino de tierra del barrio del otro lado del río.
        if (Mathf.Abs(position.x - EastRoadX) < 5f + marginX && position.z > EastRoadFromZ && position.z < EastRoadToZ) return true;

        return false;
    }

    // Registro de todo lo que ya ocupa un pedazo de suelo (casas, bodega,
    // botadero). Antes las posiciones se calculaban a mano y terminaban
    // casas dentro de jirones, letreros metidos en una pared y árboles
    // creciendo dentro de las casas. Ahora cada cosa pregunta primero si el
    // sitio está libre, y si no, simplemente no se coloca ahí.
    private static readonly List<Bounds> PlacedVolumes = new List<Bounds>();

    private static void Occupy(Vector3 center, float sizeX, float sizeZ)
    {
        PlacedVolumes.Add(new Bounds(new Vector3(center.x, 3f, center.z), new Vector3(sizeX, 10f, sizeZ)));
    }

    private static bool IsAreaFree(Vector3 center, float radius)
    {
        return IsAreaFree(center, radius, radius);
    }

    // Comprobación exacta: se agranda cada corredor de calle con el medio
    // ancho de la caja, en cada eje por separado. Es equivalente a preguntar
    // "¿se cruzan estos dos rectángulos?", y no se le escapan los casos que
    // sí se le escapaban al muestreo por esquinas.
    private static bool IsAreaFree(Vector3 center, float halfX, float halfZ)
    {
        if (IsOnStreetOrRiver(center, halfX, halfZ)) return false;
        return !OverlapsPlaced(center, halfX, halfZ);
    }

    private static bool OverlapsPlaced(Vector3 center, float radius)
    {
        return OverlapsPlaced(center, radius, radius);
    }

    private static bool OverlapsPlaced(Vector3 center, float halfX, float halfZ)
    {
        Bounds candidate = new Bounds(new Vector3(center.x, 3f, center.z), new Vector3(halfX * 2f, 10f, halfZ * 2f));
        foreach (Bounds placed in PlacedVolumes)
        {
            if (placed.Intersects(candidate)) return true;
        }

        return false;
    }

    // Solo el ASFALTO, sin contar las veredas. Un letrero, un poste o una
    // señal sí pueden ir en la vereda -es su sitio-, pero nunca en medio de
    // la pista; para eso no sirve IsOnStreetOrRiver, que da por ocupada la
    // calle entera.
    private static bool IsOnRoadway(Vector3 position)
    {
        if (Mathf.Abs(position.x - MainStreetX) < MainStreetHalf + 0.6f && position.z < TownNorthZ + 1f && position.z > TopRoadZ - 4f) return true;
        if (Mathf.Abs(position.z - AvenueZ) < AvenueHalf + 0.6f && position.x > CrossStreetFromX - 1f && position.x < GiraldezEastEndX) return true;
        if (RiverDistance(position) < RiverBankHalf + 1f && Mathf.Abs(position.z) < RiverEndZ + 10f) return true;
        if (Mathf.Abs(position.z - TopRoadZ) < CrossStreetHalfRoad + 0.6f && position.x > PunoX - 4f && position.x < CuscoX + 4f) return true;

        foreach (float z in CrossStreetsZ)
        {
            if (Mathf.Abs(position.z - z) < CrossStreetHalfRoad + 0.6f &&
                position.x > CrossStreetFromX && position.x < CrossStreetToX) return true;
        }

        foreach (float streetX in new[] { PunoX, CuscoX, ArequipaX })
        {
            float southZ = streetX == ArequipaX ? SideStreetSouthZ - 2f : TopRoadZ - CrossStreetHalfRoad;
            if (Mathf.Abs(position.x - streetX) < CrossStreetHalfRoad + 0.6f &&
                position.z > southZ && position.z < SideStreetNorthZ + 2f) return true;
        }
        if (Mathf.Abs(position.x - EastRoadX) < 4f && position.z > EastRoadFromZ && position.z < EastRoadToZ) return true;

        return false;
    }

    private static bool IsOnBuilding(Vector3 position)
    {
        // Cualquier cosa ya colocada (con un poco de margen para no pegar un
        // árbol contra la pared).
        if (!IsAreaFree(position, 1.8f)) return true;

        return IsOnReservedSpot(position);
    }

    private static bool IsOnReservedSpot(Vector3 position)
    {
        // Las casas, la bodega y el botadero ya están registrados como
        // volúmenes ocupados; acá solo quedan los sitios que hay que dejar
        // despejados aunque no tengan nada construido encima.
        if (Vector3.Distance(position, new Vector3(0f, 0f, -10f)) < 6f) return true; // aparición del jugador
        if (Vector3.Distance(position, YamileSpot) < 4f) return true;
        if (Vector3.Distance(position, RosaSpot) < 4f) return true;
        return false;
    }

    private static GameObject MeshObject(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Color color, Quaternion? rotation = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        if (rotation.HasValue) go.transform.rotation = rotation.Value;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial(color);
        go.isStatic = true; // decorado: permite batching estático
        return go;
    }

    private static GameObject PrimitiveObject(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Quaternion? rotation = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        if (rotation.HasValue) go.transform.rotation = rotation.Value;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider); // decorado: no estorba al caminar

        SetColor(go, color);
        go.isStatic = true;
        return go;
    }

    private static Camera BuildPlayerAndCamera(InputActionAsset controls)
    {
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1f, -10f);

        CharacterController cc = player.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.height = 1.8f;
        cc.radius = 0.35f;

        KunturVisualParts kunturVisual = BuildKunturVisual(player.transform);

        GameObject pivotGO = new GameObject("CameraPivot");
        pivotGO.transform.SetParent(player.transform, false);
        pivotGO.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        Camera cam = Camera.main;
        GameObject camGO = cam != null ? cam.gameObject : new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGO.tag = "MainCamera";
        cam = camGO.GetComponent<Camera>();
        // El agua del río necesita la profundidad de la escena para teñirse
        // más oscura donde es honda y hacer espuma en la orilla.
        if (camGO.GetComponent<CameraDepthTexture>() == null) camGO.AddComponent<CameraDepthTexture>();
        // Tomas de diálogo (los dos de perfil) y de misión fallida.
        if (camGO.GetComponent<KunturCinematicCamera>() == null) camGO.AddComponent<KunturCinematicCamera>();
        camGO.transform.SetParent(pivotGO.transform, false);
        // Un poco más atrás y más alta que antes: a 4.5 de distancia el
        // personaje ocupaba media pantalla y tapaba la pista de adelante.
        // Detrás y apenas por encima de Kuntur (ver SimpleThirdPersonController,
        // que aplica este mismo encuadre al arrancar).
        camGO.transform.localPosition = new Vector3(0f, 0.75f, -3.7f);
        camGO.transform.localRotation = Quaternion.identity;

        // Skybox y no color plano: el cielo lo pinta SkyController con el
        // cielo procedural de Unity, que calcula el naranja del atardecer a
        // partir de hacia dónde apunta el sol.
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.farClipPlane = 900f; // la cordillera del fondo llega hasta los 700

        SimpleThirdPersonController controller = player.AddComponent<SimpleThirdPersonController>();
        // v56: subir y bajar de los vehículos, manejar y pedir el tuk tuk.
        player.AddComponent<VehicleSystem>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("cameraPivot").objectReferenceValue = pivotGO.transform;
        so.FindProperty("moveAction").objectReferenceValue = FindActionRef(controls, "Move");
        so.FindProperty("lookAction").objectReferenceValue = FindActionRef(controls, "Look");
        so.FindProperty("sprintAction").objectReferenceValue = FindActionRef(controls, "Sprint");
        so.FindProperty("jumpAction").objectReferenceValue = FindActionRef(controls, "Jump");
        so.ApplyModifiedProperties();

        // Kuntur no tiene huesos/animación real (es un modelo estático de IA
        // imagen-a-3D), así que simulamos el caminar/correr con un rebote
        // procedural en vez de dejarlo clavado mientras se mueve. Cuando el
        // cóndor está armado con primitivas (patas/alas/cabeza como objetos
        // separados) además animamos cada parte por separado: patas que
        // alternan al caminar, alas que aletean y cabeza con vaivén. Si en
        // cambio se está usando el modelo 3D importado (una sola malla sin
        // huesos), esas referencias quedan null y el animator simplemente no
        // anima esas partes - solo el rebote general del cuerpo completo.
        // Con el modelo rigueado esto se salta: la caminata ya la hace el
        // esqueleto, y sumarle encima el rebote procedural haría que las dos
        // cosas muevan el mismo transform y el cóndor tiemble.
        if (!kunturVisual.rigged)
        {
            KunturVisualAnimator visualAnimator = player.AddComponent<KunturVisualAnimator>();
            SerializedObject animSo = new SerializedObject(visualAnimator);
            animSo.FindProperty("visualRoot").objectReferenceValue = kunturVisual.root;
            animSo.FindProperty("headTransform").objectReferenceValue = kunturVisual.head;
            animSo.FindProperty("leftLegPivot").objectReferenceValue = kunturVisual.leftLegPivot;
            animSo.FindProperty("rightLegPivot").objectReferenceValue = kunturVisual.rightLegPivot;
            animSo.FindProperty("leftWingPivot").objectReferenceValue = kunturVisual.leftWingPivot;
            animSo.FindProperty("rightWingPivot").objectReferenceValue = kunturVisual.rightWingPivot;
            animSo.ApplyModifiedProperties();
        }

        InteractionSystem interaction = camGO.AddComponent<InteractionSystem>();
        SerializedObject isSo = new SerializedObject(interaction);
        // Radio generoso: la basura es chica y está a ras de piso, así que con
        // 2.5 había que pararse casi encima. Con 3.4 basta con acercarse.
        isSo.FindProperty("interactionRange").floatValue = 3.4f;
        int mask = LayerMask.GetMask(InteractableLayerName);
        if (mask == 0) mask = 1 << 8;
        isSo.FindProperty("interactableLayer").intValue = mask;
        // Detección por cercanía al jugador (no a la cámara): así el prompt
        // "E"/"F" aparece con solo acercarse a la basura o la muestra de
        // agua, sin necesitar apuntar la cámara exactamente al objeto.
        isSo.FindProperty("detectionOrigin").objectReferenceValue = player.transform;
        isSo.FindProperty("interactPrimary").objectReferenceValue = FindActionRef(controls, "InteractPrimary");
        isSo.FindProperty("interactSecondary").objectReferenceValue = FindActionRef(controls, "InteractSecondary");
        isSo.ApplyModifiedProperties();

        return cam;
    }

    // Kuntur estilizado por primitivas: cuerpo oscuro, poncho rojo, chullo con
    // pompón y patas anaranjadas. No es un modelo 3D real, pero se lee mucho
    // mejor que una cápsula lisa y no depende de ningún asset externo.
    private static readonly string[] KunturModelSearchPaths =
    {
        // El .glb original fallaba al importar (malla de ~2M triángulos con
        // compresión meshopt que el importador de glTFast no lograba
        // decodificar). Lo reemplazamos por un .obj simplificado (~65k
        // triángulos, con textura) que Unity importa de forma nativa sin
        // depender de ningún paquete extra.
        "Assets/_Project/Art/Kuntur/Kuntur_Modelo.obj",
        "Assets/_Project/Art/Kuntur/tripo_kuntur.glb",
    };

    // Si ya importaste el modelo 3D generado (Tripo/Meshy) y está instalado el
    // paquete glTFast, lo usamos en vez de las primitivas. Si no existe o el
    // paquete todavía no está instalado, AssetDatabase simplemente devuelve
    // null y seguimos con el cóndor armado a mano más abajo.
    // Orientación del modelo de Kuntur. Se guarda en EditorPrefs para que
    // sobreviva a las reconstrucciones de la escena.
    // v5: hubo que invalidar el ángulo guardado al entrar el modelo rigueado.
    // El .obj viejo necesitaba 90° de giro; el FBX con esqueleto no necesita
    // ninguno, y con la clave anterior se le aplicaba igual el 90 del otro.
    // v8: el modelo rigueado quedaba mirando a la CÁMARA (de espaldas al
    // camino). Hay que invalidar el ángulo guardado o se sigue usando el 0°
    // de antes y el arreglo no se nota.
    private const string KunturYawKey = "Kuntur_ModeloYaw_v8";     // hacia dónde mira
    private const string KunturPitchKey = "Kuntur_ModeloPitch_v8"; // de pie o acostado

    private static Quaternion GetKunturModelRotation(GameObject instance)
    {
        float pitch, yaw;

        if (EditorPrefs.HasKey(KunturPitchKey) && EditorPrefs.HasKey(KunturYawKey))
        {
            pitch = EditorPrefs.GetFloat(KunturPitchKey);
            yaw = EditorPrefs.GetFloat(KunturYawKey);
        }
        else
        {
            (pitch, yaw) = GuessKunturRotation(instance);
            EditorPrefs.SetFloat(KunturPitchKey, pitch);
            EditorPrefs.SetFloat(KunturYawKey, yaw);
        }

        // Primero se endereza (X) y luego se gira sobre sí mismo (Y).
        return Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, 0f);
    }

    // Calcula cómo hay que girar el modelo leyendo su GEOMETRÍA, no a ojo.
    // Dos medidas bastan:
    //  1) el eje más largo es el vertical (un ave de pie es más alta que ancha);
    //     si ese eje no es Y, el modelo vino acostado y se para con un giro en X.
    //  2) la cabeza (el tercio superior) está corrida hacia el lado al que
    //     MIRA el ave: comparando su centro con el centro del cuerpo se saca
    //     la dirección de la cara, y de ahí el giro en Y que la lleva al +Z
    //     de Unity, que es lo que el juego considera "adelante".
    private static (float pitch, float yaw) GuessKunturRotation(GameObject instance)
    {
        // OJO CON ESTO, que ya me costó caro dos veces.
        //
        // La regla de abajo ("si el eje Z es el más largo, el modelo vino
        // acostado") vale para un personaje HUMANO, que es más alto que
        // profundo. Un ave NO: un cóndor posado mide como un metro del pico a
        // la cola y apenas sesenta centímetros de alto, así que su eje más
        // largo es el Z estando perfectamente de pie. Aplicarle el giro de 90°
        // por esa regla es justo lo que lo deja echado sobre la pista.
        //
        // El modelo rigueado sale de Blender ya DERECHO (pitch 0), pero mirando
        // al revés: su cara apunta al -Z, y la cámara de tercera persona está
        // justo ahí atrás. Resultado: se le veía la cara todo el tiempo y, como
        // el jugador se guía por hacia dónde mira el personaje, parecía que W
        // lo hacía caminar de espaldas y S hacia adelante. El movimiento nunca
        // estuvo invertido; lo que estaba al revés era el modelo.
        //
        // 180° en Y lo deja mirando al +Z, que es transform.forward del
        // jugador y lo que el controlador considera "adelante".
        //
        // Va fijo y no medido: deducirlo de la geometría (buscar hacia qué lado
        // se corre la cabeza) ya dio al revés una vez, porque en un cóndor lo
        // que más sobresale no es el pico sino la cola y la espalda.
        if (instance.GetComponentInChildren<SkinnedMeshRenderer>() != null) return (0f, 180f);

        List<Vector3> points = new List<Vector3>();

        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            points.AddRange(filter.sharedMesh.vertices);
        }

        if (points.Count < 32) return (0f, 0f);

        Vector3 min = points[0];
        Vector3 max = points[0];
        foreach (Vector3 p in points)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        Vector3 size = max - min;

        // ¿Está acostado? Solo si el eje vertical NO es el más largo.
        float pitch = 0f;
        if (size.z > size.y && size.z >= size.x) pitch = -90f;
        else if (size.x > size.y && size.x > size.z) pitch = 0f; // caso raro: se deja como está

        if (pitch != 0f) return (pitch, 0f); // primero enderezar; el giro se ajusta a mano

        // Giro horizontal: +90°, comprobado en pantalla.
        //
        // Este modelo mira hacia -X, o sea que sin girar camina de perfil
        // hacia la izquierda. Intenté deducirlo midiendo hacia qué lado se
        // corre la cabeza y dio al revés: lo que sobresale del otro lado no es
        // el pico, es la cola y la espalda del cóndor. Así que va fijo y
        // verificado con el juego corriendo: +90° lo deja mirando al +Z de
        // Unity, que es "adelante" para el juego.
        return (0f, 90f);
    }

    private static GameObject FindKunturModel()
    {
        GameObject model = GameObject.Find("Player/Visual/Modelo_Kuntur");
        if (model == null)
        {
            EditorUtility.DisplayDialog("Kuntur",
                "No encontré el modelo en la escena. Abre Exploracion.unity y vuelve a intentarlo.", "Listo");
        }
        return model;
    }

    private static void ApplyKunturRotation(GameObject model, float pitch, float yaw)
    {
        EditorPrefs.SetFloat(KunturPitchKey, pitch);
        EditorPrefs.SetFloat(KunturYawKey, yaw);

        Undo.RecordObject(model.transform, "Orientar Kuntur");
        model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, 0f);
        EditorSceneManager.MarkSceneDirty(model.scene);

        Debug.Log($"[Kuntur] Orientación: vertical {pitch}°, giro {yaw}°.");
    }

    // Lo pone de pie / lo acuesta, de 90 en 90. Para cuando el modelo viene
    // tumbado sobre la pista.
    [MenuItem("Kuntur/Enderezar a Kuntur (vertical 90°)")]
    private static void PitchKunturModel()
    {
        GameObject model = FindKunturModel();
        if (model == null) return;

        float pitch = Mathf.Repeat(EditorPrefs.GetFloat(KunturPitchKey, 0f) + 90f, 360f);
        if (pitch > 180f) pitch -= 360f; // se mantiene entre -180 y 180
        ApplyKunturRotation(model, pitch, EditorPrefs.GetFloat(KunturYawKey, 0f));
    }

    // Lo hace mirar a otro lado, de 90 en 90. Para cuando camina "de esquina".
    [MenuItem("Kuntur/Girar a Kuntur (horizontal 90°)")]
    private static void RotateKunturModel()
    {
        GameObject model = FindKunturModel();
        if (model == null) return;

        float yaw = Mathf.Repeat(EditorPrefs.GetFloat(KunturYawKey, 0f) + 90f, 360f);
        ApplyKunturRotation(model, EditorPrefs.GetFloat(KunturPitchKey, 0f), yaw);
    }

    // Enciende y apaga la animación de patas sobre la escena abierta, sin
    // reconstruir nada. Para cuando el modelo se descuadra y hay que ver si la
    // culpa es de los huesos o de otra cosa.
    [MenuItem("Kuntur/Animación de patas: encender o apagar")]
    private static void ToggleBoneAnimation()
    {
        KunturBoneAnimator animator = Object.FindFirstObjectByType<KunturBoneAnimator>();
        if (animator == null)
        {
            EditorUtility.DisplayDialog("Kuntur",
                "No encontré el animador de huesos en la escena. Abre Exploracion.unity y vuelve a intentarlo.", "Listo");
            return;
        }

        SerializedObject so = new SerializedObject(animator);
        SerializedProperty flag = so.FindProperty("animateBones");
        flag.boolValue = !flag.boolValue;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);
        Debug.Log($"[Kuntur] Animación de patas: {(flag.boolValue ? "ENCENDIDA" : "APAGADA")}.");
    }

    // Vuelve al ángulo que el generador deduce solo, por si se perdió el ajuste.
    [MenuItem("Kuntur/Reiniciar orientación de Kuntur")]
    private static void ResetKunturRotation()
    {
        GameObject model = FindKunturModel();
        if (model == null) return;

        EditorPrefs.DeleteKey(KunturPitchKey);
        EditorPrefs.DeleteKey(KunturYawKey);

        (float pitch, float yaw) = GuessKunturRotation(model);
        ApplyKunturRotation(model, pitch, yaw);
    }

    private static GameObject TryLoadKunturModel()
    {
        // El rigueado tiene prioridad: es el único que camina de verdad.
        GameObject rigged = LoadRiggedKuntur();
        if (rigged != null) return rigged;

        foreach (string path in KunturModelSearchPaths)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model != null) return model;
        }
        return null;
    }

    // Busca un personaje descargado en Assets/_Project/Art/Personaje y, si lo
    // encuentra, lo usa como jugador.
    //
    // Sirve con cualquier cosa: un prefab del Asset Store que ya traiga su
    // Animator montado, o un FBX de Mixamo con sus clips. Si trae controlador
    // propio se respeta tal cual (sus animaciones ya están probadas por quien
    // las hizo, que es más de lo que yo puedo decir de las mías); si solo trae
    // clips, se le arma un controlador con un blend tree de quieto-caminar-
    // correr, que es lo que no necesita ajustar transiciones a mano.
    private static KunturVisualParts? TryBuildDownloadedCharacter(Transform visual, Transform player)
    {
        if (!AssetDatabase.IsValidFolder(CharacterDropFolder)) return null;

        GameObject source = FindCharacterAsset();
        if (source == null) return null;

        Debug.Log($"[Kuntur] Usando el personaje descargado: {source.name}");

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, visual);
        if (instance == null) instance = Object.Instantiate(source, visual, false);
        instance.name = "Personaje";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        foreach (Collider col in instance.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);
        foreach (Camera stray in instance.GetComponentsInChildren<Camera>(true))
            Object.DestroyImmediate(stray.gameObject);

        NormalizeKunturModel(instance, visual);

        Animator animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.applyRootMotion = false; // quien mueve al personaje es el CharacterController

        if (animator.runtimeAnimatorController == null)
            animator.runtimeAnimatorController = BuildCharacterController(source);

        CharacterAnimatorDriver driver = instance.GetComponent<CharacterAnimatorDriver>();
        if (driver == null) driver = instance.AddComponent<CharacterAnimatorDriver>();

        SerializedObject so = new SerializedObject(driver);
        so.FindProperty("controller").objectReferenceValue = player.GetComponent<CharacterController>();
        so.FindProperty("playerController").objectReferenceValue = player.GetComponent<SimpleThirdPersonController>();
        so.ApplyModifiedProperties();

        return new KunturVisualParts { root = visual, rigged = true };
    }

    private static GameObject FindCharacterAsset()
    {
        // Primero prefabs (suelen venir ya montados), después modelos sueltos.
        foreach (string filter in new[] { "t:Prefab", "t:Model" })
        {
            foreach (string guid in AssetDatabase.FindAssets(filter, new[] { CharacterDropFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (candidate == null) continue;

                // Tiene que ser un personaje: algo con malla deformable.
                if (candidate.GetComponentInChildren<SkinnedMeshRenderer>() == null) continue;
                return candidate;
            }
        }
        return null;
    }

    // Arma un controlador con blend tree para un personaje que trae clips
    // sueltos. El blend tree evita tener que ajustar transiciones a mano: la
    // mezcla entre quieto, caminar y correr sale suave sola según la velocidad.
    private static RuntimeAnimatorController BuildCharacterController(GameObject source)
    {
        string modelPath = AssetDatabase.GetAssetPath(source);

        AnimationClip idle = null, walk = null, run = null, jump = null, crouch = null;

        // Se buscan clips en TODA la carpeta, no solo en este modelo: en
        // Mixamo cada animación se baja en su propio archivo.
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { CharacterDropFolder }))
            ClassifyClip(AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid)),
                ref idle, ref walk, ref run, ref jump, ref crouch);

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            ClassifyClip(asset as AnimationClip, ref idle, ref walk, ref run, ref jump, ref crouch);

        if (idle == null && walk == null && run == null)
        {
            Debug.LogWarning("[Kuntur] El personaje no trae clips de animación reconocibles. " +
                             "Se va a mover por el mundo, pero sin animación. Los clips deben llamarse " +
                             "con algo como idle/walk/run/jump en el nombre.");
            return null;
        }

        string dir = ArtDir + "/Generated";
        CreateFolderRecursive(dir);
        string path = dir + "/Personaje_Animator.controller";

        AssetDatabase.DeleteAsset(path);
        UnityEditor.Animations.AnimatorController controller =
            UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);

        UnityEditor.Animations.AnimatorStateMachine machine = controller.layers[0].stateMachine;

        UnityEditor.Animations.BlendTree tree;
        UnityEditor.Animations.AnimatorState locomotion =
            controller.CreateBlendTreeInController("Locomocion", out tree);
        tree.blendParameter = "Speed";
        tree.blendType = UnityEditor.Animations.BlendTreeType.Simple1D;

        // Los umbrales van en METROS POR SEGUNDO, las mismas unidades que
        // reporta el CharacterController, para que la mezcla case con lo que
        // se ve en pantalla.
        if (idle != null) tree.AddChild(idle, 0f);
        if (walk != null) tree.AddChild(walk, 2.8f);
        if (run != null) tree.AddChild(run, 5f);

        machine.defaultState = locomotion;

        AddSimpleState(controller, machine, locomotion, jump, "Salto", "Grounded", false);
        AddSimpleState(controller, machine, locomotion, crouch, "Agachado", "Crouch", true);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Kuntur] Controlador armado -> quieto: {NameOf(idle)}, caminar: {NameOf(walk)}, " +
                  $"correr: {NameOf(run)}, salto: {NameOf(jump)}, agachado: {NameOf(crouch)}.");
        return controller;
    }

    private static string NameOf(AnimationClip clip) => clip != null ? clip.name : "ninguno";

    private static void ClassifyClip(AnimationClip clip, ref AnimationClip idle, ref AnimationClip walk,
        ref AnimationClip run, ref AnimationClip jump, ref AnimationClip crouch)
    {
        if (clip == null) return;
        if (clip.name.StartsWith("__preview__")) return;
        if (clip.empty || clip.length < 0.05f) return;

        string name = clip.name.ToLowerInvariant();

        // El orden importa: "crouch walk" es agacharse, no caminar.
        if (crouch == null && (name.Contains("crouch") || name.Contains("agach"))) { crouch = clip; return; }
        if (jump == null && (name.Contains("jump") || name.Contains("salt"))) { jump = clip; return; }
        if (run == null && (name.Contains("run") || name.Contains("sprint") || name.Contains("corr"))) { run = clip; return; }
        if (walk == null && (name.Contains("walk") || name.Contains("camin"))) { walk = clip; return; }
        if (idle == null && (name.Contains("idle") || name.Contains("quiet") || name.Contains("stand"))) idle = clip;
    }

    private static void AddSimpleState(UnityEditor.Animations.AnimatorController controller,
        UnityEditor.Animations.AnimatorStateMachine machine, UnityEditor.Animations.AnimatorState from,
        AnimationClip clip, string stateName, string parameter, bool enterValue)
    {
        if (clip == null) return;

        UnityEditor.Animations.AnimatorState state = machine.AddState(stateName);
        state.motion = clip;

        UnityEditor.Animations.AnimatorStateTransition enter = from.AddTransition(state);
        enter.hasExitTime = false;
        enter.duration = 0.1f;
        enter.AddCondition(enterValue ? UnityEditor.Animations.AnimatorConditionMode.If
            : UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, parameter);

        UnityEditor.Animations.AnimatorStateTransition exit = state.AddTransition(from);
        exit.hasExitTime = false;
        exit.duration = 0.15f;
        exit.AddCondition(enterValue ? UnityEditor.Animations.AnimatorConditionMode.IfNot
            : UnityEditor.Animations.AnimatorConditionMode.If, 0f, parameter);
    }

    // Carga el modelo con esqueleto, y si Unity lo tiene importado con los
    // ajustes equivocados, lo obliga a reimportarlo antes de usarlo.
    //
    // Esto hace falta porque Unity conserva para siempre los ajustes con los
    // que importó un modelo la primera vez: cambiar el postprocesador no basta.
    // El síntoma es de los que vuelven loco a cualquiera, porque el código
    // nuevo está bien y aun así no pasa nada: sin "Generic" Unity descarta el
    // esqueleto, el cóndor entra por el camino de malla estática y termina
    // blanco, gigante y sin animación.
    private static GameObject LoadRiggedKuntur()
    {
        string path = KunturModelPostprocessor.RiggedModelPath;

        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer != null && importer.animationType != ModelImporterAnimationType.Generic)
        {
            Debug.Log("[Kuntur] El modelo estaba importado sin esqueleto. Reimportando...");

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    // Normaliza escala y pivote de un modelo importado: ~1.6 m de alto y con
    // los pies en el suelo, sin importar en qué unidades lo haya exportado la
    // herramienta que lo generó (este FBX, por ejemplo, viene con la raíz a
    // 37 unidades de altura).
    private static void NormalizeKunturModel(GameObject instance, Transform visual)
    {
        instance.transform.localScale = Vector3.one;
        instance.transform.localPosition = Vector3.zero;

        Bounds? measured = MeasureModelInWorld(instance);
        if (measured == null) return;

        const float targetHeight = 1.6f;
        float currentHeight = Mathf.Max(measured.Value.size.y, 0.001f);
        instance.transform.localScale = Vector3.one * (targetHeight / currentHeight);

        Bounds? scaled = MeasureModelInWorld(instance);
        if (scaled == null) return;

        // La altura se resta respecto del suelo REAL del jugador (la de
        // "visual"), no de un 0 absoluto: si no, en cualquier punto que no
        // esté exactamente a Y=0 el modelo queda enterrado o flotando.
        float groundWorldY = visual.position.y;
        instance.transform.localPosition = new Vector3(0f, groundWorldY - scaled.Value.min.y, 0f);
    }

    // Mide el modelo con los VÉRTICES de verdad, en espacio de mundo.
    //
    // No se usa Renderer.bounds a propósito: en un SkinnedMeshRenderer esos
    // límites los precalcula Unity al importar y en el editor suelen venir
    // muy inflados. Midiendo con eso, el cóndor salía tres o cuatro veces más
    // grande de lo que debía y tapaba media pantalla.
    private static Bounds? MeasureModelInWorld(GameObject instance)
    {
        Bounds bounds = new Bounds();
        bool started = false;

        foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (skin.sharedMesh == null) continue;
            AccumulateVertices(skin.sharedMesh, skin.transform, ref bounds, ref started);
        }

        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            AccumulateVertices(filter.sharedMesh, filter.transform, ref bounds, ref started);
        }

        return started ? bounds : (Bounds?)null;
    }

    private static void AccumulateVertices(Mesh mesh, Transform owner, ref Bounds bounds, ref bool started)
    {
        Vector3[] vertices = mesh.vertices;
        // Con 126 mil vértices no hace falta mirarlos todos para sacar la caja:
        // uno de cada diez da el mismo resultado y es diez veces más rápido.
        int step = Mathf.Max(1, vertices.Length / 12000);

        for (int i = 0; i < vertices.Length; i += step)
        {
            Vector3 world = owner.TransformPoint(vertices[i]);
            if (!started)
            {
                bounds = new Bounds(world, Vector3.zero);
                started = true;
            }
            else
            {
                bounds.Encapsulate(world);
            }
        }
    }

    // Deja listo el cóndor con esqueleto: le quita lo que sobra del FBX, lo
    // orienta, lo normaliza y le engancha el Animator con el clip de caminata.
    private static void SetupKunturRig(GameObject instance, Transform visual, Transform player)
    {
        foreach (Collider col in instance.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);

        // Por si el FBX se importó ANTES de que existiera el postprocesador y
        // quedaron adentro la cámara y la luz que trae de Blender.
        foreach (Camera stray in instance.GetComponentsInChildren<Camera>(true))
            Object.DestroyImmediate(stray.gameObject);
        foreach (Light stray in instance.GetComponentsInChildren<Light>(true))
            Object.DestroyImmediate(stray.gameObject);

        instance.transform.localRotation = GetKunturModelRotation(instance);
        NormalizeKunturModel(instance, visual);
        ApplyKunturMaterial(instance);

        AnimationClip clip = FindKunturWalkClip();
        if (clip != null)
        {
            // El FBX trae animación propia: se usa esa.
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = BuildKunturAnimatorController();
            animator.applyRootMotion = false;

            KunturRigAnimator driver = instance.GetComponent<KunturRigAnimator>();
            if (driver == null) driver = instance.AddComponent<KunturRigAnimator>();

            SerializedObject driverSo = new SerializedObject(driver);
            driverSo.FindProperty("controller").objectReferenceValue = player.GetComponent<CharacterController>();
            driverSo.FindProperty("animatedRoot").objectReferenceValue = FindChildByName(instance.transform, "Armature");
            driverSo.ApplyModifiedProperties();
            return;
        }

        // Sin clips: el modelo viene rigueado pero quieto. Se le anima el
        // esqueleto por código (caminar, correr, saltar, agacharse).
        //
        // Importante quitarle el Animator: si queda uno (aunque sea sin
        // controlador), Unity le aplica su pose al esqueleto y pelea con el
        // animador por código, dejando la malla deformada.
        Animator stale = instance.GetComponent<Animator>();
        if (stale != null) Object.DestroyImmediate(stale);

        WireKunturBoneAnimator(instance, player);
    }

    // Le pone al modelo el material con la textura del cóndor. El FBX apunta a
    // unas texturas que viven en la carpeta de Tripo y no existen dentro del
    // proyecto, así que Unity lo importa en gris; la textura buena ya está en
    // el proyecto desde el modelo anterior (es el mismo cóndor).
    private static void ApplyKunturMaterial(GameObject instance)
    {
        // Se busca primero la textura de 4K sacada de la carpeta de Tripo (la
        // que el FBX espera de verdad) y, si no está, la del modelo anterior.
        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Kuntur/Color_4ff1d26c-420c-4b0c-95ae-575228baba14.jpg")
            ?? AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Kuntur/Kuntur_Textura.jpg");

        if (texture == null)
        {
            Debug.LogWarning("[Kuntur] No encontré la textura del cóndor: se va a ver gris.");
            return;
        }

        string path = ArtDir + "/Kuntur/Kuntur_Material.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(FindBestShader());
            AssetDatabase.CreateAsset(material, path);
        }

        // Se asigna por TODOS los nombres que usan los distintos shaders de
        // Unity: _BaseMap es el de URP, _MainTex el del pipeline clásico. Con
        // poner solo uno, en el pipeline equivocado el cóndor sale blanco.
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        // Sin brillo: un cóndor con plumas no es de plástico.
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.12f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = material;

        Debug.Log($"[Kuntur] Textura aplicada: {texture.name} (shader {material.shader.name}).");
    }

    // Identifica qué hueso es qué y se los pasa al animador. Los nombres del
    // rig son genéricos (Bone, Bone.003.L...), así que en vez de fiarse de
    // ellos se clasifican por DÓNDE ESTÁN: el sufijo .L/.R marca las dos
    // patas, y de los huesos centrales, los que más se separan del eje son
    // las alas y el más alto es el cuello.
    private static void WireKunturBoneAnimator(GameObject instance, Transform player)
    {
        SkinnedMeshRenderer skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin == null || skin.bones == null || skin.bones.Length == 0)
        {
            Debug.LogWarning("[Kuntur] El modelo no tiene huesos: no se le puede animar la caminata.");
            return;
        }

        List<Transform> bones = new List<Transform>();
        foreach (Transform bone in skin.bones)
        {
            // Los "_end" son las puntas del rig: no deforman nada.
            if (bone != null && !bone.name.EndsWith("_end")) bones.Add(bone);
        }

        // De la cadera al pie, siguiendo la JERARQUÍA del esqueleto y no la
        // altura: ordenar por altura fallaba cuando la rodilla y el pie
        // quedan casi al mismo nivel, y entonces la flexión se aplicaba a la
        // articulación equivocada.
        List<Transform> leftLeg = OrderBoneChain(bones.FindAll(b => b.name.EndsWith(".L")));
        List<Transform> rightLeg = OrderBoneChain(bones.FindAll(b => b.name.EndsWith(".R")));

        // ¿Y si esas cadenas .L/.R no son las patas sino las ALAS? En un rig
        // de ave las dos cosas son cadenas simétricas de tres huesos, y el
        // nombre no distingue. Lo que sí distingue es la ALTURA: las patas
        // cuelgan por debajo del centro del cuerpo, las alas salen por arriba.
        // Columpiar las alas como si fueran patas es justo lo que descuadra
        // al cóndor.
        Bounds? modelBounds = MeasureModelInWorld(instance);
        if (modelBounds != null && leftLeg.Count > 0)
        {
            float bottom = modelBounds.Value.min.y;
            float height = Mathf.Max(modelBounds.Value.size.y, 0.001f);

            // Se mira el hueso más BAJO de la cadena, no el promedio: una pata
            // termina en un pie que llega casi al suelo, un ala nunca baja de
            // media altura. Comparar promedios fallaba porque las patas de un
            // ave son cortas y van metidas bajo el cuerpo, así que su promedio
            // queda a media altura igual que el de un ala: con esa regla el
            // chequeo descartaba las patas de verdad y no se movía nada.
            float lowest = float.PositiveInfinity;
            foreach (Transform bone in leftLeg) lowest = Mathf.Min(lowest, bone.position.y);
            float relative = (lowest - bottom) / height;

            Debug.Log($"[Kuntur] Cadena .L/.R: su punto más bajo queda al {relative * 100f:F0}% de la " +
                      "altura del modelo (0% = el suelo).");

            if (relative > 0.55f)
            {
                Debug.LogWarning("[Kuntur] Esa cadena no baja de la mitad del cuerpo: son alas, no patas. " +
                                 "No se animan, para no deformar el modelo.");
                leftLeg.Clear();
                rightLeg.Clear();
            }
        }

        List<Transform> center = bones.FindAll(b => !b.name.EndsWith(".L") && !b.name.EndsWith(".R"));

        // Alas: de los huesos centrales, los que se apartan del eje del cuerpo.
        float widest = 0f;
        foreach (Transform bone in center)
            widest = Mathf.Max(widest, Mathf.Abs(instance.transform.InverseTransformPoint(bone.position).x));

        List<Transform> leftWing = new List<Transform>();
        List<Transform> rightWing = new List<Transform>();
        if (widest > 0.08f) // si todos están casi en el centro, este rig no tiene alas separadas
        {
            foreach (Transform bone in center)
            {
                float x = instance.transform.InverseTransformPoint(bone.position).x;
                if (Mathf.Abs(x) < widest * 0.45f) continue;
                if (x > 0f) rightWing.Add(bone); else leftWing.Add(bone);
            }

            // Del hombro a la punta.
            leftWing.Sort((a, b) => Mathf.Abs(instance.transform.InverseTransformPoint(a.position).x)
                .CompareTo(Mathf.Abs(instance.transform.InverseTransformPoint(b.position).x)));
            rightWing.Sort((a, b) => Mathf.Abs(instance.transform.InverseTransformPoint(a.position).x)
                .CompareTo(Mathf.Abs(instance.transform.InverseTransformPoint(b.position).x)));
        }

        // Las alas solo se usan si la detección salió SIMÉTRICA. Si de un lado
        // salen 4 huesos y del otro 1, es que agarró huesos del cuerpo por
        // error, y girarlos estira la malla en picos (eso eran las rayas rojas
        // que se veían cruzando la pista). Ante la duda, no se animan: un
        // cóndor con las alas plegadas se ve bien; uno con la espalda estirada
        // hacia el cielo, no.
        if (leftWing.Count < 2 || rightWing.Count < 2 || Mathf.Abs(leftWing.Count - rightWing.Count) > 1)
        {
            Debug.Log($"[Kuntur] Alas descartadas (detección poco fiable: {leftWing.Count}/{rightWing.Count}). " +
                      "El cóndor camina con las alas plegadas.");
            leftWing.Clear();
            rightWing.Clear();
        }

        // El cuello NO se adivina. La regla de "el hueso central más alto"
        // elegía un hueso de la cadena de una pata, y moverlo deformaba el
        // cuerpo. Sin una forma fiable de identificarlo, se deja quieto: que
        // la cabeza no se incline es mucho menos notorio que verla torcida.
        Transform neck = null;

        KunturBoneAnimator animator = instance.GetComponent<KunturBoneAnimator>();
        if (animator == null) animator = instance.AddComponent<KunturBoneAnimator>();

        SerializedObject so = new SerializedObject(animator);
        so.FindProperty("controller").objectReferenceValue = player.GetComponent<CharacterController>();
        so.FindProperty("playerController").objectReferenceValue = player.GetComponent<SimpleThirdPersonController>();
        so.FindProperty("modelRoot").objectReferenceValue = instance.transform;
        so.FindProperty("neck").objectReferenceValue = neck;
        SetTransformArray(so, "leftLeg", leftLeg);
        SetTransformArray(so, "rightLeg", rightLeg);
        SetTransformArray(so, "leftWing", leftWing);
        SetTransformArray(so, "rightWing", rightWing);
        so.ApplyModifiedProperties();

        Debug.Log($"[Kuntur] Esqueleto conectado: pata izq {leftLeg.Count} huesos, " +
                  $"pata der {rightLeg.Count}, alas {leftWing.Count}/{rightWing.Count}, " +
                  $"cuello {(neck != null ? neck.name : "ninguno")}.");
    }

    // Ordena una cadena de huesos de la raíz hacia la punta siguiendo quién es
    // padre de quién, que es el único criterio que no se equivoca.
    private static List<Transform> OrderBoneChain(List<Transform> bones)
    {
        if (bones.Count < 2) return bones;

        Transform root = null;
        foreach (Transform bone in bones)
        {
            if (bone.parent != null && bones.Contains(bone.parent)) continue;
            root = bone;
            break;
        }

        if (root == null) return bones;

        List<Transform> chain = new List<Transform> { root };
        Transform current = root;

        while (chain.Count < bones.Count)
        {
            Transform next = null;
            foreach (Transform bone in bones)
            {
                if (bone.parent != current) continue;
                next = bone;
                break;
            }

            if (next == null) break;
            chain.Add(next);
            current = next;
        }

        // Si la cadena no salió completa, el rig no es lineal: mejor devolver
        // la lista tal cual que una mitad mal ordenada.
        return chain.Count == bones.Count ? chain : bones;
    }

    private static void SetTransformArray(SerializedObject so, string propertyName, List<Transform> values)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null) return;

        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindChildByName(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // Controlador de animación de un solo estado: el clip de caminata en bucle.
    // No hacen falta transiciones porque no hay un segundo clip; el ritmo -y el
    // quedarse quieto- lo maneja KunturRigAnimator cambiando animator.speed.
    // Se regenera en cada construcción para que nunca quede apuntando a un
    // clip viejo si se reemplaza el FBX.
    private static RuntimeAnimatorController BuildKunturAnimatorController()
    {
        AnimationClip clip = FindKunturWalkClip();
        if (clip == null)
        {
            Debug.LogWarning("[Kuntur] El modelo rigueado no trae ningún clip de animación. " +
                             "Revisa que Kuntur_Caminando.fbx se haya importado con Import Animation activo.");
            return null;
        }

        string dir = ArtDir + "/Generated";
        CreateFolderRecursive(dir);
        string path = dir + "/Kuntur_Animator.controller";

        AssetDatabase.DeleteAsset(path);
        UnityEditor.Animations.AnimatorController controller =
            UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);

        UnityEditor.Animations.AnimatorStateMachine machine = controller.layers[0].stateMachine;
        UnityEditor.Animations.AnimatorState walk = machine.AddState("Caminar");
        walk.motion = clip;
        machine.defaultState = walk;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimationClip FindKunturWalkClip()
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(KunturModelPostprocessor.RiggedModelPath))
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip == null) continue;
            if (clip.name.StartsWith("__preview__")) continue; // el que Unity usa para la vista previa

            // Clip VACÍO: no sirve, y es peor que no tener ninguno. Pasa
            // cuando se reemplaza el FBX por otro sin animación: el .meta
            // anterior deja declarado un clip que ya no existe, Unity lo crea
            // sin curvas, y el Animator se lo aplica al esqueleto aplastando
            // todos los huesos. Eso es lo que dejaba a Kuntur hecho un nudo.
            if (clip.empty || clip.length < 0.05f) continue;

            return clip;
        }
        return null;
    }

    // Referencias a las partes móviles del cóndor armado con primitivas, para
    // que KunturVisualAnimator pueda animar patas/alas/cabeza por separado en
    // vez de solo mover el cuerpo entero como un bloque. Con el modelo 3D
    // importado (una sola malla, sin huesos) estos campos quedan en null.
    private struct KunturVisualParts
    {
        // true = se está usando el FBX con esqueleto, que trae su propia
        // animación de caminata. En ese caso NO hay que encima aplicarle el
        // rebote procedural: se pelearían por mover el mismo transform.
        public bool rigged;

        public Transform root;
        public Transform head;
        public Transform leftLegPivot;
        public Transform rightLegPivot;
        public Transform leftWingPivot;
        public Transform rightWingPivot;
    }

    // Carpeta donde se suelta un personaje descargado (Mixamo, Asset Store...).
    // Si hay algo adentro, ESE es el jugador y se ignora el cóndor de Tripo.
    private const string CharacterDropFolder = "Assets/_Project/Art/Personaje";

    private static KunturVisualParts BuildKunturVisual(Transform parent)
    {
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(parent, false);

        // v53: el Kuntur rigueado en Mixamo (Art/Kuntur2) va primero: es el
        // único con todas las animaciones (caminar, correr, saltar, hablar,
        // recoger, nadar...).
        KunturVisualParts? mixamo = TryBuildKuntur2(visual.transform, parent);
        if (mixamo != null) return mixamo.Value;

        // Antes que nada: ¿hay un personaje descargado esperando en su carpeta?
        KunturVisualParts? downloaded = TryBuildDownloadedCharacter(visual.transform, parent);
        if (downloaded != null) return downloaded.Value;

        GameObject importedModel = TryLoadKunturModel();
        if (importedModel != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(importedModel, visual.transform);
            if (instance == null) instance = Object.Instantiate(importedModel, visual.transform, false);
            instance.name = "Modelo_Kuntur";
            instance.transform.localScale = Vector3.one;
            instance.transform.localPosition = Vector3.zero;

            // Modelo con esqueleto y animación propia: se arma por otro lado
            // (Animator + clip de caminata) y no pasa por el rebote procedural
            // que se inventó cuando Kuntur era una malla tiesa sin huesos.
            if (instance.GetComponentInChildren<SkinnedMeshRenderer>() != null)
            {
                SetupKunturRig(instance, visual.transform, parent);
                return new KunturVisualParts { root = visual.transform, rigged = true };
            }

            Debug.LogWarning("[Kuntur] El modelo se cargó SIN esqueleto (malla estática), así que no " +
                             "va a tener caminata ni textura propia. Suele ser que Unity lo importó con " +
                             "Animation Type = None: selecciona el FBX, ponlo en Generic y dale Apply.");

            foreach (Collider col in instance.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);

            // Los modelos generados por IA casi nunca salen derechos: muchos
            // exportadores usan Z como eje vertical, así que al importarlos en
            // Unity el cóndor queda ACOSTADO, y otros lo dejan mirando de
            // costado. Acá se corrigen las dos cosas: primero se endereza
            // (giro en X) y después se le da la vuelta para que mire adelante
            // (giro en Y). Ambos ángulos se pueden ajustar a mano desde el
            // menú Kuntur.
            instance.transform.localRotation = GetKunturModelRotation(instance);

            // El modelo importado puede venir en cualquier escala/pivote según la
            // herramienta que lo generó (Tripo3D no exporta a "tamaño de personaje
            // Unity"). Lo normalizamos automáticamente: lo escalamos para que mida
            // ~1.6m de alto y lo plantamos sobre y=0, en vez de adivinar números
            // fijos que se rompen si se reemplaza el modelo más adelante.
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

                const float targetHeight = 1.6f;
                float currentHeight = Mathf.Max(bounds.size.y, 0.001f);
                instance.transform.localScale = Vector3.one * (targetHeight / currentHeight);

                Bounds scaledBounds = renderers[0].bounds;
                foreach (Renderer r in renderers) scaledBounds.Encapsulate(r.bounds);

                // OJO: Renderer.bounds está en espacio del mundo, así que ya incluye la
                // altura a la que esté parado el jugador (que puede ser cualquier punto
                // de la pista). El bug original restaba "0" absoluto del mundo en vez de
                // la altura real del jugador, así que en cualquier punto que no fuera
                // exactamente Y=0 el modelo quedaba enterrado o flotando. Referenciamos
                // la altura de "visual" (que es la del jugador, el "piso" para este
                // personaje) para plantar los pies ahí sin importar dónde esté parado.
                float groundWorldY = visual.transform.position.y;
                instance.transform.localPosition = new Vector3(0f, groundWorldY - scaledBounds.min.y, 0f);
            }
            // Malla única sin huesos: no hay patas/alas/cabeza por separado
            // que animar, así que solo devolvemos la raíz.
            return new KunturVisualParts { root = visual.transform };
        }

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Cuerpo";
        body.transform.SetParent(visual.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        body.transform.localScale = new Vector3(0.8f, 0.7f, 0.8f);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        SetColor(body, HexColor("#2b2a28"));

        GameObject poncho = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        poncho.name = "Poncho";
        poncho.transform.SetParent(visual.transform, false);
        poncho.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        poncho.transform.localScale = new Vector3(1f, 0.28f, 1f);
        Object.DestroyImmediate(poncho.GetComponent<Collider>());
        SetColor(poncho, HexColor("#c0392b"));

        GameObject ruff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ruff.name = "Cuello";
        ruff.transform.SetParent(visual.transform, false);
        ruff.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        ruff.transform.localScale = new Vector3(0.55f, 0.3f, 0.55f);
        Object.DestroyImmediate(ruff.GetComponent<Collider>());
        SetColor(ruff, HexColor("#f2ece0"));

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Cabeza";
        head.transform.SetParent(visual.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        head.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
        Object.DestroyImmediate(head.GetComponent<Collider>());
        SetColor(head, HexColor("#1f1e1c"));

        GameObject beak = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beak.name = "Pico";
        beak.transform.SetParent(visual.transform, false);
        beak.transform.localPosition = new Vector3(0f, 1.58f, 0.24f);
        beak.transform.localScale = new Vector3(0.12f, 0.1f, 0.22f);
        Object.DestroyImmediate(beak.GetComponent<Collider>());
        SetColor(beak, HexColor("#e0952f"));

        GameObject hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hat.name = "Gorro";
        hat.transform.SetParent(visual.transform, false);
        hat.transform.localPosition = new Vector3(0f, 1.9f, 0f);
        hat.transform.localScale = new Vector3(0.36f, 0.22f, 0.36f);
        Object.DestroyImmediate(hat.GetComponent<Collider>());
        SetColor(hat, HexColor("#b8332a"));

        GameObject pompom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pompom.name = "Pompon";
        pompom.transform.SetParent(visual.transform, false);
        pompom.transform.localPosition = new Vector3(0f, 2.14f, 0f);
        pompom.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
        Object.DestroyImmediate(pompom.GetComponent<Collider>());
        SetColor(pompom, Color.white);

        Transform leftHip = BuildLeg(visual.transform, new Vector3(-0.16f, 0.30f, 0f));
        Transform rightHip = BuildLeg(visual.transform, new Vector3(0.16f, 0.30f, 0f));

        // Alas a la altura del poncho/hombros. Cada una cuelga de un pivote
        // ("Hombro") ubicado junto al cuerpo, con la malla del ala
        // desplazada hacia afuera - así rotar el pivote en Z simula el aleteo
        // (la punta del ala sube/baja) en vez de que el ala rote sobre su
        // propio centro.
        Transform leftWing = BuildWing(visual.transform, 1f);
        Transform rightWing = BuildWing(visual.transform, -1f);

        return new KunturVisualParts
        {
            root = visual.transform,
            head = head.transform,
            leftLegPivot = leftHip,
            rightLegPivot = rightHip,
            leftWingPivot = leftWing,
            rightWingPivot = rightWing,
        };
    }

    // Pivote de cadera arriba de la pata (no en su propio centro): así el
    // ciclo de caminata rota la pata completa desde la cadera, como un paso
    // real, en vez de verse como un péndulo raro balanceándose por la mitad.
    private static Transform BuildLeg(Transform parent, Vector3 hipLocalPosition)
    {
        GameObject hip = new GameObject("Cadera");
        hip.transform.SetParent(parent, false);
        hip.transform.localPosition = hipLocalPosition;

        GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leg.name = "Pata";
        leg.transform.SetParent(hip.transform, false);
        leg.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        leg.transform.localScale = new Vector3(0.08f, 0.15f, 0.08f);
        Object.DestroyImmediate(leg.GetComponent<Collider>());
        SetColor(leg, HexColor("#e0952f"));

        return hip.transform;
    }

    // side = +1 para el ala izquierda (sale hacia +X), -1 para la derecha
    // (hacia -X). Devuelve el pivote del hombro, que es lo que se rota para
    // el aleteo (nunca la malla del ala directamente).
    private static Transform BuildWing(Transform parent, float side)
    {
        GameObject shoulder = new GameObject(side > 0 ? "Hombro_Izquierdo" : "Hombro_Derecho");
        shoulder.transform.SetParent(parent, false);
        shoulder.transform.localPosition = new Vector3(0.38f * side, 1.15f, -0.05f);

        GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wing.name = side > 0 ? "Ala_Izquierda" : "Ala_Derecha";
        wing.transform.SetParent(shoulder.transform, false);
        wing.transform.localPosition = new Vector3(0.32f * side, -0.04f, 0f);
        wing.transform.localScale = new Vector3(0.55f, 0.05f, 0.26f);
        Object.DestroyImmediate(wing.GetComponent<Collider>());
        SetColor(wing, HexColor("#1f1e1c"));

        return shoulder.transform;
    }

    private static void BuildManagers()
    {
        GameObject managers = new GameObject("--- MANAGERS ---");
        managers.AddComponent<GameManager>();
        managers.AddComponent<ScoreManager>();
        // La Salud del Valle arranca en CERO: el valle está contaminado y solo
        // sube por lo que hace el jugador. Las cuentas cierran justo en 100%:
        // los residuos reparten 80 puntos + 4 muestras de agua x 5 puntos.
        ValleyHealthManager valleyHealth = managers.AddComponent<ValleyHealthManager>();
        SerializedObject healthSo = new SerializedObject(valleyHealth);
        healthSo.FindProperty("startingHealth").floatValue = 0f;
        healthSo.ApplyModifiedProperties();

        managers.AddComponent<CountdownManager>();
        managers.AddComponent<TrashInventory>();

        // Decide qué postes llevan la luz real encendida de noche (los más
        // cercanos a la cámara). Ver StreetLightBudget.
        managers.AddComponent<StreetLightBudget>();

        // Música del valle y efectos de las mecánicas.
        AudioManager audio = managers.AddComponent<AudioManager>();
        SerializedObject audioSo = new SerializedObject(audio);
        // v56: la música de la aventura es la nueva (musicaactualizada); la
        // anterior queda de respaldo por si el archivo no está.
        AudioClip adventure = File.Exists(AudioDir + "/Musica_Aventura.mp3") ? LoadAudio("Musica_Aventura") : null;
        audioSo.FindProperty("music").objectReferenceValue = adventure != null ? adventure : LoadAudio("Musica_Juego");
        audioSo.FindProperty("trashPickup").objectReferenceValue = LoadAudio("SFX_RecogerBasura");
        audioSo.FindProperty("waterSample").objectReferenceValue = LoadAudio("SFX_MuestraAgua");
        audioSo.FindProperty("victory").objectReferenceValue = LoadAudio("SFX_Victoria");
        audioSo.FindProperty("defeat").objectReferenceValue = LoadAudio("SFX_Derrota");
        audioSo.FindProperty("missionMusic").objectReferenceValue = LoadAudio("Musica_Mision");
        audioSo.FindProperty("sleepMusic").objectReferenceValue = LoadAudio("Musica_Dormir");
        audioSo.ApplyModifiedProperties();

        // Hora del día: el sol avanza mientras el jugador limpia el valle.
        DayNightCycle dayNight = managers.AddComponent<DayNightCycle>();
        SerializedObject dayNightSo = new SerializedObject(dayNight);
        GameObject sunGO = GameObject.Find("Directional Light");
        if (sunGO != null) dayNightSo.FindProperty("sun").objectReferenceValue = sunGO.GetComponent<Light>();
        dayNightSo.FindProperty("startHour").floatValue = 8.5f;
        // Un día más largo (de 8 AM a 9 PM son ~13 minutos reales): alcanza
        // para varias misiones antes de volver a casa.
        dayNightSo.FindProperty("minutesPerFullDay").floatValue = 24f;
        dayNightSo.ApplyModifiedProperties();

        ObjectiveSystem objectives = managers.AddComponent<ObjectiveSystem>();
        // La lista la arma MissionDirector nivel por nivel (hablar con el
        // vecino, limpiar sus zonas). Al arrancar solo se ve Doña Rosa.
        objectives.SetObjectives(new List<Objective>
        {
            new Objective { id = "hablar_con_rosa", description = "Convence a Doña Rosa de no botar basura al río", targetCount = 1, isSecondary = true },
        });
        EditorUtility.SetDirty(objectives);
    }

    private static void BuildMechanics()
    {
        GameObject mechanics = new GameObject("--- MECANICAS ---");
        int interactableLayer = LayerMask.NameToLayer(InteractableLayerName);
        if (interactableLayer == -1) interactableLayer = 8;

        // La basura ya no está toda junta en un botadero: está repartida en
        // las ZONAS de las misiones (Plaza Vea, parque, camino a la chacra y
        // río). Cada zona aparece recién cuando un vecino te la encarga (ver
        // BuildMissionZones y MissionDirector).
        // El botadero del puente ya no existe: esa basura ahora es parte de la
        // zona "camino a la chacra".
        BuildMissionZones(mechanics.transform, interactableLayer);
        BuildHomeDoor(mechanics.transform, interactableLayer);
        // v53: tres perros por la chacra y el gatito de la casa de Kuntur.
        BuildAnimals(mechanics.transform, interactableLayer);

        // Yamile (misión principal, líder comunitaria en la plaza).
        GameObject yamile = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        yamile.name = "NPC_Yamile";
        yamile.transform.SetParent(mechanics.transform);
        // En la vereda, no en medio de la pista (antes estaba en x=0, o sea
        // parada sobre la línea amarilla de la Calle Real).
        yamile.transform.position = YamileSpot + Vector3.up;
        yamile.layer = interactableLayer;
        SetColor(yamile, HexColor("#6d5847"));
        DecorateNPC(yamile, HexColor("#e67e22"), HexColor("#8a5a3b"), HexColor("#c58c68"));
        SwapNpcBody(yamile, PeopleDir + "/Prefabs/downtown/casual_Female_K.prefab");
        yamile.GetComponent<Collider>().enabled = false; // se prende si le toca dar la misión

        DialogueNPC dialogueYamile = yamile.AddComponent<DialogueNPC>();
        SerializedObject yamileSo = new SerializedObject(dialogueYamile);
        yamileSo.FindProperty("npcName").stringValue = "Yamile";
        yamileSo.FindProperty("npcRole").stringValue = "LÍDER COMUNITARIA";
        yamileSo.FindProperty("openingLine").stringValue =
            "¡Kuntur, compañero! Qué bueno que llegas. El Mantaro está lleno de basura y el barrio tiene que organizarse. ¿Nos ayudas?";
        yamileSo.FindProperty("objectiveId").stringValue = "hablar_con_yamile";

        SerializedProperty yamileOptions = yamileSo.FindProperty("options");
        yamileOptions.arraySize = 3;
        SetDialogueOption(yamileOptions.GetArrayElementAtIndex(0),
            "¡Claro, Yamile! Cuenta conmigo.", true,
            "¡Eso, compañero! Los vecinos te van a ir pidiendo ayuda, búscalos en el mapa.");
        SetDialogueOption(yamileOptions.GetArrayElementAtIndex(1),
            "No es mi problema, solo estoy de paso.", false,
            "Todos tomamos el agua del mismo río, Kuntur. Sí es tu problema, pues.");
        SetDialogueOption(yamileOptions.GetArrayElementAtIndex(2),
            "Más tarde, ahorita estoy ocupado.", false,
            "La basura no espera, compañero: con la primera lluvia se va al río.");
        yamileSo.ApplyModifiedProperties();
        // Yamile también puede ser quien te encargue una misión.
        MissionGivers.Add(dialogueYamile);
        MissionGiverNames.Add("Yamile");
        MissionGiverFemale.Add(true);
        MissionGiverRoles.Add("LÍDER COMUNITARIA");
        MissionGiverZones.Add("");
        MissionGiverVoices.Add("leader");

        // Doña Rosa, cerca de su bodega.
        GameObject rosa = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        rosa.name = "NPC_DonaRosa";
        rosa.transform.SetParent(mechanics.transform);
        // Afuera de su bodega, no dentro de la pared ni sobre el Jr. Cusco.
        rosa.transform.position = RosaSpot + Vector3.up;
        rosa.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // v56: mirando al Jr. Cusco
        rosa.layer = interactableLayer;
        SetColor(rosa, HexColor("#5e4b3c"));
        DecorateNPC(rosa, HexColor("#9b5b8a"), HexColor("#3f3630"), HexColor("#c58c68"));
        SwapNpcBody(rosa, PeopleDir + "/Prefabs/elder/elder_Female_A.prefab");

        DialogueNPC dialogue = rosa.AddComponent<DialogueNPC>();
        SerializedObject npcSo = new SerializedObject(dialogue);
        npcSo.FindProperty("npcName").stringValue = "Doña Rosa";
        npcSo.FindProperty("npcRole").stringValue = "VECINA · BODEGUERA";
        npcSo.FindProperty("openingLine").stringValue =
            "Ay, hijito, aquí siempre hemos botado la basura al río, desde que mi mamá tenía la bodega... ¿qué daño va a hacer un poquito, pues?";
        npcSo.FindProperty("objectiveId").stringValue = "hablar_con_rosa";

        SerializedProperty options = npcSo.FindProperty("options");
        options.arraySize = 3;
        SetDialogueOption(options.GetArrayElementAtIndex(0),
            "Doña Rosa, esa misma agua es la que después toman sus nietitos.", true,
            "Ay... tienes razón, hijito, no lo había pensado así. Desde hoy la guardo para el camión, te lo prometo.");
        SetDialogueOption(options.GetArrayElementAtIndex(1),
            "¡Le van a poner una multa si sigue botando basura ahí!", false,
            "¿Multa? ¡Ya, ya, vete de aquí! A mí nadie me viene a amenazar en mi propia bodega.");
        SetDialogueOption(options.GetArrayElementAtIndex(2),
            "No pasa nada, un poquito no afecta a nadie.", false,
            "¿Ya ves? Eso mismo digo yo, hijito... (pero el río sigue igual de sucio)");
        npcSo.ApplyModifiedProperties();
    }

    // Los vecinos dejan de ser cápsulas de color: se les agrega cabeza, chullo
    // y poncho, para que se lean como las personas del pueblo que aparecen en
    // las imágenes del GDD. El collider y el script de diálogo siguen en la
    // cápsula raíz; esto es solo decoración encima.
    private static void DecorateNPC(GameObject npc, Color ponchoColor, Color hatColor, Color skinColor)
    {
        Vector3 basePos = npc.transform.position - Vector3.up * 1f; // pies (la cápsula mide 2)

        PrimitiveObject(npc.transform, "Poncho", PrimitiveType.Cylinder,
            basePos + new Vector3(0f, 1.25f, 0f), new Vector3(0.62f, 0.3f, 0.62f), ponchoColor);
        PrimitiveObject(npc.transform, "Cabeza", PrimitiveType.Sphere,
            basePos + new Vector3(0f, 1.86f, 0f), Vector3.one * 0.42f, skinColor);
        PrimitiveObject(npc.transform, "Sombrero_Copa", PrimitiveType.Cylinder,
            basePos + new Vector3(0f, 2.12f, 0f), new Vector3(0.34f, 0.12f, 0.34f), hatColor);
        PrimitiveObject(npc.transform, "Sombrero_Ala", PrimitiveType.Cylinder,
            basePos + new Vector3(0f, 2.02f, 0f), new Vector3(0.62f, 0.03f, 0.62f), hatColor);
        PrimitiveObject(npc.transform, "Brazo_Izq", PrimitiveType.Capsule,
            basePos + new Vector3(-0.42f, 1.25f, 0f), new Vector3(0.16f, 0.32f, 0.16f), ponchoColor);
        PrimitiveObject(npc.transform, "Brazo_Der", PrimitiveType.Capsule,
            basePos + new Vector3(0.42f, 1.25f, 0f), new Vector3(0.16f, 0.32f, 0.16f), ponchoColor);
    }

    // Los residuos ya no son cubos marrones: cada uno es una botella, una lata
    // o un papel arrugado, armados con primitivas, igual que los que aparecen
    // tirados junto al río en las imágenes del GDD. Que se reconozca QUÉ es
    // cada residuo importa: la mecánica del juego es clasificarlos, no recoger
    // cubos genéricos.
    private static void BuildTrashItem(Transform parent, int index, Vector3 groundPosition, int interactableLayer, string objectiveId = null)
    {
        int kind = index % 3;
        string kindName = kind == 0 ? "Botella" : kind == 1 ? "Lata" : "Papel";

        GameObject trash = new GameObject($"Basura_{index + 1}_{kindName}");
        trash.transform.SetParent(parent);
        trash.transform.position = groundPosition;
        trash.transform.rotation = Quaternion.Euler(0f, (index * 47f) % 360f, 0f);
        trash.layer = interactableLayer;

        CapsuleCollider collider = trash.AddComponent<CapsuleCollider>();
        // Trigger: detecta la cercanía del jugador pero no le tapa el paso
        // (con la basura ya a tamaño real, un collider sólido lo frenaría).
        collider.isTrigger = true;

        // Los residuos van a escala "de verdad" (una botella de gaseosa le
        // llega a Kuntur casi a la rodilla): antes eran tan chicos que se
        // perdían contra el pasto y no se sabía si había algo que recoger.
        if (kind == 0)
        {
            // Botella de plástico: cuerpo, hombro, cuello y tapa.
            PrimitiveObject(trash.transform, "Cuerpo", PrimitiveType.Cylinder,
                groundPosition + new Vector3(0f, 0.4f, 0f), new Vector3(0.26f, 0.4f, 0.26f), HexColor("#bfe9f5"));
            PrimitiveObject(trash.transform, "Hombro", PrimitiveType.Cylinder,
                groundPosition + new Vector3(0f, 0.86f, 0f), new Vector3(0.19f, 0.09f, 0.19f), HexColor("#bfe9f5"));
            PrimitiveObject(trash.transform, "Cuello", PrimitiveType.Cylinder,
                groundPosition + new Vector3(0f, 1f, 0f), new Vector3(0.11f, 0.09f, 0.11f), HexColor("#cfeefb"));
            PrimitiveObject(trash.transform, "Tapa", PrimitiveType.Cylinder,
                groundPosition + new Vector3(0f, 1.13f, 0f), new Vector3(0.14f, 0.055f, 0.14f), HexColor("#2f80c2"));
            PrimitiveObject(trash.transform, "Etiqueta", PrimitiveType.Cylinder,
                groundPosition + new Vector3(0f, 0.44f, 0f), new Vector3(0.27f, 0.13f, 0.27f), HexColor("#e8f4f8"));

            collider.height = 1.4f;
            collider.radius = 0.38f;
            collider.center = new Vector3(0f, 0.66f, 0f);
        }
        else if (kind == 1)
        {
            // Lata de aluminio con su franja de color.
            PrimitiveObject(trash.transform, "Cuerpo", PrimitiveType.Cylinder,
                groundPosition + new Vector3(0f, 0.29f, 0f), new Vector3(0.24f, 0.29f, 0.24f), HexColor("#c8ccd0"));
            PrimitiveObject(trash.transform, "Franja", PrimitiveType.Cylinder,
                groundPosition + new Vector3(0f, 0.29f, 0f), new Vector3(0.25f, 0.11f, 0.25f),
                index % 2 == 0 ? HexColor("#d94f4f") : HexColor("#e0952f"));
            PrimitiveObject(trash.transform, "Tapa", PrimitiveType.Cylinder,
                groundPosition + new Vector3(0f, 0.6f, 0f), new Vector3(0.22f, 0.022f, 0.22f), HexColor("#9aa0a6"));

            collider.height = 0.85f;
            collider.radius = 0.36f;
            collider.center = new Vector3(0f, 0.36f, 0f);
        }
        else
        {
            // Papel/bolsa arrugada: dos bultos irregulares.
            Mesh ico = LowPolyMeshFactory.Icosphere(1);
            MeshObject(trash.transform, "Bulto_1", ico,
                groundPosition + new Vector3(0f, 0.26f, 0f), new Vector3(0.7f, 0.48f, 0.62f), HexColor("#f2f0e8"),
                Quaternion.Euler(12f, index * 33f, 8f));
            MeshObject(trash.transform, "Bulto_2", ico,
                groundPosition + new Vector3(0.2f, 0.37f, 0.11f), new Vector3(0.44f, 0.37f, 0.4f), HexColor("#e6e2d6"),
                Quaternion.Euler(-15f, index * 21f, 20f));

            collider.height = 0.85f;
            collider.radius = 0.42f;
            collider.center = new Vector3(0f, 0.33f, 0f);
        }

        // Las piezas del residuo no deben ser estáticas: el objeto entero
        // flota, gira y se encoge al recogerlo.
        foreach (Transform child in trash.transform) child.gameObject.isStatic = false;

        TrashPickup pickup = trash.AddComponent<TrashPickup>();
        SerializedObject pickupSo = new SerializedObject(pickup);
        // El tipo coincide con la forma que se acaba de armar, para que el
        // inventario cuente botellas como botellas y latas como latas.
        pickupSo.FindProperty("trashType").enumValueIndex = kind;
        // Entre TODOS los residuos suman 80 de Salud del Valle, y las 4
        // muestras de agua ponen el 20 que falta para el 100%. Se reparte en
        // vez de fijar un número, así agregar basura no rompe la barra.
        // Los residuos se reutilizan misión tras misión (la gente vuelve a
        // ensuciar), así que cada uno suma un poquito fijo al valle.
        pickupSo.FindProperty("healthContribution").floatValue = 1.2f;
        // Cada residuo reporta a la misión de SU zona (ver MissionDirector).
        if (!string.IsNullOrEmpty(objectiveId)) pickupSo.FindProperty("objectiveId").stringValue = objectiveId;
        pickupSo.ApplyModifiedProperties();

        CollectibleGlow glow = trash.AddComponent<CollectibleGlow>();
        SerializedObject glowSo = new SerializedObject(glow);
        glowSo.FindProperty("glowColor").colorValue = UIPalette_Green();
        glowSo.ApplyModifiedProperties();

        // Más grandecita: que se vea de lejos. Y un punto verde que solo ve
        // la cámara del mapa, para encontrarla en el minimapa.
        trash.transform.localScale = Vector3.one * 0.6f; // tamaño de basura real, no de un niño
        AddMapIcon(trash.transform, groundPosition, UIPalette_Green(), 3.2f);
    }

    // Cantidad total de residuos recolectables del nivel:
    // 8 (anillo del lote) + 2 (tablero del puente) + 2 (orilla) + 4 + 14
    // (descampado cruzando el puente) + 6 (montón del lote) = 36.
    private const int EastDumpTrash = 14;
    // Residuos de las cuatro zonas de misión: Plaza Vea 8, parque 8, camino a
    // la chacra 10 y río 7.
    private const int TotalTrash = 8 + 8 + 10 + 7;

    // Descampado de la otra orilla: del fin del puente (x≈72) hasta antes del
    // camino de tierra (x≈88), y en z lejos de la avenida y sus veredas
    // (24–36). Posiciones fijas por semilla, así la basura queda siempre en
    // el mismo lugar entre una reconstrucción y otra.
    private static void AddEastDumpTrash(List<Vector3> positions, int count)
    {
        System.Random rng = new System.Random(20260921);
        int added = 0;
        int attempts = 0;

        while (added < count && attempts < 800)
        {
            attempts++;
            bool north = (added % 2) == 0;
            float z = north ? NextFloat(rng, 38.5f, 54f) : NextFloat(rng, 6f, 21.5f);
            Vector3 candidate = new Vector3(NextFloat(rng, 73f, 86.5f), 0f, z);

            if (OverlapsPlaced(candidate, 0.7f)) continue;     // árboles, rocas, casas

            bool tooClose = false;
            foreach (Vector3 other in positions)
            {
                if ((other - candidate).sqrMagnitude < 2.4f * 2.4f) { tooClose = true; break; }
            }
            if (tooClose) continue;

            positions.Add(candidate);
            added++;
        }

        if (added < count)
            Debug.LogWarning($"[Kuntur] Solo entraron {added} de {count} residuos en el descampado del puente.");
    }

    // Basura de relleno (no se recoge) en el mismo descampado: bolsas, cajas
    // aplastadas y llantas desparramadas. Sin esto, los residuos recolectables
    // se ven como objetos sueltos puestos a mano; con el relleno el lugar se
    // lee como lo que es, un basural a la entrada del puente.
    private static void BuildBridgeDump(Transform parent)
    {
        GameObject dump = new GameObject("Basural_Puente");
        dump.transform.SetParent(parent);

        System.Random rng = new System.Random(778899);
        Mesh ico = LowPolyMeshFactory.Icosphere(1);
        Color[] bagColors =
        {
            HexColor("#3b3f45"), HexColor("#4a4640"), HexColor("#2f3338"),
            HexColor("#d9d6cc"), HexColor("#5b7c99"), HexColor("#6b4f3a"),
        };

        // Tierra removida debajo, para que no parezca basura sobre césped
        // cuidado. Una mancha por franja.
        foreach (float z in new[] { 46f, 14f })
        {
            PrimitiveObject(dump.transform, $"Tierra_Basural_{z}", PrimitiveType.Cylinder,
                new Vector3(79.5f, 0.006f, z), new Vector3(15f, 0.006f, 13f), HexColor("#8c7556"));
        }

        int placed = 0;
        for (int i = 0; i < 70 && placed < 34; i++)
        {
            bool north = (i % 2) == 0;
            Vector3 pos = new Vector3(NextFloat(rng, 73f, 86.5f), 0f,
                north ? NextFloat(rng, 39f, 53.5f) : NextFloat(rng, 6.5f, 21f));
            if (OverlapsPlaced(pos, 0.6f)) continue;

            int kind = rng.Next(10);
            if (kind < 6)
            {
                float size = NextFloat(rng, 0.45f, 0.95f);
                MeshObject(dump.transform, $"Bolsa_Basural_{placed}", ico,
                    pos + Vector3.up * size * 0.3f,
                    new Vector3(size, size * 0.7f, size * 0.85f), bagColors[rng.Next(bagColors.Length)],
                    Quaternion.Euler(NextFloat(rng, -15f, 15f), NextFloat(rng, 0f, 360f), NextFloat(rng, -15f, 15f)));
            }
            else if (kind < 9)
            {
                // Cartón aplastado: plano y torcido.
                PrimitiveObject(dump.transform, $"Carton_Basural_{placed}", PrimitiveType.Cube,
                    pos + Vector3.up * 0.04f,
                    new Vector3(NextFloat(rng, 0.6f, 1.1f), 0.06f, NextFloat(rng, 0.5f, 0.9f)), HexColor("#a9794c"),
                    Quaternion.Euler(NextFloat(rng, -4f, 4f), NextFloat(rng, 0f, 360f), NextFloat(rng, -4f, 4f)));
            }
            else
            {
                PrimitiveObject(dump.transform, $"Llanta_Basural_{placed}", PrimitiveType.Cylinder,
                    pos + Vector3.up * 0.16f,
                    new Vector3(0.8f, 0.16f, 0.8f), HexColor("#26292d"),
                    Quaternion.Euler(90f, NextFloat(rng, 0f, 360f), 0f));
            }
            placed++;
        }

        // Nada de esto debe estorbar el paso ni tapar los residuos: se le
        // quitan los colliders.
        foreach (Collider collider in dump.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(collider);
    }

    // Botadero informal de la esquina: bolsas, cajas y llantas amontonadas
    // (decorado, no se recoge) más 6 residuos que sí se pueden clasificar.
    // Es la imagen que motiva la misión: una esquina del barrio convertida en
    // basural, como pasa de verdad en varios jirones de Huancayo.
    private static void BuildTrashPile(Transform parent, Vector3 center, int startIndex, int interactableLayer)
    {
        // El terreno ya se apartó antes de levantar las casas (ver
        // CleanupLotCenter en BuildEnvironment), así que acá no hay que buscar
        // sitio: se construye donde se pidió.
        GameObject pile = new GameObject("Botadero_Esquina");
        pile.transform.SetParent(parent);

        BuildVacantLot(pile.transform, center);

        System.Random rng = new System.Random(556677);
        Mesh ico = LowPolyMeshFactory.Icosphere(1);

        Color[] bagColors =
        {
            HexColor("#3b3f45"), HexColor("#4a4640"), HexColor("#5a5048"), HexColor("#2f3338"),
        };

        // Bolsas de basura amontonadas.
        for (int i = 0; i < 16; i++)
        {
            Vector3 offset = new Vector3(NextFloat(rng, -2.6f, 2.6f), 0f, NextFloat(rng, -2.2f, 2.2f));
            float size = NextFloat(rng, 0.7f, 1.35f);
            MeshObject(pile.transform, $"Bolsa_{i}", ico,
                center + offset + Vector3.up * size * 0.34f,
                new Vector3(size, size * 0.8f, size * 0.9f), bagColors[i % bagColors.Length],
                Quaternion.Euler(NextFloat(rng, -12f, 12f), NextFloat(rng, 0f, 360f), NextFloat(rng, -12f, 12f)));
        }

        // Cajas de cartón mojadas.
        for (int i = 0; i < 7; i++)
        {
            Vector3 offset = new Vector3(NextFloat(rng, -3f, 3f), 0f, NextFloat(rng, -2.4f, 2.4f));
            float size = NextFloat(rng, 0.5f, 0.95f);
            PrimitiveObject(pile.transform, $"Caja_{i}", PrimitiveType.Cube,
                center + offset + Vector3.up * size * 0.5f,
                new Vector3(size * 1.3f, size, size), HexColor("#a9794c"),
                Quaternion.Euler(NextFloat(rng, -8f, 8f), NextFloat(rng, 0f, 360f), NextFloat(rng, -8f, 8f)));
        }

        // Un par de llantas viejas, el clásico de todo botadero.
        for (int i = 0; i < 3; i++)
        {
            PrimitiveObject(pile.transform, $"Llanta_{i}", PrimitiveType.Cylinder,
                center + new Vector3(NextFloat(rng, -3.2f, 3.2f), 0.16f, NextFloat(rng, -2.6f, 2.6f)),
                new Vector3(0.85f, 0.16f, 0.85f), HexColor("#26292d"),
                Quaternion.Euler(90f, NextFloat(rng, 0f, 360f), 0f));
        }

        // Residuos que SÍ se pueden recoger, alrededor del montón.
        for (int i = 0; i < 6; i++)
        {
            float angle = (i / 6f) * Mathf.PI * 2f;
            Vector3 position = center + new Vector3(Mathf.Cos(angle) * 4.2f, 0f, Mathf.Sin(angle) * 3.6f);
            BuildTrashItem(parent, startIndex + i, position, interactableLayer);
        }
    }

    // El lote baldío en sí: piso de tierra pisada y un cerco de adobe a medio
    // caer en tres lados, con la boca abierta hacia la calle. Es lo que hace
    // que se lea como "el terreno de la esquina donde todos botan" y no como
    // basura tirada en medio del pasto.
    // ---------------------------------------------------------------
    // Plaza Vea
    // ---------------------------------------------------------------
    // Hipermercado como el de verdad: caja roja grande, zócalo de piedra
    // abajo, entrada de vidrio con marquesina y franja turquesa, el logo
    // "plazaVea hiper" en blanco con la curva amarilla, y el estacionamiento
    // adelante con autos y postes. La fachada mira al SUR, a la Av. Giráldez.
    private static void BuildPlazaVea(Transform parent)
    {
        Occupy(PlazaVeaBlockCenter, PlazaVeaBlockSizeX, PlazaVeaBlockSizeZ);

        GameObject store = new GameObject("PlazaVea_Hiper");
        store.transform.SetParent(parent);

        Color red = HexColor("#d4261c");
        Color redDark = HexColor("#a51d15");
        Color stone = HexColor("#6f665c");
        Color stoneDark = HexColor("#524a43");
        Color glass = HexColor("#5a8292");
        Color white = HexColor("#f4f4f2");

        const float parkingFrontZ = 37.2f;
        const float parkingBackZ = 43.2f;
        const float frontZ = 44.6f;   // la fachada
        const float backZ = 51.6f;
        const float leftX = -34.5f;
        const float rightX = -8.5f;
        const float height = 9f;
        float width = rightX - leftX;
        float depth = backZ - frontZ;
        float cx = (leftX + rightX) / 2f;
        float cz = (frontZ + backZ) / 2f;

        // ---- Estacionamiento ----
        PrimitiveObject(store.transform, "Estacionamiento", PrimitiveType.Cube,
            new Vector3(cx, 0.015f, (parkingFrontZ + parkingBackZ) / 2f),
            new Vector3(27f, 0.03f, parkingBackZ - parkingFrontZ), HexColor("#3b3f44"));

        for (int i = 0; i <= 9; i++)
        {
            PrimitiveObject(store.transform, $"Linea_Estacionamiento_{i}", PrimitiveType.Cube,
                new Vector3(-35f + i * 3f, 0.035f, 40.9f), new Vector3(0.12f, 0.02f, 4.4f), white);
        }

        // Vereda de la tienda, entre los autos y la puerta.
        PrimitiveObject(store.transform, "Vereda_Tienda", PrimitiveType.Cube,
            new Vector3(cx, 0.07f, (parkingBackZ + frontZ) / 2f),
            new Vector3(27f, 0.14f, frontZ - parkingBackZ), HexColor("#c9c6bf"));

        // Autos estacionados de trompa hacia la tienda, con huecos libres.
        int[] slots = { 0, 1, 3, 5, 6, 8 };
        Color[] carColors =
        {
            HexColor("#c8ccd0"), HexColor("#2b2f33"), HexColor("#8a1f1a"),
            HexColor("#e9e9e6"), HexColor("#23426b"), HexColor("#7b7f84"),
        };
        for (int i = 0; i < slots.Length; i++)
        {
            BuildParkedCar(store.transform, new Vector3(-35f + slots[i] * 3f + 1.5f, 0f, 40.6f), carColors[i], 0f);
        }

        // ---- Edificio ----
        PrimitiveObject(store.transform, "Edificio", PrimitiveType.Cube,
            new Vector3(cx, height / 2f, cz), new Vector3(width, height, depth), red);
        PrimitiveObject(store.transform, "Remate", PrimitiveType.Cube,
            new Vector3(cx, height + 0.15f, cz), new Vector3(width + 0.3f, 0.3f, depth + 0.3f), redDark);

        // Colisión aparte (las piezas de PrimitiveObject no la tienen): así
        // Kuntur no atraviesa la tienda.
        GameObject solid = new GameObject("Edificio_Colision");
        solid.transform.SetParent(store.transform);
        solid.transform.position = new Vector3(cx, height / 2f, cz);
        BoxCollider box = solid.AddComponent<BoxCollider>();
        box.size = new Vector3(width, height, depth);

        // Paneles verticales del revestimiento rojo (en la foto la fachada es
        // de planchas perforadas, no una pared lisa).
        for (float x = leftX + 0.65f; x < rightX; x += 1.3f)
        {
            PrimitiveObject(store.transform, "Panel_Junta", PrimitiveType.Cube,
                new Vector3(x, 5.8f, frontZ - 0.02f), new Vector3(0.05f, 6.3f, 0.03f), redDark);
        }

        // Zócalo de piedra al frente y en el costado que da a la Calle Real.
        PrimitiveObject(store.transform, "Zocalo_Frente", PrimitiveType.Cube,
            new Vector3(cx, 1.3f, frontZ - 0.15f), new Vector3(width + 0.3f, 2.6f, 0.3f), stone);
        PrimitiveObject(store.transform, "Zocalo_Este", PrimitiveType.Cube,
            new Vector3(rightX + 0.15f, 1.3f, cz), new Vector3(0.3f, 2.6f, depth + 0.3f), stone);
        for (int row = 1; row <= 2; row++)
        {
            PrimitiveObject(store.transform, "Junta_Piedra_Frente", PrimitiveType.Cube,
                new Vector3(cx, row * 0.86f, frontZ - 0.31f), new Vector3(width + 0.3f, 0.04f, 0.02f), stoneDark);
            PrimitiveObject(store.transform, "Junta_Piedra_Este", PrimitiveType.Cube,
                new Vector3(rightX + 0.31f, row * 0.86f, cz), new Vector3(0.02f, 0.04f, depth + 0.3f), stoneDark);
        }

        // ---- Entrada ----
        const float doorX = -19f;
        PrimitiveObject(store.transform, "Entrada_Vidrio", PrimitiveType.Cube,
            new Vector3(doorX, 1.35f, frontZ - 0.33f), new Vector3(7f, 2.5f, 0.08f), glass);
        for (int i = 0; i <= 5; i++)
        {
            PrimitiveObject(store.transform, "Marco_Vidrio", PrimitiveType.Cube,
                new Vector3(doorX - 3.5f + i * 1.4f, 1.35f, frontZ - 0.39f), new Vector3(0.08f, 2.5f, 0.04f), white);
        }
        PrimitiveObject(store.transform, "Marquesina", PrimitiveType.Cube,
            new Vector3(doorX, 2.85f, frontZ - 0.95f), new Vector3(8.4f, 0.22f, 1.5f), white);
        PrimitiveObject(store.transform, "Franja_Turquesa", PrimitiveType.Cube,
            new Vector3(doorX, 2.62f, frontZ - 1.68f), new Vector3(8.4f, 0.26f, 0.06f), HexColor("#1fb3b0"));

        // ---- Logos ----
        // Dos al frente (como en la foto) y uno en el costado que se ve desde
        // la Calle Real. facingYaw = hacia dónde mira el logo.
        BuildPlazaVeaLogo(store.transform, new Vector3(-28.3f, 5.7f, frontZ - 0.07f), 180f, 1f);
        BuildPlazaVeaLogo(store.transform, new Vector3(-14.8f, 5.7f, frontZ - 0.07f), 180f, 1f);
        BuildPlazaVeaLogo(store.transform, new Vector3(rightX + 0.07f, 5.7f, cz + 1.6f), 90f, 0.72f);

        // ---- Postes del estacionamiento ----
        foreach (float x in new[] { -31f, -21.5f, -12f })
        {
            BuildStreetLamp(store.transform, new Vector3(x, 0f, 37.6f), Vector3.forward, true);
        }
    }

    // Logo "plazaVea hiper": "plaza" y "Vea" en blanco y la curva amarilla que
    // nace en la V y pasa por encima de "ea".
    //
    // "plaza" y "Vea" son DOS textos que se tocan justo donde empieza la V.
    // Así la curva se puede colgar exactamente de la V sin saber cuánto mide
    // cada letra en esta fuente: el punto de unión es siempre el mismo.
    private static void BuildPlazaVeaLogo(Transform parent, Vector3 anchor, float facingYaw, float scale)
    {
        Quaternion facing = Quaternion.Euler(0f, facingYaw, 0f);
        Quaternion textRotation = ReadableFrom(facing);
        Vector3 right = facing * Vector3.left;     // la derecha de quien mira el logo
        Vector3 outward = facing * Vector3.forward;

        float em = 1.8f * scale;                   // alto de la letra en metros
        float fontSize = em * 10f;                 // en TextMeshPro 3D, 10 de fuente = 1 m
        Color white = HexColor("#ffffff");

        MakeLogoText(parent, "Logo_plaza", "plaza", fontSize, white,
            anchor - right * (0.04f * em), textRotation, new Vector2(1f, 0f), TextAlignmentOptions.BottomRight, em);
        MakeLogoText(parent, "Logo_Vea", "Vea", fontSize, white,
            anchor + right * (0.02f * em), textRotation, new Vector2(0f, 0f), TextAlignmentOptions.BottomLeft, em);
        MakeLogoText(parent, "Logo_hiper", "hiper", fontSize * 0.5f, white,
            anchor + right * (0.55f * em) - Vector3.up * (0.06f * em), textRotation, new Vector2(0f, 1f),
            TextAlignmentOptions.TopLeft, em);

        // Curva amarilla: una Bézier cuadrática en el plano del logo (x a la
        // derecha, y hacia arriba, en unidades de "alto de letra"), armada con
        // tramos rectos cortos que se van afinando hacia la punta.
        Color yellow = HexColor("#f7b500");
        Vector2 p0 = new Vector2(0.05f, 0.95f);
        Vector2 p1 = new Vector2(0.8f, 1.7f);
        Vector2 p2 = new Vector2(1.95f, 1.15f);
        const int segments = 12;

        for (int i = 0; i < segments; i++)
        {
            float t0 = i / (float)segments;
            float t1 = (i + 1) / (float)segments;
            Vector2 a = Bezier(p0, p1, p2, t0);
            Vector2 b = Bezier(p0, p1, p2, t1);
            Vector2 mid = (a + b) * 0.5f;
            Vector2 dir = b - a;

            float thickness = Mathf.Lerp(0.15f, 0.06f, (t0 + t1) * 0.5f) * em;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            PrimitiveObject(parent, "Logo_Curva", PrimitiveType.Cube,
                anchor + right * (mid.x * em) + Vector3.up * (mid.y * em) + outward * 0.03f,
                new Vector3(dir.magnitude * em * 1.18f, thickness, 0.05f), yellow,
                textRotation * Quaternion.Euler(0f, 0f, angle));
        }
    }

    private static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    private static TextMeshPro MakeLogoText(Transform parent, string name, string text, float fontSize, Color color,
        Vector3 position, Quaternion rotation, Vector2 pivot, TextAlignmentOptions alignment, float em)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.rotation = rotation;

        TextMeshPro label = go.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        if (defaultFont != null) label.font = defaultFont;

        label.rectTransform.pivot = pivot;
        label.rectTransform.sizeDelta = new Vector2(6f * em, 1.4f * em);
        return label;
    }

    // ---------------------------------------------------------------
    // Peatones
    // ---------------------------------------------------------------
    // Gente caminando por las veredas: por ahora cápsulas con poncho y
    // sombrero (como los vecinos); cuando lleguen los modelos de personas se
    // cambia el cuerpo y PedestrianWalker los sigue moviendo igual.
    //
    // Cada recorrido va de esquina a esquina DENTRO de una cuadra, sin cruzar
    // la Av. Giráldez: por ahí pasan el bus y la combi, y el tránsito no
    // frena por los peatones (solo por Kuntur).
    private static void BuildPedestrians(Transform parent)
    {
        GameObject root = new GameObject("Peatones");
        root.transform.SetParent(parent);

        var routes = new (Vector3 a, Vector3 b)[]
        {
            (new Vector3(4.8f, 0f, -65f),   new Vector3(4.8f, 0f, -52f)),
            (new Vector3(-4.8f, 0f, -65f),  new Vector3(-4.8f, 0f, -52f)),
            (new Vector3(-4.8f, 0f, -38.5f), new Vector3(-4.8f, 0f, -24.5f)),
            (new Vector3(4.8f, 0f, -11.5f), new Vector3(4.8f, 0f, 7.5f)),
            (new Vector3(-4.8f, 0f, 38f),   new Vector3(-4.8f, 0f, 51f)),   // al costado del Plaza Vea
            (new Vector3(-34f, 0f, 34.8f),  new Vector3(-9f, 0f, 34.8f)),   // frente al Plaza Vea
            (new Vector3(9f, 0f, 34.8f),    new Vector3(27f, 0f, 34.8f)),
            (new Vector3(9f, 0f, 25.2f),    new Vector3(27f, 0f, 25.2f)),
            (new Vector3(-34f, 0f, 25.2f),  new Vector3(-9f, 0f, 25.2f)),
            (new Vector3(4.8f, 0f, 64.5f),  new Vector3(4.8f, 0f, 75f)),
            (new Vector3(8f, 0f, 17.6f),    new Vector3(25f, 0f, 17.6f)),
            (new Vector3(-34f, 0f, -14.3f), new Vector3(-9f, 0f, -14.3f)),
            // Más gente: veredas de Puno, Cusco, Junín y Ferrocarril, cada
            // tramo dentro de una sola cuadra para no cruzar por la pista.
            (new Vector3(-37.4f, 0f, -60f), new Vector3(-37.4f, 0f, -50f)),
            (new Vector3(-37.4f, 0f, -12f), new Vector3(-37.4f, 0f, 8f)),
            (new Vector3(29.4f, 0f, -40f),  new Vector3(29.4f, 0f, -23f)),
            (new Vector3(29.4f, 0f, 37f),   new Vector3(29.4f, 0f, 53f)),
            (new Vector3(-30f, 0f, -40.4f), new Vector3(-10f, 0f, -40.4f)),
            (new Vector3(10f, 0f, 62.6f),   new Vector3(28f, 0f, 62.6f)),
            // Barrios de la ampliación (Jr. Arequipa, Huánuco y Amazonas).
            (new Vector3(-75.4f, 0f, -62f), new Vector3(-75.4f, 0f, -50f)),
            (new Vector3(-75.4f, 0f, 36f),  new Vector3(-75.4f, 0f, 52f)),
            (new Vector3(-60f, 0f, -95.4f), new Vector3(-48f, 0f, -95.4f)),
            (new Vector3(10f, 0f, 90.6f),   new Vector3(26f, 0f, 90.6f)),
            (new Vector3(-4.8f, 0f, -110f), new Vector3(-4.8f, 0f, -106f)),
            (new Vector3(4.8f, 0f, 96f),    new Vector3(4.8f, 0f, 108f)),
        };

        Color[] clothes =
        {
            HexColor("#c0392b"), HexColor("#2471a3"), HexColor("#7d3c98"), HexColor("#148f77"),
            HexColor("#d35400"), HexColor("#6e4b3a"), HexColor("#b7950b"), HexColor("#34495e"),
        };
        Color[] hats = { HexColor("#3f3630"), HexColor("#8a5a3b"), HexColor("#1f1f1f"), HexColor("#c9b28f") };
        Color[] skins = { HexColor("#c58c68"), HexColor("#a8704f"), HexColor("#d7a07e"), HexColor("#8d5a3c") };

        System.Random rng = new System.Random(4242);

        for (int i = 0; i < routes.Length; i++)
        {
            // Persona de verdad (City People) si el asset está.
            if (TryBuildAssetPedestrian(root.transform, $"Peaton_{i + 1}", routes[i].a, routes[i].b, rng)) continue;

            GameObject walker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            walker.name = $"Peaton_{i + 1}";
            walker.transform.SetParent(root.transform);
            walker.transform.position = routes[i].a + Vector3.up;
            walker.transform.localScale = new Vector3(0.74f, 0.9f, 0.74f);
            Object.DestroyImmediate(walker.GetComponent<Collider>());
            SetColor(walker, HexColor("#5b4a3d"));

            DecorateNPC(walker, clothes[rng.Next(clothes.Length)], hats[rng.Next(hats.Length)], skins[rng.Next(skins.Length)]);
            foreach (Transform child in walker.transform) child.gameObject.isStatic = false;
            walker.isStatic = false;

            PedestrianWalker legs = walker.AddComponent<PedestrianWalker>();
            SerializedObject so = new SerializedObject(legs);
            so.FindProperty("pointA").vector3Value = routes[i].a;
            so.FindProperty("pointB").vector3Value = routes[i].b;
            so.FindProperty("speed").floatValue = (float)(1.0 + rng.NextDouble() * 0.45);
            so.FindProperty("pauseAtEnds").floatValue = (float)(1.2 + rng.NextDouble() * 2.0);
            so.FindProperty("startProgress").floatValue = (float)rng.NextDouble();
            so.ApplyModifiedProperties();
        }
    }

    private static void BuildVacantLot(Transform parent, Vector3 center)
    {
        float halfX = CleanupLotSizeX / 2f;
        float halfZ = CleanupLotSizeZ / 2f;

        PrimitiveObject(parent, "Lote_Piso", PrimitiveType.Cube,
            center + Vector3.up * 0.02f, new Vector3(CleanupLotSizeX, 0.04f, CleanupLotSizeZ), HexColor("#a8977c"));

        Color adobe = HexColor("#a87c58");
        Color adobeTop = HexColor("#8f6746");

        // Pared del fondo y las dos laterales; el frente queda abierto para
        // entrar caminando desde la Calle Real.
        PrimitiveObject(parent, "Lote_Cerco_Fondo", PrimitiveType.Cube,
            center + new Vector3(-halfX, 0.9f, 0f), new Vector3(0.35f, 1.8f, CleanupLotSizeZ), adobe);
        PrimitiveObject(parent, "Lote_Cerco_Norte", PrimitiveType.Cube,
            center + new Vector3(-1.5f, 0.9f, halfZ), new Vector3(CleanupLotSizeX - 3f, 1.8f, 0.35f), adobe);
        PrimitiveObject(parent, "Lote_Cerco_Sur", PrimitiveType.Cube,
            center + new Vector3(-1.5f, 0.75f, -halfZ), new Vector3(CleanupLotSizeX - 3f, 1.5f, 0.35f), adobe);

        // Remate de las paredes, del color de la teja.
        PrimitiveObject(parent, "Lote_Cerco_Remate", PrimitiveType.Cube,
            center + new Vector3(-halfX, 1.85f, 0f), new Vector3(0.5f, 0.12f, CleanupLotSizeZ), adobeTop);

        // Cartel pintado a mano, de los que nadie respeta. Va en el filo del
        // lote que da a la calle y MIRANDO a la calle (+X), que es por donde
        // llega el jugador: la placa es delgada en X, no en Z.
        PrimitiveObject(parent, "Lote_Cartel_Poste", PrimitiveType.Cylinder,
            center + new Vector3(halfX - 0.6f, 1f, halfZ - 2f), new Vector3(0.09f, 1f, 0.09f), HexColor("#7c6a55"));
        GameObject board = PrimitiveObject(parent, "Lote_Cartel", PrimitiveType.Cube,
            center + new Vector3(halfX - 0.6f, 2.1f, halfZ - 2f), new Vector3(0.08f, 0.9f, 2.4f), HexColor("#f2efe6"));

        // ESTA era la franja roja que cruzaba medio pueblo.
        //
        // El texto estaba colgado de la placa, y la placa tiene escala
        // (0.08, 0.9, 2.4). Al colgarlo, Unity le compensó la escala con
        // (12.5, 1.1, 0.4) para que no cambiara de tamaño; pero después se lo
        // giró 90°, y ese 12.5 que era para el eje flaco quedó apuntando al eje
        // largo de la placa: 12.5 x 2.4 = 30 veces más ancho. Las letras rojas
        // de "PROHIBIDO ARROJAR BASURA" terminaban estiradas a unos 70 metros,
        // y de lejos eso se ve como una raya.
        //
        // Regla: un texto nunca va colgado de una pieza con escala desigual.
        // Va del objeto padre, que no tiene escala.
        GameObject textGO = new GameObject("Lote_Cartel_Texto");
        textGO.transform.SetParent(parent);
        textGO.transform.position = board.transform.position + new Vector3(0.07f, 0f, 0f);
        // -90° y no 90°: el lado que se lee de un texto de TextMeshPro es su
        // -Z local. Con -90° ese lado queda mirando al +X, hacia la calle.
        textGO.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

        TextMeshPro label = textGO.AddComponent<TextMeshPro>();
        label.text = "PROHIBIDO\nARROJAR BASURA";
        label.fontSize = 1.4f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = HexColor("#b03a2e");
        if (defaultFont != null) label.font = defaultFont;
        label.rectTransform.sizeDelta = new Vector2(2.3f, 0.85f);
    }

    // Kit de muestreo: un frasco de vidrio con agua del río adentro, parado en
    // la orilla. Se recoge con F (ver WaterSampleKit.GetInteractKey).
    private static void BuildWaterSample(Transform parent, int index, Vector3 groundPosition, int interactableLayer)
    {
        GameObject sample = new GameObject($"MuestraAgua_{index + 1}");
        sample.transform.SetParent(parent);
        sample.transform.position = groundPosition;
        sample.layer = interactableLayer;

        CapsuleCollider collider = sample.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.height = 1.2f;
        collider.radius = 0.4f;
        collider.center = new Vector3(0f, 0.5f, 0f);

        PrimitiveObject(sample.transform, "Frasco", PrimitiveType.Cylinder,
            groundPosition + new Vector3(0f, 0.34f, 0f), new Vector3(0.28f, 0.34f, 0.28f), HexColor("#dff4fc"));
        PrimitiveObject(sample.transform, "Agua", PrimitiveType.Cylinder,
            groundPosition + new Vector3(0f, 0.26f, 0f), new Vector3(0.25f, 0.24f, 0.25f), UIPalette_Blue());
        PrimitiveObject(sample.transform, "Tapa", PrimitiveType.Cylinder,
            groundPosition + new Vector3(0f, 0.73f, 0f), new Vector3(0.3f, 0.07f, 0.3f), UIPalette_Gold());

        foreach (Transform child in sample.transform) child.gameObject.isStatic = false;

        sample.AddComponent<WaterSampleKit>();

        CollectibleGlow glow = sample.AddComponent<CollectibleGlow>();
        SerializedObject glowSo = new SerializedObject(glow);
        glowSo.FindProperty("glowColor").colorValue = UIPalette_Blue();
        glowSo.ApplyModifiedProperties();

        sample.transform.localScale = Vector3.one * 1.3f;
        AddMapIcon(sample.transform, groundPosition, UIPalette_Blue(), 3f);
    }

    private static void SetDialogueOption(SerializedProperty element, string text, bool correct, string response)
    {
        element.FindPropertyRelative("text").stringValue = text;
        element.FindPropertyRelative("isCorrect").boolValue = correct;
        element.FindPropertyRelative("npcResponse").stringValue = response;
    }

    private static void BuildEventSystem()
    {
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    // ---------------------------------------------------------------
    // Canvas de HUD + Diálogo + Resultado
    // ---------------------------------------------------------------

    private static void BuildHudAndDialogueCanvas(Camera playerCamera, InputActionAsset controls)
    {
        GameObject canvasGO = new GameObject("HUD_Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        Transform root = canvasGO.transform;

        Sprite frameSprite = UISpriteFactory.RoundedPanel("UI_PanelMarco", Color.clear, Color.white, 14f, 3f);
        Sprite heartSprite = UISpriteFactory.Heart("UI_Corazon_V2", Color.white);
        // Estrella y no moneda: el contador cuenta las estrellas ganadas al
        // cumplir cada misión.
        Sprite coinSprite = UISpriteFactory.Star("UI_EstrellaHUD", HexColor("#f5c400"));
        Sprite arrowSprite = UISpriteFactory.Arrow("UI_FlechaJugador_V2", HexColor("#f5c400"), HexColor("#3a2a00"));

        // ================= ESQUINA SUPERIOR IZQUIERDA =================
        // Minimapa + reloj + barra de Salud del Valle, igual que el mockup.

        // ---- Minimapa ----
        RenderTexture mapTexture = GetMapRenderTexture();
        Camera mapCamera = BuildMapCamera(mapTexture);

        Image minimapPanel = MakeRoundedPanel(root, "Panel_Minimapa", UIPalette_PanelDark());
        SetRect(minimapPanel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(200, 200));

        GameObject minimapImageGO = new GameObject("Minimapa_Render", typeof(RectTransform));
        minimapImageGO.transform.SetParent(minimapPanel.transform, false);
        RawImage minimapRaw = minimapImageGO.AddComponent<RawImage>();
        minimapRaw.texture = mapTexture;
        SetRect(minimapRaw.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12, -12));

        Image minimapFrame = MakeIcon(minimapPanel.transform, "Minimapa_Marco", frameSprite, UIPalette_Cream());
        StretchFull(minimapFrame.rectTransform);
        minimapFrame.type = Image.Type.Sliced;
        minimapFrame.raycastTarget = false;

        Image minimapArrow = MakeIcon(minimapPanel.transform, "Minimapa_Flecha", arrowSprite, Color.white);
        SetRect(minimapArrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));

        // ---- Reloj (hora del día) ----
        // Pasó de la esquina de arriba (24 h, "18:45") a la de abajo a la
        // derecha, en 12 horas con AM/PM y un sol o una luna.
        BuildHudClock(root);

        // ---- Salud del Valle (v55: animada, ver HealthBarFx) ----
        Image healthPanel = MakeRoundedPanel(root, "Panel_SaludDelValle", new Color(0f, 0f, 0f, 0.62f));
        SetRect(healthPanel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(226, -16), new Vector2(282, 70));

        // Halo del color de la salud que late detrás del corazón.
        Image heartGlow = MakeIcon(healthPanel.transform, "Halo_Corazon", UISpriteFactory.SoftGlow("UI_Brillo", Color.white), new Color(1f, 1f, 1f, 0f));
        SetRect(heartGlow.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(27, -24), new Vector2(64, 64));
        Image heartIcon = MakeIcon(healthPanel.transform, "Icono_Corazon", heartSprite, UIPalette_Green());
        SetRect(heartIcon.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(27, -24), new Vector2(28, 28));

        TextMeshProUGUI healthLabel = MakeText(healthPanel.transform, "Text_Label", "SALUD DEL VALLE", 13f, UIPalette_Cream(), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        SetRect(healthLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(50, -11), new Vector2(-110, 18));
        healthLabel.characterSpacing = 4f;

        TextMeshProUGUI healthPercent = MakeText(healthPanel.transform, "Text_Porcentaje", "35%", 20f, UIPalette_Gold(), TextAlignmentOptions.TopRight, FontStyles.Bold);
        SetRect(healthPercent.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -6), new Vector2(80, 26));

        Image healthBg = MakeRoundedPanel(healthPanel.transform, "HealthBar_BG", new Color(1f, 1f, 1f, 0.1f));
        SetRect(healthBg.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(50, -34), new Vector2(-62, 12));
        healthBg.gameObject.AddComponent<RectMask2D>();

        Image healthTrail = MakeRoundedPanel(healthBg.transform, "HealthBar_Estela", new Color(1f, 1f, 1f, 0.35f));
        healthTrail.type = Image.Type.Filled;
        healthTrail.fillMethod = Image.FillMethod.Horizontal;
        healthTrail.fillAmount = 0f;
        StretchFull(healthTrail.rectTransform);

        Image healthFill = MakeRoundedPanel(healthBg.transform, "HealthBar_Fill", UIPalette_Green());
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillAmount = 0f; // el valle arranca contaminado: barra vacía
        StretchFull(healthFill.rectTransform);

        // Brillo que cruza la barra (una franja blanca inclinada).
        Image healthShine = MakeIcon(healthBg.transform, "Brillo", UISpriteFactory.SoftGlow("UI_Brillo", Color.white), new Color(1f, 1f, 1f, 0.55f));
        SetRect(healthShine.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 30));
        healthShine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f);

        TextMeshProUGUI healthStatus = MakeText(healthPanel.transform, "Text_Estado", "Valle en Riesgo", 12f, UIPalette_TextMuted(), TextAlignmentOptions.TopLeft);
        SetRect(healthStatus.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(50, -49), new Vector2(-62, 16));

        HealthBarFx healthFx = healthPanel.gameObject.AddComponent<HealthBarFx>();
        SerializedObject healthFxSo = new SerializedObject(healthFx);
        healthFxSo.FindProperty("fill").objectReferenceValue = healthFill;
        healthFxSo.FindProperty("trail").objectReferenceValue = healthTrail;
        healthFxSo.FindProperty("shine").objectReferenceValue = healthShine.rectTransform;
        healthFxSo.FindProperty("heart").objectReferenceValue = heartIcon.rectTransform;
        healthFxSo.FindProperty("heartImage").objectReferenceValue = heartIcon;
        healthFxSo.FindProperty("glow").objectReferenceValue = heartGlow;
        healthFxSo.FindProperty("percentText").objectReferenceValue = healthPercent;
        healthFxSo.FindProperty("statusText").objectReferenceValue = healthStatus;
        healthFxSo.ApplyModifiedProperties();

        // ================= ESQUINA SUPERIOR DERECHA =================

        // ---- Puntaje con moneda dorada ----
        Image scorePanel = MakeRoundedPanel(root, "Panel_Puntaje", UIPalette_PanelDark());
        SetRect(scorePanel.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(132, 46));

        Image coinIcon = MakeIcon(scorePanel.transform, "Icono_Moneda", coinSprite, Color.white);
        SetRect(coinIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(26, 26));

        TextMeshProUGUI scoreText = MakeText(scorePanel.transform, "Text_Score", "0", 24f, UIPalette_Gold(), TextAlignmentOptions.Right, FontStyles.Bold);
        SetRect(scoreText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-6, 0), new Vector2(-56, 0));

        // ---- Misiones (v55: franja negra, solo texto, se ajusta a su contenido) ----
        Image missionsPanel = MakeRoundedPanel(root, "Panel_Misiones", new Color(0f, 0f, 0f, 0.6f));
        SetRect(missionsPanel.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -72), new Vector2(320, 60));
        VerticalLayoutGroup panelLayout = missionsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(14, 14, 10, 12);
        panelLayout.spacing = 6f;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;
        ContentSizeFitter panelFitter = missionsPanel.gameObject.AddComponent<ContentSizeFitter>();
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        missionsPanel.gameObject.AddComponent<CanvasGroup>();
        missionsPanel.gameObject.AddComponent<UIPopIn>();

        TextMeshProUGUI missionsTitle = MakeText(missionsPanel.transform, "Text_TituloMisiones", "MISIÓN: CUIDADO AMBIENTAL", 12f, UIPalette_Gold(), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        missionsTitle.characterSpacing = 3f;
        LayoutElement titleLe = missionsTitle.gameObject.AddComponent<LayoutElement>();
        titleLe.preferredHeight = 18f;

        Image missionsRule = MakePanel(missionsPanel.transform, "Linea", new Color(0.96f, 0.77f, 0f, 0.5f));
        LayoutElement ruleLe = missionsRule.gameObject.AddComponent<LayoutElement>();
        ruleLe.preferredHeight = 1f;

        GameObject content = new GameObject("Content_Objetivos", typeof(RectTransform));
        content.transform.SetParent(missionsPanel.transform, false);
        RectTransform contentRt = content.GetComponent<RectTransform>();
        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 5f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperLeft;

        // ---- Tiempo de la misión (arriba al centro) ----
        // Reloj + "TIEMPO PARA LIMPIAR" + los minutos grandes + una barra que
        // se va vaciando; los colores los cambia HUDController.
        Image countdownPanel = MakeRoundedPanel(root, "Panel_TiempoMision", UIPalette_PanelDark());
        SetRect(countdownPanel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(250, 72));
        Image countdownFrame = MakeIcon(countdownPanel.transform, "Marco", frameSprite, new Color(1f, 1f, 1f, 0.25f));
        StretchFull(countdownFrame.rectTransform);
        countdownFrame.type = Image.Type.Sliced;
        Sprite clockCircle = UISpriteFactory.Circle("UI_Circulo", Color.white, Color.clear, 0f);
        Image clockFace = MakeIcon(countdownPanel.transform, "Icono_Reloj", clockCircle, UIPalette_Cream());
        SetRect(clockFace.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 4), new Vector2(34, 34));
        Image clockInner = MakeIcon(clockFace.transform, "Centro", clockCircle, UIPalette_PanelDark());
        SetRect(clockInner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));
        Image handA = MakePanel(clockFace.transform, "Aguja_Min", UIPalette_Cream());
        SetRect(handA.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(3, 11));
        Image handB = MakePanel(clockFace.transform, "Aguja_Hora", UIPalette_Cream());
        SetRect(handB.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(3, 8));
        handB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        TextMeshProUGUI countdownLabel = MakeText(countdownPanel.transform, "Text_Label", "TIEMPO PARA LIMPIAR", 11f, UIPalette_TextMuted(), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        SetRect(countdownLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -9), new Vector2(-72, 16));
        TextMeshProUGUI countdownText = MakeText(countdownPanel.transform, "Text_Countdown", "05:00", 32f, UIPalette_Cream(), TextAlignmentOptions.Left, FontStyles.Bold);
        SetRect(countdownText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -22), new Vector2(-72, 34));
        countdownText.textWrappingMode = TextWrappingModes.NoWrap;
        Image countdownBarBg = MakeRoundedPanel(countdownPanel.transform, "Barra_Fondo", new Color(0f, 0f, 0f, 0.45f));
        SetRect(countdownBarBg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 9), new Vector2(-28, 8));
        Image countdownFill = MakeRoundedPanel(countdownBarBg.transform, "Barra_Relleno", UIPalette_Green());
        countdownFill.type = Image.Type.Filled;
        countdownFill.fillMethod = Image.FillMethod.Horizontal;
        countdownFill.fillAmount = 1f;
        StretchFull(countdownFill.rectTransform);
        countdownPanel.gameObject.SetActive(false);

        // ---- Ayuda de teclas ----
        TextMeshProUGUI mapHint = MakeText(root, "Text_AyudaMapa", "[M] Mapa   [I] Mochila   [Z] Cámara   [V] Ver a Kuntur   [P] Pausa", 13f, UIPalette_TextMuted(), TextAlignmentOptions.BottomLeft);
        SetRect(mapHint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(18, 16), new Vector2(420, 22));
        mapHint.fontSize = 12f;

        // ---- Mochila de residuos (tecla I) ----
        BuildInventoryPanel(root);

        // ---- Mapa completo (tecla M) ----
        (GameObject fullMapPanel, RectTransform fullMapImage, RectTransform fullMapArrow, Image mapMarkerTemplate,
            TextMeshProUGUI streetLabelTemplate) = BuildFullMapPanel(root, mapTexture, frameSprite, arrowSprite);

        // ---- Popup_Anchor ----
        RectTransform popupAnchor = MakeRect(root, "Popup_Anchor");
        SetRect(popupAnchor, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(300, 120));

        // ---- Prompt_Primary / Prompt_Secondary ----
        (GameObject primaryGO, TextMeshProUGUI primaryText) = BuildPrompt(root, "Prompt_Primary", "E", "Recoger basura",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 66), UIPalette_Amber());
        (GameObject secondaryGO, TextMeshProUGUI secondaryText) = BuildPrompt(root, "Prompt_Secondary", "F", "Tomar muestra de agua",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 128), UIPalette_Blue());
        primaryGO.SetActive(false);
        secondaryGO.SetActive(false);

        // ---- Plantillas (no visibles, se clonan por script) ----
        ObjectiveRowUI rowTemplate = BuildObjectiveRowTemplate(root);
        ScorePopup popupTemplate = BuildScorePopupTemplate(root);

        // ---- HUDController ----
        GameObject hudControllerGO = new GameObject("HUDController");
        hudControllerGO.transform.SetParent(root, false);
        HUDController hud = hudControllerGO.AddComponent<HUDController>();
        SerializedObject hudSo = new SerializedObject(hud);
        hudSo.FindProperty("healthFillImage").objectReferenceValue = healthFill;
        hudSo.FindProperty("healthPercentText").objectReferenceValue = healthPercent;
        hudSo.FindProperty("scoreText").objectReferenceValue = scoreText;
        hudSo.FindProperty("scorePopupPrefab").objectReferenceValue = popupTemplate;
        hudSo.FindProperty("popupSpawnArea").objectReferenceValue = popupAnchor;
        hudSo.FindProperty("objectiveListParent").objectReferenceValue = contentRt;
        hudSo.FindProperty("objectiveRowPrefab").objectReferenceValue = rowTemplate;
        hudSo.FindProperty("promptPrimary").objectReferenceValue = primaryGO;
        hudSo.FindProperty("promptPrimaryText").objectReferenceValue = primaryText;
        hudSo.FindProperty("promptSecondary").objectReferenceValue = secondaryGO;
        hudSo.FindProperty("promptSecondaryText").objectReferenceValue = secondaryText;
        hudSo.FindProperty("interactionSystem").objectReferenceValue = playerCamera.GetComponent<InteractionSystem>();
        hudSo.FindProperty("healthStatusText").objectReferenceValue = healthStatus;
        hudSo.FindProperty("healthFx").objectReferenceValue = healthFx;
        hudSo.FindProperty("countdownPanel").objectReferenceValue = countdownPanel.gameObject;
        hudSo.FindProperty("countdownText").objectReferenceValue = countdownText;
        hudSo.FindProperty("countdownFill").objectReferenceValue = countdownFill;
        hudSo.FindProperty("countdownLabel").objectReferenceValue = countdownLabel;
        hudSo.FindProperty("countdown").objectReferenceValue = Object.FindAnyObjectByType<CountdownManager>();
        hudSo.ApplyModifiedProperties();

        // ---- MapSystem (minimapa + mapa completo con M) ----
        GameObject playerGO = GameObject.Find("Player");
        GameObject mapSystemGO = new GameObject("MapSystem");
        mapSystemGO.transform.SetParent(root, false);
        MapSystem mapSystem = mapSystemGO.AddComponent<MapSystem>();
        SerializedObject mapSo = new SerializedObject(mapSystem);
        mapSo.FindProperty("mapCamera").objectReferenceValue = mapCamera;
        mapSo.FindProperty("player").objectReferenceValue = playerGO != null ? playerGO.transform : null;
        mapSo.FindProperty("viewDirection").objectReferenceValue = playerCamera != null ? playerCamera.transform : null;
        // Minimapa giratorio: arriba es hacia donde mira la cámara.
        mapSo.FindProperty("minimapFollowsHeading").boolValue = true;
        mapSo.FindProperty("minimapPanel").objectReferenceValue = minimapPanel.gameObject;
        mapSo.FindProperty("minimapArrow").objectReferenceValue = minimapArrow.rectTransform;
        mapSo.FindProperty("fullMapPanel").objectReferenceValue = fullMapPanel;
        mapSo.FindProperty("fullMapImage").objectReferenceValue = fullMapImage;
        mapSo.FindProperty("fullMapArrow").objectReferenceValue = fullMapArrow;
        mapSo.FindProperty("markerTemplate").objectReferenceValue = mapMarkerTemplate;
        mapSo.FindProperty("fullMapZoom").floatValue = 150f;   // el pueblo entero cabe en pantalla
        mapSo.FindProperty("maxZoom").floatValue = 260f;       // alejando se ve el río de punta a punta
        mapSo.FindProperty("worldLimit").vector2Value = new Vector2(230f, 280f);
        mapSo.FindProperty("minimapZoom").floatValue = 34f;
        mapSo.FindProperty("worldCenter").vector2Value = new Vector2(0f, -5f);
        mapSo.FindProperty("streetLabelTemplate").objectReferenceValue = streetLabelTemplate;
        FillStreetLabels(mapSo.FindProperty("streets"));
        mapSo.ApplyModifiedProperties();

        BuildDialoguePanel(root);
        GameObject resultPanel = BuildResultPanel(root);

        // ---- ClimaxDirector: jornada -> clímax -> resultado ----
        GameObject managersGO = GameObject.Find("--- MANAGERS ---");
        if (managersGO != null)
        {
            ClimaxDirector director = managersGO.AddComponent<ClimaxDirector>();
            SerializedObject dirSo = new SerializedObject(director);
            dirSo.FindProperty("countdown").objectReferenceValue = managersGO.GetComponent<CountdownManager>();
            dirSo.FindProperty("resultPanel").objectReferenceValue = resultPanel;
            dirSo.ApplyModifiedProperties();

            // Misiones por nivel, estrellas y la ruta morada del mapa.
            BuildMissionSystem(root, mapCamera, playerCamera, managersGO, director);
            // Días: casa, descansar a las 9 de la noche y guardado.
            BuildDaySystem(root, managersGO);
        }

        // v55b: tutorial con Kuntur saludando (después de la intro).
        BuildTutorialPanel(root);
        BuildPauseMenu(root);
    }

    // Reloj de la esquina inferior derecha: "8:45" grande, "AM" al lado, el
    // momento del día debajo ("Mañana", "Tarde"...) y un sol que de noche se
    // vuelve luna (un segundo círculo del color del panel le "muerde" un
    // costado y deja la media luna).
    private static void BuildHudClock(Transform root)
    {
        Image panel = MakeRoundedPanel(root, "Panel_Reloj", UIPalette_PanelDark());
        SetRect(panel.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-16f, 16f), new Vector2(200f, 62f));

        Sprite circle = UISpriteFactory.Circle("UI_Circulo", Color.white, Color.clear, 0f);

        Image icon = MakeIcon(panel.transform, "Icono_DiaNoche", circle, HexColor("#ffcc33"));
        SetRect(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(14f, 0f), new Vector2(30f, 30f));

        Image moonShadow = MakeIcon(icon.transform, "Sombra_Luna", circle, UIPalette_PanelDark());
        SetRect(moonShadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(8f, 6f), new Vector2(26f, 26f));
        moonShadow.enabled = false;

        TextMeshProUGUI time = MakeText(panel.transform, "Text_Hora", "8:00", 30f, UIPalette_Cream(),
            TextAlignmentOptions.Right, FontStyles.Bold);
        SetRect(time.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(1, 0.5f),
            new Vector2(138f, 1f), new Vector2(88f, 40f));
        time.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI period = MakeText(panel.transform, "Text_AmPm", "AM", 18f, UIPalette_Amber(),
            TextAlignmentOptions.Left, FontStyles.Bold);
        SetRect(period.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(142f, 9f), new Vector2(52f, 24f));

        TextMeshProUGUI moment = MakeText(panel.transform, "Text_Momento", "Mañana", 12f, UIPalette_TextMuted(),
            TextAlignmentOptions.Left);
        SetRect(moment.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(142f, -12f), new Vector2(58f, 18f));
        moment.textWrappingMode = TextWrappingModes.NoWrap;

        HudClock clock = panel.gameObject.AddComponent<HudClock>();
        SerializedObject so = new SerializedObject(clock);
        so.FindProperty("timeText").objectReferenceValue = time;
        so.FindProperty("periodText").objectReferenceValue = period;
        so.FindProperty("momentText").objectReferenceValue = moment;
        so.FindProperty("dayNightIcon").objectReferenceValue = icon;
        so.FindProperty("moonShadow").objectReferenceValue = moonShadow;
        so.ApplyModifiedProperties();
    }

    // Nombres de calle para el mapa. Salen de las MISMAS constantes del
    // trazado que usan los letreros de esquina, así que el mapa y la calle
    // nunca pueden decir cosas distintas.
    //
    // Cada calle tiene su propio nombre y ninguno se repite: son nueve calles
    // y nueve nombres. El nombre sí se repite a lo largo de UNA misma calle
    // (aparece en cada una de sus esquinas), que es justo lo que hace que
    // sirvan para ubicarse: caminando por la Calle Real, todos los letreros
    // que la nombran dicen "CALLE REAL".
    private static void FillStreetLabels(SerializedProperty list)
    {
        Color place = HexColor("#ffd56b");
        Color water = HexColor("#9fd8ff");
        Color none = new Color(0f, 0f, 0f, 0f);

        // (nombre, x, z, ángulo, color, tamaño). Ángulo 90 = se lee girado,
        // como las calles que corren norte-sur. Los LUGARES van en dorado y
        // se ven siempre; las calles, en blanco y solo con zoom.
        var entries = new (string name, float x, float z, float angle, Color color, float size)[]
        {
            ("JR. AREQUIPA",    ArequipaX,   -30f, 90f, none, 0f),
            ("JR. PUNO",        PunoX,       -30f, 90f, none, 0f),
            ("CALLE REAL",      MainStreetX,  -5f, 90f, none, 0f),
            ("JR. CUSCO",       CuscoX,      -30f, 90f, none, 0f),
            ("JR. HUÁNUCO",     -20f, CrossStreetsZ[5], 0f, none, 0f),
            ("JR. ANCASH",      -20f, CrossStreetsZ[0], 0f, none, 0f),
            ("JR. JUNÍN",       -20f, CrossStreetsZ[1], 0f, none, 0f),
            ("JR. LORETO",       12f, CrossStreetsZ[2], 0f, none, 0f),
            ("JR. AYACUCHO",     18f, CrossStreetsZ[3], 0f, none, 0f),
            ("AV. GIRÁLDEZ",     14f, AvenueZ,          0f, none, 0f),
            ("AV. FERROCARRIL",  -8f, CrossStreetsZ[4], 0f, none, 0f),
            ("JR. AMAZONAS",    -20f, CrossStreetsZ[6], 0f, none, 0f),
            ("CAMINO A LA CHACRA", EastRoadX, 10f, 90f, none, 0f),
            ("SUBIDA AL MIRADOR", MainStreetX, -168f, 90f, none, 0f),
            ("JR. MANTARO",      -20f, SouthRingZ,        0f, none, 0f),
            ("AV. HUANCAVELICA", -20f, NorthRingZ,        0f, none, 0f),
            ("CALLE DEL MIRADOR", -20f, TopRoadZ,         0f, none, 0f),

            ("PLAZA VEA",         -21f, 45f, 0f, place, 17f),
            ("PLAZA CONSTITUCIÓN", ParkCenter.x, ParkCenter.z, 0f, place, 15f),
            ("CATEDRAL", CathedralCenter.x, CathedralCenter.z, 0f, place, 12f),
            ("CASA DE KUNTUR",    HomeCenter.x, HomeCenter.z - 7f, 0f, HexColor("#ff9f43"), 14f),
            ("PUENTE",            RiverCenterX, AvenueZ + 7f, 0f, place, 14f),
            ("BODEGA DE DOÑA ROSA", 45f, 16f, 0f, place, 13f),
            ("MIRADOR DEL VALLE", 0f, TopRoadZ - 9f, 0f, place, 14f),
            ("PUENTE PEATONAL",   RiverX(FootbridgeZ), FootbridgeZ - 6f, 0f, place, 12f),
            ("TU TUK TUK",        TukTukSpot.x - 7f, TukTukSpot.z - 4f, 0f, HexColor("#ff9f43"), 11f),
            ("HELADERÍA", TownIceCreamSpot.x + 2f, TownIceCreamSpot.z + 6f, 0f, HexColor("#ff8fb8"), 12f),
            ("HELADERÍA", 14f, -196f, 0f, HexColor("#ff8fb8"), 12f),
            ("RÍO MANTARO",       RiverX(-20f) + 1f, -20f, 90f, water, 18f),
            ("RÍO MANTARO",       RiverX(170f), 170f, 90f, water, 18f),
            ("RÍO MANTARO",       RiverX(-170f), -170f, 90f, water, 18f),
        };

        list.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("name").stringValue = entries[i].name;
            entry.FindPropertyRelative("position").vector2Value = new Vector2(entries[i].x, entries[i].z);
            entry.FindPropertyRelative("angle").floatValue = entries[i].angle;
            entry.FindPropertyRelative("color").colorValue = entries[i].color;
            entry.FindPropertyRelative("fontSize").floatValue = entries[i].size;
        }
    }

    // Pantalla de pausa (tecla P). v55: más animada (la tarjeta rebota, las
    // filas entran escalonadas), sin créditos (están en el menú principal) y
    // con dos botones: VOLVER AL JUEGO e IR AL MENÚ.
    private static void BuildPauseMenu(Transform root)
    {
        Image backdrop = MakePanel(root, "Panel_Pausa", new Color(0f, 0f, 0f, 0.8f));
        StretchFull(backdrop.rectTransform);
        CanvasGroup group = backdrop.gameObject.AddComponent<CanvasGroup>();

        Image card = MakeRoundedPanel(backdrop.transform, "Card_Pausa", new Color(0.03f, 0.05f, 0.04f, 0.94f));
        SetRect(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(660f, 548f));
        Image frame = MakeIcon(card.transform, "Marco", UISpriteFactory.RoundedPanel("UI_PanelMarco", Color.clear, Color.white, 14f, 3f), new Color(0.96f, 0.77f, 0f, 0.55f));
        StretchFull(frame.rectTransform);
        frame.type = Image.Type.Sliced;

        TextMeshProUGUI title = MakeText(card.transform, "Text_Titulo", "PAUSA", 44f, UIPalette_Amber(),
            TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0f, -26f), new Vector2(-60f, 56f));
        title.characterSpacing = 16f;

        Image titleLine = MakePanel(card.transform, "Linea_Titulo", UIPalette_Gold());
        SetRect(titleLine.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -88f), new Vector2(180f, 2f));

        TextMeshProUGUI soundHeader = MakeText(card.transform, "Text_Sonido", "SONIDO", 16f, UIPalette_Gold(),
            TextAlignmentOptions.Left, FontStyles.Bold);
        SetRect(soundHeader.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0f, -108f), new Vector2(-90f, 24f));
        soundHeader.characterSpacing = 5f;
        soundHeader.gameObject.AddComponent<CanvasGroup>();

        // Tres controles separados a propósito: "bajar el sonido" no es una
        // sola cosa. Se puede dejar el río y los grillos y apagar la música.
        Slider musicSlider = MakeSettingSlider(card.transform, "Musica", "Música", -138f, out TextMeshProUGUI musicValue);
        Slider ambienceSlider = MakeSettingSlider(card.transform, "Ambiente", "Ambiente", -198f, out TextMeshProUGUI ambienceValue);
        Slider sfxSlider = MakeSettingSlider(card.transform, "Efectos", "Efectos", -258f, out TextMeshProUGUI sfxValue);
        foreach (string row in new[] { "Fila_Musica", "Fila_Ambiente", "Fila_Efectos" })
        {
            Transform r = card.transform.Find(row);
            r.gameObject.AddComponent<CanvasGroup>();
            // Tarjeta más angosta que la de Ajustes: etiqueta más corta y la
            // barra ocupa el resto.
            RectTransform labelRt = (RectTransform)r.Find("Text_Label");
            labelRt.sizeDelta = new Vector2(170f, 0f);
            labelRt.GetComponent<TextMeshProUGUI>().fontSize = 21f;
            RectTransform sliderRt = (RectTransform)r.Find("Slider");
            sliderRt.anchoredPosition = new Vector2(25f, 0f);
            sliderRt.sizeDelta = new Vector2(-330f, -14f);
        }

        // Botones: VOLVER AL JUEGO (principal) e IR AL MENÚ.
        Button resume = MakePauseButton(card.transform, "Boton_Volver", "VOLVER AL JUEGO", new Vector2(-150f, 50f), true);
        Button toMenu = MakePauseButton(card.transform, "Boton_Menu", "IR AL MENÚ", new Vector2(150f, 50f), false);
        // v56: el tuk tuk llega solo hasta donde está Kuntur.
        Button tukTuk = MakePauseButton(card.transform, "Boton_TukTuk", "PEDIR MI TUK TUK", new Vector2(0f, 124f), false);
        ((RectTransform)tukTuk.transform.parent).sizeDelta = new Vector2(570f, 58f);

        // Botoncito para volver a ver el tutorial.
        RectTransform tutHolder = MakeRect(card.transform, "Boton_Tutorial");
        SetRect(tutHolder, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18f, -18f), new Vector2(150f, 36f));
        Image tutBg = MakeRoundedPanel(tutHolder, "Fondo", new Color(1f, 1f, 1f, 0.1f));
        StretchFull(tutBg.rectTransform);
        Button tutButton = tutBg.gameObject.AddComponent<Button>();
        tutButton.targetGraphic = tutBg;
        ColorBlock tutColors = tutButton.colors;
        tutColors.highlightedColor = new Color(2.2f, 1.7f, 0.5f, 1f);
        tutButton.colors = tutColors;
        TextMeshProUGUI tutText = MakeText(tutBg.transform, "Text", "? TUTORIAL", 14f, UIPalette_Cream(), TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(tutText.rectTransform);
        tutText.characterSpacing = 3f;
        tutText.raycastTarget = false;

        TextMeshProUGUI note = MakeText(card.transform, "Text_Nota", "Tu avance se guarda al cumplir cada misión y al dormir.", 13f,
            new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Center);
        SetRect(note.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0f, 14f), new Vector2(-60f, 20f));

        PauseMenu pause = root.gameObject.AddComponent<PauseMenu>();
        SerializedObject so = new SerializedObject(pause);
        so.FindProperty("panel").objectReferenceValue = group;
        so.FindProperty("musicSlider").objectReferenceValue = musicSlider;
        so.FindProperty("musicValue").objectReferenceValue = musicValue;
        so.FindProperty("ambienceSlider").objectReferenceValue = ambienceSlider;
        so.FindProperty("ambienceValue").objectReferenceValue = ambienceValue;
        so.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
        so.FindProperty("sfxValue").objectReferenceValue = sfxValue;
        so.FindProperty("card").objectReferenceValue = card.rectTransform;
        so.FindProperty("title").objectReferenceValue = title;
        so.FindProperty("titleLine").objectReferenceValue = titleLine.rectTransform;
        RectTransform[] rows =
        {
            soundHeader.rectTransform,
            (RectTransform)card.transform.Find("Fila_Musica"),
            (RectTransform)card.transform.Find("Fila_Ambiente"),
            (RectTransform)card.transform.Find("Fila_Efectos"),
            (RectTransform)tukTuk.transform.parent,
            (RectTransform)resume.transform.parent,
            (RectTransform)toMenu.transform.parent,
        };
        SerializedProperty rowsProp = so.FindProperty("rows");
        rowsProp.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++) rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
        so.ApplyModifiedProperties();

        UnityEventTools.AddPersistentListener(resume.onClick, pause.Resume);
        UnityEventTools.AddPersistentListener(toMenu.onClick, pause.GoToMenu);
        UnityEventTools.AddPersistentListener(tukTuk.onClick, pause.RequestTukTuk);
        UnityEventTools.AddPersistentListener(tutButton.onClick, pause.OpenTutorial);
    }

    // Botón de la pausa: contenedor (lo mueve la entrada escalonada) + botón
    // (lo anima UIButtonHover al pasar el mouse).
    private static Button MakePauseButton(Transform card, string name, string label, Vector2 bottomOffset, bool primary)
    {
        RectTransform holder = MakeRect(card, name);
        SetRect(holder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), bottomOffset, new Vector2(270f, 58f));
        holder.gameObject.AddComponent<CanvasGroup>();

        Image bg = MakeRoundedPanel(holder, "Button", primary ? UIPalette_Amber() : new Color(1f, 1f, 1f, 0.1f));
        StretchFull(bg.rectTransform);
        Button button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        TextMeshProUGUI text = MakeText(bg.transform, "Text", label, 20f, primary ? HexColor("#2a1a05") : UIPalette_Cream(),
            TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(text.rectTransform);
        text.characterSpacing = 4f;
        text.raycastTarget = false;

        UIButtonHover hover = bg.gameObject.AddComponent<UIButtonHover>();
        SerializedObject hoverSo = new SerializedObject(hover);
        hoverSo.FindProperty("label").objectReferenceValue = text;
        hoverSo.FindProperty("hoverSlide").floatValue = 0f;
        hoverSo.FindProperty("hoverScale").floatValue = 1.06f;
        hoverSo.FindProperty("normalColor").colorValue = primary ? HexColor("#2a1a05") : UIPalette_Cream();
        hoverSo.FindProperty("hoverColor").colorValue = primary ? Color.black : UIPalette_Gold();
        hoverSo.ApplyModifiedProperties();
        return button;
    }

    // Mochila de Kuntur (tecla I). v55: se usa con el mouse (X o clic afuera
    // para cerrar, sin textos de teclas), Kuntur no gira mientras está
    // abierta y todo entra animado (ver InventoryUI).
    private static void BuildInventoryPanel(Transform root)
    {
        Image backdrop = MakePanel(root, "Panel_Inventario", new Color(0f, 0f, 0f, 0.7f));
        StretchFull(backdrop.rectTransform);
        // Clic afuera de la tarjeta = cerrar.
        Button backdropButton = backdrop.gameObject.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;

        Image card = MakeRoundedPanel(backdrop.transform, "Card_Inventario", new Color(0.03f, 0.05f, 0.04f, 0.95f));
        SetRect(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 360));
        // La tarjeta se come el clic (un botón que no hace nada): así solo
        // cierra el clic AFUERA, no el que cae sobre la mochila.
        card.raycastTarget = true;
        Button cardBlocker = card.gameObject.AddComponent<Button>();
        cardBlocker.transition = Selectable.Transition.None;

        Image frame = MakeIcon(card.transform, "Marco", UISpriteFactory.RoundedPanel("UI_PanelMarco", Color.clear, Color.white, 14f, 3f), new Color(0.96f, 0.77f, 0f, 0.55f));
        StretchFull(frame.rectTransform);
        frame.type = Image.Type.Sliced;

        TextMeshProUGUI title = MakeText(card.transform, "Text_Titulo", "MOCHILA DE RESIDUOS", 20f, UIPalette_Gold(), TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(-90, 28));
        title.characterSpacing = 6f;

        // Botón X arriba a la derecha.
        Image close = MakeRoundedPanel(card.transform, "Boton_Cerrar", new Color(1f, 1f, 1f, 0.1f));
        SetRect(close.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -14), new Vector2(38, 38));
        Button closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close;
        ColorBlock cb = closeButton.colors;
        cb.highlightedColor = new Color(2.2f, 1.6f, 0.4f, 1f);
        closeButton.colors = cb;
        TextMeshProUGUI closeX = MakeText(close.transform, "Text", "X", 20f, UIPalette_Cream(), TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(closeX.rectTransform);
        closeX.raycastTarget = false;

        (TextMeshProUGUI bottles, TextMeshProUGUI cans, TextMeshProUGUI papers, RectTransform[] slots, RectTransform[] icons) = BuildInventorySlots(card.transform);

        TextMeshProUGUI total = MakeText(card.transform, "Text_Total", "0 residuos recogidos", 14f, UIPalette_Cream(), TextAlignmentOptions.Left, FontStyles.Bold);
        SetRect(total.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(-70, 22));

        Image progressBg = MakeRoundedPanel(card.transform, "Progreso_Fondo", new Color(1f, 1f, 1f, 0.1f));
        SetRect(progressBg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(-70, 10));
        Image progressFill = MakeRoundedPanel(progressBg.transform, "Progreso", UIPalette_Green());
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillAmount = 0f;
        StretchFull(progressFill.rectTransform);

        TextMeshProUGUI remaining = MakeText(card.transform, "Text_Faltantes", "", 14f, UIPalette_Green(), TextAlignmentOptions.Center);
        SetRect(remaining.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(-30, 24));

        GameObject inventoryUiGO = new GameObject("InventoryUI");
        inventoryUiGO.transform.SetParent(root, false);
        InventoryUI inventoryUi = inventoryUiGO.AddComponent<InventoryUI>();
        SerializedObject so = new SerializedObject(inventoryUi);
        so.FindProperty("panel").objectReferenceValue = backdrop.gameObject;
        so.FindProperty("bottleCountText").objectReferenceValue = bottles;
        so.FindProperty("canCountText").objectReferenceValue = cans;
        so.FindProperty("paperCountText").objectReferenceValue = papers;
        so.FindProperty("remainingText").objectReferenceValue = remaining;
        so.FindProperty("card").objectReferenceValue = card.rectTransform;
        so.FindProperty("progressFill").objectReferenceValue = progressFill;
        so.FindProperty("totalText").objectReferenceValue = total;
        SerializedProperty slotsProp = so.FindProperty("slots");
        SerializedProperty iconsProp = so.FindProperty("icons");
        slotsProp.arraySize = slots.Length;
        iconsProp.arraySize = icons.Length;
        for (int i = 0; i < slots.Length; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            iconsProp.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
        }
        so.ApplyModifiedProperties();

        UnityEventTools.AddPersistentListener(closeButton.onClick, inventoryUi.Close);
        UnityEventTools.AddPersistentListener(backdropButton.onClick, inventoryUi.Close);

        backdrop.gameObject.SetActive(false);
    }

    private static (TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI, RectTransform[], RectTransform[]) BuildInventorySlots(Transform card)
    {
        string[] labels = { "BOTELLAS", "LATAS", "PAPEL Y BOLSAS" };
        Color[] colors = { HexColor("#7fd3f0"), HexColor("#c8ccd0"), HexColor("#f2e6c8") };
        TextMeshProUGUI[] counters = new TextMeshProUGUI[3];
        RectTransform[] slots = new RectTransform[3];
        RectTransform[] icons = new RectTransform[3];
        Sprite circle = UISpriteFactory.Circle("UI_Circulo", Color.white, Color.clear, 0f);
        Sprite glow = UISpriteFactory.SoftGlow("UI_Brillo", Color.white);

        for (int i = 0; i < 3; i++)
        {
            Image slot = MakeRoundedPanel(card, $"Slot_{i}", new Color(1f, 1f, 1f, 0.06f));
            SetRect(slot.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2((i - 1) * 168f, -62), new Vector2(154, 170));
            slot.gameObject.AddComponent<CanvasGroup>();
            slots[i] = slot.rectTransform;

            Image halo = MakeIcon(slot.transform, "Halo", glow, new Color(colors[i].r, colors[i].g, colors[i].b, 0.35f));
            SetRect(halo.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -44), new Vector2(96, 96));

            // Icono dibujado con piezas: botella, lata o bolsa.
            RectTransform icon = MakeRect(slot.transform, "Icono");
            SetRect(icon, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -44), new Vector2(50, 50));
            icons[i] = icon;
            if (i == 0)
            {
                Image body = MakeRoundedPanel(icon, "Botella", colors[i]);
                SetRect(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -5), new Vector2(22, 36));
                Image neck = MakePanel(icon, "Cuello", colors[i]);
                SetRect(neck.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 17), new Vector2(9, 10));
                Image cap = MakePanel(icon, "Tapa", HexColor("#2f7fd1"));
                SetRect(cap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 24), new Vector2(11, 5));
                Image tag = MakePanel(icon, "Etiqueta", new Color(1f, 1f, 1f, 0.7f));
                SetRect(tag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(22, 9));
            }
            else if (i == 1)
            {
                Image can = MakeRoundedPanel(icon, "Lata", colors[i]);
                SetRect(can.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28, 38));
                Image band = MakePanel(icon, "Franja", HexColor("#e2403a"));
                SetRect(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -2), new Vector2(28, 12));
                Image top = MakePanel(icon, "Borde", HexColor("#9aa0a6"));
                SetRect(top.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 18), new Vector2(24, 4));
            }
            else
            {
                Image bag = MakeIcon(icon, "Bolsa", circle, HexColor("#3d4a3f"));
                SetRect(bag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(40, 36));
                Image knot = MakeIcon(icon, "Nudo", circle, HexColor("#3d4a3f"));
                SetRect(knot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(14, 12));
                Image tie = MakePanel(icon, "Amarre", HexColor("#f5c400"));
                SetRect(tie.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 11), new Vector2(14, 3));
            }

            counters[i] = MakeText(slot.transform, "Text_Cantidad", "0", 34f, UIPalette_Cream(), TextAlignmentOptions.Center, FontStyles.Bold);
            SetRect(counters[i].rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -86), new Vector2(-16, 44));

            TextMeshProUGUI caption = MakeText(slot.transform, "Text_Nombre", labels[i], 12f, UIPalette_TextMuted(), TextAlignmentOptions.Center, FontStyles.Bold);
            SetRect(caption.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(-12, 20));
            caption.characterSpacing = 3f;
        }

        return (counters[0], counters[1], counters[2], slots, icons);
    }

    // Cámara cenital que alimenta tanto el minimapa como el mapa grande.
    // Renderiza a una RenderTexture, así que no aparece en pantalla por su
    // cuenta; MapSystem la mueve y le cambia el zoom según el modo.
    private static Camera BuildMapCamera(RenderTexture target)
    {
        GameObject mapCamGO = new GameObject("MapCamera");
        Camera mapCam = mapCamGO.AddComponent<Camera>();
        mapCam.orthographic = true;
        mapCam.orthographicSize = 28f;
        mapCam.clearFlags = CameraClearFlags.SolidColor;
        mapCam.backgroundColor = HexColor("#15241a");
        mapCam.cullingMask = ~0;
        mapCam.depth = -10;
        mapCam.targetTexture = target;
        mapCam.transform.position = new Vector3(0f, 60f, -10f);
        mapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return mapCam;
    }

    private static RenderTexture GetMapRenderTexture()
    {
        string dir = ArtDir + "/Generated";
        string path = dir + "/RT_Mapa.renderTexture";

        // 1024: con el pueblo más grande, a 512 las calles del mapa se veían
        // borrosas y los nombres flotaban sobre manchas.
        RenderTexture existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (existing != null && existing.width == 1024) return existing;
        if (existing != null) AssetDatabase.DeleteAsset(path);

        CreateFolderRecursive(dir);
        RenderTexture rt = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32)
        {
            name = "RT_Mapa",
            filterMode = FilterMode.Bilinear,
        };
        AssetDatabase.CreateAsset(rt, path);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
    }

    // Pantalla de mapa completo: se abre con M y muestra todo el valle con la
    // flecha del jugador, igual que la referencia del GDD.
    private static (GameObject, RectTransform, RectTransform, Image, TextMeshProUGUI) BuildFullMapPanel(Transform root, RenderTexture mapTexture, Sprite frameSprite, Sprite arrowSprite)
    {
        Image backdrop = MakePanel(root, "Panel_MapaCompleto", new Color(0f, 0f, 0f, 0.82f));
        StretchFull(backdrop.rectTransform);

        // Mapa grande y abierto, ocupando casi toda la pantalla: la idea es
        // ver el valle entero de un vistazo, no espiarlo por una ventanita.
        Image card = MakeRoundedPanel(backdrop.transform, "Mapa_Marco", UIPalette_PanelDark());
        SetRect(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(860, 1000));

        TextMeshProUGUI title = MakeText(card.transform, "Text_TituloMapa", "MAPA DEL VALLE", 24f, UIPalette_Gold(), TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(-24, 32));

        GameObject mapImageGO = new GameObject("Mapa_Render", typeof(RectTransform));
        mapImageGO.transform.SetParent(card.transform, false);
        RawImage mapRaw = mapImageGO.AddComponent<RawImage>();
        mapRaw.texture = mapTexture;
        // Cuadrado, igual que la cámara: si no, el mapa se ve estirado.
        SetRect(mapRaw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(800, 800));

        Image frame = MakeIcon(mapImageGO.transform, "Mapa_Borde", frameSprite, UIPalette_Cream());
        StretchFull(frame.rectTransform);
        frame.type = Image.Type.Sliced;
        frame.raycastTarget = false;

        // Plantilla del marcador de basura: MapSystem la clona una vez por
        // residuo y la va apagando conforme Kuntur los recoge.
        Image markerTemplate = MakeIcon(mapImageGO.transform, "Marcador_Basura", UISpriteFactory.Circle("UI_MarcadorMapa", Color.white, new Color(0f, 0f, 0f, 0.55f), 2.5f), UIPalette_Green());
        SetRect(markerTemplate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12));
        markerTemplate.gameObject.SetActive(false);

        Image arrow = MakeIcon(mapImageGO.transform, "Mapa_Flecha", arrowSprite, Color.white);
        SetRect(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));

        // Plantilla del nombre de calle: MapSystem la clona una vez por calle
        // y la va moviendo con el mapa.
        TextMeshProUGUI streetTemplate = MakeText(mapImageGO.transform, "Nombre_Calle", "CALLE", 12f,
            HexColor("#f7f2e3"), TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(streetTemplate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(150f, 18f));
        streetTemplate.textWrappingMode = TextWrappingModes.NoWrap;
        // Contorno oscuro: el nombre tiene que leerse igual sobre el asfalto
        // gris que sobre el pasto verde.
        streetTemplate.fontMaterial.EnableKeyword("OUTLINE_ON");
        streetTemplate.outlineWidth = 0.22f;
        streetTemplate.outlineColor = new Color32(10, 20, 14, 255);
        streetTemplate.gameObject.SetActive(false);

        TextMeshProUGUI legend = MakeText(card.transform, "Text_Leyenda",
            "<color=#7be3a1>● Basura</color>      <color=#59b8ff>● Muestra de agua</color>      <color=#ffa033>● Misión</color>      <color=#ffffff>▲ Kuntur</color>",
            15f, UIPalette_Green(), TextAlignmentOptions.Center);
        SetRect(legend.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(-24, 22));

        TextMeshProUGUI hint = MakeText(card.transform, "Text_CerrarMapa", "Arrastra: mover  ·  Rueda: zoom  ·  Clic derecho: marcar destino (ruta morada)  ·  X: borrar ruta  ·  C: centrar  ·  [M] Cerrar", 13f, UIPalette_TextMuted(), TextAlignmentOptions.Center);
        SetRect(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(-24, 22));

        backdrop.gameObject.SetActive(false);
        return (backdrop.gameObject, mapRaw.rectTransform, arrow.rectTransform, markerTemplate, streetTemplate);
    }

    // Panel con esquinas redondeadas (sprite 9-sliced) en vez del rectángulo
    // duro por defecto de Unity: es lo que le da el aspecto del mockup.
    private static Image MakeRoundedPanel(Transform parent, string name, Color color)
    {
        Sprite sprite = UISpriteFactory.RoundedPanel("UI_PanelFondo", Color.white, Color.clear, 14f, 0f);

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    private static Image MakeIcon(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static (GameObject, TextMeshProUGUI) BuildPrompt(Transform root, string name, string key, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Color accent)
    {
        Image panel = MakePanel(root, name, UIPalette_PanelDark());
        SetRect(panel.rectTransform, anchorMin, anchorMax, pivot, anchoredPos, new Vector2(240, 56));

        Image keyBadge = MakePanel(panel.transform, "Key", accent);
        SetRect(keyBadge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(36, 36));
        TextMeshProUGUI keyText = MakeText(keyBadge.transform, "Text_Key", key, 20f, Color.black, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(keyText.rectTransform);

        TextMeshProUGUI labelText = MakeText(panel.transform, "Text_Label", label, 15f, UIPalette_Cream(), TextAlignmentOptions.Left);
        SetRect(labelText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(-70, 0));

        return (panel.gameObject, labelText);
    }

    private static ObjectiveRowUI BuildObjectiveRowTemplate(Transform root)
    {
        GameObject go = new GameObject("Template_ObjectiveRow", typeof(RectTransform));
        go.transform.SetParent(root, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 22);
        CanvasGroup group = go.AddComponent<CanvasGroup>();

        HorizontalLayoutGroup hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 9f;
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Círculo (vacío / verde) con la palomita encima al cumplir.
        Image check = MakeIcon(go.transform, "CheckIcon", UISpriteFactory.Circle("UI_Circulo", Color.white, Color.clear, 0f), new Color(1f, 1f, 1f, 0.18f));
        LayoutElement checkLe = check.gameObject.AddComponent<LayoutElement>();
        checkLe.preferredWidth = 16;
        checkLe.preferredHeight = 16;
        checkLe.minWidth = 16;
        Image tick = MakeIcon(check.transform, "Palomita", UISpriteFactory.Check("UI_Palomita", Color.white), HexColor("#10301c"));
        SetRect(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 14));
        tick.enabled = false;

        TextMeshProUGUI label = MakeText(go.transform, "Label", "Objetivo", 13.5f, Color.white, TextAlignmentOptions.TopLeft);
        label.richText = true;
        LayoutElement labelLe = label.gameObject.AddComponent<LayoutElement>();
        labelLe.flexibleWidth = 1;

        ObjectiveRowUI row = go.AddComponent<ObjectiveRowUI>();
        SerializedObject so = new SerializedObject(row);
        so.FindProperty("checkIcon").objectReferenceValue = check.gameObject;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("tick").objectReferenceValue = tick;
        so.FindProperty("group").objectReferenceValue = group;
        so.FindProperty("layout").objectReferenceValue = hlg;
        so.ApplyModifiedProperties();

        go.SetActive(false);
        return row;
    }

    private static ScorePopup BuildScorePopupTemplate(Transform root)
    {
        GameObject go = new GameObject("Template_ScorePopup", typeof(RectTransform));
        go.transform.SetParent(root, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 50);
        CanvasGroup cg = go.AddComponent<CanvasGroup>();

        TextMeshProUGUI label = MakeText(go.transform, "Label", "+10 pts", 22f, UIPalette_Gold(), TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(label.rectTransform);

        ScorePopup popup = go.AddComponent<ScorePopup>();
        SerializedObject so = new SerializedObject(popup);
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedProperties();

        go.SetActive(false);
        return popup;
    }

    // v57: conversación filmada, como en los juegos de mundo abierto. La
    // pantalla NO se tapa con un cuadro: la cámara enfoca a quien habla y lo
    // que dice sale en una sola línea ancha al pie, sobre un degradé oscuro
    // que solo existe para que el texto se lea sobre cualquier fondo.
    //
    // Las tres respuestas aparecen únicamente cuando le toca contestar al
    // jugador (DialogueUI las prende y apaga), y el subtítulo sube para
    // hacerles sitio.
    private static void BuildDialoguePanel(Transform root)
    {
        Image bg = MakePanel(root, "Panel_Dialogo", new Color(0f, 0f, 0f, 0f));
        StretchFull(bg.rectTransform);
        bg.raycastTarget = false;

        // Degradé al pie de la pantalla: negro abajo, transparente arriba.
        // Sirve de fondo general para las opciones; el subtítulo en sí lleva
        // ADEMÁS su propia caja sólida (ver abajo) porque el degradé solo no
        // daba contraste suficiente contra un cielo claro o una pared blanca.
        Image shade = MakeIcon(bg.transform, "Vineta_Abajo",
            UISpriteFactory.VerticalFade("UI_DegradeVertical", Color.white), new Color(0f, 0f, 0f, 0.74f));
        SetRect(shade.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            Vector2.zero, new Vector2(0f, 460f));
        shade.raycastTarget = false;

        // ---- Subtítulo (abajo, ancho) ----
        // v57d: FIJO en su lugar, ya no sube ni baja. Antes se movía cuando
        // aparecían las opciones -y ese movimiento pasaba mientras todavía se
        // estaba leyendo el texto-, y encima por un instante (lo que tardaba
        // en subir) el texto y las opciones quedaban montados uno sobre el
        // otro. Ahora vive siempre arriba de donde van las opciones, se vean
        // o no, así nunca hay salto ni superposición.
        //
        // El ancho se ESTIRA de borde a borde con un margen fijo a los
        // costados (no un ancho en píxeles fijo): en una pantalla más
        // angosta que 16:9 un ancho fijo se salía de los bordes y cortaba
        // las últimas letras de cada línea.
        Image subtitleCard = MakeRoundedPanel(bg.transform, "Grupo_Subtitulo", new Color(0.03f, 0.035f, 0.03f, 0.86f));
        SetRect(subtitleCard.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 246f), new Vector2(-160f, 172f));
        RectTransform subtitle = subtitleCard.rectTransform;

        TextMeshProUGUI nameText = MakeText(subtitle, "Text_Nombre", "BRAYAN", 20f, HexColor("#FFD77A"),
            TextAlignmentOptions.Top, FontStyles.Bold);
        SetRect(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -14f), new Vector2(-40f, 26f));
        nameText.characterSpacing = 7f;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.raycastTarget = false;

        TextMeshProUGUI speechText = MakeText(subtitle, "Text_Speech", "...", 24f, Color.white,
            TextAlignmentOptions.Top);
        SetRect(speechText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -44f), new Vector2(-80f, -44f));
        // Autoajuste MÁS conservador que antes: una línea de más en pantalla
        // se lee mejor que letra grande apretada en tres.
        speechText.enableAutoSizing = true;
        speechText.fontSizeMin = 16f;
        speechText.fontSizeMax = 24f;
        speechText.lineSpacing = -6f;
        speechText.outlineWidth = 0.15f;
        speechText.outlineColor = new Color32(0, 0, 0, 220);
        speechText.raycastTarget = false;

        // ---- Respuestas: solo en el turno del jugador ----
        // Mismo criterio que el subtítulo: ancho ESTIRADO con margen, no un
        // ancho fijo en píxeles, para que tampoco se salga de la pantalla en
        // una ventana angosta.
        GameObject optionsGO = new GameObject("Content_Opciones", typeof(RectTransform));
        optionsGO.transform.SetParent(bg.transform, false);
        RectTransform optionsRt = optionsGO.GetComponent<RectTransform>();
        SetRect(optionsRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 24f), new Vector2(-460f, 0f));

        VerticalLayoutGroup optionsVlg = optionsGO.AddComponent<VerticalLayoutGroup>();
        optionsVlg.spacing = 6f;
        optionsVlg.childControlWidth = true;
        // El alto lo manda el LayoutElement de cada opción: con esto apagado,
        // cada una quedaba de 100 px (el tamaño por defecto) y la tercera se
        // salía de la pantalla.
        optionsVlg.childControlHeight = true;
        optionsVlg.childForceExpandWidth = true;
        optionsVlg.childForceExpandHeight = false;
        optionsVlg.childAlignment = TextAnchor.LowerCenter;

        ContentSizeFitter optionsFit = optionsGO.AddComponent<ContentSizeFitter>();
        optionsFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Entran con un pequeño salto cada vez que vuelve a ser tu turno.
        optionsGO.AddComponent<CanvasGroup>();
        optionsGO.AddComponent<UIPopIn>();

        DialogueOptionUI[] optionUis = new DialogueOptionUI[3];
        for (int i = 0; i < 3; i++)
            optionUis[i] = BuildDialogueOption(optionsGO.transform, i + 1);
        // Siempre al final: "Salir de la conversación".
        DialogueOptionUI exitUi = BuildDialogueOption(optionsGO.transform, 4);

        // OJO: el componente NO puede vivir en el panel, porque el panel nace
        // desactivado y Unity no llama Awake en objetos inactivos - DialogueUI.Instance
        // quedaría en null y hablar con un vecino tiraría NullReference.
        // Por eso va en un objeto propio siempre activo, y el panel es solo su
        // "panelRoot".
        GameObject dialogueUiGO = new GameObject("DialogueUI");
        dialogueUiGO.transform.SetParent(root, false);
        DialogueUI dialogueUi = dialogueUiGO.AddComponent<DialogueUI>();
        SerializedObject so = new SerializedObject(dialogueUi);
        so.FindProperty("panelRoot").objectReferenceValue = bg.gameObject;
        so.FindProperty("subtitleGroup").objectReferenceValue = subtitle;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("speechText").objectReferenceValue = speechText;
        so.FindProperty("optionsRoot").objectReferenceValue = optionsGO;
        SerializedProperty optionsProp = so.FindProperty("optionButtons");
        optionsProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
            optionsProp.GetArrayElementAtIndex(i).objectReferenceValue = optionUis[i];
        so.FindProperty("exitButton").objectReferenceValue = exitUi;
        so.ApplyModifiedProperties();

        bg.gameObject.SetActive(false);
    }

    private static DialogueOptionUI BuildDialogueOption(Transform parent, int number)
    {
        Image panel = MakeRoundedPanel(parent, $"Option_{number}", new Color(0.04f, 0.055f, 0.05f, 0.88f));
        LayoutElement le = panel.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 46;
        Button button = panel.gameObject.AddComponent<Button>();
        button.targetGraphic = panel;

        Image optionFrame = MakeIcon(panel.transform, "Marco", UISpriteFactory.RoundedPanel("UI_PanelMarco", Color.clear, Color.white, 14f, 3f), new Color(1f, 1f, 1f, 0.35f));
        StretchFull(optionFrame.rectTransform);
        optionFrame.type = Image.Type.Sliced;

        // Viñeta de color por respuesta, igual que la imagen del diálogo del
        // GDD: cada opción se distingue de un vistazo, sin leerla entera.
        Color badgeColor = number switch
        {
            1 => HexColor("#d94f4f"),
            2 => HexColor("#4f8fd9"),
            3 => UIPalette_Amber(),
            _ => HexColor("#7d858e"), // salir
        };

        Image numberBadge = MakeIcon(panel.transform, "Number", UISpriteFactory.Circle("UI_Circulo", Color.white, Color.clear, 0f), badgeColor);
        SetRect(numberBadge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(28, 28));
        TextMeshProUGUI numberText = MakeText(numberBadge.transform, "Text_Number", number.ToString(), 16f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(numberText.rectTransform);

        TextMeshProUGUI optionText = MakeText(panel.transform, "Text_Option", "Opción " + number, 16f, UIPalette_Cream(), TextAlignmentOptions.Left);
        SetRect(optionText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(52, 0), new Vector2(-64, 0));
        optionText.enableAutoSizing = true;
        optionText.fontSizeMin = 12.5f;
        optionText.fontSizeMax = 16f;

        DialogueOptionUI optionUi = panel.gameObject.AddComponent<DialogueOptionUI>();
        SerializedObject so = new SerializedObject(optionUi);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("numberText").objectReferenceValue = numberText;
        so.FindProperty("optionText").objectReferenceValue = optionText;
        so.ApplyModifiedProperties();

        return optionUi;
    }

    private static GameObject BuildResultPanel(Transform root)
    {
        Image bg = MakePanel(root, "Panel_Resultado", new Color(0.055f, 0.078f, 0.063f, 0.95f));
        StretchFull(bg.rectTransform);

        Image card = MakeRoundedPanel(bg.transform, "Card_Resultado", UIPalette_PanelDark());
        SetRect(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660, 470));
        card.gameObject.AddComponent<CanvasGroup>();
        card.gameObject.AddComponent<UIPopIn>();

        TextMeshProUGUI title = MakeText(card.transform, "Text_Titulo", "FIN DE LA JORNADA", 30f, UIPalette_Gold(), TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(-40, 38));

        TextMeshProUGUI subtitle = MakeText(card.transform, "Text_Subtitulo", "Se acabó el tiempo en el valle del Mantaro", 14f, UIPalette_TextMuted(), TextAlignmentOptions.Center);
        SetRect(subtitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, -58), new Vector2(-60, 46));

        Sprite starFilled = UISpriteFactory.Star("UI_EstrellaLlena", UIPalette_Gold());
        Sprite starEmpty = UISpriteFactory.Star("UI_EstrellaVacia", new Color(1f, 1f, 1f, 0.22f));

        Image[] stars = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject starGO = new GameObject($"Star_{i + 1}", typeof(RectTransform));
            starGO.transform.SetParent(card.transform, false);
            Image starImg = starGO.AddComponent<Image>();
            starImg.sprite = starEmpty;
            RectTransform starRt = starGO.GetComponent<RectTransform>();
            float xOffset = (i - 1) * 70f;
            SetRect(starRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(xOffset, -112), new Vector2(54, 54));
            stars[i] = starImg;
        }

        (TextMeshProUGUI healthStat, TextMeshProUGUI neighborsStat, TextMeshProUGUI pointsStat) = BuildResultStatCards(card.transform);

        ResultScreenController controller = bg.gameObject.AddComponent<ResultScreenController>();
        SerializedObject so = new SerializedObject(controller);
        SerializedProperty starsProp = so.FindProperty("stars");
        starsProp.arraySize = 3;
        for (int i = 0; i < 3; i++) starsProp.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
        so.FindProperty("starFilled").objectReferenceValue = starFilled;
        so.FindProperty("starEmpty").objectReferenceValue = starEmpty;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("subtitleText").objectReferenceValue = subtitle;
        so.FindProperty("healthStatText").objectReferenceValue = healthStat;
        so.FindProperty("neighborsStatText").objectReferenceValue = neighborsStat;
        so.FindProperty("pointsStatText").objectReferenceValue = pointsStat;
        so.ApplyModifiedProperties();

        // Reintentar (vuelve al último día guardado) o ir al menú.
        Button retry = BuildResultButton(card.transform, "REINTENTAR", new Vector2(-110f, 26f), UIPalette_Amber());
        Button menu = BuildResultButton(card.transform, "MENÚ", new Vector2(110f, 26f), new Color(1f, 1f, 1f, 0.18f));
        UnityEventTools.AddPersistentListener(retry.onClick, controller.Retry);
        UnityEventTools.AddPersistentListener(menu.onClick, controller.GoToMenu);

        bg.gameObject.SetActive(false);
        return bg.gameObject;
    }

    private static (TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI) BuildResultStatCards(Transform card)
    {
        GameObject row = new GameObject("Row_Stats", typeof(RectTransform));
        row.transform.SetParent(card, false);
        RectTransform rowRt = row.GetComponent<RectTransform>();
        SetRect(rowRt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(0, 92), new Vector2(-40, 150));
        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        TextMeshProUGUI health = BuildStatCard(row.transform, "SALUD DEL VALLE", "0%");
        TextMeshProUGUI neighbors = BuildStatCard(row.transform, "MISIONES CUMPLIDAS", "0");
        TextMeshProUGUI points = BuildStatCard(row.transform, "ESTRELLAS", "0");
        return (health, neighbors, points);
    }

    private static Button BuildResultButton(Transform parent, string label, Vector2 position, Color color)
    {
        Image panel = MakeRoundedPanel(parent, "Boton_" + label, color);
        SetRect(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), position, new Vector2(190, 48));
        Button button = panel.gameObject.AddComponent<Button>();
        button.targetGraphic = panel;
        TextMeshProUGUI text = MakeText(panel.transform, "Text", label, 20f, UIPalette_Cream(), TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(text.rectTransform);
        return button;
    }

    private static TextMeshProUGUI BuildStatCard(Transform parent, string label, string initialValue)
    {
        Image card = MakePanel(parent, "Card_" + label, new Color(1f, 1f, 1f, 0.06f));
        TextMeshProUGUI labelText = MakeText(card.transform, "Text_Label", label, 11f, UIPalette_TextMuted(), TextAlignmentOptions.Top, FontStyles.Bold);
        SetRect(labelText.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(-10, 40));
        TextMeshProUGUI valueText = MakeText(card.transform, "Text_Value", initialValue, 22f, UIPalette_Cream(), TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(valueText.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(-10, 40));
        return valueText;
    }

    private static Sprite CreateSolidSprite(string name, Color color)
    {
        string path = $"{ArtDir}/{name}.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        const int size = 8;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels32(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------------------------------------------------------------
    // Escena de Menú Principal
    // ---------------------------------------------------------------

    private static void BuildMainMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // v55: fondo nuevo (el valle desde el cerro al atardecer) y menú al
        // estilo Horizon Zero Dawn: título grande y fino al centro, opciones
        // chicas abajo a la izquierda con una línea dorada bajo la elegida,
        // chispas de luz, neblina y el sol que respira.
        Sprite mainBg = LoadBackgroundSprite("Fondo_MenuHorizonte") ?? LoadBackgroundSprite("Fondo_MenuPrincipal");
        Sprite settingsBg = LoadBackgroundSprite("Fondo_Ajustes");
        Sprite creditsBg = LoadBackgroundSprite("Fondo_Creditos");
        TMP_FontAsset lightFont = GetMenuFont("Light");
        TMP_FontAsset boldFont = GetMenuFont("SemiBold");

        GameObject canvasGO = new GameObject("Menu_Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // 1920x1080 y no 1280x720: es el tamaño real de los fondos, así el
        // menú se diseña en las mismas medidas que la ilustración.
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ---- Fondo con fundido cruzado + vaivén lento ----
        RectTransform bgHolder = MakeRect(canvasGO.transform, "Fondo");
        StretchFull(bgHolder);
        bgHolder.gameObject.AddComponent<BackgroundKenBurns>();

        Image bgBase = MakePanel(bgHolder, "Fondo_Base", Color.white);
        StretchFull(bgBase.rectTransform);
        bgBase.sprite = mainBg;
        bgBase.preserveAspect = false;
        bgBase.raycastTarget = false;
        if (mainBg == null) bgBase.color = HexColor("#2b1b2e");

        Image bgOverlay = MakePanel(bgHolder, "Fondo_Overlay", new Color(1f, 1f, 1f, 0f));
        StretchFull(bgOverlay.rectTransform);
        bgOverlay.raycastTarget = false;

        // ---- Luz y aire: sol que respira, neblina y chispas ----
        Sprite glowSprite = UISpriteFactory.SoftGlow("UI_Brillo", Color.white);
        RectTransform fxLayer = MakeRect(canvasGO.transform, "Ambiente");
        StretchFull(fxLayer);
        Image sun = MakeIcon(fxLayer, "Sol_Halo", glowSprite, new Color(1f, 0.78f, 0.45f, 0.38f));
        SetRect(sun.rectTransform, new Vector2(0.62f, 0.74f), new Vector2(0.62f, 0.74f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 760f));
        Image mistA = MakeIcon(fxLayer, "Neblina_A", glowSprite, new Color(1f, 0.92f, 0.85f, 0.13f));
        SetRect(mistA.rectTransform, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2600f, 340f));
        Image mistB = MakeIcon(fxLayer, "Neblina_B", glowSprite, new Color(0.95f, 0.88f, 0.95f, 0.1f));
        SetRect(mistB.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2800f, 260f));
        RectTransform motes = MakeRect(fxLayer, "Chispas");
        StretchFull(motes);
        MenuAmbientFx ambient = motes.gameObject.AddComponent<MenuAmbientFx>();
        SerializedObject ambientSo = new SerializedObject(ambient);
        ambientSo.FindProperty("moteSprite").objectReferenceValue = glowSprite;
        ambientSo.FindProperty("sunGlow").objectReferenceValue = sun.rectTransform;
        SerializedProperty mistProp = ambientSo.FindProperty("mist");
        mistProp.arraySize = 2;
        mistProp.GetArrayElementAtIndex(0).objectReferenceValue = mistA.rectTransform;
        mistProp.GetArrayElementAtIndex(1).objectReferenceValue = mistB.rectTransform;
        ambientSo.ApplyModifiedProperties();

        // ---- Sombras suaves para que el texto se lea sobre la ilustración ----
        Image leftShade = MakeIcon(canvasGO.transform, "Sombra_Izquierda", CreateGradientSprite("UI_Degradado_Izq", true), new Color(0f, 0f, 0f, 0.55f));
        SetRect(leftShade.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(820f, 0f));
        leftShade.raycastTarget = false;

        Image bottomShade = MakeIcon(canvasGO.transform, "Sombra_Abajo", CreateGradientSprite("UI_Degradado_Abajo", false), new Color(0f, 0f, 0f, 0.72f));
        SetRect(bottomShade.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0f, 420f));
        bottomShade.raycastTarget = false;

        // =====================================================
        //  PANTALLA 1 - MENÚ PRINCIPAL
        // =====================================================
        CanvasGroup mainPanel = MakeScreen(canvasGO.transform, "Panel_Principal");

        // Título grande y fino, al centro (como "HORIZON").
        Image titleGlow = MakeIcon(mainPanel.transform, "Titulo_Brillo", glowSprite, new Color(1f, 0.85f, 0.6f, 0.22f));
        SetRect(titleGlow.rectTransform, new Vector2(0.5f, 0.63f), new Vector2(0.5f, 0.63f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 420f));
        titleGlow.raycastTarget = false;

        TextMeshProUGUI title = MakeText(mainPanel.transform, "Text_Titulo", "KUNTUR", 170f, new Color(1f, 0.98f, 0.94f), TextAlignmentOptions.Center, FontStyles.Normal);
        SetRect(title.rectTransform, new Vector2(0.5f, 0.63f), new Vector2(0.5f, 0.63f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1600f, 200f));
        if (lightFont != null) title.font = lightFont;
        title.characterSpacing = 42f;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.gameObject.AddComponent<TitleTextAnimator>();

        TextMeshProUGUI subtitle = MakeText(mainPanel.transform, "Text_Subtitulo", "ECOS DEL VALLE", 40f, new Color(1f, 0.95f, 0.88f, 0.95f), TextAlignmentOptions.Center, FontStyles.Normal);
        SetRect(subtitle.rectTransform, new Vector2(0.5f, 0.63f), new Vector2(0.5f, 0.63f), new Vector2(0.5f, 0.5f), new Vector2(0f, -95f), new Vector2(900f, 56f));
        if (lightFont != null) subtitle.font = lightFont;
        subtitle.characterSpacing = 34f;
        subtitle.textWrappingMode = TextWrappingModes.NoWrap;
        AddSlideIn(subtitle.gameObject, 0.7f, new Vector2(0f, -20f));

        Sprite fadeLine = UISpriteFactory.HorizontalFade("UI_DegradeHorizontal", Color.white);
        foreach (int side in new[] { -1, 1 })
        {
            Image rule = MakeIcon(mainPanel.transform, side < 0 ? "Linea_Izq" : "Linea_Der", fadeLine, new Color(1f, 0.84f, 0.42f, 0.9f));
            SetRect(rule.rectTransform, new Vector2(0.5f, 0.63f), new Vector2(0.5f, 0.63f), new Vector2(0f, 0.5f), new Vector2(side * 300f, -95f), new Vector2(230f, 2f));
            if (side < 0) rule.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            rule.raycastTarget = false;
            AddSlideIn(rule.gameObject, 0.85f, new Vector2(side * 60f, 0f));
        }

        // Opciones chicas abajo a la izquierda, una debajo de otra.
        string[] labels = { "CONTINUAR", "NUEVA PARTIDA", "AJUSTES", "CRÉDITOS", "SALIR" };
        Button[] mainButtons = new Button[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            mainButtons[i] = MakeHorizonMenuItem(mainPanel.transform, labels[i], 360f - i * 62f, 1.0f + i * 0.09f, boldFont);
        }

        // Ayuda de teclas abajo a la derecha.
        TextMeshProUGUI keys = MakeText(mainPanel.transform, "Text_Teclas",
            "<color=#F5D27A>ENTER</color>  Seleccionar      <color=#F5D27A>ESC</color>  Volver", 22f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Right);
        SetRect(keys.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-70f, 46f), new Vector2(700f, 30f));
        if (lightFont != null) keys.font = lightFont;
        keys.characterSpacing = 3f;
        AddSlideIn(keys.gameObject, 1.5f, new Vector2(0f, -12f));

        // =====================================================
        //  PANTALLA 2 - AJUSTES
        // =====================================================
        CanvasGroup settingsPanel = MakeScreen(canvasGO.transform, "Panel_Ajustes");
        MakeScreenTitle(settingsPanel.transform, "AJUSTES");

        Image settingsCard = MakeRoundedPanel(settingsPanel.transform, "Tarjeta", UIPalette_PanelDark());
        SetRect(settingsCard.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(132f, -300f), new Vector2(840f, 340f));
        AddSlideIn(settingsCard.gameObject, 0.15f, new Vector2(-50f, 0f));

        Slider volumeSlider = MakeSettingSlider(settingsCard.transform, "Volumen", "Volumen general", -56f, out TextMeshProUGUI volumeValue);
        Slider qualitySlider = MakeSettingSlider(settingsCard.transform, "Calidad", "Calidad gráfica", -152f, out TextMeshProUGUI qualityValue);
        Toggle fullscreenToggle = MakeSettingToggle(settingsCard.transform, "PantallaCompleta", "Pantalla completa", -248f);

        SettingsPanel settings = settingsPanel.gameObject.AddComponent<SettingsPanel>();
        SerializedObject settingsSo = new SerializedObject(settings);
        settingsSo.FindProperty("volumeSlider").objectReferenceValue = volumeSlider;
        settingsSo.FindProperty("volumeValue").objectReferenceValue = volumeValue;
        settingsSo.FindProperty("qualitySlider").objectReferenceValue = qualitySlider;
        settingsSo.FindProperty("qualityValue").objectReferenceValue = qualityValue;
        settingsSo.FindProperty("fullscreenToggle").objectReferenceValue = fullscreenToggle;
        settingsSo.ApplyModifiedProperties();

        Button settingsBack = MakeMenuItem(settingsPanel.transform, "VOLVER", -700f, 0.45f);

        // =====================================================
        //  PANTALLA 3 - CRÉDITOS
        // =====================================================
        CanvasGroup creditsPanel = MakeScreen(canvasGO.transform, "Panel_Creditos");
        BuildCreditsRoll(creditsPanel.transform);

        Button creditsBack = MakeMenuItem(creditsPanel.transform, "VOLVER", -720f, 0.45f);

        // =====================================================
        //  Lógica: navegación de escenas + cambio de pantallas
        // =====================================================
        GameObject controllerGO = new GameObject("MenuController");
        MenuController controller = controllerGO.AddComponent<MenuController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("explorationSceneName").stringValue = "Exploracion";
        so.FindProperty("citySelectSceneName").stringValue = "Exploracion";
        so.FindProperty("mainMenuSceneName").stringValue = "MenuPrincipal";
        so.ApplyModifiedProperties();

        MainMenuUI menuUI = canvasGO.AddComponent<MainMenuUI>();
        SerializedObject menuSo = new SerializedObject(menuUI);
        menuSo.FindProperty("mainPanel").objectReferenceValue = mainPanel;
        menuSo.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
        menuSo.FindProperty("creditsPanel").objectReferenceValue = creditsPanel;
        menuSo.FindProperty("backgroundBase").objectReferenceValue = bgBase;
        menuSo.FindProperty("backgroundOverlay").objectReferenceValue = bgOverlay;
        menuSo.FindProperty("mainBackground").objectReferenceValue = mainBg;
        menuSo.FindProperty("settingsBackground").objectReferenceValue = settingsBg;
        menuSo.FindProperty("creditsBackground").objectReferenceValue = creditsBg;
        menuSo.FindProperty("firstMainButton").objectReferenceValue = mainButtons[0].gameObject;
        menuSo.FindProperty("firstSettingsButton").objectReferenceValue = settingsBack.gameObject;
        menuSo.FindProperty("firstCreditsButton").objectReferenceValue = creditsBack.gameObject;
        menuSo.ApplyModifiedProperties();

        // "CONTINUAR · DÍA N" (o "JUGAR" la primera vez) y "NUEVA PARTIDA".
        mainButtons[0].gameObject.AddComponent<MenuContinueLabel>();
        UnityEventTools.AddPersistentListener(mainButtons[0].onClick, controller.ContinueGame);
        UnityEventTools.AddPersistentListener(mainButtons[1].onClick, controller.NewGame);
        UnityEventTools.AddPersistentListener(mainButtons[2].onClick, menuUI.ShowSettings);
        UnityEventTools.AddPersistentListener(mainButtons[3].onClick, menuUI.ShowCredits);
        UnityEventTools.AddPersistentListener(mainButtons[4].onClick, controller.QuitGame);
        UnityEventTools.AddPersistentListener(settingsBack.onClick, menuUI.ShowMain);
        UnityEventTools.AddPersistentListener(creditsBack.onClick, menuUI.ShowMain);

        BuildSceneFader();

        // Música del menú. Va en su propio AudioManager de esta escena: el de
        // la partida no existe todavía acá.
        GameObject menuAudioGO = new GameObject("AudioManager");
        AudioManager menuAudio = menuAudioGO.AddComponent<AudioManager>();
        SerializedObject menuAudioSo = new SerializedObject(menuAudio);
        menuAudioSo.FindProperty("music").objectReferenceValue = LoadAudio("Musica_Menu");
        menuAudioSo.FindProperty("musicVolume").floatValue = 0.7f;
        menuAudioSo.ApplyModifiedProperties();

        // v56c: sonido al cambiar de opción y al hacer clic.
        MenuSounds menuSounds = menuAudioGO.AddComponent<MenuSounds>();
        SerializedObject msSo = new SerializedObject(menuSounds);
        msSo.FindProperty("changeClip").objectReferenceValue = LoadAudio("SFX_Menu_Cambiar");
        msSo.FindProperty("clickClip").objectReferenceValue = LoadAudio("SFX_Menu_Click");
        msSo.ApplyModifiedProperties();

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        string path = ScenesDir + "/MenuPrincipal.unity";
        EditorSceneManager.SaveScene(scene, path);
    }

    // ---------------------------------------------------------------
    // Piezas del menú
    // ---------------------------------------------------------------

    // Cortina negra a pantalla completa para las transiciones entre escenas.
    // Va en su PROPIO Canvas con sortingOrder alto: así tapa TODO (menú, HUD,
    // diálogo) sin depender de dónde quedó en la jerarquía.
    private static void BuildSceneFader()
    {
        GameObject faderCanvasGO = new GameObject("Fader_Canvas");
        Canvas faderCanvas = faderCanvasGO.AddComponent<Canvas>();
        faderCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        faderCanvas.sortingOrder = 100;
        faderCanvasGO.AddComponent<GraphicRaycaster>();

        Image curtain = MakePanel(faderCanvasGO.transform, "Cortina", Color.black);
        StretchFull(curtain.rectTransform);
        curtain.raycastTarget = false;
        curtain.gameObject.AddComponent<CanvasGroup>();
        curtain.gameObject.AddComponent<ScreenFader>();
    }

    // Una "pantalla" del menú: ocupa todo y se prende/apaga con su CanvasGroup.
    // v53: créditos de película. Fondo negro, letras blancas que suben
    // despacio (CreditsRoll) y se esfuman arriba y abajo.
    private static void BuildCreditsRoll(Transform parent)
    {
        Image black = MakePanel(parent, "Fondo_Negro", Color.black);
        StretchFull(black.rectTransform);

        RectTransform viewport = MakeRect(parent, "Creditos_Ventana");
        StretchFull(viewport);

        TextMeshProUGUI text = MakeText(viewport, "Text_Creditos", CreditsText, 26f, Color.white, TextAlignmentOptions.Top);
        SetRect(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -1080f), new Vector2(1100f, 100f));
        text.textWrappingMode = TextWrappingModes.Normal;
        text.lineSpacing = 4f;
        text.raycastTarget = false;
        ContentSizeFitter fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreditsRoll roll = viewport.gameObject.AddComponent<CreditsRoll>();
        SerializedObject so = new SerializedObject(roll);
        so.FindProperty("content").objectReferenceValue = text.rectTransform;
        so.ApplyModifiedProperties();

        // Degradés negros arriba y abajo: el texto aparece y se va suave.
        Sprite fade = UISpriteFactory.VerticalFade("UI_DegradeVertical", Color.black);
        Image bottom = MakeIcon(parent, "Degrade_Abajo", fade, Color.white);
        SetRect(bottom.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 220f));
        Image top = MakeIcon(parent, "Degrade_Arriba", fade, Color.white);
        SetRect(top.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 220f));
        top.rectTransform.localScale = new Vector3(1f, -1f, 1f);

        TextMeshProUGUI hint = MakeText(parent, "Text_AyudaCreditos", "Espacio: adelantar    ·    Esc: volver", 15f,
            new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Right);
        SetRect(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(600f, 24f));
    }

    private const string CreditsText =
        "<size=150%><b><color=#F5C400>KUNTUR</color></b></size>\n" +
        "<size=80%><cspace=0.35em>ECOS DEL VALLE</cspace></size>\n" +
        "<size=60%><color=#BBBBBB>Proyecto Integrador de Videojuegos</color></size>\n\n\n\n" +

        "<size=70%><color=#F5C400><cspace=0.3em>DESARROLLO Y DIRECCIÓN</cspace></color></size>\n\n" +
        "<size=60%><color=#AAAAAA>Diseño de juego, programación y arte</color></size>\n" +
        "<b>Junior Saul Ramirez Espiritu</b>\n\n" +
        "<size=60%><color=#AAAAAA>Docente del curso</color></size>\n" +
        "Miguel Torres Vargas\n\n" +
        "<size=60%><color=#AAAAAA>Curso</color></size>\n" +
        "Desarrollo de Videojuegos\n\n" +
        "<size=60%><color=#AAAAAA>Carrera</color></size>\n" +
        "Ingeniería de Sistemas e Informática\n\n" +
        "<size=60%><color=#AAAAAA>Institución</color></size>\n" +
        "Universidad Continental · Huancayo, Perú\n\n\n\n" +

        "<size=70%><color=#F5C400><cspace=0.3em>TECNOLOGÍA Y RECURSOS</cspace></color></size>\n\n" +
        "<size=60%><color=#AAAAAA>Motor de desarrollo y programación</color></size>\n" +
        "Unity 6 · C#\n\n" +
        "<size=60%><color=#AAAAAA>Arte conceptual y modelo 3D del personaje</color></size>\n" +
        "Google Gemini · Tripo AI\n\n" +
        "<size=60%><color=#AAAAAA>Modelado y edición 3D</color></size>\n" +
        "Blender\n\n" +
        "<size=60%><color=#AAAAAA>Rigging y animaciones 3D</color></size>\n" +
        "Mixamo (Adobe)\n\n" +
        "<size=60%><color=#AAAAAA>Banda sonora y efectos de audio</color></size>\n" +
        "Pixabay · música andina y sonidos ambientales\n\n" +
        "<size=60%><color=#AAAAAA>Recursos de la Unity Asset Store</color></size>\n" +
        "<size=75%>City People FREE Samples (Denys Almaral) · ARCADE: FREE Racing Car (Mena)\n" +
        "Simple Retro Car (Polyeler) · Ukraine only truck (KOS-store)\n" +
        "German Shepherd 3D Model (RetroStyle Games) · Free chibi cat (Ladymito)\n" +
        "City Traffic Lights Pack · WasteOvergrowth · WaterWorks · AllSky Free\n" +
        "Northern Mountain FREE · Free Stylized Low Poly Tree · Modular Lowpoly Streets\n" +
        "Underwater Camera FX</size>\n\n\n\n" +

        "<size=70%><color=#7BE3A0><cspace=0.3em>CONCIENCIA AMBIENTAL</cspace></color></size>\n\n" +
        "<size=85%>Inspirado en la acumulación de residuos y la contaminación\n" +
        "de los ríos del Valle del Mantaro, Huancayo.\n\n" +
        "Un proyecto para concientizar sobre la limpieza de nuestras calles,\n" +
        "el cuidado del agua y la preservación de nuestro entorno.</size>\n\n" +
        "<size=65%><color=#7DD3FC>ODS 6 · Agua limpia y saneamiento\n" +
        "ODS 11 · Ciudades y comunidades sostenibles</color></size>\n\n\n\n\n" +

        "<size=90%><b>Gracias por jugar.</b></size>\n" +
        "<size=65%><color=#BBBBBB>Cada bolsa que recoges es un río que respira.</color></size>";

    private static CanvasGroup MakeScreen(Transform parent, string name)
    {
        RectTransform rt = MakeRect(parent, name);
        StretchFull(rt);
        return rt.gameObject.AddComponent<CanvasGroup>();
    }

    private static void MakeScreenTitle(Transform parent, string text)
    {
        TextMeshProUGUI title = MakeText(parent, "Text_Titulo", text, 64f, UIPalette_Amber(), TextAlignmentOptions.Left, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(132f, -120f), new Vector2(760f, 80f));
        title.characterSpacing = 10f;
        title.gameObject.AddComponent<TitleTextAnimator>();

        Image rule = MakePanel(parent, "Linea_Titulo", UIPalette_Gold());
        SetRect(rule.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(140f, -212f), new Vector2(300f, 3f));
        rule.raycastTarget = false;
        AddSlideIn(rule.gameObject, 0.45f, new Vector2(-220f, 0f));
    }

    // Un renglón del menú: contenedor que hace la entrada + botón que hace el
    // hover. Van separados a propósito: si los dos scripts movieran el MISMO
    // objeto, se pelearían por la posición y el botón temblaría.
    private static Button MakeMenuItem(Transform parent, string label, float yOffset, float delay)
    {
        RectTransform holder = MakeRect(parent, "Item_" + label);
        SetRect(holder, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(140f, yOffset), new Vector2(480f, 66f));
        AddSlideIn(holder.gameObject, delay, new Vector2(-80f, 0f));

        // Imagen transparente: no se ve, pero es la que recibe el clic (un
        // objeto sin Graphic no es clickeable en UGUI).
        Image hit = MakePanel(holder, "Button_" + label, new Color(1f, 1f, 1f, 0f));
        StretchFull(hit.rectTransform);

        Button button = hit.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None; // la animación la hace UIButtonHover

        // El marcador es una IMAGEN, no el carácter "▶".
        //
        // La fuente por defecto (LiberationSans SDF) no trae ese símbolo, así
        // que TextMeshPro lo reemplazaba por "¡" y llenaba la consola de
        // advertencias. Con el sprite de flecha que ya usa el proyecto se ve
        // igual y funciona con cualquier fuente.
        // Nombre propio ("UI_FlechaMenu") y no el de la flecha del mapa: la
        // fábrica de sprites guarda el PNG por NOMBRE y reutiliza el que ya
        // exista, así que pedir "UI_FlechaJugador" devolvería la del mapa, con
        // su contorno oscuro ya pintado encima.
        Image marker = MakeIcon(hit.transform, "Marcador", UISpriteFactory.Arrow("UI_FlechaMenu_V2", Color.white, Color.white), UIPalette_Amber());
        SetRect(marker.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2f, 0f), new Vector2(20f, 22f));
        marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f); // la flecha viene apuntando arriba

        TextMeshProUGUI text = MakeText(hit.transform, "Text", label, 36f, HexColor("#efeade"), TextAlignmentOptions.Left, FontStyles.Bold);
        SetRect(text.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(36f, 0f), new Vector2(-72f, 0f));
        text.characterSpacing = 6f;
        text.raycastTarget = false;

        UIButtonHover hover = hit.gameObject.AddComponent<UIButtonHover>();
        SerializedObject hoverSo = new SerializedObject(hover);
        hoverSo.FindProperty("label").objectReferenceValue = text;
        hoverSo.FindProperty("marker").objectReferenceValue = marker;
        hoverSo.ApplyModifiedProperties();

        return button;
    }

    // v55: renglón del menú estilo Horizon (ver HorizonMenuItem).
    private static Button MakeHorizonMenuItem(Transform parent, string label, float yFromBottom, float delay, TMP_FontAsset font)
    {
        RectTransform holder = MakeRect(parent, "Item_" + label);
        SetRect(holder, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(120f, yFromBottom), new Vector2(560f, 54f));
        AddSlideIn(holder.gameObject, delay, new Vector2(-50f, 0f));

        Image hit = MakePanel(holder, "Button_" + label, new Color(1f, 1f, 1f, 0f));
        StretchFull(hit.rectTransform);
        Button button = hit.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;

        TextMeshProUGUI text = MakeText(hit.transform, "Text", label, 30f, new Color(0.93f, 0.9f, 0.84f, 0.82f), TextAlignmentOptions.Left, FontStyles.Normal);
        SetRect(text.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), Vector2.zero);
        if (font != null) text.font = font;
        text.characterSpacing = 6f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        Image underline = MakeIcon(hit.transform, "Subrayado", UISpriteFactory.HorizontalFade("UI_DegradeHorizontal", Color.white), new Color(1f, 0.84f, 0.42f, 0f));
        SetRect(underline.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(0f, 6f), new Vector2(0f, 2f));

        HorizonMenuItem item = hit.gameObject.AddComponent<HorizonMenuItem>();
        SerializedObject so = new SerializedObject(item);
        so.FindProperty("label").objectReferenceValue = text;
        so.FindProperty("underline").objectReferenceValue = underline.rectTransform;
        so.FindProperty("underlineImage").objectReferenceValue = underline;
        so.ApplyModifiedProperties();
        return button;
    }

    private static void AddSlideIn(GameObject target, float delay, Vector2 offset)
    {
        if (target.GetComponent<CanvasGroup>() == null) target.AddComponent<CanvasGroup>();
        UISlideIn slide = target.AddComponent<UISlideIn>();
        SerializedObject so = new SerializedObject(slide);
        so.FindProperty("delay").floatValue = delay;
        so.FindProperty("offset").vector2Value = offset;
        so.ApplyModifiedProperties();
    }

    private static Slider MakeSettingSlider(Transform parent, string name, string label, float yOffset, out TextMeshProUGUI valueLabel)
    {
        RectTransform row = MakeRect(parent, "Fila_" + name);
        SetRect(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0f, yOffset), new Vector2(-96f, 56f));

        TextMeshProUGUI text = MakeText(row, "Text_Label", label, 24f, UIPalette_CreamSoft(), TextAlignmentOptions.Left);
        SetRect(text.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(300f, 0f));

        valueLabel = MakeText(row, "Text_Valor", "", 22f, UIPalette_Amber(), TextAlignmentOptions.Right, FontStyles.Bold);
        SetRect(valueLabel.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(120f, 0f));

        RectTransform sliderRect = MakeRect(row, "Slider");
        SetRect(sliderRect, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(30f, 0f), new Vector2(-460f, -14f));
        Slider slider = sliderRect.gameObject.AddComponent<Slider>();

        Image background = MakeRoundedPanel(sliderRect, "Fondo", new Color(1f, 1f, 1f, 0.16f));
        SetRect(background.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 10f));

        RectTransform fillArea = MakeRect(sliderRect, "Fill Area");
        SetRect(fillArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-22f, 10f));
        Image fill = MakeRoundedPanel(fillArea, "Fill", UIPalette_Amber());
        SetRect(fill.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 0f));

        RectTransform handleArea = MakeRect(sliderRect, "Handle Slide Area");
        SetRect(handleArea, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-22f, 0f));
        Image handle = MakeIcon(handleArea, "Handle", UISpriteFactory.Circle("UI_Circulo", Color.white, Color.clear, 0f), UIPalette_Cream());
        SetRect(handle.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static Toggle MakeSettingToggle(Transform parent, string name, string label, float yOffset)
    {
        RectTransform row = MakeRect(parent, "Fila_" + name);
        SetRect(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0f, yOffset), new Vector2(-96f, 56f));

        TextMeshProUGUI text = MakeText(row, "Text_Label", label, 24f, UIPalette_CreamSoft(), TextAlignmentOptions.Left);
        SetRect(text.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(320f, 0f));

        RectTransform toggleRect = MakeRect(row, "Toggle");
        SetRect(toggleRect, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(46f, 0f));
        Toggle toggle = toggleRect.gameObject.AddComponent<Toggle>();

        Image box = MakeRoundedPanel(toggleRect, "Fondo", new Color(1f, 1f, 1f, 0.16f));
        SetRect(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 42f));

        Image check = MakeRoundedPanel(box.transform, "Check", UIPalette_Amber());
        SetRect(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));

        toggle.targetGraphic = box;
        toggle.graphic = check;
        return toggle;
    }

    // ---------------------------------------------------------------
    // Sprites e imágenes del menú
    // ---------------------------------------------------------------

    // Carga uno de los fondos ilustrados. Si el PNG acaba de aparecer en la
    // carpeta, Unity todavía no lo importó como Sprite (por defecto entra como
    // textura suelta y no se puede usar en un Image), así que se corrige acá.
    private static Sprite LoadBackgroundSprite(string fileName)
    {
        string path = $"{ArtDir}/UI/Fondos/{fileName}.png";

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[Kuntur] Falta el fondo {path}. El menú se arma igual, con color plano de respaldo.");
            return null;
        }

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Degradado de negro a transparente. Estirado a pantalla completa hace de
    // sombra suave para que el texto blanco se lea encima de la ilustración
    // sin tener que oscurecer la imagen entera.
    private static Sprite CreateGradientSprite(string name, bool horizontal)
    {
        string dir = ArtDir + "/UI";
        string path = $"{dir}/{name}.png";

        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        CreateFolderRecursive(dir);

        int width = horizontal ? 256 : 8;
        int height = horizontal ? 8 : 256;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // t = 0 en el borde opaco (izquierda o abajo), 1 en el otro.
                float t = horizontal ? x / (float)(width - 1) : y / (float)(height - 1);
                float alpha = Mathf.Pow(1f - t, 1.7f); // cae rápido al principio y se desvanece largo
                pixels[y * width + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void AddScenesToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(ScenesDir + "/MenuPrincipal.unity", true),
            new EditorBuildSettingsScene(ScenesDir + "/Exploracion.unity", true),
            new EditorBuildSettingsScene(ScenesDir + "/Intro.unity", true),
        };
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------------------------------------------------------------
    // Helpers de UI
    // ---------------------------------------------------------------

    private static RectTransform MakeRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Image MakePanel(Transform parent, string name, Color color)
    {
        RectTransform rt = MakeRect(parent, name);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, string content, float fontSize, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        RectTransform rt = MakeRect(parent, name);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        if (defaultFont != null) tmp.font = defaultFont;
        return tmp;
    }

    private static Shader cachedShader;

    // El proyecto se creó con la plantilla 3D básica (no URP), así que el
    // pipeline activo es el Built-in: "Standard" es el shader correcto ahí.
    // Si en algún momento se configura URP como pipeline activo, cambia esto
    // a "Universal Render Pipeline/Lit" (o deja el fallback, que ya lo intenta).
    private static Shader FindBestShader()
    {
        if (cachedShader != null) return cachedShader;

        // Si hay un Render Pipeline activo (URP), "Standard" existe igual pero
        // se vería TODO en magenta: hay que pedir el shader del pipeline
        // primero y dejar "Standard" solo para el pipeline Built-in.
        bool usingScriptablePipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        if (usingScriptablePipeline)
        {
            cachedShader = Shader.Find("Universal Render Pipeline/Lit");
            if (cachedShader == null) cachedShader = Shader.Find("HDRP/Lit");
        }

        if (cachedShader == null) cachedShader = Shader.Find("Standard");
        if (cachedShader == null) cachedShader = Shader.Find("Universal Render Pipeline/Lit");
        if (cachedShader == null) cachedShader = Shader.Find("Sprites/Default");
        return cachedShader;
    }

    private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

    // Un material por COLOR, guardado como asset y reutilizado. Antes se creaba
    // un material nuevo por cada objeto: con las montañas, los árboles y las
    // piedras del valle eso serían cientos de materiales, la escena pesaría de
    // más y Unity no podría agrupar los dibujados (batching).
    private static Material GetMaterial(Color color)
    {
        string key = ColorUtility.ToHtmlStringRGBA(color);
        if (MaterialCache.TryGetValue(key, out Material cached) && cached != null) return cached;

        // Pista, vereda y sardinel llevan su textura de verdad (ver
        // TryGetSurfaceMaterial).
        Material surface = TryGetSurfaceMaterial(key);
        if (surface != null) { MaterialCache[key] = surface; return surface; }

        string dir = ArtDir + "/Materiales";
        string path = $"{dir}/Mat_{key}.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null && mat.shader != FindBestShader())
        {
            // El material quedó de una construcción anterior con otro render
            // pipeline: se le pone el shader correcto en vez de dejarlo magenta.
            mat.shader = FindBestShader();
            mat.color = color;
            EditorUtility.SetDirty(mat);
        }

        if (mat == null)
        {
            CreateFolderRecursive(dir);
            mat = new Material(FindBestShader());
            mat.color = color;
            // Mate, sin reflejos: es lo que da el aspecto low-poly de las
            // imágenes del GDD en vez de plástico brillante.
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.04f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.04f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(mat, path);
        }

        // Sin brillo ni reflejos: el Standard, aun "mate", pinta un velo gris
        // plateado al mirar las superficies de costado, y eso era lo que hacía
        // ver el pueblo brilloso en vez de claro como el Figma.
        if (mat.HasProperty("_SpecularHighlights"))
        {
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_GlossyReflections", 0f);
            mat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            mat.SetFloat("_Glossiness", 0f);
            EditorUtility.SetDirty(mat);
        }

        MaterialCache[key] = mat;
        return mat;
    }

    private static void SetColor(GameObject primitive, Color color)
    {
        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer == null) return;
        renderer.sharedMaterial = GetMaterial(color);
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    // Espejos rápidos de UIPalette (evita depender del orden de compilación
    // entre Assembly-CSharp y Assembly-CSharp-Editor para las constantes).
    private static Color UIPalette_Amber() => HexColor("#f6b93b");
    private static Color UIPalette_AmberDrop() => HexColor("#a8621a");
    private static Color UIPalette_Gold() => HexColor("#f5c400");
    private static Color UIPalette_Green() => HexColor("#7be3a0");
    private static Color UIPalette_Blue() => HexColor("#7dd3fc");
    private static Color UIPalette_Cream() => HexColor("#fff2df");
    private static Color UIPalette_CreamSoft() => HexColor("#fdf3e2");
    private static Color UIPalette_TextMuted() => HexColor("#cbd5df");
    private static Color UIPalette_PanelDark()
    {
        Color c = HexColor("#0E1610");
        c.a = 0.78f;
        return c;
    }
}
