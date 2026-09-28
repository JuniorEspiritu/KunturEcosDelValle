using UnityEngine;
using UnityEngine.UI;

// v55: la vida del menú principal (estilo Horizon): chispitas de luz que suben
// despacio como polvo al atardecer, el sol que respira y dos capas de neblina
// que cruzan el valle. Todo con imágenes de UI y tiempo sin escalar.
public class MenuAmbientFx : MonoBehaviour
{
    [SerializeField] private Sprite moteSprite;
    [SerializeField] private int moteCount = 46;
    [SerializeField] private RectTransform sunGlow;
    [SerializeField] private RectTransform[] mist;

    private RectTransform[] motes;
    private Image[] moteImages;
    private Vector2[] origin;
    private float[] speed, sway, phase, size, life;
    private Vector2[] mistBase;
    private RectTransform area;

    private void Start()
    {
        area = (RectTransform)transform;
        motes = new RectTransform[moteCount];
        moteImages = new Image[moteCount];
        origin = new Vector2[moteCount];
        speed = new float[moteCount];
        sway = new float[moteCount];
        phase = new float[moteCount];
        size = new float[moteCount];
        life = new float[moteCount];

        for (int i = 0; i < moteCount; i++)
        {
            GameObject go = new GameObject("Chispa", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            Image img = go.AddComponent<Image>();
            img.sprite = moteSprite;
            img.raycastTarget = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            motes[i] = rt;
            moteImages[i] = img;
            Respawn(i, true);
        }

        if (mist != null)
        {
            mistBase = new Vector2[mist.Length];
            for (int i = 0; i < mist.Length; i++) if (mist[i] != null) mistBase[i] = mist[i].anchoredPosition;
        }
    }

    private void Respawn(int i, bool anywhere)
    {
        Rect r = area.rect;
        origin[i] = new Vector2(Random.Range(0f, r.width), anywhere ? Random.Range(0f, r.height) : Random.Range(-40f, r.height * 0.25f));
        speed[i] = Random.Range(12f, 38f);
        sway[i] = Random.Range(10f, 40f);
        phase[i] = Random.Range(0f, 10f);
        size[i] = Random.Range(4f, 15f);
        life[i] = anywhere ? Random.Range(0f, 1f) : 0f;
        motes[i].sizeDelta = Vector2.one * size[i];
        // Dorado cálido, algunas más blancas.
        moteImages[i].color = Color.Lerp(new Color(1f, 0.78f, 0.45f, 0f), new Color(1f, 0.95f, 0.85f, 0f), Random.value);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;
        Rect r = area.rect;

        for (int i = 0; i < moteCount; i++)
        {
            life[i] += dt / Mathf.Lerp(9f, 16f, size[i] / 15f);
            origin[i].y += speed[i] * dt;
            float x = origin[i].x + Mathf.Sin(now * 0.6f + phase[i]) * sway[i];
            motes[i].anchoredPosition = new Vector2(x, origin[i].y);
            float fade = Mathf.Sin(Mathf.Clamp01(life[i]) * Mathf.PI);
            float twinkle = 0.7f + 0.3f * Mathf.Sin(now * 3f + phase[i] * 7f);
            Color c = moteImages[i].color;
            c.a = fade * twinkle * 0.75f;
            moteImages[i].color = c;
            if (life[i] >= 1f || origin[i].y > r.height + 20f) Respawn(i, false);
        }

        if (sunGlow != null)
        {
            float breathe = 1f + 0.06f * Mathf.Sin(now * 0.8f) + 0.02f * Mathf.Sin(now * 2.3f);
            sunGlow.localScale = Vector3.one * breathe;
        }

        if (mist != null && mistBase != null)
        {
            for (int i = 0; i < mist.Length; i++)
            {
                if (mist[i] == null) continue;
                float dir = i % 2 == 0 ? 1f : -1f;
                mist[i].anchoredPosition = mistBase[i] + new Vector2(Mathf.Sin(now * (0.03f + i * 0.015f)) * 160f * dir, Mathf.Sin(now * 0.1f + i) * 8f);
            }
        }
    }
}
