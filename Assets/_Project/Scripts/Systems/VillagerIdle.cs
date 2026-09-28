using UnityEngine;

// Vecinos del pueblo que no dan misión: están parados conversando en las
// esquinas y en el barrio del fondo. Se balancean, gesticulan girando un poco
// el cuerpo y les aparece un globito "..." por turnos, como si hablaran entre
// ellos. Es lo que hace que el valle se sienta habitado en vez de vacío.
public class VillagerIdle : MonoBehaviour
{
    [SerializeField] private GameObject speechBubble;   // globito que se prende y apaga
    [SerializeField] private Transform lookAtTarget;    // el vecino con el que conversa
    [SerializeField] private float bobAmplitude = 0.05f;
    [SerializeField] private float bobSpeed = 1.6f;
    [SerializeField] private float swayAngle = 7f;
    [SerializeField] private float talkCycle = 3.4f;    // cada cuánto le toca "hablar"
    [SerializeField] private float talkOffset;          // desfase para que no hablen a la vez
    // true cuando el vecino es un modelo con animación propia: entonces no
    // se le suma el meneo falso (se vería mareado), solo el globito.
    [SerializeField] private bool animatedBody;

    private Vector3 restPosition;
    private Quaternion restRotation;

    private void Start()
    {
        restPosition = transform.position;

        if (lookAtTarget != null)
        {
            Vector3 direction = lookAtTarget.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        restRotation = transform.rotation;
        if (speechBubble != null) speechBubble.SetActive(false);
    }

    private void Update()
    {
        float time = Time.time + talkOffset;

        if (!animatedBody)
        {
            transform.position = restPosition + Vector3.up * (Mathf.Sin(time * bobSpeed) * bobAmplitude);
            transform.rotation = restRotation * Quaternion.Euler(0f, Mathf.Sin(time * bobSpeed * 0.7f) * swayAngle, 0f);
        }

        if (speechBubble != null)
        {
            // Habla durante la primera mitad de su turno y escucha en la otra.
            bool talking = Mathf.Repeat(time, talkCycle * 2f) < talkCycle;
            if (speechBubble.activeSelf != talking) speechBubble.SetActive(talking);
        }
    }
}
