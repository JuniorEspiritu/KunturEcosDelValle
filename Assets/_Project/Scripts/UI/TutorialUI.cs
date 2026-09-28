using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// v55b: tutorial de la primera partida. Al terminar la intro (Nueva Partida),
// la cámara se pone delante de Kuntur, que saluda con el ala, y al costado
// aparece un cuadro que explica UNA mecánica por página: qué tecla es y qué
// hace. Abajo, VOLVER (por si no quedó claro) y SIGUIENTE; en la última,
// ¡A JUGAR!. También se puede volver a ver desde la pausa.
public class TutorialUI : MonoBehaviour
{
    public static TutorialUI Instance { get; private set; }
    public static bool Showing { get; private set; }
    // v56: el tutorial ya está por abrirse (primera partida). Mientras tanto
    // nadie más saluda ni reparte misiones: todo espera a que termine.
    public static bool Pending { get; private set; }
    public static float ClosedAt { get; private set; } = -100f;

    // Lo pone la intro (IntroCinematica) antes de cargar el juego.
    public const string PendingKey = "Kuntur_TutorialPendiente";

    [System.Serializable]
    public class Page
    {
        public string keys;     // "W A S D", "E", "Shift"...
        public string title;
        [TextArea(2, 5)] public string body;
    }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text keysText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text pageText;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextLabel;
    [SerializeField] private Button backButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private float cameraDistance = 3.6f;
    [SerializeField] private Page[] pages;

    private int index;
    private float pageTime;
    private CanvasGroup cardGroup;
    private Vector2 cardBase;

