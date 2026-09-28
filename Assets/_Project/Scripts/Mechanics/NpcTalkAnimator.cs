using UnityEngine;

// Cuando Kuntur le habla a alguien: la persona deja lo que estaba haciendo,
// se voltea a mirarlo y gesticula mientras habla (mueve los brazos y asiente
// con la cabeza), como la gente de verdad. Al terminar vuelve a lo suyo.
//
// Los gestos se suman ENCIMA de la animación (en LateUpdate), girando los
// huesos del esqueleto humanoide alrededor de los ejes del propio personaje,
// así funciona con cualquier modelo de City People.
public class NpcTalkAnimator : MonoBehaviour
{
    [SerializeField] private float turnSpeed = 6f;
    [SerializeField] private float gestureSpeed = 3.2f;

    private Animator animator;
    private Transform player;
    private bool talking;
    private float talkBlend;          // 0 = normal, 1 = hablando (para entrar y salir suave)
    private Quaternion restRotation;
    private float returnTimer;
    private float phase;

    private Behaviour walker;         // PedestrianWalker, si camina
    private Behaviour idle;           // VillagerIdle, si conversa en una esquina
    private bool walkerWasEnabled;
    private bool idleWasEnabled;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        walker = GetComponent<PedestrianWalker>();
        idle = GetComponent<VillagerIdle>();
        phase = Random.value * 10f;
    }

    public void SetTalking(bool value)
    {
        if (talking == value) return;
        talking = value;

        if (talking)
        {
            restRotation = transform.rotation;
            if (player == null)
            {
                SimpleThirdPersonController p = FindAnyObjectByType<SimpleThirdPersonController>();
                if (p != null) player = p.transform;
            }
            if (walker != null) { walkerWasEnabled = walker.enabled; walker.enabled = false; }
            if (idle != null) { idleWasEnabled = idle.enabled; idle.enabled = false; }
        }
        else
        {
            returnTimer = 1.5f;
            if (walker != null && walkerWasEnabled) walker.enabled = true;
            if (idle != null && idleWasEnabled) idle.enabled = true;
        }
    }

    private void Update()
    {
        talkBlend = Mathf.MoveTowards(talkBlend, talking ? 1f : 0f, Time.deltaTime * 3f);

        if (talking && player != null)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer), Time.deltaTime * turnSpeed);
        }
        else if (returnTimer > 0f && walker == null)
        {
            // Los que conversan en la esquina vuelven a mirar a su compañero.
            returnTimer -= Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, restRotation, Time.deltaTime * 2.5f);
        }
    }

    private void LateUpdate()
    {
        if (talkBlend <= 0.001f || animator == null || !animator.isHuman) return;

        float t = Time.time * gestureSpeed + phase;
        Vector3 fwd = transform.forward, right = transform.right;

        // Brazo derecho: se levanta hacia adelante y "explica" con la mano.
        Rotate(HumanBodyBones.RightUpperArm, right, -(28f + Mathf.Sin(t) * 12f));
        Rotate(HumanBodyBones.RightUpperArm, fwd, 14f + Mathf.Sin(t * 0.7f) * 6f);
        Rotate(HumanBodyBones.RightLowerArm, right, -(45f + Mathf.Sin(t * 1.6f) * 25f));

        // Brazo izquierdo: acompaña, más tranquilo y desfasado.
        Rotate(HumanBodyBones.LeftUpperArm, right, -(14f + Mathf.Sin(t * 0.8f + 2f) * 10f));
        Rotate(HumanBodyBones.LeftUpperArm, fwd, -(10f + Mathf.Sin(t * 0.6f + 1f) * 5f));
        Rotate(HumanBodyBones.LeftLowerArm, right, -(30f + Mathf.Sin(t * 1.3f + 1.5f) * 18f));

        // La cabeza asiente y se ladea un poquito.
        Rotate(HumanBodyBones.Head, right, Mathf.Sin(t * 2.1f) * 6f);
        Rotate(HumanBodyBones.Head, fwd, Mathf.Sin(t * 0.9f) * 4f);
    }

    private void Rotate(HumanBodyBones bone, Vector3 axis, float degrees)
    {
        Transform b = animator.GetBoneTransform(bone);
        if (b == null) return;
        b.rotation = Quaternion.AngleAxis(degrees * talkBlend, axis) * b.rotation;
    }
}
