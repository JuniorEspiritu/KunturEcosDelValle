using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

// Los días de Kuntur: su casa en una esquina del pueblo (de ahí sale cada
// mañana y ahí vuelve a descansar), la pantalla de "la noche pasa" y el
// DayManager que lo conecta todo. Más gente en las calles y en el parque.
public static partial class KunturSceneBuilder
{
    // Esquina sur-oeste del pueblo, al lado del Jr. Arequipa, mirando a la calle.
    private static readonly Vector3 HomeCenter = new Vector3(-92.6f, 0f, -111f);
    private const float HomeSizeX = 8f;
    private const float HomeSizeZ = 9f;
    // En la vereda del Jr. Arequipa, al lado de la casa, mirando al norte
    // (hacia el pueblo): así la cámara, que va detrás, no queda dentro de la casa.
    private static readonly Vector3 HomeSpawn = new Vector3(-84.6f, 0f, -108.5f);

    // ---------------------------------------------------------------
    // Casa de Kuntur
    // ---------------------------------------------------------------
    private static void BuildKunturHome(Transform parent)
    {
        Vector3 c = HomeCenter;
        const float h = 3.3f;
        float front = c.x + HomeSizeX / 2f; // la fachada mira al este (+X), al Jr. Arequipa

        GameObject home = new GameObject("Casa_de_Kuntur");
        home.transform.SetParent(parent);
        home.transform.position = c;
        Transform t = home.transform;

        Color wall = HexColor("#eadcbf");
        Color trim = HexColor("#7a4b2a");
        Color door = HexColor("#2f6db5");
        Color tile = HexColor("#b5462f");
        Color tileDark = HexColor("#8e3322");
        Color wood = HexColor("#5a3a24");

        PrimitiveObject(t, "Muros", PrimitiveType.Cube, c + new Vector3(0f, h / 2f, 0f), new Vector3(HomeSizeX, h, HomeSizeZ), wall);
        PrimitiveObject(t, "Zocalo", PrimitiveType.Cube, c + new Vector3(0f, 0.25f, 0f), new Vector3(HomeSizeX + 0.12f, 0.5f, HomeSizeZ + 0.12f), HexColor("#9c7b5c"));
        PrimitiveObject(t, "Viga", PrimitiveType.Cube, c + new Vector3(0f, h - 0.12f, 0f), new Vector3(HomeSizeX + 0.2f, 0.24f, HomeSizeZ + 0.2f), trim);

        // Puerta azul con marco de madera.
        Vector3 d = new Vector3(front, 0f, c.z);
        PrimitiveObject(t, "Puerta", PrimitiveType.Cube, d + new Vector3(0.03f, 1.15f, 0f), new Vector3(0.08f, 2.3f, 1.3f), door);
        PrimitiveObject(t, "Marco_Sup", PrimitiveType.Cube, d + new Vector3(0.06f, 2.4f, 0f), new Vector3(0.12f, 0.2f, 1.7f), wood);
        PrimitiveObject(t, "Marco_Izq", PrimitiveType.Cube, d + new Vector3(0.06f, 1.2f, -0.75f), new Vector3(0.12f, 2.4f, 0.2f), wood);
        PrimitiveObject(t, "Marco_Der", PrimitiveType.Cube, d + new Vector3(0.06f, 1.2f, 0.75f), new Vector3(0.12f, 2.4f, 0.2f), wood);
        PrimitiveObject(t, "Perilla", PrimitiveType.Sphere, d + new Vector3(0.1f, 1.1f, 0.42f), Vector3.one * 0.1f, HexColor("#f2c230"));
        // Escalón y caminito hasta la vereda.
        PrimitiveObject(t, "Escalon", PrimitiveType.Cube, d + new Vector3(0.45f, 0.08f, 0f), new Vector3(0.9f, 0.16f, 1.8f), HexColor("#b9b5ac"));
        PrimitiveObject(t, "Caminito", PrimitiveType.Cube, d + new Vector3(1.6f, 0.02f, 0f), new Vector3(1.6f, 0.04f, 1.4f), HexColor("#cdbf9f"));

        // Ventanas con marco y macetas con flores.
        foreach (float dz in new[] { -2.6f, 2.6f })
        {
            Vector3 w = new Vector3(front, 1.6f, c.z + dz);
            PrimitiveObject(t, "Ventana", PrimitiveType.Cube, w + new Vector3(0.03f, 0f, 0f), new Vector3(0.06f, 1.1f, 1.3f), HexColor("#9cc8e6"));
            PrimitiveObject(t, "Ventana_Marco", PrimitiveType.Cube, w + new Vector3(0.05f, 0.62f, 0f), new Vector3(0.1f, 0.12f, 1.5f), wood);
            PrimitiveObject(t, "Ventana_Marco", PrimitiveType.Cube, w + new Vector3(0.05f, -0.62f, 0f), new Vector3(0.18f, 0.12f, 1.5f), wood);
            PrimitiveObject(t, "Ventana_Cruz", PrimitiveType.Cube, w + new Vector3(0.07f, 0f, 0f), new Vector3(0.06f, 1.1f, 0.07f), wood);
            PrimitiveObject(t, "Maceta", PrimitiveType.Cube, w + new Vector3(0.25f, -0.8f, 0f), new Vector3(0.35f, 0.25f, 1.2f), HexColor("#a0522d"));
            for (int k = 0; k < 4; k++)
                PrimitiveObject(t, "Flor", PrimitiveType.Sphere, w + new Vector3(0.25f, -0.6f, -0.45f + k * 0.3f), Vector3.one * 0.22f,
                    k % 2 == 0 ? HexColor("#e74c3c") : HexColor("#f1c40f"));
        }

        // Letrero de madera junto a la puerta.
        Vector3 board = new Vector3(front + 0.1f, 2.85f, c.z);
        PrimitiveObject(t, "Letrero", PrimitiveType.Cube, board, new Vector3(0.1f, 0.5f, 2.6f), wood);
        TextMeshPro label = MakeLogoText(t, "Letrero_Texto", "CASA DE KUNTUR", 3.2f, HexColor("#fff2c6"),
            board + new Vector3(0.07f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(2.5f, 0.5f);

        // Techo de tejas a dos aguas (cumbrera a lo largo de Z).
        const float slope = 26f;
        float rise = (HomeSizeX / 2f) * Mathf.Tan(slope * Mathf.Deg2Rad);
        float half = HomeSizeX / 2f + 0.5f;
        float panel = half / Mathf.Cos(slope * Mathf.Deg2Rad);
        float ridgeY = h + rise;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 center = new Vector3(c.x + side * half / 2f, ridgeY - half * Mathf.Tan(slope * Mathf.Deg2Rad) / 2f + 0.08f, c.z);
            PrimitiveObject(t, "Techo", PrimitiveType.Cube, center, new Vector3(panel, 0.14f, HomeSizeZ + 0.9f), tile,
                Quaternion.Euler(0f, 0f, -side * slope));
            for (int r = 1; r <= 3; r++)
            {
                float f = r / 4f;
                Vector3 p = new Vector3(c.x + side * half * f, ridgeY - half * f * Mathf.Tan(slope * Mathf.Deg2Rad) + 0.17f, c.z);
                PrimitiveObject(t, "Tejas", PrimitiveType.Cube, p, new Vector3(0.12f, 0.05f, HomeSizeZ + 0.9f), tileDark,
                    Quaternion.Euler(0f, 0f, -side * slope));
            }
        }
        PrimitiveObject(t, "Cumbrera", PrimitiveType.Cylinder, new Vector3(c.x, ridgeY + 0.16f, c.z),
            new Vector3(0.28f, (HomeSizeZ + 0.9f) / 2f, 0.28f), tileDark, Quaternion.Euler(90f, 0f, 0f));
        Mesh gable = BuildGableMesh(HomeSizeZ, HomeSizeX, rise);
        MeshObject(t, "Hastiales", gable, new Vector3(c.x, h, c.z), Vector3.one, wall, Quaternion.Euler(0f, 90f, 0f));

        // Farolito sobre la puerta (se prende de noche con el presupuesto de luces).
        PrimitiveObject(t, "Farolito", PrimitiveType.Cube, d + new Vector3(0.25f, 2.75f, -1.1f), new Vector3(0.22f, 0.3f, 0.22f), HexColor("#ffe7a8"));
        AddParkLampLight(home.transform, d + new Vector3(0.9f, 2.6f, 0f));

        BoxCollider box = home.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, h / 2f, 0f);
        box.size = new Vector3(HomeSizeX, h, HomeSizeZ);

        SetStaticRecursive(home, true);
        Occupy(c, HomeSizeX + 1f, HomeSizeZ + 1f);
        Occupy(new Vector3(front + 1.6f, 0f, c.z), 3.2f, 2.4f); // el caminito a la puerta queda libre
    }

