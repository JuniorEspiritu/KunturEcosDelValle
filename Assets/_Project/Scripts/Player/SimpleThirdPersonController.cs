using UnityEngine;
using UnityEngine.InputSystem;

// Controlador de tercera persona propio (no usa "Starter Assets" de Unity
// porque ese paquete requiere Asset Store con sesión iniciada). Cubre lo que
// pide el GDD: caminar, correr y saltar, más una cámara en tercera persona
// que orbita con el mouse. Implementado con CharacterController: simple,
// predecible y suficiente para un proyecto de curso.
[RequireComponent(typeof(CharacterController))]
public class SimpleThirdPersonController : MonoBehaviour
{
    [Header("Movimiento")]
    // Bajadas a propósito: a 4 y 7.5 m/s Kuntur cruzaba el pueblo en nada y
    // se veía patinando, porque ningún paso alcanza a acompañar esa velocidad.
    // A 2.8 se camina y a 5 se corre, que es lo que la animación puede seguir.
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 8.5f;

    [Header("Energía para correr")]
    // Correr gasta energía; recoger basura y ganar estrellas la recarga. Así
    // cumplir con el valle te hace más rápido para la siguiente misión.
    [SerializeField] private float energyDrainPerSecond = 0.16f;
    [SerializeField] private float energyRegenPerSecond = 0.035f;
    [SerializeField] private float tiredSprintSpeed = 5f;       // sin energía se trota nomás
    [SerializeField] private float jumpHeight = 1.3f;
    [SerializeField] private float gravity = -20f;

    [Header("Agacharse (tecla C)")]
    [SerializeField] private float crouchSpeed = 1.7f;
    [SerializeField] private float crouchHeight = 1f;

    [Header("Nadar (río Mantaro)")]
    // Kuntur entra al río caminando por la orilla y, cuando el agua le pasa
    // de la cintura, se pone a nadar: flota a ras del agua en vez de caminar
    // por el fondo. Para salir basta con nadar hasta la orilla y subir.
    [SerializeField] private float swimSpeed = 1.8f;
    [SerializeField] private float swimSprintSpeed = 2.6f;
    [SerializeField] private float swimDepthIdle = 0.85f;    // quieto (nadar1): flota parado, con el agua al pecho
    [SerializeField] private float swimDepthMoving = 0.45f;  // nadando (nadar2): el cuerpo va horizontal, más arriba
    [SerializeField] private float swimEnterDepth = 0.8f;    // los pies a esta profundidad -> empieza a nadar
    [SerializeField] private float swimExitDepth = 0.35f;    // los pies más arriba que esto -> vuelve a caminar

