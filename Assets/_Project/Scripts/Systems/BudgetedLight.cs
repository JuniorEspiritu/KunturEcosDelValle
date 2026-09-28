using UnityEngine;

// Se anota en el StreetLightBudget para que decida él cuándo prender esta luz
// (de noche y solo si está entre las más cercanas a la cámara).
[RequireComponent(typeof(Light))]
public class BudgetedLight : MonoBehaviour
{
    private Light lightSource;

    private void Awake()
    {
        lightSource = GetComponent<Light>();
        lightSource.enabled = false; // arranca apagada; el presupuesto la prende si toca
    }

    private void OnEnable() => StreetLightBudget.Register(lightSource);
    private void OnDisable() => StreetLightBudget.Unregister(lightSource);
}
