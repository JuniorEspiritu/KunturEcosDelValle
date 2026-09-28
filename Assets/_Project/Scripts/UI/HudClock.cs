using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reloj de la esquina inferior derecha: la hora del valle en formato de 12
// horas (8:45 AM) con un sol o una luna según sea de día o de noche.
//
// Sale del mismo DayNightCycle que mueve el sol, así la hora que se lee
// siempre coincide con la luz que se ve. Si fueran dos relojes separados,
// tarde o temprano marcaría "3:00 PM" con el cielo ya negro.
public class HudClock : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timeText;     // "8:45"
    [SerializeField] private TextMeshProUGUI periodText;   // "AM" / "PM"
    [SerializeField] private TextMeshProUGUI momentText;   // "Mañana", "Tarde", "Noche"
    [SerializeField] private Image dayNightIcon;
    [SerializeField] private Image moonShadow;  // de noche "muerde" el círculo y queda la media luna
    [SerializeField] private Color sunColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color moonColor = new Color(0.78f, 0.84f, 1f);

    private int lastMinute = -1;

    private void Update()
    {
        if (DayNightCycle.Instance == null) return;

        float hour = DayNightCycle.Instance.CurrentHour;
        int totalMinutes = Mathf.FloorToInt(hour * 60f);

        // Solo se reescribe cuando cambia el minuto: armar un texto nuevo en
        // cada frame genera basura en memoria sin ningún motivo.
        if (totalMinutes == lastMinute) return;
        lastMinute = totalMinutes;

        int hour24 = (totalMinutes / 60) % 24;
        int minute = totalMinutes % 60;

        // En 12 horas no existe el 0: la medianoche es 12 AM y el mediodía
        // 12 PM.
        int hour12 = hour24 % 12;
        if (hour12 == 0) hour12 = 12;

        if (timeText != null) timeText.text = $"{hour12}:{minute:00}";
        if (periodText != null) periodText.text = hour24 < 12 ? "AM" : "PM";
        if (momentText != null) momentText.text = MomentName(hour);

        bool night = DayNightCycle.Instance.IsNight;
        if (dayNightIcon != null) dayNightIcon.color = night ? moonColor : sunColor;
        if (moonShadow != null) moonShadow.enabled = night;
    }

    private static string MomentName(float hour)
    {
        if (hour < 5f) return "Madrugada";
        if (hour < 12f) return "Mañana";
        if (hour < 18.5f) return "Tarde";
        return "Noche";
    }
}
