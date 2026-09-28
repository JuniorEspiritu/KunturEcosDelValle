using TMPro;
using UnityEngine;

// v55: el cartel de misión, minimalista: una franja negra con el texto y nada
// más. Entra bajando y aclarándose, el título se "cierra" (las letras llegan
// separadas y se juntan) mientras una línea dorada se abre debajo; al irse
// sube y se esfuma. Todo con tiempo sin escalar (sigue andando en pausa).
public class BannerFx : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform band;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text body;
    [SerializeField] private RectTransform line;
    [SerializeField] private float lineWidth = 220f;

    private Vector2 basePos;
    private float baseSpacing;
    private bool ready;
    private float t;
    private float hideT = -1f;

    private void Init()
    {
        if (ready) return;
        ready = true;
        if (band == null) band = transform as RectTransform;
        if (group == null) group = GetComponent<CanvasGroup>();
        basePos = band.anchoredPosition;
        baseSpacing = title != null ? title.characterSpacing : 0f;
    }

    private void OnEnable() => Play();

    public void Play()
    {
        Init();
        t = 0f;
        hideT = -1f;
        Apply();
    }

    public void Hide()
    {
        if (!gameObject.activeInHierarchy) return;
        if (hideT < 0f) hideT = 0f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        t += dt;
        if (hideT >= 0f)
        {
            hideT += dt;
            if (hideT >= 0.4f)
            {
                hideT = -1f;
                gameObject.SetActive(false);
                return;
            }
        }
        Apply();
    }

    private static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }

    private void Apply()
    {
        float inA = EaseOut(t / 0.35f);
        float outA = hideT >= 0f ? EaseOut(hideT / 0.4f) : 0f;

        if (group != null) group.alpha = inA * (1f - outA);
        band.anchoredPosition = basePos + new Vector2(0f, (1f - inA) * 22f + outA * 14f);

        if (title != null)
        {
            float close = EaseOut((t - 0.05f) / 0.6f);
            title.characterSpacing = baseSpacing + (1f - close) * 24f;
            title.alpha = Mathf.Clamp01((t - 0.05f) / 0.3f);
        }
        if (body != null)
        {
            float b = EaseOut((t - 0.28f) / 0.4f);
            body.alpha = b;
        }
        if (line != null)
        {
            float w = EaseOut((t - 0.2f) / 0.5f);
            line.sizeDelta = new Vector2(lineWidth * w, line.sizeDelta.y);
            // Brillo que respira mientras el cartel está a la vista.
            var img = line.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                Color c = img.color;
                c.a = 0.65f + 0.35f * Mathf.Sin(t * 3f);
                img.color = c;
            }
        }
    }
}
