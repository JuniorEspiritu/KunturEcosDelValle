using UnityEngine;

// Animación de entrada para los paneles: aparecen con un pequeño "pop"
// (crecen desde un poco más chicos y suben) en vez de saltar de golpe a la
// pantalla. Se usa en el cuadro de diálogo, la mochila y el resultado: es lo
// que hace que abrir algo se sienta parte del juego y no un SetActive seco.
[RequireComponent(typeof(CanvasGroup))]
public class UIPopIn : MonoBehaviour
{
    [SerializeField] private float duration = 0.22f;
    [SerializeField] private float startScale = 0.86f;
    [SerializeField] private float riseFrom = 26f; // píxeles que sube al aparecer

    private CanvasGroup canvasGroup;
    private RectTransform rect;
    private Vector2 restPosition;
    private float elapsed;
    private bool animating;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rect = GetComponent<RectTransform>();
        restPosition = rect.anchoredPosition;
    }

    private void OnEnable()
    {
        elapsed = 0f;
        animating = true;

        canvasGroup.alpha = 0f;
        rect.localScale = Vector3.one * startScale;
        rect.anchoredPosition = restPosition - new Vector2(0f, riseFrom);
    }

    private void Update()
    {
        if (!animating) return;

        elapsed += Time.unscaledDeltaTime; // funciona aunque el juego esté en pausa
        float t = Mathf.Clamp01(elapsed / duration);

        // Curva con un pequeño rebote al final.
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        float overshoot = Mathf.Sin(t * Mathf.PI) * 0.045f;

        canvasGroup.alpha = t;
        rect.localScale = Vector3.one * (Mathf.Lerp(startScale, 1f, eased) + overshoot);
        rect.anchoredPosition = Vector2.Lerp(restPosition - new Vector2(0f, riseFrom), restPosition, eased);

        if (t >= 1f)
        {
            animating = false;
            canvasGroup.alpha = 1f;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = restPosition;
        }
    }
}
