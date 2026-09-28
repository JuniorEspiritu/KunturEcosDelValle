using UnityEngine;

// Le da vida al fondo aunque sea una imagen fija: la acerca y la pasea muy
// despacio, en un vaivén que nunca corta (efecto "Ken Burns", el mismo que
// usan los menús de consola para que una ilustración no se vea congelada).
//
// El zoom mínimo tiene que ser > 1 siempre: al pasear la imagen, si estuviera
// al 100% se vería el borde vacío de la pantalla.
public class BackgroundKenBurns : MonoBehaviour
{
    [SerializeField] private float zoomMin = 1.05f;
    [SerializeField] private float zoomMax = 1.13f;
    [SerializeField] private float zoomSpeed = 0.045f;

    [SerializeField] private float panX = 26f;
    [SerializeField] private float panY = 14f;
    [SerializeField] private float panSpeed = 0.035f;

    private RectTransform rect;
    private Vector2 basePosition;
    private float seed;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        basePosition = rect.anchoredPosition;
        // Desfase aleatorio: si hubiera dos fondos con el script, no se mueven
        // exactamente igual.
        seed = Random.value * 100f;
    }

    private void Update()
    {
        float time = Time.unscaledTime + seed;

        // Mathf.Sin da un vaivén continuo: llega al extremo, vuelve, y nunca
        // hay un salto (a diferencia de reiniciar el zoom al llegar al tope).
        float zoomWave = (Mathf.Sin(time * zoomSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float zoom = Mathf.Lerp(zoomMin, zoomMax, zoomWave);
        rect.localScale = new Vector3(zoom, zoom, 1f);

        float x = Mathf.Sin(time * panSpeed * Mathf.PI * 2f) * panX;
        float y = Mathf.Cos(time * panSpeed * Mathf.PI * 2f * 0.7f) * panY;
        rect.anchoredPosition = basePosition + new Vector2(x, y);
    }
}
