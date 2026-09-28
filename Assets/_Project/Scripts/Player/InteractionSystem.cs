using UnityEngine;
using UnityEngine.InputSystem;

// Se coloca en la cámara del jugador. Cada frame busca el IInteractable más
// cercano dentro de un radio alrededor del jugador (no un raycast apuntando
// con la cámara) y expone su prompt para que el HUD lo muestre. Se usa
// detección por cercanía porque con cámara en tercera persona el jugador rara
// vez apunta exactamente al objeto que tiene al lado (la basura y las muestras
// están a ras de piso, junto a los pies): basta con acercarse.
//
// IMPORTANTE - por qué se lee el teclado directamente:
// antes esto dependía SOLO de dos InputActionReference del asset
// PlayerControls. Al asset le faltaba la referencia de InteractPrimary (la
// tecla E) y la mecánica entera quedaba muerta sin ningún error en consola:
// no se podía recoger basura NI hablar con los vecinos. Ahora la tecla se lee
// también de forma directa, así que la interacción funciona aunque el asset
// de Input esté incompleto; las acciones se siguen soportando como
// alternativa (mando, remapeo).
public class InteractionSystem : MonoBehaviour
{
    [SerializeField] private float interactionRange = 3.4f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private Transform detectionOrigin; // normalmente el Player, no la cámara
    [SerializeField] private InputActionReference interactPrimary;   // opcional: tecla E
    [SerializeField] private InputActionReference interactSecondary; // opcional: tecla F

    private IInteractable currentTarget;
    private static readonly Collider[] Overlaps = new Collider[24];

    public bool HasTarget => currentTarget != null;

    // v56: para que la E del gatito en brazos no se pise con la de la basura.
    public static int LastInteractFrame { get; private set; } = -1;
    public static bool AnyTargetInRange { get; private set; }
    public string CurrentPrompt => currentTarget?.GetPrompt();
    public InteractKey? CurrentTargetKey => currentTarget?.GetInteractKey();

    private void OnEnable()
    {
        if (interactPrimary != null && interactPrimary.action != null)
        {
            interactPrimary.action.Enable();
            interactPrimary.action.performed += OnPrimaryPerformed;
        }

        if (interactSecondary != null && interactSecondary.action != null)
        {
            interactSecondary.action.Enable();
            interactSecondary.action.performed += OnSecondaryPerformed;
        }
    }

    private void OnDisable()
    {
        // Manejando se apaga: que no quede un cartel viejo en pantalla.
        currentTarget = null;
        AnyTargetInRange = false;

        if (interactPrimary != null && interactPrimary.action != null)
        {
            interactPrimary.action.performed -= OnPrimaryPerformed;
            interactPrimary.action.Disable();
        }

        if (interactSecondary != null && interactSecondary.action != null)
        {
            interactSecondary.action.performed -= OnSecondaryPerformed;
            interactSecondary.action.Disable();
        }
    }

    private void Update()
    {
        currentTarget = FindNearestInteractable();
        AnyTargetInRange = currentTarget != null;

        // Lectura directa del teclado: es la que hace que E y F respondan
        // siempre. Si además hay una acción configurada, dispara lo mismo; no
        // hay riesgo de recoger dos veces porque cada objeto se marca como
        // recogido dentro de su propio Interact().
        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame) TryInteract(InteractKey.Primary);
        if (Keyboard.current.fKey.wasPressedThisFrame) TryInteract(InteractKey.Secondary);
    }

    private void OnPrimaryPerformed(InputAction.CallbackContext ctx) => TryInteract(InteractKey.Primary);
    private void OnSecondaryPerformed(InputAction.CallbackContext ctx) => TryInteract(InteractKey.Secondary);

    private void TryInteract(InteractKey key)
    {
        if (currentTarget == null) return;
        // Mientras se agacha a recoger algo no se puede empezar otra cosa:
        // si no, apretar E dos veces recogía dos bolsas con un solo gesto.
        SimpleThirdPersonController player = SimpleThirdPersonController.Instance;
        if (player != null && (player.IsActionLocked || player.IsSwimming)) return;
        if (currentTarget.GetInteractKey() != key) return;

        currentTarget.Interact();
        LastInteractFrame = Time.frameCount;
        currentTarget = null; // se recalcula en el siguiente frame
    }

    private IInteractable FindNearestInteractable()
    {
        Transform origin = detectionOrigin != null ? detectionOrigin : transform;

        // Si la máscara quedara vacía por cualquier motivo, se buscan todas
        // las capas: mejor detectar de más que dejar la mecánica muerta.
        int mask = interactableLayer.value != 0 ? interactableLayer.value : ~0;

        int count = Physics.OverlapSphereNonAlloc(origin.position, interactionRange, Overlaps, mask);
        IInteractable nearest = null;
        float nearestSqrDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            // GetComponentInParent y no GetComponent: el collider puede estar
            // en una pieza hija del residuo (la botella son varias piezas).
            IInteractable candidate = Overlaps[i].GetComponentInParent<IInteractable>();
            if (candidate == null) continue;

            float sqrDistance = (Overlaps[i].transform.position - origin.position).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = candidate;
            }
        }

        return nearest;
    }
}
