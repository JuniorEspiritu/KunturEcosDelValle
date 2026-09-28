using UnityEngine;
using UnityEngine.InputSystem;

// Créditos de película: el texto sube despacio sobre fondo negro y, cuando
// termina de pasar, vuelve a empezar desde abajo. Mantener Espacio (o la
// flecha abajo) los adelanta; la rueda del mouse los mueve a mano.
[RequireComponent(typeof(RectTransform))]
public class CreditsRoll : MonoBehaviour
{
    [SerializeField] private RectTransform content;   // el bloque de texto que sube
    [SerializeField] private float speed = 55f;       // píxeles por segundo
    [SerializeField] private float fastMultiplier = 5f;
    [SerializeField] private float startDelay = 0.6f;

    private RectTransform viewport;
    private float delay;

    private void Awake()
    {
        viewport = (RectTransform)transform;
    }

    private void OnEnable()
    {
        delay = startDelay;
        ResetToBottom();
    }

    private void ResetToBottom()
    {
        if (content == null || viewport == null) return;
        // El borde de arriba del texto arranca justo debajo de la pantalla.
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, -viewport.rect.height);
    }

    private void Update()
    {
        if (content == null || viewport == null) return;

        if (delay > 0f)
        {
            delay -= Time.unscaledDeltaTime;
            return;
        }

        float mult = 1f;
        Keyboard k = Keyboard.current;
        if (k != null && (k.spaceKey.isPressed || k.downArrowKey.isPressed)) mult = fastMultiplier;
        if (k != null && k.upArrowKey.isPressed) mult = -fastMultiplier * 0.6f;

        float y = content.anchoredPosition.y + speed * mult * Time.unscaledDeltaTime;
        if (Mouse.current != null) y -= Mouse.current.scroll.ReadValue().y * 0.6f;

        // Ya pasó entero por arriba: otra vez desde abajo.
        float height = content.rect.height;
        if (y > height) y = -viewport.rect.height;
        if (y < -viewport.rect.height) y = -viewport.rect.height;

        content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
    }
}