    private void Awake()
    {
        Instance = this;
        Showing = false;
        Pending = PlayerPrefs.GetInt(PendingKey, 0) == 1;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (nextButton != null) nextButton.onClick.AddListener(Next);
        if (backButton != null) backButton.onClick.AddListener(Back);
        if (skipButton != null) skipButton.onClick.AddListener(Close);
        if (card != null)
        {
            cardBase = card.anchoredPosition;
            cardGroup = card.GetComponent<CanvasGroup>();
        }
        if (pages == null || pages.Length == 0) pages = DefaultPages();
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; Showing = false; Pending = false; }
    }

    private IEnumerator Start()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) != 1) { Pending = false; yield break; }
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        // Que primero entre la escena desde negro.
        yield return new WaitForSeconds(1.2f);
        Open();
        Pending = false;
    }

    public void Open()
    {
        if (Showing || panelRoot == null) return;
        // Del tuk tuk se baja antes (la cámara del tutorial mira a Kuntur).
        if (VehicleSystem.Instance != null) VehicleSystem.Instance.ForceExit();
        Showing = true;
        index = 0;
        // Nada de carteles por detrás mientras se lee el tutorial.
        if (MissionDirector.Instance != null) MissionDirector.Instance.HideBanner();

        if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Resultado);
        SimpleThirdPersonController player = SimpleThirdPersonController.Instance;
        if (player != null && Camera.main != null) player.FaceTowards(Camera.main.transform.position);
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayGreet();
        if (KunturCinematicCamera.Instance != null) KunturCinematicCamera.Instance.StartPortrait(cameraDistance, false);

        panelRoot.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ShowPage();
    }

    public void Close()
    {
        if (!Showing) return;
        Showing = false;
        Pending = false;
        ClosedAt = Time.time;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.StopDance();
        if (KunturCinematicCamera.Instance != null) KunturCinematicCamera.Instance.Stop();
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Resultado)
            GameManager.Instance.SetState(GameState.Exploracion);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Next()
    {
        if (index >= pages.Length - 1) { Close(); return; }
        index++;
        ShowPage();
    }

    public void Back()
    {
        if (index <= 0) return;
        index--;
        ShowPage();
    }

    private void ShowPage()
    {
        Page p = pages[index];
        if (keysText != null) keysText.text = p.keys;
        if (titleText != null) titleText.text = p.title;
        if (bodyText != null) bodyText.text = p.body;
        if (pageText != null) pageText.text = $"{index + 1} / {pages.Length}";
        if (nextLabel != null) nextLabel.text = index >= pages.Length - 1 ? "¡A JUGAR!" : "SIGUIENTE";
        if (backButton != null) backButton.interactable = index > 0;
        pageTime = 0f;
    }

    private void Update()
    {
        if (!Showing) return;

        // Cada página entra deslizándose desde la derecha.
        pageTime += Time.unscaledDeltaTime;
        float x = Mathf.Clamp01(pageTime / 0.35f);
        float e = 1f - (1f - x) * (1f - x) * (1f - x);
        if (card != null) card.anchoredPosition = cardBase + new Vector2((1f - e) * 40f, 0f);
        if (cardGroup != null) cardGroup.alpha = Mathf.Lerp(0.2f, 1f, e);
        if (keysText != null) keysText.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 3f));

        if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        Keyboard k = Keyboard.current;
        if (k == null || pageTime < 0.25f) return;
        if (k.rightArrowKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) Next();
        else if (k.leftArrowKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame) Back();
    }

    public static Page[] DefaultPages() => new[]
    {
        new Page { keys = "¡HOLA!", title = "Soy Kuntur", body = "El valle del Mantaro se está llenando de basura y los vecinos necesitan ayuda. Te enseño rápido cómo se juega." },
        new Page { keys = "W  A  S  D", title = "Caminar", body = "Muévete con W A S D (o las flechas). El mouse gira la cámara y Kuntur camina hacia donde miras." },
        new Page { keys = "SHIFT  ·  ESPACIO", title = "Correr y saltar", body = "Mantén SHIFT para correr: gasta la barra de ENERGÍA de abajo. ESPACIO para saltar. En el río, Kuntur nada solo." },
        new Page { keys = "Z  ·  V", title = "Cámara", body = "Z cambia la cámara: cerca, lejos o primera persona. Mantén V para ver a Kuntur de frente." },
        new Page { keys = "M", title = "Mapa y ruta morada", body = "Abre el mapa con M. La línea morada te lleva a tu objetivo. El minimapa de arriba también te guía." },
        new Page { keys = "E", title = "Hablar con los vecinos", body = "Busca al vecino con el \"!\" dorado y presiona E. Él te dirá dónde se juntó la basura. Acepta la misión con \"¡Claro que sí!\"." },
        new Page { keys = "E", title = "Recoger bolsas", body = "Acércate a cada bolsa de basura y presiona E para recogerla. Tienes un TIEMPO LÍMITE: arriba ves el reloj de la misión." },
        new Page { keys = "F", title = "Muestras de agua", body = "Algunas misiones piden sacar muestras del río. Ve a la orilla, busca el frasco azul y presiona F." },
        new Page { keys = "F", title = "Tu tuk tuk", body = "Tu mototaxi te espera frente a tu casa. Acércate y presiona F para subir. W A S D para manejar, ESPACIO frena, E toca el claxon y F otra vez para bajar." },
        new Page { keys = "P  ·  PEDIR TUK TUK", title = "Pide tu tuk tuk", body = "¿Lo dejaste lejos? Pausa con P y elige PEDIR MI TUK TUK: llega solito por las calles, cruza el puente y te espera donde estés." },
        new Page { keys = "3 ESTRELLAS", title = "Estrellas y dificultad", body = "Mientras más rápido termines, más estrellas ganas (hasta 3). Cada misión trae más bolsas, más lugares y menos tiempo." },
        new Page { keys = "E  ·  HELADERÍA", title = "Energía", body = "Recoger basura y cumplir misiones te recarga. Si te quedas sin energía, ve a una heladería y presiona E para comer un helado." },
        new Page { keys = "I  ·  P", title = "Mochila y pausa", body = "I abre tu mochila con lo que recogiste (se usa con el mouse). P pausa el juego: ahí ajustas el sonido o vuelves al menú." },
        new Page { keys = "E  ·  CASA", title = "Descansar", body = "Desde las 6 de la tarde puedes dormir en la puerta de tu casa con E. Al dormir se guarda tu avance y empieza un nuevo día." },
        new Page { keys = "¡VAMOS!", title = "El valle cuenta contigo", body = "Limpia, ayuda a tus vecinos y cuida el río. ¡Es hora de comenzar la aventura!" },
    };
}
