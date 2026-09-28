using UnityEngine;

// La puerta de la casa de Kuntur: "E  Descansar". Solo deja dormir de noche
// (desde las 6 de la tarde, o cuando el día ya terminó a las 9).
public class HomeRest : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        if (DayManager.Instance != null) DayManager.Instance.RequestRest();
    }

    public string GetPrompt()
    {
        if (DayManager.Instance == null) return "Casa de Kuntur";
        if (DayManager.Instance.Sleeping) return "Durmiendo...";
        return DayManager.Instance.CanRest ? "Descansar hasta mañana" : "Descansar (desde las 6 PM)";
    }

    public InteractKey GetInteractKey() => InteractKey.Primary;
}
