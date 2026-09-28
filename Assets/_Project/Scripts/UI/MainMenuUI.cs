using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Cerebro del menú principal: cambia entre las pantallas (Principal, Ajustes,
// Créditos) con transición, y de paso cambia el FONDO de cada una con un
// fundido cruzado.
//
// El fundido cruzado se hace con DOS imágenes superpuestas: la de abajo tiene
// el fondo actual y la de arriba el nuevo, y se le sube el alpha. Al terminar,
// la de arriba pasa a ser la de abajo. Es la forma más simple de cruzar dos
// imágenes sin shaders.
public class MainMenuUI : MonoBehaviour
{
    [Header("Pantallas")]
    [SerializeField] private CanvasGroup mainPanel;
    [SerializeField] private CanvasGroup settingsPanel;
    [SerializeField] private CanvasGroup creditsPanel;

    [Header("Fondos (fundido cruzado)")]
    [SerializeField] private Image backgroundBase;
    [SerializeField] private Image backgroundOverlay;
    [SerializeField] private Sprite mainBackground;
    [SerializeField] private Sprite settingsBackground;
    [SerializeField] private Sprite creditsBackground;

    [Header("Primer botón de cada pantalla (para teclado/mando)")]
    [SerializeField] private GameObject firstMainButton;
    [SerializeField] private GameObject firstSettingsButton;
    [SerializeField] private GameObject firstCreditsButton;

    [SerializeField] private float transitionDuration = 0.35f;
    [SerializeField] private float backgroundDuration = 0.55f;

    private CanvasGroup current;
    private Coroutine panelRoutine;
    private Coroutine backgroundRoutine;

    private void Start()
    {
        // Arranca en el menú principal; las otras dos empiezan apagadas.
        HideImmediate(settingsPanel);
        HideImmediate(creditsPanel);
        ShowImmediate(mainPanel);
        current = mainPanel;

        if (backgroundOverlay != null)
        {
            Color c = backgroundOverlay.color;
            c.a = 0f;
            backgroundOverlay.color = c;
            backgroundOverlay.raycastTarget = false;
        }

        Select(firstMainButton);
    }

    private void Update()
    {
        // Escape vuelve al menú principal desde Ajustes o Créditos. Es lo que
        // espera cualquiera sin que se lo expliquen.
        if (current == mainPanel) return;
        if (UnityEngine.InputSystem.Keyboard.current == null) return;
        if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) ShowMain();
    }

    // --- Métodos que llaman los botones ---

    public void ShowMain() => SwitchTo(mainPanel, mainBackground, firstMainButton);
    public void ShowSettings() => SwitchTo(settingsPanel, settingsBackground, firstSettingsButton);
    public void ShowCredits() => SwitchTo(creditsPanel, creditsBackground, firstCreditsButton);

    private void SwitchTo(CanvasGroup target, Sprite background, GameObject firstButton)
    {
        if (target == null || target == current) return;

        if (panelRoutine != null) StopCoroutine(panelRoutine);
        panelRoutine = StartCoroutine(SwitchRoutine(target, firstButton));

        if (background != null) CrossfadeBackground(background);
    }

    private IEnumerator SwitchRoutine(CanvasGroup target, GameObject firstButton)
    {
        CanvasGroup previous = current;
        current = target;

        // Nadie puede clickear mientras cruza: evita abrir dos pantallas.
        if (previous != null) previous.blocksRaycasts = false;
        target.blocksRaycasts = false;

        // 1) se apaga la pantalla anterior
        if (previous != null)
        {
            yield return Fade(previous, 0f, transitionDuration);
            previous.gameObject.SetActive(false);
        }

        // 2) se prende la nueva (SetActive reinicia las animaciones de
        //    entrada de sus botones, que es justo lo que se quiere)
        target.gameObject.SetActive(true);
        target.alpha = 0f;
        yield return Fade(target, 1f, transitionDuration);

        target.blocksRaycasts = true;
        target.interactable = true;
        Select(firstButton);
        panelRoutine = null;
    }

    private void CrossfadeBackground(Sprite background)
    {
        if (backgroundBase == null || backgroundOverlay == null) return;
        if (backgroundBase.sprite == background) return;

        if (backgroundRoutine != null) StopCoroutine(backgroundRoutine);
        backgroundRoutine = StartCoroutine(BackgroundRoutine(background));
    }

    private IEnumerator BackgroundRoutine(Sprite background)
    {
        backgroundOverlay.sprite = background;

        float time = 0f;
        while (time < backgroundDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / backgroundDuration);
            Color c = backgroundOverlay.color;
            c.a = t * t * (3f - 2f * t);
            backgroundOverlay.color = c;
            yield return null;
        }

        // El de arriba ya llegó al 100%: se copia al de abajo y se vuelve a
        // dejar transparente, listo para el siguiente cruce.
        backgroundBase.sprite = background;
        Color clear = backgroundOverlay.color;
        clear.a = 0f;
        backgroundOverlay.color = clear;
        backgroundRoutine = null;
    }

    private IEnumerator Fade(CanvasGroup group, float target, float duration)
    {
        float start = group.alpha;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            group.alpha = Mathf.Lerp(start, target, t * t * (3f - 2f * t));
            yield return null;
        }
        group.alpha = target;
    }

    private void ShowImmediate(CanvasGroup group)
    {
        if (group == null) return;
        group.gameObject.SetActive(true);
        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;
    }

    private void HideImmediate(CanvasGroup group)
    {
        if (group == null) return;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.gameObject.SetActive(false);
    }

    private void Select(GameObject target)
    {
        if (target == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }
}
