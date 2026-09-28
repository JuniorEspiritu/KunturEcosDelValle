using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Misión cumplida: la cámara se pone delante de Kuntur y él se pone a
// bailar (animación "baile" de Mixamo) hasta que el jugador aprieta
// CONTINUAR. Abajo, un cartel con las estrellas ganadas.
//
// El baile es el premio de la misión: después de limpiar el barrio a
// contrarreloj, el jugador tiene un momento para celebrar con su personaje
// (y ver el valle un poco más limpio detrás) antes de la siguiente.
public class VictoryDanceUI : MonoBehaviour
{
    public static VictoryDanceUI Instance { get; private set; }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image[] stars;
    [SerializeField] private Sprite starFilled;
    [SerializeField] private Sprite starEmpty;
    [SerializeField] private Button continueButton;
    [SerializeField] private float cameraDistance = 4.2f;

    private Action onContinue;
    private bool showing;
    private float inputUnlockTime;

    public bool Showing => showing;

    private void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (continueButton != null) continueButton.onClick.AddListener(Continue);
    }

    public void Show(string title, string body, int starCount, Action continueAction)
    {
        onContinue = continueAction;
        StopAllCoroutines();
        StartCoroutine(ShowRoutine(title, body, starCount));
    }

    private IEnumerator ShowRoutine(string title, string body, int starCount)
    {
        showing = true;

        // Si todavía se está agachando a recoger la última bolsa, primero
        // termina el gesto (si no, el baile le corta la animación a la mitad).
        float wait = 0f;
        while (KunturMixamoAnimator.Instance != null && KunturMixamoAnimator.Instance.Busy && wait < 4f)
        {
            wait += Time.deltaTime;
            yield return null;
        }

        if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Resultado);
        SimpleThirdPersonController player = SimpleThirdPersonController.Instance;
        if (player != null && Camera.main != null)
        {
            // Que mire hacia donde estaba la cámara: así se le ve de frente
            // sin que la toma tenga que dar media vuelta.
            Vector3 camPos = Camera.main.transform.position;
            player.FaceTowards(camPos);
        }

        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayDance();
        if (KunturCinematicCamera.Instance != null) KunturCinematicCamera.Instance.StartPortrait(cameraDistance, true);

        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;
        if (stars != null)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;
                stars[i].sprite = i < starCount ? starFilled : starEmpty;
            }
        }

        yield return new WaitForSeconds(0.6f);
        if (panelRoot != null) panelRoot.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Un respiro antes de aceptar teclas: la E con la que recogió la
        // última bolsa no debe saltarse el baile.
        inputUnlockTime = Time.unscaledTime + 1.2f;
    }

    private void Update()
    {
        if (!showing || panelRoot == null || !panelRoot.activeSelf) return;
        if (Time.unscaledTime < inputUnlockTime) return;

        Keyboard k = Keyboard.current;
        if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame
                          || k.spaceKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame))
            Continue();
    }

    public void Continue()
    {
        if (!showing) return;
        showing = false;
        if (panelRoot != null) panelRoot.SetActive(false);

        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.StopDance();
        if (KunturCinematicCamera.Instance != null) KunturCinematicCamera.Instance.Stop();
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Resultado)
            GameManager.Instance.SetState(GameState.Exploracion);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Action action = onContinue;
        onContinue = null;
        action?.Invoke();
    }
}
