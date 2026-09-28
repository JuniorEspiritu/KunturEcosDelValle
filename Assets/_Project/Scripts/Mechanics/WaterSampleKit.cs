using UnityEngine;
using System.Collections;

// Punto de muestreo de agua a lo largo del río (4 en total, según el GDD).
// Recolectar las 4 es lo que permite "diagnosticar" el estado del agua en
// distintos tramos del valle - hace tangible el problema de contaminación del
// ODS 6 en vez de solo mencionarlo en un texto.
public class WaterSampleKit : MonoBehaviour, IInteractable
{
    [SerializeField] private int scoreValue = 25;            // +25 pts, igual que el mockup
    [SerializeField] private float healthContribution = 5f;
    [SerializeField] private string objectiveId = "muestras_agua";
    [SerializeField] private GameObject visualToHide;
    [SerializeField] private float pickupAnimDuration = 0.35f;

    private bool sampled;
    public bool Sampled => sampled; // el mapa la esconde una vez tomada

    public void Interact()
    {
        if (sampled) return;
        sampled = true;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        CollectibleGlow glow = GetComponent<CollectibleGlow>();
        if (glow != null) glow.enabled = false;

        // Kuntur se arrodilla en la orilla a llenar el frasco (recojer2).
        // Igual que la basura: el gesto arranca ANTES de sumar el objetivo,
        // por si esta muestra es lo último que faltaba para cumplir la misión.
        bool animated = KunturMixamoAnimator.Instance != null;
        if (animated) KunturMixamoAnimator.Instance.PlaySample(transform.position, OnGrabbed);

        ScoreManager.Instance.AddScore(scoreValue, UIPalette.Blue);
        ValleyHealthManager.Instance.ChangeHealth(healthContribution);
        ObjectiveSystem.Instance.ReportProgress(objectiveId, 1);

        if (AudioManager.Instance != null) AudioManager.Instance.PlayWaterSample();

        if (!animated) OnGrabbed();
    }

    // Si la zona ya se apagó, no se arranca la corrutina (sería un error de
    // Unity en un objeto apagado): el frasco queda tomado de una vez.
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

    // Mismo efecto de "encoger y subir" que la basura, para que la
    // recolección se sienta consistente en todo el juego.
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

    public string GetPrompt() => "Tomar muestra de agua";
    public InteractKey GetInteractKey() => InteractKey.Secondary; // tecla F
}
