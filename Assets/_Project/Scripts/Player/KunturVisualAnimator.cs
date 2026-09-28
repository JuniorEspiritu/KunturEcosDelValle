using UnityEngine;

// Kuntur es un modelo estático (viene de una herramienta de IA imagen-a-3D,
// no de un rig con huesos), así que no tiene animación de caminata real.
// En vez de dejarlo clavado mientras el jugador se mueve, simulamos el
// movimiento con rebote/inclinación del cuerpo, más - cuando el cóndor está
// armado con primitivas (patas/alas/cabeza como objetos separados, ver
// KunturSceneBuilder.BuildKunturVisual) - un ciclo de caminata real: las
// patas alternan como un paso, las alas aletean y la cabeza tiene un vaivén,
// igual que un ave de verdad. No es una animación con huesos, pero comunica
// mucho mejor "está caminando/volando" sin necesitar riggear el modelo (eso
// sí tomaría mucho más tiempo/herramientas de pago para este proyecto).
[RequireComponent(typeof(CharacterController))]
public class KunturVisualAnimator : MonoBehaviour
{
    [Header("Referencias (las asigna KunturSceneBuilder)")]
    [SerializeField] private Transform visualRoot;      // el hijo "Visual" bajo el jugador
    [SerializeField] private Transform headTransform;   // cabeza, para el vaivén
    [SerializeField] private Transform leftLegPivot;    // cadera izquierda
    [SerializeField] private Transform rightLegPivot;   // cadera derecha
    [SerializeField] private Transform leftWingPivot;   // hombro izquierdo
    [SerializeField] private Transform rightWingPivot;  // hombro derecho

    [Header("Cuerpo (rebote general al caminar/correr)")]
    [SerializeField] private float bobAmplitude = 0.06f;
    [SerializeField] private float bobFrequencyWalk = 8f;
    [SerializeField] private float bobFrequencySprint = 13f;
    [SerializeField] private float leanAngle = 6f;
    [SerializeField] private float smoothing = 8f;

    [Header("Patas (alternan al caminar, como un paso real)")]
    [SerializeField] private float legSwingAngle = 24f;

    [Header("Alas (aleteo)")]
    [SerializeField] private float wingIdleAngle = 6f;    // aleteo lento en reposo
    [SerializeField] private float wingIdleSpeed = 1.4f;
    [SerializeField] private float wingMoveAngle = 18f;   // aleteo extra al moverse

    [Header("Cabeza (vaivén tipo ave)")]
    [SerializeField] private float headBobAngle = 5f;
    [SerializeField] private float headIdleSwayAngle = 3f;
    [SerializeField] private float headIdleSwaySpeed = 0.6f;

    private CharacterController controller;
    private Vector3 restLocalPosition;
    private float bobTimer;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (visualRoot != null) restLocalPosition = visualRoot.localPosition;
    }

    private void Update()
    {
        if (visualRoot == null) return;

        // Velocidad horizontal real (ignora la caída/el salto en Y para que
        // el rebote no se dispare al saltar).
        Vector3 horizontalVelocity = controller.velocity;
        horizontalVelocity.y = 0f;
        float speed = horizontalVelocity.magnitude;
        bool moving = controller.isGrounded && speed > 0.15f;

        // 0 = velocidad de caminata, 1 = sprint a fondo. Se calcula siempre
        // (no solo cuando "moving") para que bobStrength llegue suave a 0.
        float speedFactor = Mathf.InverseLerp(0f, 7.5f, speed);
        float bobStrength = moving ? Mathf.Clamp01(speedFactor + 0.3f) : 0f;

        if (moving)
        {
            float frequency = Mathf.Lerp(bobFrequencyWalk, bobFrequencySprint, speedFactor);
            bobTimer += Time.deltaTime * frequency;
        }
        else
        {
            bobTimer = Mathf.Lerp(bobTimer, 0f, Time.deltaTime * smoothing);
        }

        AnimateBody(bobStrength);
        AnimateLegs(bobStrength);
        AnimateWings(bobStrength);
        AnimateHead(bobStrength);
    }

    private void AnimateBody(float bobStrength)
    {
        float bob = Mathf.Abs(Mathf.Sin(bobTimer)) * bobAmplitude * bobStrength;
        float lean = Mathf.Sin(bobTimer) * leanAngle * bobStrength;

        Vector3 targetPos = restLocalPosition + Vector3.up * bob;
        visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, targetPos, Time.deltaTime * smoothing);

        Quaternion targetRot = Quaternion.Euler(0f, 0f, lean);
        visualRoot.localRotation = Quaternion.Lerp(visualRoot.localRotation, targetRot, Time.deltaTime * smoothing);
    }

    private void AnimateLegs(float bobStrength)
    {
        if (leftLegPivot == null || rightLegPivot == null) return;

        // Fase opuesta entre las dos patas: mientras una va hacia adelante
        // la otra va hacia atrás, como un paso real.
        float swing = Mathf.Sin(bobTimer) * legSwingAngle * bobStrength;

        Quaternion leftTarget = Quaternion.Euler(swing, 0f, 0f);
        Quaternion rightTarget = Quaternion.Euler(-swing, 0f, 0f);

        leftLegPivot.localRotation = Quaternion.Lerp(leftLegPivot.localRotation, leftTarget, Time.deltaTime * smoothing);
        rightLegPivot.localRotation = Quaternion.Lerp(rightLegPivot.localRotation, rightTarget, Time.deltaTime * smoothing);
    }

    private void AnimateWings(float bobStrength)
    {
        if (leftWingPivot == null || rightWingPivot == null) return;

        // Aleteo lento siempre presente (aunque esté quieto, un cóndor no se
        // queda con las alas totalmente rígidas) más un aleteo más marcado
        // sincronizado al paso cuando se está moviendo.
        float idleFlap = Mathf.Sin(Time.time * wingIdleSpeed) * wingIdleAngle;
        float moveFlap = Mathf.Sin(bobTimer * 2f) * wingMoveAngle * bobStrength;
        float flap = idleFlap + moveFlap;

        // Mismo ángulo con signo opuesto en cada hombro para que ambas alas
        // suban/bajen juntas (ver comentario en BuildWing sobre los signos).
        Quaternion leftTarget = Quaternion.Euler(0f, 0f, flap);
        Quaternion rightTarget = Quaternion.Euler(0f, 0f, -flap);

        leftWingPivot.localRotation = Quaternion.Lerp(leftWingPivot.localRotation, leftTarget, Time.deltaTime * smoothing);
        rightWingPivot.localRotation = Quaternion.Lerp(rightWingPivot.localRotation, rightTarget, Time.deltaTime * smoothing);
    }

    private void AnimateHead(float bobStrength)
    {
        if (headTransform == null) return;

        // Vaivén tipo ave: cabecea con cada paso al caminar, y un balanceo
        // lento y sutil cuando está quieto (para que no se vea congelada).
        float walkNod = Mathf.Sin(bobTimer * 2f) * headBobAngle * bobStrength;
        float idleSway = Mathf.Sin(Time.time * headIdleSwaySpeed) * headIdleSwayAngle * (1f - bobStrength);

        Quaternion targetRot = Quaternion.Euler(walkNod, idleSway, 0f);
        headTransform.localRotation = Quaternion.Lerp(headTransform.localRotation, targetRot, Time.deltaTime * smoothing);
    }
}
