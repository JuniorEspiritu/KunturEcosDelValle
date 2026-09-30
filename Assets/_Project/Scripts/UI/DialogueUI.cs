using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// La conversación, filmada: la cámara enfoca SIEMPRE a quien está hablando
// (el vecino primero, después Kuntur cuando contesta) y lo que dice sale en
// un subtítulo ancho al pie de la pantalla, sin tapar el pueblo.
//
// El turno del jugador es el único momento en que aparecen las 3 opciones;
// al elegir una, Kuntur la DICE en voz alta -con la cámara sobre él- y recién
// después el vecino responde. Es el ida y vuelta de una conversación de
// verdad, no un menú.
//
// Siempre hay una salida (tecla 4 o Escape): uno no tiene por qué quedarse
// atrapado hablando. Las respuestas se eligen con 1, 2 y 3 sin soltar el
// mouse, y con Espacio o Enter se adelanta el texto que ya se leyó.
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [Header("Subtítulo (abajo, ancho)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform subtitleGroup;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text speechText;

    [Header("Respuestas")]
    [SerializeField] private GameObject optionsRoot;
    [SerializeField] private DialogueOptionUI[] optionButtons; // las 3 respuestas
    [SerializeField] private DialogueOptionUI exitButton;      // "Salir de la conversación"

    [Header("Quién habla")]
    [SerializeField] private string playerName = "Kuntur";
    [SerializeField] private string playerRole = "CÓNDOR DEL VALLE";
    [SerializeField] private Color npcNameColor = new Color(1f, 0.843f, 0.478f);
    [SerializeField] private Color playerNameColor = new Color(0.612f, 0.859f, 1f);

    [Header("Ritmo")]
    [SerializeField] private float charsPerSecond = 46f;   // máquina de escribir
    [SerializeField] private float minHold = 1.0f;         // lo mínimo que queda en pantalla
    [SerializeField] private float holdPerChar = 0.026f;
    [SerializeField] private float maxHold = 3.4f;

    private DialogueNPC currentNpc;
    private Coroutine routine;
    private bool choosing;
    private bool skipRequested;

    // Lo que el vecino contesta llega por ShowResponse mientras la corrutina
    // del turno de Kuntur sigue corriendo: se guarda acá y lo reproduce ella
    // misma, así la conversación queda en un solo hilo ordenado.
    private string pendingResponse;
    private bool pendingEnded;
    private bool hasPendingResponse;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
    // El Escape que cierra el diálogo no debe además soltar el mouse en el
    // controlador del jugador (que también escucha Escape).
    public static int LastClosedFrame { get; private set; } = -1;

    private void Awake() => Instance = this;

    // ---------------------------------------------------------------
    // Abrir / cerrar
    // ---------------------------------------------------------------
    public void Open(DialogueNPC npc)
    {
        if (npc == null) return;
        if (IsOpen && currentNpc == npc) return;

        StopRoutine();
        if (currentNpc != null && currentNpc != npc) currentNpc.SetTalking(false);
        currentNpc = npc;

        panelRoot.SetActive(true);
        HideOptions();

        // Kuntur se voltea hacia el vecino y se queda en pose de conversar
        // toda la charla (hable o escuche).
        SimpleThirdPersonController player = SimpleThirdPersonController.Instance;
        if (player != null) player.FaceTowards(npc.transform.position);
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.SetTalking(true);

        GameManager.Instance.SetState(GameState.Dialogo);

        routine = StartCoroutine(OpeningTurn());
    }

    public void Close()
    {
        StopRoutine();
        choosing = false;
        hasPendingResponse = false;

        if (currentNpc != null) currentNpc.SetTalking(false);
        currentNpc = null;

        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.SetTalking(false);
        if (KunturCinematicCamera.Instance != null) KunturCinematicCamera.Instance.Stop();

        LastClosedFrame = Time.frameCount;
        HideOptions();
        panelRoot.SetActive(false);

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Dialogo)
            GameManager.Instance.SetState(GameState.Exploracion);

        // De vuelta a caminar: el mouse vuelve a mover la cámara.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ---------------------------------------------------------------
    // Los turnos de la conversación
    // ---------------------------------------------------------------
    private IEnumerator OpeningTurn()
    {
        yield return NpcSays(currentNpc.OpeningLine);
        ShowOptions();
    }

    // El jugador eligió: primero lo dice Kuntur (cámara sobre él) y después
    // contesta el vecino. Igual para todas las misiones, porque el texto de
    // las opciones lo arma MissionDirector y esto solo lo pone en escena.
    private IEnumerator AnswerTurn(int index)
    {
        DialogueNPC npc = currentNpc;
        DialogueOption chosen = npc.Options[index];

        HideOptions();
        yield return PlayerSays(chosen.text);

        // Acá el vecino decide (puntaje, salud del valle, si lo convenció) y
        // nos devuelve su respuesta por ShowResponse.
        hasPendingResponse = false;
        npc.ChooseOption(index);
        if (currentNpc == null) yield break;           // se cerró en el camino
        if (!hasPendingResponse) { ShowOptions(); yield break; }

        hasPendingResponse = false;
        yield return NpcSays(pendingResponse);

        if (pendingEnded) Close();
        else ShowOptions();                            // deja reintentar
    }

    // Llamado por DialogueNPC.ChooseOption.
    public void ShowResponse(string npcResponse, bool wasCorrect, bool conversationEnded)
    {
        pendingResponse = npcResponse;
        pendingEnded = conversationEnded;
        hasPendingResponse = true;

        // Dijo que SÍ: Kuntur hace su gesto de aceptar (hablar2).
        if (wasCorrect && KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayYes();
    }

    private IEnumerator NpcSays(string line)
    {
        FocusOn(true);
        yield return Say(currentNpc.NpcName, currentNpc.NpcRole, npcNameColor, line);
    }

    private IEnumerator PlayerSays(string line)
    {
        FocusOn(false);
        yield return Say(playerName, playerRole, playerNameColor, line);
    }

    // Quién queda enfocado y quién solo escucha.
    private void FocusOn(bool npcSpeaks)
    {
        if (currentNpc == null) return;
        if (npcSpeaks) currentNpc.SetTalking(true);
        else currentNpc.SetListening(true);

        Transform player = SimpleThirdPersonController.Instance != null
            ? SimpleThirdPersonController.Instance.transform : null;
        if (player == null || KunturCinematicCamera.Instance == null) return;

        Transform npc = currentNpc.transform;
        KunturCinematicCamera.Instance.StartSpeaker(npcSpeaks ? npc : player, npcSpeaks ? player : npc);
    }

    // Escribe la línea letra por letra y la deja un rato en pantalla. Con
    // Espacio o Enter se adelanta (primero completa el texto, después salta
    // la espera), como en cualquier juego con subtítulos.
    private IEnumerator Say(string who, string role, Color color, string line)
    {
        if (string.IsNullOrEmpty(line)) yield break;

        if (nameText != null)
        {
            nameText.color = color;
            nameText.text = string.IsNullOrEmpty(role)
                ? who.ToUpper()
                : $"{who.ToUpper()}<size=72%><alpha=#77>   ·   {role.ToUpper()}</size>";
        }

        speechText.text = line;
        speechText.maxVisibleCharacters = 0;
        speechText.ForceMeshUpdate();
        int total = Mathf.Max(1, speechText.textInfo.characterCount);

        skipRequested = false;
        float shown = 0f;
        while (shown < total && !skipRequested)
        {
            shown += Mathf.Max(1f, charsPerSecond) * Time.unscaledDeltaTime;
            speechText.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt(shown), 0, total);
            yield return null;
        }
        speechText.maxVisibleCharacters = total;

        skipRequested = false;
        float hold = Mathf.Clamp(minHold + total * holdPerChar, minHold, maxHold);
        float elapsed = 0f;
        while (elapsed < hold && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ---------------------------------------------------------------
    // Las 3 respuestas (solo en el turno del jugador)
    // ---------------------------------------------------------------
    private void ShowOptions()
    {
        if (currentNpc == null) return;
        choosing = true;
        if (optionsRoot != null) optionsRoot.SetActive(true);

        int shown = 0;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            bool active = i < currentNpc.Options.Length;
            optionButtons[i].gameObject.SetActive(active);
            if (!active) continue;
            int index = i; // captura local: evita el bug clásico de closures en el for
            optionButtons[i].Setup(i + 1, currentNpc.Options[i].text, () => Choose(index));
            optionButtons[i].SetInteractable(true);
            shown++;
        }

        if (exitButton != null)
        {
            exitButton.gameObject.SetActive(true);
            exitButton.Setup(shown + 1, "Salir de la conversación", Close);
        }
    }

    private void HideOptions()
    {
        choosing = false;
        if (optionsRoot != null) optionsRoot.SetActive(false);
    }

    private void Choose(int index)
    {
        if (!choosing || currentNpc == null) return;
        if (index < 0 || index >= currentNpc.Options.Length) return;
        choosing = false;
        StopRoutine();
        routine = StartCoroutine(AnswerTurn(index));
    }

    // ---------------------------------------------------------------
    // Teclado y el subtítulo que sube y baja
    // ---------------------------------------------------------------
    private void Update()
    {
        if (!IsOpen) return;

        Keyboard k = Keyboard.current;
        if (k == null) return;

        if (k.escapeKey.wasPressedThisFrame) { Close(); return; }

        if (choosing)
        {
            if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) Choose(0);
            else if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) Choose(1);
            else if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) Choose(2);
            else if (k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame) Close();
            return;
        }

        if (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)
            skipRequested = true;
    }

    private void StopRoutine()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }
}
