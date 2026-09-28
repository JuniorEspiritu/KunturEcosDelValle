using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// Controla el panel de diálogo de concientización (pantalla 04 del mockup).
// Se abre desde DialogueNPC.Interact() y llama de vuelta a
// DialogueNPC.ChooseOption() cuando el jugador elige - el puntaje y la salud
// del valle los decide el NPC, esta clase solo pinta la UI.
//
// Siempre hay una última opción "Salir de la conversación": uno no tiene por
// qué quedarse atrapado hablando. Las respuestas también se eligen con las
// teclas 1, 2, 3 y la salida con 4 o Escape, sin tener que soltar el mouse.
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text portraitInitialText; // círculo con la inicial del NPC (ej. "R" de Rosa)
    [SerializeField] private Image portraitImage;           // opcional, si el NPC tiene sprite propio
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text speechText;
    [SerializeField] private DialogueOptionUI[] optionButtons; // las 3 respuestas, en el orden del panel
    [SerializeField] private DialogueOptionUI exitButton;      // "Salir de la conversación"

    private DialogueNPC currentNpc;
    private bool waitingResponse;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
    // El Escape que cierra el diálogo no debe además soltar el mouse en el
    // controlador del jugador (que también escucha Escape).
    public static int LastClosedFrame { get; private set; } = -1;

    private void Awake() => Instance = this;

    public void Open(DialogueNPC npc)
    {
        if (IsOpen && currentNpc == npc) return;
        CancelInvoke();
        if (currentNpc != null && currentNpc != npc) currentNpc.SetTalking(false);
        currentNpc = npc;
        currentNpc.SetTalking(true); // se voltea a mirar a Kuntur y gesticula
        BeginConversationShot(npc);
        waitingResponse = false;
        panelRoot.SetActive(true);

        nameText.text = npc.NpcName;
        roleText.text = npc.NpcRole;
        speechText.text = npc.OpeningLine;
        portraitInitialText.text = npc.NpcName.Length > 0 ? npc.NpcName.Substring(0, 1).ToUpper() : "?";
        if (portraitImage != null) portraitImage.sprite = npc.NpcPortrait;

        int shown = 0;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            bool active = i < npc.Options.Length;
            optionButtons[i].gameObject.SetActive(active);
            if (active)
            {
                int index = i; // captura local: evita el bug clásico de closures en el for
                optionButtons[i].Setup(i + 1, npc.Options[i].text, () => Choose(index));
                shown++;
            }
        }

        if (exitButton != null)
        {
            exitButton.gameObject.SetActive(true);
            exitButton.Setup(shown + 1, "Salir de la conversación", Close);
        }

        GameManager.Instance.SetState(GameState.Dialogo);
    }

    private void Update()
    {
        if (!IsOpen || Keyboard.current == null) return;

        Keyboard k = Keyboard.current;
        if (!waitingResponse && currentNpc != null)
        {
            if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) Choose(0);
            else if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) Choose(1);
            else if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) Choose(2);
        }

        if (k.escapeKey.wasPressedThisFrame || k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame)
            Close();
    }

    private void Choose(int index)
    {
        if (waitingResponse || currentNpc == null || index >= currentNpc.Options.Length) return;
        currentNpc.ChooseOption(index);
    }

    public void ShowResponse(string npcResponse, bool wasCorrect, bool conversationEnded)
    {
        speechText.text = npcResponse;
        // Dijo que SÍ: Kuntur hace su gesto de aceptar (hablar2).
        if (wasCorrect && KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayYes();
        waitingResponse = true;
        foreach (DialogueOptionUI option in optionButtons) option.SetInteractable(false);

        if (conversationEnded)
            Invoke(nameof(Close), 3.2f); // que alcance a leer lo que le contestan
        else
            Invoke(nameof(ReenableOptions), 1.8f); // dejar reintentar tras una respuesta "trampa"
    }

    private void ReenableOptions()
    {
        waitingResponse = false;
        foreach (DialogueOptionUI option in optionButtons)
            if (option.gameObject.activeSelf) option.SetInteractable(true);
    }

    // Al apretar E: Kuntur se voltea hacia el vecino, se pone a conversar
    // (hablar1) y la cámara hace la toma de los dos de perfil.
    private void BeginConversationShot(DialogueNPC npc)
    {
        SimpleThirdPersonController player = SimpleThirdPersonController.Instance;
        if (player != null) player.FaceTowards(npc.transform.position);
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.SetTalking(true);
        if (KunturCinematicCamera.Instance != null) KunturCinematicCamera.Instance.StartDialogue(npc.transform);
    }

    // Terminó la conversación: la cámara vuelve detrás de Kuntur, como siempre.
    private void EndConversationShot()
    {
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.SetTalking(false);
        if (KunturCinematicCamera.Instance != null) KunturCinematicCamera.Instance.Stop();
    }

    public void Close()
    {
        CancelInvoke();
        waitingResponse = false;
        if (currentNpc != null) currentNpc.SetTalking(false);
        currentNpc = null;
        EndConversationShot();
        LastClosedFrame = Time.frameCount;
        panelRoot.SetActive(false);
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Dialogo)
            GameManager.Instance.SetState(GameState.Exploracion);

        // De vuelta a caminar: el mouse vuelve a mover la cámara.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