    [Header("Cámara (tercera persona)")]
    [SerializeField] private Transform cameraPivot; // hijo del jugador; rota en X (pitch)
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 60f;
    // Encuadre "detrás del hombro", como en las imágenes de referencia:
    // cámara baja y cerca, apenas por encima de la cabeza de Kuntur, mirando
    // un poco hacia abajo. Kuntur queda al centro, abajo, de espaldas, y se
    // ve la calle entera hacia adelante.
    [SerializeField] private float pivotHeight = 1.5f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0.75f, -3.7f);
    [SerializeField] private float startPitch = 7f;

    [Header("Modos de cámara (tecla Z, como en GTA)")]
    // Cerca (por defecto): Kuntur grande en pantalla, detrás del hombro.
    // Lejos: la de antes, se ve más calle. Primera persona: desde sus ojos.
    [SerializeField] private float closePivotHeight = 1.3f;
    [SerializeField] private Vector3 closeOffset = new Vector3(0f, 0.3f, -2.2f);
    [SerializeField] private float firstPersonHeight = 1.38f;
    [SerializeField] private Vector3 firstPersonOffset = new Vector3(0f, 0f, 0.18f);
    [SerializeField] private float cameraBlendSpeed = 7f;

    [Header("Ver la cara (mantener V)")]
    // La cámara normal siempre mira hacia donde camina Kuntur, así que solo
    // se le ve la espalda. Manteniendo V, la cámara orbita 180° alrededor del
    // pivote para mostrar el frente/cara sin cambiar hacia dónde se mueve el
    // jugador (el movimiento sigue usando transform.forward del jugador, no
    // el de la cámara).
    [SerializeField] private float faceViewSpeed = 6f;

    [Header("Input (asset PlayerControls)")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference jumpAction;

    private CharacterController controller;
    private float verticalVelocity;
    private float pitch;
    private float faceViewYaw;
    private bool jumpQueued;
    private bool cursorFreedByCtrl;
    private bool crouching;
    private float standHeight;
    private Vector3 standCenter;

    private Vector3 spawnPosition;

    public enum CameraMode { Cerca, Lejos, PrimeraPersona }
    public CameraMode CurrentCameraMode { get; private set; } = CameraMode.Cerca;
    private const string CameraModeKey = "Kuntur_ModoCamara";
    private float currentPivotHeight;
    private Vector3 currentCamOffset;
    private Renderer[] bodyRenderers;
    private bool bodyHidden;

    // Lo prende DayManager mientras pasa la noche.
    public static bool Frozen { get; set; }

    // v56: sentado en un vehículo (ver VehicleSystem). El cuerpo lo lleva el
    // vehículo; acá solo se maneja la cámara.
    public bool InVehicle { get; private set; }
    private float vehicleCameraDistance = 6f;
    private float vehicleYaw;
    private float vehicleLookIdle;

    public void SetVehicleMode(bool on, float cameraDistance)
    {
        InVehicle = on;
        if (cameraDistance > 0f) vehicleCameraDistance = cameraDistance;
        controller.enabled = !on;
        verticalVelocity = on ? 0f : -1f;
        jumpQueued = false;
        crouching = false;
        vehicleYaw = 0f;
        vehicleLookIdle = 0f;
        HorizontalSpeed = 0f;
        if (on) pitch = 10f;
        if (!on)
        {
            // Al bajar, Kuntur queda derecho (el vehículo pudo estar en la cuesta).
            Vector3 e = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, e.y, 0f);
            spawnPosition = transform.position;
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    // Hacia dónde camina Kuntur (último movimiento real). Lo usa la flechita
    // del minimapa: tiene que señalar hacia donde uno se mueve.
    public Vector3 LastMoveDirection { get; private set; } = Vector3.forward;

    // 0..1. La lee la barra de ENERGÍA del HUD.
    public float Energy { get; private set; } = 1f;
    public bool IsSprinting { get; private set; }
    public static SimpleThirdPersonController Instance { get; private set; }

    // Grabación del tráiler (lo usa KunturTrailerCapture).
    public static Vector2 DebugMove;

    public static void AddEnergy(float amount)
    {
        if (Instance != null) Instance.Energy = Mathf.Clamp01(Instance.Energy + amount);
    }

    // Lo lee KunturBoneAnimator para doblarle las patas y bajarle el cuerpo.
    public bool IsCrouching => crouching;

    // --- Lo que lee el Animator de Kuntur (KunturMixamoAnimator) ---
    public bool IsSwimming { get; private set; }
    public bool IsGrounded => controller != null && controller.isGrounded;
    public float VerticalVelocity => verticalVelocity;
    // Velocidad horizontal real de este frame (0 si no se movió: diálogo,
    // pausa, recogiendo algo...). Mejor que CharacterController.velocity,
    // que se queda con el último valor cuando no se llama a Move.
    public float HorizontalSpeed { get; private set; }
    public event System.Action Jumped;

    // Mientras Kuntur recoge una bolsa o toma una muestra no camina: se queda
    // quieto (y mirando hacia lo que recoge) hasta que termina la animación.
    private float actionLockUntil;
    private Vector3? actionFaceTarget;
    public bool IsActionLocked => Time.time < actionLockUntil;

    public void LockForAction(float seconds, Vector3? faceTarget = null)
    {
        actionLockUntil = Mathf.Max(actionLockUntil, Time.time + seconds);
        actionFaceTarget = faceTarget;
        jumpQueued = false;
    }

    // Gira a Kuntur de golpe hacia un punto (el vecino con el que habla).
    public void FaceTowards(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized);
        LastMoveDirection = transform.forward;
    }

    private void Awake()
    {
        Instance = this;
        controller = GetComponent<CharacterController>();
        spawnPosition = transform.position;
        standHeight = controller.height;
        standCenter = controller.center;
        CurrentCameraMode = (CameraMode)Mathf.Clamp(PlayerPrefs.GetInt(CameraModeKey, 0), 0, 2);
        ApplyCameraFraming();
    }

    private void Start()
    {
        // El cuerpo de Kuntur (para ocultarlo en primera persona, dejando
        // solo su sombra). Todo lo que cuelga de "Visual".
        Transform visual = transform.Find("Visual");
        bodyRenderers = visual != null ? visual.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
    }

    private void TargetFraming(out float pivotH, out Vector3 offset)
    {
        // Manejando: cámara detrás y más arriba, para ver la calle por
        // encima del vehículo (en primera persona se queda en los ojos).
        if (InVehicle && CurrentCameraMode != CameraMode.PrimeraPersona)
        {
            float far = CurrentCameraMode == CameraMode.Lejos ? 1.25f : 1f;
            pivotH = 1.35f;
            offset = new Vector3(0f, 0.55f + vehicleCameraDistance * 0.06f, -vehicleCameraDistance * far);
            return;
        }
        switch (CurrentCameraMode)
        {
            case CameraMode.Lejos: pivotH = pivotHeight; offset = cameraOffset; break;
            case CameraMode.PrimeraPersona: pivotH = firstPersonHeight; offset = firstPersonOffset; break;
            default: pivotH = closePivotHeight; offset = closeOffset; break;
        }
    }

    // Z: cerca -> lejos -> primera persona -> cerca...
    private void CycleCameraMode()
    {
        CurrentCameraMode = (CameraMode)(((int)CurrentCameraMode + 1) % 3);
        PlayerPrefs.SetInt(CameraModeKey, (int)CurrentCameraMode);
        if (CurrentCameraMode == CameraMode.PrimeraPersona) pitch = Mathf.Clamp(pitch, -60f, 60f);
    }

    // La cámara se desliza hacia el encuadre del modo elegido (sin saltos).
    private void LateUpdate()
    {
        if (cameraPivot == null) return;
        KunturCinematicCamera cinematic = KunturCinematicCamera.Instance;
        bool cinematicBusy = cinematic != null && cinematic.Busy;

        // En primera persona no se ve el cuerpo (si no, la cámara queda
        // dentro de la cabeza); en las tomas de diálogo o baile, sí.
        bool hide = CurrentCameraMode == CameraMode.PrimeraPersona && !cinematicBusy;
        if (hide != bodyHidden && bodyRenderers != null)
        {
            bodyHidden = hide;
            foreach (Renderer r in bodyRenderers)
            {
                if (r == null) continue;
                r.shadowCastingMode = hide ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                                           : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        if (cinematicBusy) return;

        TargetFraming(out float targetH, out Vector3 targetOffset);
        float k = 1f - Mathf.Exp(-cameraBlendSpeed * Time.unscaledDeltaTime);
        currentPivotHeight = Mathf.Lerp(currentPivotHeight, targetH, k);
        currentCamOffset = Vector3.Lerp(currentCamOffset, targetOffset, k);

        cameraPivot.localPosition = new Vector3(0f, currentPivotHeight, 0f);
        Camera cam = cameraPivot.GetComponentInChildren<Camera>();
        if (cam != null && cam.transform.parent == cameraPivot)
        {
            cam.transform.localPosition = currentCamOffset;
            cam.transform.localRotation = Quaternion.identity;
            KeepCameraOutOfWalls(cam.transform);
        }
    }

    // v56c: si entre Kuntur y la cámara hay una pared, un letrero o un poste,
    // la cámara se acerca en vez de meterse adentro (se veían las letras de
    // los letreros gigantes y al revés).
    private static readonly RaycastHit[] CamHits = new RaycastHit[16];
    private void KeepCameraOutOfWalls(Transform cam)
    {
        if (CurrentCameraMode == CameraMode.PrimeraPersona) return;
        Vector3 from = cameraPivot.position;
        Vector3 to = cam.position - from;
        float dist = to.magnitude;
        if (dist < 0.3f) return;
        Vector3 dir = to / dist;
        int n = Physics.SphereCastNonAlloc(from, 0.22f, dir, CamHits, dist, ~0, QueryTriggerInteraction.Ignore);
        float best = dist;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = CamHits[i];
            if (h.distance <= 0f || h.collider == null) continue;
            if (h.collider.GetComponentInParent<SimpleThirdPersonController>() != null) continue;
            if (h.collider.GetComponentInParent<DrivableVehicle>() != null) continue;
            if (h.collider.GetComponentInParent<CarPatrol>() != null) continue;   // un auto que pasa no mueve la cámara
            if (h.collider.GetComponentInParent<IInteractable>() != null) continue;
            if (h.distance < best) best = h.distance;
        }
        if (best < dist) cam.position = from + dir * Mathf.Max(0.35f, best - 0.12f);
    }

    // Se aplica al arrancar (y no solo al construir la escena) para que el
    // encuadre valga aunque la escena se haya armado con valores viejos.
    private void ApplyCameraFraming()
    {
        if (cameraPivot == null) return;
        TargetFraming(out currentPivotHeight, out currentCamOffset);
        cameraPivot.localPosition = new Vector3(0f, currentPivotHeight, 0f);

        Camera cam = cameraPivot.GetComponentInChildren<Camera>();
        if (cam != null && cam.transform.parent == cameraPivot)
        {
            cam.transform.localPosition = currentCamOffset;
            cam.transform.localRotation = Quaternion.identity;
        }

        pitch = startPitch;
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void OnEnable()
    {
        if (moveAction != null && moveAction.action != null) moveAction.action.Enable();
        if (lookAction != null && lookAction.action != null) lookAction.action.Enable();
        if (sprintAction != null && sprintAction.action != null) sprintAction.action.Enable();
        if (jumpAction != null && jumpAction.action != null)
        {
            jumpAction.action.Enable();
            jumpAction.action.performed += OnJumpPerformed;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        if (jumpAction != null && jumpAction.action != null)
        {
            jumpAction.action.performed -= OnJumpPerformed;
            jumpAction.action.Disable();
        }
        if (moveAction != null && moveAction.action != null) moveAction.action.Disable();
        if (lookAction != null && lookAction.action != null) lookAction.action.Disable();
        if (sprintAction != null && sprintAction.action != null) sprintAction.action.Disable();
    }

    private void Update()
    {
        // El clímax también se juega caminando (es la fase con la cuenta
        // regresiva): solo el diálogo y la pantalla de resultado detienen a
        // Kuntur y liberan el mouse.
        HorizontalSpeed = 0f;

        // Con la pausa abierta no se camina NI se gira la cámara. No alcanza
        // con que Time.timeScale sea 0: el giro de cámara usa el movimiento
        // del mouse, que no pasa por deltaTime, así que seguiría girando
        // mientras el jugador clickea los controles de la pantalla.
        // Con la mochila abierta tampoco (v55): el mouse es para la mochila.
        // Y al cerrar cualquiera de las dos se ignoran unos cuadros de mouse:
        // al volver a bloquear el puntero Unity entrega de golpe todo lo que
        // se movió el mouse mientras estaba libre, y Kuntur giraba solo.
        if (PauseMenu.IsOpen || InventoryUI.IsOpen || MapSystem.FullMapOpen)
        {
            lookSuppressFrames = 4;
            return;
        }

        // Durmiendo (la noche pasando) Kuntur no se mueve.
        if (Frozen) return;

        // Con el mapa completo abierto tampoco: el mouse está arrastrando el
        // mapa, y si además girara la cámara, al cerrarlo Kuntur estaría
        // mirando a cualquier lado.
        if (MapSystem.FullMapOpen) return;

        bool canMove = GameManager.Instance == null
            || GameManager.Instance.CurrentState == GameState.Exploracion
            || GameManager.Instance.CurrentState == GameState.Climax;

        if (!canMove)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }

        // Escape libera el mouse para poder revisar el Inspector sin salir del Play.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
            && DialogueUI.LastClosedFrame != Time.frameCount)
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
            cursorFreedByCtrl = false;
        }

        // MANTENER CTRL saca el puntero del mouse sin salir del juego: sirve
        // para clicar los botones de la pantalla (inventario, opciones de
        // diálogo) y al soltarlo la cámara vuelve al control normal.
        bool ctrlHeld = Keyboard.current != null &&
                        (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);

        if (ctrlHeld && !cursorFreedByCtrl)
        {
            cursorFreedByCtrl = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (!ctrlHeld && cursorFreedByCtrl)
        {
            cursorFreedByCtrl = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Clic en pantalla vuelve a bloquear el mouse si fue liberado
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Salto con teclado directo como respaldo
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            // Saltar estando agachado primero lo pone de pie: es lo que uno
            // espera al apretar espacio, en vez de que no pase nada.
            if (crouching) crouching = false;
            else if (!IsSwimming && !IsActionLocked) jumpQueued = true;
        }

        // Z cambia la cámara: cerca, lejos, primera persona.
        if (Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame) CycleCameraMode();

        // C agacha y vuelve a levantar (interruptor, no mantener pulsado).
        // (El Kuntur de Mixamo no trae animación de agacharse: con él, la C
        // no hace nada en vez de hacerlo caminar lento sin que se note por qué.)
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame && !IsSwimming && !InVehicle
            && KunturMixamoAnimator.Instance == null)
        {
            crouching = !crouching;
        }
        if (IsSwimming) crouching = false;

        if (InVehicle)
        {
            jumpQueued = false;
            HandleVehicleLook();
            return;
        }

        UpdateCrouchCollider();

        HandleLook();
        HandleMove();
        CheckFellOutOfWorld();
    }

    // Manejando, el mouse ya no gira a Kuntur (lo gira el volante): orbita la
    // cámara alrededor del vehículo, y si se suelta el mouse un momento, la
    // cámara vuelve sola detrás.
    private void HandleVehicleLook()
    {
        HorizontalSpeed = 0f;
        if (Cursor.lockState != CursorLockMode.Locked) { lookSuppressFrames = 4; return; }
        if (lookSuppressFrames > 0)
        {
            lookSuppressFrames--;
            if (lookAction != null && lookAction.action != null) lookAction.action.ReadValue<Vector2>();
            return;
        }

        Vector2 look = Vector2.zero;
        if (lookAction != null && lookAction.action != null) look = lookAction.action.ReadValue<Vector2>();
        else if (Mouse.current != null) look = Mouse.current.delta.ReadValue();

        if (look.sqrMagnitude > 0.5f) vehicleLookIdle = 0f;
        else vehicleLookIdle += Time.deltaTime;

        vehicleYaw += look.x * mouseSensitivity;
        if (vehicleLookIdle > 1.6f)
            vehicleYaw = Mathf.MoveTowardsAngle(vehicleYaw, 0f, 120f * Time.deltaTime);

        bool fp = CurrentCameraMode == CameraMode.PrimeraPersona;
        pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, fp ? -50f : -10f, fp ? 50f : 45f);
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, vehicleYaw, 0f);
    }

    // La cápsula de colisión se encoge al agacharse, si no el cóndor se ve
    // agachado pero sigue chocando con la altura de cuando está de pie.
    private void UpdateCrouchCollider()
    {
        float targetHeight = crouching ? crouchHeight : standHeight;
        if (Mathf.Approximately(controller.height, targetHeight)) return;

        controller.height = Mathf.MoveTowards(controller.height, targetHeight, Time.deltaTime * 4f);
        // El centro acompaña a la altura para que los pies no se hundan.
        controller.center = new Vector3(standCenter.x, controller.height / 2f, standCenter.z);
    }

    // Red de seguridad: si por lo que sea Kuntur se cae fuera del mundo (un
    // borde del terreno, el cauce del río), en vez de caer para siempre vuelve
    // al punto de partida. Sin esto, un hueco cualquiera obliga a reiniciar.
    private void CheckFellOutOfWorld()
    {
        if (transform.position.y > -12f) return;

        controller.enabled = false; // el CharacterController ignora los cambios de posición si está activo
        transform.position = spawnPosition;
        controller.enabled = true;
        verticalVelocity = 0f;
    }

    // Aparecer en un punto (la puerta de la casa al empezar el día).
    public void Teleport(Vector3 position, float yaw)
    {
        controller.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        controller.enabled = true;
        verticalVelocity = 0f;
        spawnPosition = position;
        LastMoveDirection = transform.forward;
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (!IsSwimming && !IsActionLocked) jumpQueued = true;
    }

    private int lookSuppressFrames;

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) { lookSuppressFrames = 4; return; }
        if (lookSuppressFrames > 0)
        {
            lookSuppressFrames--;
            if (lookAction != null && lookAction.action != null) lookAction.action.ReadValue<Vector2>();
            return;
        }

        Vector2 look = Vector2.zero;
        if (lookAction != null && lookAction.action != null)
        {
            look = lookAction.action.ReadValue<Vector2>();
        }
        else if (Mouse.current != null)
        {
            look = Mouse.current.delta.ReadValue();
        }

        // Mantén V para que la cámara dé la vuelta y muestres la cara de
        // Kuntur (por defecto la cámara solo ve su espalda, igual que en
        // cualquier juego de tercera persona). Mientras se mantiene V, el
        // mouse ya no gira al jugador: en cambio orbita la cámara a mano
        // (targetYaw += look.x), para poder mirar la cara desde distintos
        // ángulos. Al soltar V, targetYaw vuelve suavemente a 0° y el mouse
        // recupera el control normal de giro del personaje.
        bool wantsFaceView = Keyboard.current != null && Keyboard.current.vKey.isPressed
                             && CurrentCameraMode != CameraMode.PrimeraPersona;

        if (wantsFaceView)
        {
            faceViewYaw += look.x * mouseSensitivity;
        }
        else
        {
            // Recogiendo algo, el mouse no le gira el cuerpo: se queda mirando
            // la bolsa hasta terminar (si no, la mano agarra el aire).
            if (!IsActionLocked) transform.Rotate(Vector3.up, look.x * mouseSensitivity);
            faceViewYaw = Mathf.MoveTowardsAngle(faceViewYaw, 0f, faceViewSpeed * 60f * Time.deltaTime);
        }

        bool fp = CurrentCameraMode == CameraMode.PrimeraPersona;
        pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, fp ? -65f : minPitch, fp ? 65f : maxPitch);

        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(pitch, faceViewYaw, 0f);
    }

    private void HandleMove()
    {
        Vector2 input = Vector2.zero;
        if (moveAction != null && moveAction.action != null)
        {
            input = moveAction.action.ReadValue<Vector2>();
        }
        else if (Keyboard.current != null)
        {
            float x = 0f;
            float y = 0f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
            input = new Vector2(x, y);
        }

        // Solo para grabar el tráiler: caminar sin teclado.
        if (DebugMove != Vector2.zero) input = DebugMove;

        bool sprinting = false;
        if (sprintAction != null && sprintAction.action != null)
        {
            sprinting = sprintAction.action.IsPressed();
        }
        else if (Keyboard.current != null)
        {
            sprinting = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }

        // Recogiendo una bolsa o tomando una muestra: quieto, girando hacia
        // lo que recoge.
        if (IsActionLocked)
        {
            input = Vector2.zero;
            sprinting = false;
            if (actionFaceTarget.HasValue)
            {
                Vector3 to = actionFaceTarget.Value - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 10f);
            }
        }

        Vector3 moveDir = transform.right * input.x + transform.forward * input.y;
        moveDir = Vector3.ClampMagnitude(moveDir, 1f);
        if (moveDir.sqrMagnitude > 0.04f) LastMoveDirection = moveDir.normalized;
        else LastMoveDirection = transform.forward;
        // Agachado se camina lento, y no se puede correr aunque se mantenga
        // Shift: si no, agacharse no tendría ningún costo.
        bool moving = moveDir.sqrMagnitude > 0.04f;

        UpdateSwimState();
        if (IsSwimming)
        {
            SwimMove(moveDir, moving && sprinting);
            return;
        }

        IsSprinting = sprinting && moving && !crouching;
        if (IsSprinting) Energy = Mathf.Max(0f, Energy - energyDrainPerSecond * Time.deltaTime);
        else Energy = Mathf.Min(1f, Energy + energyRegenPerSecond * Time.deltaTime);
        float runSpeed = Energy > 0.01f ? sprintSpeed : tiredSprintSpeed;
        float speed = crouching ? crouchSpeed : (IsSprinting ? runSpeed : walkSpeed);

        if (controller.isGrounded)
        {
            verticalVelocity = -1f;
            if (jumpQueued)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                Jumped?.Invoke();
            }
        }
        jumpQueued = false;

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDir * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
        HorizontalSpeed = moving ? speed * moveDir.magnitude : 0f;
    }

    // ---------------------------------------------------------------
    // Nadar
    // ---------------------------------------------------------------
    // Se entra al agua cuando los pies quedan bastante por debajo de la
    // superficie y se sale cuando vuelven a acercarse a ella (al subir la
    // orilla). Los dos umbrales distintos evitan que en el borde Kuntur
    // cambie de nadar a caminar y de vuelta a cada frame.
    private float swimMoveBlend;

    private void UpdateSwimState()
    {
        RiverWater water = RiverWater.Active;
        if (water == null) { IsSwimming = false; return; }

        float feetDepth = water.SurfaceY - transform.position.y;
        bool insideRiver = water.Contains(transform.position, 0.5f);

        if (!IsSwimming)
        {
            if (insideRiver && feetDepth > swimEnterDepth)
            {
                IsSwimming = true;
                verticalVelocity = 0f;
                crouching = false;
            }
        }
        else if (!insideRiver || (feetDepth < swimExitDepth && controller.isGrounded))
        {
            IsSwimming = false;
            verticalVelocity = -1f;
        }
    }

    private void SwimMove(Vector3 moveDir, bool fast)
    {
        IsSprinting = false;
        // Nadar cansa menos que correr: la energía se recupera, despacito.
        Energy = Mathf.Min(1f, Energy + energyRegenPerSecond * 0.5f * Time.deltaTime);

        bool moving = moveDir.sqrMagnitude > 0.04f;
        swimMoveBlend = Mathf.MoveTowards(swimMoveBlend, moving ? 1f : 0f, Time.deltaTime * 2f);

        // Flota: el cuerpo se acomoda a la altura del agua (más arriba cuando
        // nada, porque va horizontal; más abajo cuando flota parado).
        float surface = RiverWater.Active != null ? RiverWater.Active.SurfaceY : transform.position.y;
        float targetY = surface - Mathf.Lerp(swimDepthIdle, swimDepthMoving, swimMoveBlend);
        float vertical = Mathf.Clamp((targetY - transform.position.y) * 4f, -2.5f, 2.5f);
        verticalVelocity = 0f;

        float speed = fast ? swimSprintSpeed : swimSpeed;
        Vector3 velocity = moveDir * speed + Vector3.up * vertical;
        controller.Move(velocity * Time.deltaTime);
        HorizontalSpeed = moving ? speed * moveDir.magnitude : 0f;
    }
}
