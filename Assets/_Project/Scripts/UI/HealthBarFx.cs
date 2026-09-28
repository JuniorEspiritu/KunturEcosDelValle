using TMPro;
using UnityEngine;
using UnityEngine.UI;

// v55: la barra de SALUD DEL VALLE, animada.
//  - El corazón late: más rápido y rojo cuando el valle está mal, tranquilo y
//    verde cuando está sano.
//  - La barra no salta: se desliza hasta el valor nuevo, y detrás queda una
//    "estela" clara que la alcanza despacio (se ve cuánto subió).
//  - Un brillo recorre la barra cada pocos segundos y el porcentaje cuenta
//    hacia arriba. Cuando el valle mejora, todo destella un momento.
public class HealthBarFx : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private Image trail;
    [SerializeField] private RectTransform shine;
    [SerializeField] private RectTransform heart;
    [SerializeField] private Image heartImage;
    [SerializeField] private Image glow;
    [SerializeField] private TMP_Text percentText;
    [SerializeField] private TMP_Text statusText;

    private float target = -1f;
    private float shown;
    private float trailValue;
    private float velocity;
    private float flash;
    private float beat;
    private Color targetColor = Color.white;
    private string status = "";
    private int lastPercent = -1;

    public void SetHealth(float normalized, Color color, string description)
    {
        normalized = Mathf.Clamp01(normalized);
        if (target >= 0f && normalized > target + 0.005f) flash = 1f;   // ¡mejoró!
        if (target < 0f) { shown = normalized; trailValue = normalized; }
        target = normalized;
        targetColor = color;
        status = description;
    }

    private void Update()
    {
        if (fill == null || target < 0f) return;
        float dt = Time.unscaledDeltaTime;

        shown = Mathf.SmoothDamp(shown, target, ref velocity, 0.45f, Mathf.Infinity, dt);
        // Estela clara: al subir se adelanta (muestra hasta dónde llega) y la
        // barra la alcanza; al bajar se queda atrás y se va vaciando despacio.
        if (target >= shown) trailValue = target;
        else trailValue = Mathf.MoveTowards(trailValue, shown, dt * 0.25f);
        fill.fillAmount = shown;
        if (trail != null)
        {
            trail.fillAmount = Mathf.Max(trailValue, shown);
            Color tc = targetColor; tc.a = 0.35f + 0.25f * flash;
            trail.color = tc;
        }

        flash = Mathf.MoveTowards(flash, 0f, dt * 1.2f);
        fill.color = Color.Lerp(fill.color, Color.Lerp(targetColor, Color.white, flash * 0.5f), dt * 6f);

        // Latido: dos golpes seguidos (pum-pum) y una pausa, como un corazón.
        float bpm = Mathf.Lerp(120f, 62f, target);
        beat += dt * bpm / 60f;
        float phase = Mathf.Repeat(beat, 1f);
        float pulse = Mathf.Exp(-Mathf.Pow((phase - 0.08f) * 18f, 2f)) + 0.6f * Mathf.Exp(-Mathf.Pow((phase - 0.3f) * 18f, 2f));
        if (heart != null) heart.localScale = Vector3.one * (1f + 0.16f * pulse + 0.25f * flash);
        if (heartImage != null) heartImage.color = Color.Lerp(targetColor, Color.white, 0.25f * pulse + 0.4f * flash);

        if (glow != null)
        {
            Color g = targetColor;
            g.a = 0.15f * pulse + 0.55f * flash;
            glow.color = g;
        }

        // Brillo que cruza la barra cada 3.5 s.
        if (shine != null && shine.parent is RectTransform bar)
        {
            float cycle = Mathf.Repeat(Time.unscaledTime / 3.5f, 1f);
            float x = Mathf.Lerp(-0.15f, 1.15f, Mathf.Clamp01(cycle / 0.35f)) * bar.rect.width * Mathf.Max(0.05f, shown);
            shine.anchoredPosition = new Vector2(x, 0f);
            shine.gameObject.SetActive(cycle < 0.35f && shown > 0.03f);
        }

        int percent = Mathf.RoundToInt(shown * 100f);
        if (percent != lastPercent)
        {
            lastPercent = percent;
            if (percentText != null) percentText.text = $"{percent}%";
            if (statusText != null) statusText.text = status;
        }
        if (percentText != null) percentText.rectTransform.localScale = Vector3.one * (1f + 0.2f * flash);
    }
}
