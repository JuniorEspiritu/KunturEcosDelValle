using UnityEngine;

// Hace que la animación del cóndor vaya al ritmo al que camina de verdad.
//
// El clip es uno solo (un ciclo de caminata de 1.4 s), así que en vez de
// tener varios estados se le cambia la VELOCIDAD de reproducción según lo
// rápido que se esté moviendo el CharacterController: parado se detiene,
// caminando va normal, corriendo va más rápido. Es la forma más simple de que
// los pasos peguen con el avance y no se vea patinando.
[RequireComponent(typeof(Animator))]
public class KunturRigAnimator : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CharacterController controller;
    // Raíz del esqueleto dentro del modelo ("Armature"). Se usa para anular el
    // desplazamiento horneado del clip, por si el importador no lo separó.
    [SerializeField] private Transform animatedRoot;

    [Header("Ritmo")]
    // A qué velocidad de avance el clip se ve "a velocidad natural".
    [SerializeField] private float walkReferenceSpeed = 3.5f;
    [SerializeField] private float minPlaybackSpeed = 0.45f;
    [SerializeField] private float maxPlaybackSpeed = 2.2f;
    [SerializeField] private float idleThreshold = 0.15f;
    // Qué tan rápido frena o arranca la animación. Sin esto, al soltar la
    // tecla el cóndor se queda clavado de golpe a media zancada.
    [SerializeField] private float blend = 6f;

    private Animator animator;
    private Vector3 lockedRootPosition;
    private bool hasAnimatedRoot;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // Quien mueve al personaje por el mundo es el CharacterController. Si
        // además se aplicara el root motion del clip, avanzaría dos veces.
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (controller == null) controller = GetComponentInParent<CharacterController>();

        if (animatedRoot != null)
        {
            lockedRootPosition = animatedRoot.localPosition;
            hasAnimatedRoot = true;
        }
    }

    private void Update()
    {
        float speed = 0f;
        if (controller != null)
        {
            Vector3 horizontal = controller.velocity;
            horizontal.y = 0f; // saltar o caer no debe acelerar la caminata
            speed = horizontal.magnitude;
        }

        float target = speed > idleThreshold
            ? Mathf.Clamp(speed / Mathf.Max(walkReferenceSpeed, 0.01f), minPlaybackSpeed, maxPlaybackSpeed)
            : 0f;

        animator.speed = Mathf.MoveTowards(animator.speed, target, Time.deltaTime * blend);
    }

    // Red de seguridad contra el avance horneado en el clip. Si el importador
    // separó bien el root motion esto no hace nada; si no, evita que el cóndor
    // se deslice hacia adelante y pegue un salto de vuelta en cada ciclo. La
    // altura (Y) sí se deja animada, que es el rebote natural al caminar.
    private void LateUpdate()
    {
        if (!hasAnimatedRoot) return;

        Vector3 current = animatedRoot.localPosition;
        animatedRoot.localPosition = new Vector3(lockedRootPosition.x, current.y, lockedRootPosition.z);
    }
}
