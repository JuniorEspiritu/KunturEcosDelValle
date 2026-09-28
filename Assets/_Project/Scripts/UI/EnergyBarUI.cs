using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Barra de ENERGÍA (v55: abajo al centro, como la de estamina de cualquier
// juego de mundo abierto). Se vacía corriendo con Shift y se llena recogiendo
// basura, ganando estrellas o comiendo un helado. El rayo late cuando queda
// poca, la barra destella al recargarse y, llena y sin correr, se atenúa para
// no estorbar.
public class EnergyBarUI : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform bolt;
    [SerializeField] private Image boltImage;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Color fullColor = new Color(1f, 0.82f, 0.25f);
    [SerializeField] private Color lowColor = new Color(0.95f, 0.4f, 0.3f);

    private float shown = 1f;
    private float flash;
    private float idleTime;
    private string lastLabel;

    private void Update()
    {
        SimpleThirdPersonController p = SimpleThirdPersonController.Instance;
        if (p == null || fill == null) return;

        float dt = Time.unscaledDeltaTime;
        float target = p.Energy;
        if (target > shown + 0.05f) flash = 1f;       // se recargó
        shown = Mathf.MoveTowards(shown, target, dt * 1.5f);
        flash = Mathf.MoveTowards(flash, 0f, dt * 2f);

        fill.fillAmount = shown;
        Color c = Color.Lerp(lowColor, fullColor, Mathf.InverseLerp(0.1f, 0.5f, shown));
        fill.color = Color.Lerp(c, Color.white, flash * 0.6f);

        bool low = shown < 0.25f;
        float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f) : p.IsSprinting ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f) : 0f;
        if (bolt != null) bolt.localScale = Vector3.one * (1f + 0.18f * pulse + 0.35f * flash);
        if (boltImage != null) boltImage.color = Color.Lerp(c, Color.white, 0.4f * pulse + 0.5f * flash);

        // Llena y sin usarla, la barra se hace medio transparente.
        bool busy = p.IsSprinting || shown < 0.995f || flash > 0.01f;
        idleTime = busy ? 0f : idleTime + dt;
        if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, idleTime > 2.5f ? 0.45f : 1f, dt * 2f);

        if (label != null)
        {
            string text = p.IsSprinting ? "¡CORRIENDO!" : shown < 0.02f ? "SIN ENERGÍA · recoge basura o come un helado" : "ENERGÍA";
            if (text != lastLabel) { lastLabel = text; label.text = text; }
        }
    }
}
