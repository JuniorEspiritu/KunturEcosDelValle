using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Animación del botón al pasar el mouse o seleccionarlo con teclado/mando:
// crece un poco, se corre a la derecha y el texto se enciende, con un
// indicador (►) que aparece a la izquierda.
//
// Esto es lo que más hace que un menú "se sienta" de consola: sin esto los
// botones son texto muerto. Se anima por código y no con Animator porque así
// no hay que crear ni enlazar controladores para cada botón.
public class UIButtonHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("Cuánto se mueve")]
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float hoverSlide = 18f;   // píxeles hacia la derecha
    [SerializeField] private float speed = 12f;

    [Header("Colores del texto")]
    [SerializeField] private Color normalColor = new Color(0.94f, 0.92f, 0.86f, 1f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.82f, 0.29f, 1f);

    [Header("Referencias (se autocompletan)")]
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Graphic marker;   // el ► de la izquierda

    private RectTransform rect;
    private Vector2 basePosition;
    private bool highlighted;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        basePosition = rect.anchoredPosition;
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        // Al volver de otro panel el botón debe estar en reposo, no congelado
        // a medio crecer.
        highlighted = false;
        rect.anchoredPosition = basePosition;
        rect.localScale = Vector3.one;
        if (label != null) label.color = normalColor;
        SetMarkerAlpha(0f);
    }

    private void Update()
    {
        float targetScale = highlighted ? hoverScale : 1f;
        float targetSlide = highlighted ? hoverSlide : 0f;
        float t = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime); // suavizado independiente de los FPS

        rect.localScale = Vector3.Lerp(rect.localScale, Vector3.one * targetScale, t);
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, basePosition + new Vector2(targetSlide, 0f), t);

        if (label != null) label.color = Color.Lerp(label.color, highlighted ? hoverColor : normalColor, t);
        if (marker != null) SetMarkerAlpha(Mathf.Lerp(marker.color.a, highlighted ? 1f : 0f, t));
    }

    // Se toca el alpha del COLOR del gráfico y no el del CanvasRenderer: el
    // CanvasRenderer se reescribe solo cada vez que el gráfico se redibuja, y
    // el marcador volvía a aparecer de golpe.
    private void SetMarkerAlpha(float alpha)
    {
        if (marker == null) return;
        Color c = marker.color;
        c.a = alpha;
        marker.color = c;
    }

    public void OnPointerEnter(PointerEventData eventData) => highlighted = true;
    public void OnPointerExit(PointerEventData eventData) => highlighted = false;
    public void OnSelect(BaseEventData eventData) => highlighted = true;
    public void OnDeselect(BaseEventData eventData) => highlighted = false;
}
