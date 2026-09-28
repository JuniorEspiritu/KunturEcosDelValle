using System;
using System.Collections;
using UnityEngine;

// El "director" de las animaciones del Kuntur nuevo (el rigueado en Mixamo).
//
// En vez de un Animator lleno de flechas y condiciones, cada estado se elige
// desde acá con CrossFade: así es fácil de leer y de cambiar. Las
// animaciones que usa (todas de Mixamo, sobre el mismo esqueleto):
//   estarquieto  -> quieto                  caminar / correr -> moverse
//   movimiento   -> tras 15 s sin moverse   saltar            -> salto
//   hablar1      -> conversando (tecla E)   hablar2           -> cuando dice que SÍ
//   recojer      -> recoger una bolsa       recojer2          -> tomar muestra de agua
//   triste       -> misión fallida          nadar1 / nadar2   -> en el río
//
// Detalle importante: las caminatas de Mixamo se bajaron SIN "In Place", o sea
// que la cadera avanza metro y medio en cada ciclo. Quien mueve a Kuntur es
// el CharacterController, no la animación, así que en LateUpdate la cadera se
// devuelve a su sitio (si no, el cuerpo se adelanta y vuelve de golpe).
[RequireComponent(typeof(Animator))]
public class KunturMixamoAnimator : MonoBehaviour
{
    public static KunturMixamoAnimator Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private SimpleThirdPersonController player;
    [SerializeField] private Transform hips;

    [Header("Tiempos")]
    [SerializeField] private float idleVariantAfter = 15f;   // "movimiento" tras 15 s quieto
    [SerializeField] private float speedDamping = 0.1f;

    // Nombres de los estados del Animator (los arma KunturSceneBuilder).
    public const string StateLocomotion = "Locomocion";
    public const string StateSwim = "Natacion";
    public const string StateJump = "Salto";
    public const string StateIdleVariant = "Quieto_Largo";
    public const string StateTalk = "Hablar";
    public const string StateTalkYes = "Hablar_Si";
    public const string StatePickup = "Recoger";
    public const string StateSample = "Recoger_Muestra";
    public const string StateSad = "Triste";
    public const string StateDance = "Baile";
    public const string StateGreet = "Saludo";
    // v56: subirse a un vehículo y manejarlo (ver VehicleSystem).
    public const string StateEnterCar = "Subir_Carro";
    public const string StateDrive = "Conducir";
    public const string EnterCarClip = "K2_SubirCarro";
    // La subida dura lo que dure la animación, pero nunca más de esto (el
    // generador de escena acelera el estado para que calce).
    public const float MaxEnterCarSeconds = 2.2f;

    // Duración real (ya con su velocidad) de las animaciones de una sola vez,
    // y en qué momento de cada una la mano llega al suelo.
    // (recojer: 4.9 s recortados a x1.8; recojer2: 4.6 s a x1.3; ver
    // KunturModelPostprocessor y KunturSceneBuilder.Kuntur2.)
    public const float PickupDuration = 2.7f;
    public const float PickupGrabTime = 1.0f;
    public const float SampleDuration = 3.5f;
    public const float SampleGrabTime = 0.9f;
    public const float SadDuration = 2.8f;
    // El salto de Mixamo empieza con 0.5 s parado y agachándose; el salto de
    // verdad arranca al apretar espacio, así que el clip entra ya impulsándose.
    private const float JumpClipOffset = 0.5f;

    private enum Mode { Free, Talking, Action, Sad, Dance, Vehicle }

