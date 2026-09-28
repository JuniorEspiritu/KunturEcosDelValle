using System.Collections.Generic;
using UnityEngine;

// Todos los postes se prenden de noche, pero solo los más CERCANOS a la cámara
// llevan una luz real encendida.
//
// Esto es lo que permite tener luz en todos los postes sin tumbar el juego.
// Una luz en tiempo real le cuesta a la tarjeta de video por cada objeto que
// ilumina; con cuarenta postes encendidos a la vez, una laptop con gráficos
// integrados se cae (ya pasó en este proyecto). Pero las luces lejanas casi no
// se notan: de lejos lo que se ve es el foco brillando, no la mancha de luz en
// el piso. Así que el foco brilla en TODOS los postes (eso lo hace NightLight
// y es gratis), y la luz real se reparte solo entre los que tiene cerca.
public class StreetLightBudget : MonoBehaviour
{
    [SerializeField] private int maxActiveLights = 8;
    [SerializeField] private float maxDistance = 55f;
    [SerializeField] private float refreshInterval = 0.25f;
    [SerializeField] private float onHour = 18.2f;
    [SerializeField] private float offHour = 6f;

    private static readonly List<Light> Lights = new List<Light>();
    private readonly List<float> distances = new List<float>();
    private readonly List<int> order = new List<int>();
    private float timer;

    public static void Register(Light light)
    {
        if (light != null && !Lights.Contains(light)) Lights.Add(light);
    }

    public static void Unregister(Light light) => Lights.Remove(light);

    private void Update()
    {
        // No hace falta decidir esto 60 veces por segundo: caminando, el poste
        // más cercano cambia cada varios segundos.
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = refreshInterval;

        Lights.RemoveAll(light => light == null);

        if (!IsNight())
        {
            foreach (Light light in Lights) light.enabled = false;
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 viewer = cam.transform.position;

        distances.Clear();
        order.Clear();
        for (int i = 0; i < Lights.Count; i++)
        {
            distances.Add((Lights[i].transform.position - viewer).sqrMagnitude);
            order.Add(i);
        }

        order.Sort((a, b) => distances[a].CompareTo(distances[b]));

        float maxSqr = maxDistance * maxDistance;
        for (int rank = 0; rank < order.Count; rank++)
        {
            int index = order[rank];
            Lights[index].enabled = rank < maxActiveLights && distances[index] < maxSqr;
        }
    }

    private bool IsNight()
    {
        if (DayNightCycle.Instance == null) return false;
        float hour = DayNightCycle.Instance.CurrentHour;
        return hour >= onHour || hour < offHour;
    }
}
