using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Controla el HUD de exploración (pantalla 03 del mockup): barra de Salud del
// Valle con su estado en palabras, hora del día, contador de puntos con
// popups flotantes, checklist de misiones, cuenta regresiva del clímax y los
// prompts de interacción "E"/"F". Solo escucha eventos de los managers - no
// contiene lógica de juego, así se puede rehacer la UI sin tocar la mecánica.
public class HUDController : MonoBehaviour
{
    [Header("Salud del Valle")]
    [SerializeField] private Image healthFillImage; // Image Type = Filled, Fill Method = Horizontal
    [SerializeField] private TMP_Text healthPercentText;
    [SerializeField] private TMP_Text healthStatusText; // "100% - Valle en Armonía"
    [SerializeField] private HealthBarFx healthFx;       // v55: barra animada (si está, ella dibuja)

    [Header("Puntaje")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private ScorePopup scorePopupPrefab;
    [SerializeField] private RectTransform popupSpawnArea;

    [Header("Misiones")]
    [SerializeField] private Transform objectiveListParent;
    [SerializeField] private ObjectiveRowUI objectiveRowPrefab;
    private readonly Dictionary<string, ObjectiveRowUI> objectiveRows = new();

    [Header("Hora del día y cuenta regresiva")]
    [SerializeField] private TMP_Text clockText;
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private CountdownManager countdown;
    [SerializeField] private Image countdownFill;
    [SerializeField] private TMP_Text countdownLabel;

    [Header("Prompts de interacción")]
    [SerializeField] private GameObject promptPrimary;   // panel "E"
    [SerializeField] private TMP_Text promptPrimaryText;
    [SerializeField] private GameObject promptSecondary; // panel "F"
    [SerializeField] private TMP_Text promptSecondaryText;
    [SerializeField] private InteractionSystem interactionSystem;

    private string lastClockValue;
    private string lastCountdownValue;

    // Se engancha en Start y no en OnEnable: así los Awake de los managers ya
    // corrieron y sus Instance existen (el orden de Awake entre GameObjects
    // distintos no está garantizado en Unity).
    private void Start()
    {
        ValleyHealthManager.Instance.OnHealthChanged += HandleHealthChanged;
        ScoreManager.Instance.OnScoreAdded += HandleScoreAdded;
        ObjectiveSystem.Instance.OnObjectiveProgress += HandleObjectiveProgress;
        ObjectiveSystem.Instance.OnObjectivesChanged += BuildObjectiveRows;
        // El contador de arriba a la derecha muestra ESTRELLAS (las que se
        // ganan al cumplir cada misión), no puntos sueltos.
        if (MissionDirector.Instance != null)
        {
            MissionDirector.Instance.OnStarsChanged += HandleStarsChanged;
            HandleStarsChanged(MissionDirector.Instance.TotalStars);
        }

        BuildObjectiveRows();
        HandleHealthChanged(ValleyHealthManager.Instance.CurrentHealth);
        if (MissionDirector.Instance == null) scoreText.text = ScoreManager.Instance.CurrentScore.ToString();

        if (countdownPanel != null) countdownPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (ValleyHealthManager.Instance != null) ValleyHealthManager.Instance.OnHealthChanged -= HandleHealthChanged;
        if (ScoreManager.Instance != null) ScoreManager.Instance.OnScoreAdded -= HandleScoreAdded;
        if (ObjectiveSystem.Instance != null) ObjectiveSystem.Instance.OnObjectiveProgress -= HandleObjectiveProgress;
        if (ObjectiveSystem.Instance != null) ObjectiveSystem.Instance.OnObjectivesChanged -= BuildObjectiveRows;
        if (MissionDirector.Instance != null) MissionDirector.Instance.OnStarsChanged -= HandleStarsChanged;
    }

    private void Update()
    {
        UpdatePrompts();
        UpdateClock();
        UpdateCountdown();
    }

    private void UpdatePrompts()
    {
        // Cada prompt solo se muestra si el objeto que el jugador tiene cerca
        // pide justo esa tecla (ver InteractKey en IInteractable).
        bool showPrimary = interactionSystem.HasTarget && interactionSystem.CurrentTargetKey == InteractKey.Primary;
        bool showSecondary = interactionSystem.HasTarget && interactionSystem.CurrentTargetKey == InteractKey.Secondary;

        string primaryText = showPrimary ? interactionSystem.CurrentPrompt : null;
        string secondaryText = showSecondary ? interactionSystem.CurrentPrompt : null;

        // v56: vehículos (F subir/bajar, E claxon) y el gatito en brazos.
        if (VehicleSystem.PrimaryHint != null && (primaryText == null || VehicleSystem.Driving)) primaryText = VehicleSystem.PrimaryHint;
        if (VehicleSystem.SecondaryHint != null && secondaryText == null) secondaryText = VehicleSystem.SecondaryHint;
        if (primaryText == null && AnimalWander.CarriedCat != null && !VehicleSystem.Driving) primaryText = "Soltar al gatito  (o G)";
        if (TutorialUI.Showing) primaryText = secondaryText = null;

        promptPrimary.SetActive(primaryText != null);
        if (primaryText != null) promptPrimaryText.text = primaryText;

        promptSecondary.SetActive(secondaryText != null);
        if (secondaryText != null) promptSecondaryText.text = secondaryText;
    }

    private void UpdateClock()
    {
        if (clockText == null || DayNightCycle.Instance == null) return;

        // Se lee cada frame pero solo se asigna el texto cuando cambia el
        // minuto: asignar strings en TMP cada frame genera basura para nada.
        string now = DayNightCycle.Instance.FormattedTime;
        if (now == lastClockValue) return;

        lastClockValue = now;
        clockText.text = now;
    }

    // Tiempo de la misión: panel arriba al centro con reloj, barra que se va
    // vaciando y colores que avisan (crema -> ámbar al último minuto -> rojo
    // latiendo en los últimos 20 segundos). De noche queda en pausa.
    private void UpdateCountdown()
    {
        if (countdown == null || countdownPanel == null || countdownText == null) return;

        bool show = countdown.IsActive;
        if (countdownPanel.activeSelf != show) countdownPanel.SetActive(show);
        if (!show) return;

        float remaining = countdown.Remaining;
        Color color = remaining <= 20f ? new Color(1f, 0.36f, 0.3f) : remaining <= 60f ? new Color(1f, 0.74f, 0.25f) : new Color(1f, 0.95f, 0.87f);
        if (countdownFill != null)
        {
            countdownFill.fillAmount = countdown.Fraction01;
            countdownFill.color = remaining <= 20f ? new Color(0.95f, 0.3f, 0.25f) : remaining <= 60f ? new Color(0.96f, 0.72f, 0.23f) : new Color(0.48f, 0.89f, 0.63f);
        }
        countdownText.color = color;
        float pulse = remaining <= 20f && countdown.IsRunning ? 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.time * 6f)) : 1f;
        countdownText.rectTransform.localScale = Vector3.one * pulse;
        if (countdownLabel != null)
            countdownLabel.text = countdown.IsRunning ? "TIEMPO PARA LIMPIAR" : "EN PAUSA HASTA MAÑANA";

