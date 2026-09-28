using UnityEngine;
using TMPro;
using System.Collections;

// Texto flotante "+10 pts" que sube y se desvanece, igual que en el mockup
// HTML (@keyframes scorePop). Se instancia desde HUDController cada vez que
// ScoreManager suma puntos, y se autodestruye al terminar.
[RequireComponent(typeof(CanvasGroup))]
public class ScorePopup : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private float riseDistance = 90f;
    [SerializeField] private float duration = 1.2f;

    private CanvasGroup canvasGroup;

    private void Awake() => canvasGroup = GetComponent<CanvasGroup>();

    public void Show(int amount, Color color)
    {
        label.text = $"+{amount} pts";
        label.color = color;
        StartCoroutine(RiseAndFade());
    }

    private IEnumerator RiseAndFade()
    {
        float elapsed = 0f;
        Vector3 start = transform.localPosition;
        Vector3 end = start + Vector3.up * riseDistance;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localPosition = Vector3.Lerp(start, end, t);
            canvasGroup.alpha = 1f - t;
            yield return null;
        }
        Destroy(gameObject);
    }
}
