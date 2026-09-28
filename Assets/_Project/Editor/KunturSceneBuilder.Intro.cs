using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// v55b: escena "Intro" (la historia antes de la primera partida) y el cuadro
// del tutorial que aparece al entrar al juego con Kuntur saludando.
public static partial class KunturSceneBuilder
{
    private const string IntroArtDir = ArtDir + "/UI/Intro";

    private static readonly string[] IntroTexts =
    {
        "Huancayo fue una ciudad limpia.",
        "Poco a poco, la basura llegó.",
        "El río se llenó de desechos.",
        "Hasta que Kuntur despertó.",
        "Kuntur ayudará a limpiar.",
        "Es hora de comenzar la aventura.",
    };

    private static void BuildIntroScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camGO = new GameObject("Main Camera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();

        GameObject canvasGO = new GameObject("Intro_Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Fondo negro fijo de base.
        Image black = MakePanel(canvasGO.transform, "Fondo_Negro", Color.black);
        StretchFull(black.rectTransform);

        // Ilustración a pantalla completa, en su propio CanvasGroup.
        RectTransform imageHolder = MakeRect(canvasGO.transform, "Grupo_Imagen");
        StretchFull(imageHolder);
        CanvasGroup imageGroup = imageHolder.gameObject.AddComponent<CanvasGroup>();
        imageGroup.alpha = 0f;
        imageGroup.blocksRaycasts = false;
        Image picture = MakePanel(imageHolder, "Imagen", Color.white);
        StretchFull(picture.rectTransform);
        picture.raycastTarget = false;
        picture.preserveAspect = false;

        // Texto centrado en su propio CanvasGroup.
        RectTransform textHolder = MakeRect(canvasGO.transform, "Grupo_Texto");
        StretchFull(textHolder);
        CanvasGroup textGroup = textHolder.gameObject.AddComponent<CanvasGroup>();
        textGroup.alpha = 0f;
        textGroup.blocksRaycasts = false;
        TextMeshProUGUI text = MakeText(textHolder, "Texto", "", 64f, Color.white, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 300f));
        TMP_FontAsset font = GetMenuFont("Light");
        if (font != null) text.font = font;
        text.characterSpacing = 4f;
        text.raycastTarget = false;

        // Botón "Saltar" abajo a la derecha, siempre visible.
        RectTransform skipHolder = MakeRect(canvasGO.transform, "Boton_Saltar");
        SetRect(skipHolder, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-50f, 44f), new Vector2(220f, 60f));
        Image skipBg = MakeRoundedPanel(skipHolder, "Fondo", new Color(1f, 1f, 1f, 0.12f));
        StretchFull(skipBg.rectTransform);
        Button skip = skipBg.gameObject.AddComponent<Button>();
        skip.targetGraphic = skipBg;
        ColorBlock colors = skip.colors;
        colors.highlightedColor = new Color(1.6f, 1.4f, 0.8f, 1f);
        skip.colors = colors;
        TextMeshProUGUI skipText = MakeText(skipBg.transform, "Texto", "SALTAR  (Esc)", 24f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
        StretchFull(skipText.rectTransform);
        if (font != null) skipText.font = font;
        skipText.characterSpacing = 4f;
        skipText.raycastTarget = false;

        // Audio: música de fondo en bucle + sonido de transición.
        GameObject audioGO = new GameObject("Audio_Intro");
        AudioSource music = audioGO.AddComponent<AudioSource>();
        music.clip = LoadAudio("Musica_Historia");
        music.loop = true;
        music.playOnAwake = false;
        GameObject sfxGO = new GameObject("Audio_Transicion");
        AudioSource sfx = sfxGO.AddComponent<AudioSource>();
        sfx.playOnAwake = false;

        IntroCinematica intro = canvasGO.AddComponent<IntroCinematica>();
        SerializedObject so = new SerializedObject(intro);
        SerializedProperty blocks = so.FindProperty("bloques");
        blocks.arraySize = IntroTexts.Length;
        for (int i = 0; i < IntroTexts.Length; i++)
        {
            SerializedProperty b = blocks.GetArrayElementAtIndex(i);
            b.FindPropertyRelative("texto").stringValue = IntroTexts[i];
            b.FindPropertyRelative("imagen").objectReferenceValue = LoadIntroSprite($"Intro_{i + 1}");
        }
        so.FindProperty("grupoTexto").objectReferenceValue = textGroup;
        so.FindProperty("texto").objectReferenceValue = text;
        so.FindProperty("grupoImagen").objectReferenceValue = imageGroup;
        so.FindProperty("imagen").objectReferenceValue = picture;
        so.FindProperty("botonSaltar").objectReferenceValue = skip;
        so.FindProperty("fuenteMusica").objectReferenceValue = music;
        so.FindProperty("fuenteTransicion").objectReferenceValue = sfx;
        so.FindProperty("sonidoTransicion").objectReferenceValue = LoadAudio("SFX_Transicion");
        so.FindProperty("escenaJuego").stringValue = "Exploracion";
        so.ApplyModifiedProperties();

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        EditorSceneManager.SaveScene(scene, ScenesDir + "/Intro.unity");
    }

