using UnityEngine;

// Efecto visual para que la basura y las muestras de agua no se vean como
// objetos muertos tirados en el piso: flotan con un vaivén suave, giran lento
// y brillan con una emisión pulsante - tal como se describe en las imágenes
// de concepto del GDD (los residuos con un contorno brillante que indica que
// se pueden recoger). Puramente visual: no afecta el collider ni la
// interacción.
//
// Recorre TODOS los renderers hijos, porque cada residuo está armado con
// varias piezas (una botella es cuerpo + cuello + tapa), no con una sola.
public class CollectibleGlow : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 45f;
    [SerializeField] private float bobAmplitude = 0.09f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private Color glowColor = Color.white;
    [SerializeField] private float glowSpeed = 2.5f;
    [SerializeField] private float glowIntensity = 0.9f;

    private Material[] materials;
    private Vector3 restPosition;
    private float phase;

    private void Awake()
    {
        // .material (no .sharedMaterial) crea una instancia propia por objeto,
        // así el pulso de esta basura no afecta a todas las demás que comparten
        // el mismo material del proyecto.
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        materials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            materials[i] = renderers[i].material;
            materials[i].EnableKeyword("_EMISSION");
        }

        restPosition = transform.localPosition;

        // Desfase por posición: si todos los residuos pulsaran y flotaran
        // exactamente al mismo tiempo se vería mecánico.
        phase = (transform.position.x * 0.7f + transform.position.z * 0.3f) % (Mathf.PI * 2f);
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

        float bob = Mathf.Sin(Time.time * bobSpeed + phase) * bobAmplitude;
        transform.localPosition = restPosition + Vector3.up * (bob + bobAmplitude);

        float pulse = (Mathf.Sin(Time.time * glowSpeed + phase) + 1f) * 0.5f; // 0..1
        Color emission = glowColor * (pulse * glowIntensity);

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null) materials[i].SetColor("_EmissionColor", emission);
        }
    }
}
