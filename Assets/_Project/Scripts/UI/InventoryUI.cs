using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// Mochila de residuos (tecla I). v55:
//  - Al abrirla aparece el puntero solo: se usa con el mouse (botón X o clic
//    afuera para cerrar), sin tener que mantener Ctrl.
//  - Mientras está abierta Kuntur no camina ni gira la cámara (lo mismo que
//    en pausa), así el mouse no lo hace dar vueltas.
//  - Entra con animación: la tarjeta rebota, las tres casillas caen una tras
//    otra, los números cuentan hacia arriba y los iconos flotan.
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text bottleCountText;
    [SerializeField] private TMP_Text canCountText;
    [SerializeField] private TMP_Text paperCountText;
    [SerializeField] private TMP_Text remainingText;

    [Header("Animación (v55)")]
    [SerializeField] private RectTransform card;
    [SerializeField] private RectTransform[] slots;
    [SerializeField] private RectTransform[] icons;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text totalText;

    private float openTime;
    private Vector2[] slotBase;
    private Vector2[] iconBase;
    private readonly int[] counts = new int[3];
    private readonly float[] shownCounts = new float[3];
    private float progressTarget;

    private void Awake()
    {
        Instance = this;
        IsOpen = false;
    }

    private void Start()
    {
        if (panel != null) panel.SetActive(false);

        if (TrashInventory.Instance != null)
            TrashInventory.Instance.OnInventoryChanged += Refresh;

        Refresh();
    }

    private void OnDestroy()
    {
        if (TrashInventory.Instance != null)
            TrashInventory.Instance.OnInventoryChanged -= Refresh;
        if (Instance == this) { Instance = null; IsOpen = false; }
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.iKey.wasPressedThisFrame && !PauseMenu.IsOpen) Toggle();
        else if (IsOpen && kb != null && kb.escapeKey.wasPressedThisFrame) Close();

        if (IsOpen)
        {
            // Que nada vuelva a atrapar el mouse mientras la mochila está abierta.
            if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            Animate();
        }
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (panel == null || IsOpen) return;
        // Solo explorando: no encima de un diálogo, del baile o del resultado.
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Exploracion
            && GameManager.Instance.CurrentState != GameState.Climax) return;

        IsOpen = true;
        panel.SetActive(true);
        openTime = 0f;
        for (int i = 0; i < 3; i++) shownCounts[i] = 0f;
        if (slots != null && slotBase == null)
        {
            slotBase = new Vector2[slots.Length];
            for (int i = 0; i < slots.Length; i++) if (slots[i] != null) slotBase[i] = slots[i].anchoredPosition;
        }
        if (icons != null && iconBase == null)
        {
            iconBase = new Vector2[icons.Length];
            for (int i = 0; i < icons.Length; i++) if (icons[i] != null) iconBase[i] = icons[i].anchoredPosition;
        }
        Refresh();
        Animate();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (panel != null) panel.SetActive(false);
        // De vuelta al juego: la cámara vuelve a seguir al mouse.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Refresh()
    {
        TrashInventory inventory = TrashInventory.Instance;
        if (inventory == null) return;

        counts[0] = inventory.GetCount(TrashType.Botella);
        counts[1] = inventory.GetCount(TrashType.Lata);
        counts[2] = inventory.GetCount(TrashType.Papel);
        int total = counts[0] + counts[1] + counts[2];

        if (remainingText != null)
        {
            remainingText.text = inventory.Remaining > 0
                ? $"Faltan <color=#F5C400>{inventory.Remaining}</color> bolsas por recoger"
                : "¡No queda basura pendiente!";
        }
        if (totalText != null) totalText.text = $"{total} residuos recogidos";
        int all = total + Mathf.Max(0, inventory.Remaining);
        progressTarget = all > 0 ? total / (float)all : 1f;
        if (!IsOpen) UpdateCounterTexts(true);
    }

    private void UpdateCounterTexts(bool instant)
    {
        TMP_Text[] texts = { bottleCountText, canCountText, paperCountText };
        for (int i = 0; i < 3; i++)
        {
            if (instant) shownCounts[i] = counts[i];
            if (texts[i] != null) texts[i].text = Mathf.RoundToInt(shownCounts[i]).ToString();
        }
    }

    private void Animate()
    {
        float dt = Time.unscaledDeltaTime;
        openTime += dt;
        float t = openTime;

        if (card != null)
        {
            float x = Mathf.Clamp01(t / 0.4f);
            float back = 1f + 2.2f * Mathf.Pow(x - 1f, 3f) + 1.2f * Mathf.Pow(x - 1f, 2f);
            card.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, back);
        }

        if (slots != null && slotBase != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                float x = Mathf.Clamp01((t - 0.1f - i * 0.08f) / 0.4f);
                float back = 1f + 2.4f * Mathf.Pow(x - 1f, 3f) + 1.4f * Mathf.Pow(x - 1f, 2f);
                slots[i].anchoredPosition = slotBase[i] + new Vector2(0f, (1f - back) * 50f);
                CanvasGroup g = slots[i].GetComponent<CanvasGroup>();
                if (g != null) g.alpha = x;
            }
        }

        if (icons != null && iconBase != null)
        {
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                float bob = Mathf.Sin(Time.unscaledTime * 2.4f + i * 1.3f) * 4f;
                icons[i].anchoredPosition = iconBase[i] + new Vector2(0f, bob);
                icons[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 1.7f + i) * 8f);
            }
        }

        // Los números cuentan desde 0 hasta lo que llevas (en ~0.8 s).
        for (int i = 0; i < 3; i++)
        {
            if (t < 0.3f + i * 0.08f) continue;
            float speed = Mathf.Max(6f, counts[i] * 1.6f);
            shownCounts[i] = Mathf.MoveTowards(shownCounts[i], counts[i], dt * speed);
        }
        UpdateCounterTexts(false);

        if (progressFill != null)
            progressFill.fillAmount = Mathf.MoveTowards(progressFill.fillAmount, t > 0.4f ? progressTarget : 0f, dt * 1.2f);
    }
}
