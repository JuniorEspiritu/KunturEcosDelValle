using UnityEngine;

// Entrada de un elemento de UI: llega desplazándose y apareciendo. Con
// `delay` distinto en cada botón se logra el efecto escalonado (entran uno
// detrás de otro, no todos de golpe), que es lo que hace que el menú se vea
// armado y no un volcado de botones.
[RequireComponent(typeof(CanvasGroup))]
public class UISlideIn : MonoBehaviour
{
    [SerializeField] private Vector2 offset = new Vector2(-70f, 0f);
    [SerializeField] private float duration = 0.45f;
    [SerializeField] private float delay = 0f;

    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 target;
    private float timer;

    // Lo llama el generador de escena para escalonar los botones sin tener
    // que tocar cada uno en el Inspector.
    public void Configure(float newDelay, Vector2 newOffset)
    {
        delay = newDelay;
        offset = newOffset;
    }

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        group = GetComponent<CanvasGroup>();

        // La posición de destino se guarda UNA sola vez, acá.
        //
        // Si se leyera en OnEnable, al reabrir un panel se tomaría como
        // destino la posición desplazada en la que quedó la animación
        // anterior, y el elemento se iría corriendo un poco más lejos cada
        // vez que se entra y se sale de la pantalla.
        target = rect.anchoredPosition;
    }

    private void OnEnable()
    {
        // El elemento se vuelve a animar cada vez que su panel se reabre.
        timer = 0f;
        group.alpha = 0f;
        rect.anchoredPosition = target + offset;
    }

    private void Update()
    {
        if (timer >= delay + duration) return;

        timer += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01((timer - delay) / duration);
        float eased = 1f - Mathf.Pow(1f - progress, 3f);

        rect.anchoredPosition = Vector2.Lerp(target + offset, target, eased);
        group.alpha = eased;
    }
}
