using UnityEngine;
using UnityEngine.UI;

// La pantalla de "la noche pasa": estrellas que titilan, una estrella fugaz,
// la luna que cruza el cielo, "Zzz" que suben desde la casa y, al final, el
// cielo que se pone naranja y sale el sol detrás de los cerros.
public class NightSkyAnimator : MonoBehaviour
{
    [SerializeField] private Image sky;
    [SerializeField] private RectTransform moon;
    [SerializeField] private RectTransform sun;
    [SerializeField] private RectTransform shootingStar;
    [SerializeField] private Image[] stars;
    [SerializeField] private RectTransform[] zzz;
    [SerializeField] private float duration = 12f;
    [SerializeField] private Color nightColor = new Color(0.04f, 0.07f, 0.19f);
    [SerializeField] private Color dawnColor = new Color(0.95f, 0.55f, 0.32f);

    private float time;
    private bool dawn;
    private float[] starPhase;
    private float[] starBase;

    public void Restart()
    {
        time = 0f;
        dawn = false;
    }

    // Lo llama DayManager cuando ya amaneció (queda en el cielo del amanecer).
    public void ShowDawn() => dawn = true;

    private void OnEnable()
    {
        Restart();
        if (stars == null) return;
        starPhase = new float[stars.Length];
        starBase = new float[stars.Length];
        for (int i = 0; i < stars.Length; i++)
        {
            starPhase[i] = Random.value * 10f;
            starBase[i] = stars[i] != null ? stars[i].color.a : 1f;
        }
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;
        float p = dawn ? 1f : Mathf.Clamp01(time / duration);
        float dawnT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, p));

        if (sky != null) sky.color = Color.Lerp(nightColor, dawnColor, dawnT);

        // La luna cruza en arco de izquierda a derecha y se apaga al amanecer.
        if (moon != null)
        {
            float mx = Mathf.Lerp(-520f, 520f, p);
            float my = 120f + Mathf.Sin(p * Mathf.PI) * 170f;
            moon.anchoredPosition = new Vector2(mx, my);
            SetAlpha(moon, 1f - dawnT);
        }

        // El sol asoma detrás de los cerros al final.
        if (sun != null)
        {
            sun.anchoredPosition = new Vector2(260f, Mathf.Lerp(-420f, -150f, dawnT));
            SetAlpha(sun, dawnT);
        }

        if (stars != null && starPhase != null)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;
                float twinkle = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * (1.5f + (i % 5) * 0.4f) + starPhase[i]);
                Color c = stars[i].color;
                c.a = starBase[i] * twinkle * (1f - dawnT);
                stars[i].color = c;
            }
        }

        // Estrella fugaz a mitad de la noche.
        if (shootingStar != null)
        {
            float s = Mathf.InverseLerp(0.35f, 0.45f, p);
            bool visible = s > 0f && s < 1f;
            if (shootingStar.gameObject.activeSelf != visible) shootingStar.gameObject.SetActive(visible);
            if (visible) shootingStar.anchoredPosition = Vector2.Lerp(new Vector2(-300f, 330f), new Vector2(200f, 150f), s);
        }

        // Zzz que suben y se desvanecen, uno detrás de otro.
        if (zzz != null)
        {
            for (int i = 0; i < zzz.Length; i++)
            {
                if (zzz[i] == null) continue;
                float z = Mathf.Repeat(Time.unscaledTime * 0.35f + i / (float)zzz.Length, 1f);
                zzz[i].anchoredPosition = new Vector2(-330f + z * 60f + i * 18f, -170f + z * 150f);
                zzz[i].localScale = Vector3.one * (0.7f + z * 0.7f);
                SetAlpha(zzz[i], Mathf.Sin(z * Mathf.PI) * (1f - dawnT));
            }
        }
    }

    private static void SetAlpha(RectTransform rt, float a)
    {
        CanvasGroup cg = rt.GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = a; return; }
        Graphic g = rt.GetComponent<Graphic>();
        if (g == null) return;
        Color c = g.color;
        c.a = a;
        g.color = c;
    }
}
