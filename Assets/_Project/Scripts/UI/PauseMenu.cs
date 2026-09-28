using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla de pausa dentro de la partida, con la tecla P: créditos del juego y
// control de sonido separado por tipo (música, ambiente y efectos).
//
// Todo acá usa unscaledDeltaTime y no deltaTime. Al pausar, Time.timeScale
// queda en 0 y deltaTime pasa a valer 0: cualquier animación que dependa de él
// se congelaría justo en la pantalla donde tiene que moverse.
public class PauseMenu : MonoBehaviour
{
    // Lo lee el controlador del jugador para no moverse ni girar la cámara
    // mientras la pausa está abierta.
    public static bool IsOpen { get; private set; }

    [SerializeField] private CanvasGroup panel;
    [SerializeField] private float fadeDuration = 0.18f;

    [Header("Sonido")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private TextMeshProUGUI musicValue;
    [SerializeField] private Slider ambienceSlider;
    [SerializeField] private TextMeshProUGUI ambienceValue;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TextMeshProUGUI sfxValue;

    [Header("Animación (v55)")]
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text title;
    [SerializeField] private RectTransform[] rows;      // entran una detrás de otra
    [SerializeField] private RectTransform titleLine;
    [SerializeField] private string mainMenuScene = "MenuPrincipal";

    private bool open;
    private float openTime;
    private Vector2[] rowBase;
    private float titleSpacing;
    private float fade;
    private CursorLockMode previousCursorState;

    private void Awake()
    {
        IsOpen = false;
        if (panel != null)
        {
            panel.alpha = 0f;
            panel.blocksRaycasts = false;
            panel.interactable = false;
            panel.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        Bind(musicSlider, AudioVolumeSettings.Music, musicValue, value => AudioVolumeSettings.Music = value);
        Bind(ambienceSlider, AudioVolumeSettings.Ambience, ambienceValue, value => AudioVolumeSettings.Ambience = value);
        Bind(sfxSlider, AudioVolumeSettings.Sfx, sfxValue, value => AudioVolumeSettings.Sfx = value);
    }

    private void Bind(Slider slider, float current, TextMeshProUGUI label, System.Action<float> setter)
    {
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            // SetValueWithoutNotify: poner el valor guardado NO debe contar
            // como que el jugador movió la barra, o se dispararía un guardado
            // en cada arranque.
            slider.SetValueWithoutNotify(current);
            slider.onValueChanged.AddListener(value =>
            {
                setter(value);
                if (label != null) label.text = Label(value);
            });
        }
        if (label != null) label.text = Label(current);
    }

    // A volumen 0 se escribe "Apagado" y no "0%": es lo que el jugador quiere
    // leer cuando busca silenciar algo.
    private string Label(float value)
    {
        return value <= 0.001f ? "Apagado" : $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame) Toggle();
        else if (open && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();

        if (panel == null) return;
        if (open || fade > 0f) AnimateCard();

        float target = open ? 1f : 0f;
        if (!Mathf.Approximately(fade, target))
        {
            fade = Mathf.MoveTowards(fade, target, Time.unscaledDeltaTime / Mathf.Max(fadeDuration, 0.01f));
            panel.alpha = fade;
            if (fade <= 0f) panel.gameObject.SetActive(false);
        }
    }

    // La tarjeta entra con un rebote, las filas se deslizan escalonadas, el
    // título se "cierra" y la línea dorada se abre debajo.
    private void AnimateCard()
    {
        openTime += Time.unscaledDeltaTime;
        float t = openTime;
        if (card != null)
        {
            float x = Mathf.Clamp01(t / 0.4f);
            float back = 1f + 2.2f * Mathf.Pow(x - 1f, 3f) + 1.2f * Mathf.Pow(x - 1f, 2f); // ease-out con rebote
            card.localScale = Vector3.one * Mathf.LerpUnclamped(0.88f, 1f, back);
        }
        if (title != null)
        {
            float x = Mathf.Clamp01((t - 0.05f) / 0.6f);
            float e = 1f - Mathf.Pow(1f - x, 3f);
            title.characterSpacing = titleSpacing + (1f - e) * 30f + Mathf.Sin(Time.unscaledTime * 1.6f) * 1.5f;
        }
        if (titleLine != null)
        {
            float x = Mathf.Clamp01((t - 0.2f) / 0.5f);
            titleLine.localScale = new Vector3(1f - Mathf.Pow(1f - x, 3f), 1f, 1f);
        }
        if (rows != null && rowBase != null)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                float x = Mathf.Clamp01((t - 0.12f - i * 0.07f) / 0.35f);
                float e = 1f - Mathf.Pow(1f - x, 3f);
                rows[i].anchoredPosition = rowBase[i] + new Vector2((1f - e) * -40f, 0f);
                CanvasGroup g = rows[i].GetComponent<CanvasGroup>();
                if (g != null) g.alpha = e;
            }
        }
    }

    public void Resume() => Close();

    // v56: el tuk tuk de Kuntur viene solo hasta donde está.
    public void RequestTukTuk()
    {
        Close();
        if (VehicleSystem.Instance != null) VehicleSystem.Instance.RequestTukTuk();
    }

    // v55b: volver a ver el tutorial desde la pausa.
    public void OpenTutorial()
    {
        Close();
        if (TutorialUI.Instance != null) TutorialUI.Instance.Open();
    }

    // Sale al menú principal. Lo logrado se guarda al cumplir cada misión y
    // al dormir, así que en el menú "CONTINUAR" retoma desde ahí.
    public void GoToMenu()
    {
        Close();
        Time.timeScale = 1f;
        if (ScreenFader.Instance != null) ScreenFader.Instance.FadeAndLoadScene(mainMenuScene);
        else SceneManager.LoadScene(mainMenuScene);
    }

    public void Toggle()
    {
        if (open) Close();
        else Open();
    }

    public void Open()
    {
        if (open) return;
        // No se abre encima de un cambio de escena ni de la mochila.
        if (InventoryUI.IsOpen && InventoryUI.Instance != null) InventoryUI.Instance.Close();
        open = true;
        IsOpen = true;
        openTime = 0f;
        if (rows != null && rowBase == null)
        {
            rowBase = new Vector2[rows.Length];
            for (int i = 0; i < rows.Length; i++) if (rows[i] != null) rowBase[i] = rows[i].anchoredPosition;
            if (title != null) titleSpacing = title.characterSpacing;
        }

        if (panel != null)
        {
            panel.gameObject.SetActive(true);
            panel.blocksRaycasts = true;
            panel.interactable = true;
        }

        previousCursorState = Cursor.lockState;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!open) return;
        open = false;
        IsOpen = false;

        if (panel != null)
        {
            panel.blocksRaycasts = false;
            panel.interactable = false;
        }

        // Se devuelve el mouse al estado que tenía antes, no a "bloqueado" a
        // secas: si la pausa se abrió con el puntero ya libre (por ejemplo
        // durante un diálogo), cerrarla no debería atraparlo.
        Cursor.lockState = previousCursorState;
        Cursor.visible = previousCursorState != CursorLockMode.Locked;

        Time.timeScale = 1f;
    }

    // Si el objeto se destruye con la pausa abierta (cambio de escena desde la
    // propia pantalla), el juego quedaría congelado para siempre.
    private void OnDestroy()
    {
        if (!open) return;
        IsOpen = false;
        Time.timeScale = 1f;
    }
}
