using UnityEngine;

// Peatón que va y viene por una vereda. El cuerpo es una persona de City
// People con su animación de caminar (o, si falta el asset, la cápsula con
// poncho de antes). Este script mueve la raíz; la animación mueve las piernas.
//
// Se detiene si Kuntur se le para delante, en vez de atravesarlo: un peatón
// que camina a través del jugador rompe la ilusión mucho más que uno quieto.
public class PedestrianWalker : MonoBehaviour
{
    [SerializeField] private Vector3 pointA;
    [SerializeField] private Vector3 pointB;
    [SerializeField] private float speed = 1.2f;
    [SerializeField] private float pauseAtEnds = 1.8f;
    [SerializeField] private float turnSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float startProgress; // en qué parte del recorrido arranca

    [Header("Paso")]
    [SerializeField] private float stepFrequency = 3.4f;
    [SerializeField] private float bobAmplitude = 0.05f;
    [SerializeField] private float swayAngle = 3.5f;

    [Header("Modelo animado (opcional)")]
    // Si hay Animator, las piernas las mueve la animación: se apagan el
    // rebote y el balanceo falsos, y se le avisa cuándo camina y cuándo para.
    [SerializeField] private Animator bodyAnimator;

    [Header("Esquivar a Kuntur")]
    [SerializeField] private float personalSpace = 1.3f;

    private Vector3 target;
    private float waitTimer;
    private float stepPhase;
    private float baseHeight;
    private Transform player;

    // El rumbo se guarda aparte del balanceo. Si el balanceo se sumara
    // directo sobre la rotación del frame anterior, se iría acumulando y el
    // peatón terminaría caminando torcido.
    private Quaternion heading = Quaternion.identity;
    private static readonly int WalkingId = Animator.StringToHash("Walking");

    // v55: en la subida del mirador el suelo no es plano: la altura se toma
    // del piso (rayo hacia abajo) en vez de una altura fija.
    [SerializeField] private bool followSlope;

    private float GroundOffset(Vector3 flat)
    {
        if (!followSlope) return 0f;
        Vector3 ab = new Vector3(pointB.x - pointA.x, 0f, pointB.z - pointA.z);
        float t = ab.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector3.Dot(new Vector3(flat.x - pointA.x, 0f, flat.z - pointA.z), ab) / ab.sqrMagnitude) : 0f;
        float guess = Mathf.Lerp(pointA.y, pointB.y, t);
        // Rayo corto desde un poco más arriba de la altura estimada: así no
        // choca con los postes de luz ni con los techos.
        if (Physics.Raycast(new Vector3(flat.x, guess + 3f, flat.z), Vector3.down, out RaycastHit hit, 8f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return guess;
    }


    private void Start()
    {
        baseHeight = followSlope ? 0f : transform.position.y;
        // Arranca en un punto al azar del recorrido y no en la punta: si todos
        // salen de la esquina a la vez, parecen una fila de soldados.
        transform.position = Vector3.Lerp(pointA, pointB, startProgress) + Vector3.up * baseHeight;
        if (followSlope)
        {
            Vector3 p0 = transform.position;
            transform.position = new Vector3(p0.x, GroundOffset(new Vector3(p0.x, 0f, p0.z)), p0.z);
        }
        stepPhase = startProgress * 10f; // que no den el paso todos al mismo tiempo
        target = pointB;
        FaceTarget(true);

        if (bodyAnimator != null)
        {
            // Cada uno en otro punto del paso: si no, caminan como soldados.
            bodyAnimator.Play(0, 0, startProgress);
            bodyAnimator.speed = Mathf.Clamp(speed / 1.25f, 0.8f, 1.2f);
        }
    }

    // Apagado (se detuvo a conversar o a dar una misión): queda parado.
    private void OnDisable() => SetWalking(false);

    private void SetWalking(bool walking)
    {
        if (bodyAnimator != null && bodyAnimator.runtimeAnimatorController != null)
            bodyAnimator.SetBool(WalkingId, walking);
    }

    private void Update()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            Settle();
            return;
        }

        if (IsPlayerInTheWay())
        {
            Settle();
            return;
        }

        Vector3 flatPosition = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTarget = new Vector3(target.x, 0f, target.z);
        flatPosition = Vector3.MoveTowards(flatPosition, flatTarget, speed * Time.deltaTime);

        // El rebote va con el paso: sube y baja dos veces por ciclo, una por
        // cada pie, que es como se mueve la cabeza de alguien caminando.
        SetWalking(true);
        if (bodyAnimator != null)
        {
            transform.position = flatPosition + Vector3.up * (baseHeight + GroundOffset(flatPosition));
            FaceTarget(false);
            transform.rotation = heading;
        }
        else
        {
            stepPhase += Time.deltaTime * stepFrequency;
            float bob = Mathf.Abs(Mathf.Sin(stepPhase)) * bobAmplitude;
            transform.position = flatPosition + Vector3.up * (baseHeight + GroundOffset(flatPosition) + bob);

            FaceTarget(false);
            transform.rotation = heading * Quaternion.Euler(0f, 0f, Mathf.Sin(stepPhase) * swayAngle);
        }

        if ((flatPosition - flatTarget).sqrMagnitude < 0.04f)
        {
            target = target == pointA ? pointB : pointA;
            waitTimer = pauseAtEnds;
        }
    }

    // Quieto: sin rebote ni balanceo, parado derecho.
    private void Settle()
    {
        SetWalking(false);
        Vector3 p = transform.position;
        float settleY = baseHeight + GroundOffset(new Vector3(p.x, 0f, p.z));
        transform.position = new Vector3(p.x, Mathf.Lerp(p.y, settleY, Time.deltaTime * 8f), p.z);
        FaceTarget(false);
        transform.rotation = heading;
    }

    private bool IsPlayerInTheWay()
    {
        if (player == null)
        {
            SimpleThirdPersonController controller = FindAnyObjectByType<SimpleThirdPersonController>();
            if (controller == null) return false;
            player = controller.transform;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > personalSpace * personalSpace) return false;

        // Solo si está DELANTE: si Kuntur viene por detrás, el peatón sigue.
        Vector3 forward = target - transform.position;
        forward.y = 0f;
        return Vector3.Dot(forward.normalized, toPlayer.normalized) > 0.3f;
    }

    private void FaceTarget(bool instant)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(direction);
        heading = instant ? look : Quaternion.Slerp(heading, look, Time.deltaTime * turnSpeed);
        if (instant) transform.rotation = heading;
    }
}