    private static Sprite LoadIntroSprite(string name)
    {
        foreach (string ext in new[] { ".jpg", ".png" })
        {
            string path = $"{IntroArtDir}/{name}{ext}";
            if (!System.IO.File.Exists(path)) continue;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
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
        Debug.LogWarning($"[Kuntur] Falta la imagen de la intro {name} en {IntroArtDir}.");
        return null;
    }

    // ---------------------------------------------------------------
    // Cuadro del tutorial (HUD del juego)
    // ---------------------------------------------------------------
    private static void BuildTutorialPanel(Transform root)
    {
        RectTransform host = MakeRect(root, "Tutorial");
        StretchFull(host);
        // El script vive en "Tutorial" (siempre activo, para que su Start
        // corra) y lo que se prende/apaga es "Contenido".
        RectTransform content = MakeRect(host, "Contenido");
        StretchFull(content);

        // Degradé oscuro a la derecha para que el cuadro se lea sobre el pueblo.
        Image shade = MakeIcon(content, "Sombra", UISpriteFactory.HorizontalFade("UI_DegradeHorizontal", Color.white), new Color(0f, 0f, 0f, 0.6f));
        SetRect(shade.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(720f, 0f));
        shade.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        Image card = MakeRoundedPanel(content, "Cuadro", new Color(0.03f, 0.05f, 0.04f, 0.94f));
        SetRect(card.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-50f, 10f), new Vector2(470f, 400f));
        card.gameObject.AddComponent<CanvasGroup>();
        Image frame = MakeIcon(card.transform, "Marco", UISpriteFactory.RoundedPanel("UI_PanelMarco", Color.clear, Color.white, 14f, 3f), new Color(0.96f, 0.77f, 0f, 0.6f));
        StretchFull(frame.rectTransform);
        frame.type = Image.Type.Sliced;

        TextMeshProUGUI header = MakeText(card.transform, "Text_Cabecera", "TUTORIAL", 13f, UIPalette_Gold(), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        SetRect(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24f, -18f), new Vector2(-48f, 20f));
        header.characterSpacing = 6f;

        TextMeshProUGUI page = MakeText(card.transform, "Text_Pagina", "1 / 13", 13f, UIPalette_TextMuted(), TextAlignmentOptions.TopRight, FontStyles.Bold);
        SetRect(page.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24f, -18f), new Vector2(120f, 20f));

        // La tecla, grande, en una pastilla dorada.
        Image chip = MakeRoundedPanel(card.transform, "Tecla", UIPalette_Amber());
        SetRect(chip.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24f, -50f), new Vector2(240f, 50f));
        TextMeshProUGUI keys = MakeText(chip.transform, "Text_Tecla", "W A S D", 22f, HexColor("#2a1a05"), TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(keys.rectTransform);
        keys.enableAutoSizing = true;
        keys.fontSizeMin = 12f;
        keys.fontSizeMax = 24f;
        keys.characterSpacing = 3f;

        TextMeshProUGUI title = MakeText(card.transform, "Text_Titulo", "Caminar", 28f, UIPalette_Cream(), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24f, -114f), new Vector2(-48f, 40f));

        TextMeshProUGUI body = MakeText(card.transform, "Text_Cuerpo", "", 19f, new Color(1f, 1f, 1f, 0.88f), TextAlignmentOptions.TopLeft);
        SetRect(body.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24f, -160f), new Vector2(-48f, 150f));
        body.lineSpacing = 6f;

        Button back = MakePauseButton(card.transform, "Boton_Volver", "VOLVER", new Vector2(-112f, 22f), false);
        Button next = MakePauseButton(card.transform, "Boton_Siguiente", "SIGUIENTE", new Vector2(112f, 22f), true);
        foreach (Button b in new[] { back, next })
        {
            RectTransform h = (RectTransform)b.transform.parent;
            h.sizeDelta = new Vector2(200f, 52f);
        }

        TextMeshProUGUI skipText = MakeText(content, "Text_Saltar", "Saltar tutorial", 15f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Right);
        SetRect(skipText.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-54f, -222f), new Vector2(200f, 26f));
        Button skip = skipText.gameObject.AddComponent<Button>();
        skip.targetGraphic = skipText;
        skipText.raycastTarget = true;

        TutorialUI tutorial = host.gameObject.AddComponent<TutorialUI>();
        SerializedObject so = new SerializedObject(tutorial);
        so.FindProperty("panelRoot").objectReferenceValue = content.gameObject;
        so.FindProperty("card").objectReferenceValue = card.rectTransform;
        so.FindProperty("keysText").objectReferenceValue = keys;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.FindProperty("pageText").objectReferenceValue = page;
        so.FindProperty("nextButton").objectReferenceValue = next;
        so.FindProperty("nextLabel").objectReferenceValue = next.GetComponentInChildren<TextMeshProUGUI>();
        so.FindProperty("backButton").objectReferenceValue = back;
        so.FindProperty("skipButton").objectReferenceValue = skip;
        so.ApplyModifiedProperties();
    }
}
