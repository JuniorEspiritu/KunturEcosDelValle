using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// v55: renglón del menú principal al estilo Horizon: texto chico en
// mayúsculas y, en el elegido, el texto se enciende en dorado y una línea
// dorada se abre por debajo hacia la derecha, desvaneciéndose. El mouse y el
// teclado comparten la misma selección (pasar el mouse = seleccionar), así
// nunca quedan dos opciones encendidas a la vez.
public class HorizonMenuItem : MonoBehaviour,
    IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private RectTransform underline;
    [SerializeField] private Image underlineImage;
    [SerializeField] private float underlineWidth = 380f;
    [SerializeField] private Color normalColor = new Color(0.93f, 0.9f, 0.84f, 0.82f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.84f, 0.42f, 1f);
    [SerializeField] private float baseSpacing = 6f;

    private bool selected;
    private float amount;
    private RectTransform rect;
    private Vector2 basePos;

    private void Awake()
    {
        rect = (RectTransform)transform;
        basePos = rect.anchoredPosition;
    }

    private void OnEnable()
    {
        selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        amount = selected ? 1f : 0f;
        Apply();
    }

    private void Update()
    {
        float t = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
        amount = Mathf.Lerp(amount, selected ? 1f : 0f, t);
        Apply();
    }

    private void Apply()
    {
        if (label != null)
        {
            label.color = Color.Lerp(normalColor, selectedColor, amount);
            label.characterSpacing = baseSpacing + amount * 3f;
        }
        if (underline != null)
            underline.sizeDelta = new Vector2(underlineWidth * amount, underline.sizeDelta.y);
        if (underlineImage != null)
        {
            Color c = underlineImage.color;
            c.a = amount * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 3f));
            underlineImage.color = c;
        }
        if (rect != null) rect.anchoredPosition = basePos + new Vector2(amount * 10f, 0f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;
}
