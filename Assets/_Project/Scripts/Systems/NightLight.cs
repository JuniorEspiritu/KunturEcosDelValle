using UnityEngine;

// Enciende una luz cuando cae la noche y la apaga al amanecer: los faroles de
// las esquinas, las ventanas de las casas (como si hubiera gente viviendo
// adentro) y los faros de los carros. Se apoya en DayNightCycle, así que todo
// el pueblo se prende a la misma hora sin coordinar nada a mano.
public class NightLight : MonoBehaviour
{
    [SerializeField] private Light lightSource;      // opcional: luz real (solo en los postes)
    [SerializeField] private Renderer glowRenderer;  // opcional: la pieza que "brilla" (foco, ventana)
    [SerializeField] private Color glowColor = new Color(1f, 0.86f, 0.55f);
    [SerializeField] private float glowIntensity = 1.4f;
    [SerializeField] private float onHour = 18f;   // 6 de la tarde
    [SerializeField] private float offHour = 6f;   // 6 de la mañana

    private Material glowMaterial;
    private bool isOn = true; // arranca en true para forzar el primer apagado

    private void Start()
    {
        if (glowRenderer != null)
        {
            glowMaterial = glowRenderer.material; // instancia propia, no toca el material compartido
            glowMaterial.EnableKeyword("_EMISSION");
        }

        SetState(ShouldBeOn());
    }

    private void Update()
    {
        bool wanted = ShouldBeOn();
        if (wanted != isOn) SetState(wanted);
    }

    private bool ShouldBeOn()
    {
        if (DayNightCycle.Instance == null) return false;

        float hour = DayNightCycle.Instance.CurrentHour;
        return onHour > offHour
            ? hour >= onHour || hour < offHour   // caso normal: 18h -> 6h del día siguiente
            : hour >= onHour && hour < offHour;
    }

    private void SetState(bool on)
    {
        isOn = on;

        if (lightSource != null) lightSource.enabled = on;

        if (glowMaterial != null)
        {
            glowMaterial.SetColor("_EmissionColor", on ? glowColor * glowIntensity : Color.black);
            // El color base también se aclara: de noche una ventana encendida
            // se ve amarilla, no del mismo azul de vidrio del día.
            glowMaterial.color = on ? Color.Lerp(glowMaterial.color, glowColor, 0.6f) : glowMaterial.color;
        }
    }
}
