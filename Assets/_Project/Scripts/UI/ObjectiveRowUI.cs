using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Una fila de la checklist de la misión: un círculo (vacío mientras falta,
// verde con palomita al cumplir) + el texto, con "(2/4)" cuando tiene varios
// pasos. v55: entra deslizándose, una fila detrás de otra; el contador salta
// cuando avanza y la palomita "explota" al cumplir.
public class ObjectiveRowUI : MonoBehaviour
{
    [SerializeField] private GameObject checkIcon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image tick;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private HorizontalLayoutGroup layout;

    private static readonly Color Pending = new Color(1f, 1f, 1f, 0.18f);
    private static readonly Color Done = new Color(0.48f, 0.89f, 0.63f, 1f);

    private float age = -1f;
    private float delay;
    private float pop;
    private float bump;
    private bool wasComplete;
    private int lastCount = -1;

    public void Bind(Objective objective)
    {
        age = 0f;
        delay = transform.GetSiblingIndex() * 0.09f;
        wasComplete = objective.IsComplete;
        lastCount = objective.currentCount;
        Refresh(objective);
        Animate();
    }

    public void Refresh(Objective objective)
    {
        bool complete = objective.IsComplete;
        if (complete && !wasComplete) pop = 1f;
        if (objective.currentCount != lastCount && lastCount >= 0) bump = 1f;
        wasComplete = complete;
        lastCount = objective.currentCount;

        if (checkIcon != null)
        {
            checkIcon.SetActive(true);
            Image box = checkIcon.GetComponent<Image>();
            if (box != null) box.color = complete ? Done : Pending;
        }
        if (tick != null) tick.enabled = complete;

        string countSuffix = objective.targetCount > 1
            ? $"  <color=#F5C400>{objective.currentCount}/{objective.targetCount}</color>"
            : "";
        string prefix = objective.isSecondary ? "<color=#9BD7FF>Extra:</color> " : "";
        label.text = prefix + objective.description + countSuffix;
        label.fontStyle = complete ? FontStyles.Strikethrough : FontStyles.Normal;
        label.color = complete ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
    }

    private void Update()
    {
        if (age < 0f) return;
        age += Time.unscaledDeltaTime;
        pop = Mathf.MoveTowards(pop, 0f, Time.unscaledDeltaTime * 2.2f);
        bump = Mathf.MoveTowards(bump, 0f, Time.unscaledDeltaTime * 3f);
        Animate();
    }

    private void Animate()
    {
        float x = Mathf.Clamp01((age - delay) / 0.35f);
        float e = 1f - (1f - x) * (1f - x) * (1f - x);
        if (group != null) group.alpha = e;
        if (layout != null)
        {
            int pad = Mathf.RoundToInt((1f - e) * 36f);
            if (layout.padding.left != pad)
            {
                layout.padding.left = pad;
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
            }
        }
        if (checkIcon != null)
            checkIcon.transform.localScale = Vector3.one * (1f + 0.6f * Mathf.Sin(pop * Mathf.PI) * pop);
        if (label != null)
            label.transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(bump * Mathf.PI));
    }
}
