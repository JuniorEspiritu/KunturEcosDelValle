using UnityEngine;

// Conecta el movimiento del jugador con el Animator de CUALQUIER personaje
// descargado (Mixamo, Asset Store, el que sea).
//
// La gracia está en que no asume nombres: mira qué parámetros tiene de verdad
// el Animator del personaje y solo escribe esos. Cada paquete usa los suyos
// ("Speed", "Blend", "MoveSpeed", "IsRunning"...), y escribir uno que no
// existe llena la consola de errores. Así, se le pone el que traiga.
[RequireComponent(typeof(Animator))]
public class CharacterAnimatorDriver : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private SimpleThirdPersonController playerController;

    [Header("Velocidades del personaje")]
    // A qué velocidad se considera que ya está corriendo. Sirve para los
    // personajes que usan un booleano en vez de un número.
    [SerializeField] private float runThreshold = 3.4f;
    [SerializeField] private float idleThreshold = 0.15f;
    [SerializeField] private float damping = 0.12f;

    private Animator animator;

    // Nombres que usan los paquetes más comunes, por tipo de dato.
    private static readonly string[] SpeedNames = { "Speed", "speed", "MoveSpeed", "Blend", "Forward", "VelocityZ", "Velocity" };
    private static readonly string[] GroundedNames = { "Grounded", "IsGrounded", "isGrounded", "OnGround" };
    private static readonly string[] JumpNames = { "Jump", "IsJumping", "Jumping", "InAir", "IsInAir" };
    private static readonly string[] RunNames = { "IsRunning", "Running", "Run", "Sprint", "IsSprinting" };
    private static readonly string[] WalkNames = { "IsWalking", "Walking", "Walk", "IsMoving", "Moving" };
    private static readonly string[] CrouchNames = { "Crouch", "IsCrouching", "Crouching", "IsCrouch" };

    private string speedParam, groundedParam, jumpParam, runParam, walkParam, crouchParam;
    private bool speedIsFloat;

    private void Start()
    {
        animator = GetComponent<Animator>();
        if (controller == null) controller = GetComponentInParent<CharacterController>();
        if (playerController == null) playerController = GetComponentInParent<SimpleThirdPersonController>();

        speedParam = FindParameter(SpeedNames, AnimatorControllerParameterType.Float);
        speedIsFloat = speedParam != null;
        groundedParam = FindParameter(GroundedNames, AnimatorControllerParameterType.Bool);
        jumpParam = FindParameter(JumpNames, AnimatorControllerParameterType.Bool)
                    ?? FindParameter(JumpNames, AnimatorControllerParameterType.Trigger);
        runParam = FindParameter(RunNames, AnimatorControllerParameterType.Bool);
        walkParam = FindParameter(WalkNames, AnimatorControllerParameterType.Bool);
        crouchParam = FindParameter(CrouchNames, AnimatorControllerParameterType.Bool);

        Debug.Log($"[Kuntur] Personaje conectado. Parámetros detectados -> " +
                  $"velocidad: {speedParam ?? "ninguno"}, suelo: {groundedParam ?? "ninguno"}, " +
                  $"salto: {jumpParam ?? "ninguno"}, correr: {runParam ?? "ninguno"}, " +
                  $"caminar: {walkParam ?? "ninguno"}, agacharse: {crouchParam ?? "ninguno"}.");
    }

    private string FindParameter(string[] candidates, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return null;

        foreach (string candidate in candidates)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type != type) continue;
                if (parameter.name != candidate) continue;
                return parameter.name;
            }
        }
        return null;
    }

    private void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        float speed = 0f;
        bool grounded = true;

        if (controller != null)
        {
            Vector3 horizontal = controller.velocity;
            horizontal.y = 0f; // caer no cuenta como caminar
            speed = horizontal.magnitude;
            grounded = controller.isGrounded;
        }

        bool moving = speed > idleThreshold;
        bool running = speed > runThreshold;
        bool crouching = playerController != null && playerController.IsCrouching;

        if (speedIsFloat) animator.SetFloat(speedParam, speed, damping, Time.deltaTime);
        if (groundedParam != null) animator.SetBool(groundedParam, grounded);
        if (jumpParam != null) animator.SetBool(jumpParam, !grounded);
        if (runParam != null) animator.SetBool(runParam, running);
        if (walkParam != null) animator.SetBool(walkParam, moving && !running);
        if (crouchParam != null) animator.SetBool(crouchParam, crouching);
    }
}
