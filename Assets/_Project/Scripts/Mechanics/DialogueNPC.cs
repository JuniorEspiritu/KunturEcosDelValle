using UnityEngine;

// Una de las 3 opciones de respuesta del diálogo de concientización.
// isCorrect marca la única opción empática; las otras dos representan las
// "trampas" del diseño (amenaza y evasiva/minimizar el problema).
[System.Serializable]
public class DialogueOption
{
    [TextArea] public string text;
    public bool isCorrect;
    [TextArea] public string npcResponse; // lo que el NPC contesta después de elegir esta opción
}

// Diálogo de concientización de 3 opciones. Es la mecánica que más encarna el
// ODS 11: convencer a un vecino de verdad requiere una respuesta empática, no
// imponer (amenaza) ni minimizar (evasiva) - por eso solo la opción correcta
// avanza la conversación, y las otras dos solo penalizan y dejan reintentar.
public class DialogueNPC : MonoBehaviour, IInteractable
{
    [Header("Datos del NPC")]
    [SerializeField] private string npcName = "Doña Rosa";
    [SerializeField] private string npcRole = "VECINA · BODEGUERA";
    [SerializeField] private Sprite npcPortrait;
    [TextArea][SerializeField] private string openingLine =
        "Ay hijito, esto siempre se ha botado aquí... no creo que un poco de basura le haga tanto daño al río.";

    [Header("Opciones (la 1 debe ser la empática/correcta)")]
    [SerializeField] private DialogueOption[] options;

    [Header("Recompensas")]
    [SerializeField] private int baseCorrectScore = 40;     // +40 pts según el GDD
    [SerializeField] private float correctHealthBonus = 8f;
    [SerializeField] private float wrongHealthPenalty = 2f;
    [SerializeField] private string objectiveId = ""; // ej. "hablar_con_rosa", opcional

    private bool convinced;
    private bool failedOnce;

    public string NpcName => npcName;
    public string NpcRole => npcRole;
    public Sprite NpcPortrait => npcPortrait;
    public string OpeningLine => openingLine;
    public DialogueOption[] Options => options;
    public bool Convinced => convinced;

    // Avisa cuando el jugador eligió la respuesta correcta. Lo usa
    // MissionDirector: el vecino que da la misión la "entrega" al aceptarla.
    public event System.Action<DialogueNPC> OnConvinced;

    // Para reutilizar a cualquier vecino como el que da la misión del nivel:
    // se le cambia el nombre, lo que dice y las respuestas en tiempo de juego.
    public void Configure(string name, string role, string opening, DialogueOption[] newOptions, string newObjectiveId)
    {
        npcName = name;
        npcRole = role;
        openingLine = opening;
        options = newOptions;
        objectiveId = newObjectiveId;
        convinced = false;
        failedOnce = false;
    }

    // Al cargar una partida: quien ya fue convencido otro día sigue convencido.
    public void MarkConvinced() => convinced = true;

    private NpcTalkAnimator talk;

    private void Awake()
    {
        // Toda persona con diálogo se voltea y gesticula al hablar.
        talk = GetComponent<NpcTalkAnimator>();
        if (talk == null) talk = gameObject.AddComponent<NpcTalkAnimator>();
    }

    // Lo llama DialogueUI al abrir y al cerrar la conversación.
    public void SetTalking(bool value)
    {
        if (talk != null) talk.SetTalking(value);
    }

    public void Interact()
    {
        if (convinced) return;
        DialogueUI.Instance.Open(this);
    }

    // Llamado por DialogueUI cuando el jugador elige una de las 3 opciones (0-2).
    public void ChooseOption(int index)
    {
        if (convinced || index < 0 || index >= options.Length) return;

        DialogueOption chosen = options[index];

        if (chosen.isCorrect)
        {
            // Bono x2 solo si convence al primer intento: premia leer bien a
            // la persona en vez de probar las 3 opciones por descarte.
            int points = failedOnce ? baseCorrectScore : baseCorrectScore * 2;
            ScoreManager.Instance.AddScore(points, UIPalette.Gold);
            ValleyHealthManager.Instance.ChangeHealth(correctHealthBonus);
            convinced = true;

            if (!string.IsNullOrEmpty(objectiveId))
                ObjectiveSystem.Instance.ReportProgress(objectiveId, 1);
            OnConvinced?.Invoke(this);
        }
        else
        {
            failedOnce = true;
            ValleyHealthManager.Instance.ChangeHealth(-wrongHealthPenalty);
        }

        DialogueUI.Instance.ShowResponse(chosen.npcResponse, chosen.isCorrect, convinced);
    }

    public string GetPrompt() => convinced ? $"{npcName} ya se comprometió" : $"Hablar con {npcName}";
    public InteractKey GetInteractKey() => InteractKey.Primary; // tecla E
}
