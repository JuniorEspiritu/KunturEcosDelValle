using UnityEngine;

// Mostrador de una heladería: "E  Comer un helado". Kuntur dice que sí con la
// cabeza, se le llena la ENERGÍA y aparece un cartelito. Después hay que
// esperar un rato para el siguiente (si no, correr no costaría nada).
public class IceCreamShop : MonoBehaviour, IInteractable
{
    [SerializeField] private float energy = 1f;          // cuánto recarga (1 = llena)
    [SerializeField] private float cooldown = 60f;       // segundos entre helado y helado

    private static float nextAllowed;                    // compartido: vale para todas las heladerías

    private static readonly string[] Flavors =
    {
        "fresa", "lúcuma", "chocolate", "maracuyá", "coco", "aguaymanto", "chirimoya",
    };

    public void Interact()
    {
        if (Time.time < nextAllowed) return;
        nextAllowed = Time.time + cooldown;

        SimpleThirdPersonController.AddEnergy(energy);
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.PlayYes();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayTrashPickup();

        string flavor = Flavors[Random.Range(0, Flavors.Length)];
        if (MissionDirector.Instance != null)
            MissionDirector.Instance.ShowBanner("¡QUÉ RICO HELADO!", $"Uno de {flavor}. Tu energía está al máximo: ¡a correr!", -1, 3.5f);
    }

    public string GetPrompt()
    {
        float wait = nextAllowed - Time.time;
        if (wait > 0f) return $"Otro helado en {Mathf.CeilToInt(wait)} s";
        return "Comer un helado (+energía)";
    }

    public InteractKey GetInteractKey() => InteractKey.Primary;
}