        string value = countdown.FormattedTime;
        if (value == lastCountdownValue) return;
        lastCountdownValue = value;
        countdownText.text = value;
    }

    private void HandleHealthChanged(float health)
    {
        if (healthFx != null)
        {
            Color c = health >= 70f ? UIPalette.Green : health >= 40f ? UIPalette.Amber : new Color(0.9f, 0.34f, 0.3f);
            healthFx.SetHealth(health / 100f, c, DescribeHealth(health));
            return;
        }
        healthFillImage.fillAmount = health / 100f;

        int percent = Mathf.RoundToInt(health);
        if (healthPercentText != null) healthPercentText.text = $"{percent}%";
        if (healthStatusText != null) healthStatusText.text = $"{percent}% - {DescribeHealth(health)}";

        // La barra cambia de color según el estado: verde cuando el valle está
        // sano, ámbar cuando va a medias y rojo cuando está contaminado. Es la
        // lectura de un vistazo del ODS 6 + 11 sin leer ningún número.
        healthFillImage.color = health >= 70f ? UIPalette.Green
            : health >= 40f ? UIPalette.Amber
            : new Color(0.85f, 0.32f, 0.28f);
    }

    private string DescribeHealth(float health)
    {
        if (health >= 85f) return "Valle en Armonía";
        if (health >= 60f) return "Valle en Recuperación";
        if (health >= 40f) return "Valle en Riesgo";
        if (health >= 20f) return "Valle Contaminado";
        return "Valle en Crisis";
    }

    private void HandleStarsChanged(int stars) => scoreText.text = stars.ToString();

    private void HandleScoreAdded(int amount, int total, Color popupColor)
    {
        if (MissionDirector.Instance == null) scoreText.text = total.ToString();

        if (scorePopupPrefab != null && popupSpawnArea != null)
        {
            ScorePopup popup = Instantiate(scorePopupPrefab, popupSpawnArea);
            // La plantilla vive desactivada en el Canvas, y el clon de un
            // objeto desactivado también nace desactivado: hay que activarlo
            // antes de animarlo (si no, la corutina ni siquiera arranca).
            popup.gameObject.SetActive(true);
            popup.Show(amount, popupColor);
        }
    }

    private void BuildObjectiveRows()
    {
        foreach (Transform child in objectiveListParent) Destroy(child.gameObject);
        objectiveRows.Clear();

        foreach (Objective objective in ObjectiveSystem.Instance.Objectives)
        {
            ObjectiveRowUI row = Instantiate(objectiveRowPrefab, objectiveListParent);
            row.gameObject.SetActive(true); // la plantilla está desactivada: el clon también nace así
            row.Bind(objective);
            objectiveRows[objective.id] = row;
        }
    }

    private void HandleObjectiveProgress(Objective objective)
    {
        if (objectiveRows.TryGetValue(objective.id, out ObjectiveRowUI row))
            row.Refresh(objective);
    }
}
