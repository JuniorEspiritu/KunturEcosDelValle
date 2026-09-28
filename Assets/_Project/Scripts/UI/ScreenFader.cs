using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Cortina negra a pantalla completa. Hace dos cosas:
//   - al abrir una escena, entra desde negro (el juego "aparece")
//   - antes de cambiar de escena, se va a negro y recién ahí carga
//
// Es lo que evita el corte seco al presionar JUGAR. Se pone encima de todo
// (sortingOrder alto) y con raycastTarget apagado para no bloquear los clics
// mientras es transparente.
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [SerializeField] private float fadeInDuration = 0.65f;
    [SerializeField] private float fadeOutDuration = 0.45f;

    private CanvasGroup group;
    private Coroutine running;

    private void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        Image image = GetComponent<Image>();
        if (image != null) image.raycastTarget = false;
    }

    private void OnEnable()
    {
        // Entra desde negro apenas carga la escena.
        group.alpha = 1f;
        Restart(FadeTo(0f, fadeInDuration));
    }

    // Se va a negro y después carga la escena pedida.
    public void FadeAndLoadScene(string sceneName)
    {
        Restart(FadeOutThenLoad(sceneName));
    }

    // Se va a negro y después cierra el juego.
    public void FadeAndQuit()
    {
        Restart(FadeOutThenQuit());
    }

    private void Restart(IEnumerator routine)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(routine);
    }

    private IEnumerator FadeOutThenLoad(string sceneName)
    {
        yield return FadeTo(1f, fadeOutDuration);
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator FadeOutThenQuit()
    {
        yield return FadeTo(1f, fadeOutDuration);
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        // blocksRaycasts se enciende solo cuando la cortina se está cerrando:
        // así, mientras tapa la pantalla, nadie puede seguir clickeando
        // botones por debajo (doble carga de escena por doble clic).
        group.blocksRaycasts = target > 0.5f;

        float start = group.alpha;
        float time = 0f;

        // unscaledDeltaTime a propósito: si el juego quedó en pausa
        // (Time.timeScale = 0) la transición igual se ve.
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            group.alpha = Mathf.Lerp(start, target, t * t * (3f - 2f * t)); // suavizado
            yield return null;
        }

        group.alpha = target;
        group.blocksRaycasts = target > 0.5f;
        running = null;
    }
}
