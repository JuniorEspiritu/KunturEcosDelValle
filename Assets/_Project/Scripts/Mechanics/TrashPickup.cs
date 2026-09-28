using UnityEngine;
using System.Collections;

// Punto de basura recolectable en el mundo (calles, orillas del río). Cada
// recolección suma puntos y mejora la Salud del Valle: simula que clasificar
// bien los residuos es lo que evita que terminen contaminando el río
// (conexión directa ODS 6 + 11). Colocar en un GameObject con Collider
// (isTrigger no es necesario: se detecta por cercanía, no por colisión).
public class TrashPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private int scoreValue = 10;           // +10 pts, igual que el mockup
    [SerializeField] private float healthContribution = 3f;
    [SerializeField] private string objectiveId = "clasificar_residuos";
    [SerializeField] private TrashType trashType = TrashType.Botella;
    [SerializeField] private GameObject visualToHide; // opcional: modelo/partículas a ocultar en vez de desactivar todo
    [SerializeField] private float pickupAnimDuration = 0.35f;

    private bool collected;
    private bool restCaptured;
    private Vector3 restScale;
    private Vector3 restLocalPosition;

    public bool Collected => collected;
    public TrashType Type => trashType;
    public string ObjectiveId => objectiveId;

    private void Awake() => CaptureRest();

    private void CaptureRest()
    {
        if (restCaptured) return;
        restCaptured = true;
        restScale = transform.localScale;
        restLocalPosition = transform.localPosition;
    }

    // MissionDirector reutiliza los mismos residuos en varias misiones (la
    // gente vuelve a ensuciar): se dejan otra vez como nuevos.
    public void ResetPickup(string newObjectiveId)
    {
        CaptureRest();
        StopAllCoroutines();
        collected = false;
        if (!string.IsNullOrEmpty(newObjectiveId)) objectiveId = newObjectiveId;
        transform.localScale = restScale;
        transform.localPosition = restLocalPosition;
        gameObject.SetActive(true);

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
        CollectibleGlow glow = GetComponent<CollectibleGlow>();
        if (glow != null) glow.enabled = true;
    }

    private void Start()
    {
        // Cada residuo se anota solo en el inventario al arrancar: así el
        // total del nivel sale del mundo real y la victoria puede declararse
        // cuando ya no queda ninguno, sin números escritos a mano.
        if (TrashInventory.Instance != null) TrashInventory.Instance.RegisterTrash();
    }

    public void Interact()
    {
        if (collected) return;
        collected = true;

        // Apaga el collider ya mismo para que no se pueda volver a "recoger"
        // mientras se reproduce la animación de salida.
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // El brillo/flote (CollectibleGlow) también mueve el transform cada
        // frame - se apaga acá para que no compita con la animación de
        // recolección de abajo.
        CollectibleGlow glow = GetComponent<CollectibleGlow>();
        if (glow != null) glow.enabled = false;

        // Con el Kuntur de Mixamo: se agacha de verdad (animación "recojer")
        // y la bolsa desaparece cuando la mano llega a ella, no antes.
        // Va ANTES de sumar el objetivo: si esta es la última bolsa, la misión
        // se cumple ahí mismo, y el baile de victoria tiene que ver que Kuntur
        // sigue agachado para esperar a que termine (si no, el gesto de
        // recoger le pisa el baile).
        bool animated = KunturMixamoAnimator.Instance != null;
        if (animated) KunturMixamoAnimator.Instance.PlayPickup(transform.position, OnGrabbed);

        ScoreManager.Instance.AddScore(scoreValue, UIPalette.Green);
        ValleyHealthManager.Instance.ChangeHealth(healthContribution);
        ObjectiveSystem.Instance.ReportProgress(objectiveId, 1);
        if (TrashInventory.Instance != null) TrashInventory.Instance.Collect(trashType);

        // El sonido sale del AudioManager y no de este objeto: el residuo se
        // encoge y se apaga enseguida, y un AudioSource propio se cortaría a
        // la mitad del "clink".
        if (AudioManager.Instance != null) AudioManager.Instance.PlayTrashPickup();
        // Cada residuo recogido recarga un poco la energía para correr.
        SimpleThirdPersonController.AddEnergy(0.12f);

        if (!animated) OnGrabbed();
    }

    // La mano llegó a la bolsa. Si la zona ya se apagó (misión cumplida), no
    // se puede arrancar una corrutina en un objeto apagado: Unity lo marca
    // como error y, con "Error Pause" prendido en la consola, pausa el
    // editor. En ese caso la bolsa queda recogida de una vez.
    private void OnGrabbed()
    {
        if (this == null) return;
        if (gameObject.activeInHierarchy) StartCoroutine(PickupAnimation());
        else HideNow();
    }

    private void HideNow()
    {
        if (visualToHide != null) visualToHide.SetActive(false);
        else gameObject.SetActive(false);
    }

    // Encoge el objeto mientras sube un poco, como si Kuntur lo levantara -
    // mucho más claro que un SetActive(false) instantáneo.
    private IEnumerator PickupAnimation()
    {
        Transform visual = visualToHide != null ? visualToHide.transform : transform;
        Vector3 startScale = visual.localScale;
        Vector3 startPos = visual.position;
        float elapsed = 0f;

        while (elapsed < pickupAnimDuration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / pickupAnimDuration;
            visual.localScale = Vector3.Lerp(startScale, Vector3.zero, p);
            visual.position = startPos + Vector3.up * (p * 0.6f);
            yield return null;
        }

        HideNow();
    }

    // Nombre de lo que es (botella, lata, bolsa, caja de pizza...).
    [SerializeField] private string itemName = "";

    public string GetPrompt() => string.IsNullOrEmpty(itemName) ? $"Recoger {trashType.ToString().ToLower()}" : $"Recoger {itemName}";
    public InteractKey GetInteractKey() => InteractKey.Primary; // tecla E
}