    // La puerta: "E  Descansar".
    private static void BuildHomeDoor(Transform parent, int interactableLayer)
    {
        GameObject doorGO = new GameObject("Puerta_Casa_Kuntur");
        doorGO.transform.SetParent(parent);
        doorGO.transform.position = new Vector3(HomeCenter.x + HomeSizeX / 2f + 0.6f, 1f, HomeCenter.z);
        doorGO.layer = interactableLayer;
        BoxCollider col = doorGO.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.4f, 2f, 1.8f);
        doorGO.AddComponent<HomeRest>();

        // Punto naranja en el mapa: aquí vive Kuntur.
        AddMapIcon(doorGO.transform, new Vector3(HomeCenter.x, 0f, HomeCenter.z), HexColor("#ff9f43"), 6f);
    }

    // ---------------------------------------------------------------
    // DayManager + pantalla de "la noche pasa"
    // ---------------------------------------------------------------
    private static void BuildDaySystem(Transform canvasRoot, GameObject managersGO)
    {
        if (managersGO == null) return;

        // Dónde aparece cada mañana: en el caminito, frente a la puerta.
        GameObject spawn = new GameObject("Aparicion_Casa");
        spawn.transform.position = HomeSpawn;
        spawn.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // mirando al pueblo

        GameObject playerGO = GameObject.Find("Player");
        SimpleThirdPersonController player = playerGO != null ? playerGO.GetComponent<SimpleThirdPersonController>() : null;
        if (playerGO != null) playerGO.transform.SetPositionAndRotation(HomeSpawn + Vector3.up * 0.05f, spawn.transform.rotation);

        // ---- Etiqueta "DÍA 1" sobre el reloj ----
        Image dayTab = MakeRoundedPanel(canvasRoot, "Panel_Dia", UIPalette_PanelDark());
        SetRect(dayTab.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16f, 84f), new Vector2(110f, 28f));
        TextMeshProUGUI dayLabel = MakeText(dayTab.transform, "Text_Dia", "DÍA 1", 15f, UIPalette_Gold(), TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(dayLabel.rectTransform);

        // ---- Barra de ENERGÍA (v55: abajo al centro, como la estamina) ----
        Image energyPanel = MakeRoundedPanel(canvasRoot, "Panel_Energia", new Color(0f, 0f, 0f, 0.55f));
        SetRect(energyPanel.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0f, 16f), new Vector2(320f, 36f));
        CanvasGroup energyGroup = energyPanel.gameObject.AddComponent<CanvasGroup>();
        Image bolt = MakeIcon(energyPanel.transform, "Icono_Rayo", UISpriteFactory.Bolt("UI_Rayo", Color.white), HexColor("#ffd140"));
        SetRect(bolt.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20f, 0f), new Vector2(24f, 24f));
        TextMeshProUGUI energyLabel = MakeText(energyPanel.transform, "Text_Energia", "ENERGÍA", 10f, UIPalette_Cream(), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        SetRect(energyLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(38f, -4f), new Vector2(-50f, 14f));
        energyLabel.characterSpacing = 3f;
        Image energyBg = MakeRoundedPanel(energyPanel.transform, "Barra_Fondo", new Color(1f, 1f, 1f, 0.12f));
        SetRect(energyBg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(38f, 7f), new Vector2(-50f, 9f));
        Image energyFill = MakeRoundedPanel(energyBg.transform, "Barra_Relleno", HexColor("#ffd140"));
        energyFill.type = Image.Type.Filled;
        energyFill.fillMethod = Image.FillMethod.Horizontal;
        energyFill.fillAmount = 1f;
        StretchFull(energyFill.rectTransform);
        // Marquitas cada cuarto de barra.
        for (int i = 1; i < 4; i++)
        {
            Image tickMark = MakePanel(energyBg.transform, "Marca", new Color(0f, 0f, 0f, 0.35f));
            SetRect(tickMark.rectTransform, new Vector2(i / 4f, 0f), new Vector2(i / 4f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 0f));
        }
        EnergyBarUI energyUi = energyPanel.gameObject.AddComponent<EnergyBarUI>();
        SerializedObject eso = new SerializedObject(energyUi);
        eso.FindProperty("fill").objectReferenceValue = energyFill;
        eso.FindProperty("label").objectReferenceValue = energyLabel;
        eso.FindProperty("bolt").objectReferenceValue = bolt.rectTransform;
        eso.FindProperty("boltImage").objectReferenceValue = bolt;
        eso.FindProperty("group").objectReferenceValue = energyGroup;
        eso.ApplyModifiedProperties();

        // ---- Pantalla de la noche ----
        // Cielo que se va aclarando, estrellas que titilan, la luna cruzando,
        // una estrella fugaz, los cerros, la casita de Kuntur con "Zzz" y, al
        // final, el sol saliendo. Lo anima NightSkyAnimator.
        Image overlay = MakePanel(canvasRoot, "Panel_Noche", HexColor("#0b1330"));
        StretchFull(overlay.rectTransform);
        CanvasGroup group = overlay.gameObject.AddComponent<CanvasGroup>();

        Sprite circle = UISpriteFactory.Circle("UI_Circulo", Color.white, Color.clear, 0f);
        System.Random rng = new System.Random(2109);
        var stars = new List<Image>();
        for (int i = 0; i < 70; i++)
        {
            Image star = MakeIcon(overlay.transform, "Estrella", circle, new Color(1f, 1f, 1f, NextFloat(rng, 0.35f, 0.95f)));
            float size = NextFloat(rng, 2f, 6f);
            Vector2 anchor = new Vector2(NextFloat(rng, 0.02f, 0.98f), NextFloat(rng, 0.35f, 0.98f));
            SetRect(star.rectTransform, anchor, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            stars.Add(star);
        }

        RectTransform Glow(Transform parent, string name, Color core, Color halo, float size)
        {
            GameObject holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            RectTransform rt = holder.GetComponent<RectTransform>();
            SetRect(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            holder.AddComponent<CanvasGroup>();
            Image h2 = MakeIcon(holder.transform, "Halo2", circle, new Color(halo.r, halo.g, halo.b, 0.12f));
            SetRect(h2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 2.4f, size * 2.4f));
            Image h1 = MakeIcon(holder.transform, "Halo", circle, new Color(halo.r, halo.g, halo.b, 0.25f));
            SetRect(h1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.5f, size * 1.5f));
            Image c = MakeIcon(holder.transform, "Disco", circle, core);
            StretchFull(c.rectTransform);
            return rt;
        }

        RectTransform sun = Glow(overlay.transform, "Sol", HexColor("#ffd36b"), HexColor("#ffb347"), 150f);
        RectTransform moon = Glow(overlay.transform, "Luna", HexColor("#f4ecd0"), HexColor("#cfe0ff"), 96f);
        Image moonBite = MakeIcon(moon, "Sombra", circle, HexColor("#0b1330"));
        SetRect(moonBite.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 18f), new Vector2(86f, 86f));

        // Estrella fugaz.
        Image shooting = MakePanel(overlay.transform, "Estrella_Fugaz", new Color(1f, 1f, 1f, 0.9f));
        SetRect(shooting.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(90f, 3f));
        shooting.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f);
        shooting.gameObject.SetActive(false);

        // Cerros: rombos grandes que asoman desde abajo (dos filas).
        (float x, float size, string col)[] peaks =
        {
            (-560f, 520f, "#101a3a"), (-240f, 620f, "#101a3a"), (120f, 560f, "#101a3a"), (470f, 640f, "#101a3a"),
            (-420f, 420f, "#070d22"), (-60f, 460f, "#070d22"), (300f, 400f, "#070d22"), (620f, 480f, "#070d22"),
        };
        foreach (var pk in peaks)
        {
            Image peak = MakePanel(overlay.transform, "Cerro", HexColor(pk.col));
            SetRect(peak.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(pk.x, -pk.size * 0.15f), new Vector2(pk.size, pk.size));
            peak.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        Image ground = MakePanel(overlay.transform, "Suelo", HexColor("#050a18"));
        SetRect(ground.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 70f));

        // La casita de Kuntur con la ventana encendida.
        Image house = MakePanel(overlay.transform, "Casita", HexColor("#1c2340"));
        SetRect(house.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(-340f, -300f), new Vector2(110f, 72f));
        Image roof = MakePanel(house.transform, "Techo", HexColor("#5a1f18"));
        SetRect(roof.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(80f, 80f));
        roof.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        roof.transform.SetAsFirstSibling();
        Image window = MakePanel(house.transform, "Ventana", HexColor("#ffd97a"));
        SetRect(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(18f, 2f), new Vector2(24f, 22f));

        var zzz = new List<RectTransform>();
        foreach (string z in new[] { "Z", "z", "Z" })
        {
            TextMeshProUGUI zt = MakeText(overlay.transform, "Zzz", z, 30f, UIPalette_CreamSoft(), TextAlignmentOptions.Center, FontStyles.Bold);
            SetRect(zt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            zzz.Add(zt.rectTransform);
        }

        NightSkyAnimator skyAnim = overlay.gameObject.AddComponent<NightSkyAnimator>();
        SerializedObject nso = new SerializedObject(skyAnim);
        nso.FindProperty("sky").objectReferenceValue = overlay;
        nso.FindProperty("moon").objectReferenceValue = moon;
        nso.FindProperty("sun").objectReferenceValue = sun;
        nso.FindProperty("shootingStar").objectReferenceValue = shooting.rectTransform;
        SerializedProperty starsProp = nso.FindProperty("stars");
        starsProp.arraySize = stars.Count;
        for (int i = 0; i < stars.Count; i++) starsProp.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
        SerializedProperty zzzProp = nso.FindProperty("zzz");
        zzzProp.arraySize = zzz.Count;
        for (int i = 0; i < zzz.Count; i++) zzzProp.GetArrayElementAtIndex(i).objectReferenceValue = zzz[i];
        nso.FindProperty("duration").floatValue = 13f;
        nso.ApplyModifiedProperties();

        TextMeshProUGUI title = MakeText(overlay.transform, "Text_Titulo", "Buenas noches, Kuntur", 46f, UIPalette_Gold(), TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 60f));
        TextMeshProUGUI subtitle = MakeText(overlay.transform, "Text_Subtitulo", "La noche pasa...", 22f, UIPalette_CreamSoft(), TextAlignmentOptions.Center);
        SetRect(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(900f, 34f));
        TextMeshProUGUI clock = MakeText(overlay.transform, "Text_Reloj", "9:00 PM", 64f, UIPalette_Cream(), TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(clock.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(600f, 80f));
        TextMeshProUGUI saved = MakeText(overlay.transform, "Text_Guardado", "", 16f, UIPalette_Green(), TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(saved.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(600f, 26f));

        Image advance = MakeRoundedPanel(overlay.transform, "Boton_Avanzar", UIPalette_Amber());
        SetRect(advance.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(240f, 56f));
        Button advanceButton = advance.gameObject.AddComponent<Button>();
        advanceButton.targetGraphic = advance;
        TextMeshProUGUI advanceText = MakeText(advance.transform, "Text", "AVANZAR", 24f, HexColor("#2a1a05"), TextAlignmentOptions.Center, FontStyles.Bold);
        advanceText.text = "AVANZAR";
        StretchFull(advanceText.rectTransform);
        TextMeshProUGUI advanceHint = MakeText(overlay.transform, "Text_AyudaAvanzar", "Enter / E", 13f, UIPalette_TextMuted(), TextAlignmentOptions.Center);
        SetRect(advanceHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -184f), new Vector2(300f, 20f));
        advanceHint.transform.SetParent(advance.transform, true);

        overlay.gameObject.SetActive(false);

        DayManager days = managersGO.AddComponent<DayManager>();
        SerializedObject so = new SerializedObject(days);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("homeSpawn").objectReferenceValue = spawn.transform;
        so.FindProperty("nightOverlay").objectReferenceValue = group;
        so.FindProperty("nightTitle").objectReferenceValue = title;
        so.FindProperty("nightSubtitle").objectReferenceValue = subtitle;
        so.FindProperty("nightClock").objectReferenceValue = clock;
        so.FindProperty("advanceButton").objectReferenceValue = advance.gameObject;
        so.FindProperty("savedText").objectReferenceValue = saved;
        so.FindProperty("dayLabel").objectReferenceValue = dayLabel;
        so.FindProperty("nightSky").objectReferenceValue = skyAnim;
        so.ApplyModifiedProperties();
        UnityEventTools.AddPersistentListener(advanceButton.onClick, days.Advance);
    }

    // ---------------------------------------------------------------
    // Más gente: una persona por cuadra en todas las veredas del pueblo
    // ---------------------------------------------------------------
    private static void BuildMorePedestrians(Transform parent)
    {
        GameObject root = new GameObject("Peatones_Barrio");
        root.transform.SetParent(parent);
        System.Random rng = new System.Random(8080);

        var blocksZ = new List<float>(CrossStreetsZ) { AvenueZ, SideStreetSouthZ, SideStreetNorthZ };
        blocksZ.Sort();
        var streets = new (float x, float off)[]
        {
            (MainStreetX, 5.4f), (PunoX, CrossStreetHalfRoad + SidewalkWidth / 2f),
            (CuscoX, CrossStreetHalfRoad + SidewalkWidth / 2f), (ArequipaX, CrossStreetHalfRoad + SidewalkWidth / 2f),
        };

        int n = 0;
        foreach (var st in streets)
        {
            for (int b = 0; b + 1 < blocksZ.Count; b++)
            {
                float z0 = blocksZ[b] + 7.5f, z1 = blocksZ[b + 1] - 7.5f;
                if (z1 - z0 < 6f) continue;
                float side = (n % 2 == 0) ? 1f : -1f;
                Vector3 a = new Vector3(st.x + side * st.off, 0f, z0);
                Vector3 bb = new Vector3(st.x + side * st.off, 0f, z1);
                if (rng.Next(2) == 0) (a, bb) = (bb, a);
                BuildMaybeGiverPedestrian(root.transform, $"Peaton_Barrio_{n++}", a, bb, rng);
            }
        }

        // Jirones (este-oeste): una persona cada dos cuadras.
        float[] blocksX = { CrossStreetFromX, ArequipaX, PunoX, MainStreetX, CuscoX, CrossStreetToX };
        int k = 0;
        foreach (float z in CrossStreetsZ)
        {
            for (int b = 0; b + 1 < blocksX.Length; b++, k++)
            {
                if (k % 2 == 1) continue;
                float x0 = blocksX[b] + 7.5f, x1 = blocksX[b + 1] - 7.5f;
                if (x1 - x0 < 6f) continue;
                float side = (k % 4 == 0) ? 1f : -1f;
                float off = CrossStreetHalfRoad + SidewalkWidth / 2f;
                BuildMaybeGiverPedestrian(root.transform, $"Peaton_Jiron_{n++}",
                    new Vector3(x0, 0f, z + side * off), new Vector3(x1, 0f, z + side * off), rng);
            }
        }

        BuildExtraCrowd(root.transform, rng, ref n);
    }

    // ---------------------------------------------------------------
    // v57: los huecos donde no pasaba nadie
    // ---------------------------------------------------------------
    // Quedaban calles enteras vacías: la Av. Giráldez no tenía un solo
    // peatón (el reparto de arriba solo camina por las calles norte-sur y por
    // los jirones), en los jirones se saltaba una cuadra de cada dos, y las
    // esquinas de la plaza y de la bodega estaban desiertas. Acá se llenan
    // esos huecos, que es donde más se nota un pueblo deshabitado.
    private static void BuildExtraCrowd(Transform parent, System.Random rng, ref int n)
    {
        // --- Av. Giráldez (este-oeste), las dos veredas ---
        float avenueOff = AvenueHalf + SidewalkWidth / 2f;
        float[] avenueX = { ArequipaX, PunoX, MainStreetX, CuscoX };
        for (int b = 0; b + 1 < avenueX.Length; b++)
        {
            float x0 = avenueX[b] + 7.5f, x1 = avenueX[b + 1] - 7.5f;
            if (x1 - x0 < 6f) continue;
            foreach (float side in new[] { 1f, -1f })
            {
                float z = AvenueZ + side * avenueOff;
                BuildMaybeGiverPedestrian(parent, $"Peaton_Giraldez_{n++}",
                    new Vector3(x0, 0f, z), new Vector3(x1, 0f, z), rng);
            }
        }

        // --- Las cuadras de los jirones que antes se saltaban ---
        float[] blocksX = { CrossStreetFromX, ArequipaX, PunoX, MainStreetX, CuscoX, CrossStreetToX };
        float crossOff = CrossStreetHalfRoad + SidewalkWidth / 2f;
        int k = 0;
        foreach (float z in CrossStreetsZ)
        {
            for (int b = 0; b + 1 < blocksX.Length; b++, k++)
            {
                if (k % 2 == 0) continue;               // las que ya tienen a alguien
                float x0 = blocksX[b] + 7.5f, x1 = blocksX[b + 1] - 7.5f;
                if (x1 - x0 < 6f) continue;
                float side = (k % 4 == 1) ? -1f : 1f;   // la vereda de enfrente
                BuildMaybeGiverPedestrian(parent, $"Peaton_Jiron2_{n++}",
                    new Vector3(x0, 0f, z + side * crossOff), new Vector3(x1, 0f, z + side * crossOff), rng);
            }
        }

        // --- Grupitos parados conversando donde la gente de verdad se junta ---
        // (las cuatro esquinas de la Plaza Constitución y la vereda de la
        // Calle Real, que es por donde pasa todo el mundo).
        float px = ParkSizeX / 2f + 1.6f, pz = ParkSizeZ / 2f + 1.6f;
        Vector3[] corners =
        {
            ParkCenter + new Vector3(-px, 0f, pz), ParkCenter + new Vector3(px, 0f, pz),
            ParkCenter + new Vector3(-px, 0f, -pz), ParkCenter + new Vector3(px, 0f, -pz),
            new Vector3(MainStreetX + MainStreetHalf + SidewalkWidth / 2f, 0f, 22f),
            new Vector3(MainStreetX - MainStreetHalf - SidewalkWidth / 2f, 0f, -38f),
        };

        Color[] ponchos =
        {
            HexColor("#b03a2e"), HexColor("#1f618d"), HexColor("#6c3483"),
            HexColor("#117864"), HexColor("#ba4a00"), HexColor("#7d6608"),
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 a = corners[i];
            Vector3 b = corners[i] + new Vector3(0.95f, 0f, 0.65f);
            GameObject one = BuildVillager(parent, $"Corrillo_{i}A", a, ponchos[i % ponchos.Length], rng);
            GameObject two = BuildVillager(parent, $"Corrillo_{i}B", b, ponchos[(i + 2) % ponchos.Length], rng);
            if (one == null || two == null) continue;
            LinkVillagers(one, two, 0f);
            LinkVillagers(two, one, 3.1f);  // desfasado: hablan por turnos
        }
    }

    // Gente que va caminando por la calle y que TAMBIÉN puede pedirte ayuda:
    // uno de cada cinco peatones. Cuando le toca, se detiene y te espera.
    private static readonly (string name, bool female, string prefab, string role, string voice)[] WalkingGivers =
    {
        ("Doña Zoraida", true, "elder/elder_Female_A.prefab", "VECINA · VENDE EMOLIENTE EN LA REAL", "mother"),
        ("Julius", false, "city/casual_Male_G.prefab", "CHOFER DE LA COMBI A CHILCA", "young"),
        ("Milagros", true, "downtown/casual_Female_K.prefab", "ESTUDIANTE DEL CONTINENTAL", "young"),
        ("Wilder Quispe", false, "worker_Male_constructor_B.prefab", "VECINO · CARPINTERO DE COCHAS", "father"),
        ("Señora Betsabé", true, "city/casual_Female_G.prefab", "COMERCIANTE DE LA FERIA DOMINICAL", "mother"),
        ("Jhonatan", false, "downtown/casual_Male_K.prefab", "ESTUDIANTE DEL POLITÉCNICO", "young"),
        ("Doña Aurelia", true, "elder/elder_Female_A.prefab", "VECINA · COCINERA, HACE PAPA A LA HUANCAÍNA", "mother"),
        ("Doctor Édgar", false, "professions/Doctor_Male_B.prefab", "MÉDICO DEL HOSPITAL CARRIÓN", "teacher"),
        ("Señora Rosmery", true, "city/casual_Female_G.prefab", "VECINA · TEJEDORA DE CHOMPAS", "mother"),
        ("Don Percy", false, "city/casual_Male_G.prefab", "VECINO · JUBILADO, CRÍA CUYES", "father"),
    };

    private static int walkingGiverCounter;
    private static int pedestrianGiverTick;

    private static void BuildMaybeGiverPedestrian(Transform parent, string name, Vector3 a, Vector3 b, System.Random rng)
    {
        bool giver = pedestrianGiverTick++ % 5 == 2 && walkingGiverCounter < WalkingGivers.Length;
        if (!giver)
        {
            TryBuildAssetPedestrian(parent, name, a, b, rng);
            return;
        }

        var g = WalkingGivers[walkingGiverCounter++];
        GameObject person = BuildAssetPedestrian(parent, name + "_" + g.name.Replace(" ", "_"), a, b, rng, PeopleDir + "/Prefabs/" + g.prefab);
        if (person != null) MakeMissionGiver(person, g.name, g.female, g.role, interactableLayerForGivers, "", g.voice);
    }

    // ---------------------------------------------------------------
    // Gente en el parque: sentada en las bancas y paseando
    // ---------------------------------------------------------------
    private static void BuildParkPeople(Transform parent)
    {
        GameObject root = new GameObject("Gente_Parque");
        root.transform.SetParent(parent);
        System.Random rng = new System.Random(5150);
        Vector3 c = ParkCenter;

        // Paseando por los caminos del parque.
        TryBuildAssetPedestrian(root.transform, "Paseante_1", c + new Vector3(-8.6f, 0.1f, -0.7f), c + new Vector3(-3.4f, 0.1f, -0.7f), rng);
        TryBuildAssetPedestrian(root.transform, "Paseante_2", c + new Vector3(0.7f, 0.1f, 6.6f), c + new Vector3(0.7f, 0.1f, 3.2f), rng);
        TryBuildAssetPedestrian(root.transform, "Paseante_3", c + new Vector3(3.4f, 0.1f, 0.7f), c + new Vector3(8.8f, 0.1f, 0.7f), rng);
        TryBuildAssetPedestrian(root.transform, "Paseante_4", c + new Vector3(-0.7f, 0.1f, -6.4f), c + new Vector3(-0.7f, 0.1f, -3.2f), rng);

        // Sentados en tres de las cuatro bancas (mirando a la pileta).
        Vector3[] benches =
        {
            c + new Vector3(-3.6f, 0.1f, 2.4f), c + new Vector3(3.6f, 0.1f, -2.4f), c + new Vector3(-3.6f, 0.1f, -2.4f),
        };
        for (int i = 0; i < benches.Length; i++)
        {
            Vector3 toCenter = c - benches[i]; toCenter.y = 0f;
            float yaw = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
            string[] adults = { "elder/elder_Female_A.prefab", "city/casual_Male_G.prefab", "city/casual_Female_G.prefab" };
            BuildSittingPerson(root.transform, $"Sentado_{i + 1}", benches[i] + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -0.05f), yaw,
                PeopleDir + "/Prefabs/" + adults[i % adults.Length], SitTests[i % SitTests.Length].hip, SitTests[i % SitTests.Length].knee);
        }
    }

    // Persona sentada: el asset no trae animación de sentarse, así que se
    // arma un clip humanoide con las rodillas y la cadera dobladas.
    // Probado en las fotos de la escena: cadera -1 (muslo hacia adelante) y
    // rodilla 0 (doblada en ángulo recto) es la pose de sentado en la banca.
    private static readonly (float hip, float knee)[] SitTests = { (-1f, 0f) };

    private static void BuildSittingPerson(Transform parent, string name, Vector3 seat, float yaw, string prefab, float hip, float knee)
    {
        if (LoadPrefab(PeoplePrefabs[0]) == null) return;
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(parent);
        holder.transform.SetPositionAndRotation(seat, Quaternion.Euler(0f, yaw, 0f));

        // Solo adultos: el alto del cuerpo sentado depende del largo de las
        // piernas, y un niño con el mismo clip quedaba hundido en la banca.
        GameObject model = BuildPersonModel(holder.transform, "Cuerpo", Vector3.zero, false, out Animator animator, out _, out _, prefab);
        if (model == null) { Object.DestroyImmediate(holder); return; }
        if (animator != null)
        {
            RuntimeAnimatorController sit = GetSittingController(hip, knee);
            if (sit != null) animator.runtimeAnimatorController = sit;
        }
        SetStaticRecursive(holder, false);
    }

    private static RuntimeAnimatorController sittingController;
    private static readonly Dictionary<string, RuntimeAnimatorController> SitControllers = new Dictionary<string, RuntimeAnimatorController>();

    private static RuntimeAnimatorController GetSittingController(float hip, float knee)
    {
        string key = $"Sentado_{hip:0.##}_{knee:0.##}".Replace(",", ".").Replace("-", "m");
        if (SitControllers.TryGetValue(key, out RuntimeAnimatorController cached) && cached != null) return cached;

        string dir = ArtDir + "/Animaciones/Personas";
        CreateFolderRecursive(dir);
        string clipPath = dir + $"/{key}.anim";
        string ctrlPath = dir + $"/{key}.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null) AssetDatabase.DeleteAsset(clipPath);
        if (AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath) != null) AssetDatabase.DeleteAsset(ctrlPath);

        AnimationClip clip = new AnimationClip { name = "Sentado", frameRate = 30f };
        void Muscle(string muscle, float value)
        {
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), muscle);
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f, value));
        }

        // Cuerpo: un poco más abajo (sentado) y derecho.
        Muscle("RootT.x", 0f);
        Muscle("RootT.y", SitRootY);
        Muscle("RootT.z", 0f);
        Muscle("RootQ.x", 0f);
        Muscle("RootQ.y", 0f);
        Muscle("RootQ.z", 0f);
        Muscle("RootQ.w", 1f);

        foreach (string side in new[] { "Left", "Right" })
        {
            Muscle($"{side} Upper Leg Front-Back", hip);
            Muscle($"{side} Upper Leg In-Out", 0f);
            Muscle($"{side} Lower Leg Stretch", knee);
            Muscle($"{side} Foot Up-Down", 0f);
            Muscle($"{side} Arm Down-Up", -0.55f);
            Muscle($"{side} Arm Front-Back", 0.35f);
            Muscle($"{side} Forearm Stretch", 0.35f);
        }
        Muscle("Spine Front-Back", 0.05f);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, clipPath);

        var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var state = controller.layers[0].stateMachine.AddState("Sentado");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        EditorUtility.SetDirty(controller);
        SitControllers[key] = controller;
        return controller;
    }

    // Valores del clip de sentarse (músculos humanoides, de -1 a 1).
    private const float SitRootY = 0.57f;
    private const float SitHip = 0.62f;
    private const float SitKnee = -0.95f;
}