    private Animator animator;
    private Mode mode = Mode.Free;
    private string current = StateLocomotion;
    private float idleTimer;
    private float airTimer;
    private float jumpTimer = -1f;
    private float oneShotUntil;
    private Vector3 hipsRest;
    private bool hipsRestReady;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        Instance = this;
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        if (player == null) player = GetComponentInParent<SimpleThirdPersonController>();
        if (hips == null) hips = FindDeep(transform, "mixamorig:Hips");
    }

    private void OnEnable()
    {
        if (player != null) player.Jumped += HandleJumped;
    }

    private void OnDisable()
    {
        if (player != null) player.Jumped -= HandleJumped;
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Pose de reposo de la cadera, medida con la animación de quieto ya
        // aplicada: es la referencia para "devolverla a su sitio".
        animator.Play(StateLocomotion, 0, 0f);
        animator.Update(0f);
        if (hips != null)
        {
            hipsRest = hips.localPosition;
            hipsRestReady = true;
        }
    }

    // ---------------------------------------------------------------
    // API que usan las mecánicas
    // ---------------------------------------------------------------

    // Conversación con un vecino (tecla E): gesticula en bucle.
    public void SetTalking(bool talking)
    {
        if (talking)
        {
            mode = Mode.Talking;
            Cross(StateTalk, 0.25f);
        }
        else if (mode == Mode.Talking)
        {
            mode = Mode.Free;
            Cross(StateLocomotion, 0.3f);
        }
    }

    // Eligió la respuesta del "sí": el gesto de aceptar y después sigue
    // conversando hasta que se cierra el diálogo.
    public void PlayYes()
    {
        mode = Mode.Talking;
        Cross(StateTalkYes, 0.15f);
        StopAllCoroutines();
        StartCoroutine(BackToTalkAfter(1.55f));
    }

    private IEnumerator BackToTalkAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (mode == Mode.Talking) Cross(StateTalk, 0.3f);
    }

    // Agacharse a recoger una bolsa. onGrab se llama cuando la mano llega a
    // la bolsa (ahí desaparece), no al apretar la tecla.
    public void PlayPickup(Vector3 target, Action onGrab)
    {
        PlayAction(StatePickup, PickupDuration, PickupGrabTime, target, onGrab);
    }

    // Arrodillarse en la orilla a llenar el frasco con agua del río.
    public void PlaySample(Vector3 target, Action onGrab)
    {
        PlayAction(StateSample, SampleDuration, SampleGrabTime, target, onGrab);
    }

    // Misión fallida.
    public void PlaySad()
    {
        mode = Mode.Sad;
        oneShotUntil = Time.time + SadDuration;
        Cross(StateSad, 0.2f);
    }

    public bool Busy => mode == Mode.Action && Time.time < oneShotUntil;

    // Misión cumplida: baila (en bucle) hasta que el jugador aprieta
    // CONTINUAR. Lo llama VictoryDanceUI.
    public void PlayDance()
    {
        StopAllCoroutines();
        mode = Mode.Dance;
        idleTimer = 0f;
        Cross(StateDance, 0.3f);
    }

    // v55b: saludo con el ala (en bucle) mientras está abierto el tutorial.
    // Usa el mismo modo que el baile: nada lo interrumpe hasta StopDance().
    public void PlayGreet()
    {
        StopAllCoroutines();
        mode = Mode.Dance;
        idleTimer = 0f;
        Cross(StateGreet, 0.3f);
    }

    // v56: subir al vehículo. Devuelve cuánto dura la animación (segundos).
    public float PlayEnterCar()
    {
        StopAllCoroutines();
        mode = Mode.Vehicle;
        idleTimer = 0f;
        if (!animator.HasState(0, Animator.StringToHash(StateEnterCar))) { PlayDrive(); return 0.1f; }
        Cross(StateEnterCar, 0.12f);
        float length = 1.6f;
        if (animator.runtimeAnimatorController != null)
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == EnterCarClip) { length = clip.length; break; }
        return Mathf.Min(length, MaxEnterCarSeconds);
    }

    // Sentado manejando (en bucle).
    public void PlayDrive()
    {
        StopAllCoroutines();
        mode = Mode.Vehicle;
        if (animator.HasState(0, Animator.StringToHash(StateDrive))) Cross(StateDrive, 0.25f);
        else Cross(StateLocomotion, 0.2f);
    }

    public void StopVehicle()
    {
        if (mode != Mode.Vehicle) return;
        mode = Mode.Free;
        Cross(player != null && player.IsSwimming ? StateSwim : StateLocomotion, 0.2f);
    }

    public void StopDance()
    {
        if (mode != Mode.Dance) return;
        mode = Mode.Free;
        Cross(player != null && player.IsSwimming ? StateSwim : StateLocomotion, 0.35f);
    }

    private void PlayAction(string state, float duration, float grabTime, Vector3 target, Action onGrab)
    {
        mode = Mode.Action;
        oneShotUntil = Time.time + duration;
        if (player != null) player.LockForAction(duration, target);
        Cross(state, 0.15f);
        StartCoroutine(GrabAfter(grabTime, onGrab));
    }

    private IEnumerator GrabAfter(float seconds, Action onGrab)
    {
        yield return new WaitForSeconds(seconds);
        onGrab?.Invoke();
    }

    // ---------------------------------------------------------------
    // Cada frame: elegir el estado según lo que está haciendo el jugador
    // ---------------------------------------------------------------
    private void Update()
    {
        if (player == null) return;

        float speed = player.HorizontalSpeed;
        animator.SetFloat(SpeedHash, speed, speedDamping, Time.deltaTime);

        // En un vehículo manda VehicleSystem (subir / manejar).
        if (mode == Mode.Vehicle) return;

        // Las animaciones de una sola vez mandan hasta que terminan.
        if ((mode == Mode.Action || mode == Mode.Sad) && Time.time < oneShotUntil) return;
        if (mode == Mode.Action || mode == Mode.Sad)
        {
            mode = Mode.Free;
            Cross(player.IsSwimming ? StateSwim : StateLocomotion, 0.25f);
        }
        if (mode == Mode.Talking || mode == Mode.Dance) return;

        // En el agua: flotar quieto o nadar (blend tree por velocidad).
        if (player.IsSwimming)
        {
            idleTimer = 0f;
            jumpTimer = -1f;
            if (current != StateSwim) Cross(StateSwim, 0.3f);
            return;
        }
        if (current == StateSwim) Cross(StateLocomotion, 0.25f);

        // Salto: empieza al apretar espacio (evento Jumped) y termina al
        // tocar el suelo. Caerse de un borde sin saltar también cuenta, pero
        // recién tras un ratito en el aire (si no, bajar una vereda lo activa).
        airTimer = player.IsGrounded ? 0f : airTimer + Time.deltaTime;
        if (jumpTimer >= 0f)
        {
            jumpTimer += Time.deltaTime;
            if (player.IsGrounded && jumpTimer > 0.25f)
            {
                jumpTimer = -1f;
                Cross(StateLocomotion, 0.18f);
            }
            return;
        }
        if (airTimer > 0.4f)
        {
            jumpTimer = 0.3f;
            animator.CrossFadeInFixedTime(StateJump, 0.2f, 0, 0.55f);
            current = StateJump;
            return;
        }

        // Quieto mucho rato: hace su "movimiento" y vuelve a quedarse quieto.
        if (speed < 0.1f && current == StateLocomotion)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= idleVariantAfter)
            {
                idleTimer = 0f;
                Cross(StateIdleVariant, 0.3f);
                // El estado vuelve solo a Locomocion (transición con exit time).
                StartCoroutine(MarkLocomotionAfter(2.8f));
            }
        }
        else if (speed >= 0.1f)
        {
            idleTimer = 0f;
            if (current == StateIdleVariant) Cross(StateLocomotion, 0.2f);
        }
    }

    private IEnumerator MarkLocomotionAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (current == StateIdleVariant) current = StateLocomotion;
    }

    private void HandleJumped()
    {
        if (mode != Mode.Free || player.IsSwimming) return;
        jumpTimer = 0f;
        idleTimer = 0f;
        current = StateJump;
        animator.CrossFadeInFixedTime(StateJump, 0.08f, 0, JumpClipOffset);
    }

    private void Cross(string state, float duration)
    {
        current = state;
        animator.CrossFadeInFixedTime(state, duration, 0);
    }

    // ---------------------------------------------------------------
    // La cadera en su sitio
    // ---------------------------------------------------------------
    private void LateUpdate()
    {
        if (hips == null || !hipsRestReady) return;

        // Subiendo o manejando la cadera se mueve libre: la animación de
        // subir lleva el cuerpo hacia adentro del vehículo, y sentado la
        // acomoda VehicleSystem sobre el asiento.
        if (current == StateEnterCar || current == StateDrive) return;

        Vector3 p = hips.localPosition;
        bool travelling = current == StateLocomotion || current == StateSwim;

        if (current == StateDance || current == StateGreet)
        {
            // El baile tiene pasos de verdad (se desplaza por los costados y
            // hacia adelante): se le deja moverse, pero sin irse lejos.
            p.z = hipsRest.z + Mathf.Clamp(p.z - hipsRest.z, -1.2f, 1.2f);
            p.x = hipsRest.x + Mathf.Clamp(p.x - hipsRest.x, -1.2f, 1.2f);
        }
        else if (travelling)
        {
            // Caminar, correr y nadar avanzan la cadera todo el ciclo: se
            // anula ese avance (hacia adelante y de costado un poquito).
            p.z = hipsRest.z;
            p.x = hipsRest.x + Mathf.Clamp(p.x - hipsRest.x, -0.06f, 0.06f);
        }
        else
        {
            // En el resto solo se permite un vaivén chico.
            p.z = hipsRest.z + Mathf.Clamp(p.z - hipsRest.z, -0.15f, 0.15f);
            p.x = hipsRest.x + Mathf.Clamp(p.x - hipsRest.x, -0.15f, 0.15f);
        }

        // Al saltar la animación sube la cadera 30 cm, y el salto de verdad
        // ya lo hace el CharacterController: sumados, Kuntur volaba el doble.
        // Se deja que se agache (impulso y aterrizaje) pero no que suba.
        if (current == StateJump) p.y = Mathf.Min(p.y, hipsRest.y);

        hips.localPosition = p;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
