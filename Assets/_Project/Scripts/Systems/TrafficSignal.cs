using System.Collections.Generic;
using UnityEngine;

// Semáforo de un cruce. Alterna el paso entre la calle que corre norte-sur y
// la que corre este-oeste: verde, ámbar, un instante con los dos en rojo (el
// "todo rojo" que despeja el cruce) y le toca a la otra calle.
//
// Los carros (CarPatrol) le preguntan en qué fase está su calle y se detienen
// antes de la línea de pare. Las luces de los cabezales se prenden de verdad
// (emisión), así que de noche se ven brillar.
public class TrafficSignal : MonoBehaviour
{
    public static readonly List<TrafficSignal> All = new List<TrafficSignal>();

    public enum Phase { Green, Yellow, Red }

    [Header("Tamaño del cruce (medio ancho de cada calle)")]
    [SerializeField] private float halfSizeX = 4f;   // medio ancho de la calle norte-sur
    [SerializeField] private float halfSizeZ = 4f;   // medio ancho de la calle este-oeste

    [Header("Tiempos")]
    [SerializeField] private float greenTime = 10f;
    [SerializeField] private float yellowTime = 2.5f;
    [SerializeField] private float allRedTime = 1.2f;
    [SerializeField] private float phaseOffset;      // para que no cambien todos a la vez

    [Header("Luces (cabezales que miran a cada calle)")]
    [SerializeField] private Renderer[] northSouthRed;
    [SerializeField] private Renderer[] northSouthYellow;
    [SerializeField] private Renderer[] northSouthGreen;
    [SerializeField] private Renderer[] eastWestRed;
    [SerializeField] private Renderer[] eastWestYellow;
    [SerializeField] private Renderer[] eastWestGreen;

    public float HalfSizeX => halfSizeX;
    public float HalfSizeZ => halfSizeZ;

    private static readonly Color RedOn = new Color(1f, 0.12f, 0.08f);
    private static readonly Color YellowOn = new Color(1f, 0.72f, 0.05f);
    private static readonly Color GreenOn = new Color(0.1f, 1f, 0.35f);
    private static readonly Color Off = new Color(0.09f, 0.09f, 0.09f);

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock block;
    private Phase lastNs = (Phase)(-1);
    private Phase lastEw = (Phase)(-1);

    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    private float CycleLength => 2f * (greenTime + yellowTime + allRedTime);

    public Phase PhaseFor(bool northSouth)
    {
        float t = Mathf.Repeat(Time.time + phaseOffset, CycleLength);
        float half = greenTime + yellowTime + allRedTime;

        // Primera mitad del ciclo: le toca a la calle norte-sur.
        bool nsTurn = t < half;
        float local = nsTurn ? t : t - half;

        Phase active = local < greenTime ? Phase.Green
                     : local < greenTime + yellowTime ? Phase.Yellow
                     : Phase.Red;

        if (northSouth == nsTurn) return active;
        return Phase.Red;
    }

    private void Update()
    {
        Phase ns = PhaseFor(true);
        Phase ew = PhaseFor(false);
        if (ns == lastNs && ew == lastEw) return; // solo se repinta al cambiar

        lastNs = ns;
        lastEw = ew;
        Paint(northSouthRed, ns == Phase.Red ? RedOn : Off, ns == Phase.Red);
        Paint(northSouthYellow, ns == Phase.Yellow ? YellowOn : Off, ns == Phase.Yellow);
        Paint(northSouthGreen, ns == Phase.Green ? GreenOn : Off, ns == Phase.Green);
        Paint(eastWestRed, ew == Phase.Red ? RedOn : Off, ew == Phase.Red);
        Paint(eastWestYellow, ew == Phase.Yellow ? YellowOn : Off, ew == Phase.Yellow);
        Paint(eastWestGreen, ew == Phase.Green ? GreenOn : Off, ew == Phase.Green);
    }

    private void Paint(Renderer[] lenses, Color color, bool on)
    {
        if (lenses == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        foreach (Renderer lens in lenses)
        {
            if (lens == null) continue;
            lens.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetColor(EmissionId, on ? color * 2.2f : Color.black);
            lens.SetPropertyBlock(block);
        }
    }
}
