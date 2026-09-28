using UnityEngine;

// Perros y gato del valle: deambulan cerca de su lugar, se paran, miran,
// vuelven a caminar. Encima de eso, cada especie reacciona a Kuntur:
//
//  - Perro: jadea cuando pasas a su lado; si te quedas mucho rato encima
//    de él, se enoja y te ladra hasta que te alejas.
//  - Gato: maúlla solo de vez en cuando (con pausas largas) y, si lo
//    "saludas" con E, te mira y maúlla.
//
// No usa NavMesh: se mueve en línea recta dentro de un radio chico y se
// pega al suelo con un rayo, que para un patio o una chacra plana alcanza.
public class AnimalWander : MonoBehaviour, IInteractable
{
    // v56: el gatito de la casa se puede llevar en brazos (E) y soltar (E o G).
    public static AnimalWander CarriedCat { get; private set; }

    public enum Species { Perro, Gato }

    [Header("Especie y zona")]
    [SerializeField] private Species species = Species.Perro;
    [SerializeField] private Vector3 home;
    [SerializeField] private float wanderRadius = 6f;
    [SerializeField] private float walkSpeed = 1.2f;
    [SerializeField] private float turnSpeed = 4f;
    [SerializeField] private Vector2 idleTime = new Vector2(3f, 8f);
    [SerializeField, Range(0f, 1f)] private float extraChance = 0.3f; // perro juega / gato se sienta

    [Header("Animación (nombres de estados del Animator)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string walkState = "Walk";
    [SerializeField] private string extraState = "Extra";
    [SerializeField] private string voiceState = "Voz";   // ladrar / maullar

    [Header("Sonidos")]
    [SerializeField] private AudioClip[] voiceClips;      // maullidos / ladrido
    [SerializeField] private AudioClip nearClip;          // jadeo del perro
    [SerializeField] private float volume = 0.9f;

    [Header("Perro")]
    [SerializeField] private float nearRadius = 3.5f;     // a esta distancia jadea
    [SerializeField] private float annoyAfter = 7f;       // tanto rato encima y se enoja
    [SerializeField] private float calmRadius = 7f;       // se calma cuando te alejas así
    [SerializeField] private Vector2 barkInterval = new Vector2(1.3f, 2.4f);

    [Header("Gato")]
    [SerializeField] private Vector2 meowInterval = new Vector2(22f, 48f);

    private enum State { Idle, Walking, Extra, Voice, Angry }
    private State state = State.Idle;
    private float timer;
    private Vector3 target;
    private string currentAnim;
    private AudioSource source;
    private Transform player;

    private float nearTime;
    private bool wasNear;
    private float pantCooldown;
    private float barkTimer;
    private float meowTimer;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (home == Vector3.zero) home = transform.position;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = species == Species.Gato ? 14f : 22f;
        source.dopplerLevel = 0f;
    }

    private void Start()
    {
        timer = Random.Range(0.5f, idleTime.y);
        meowTimer = Random.Range(meowInterval.x * 0.4f, meowInterval.y);
        Play(idleState, 0f);
        // Que no se muevan todos al mismo compás.
        if (animator != null) animator.Update(Random.Range(0f, 2f));
    }

    private void Update()
    {
        if (player == null)
        {
            SimpleThirdPersonController p = SimpleThirdPersonController.Instance;
            if (p != null) player = p.transform;
        }

        if (carried) { UpdateCarried(); return; }

        if (species == Species.Perro) UpdateDogMood();
        else UpdateCatVoice();

        switch (state)
        {
            case State.Idle:
            case State.Extra:
                timer -= Time.deltaTime;
                if (timer <= 0f) PickNext();
                break;
            case State.Walking:
                WalkToTarget();
                break;
            case State.Voice:
                timer -= Time.deltaTime;
                FacePlayer();
                if (timer <= 0f) { state = State.Idle; timer = Random.Range(idleTime.x, idleTime.y); Play(idleState, 0.25f); }
                break;
            case State.Angry:
                FacePlayer();
                break;
        }
    }

    private void PickNext()
    {
        if (Random.value < extraChance)
        {
            state = State.Extra;
            timer = Random.Range(idleTime.x, idleTime.y);
            Play(extraState, 0.3f);
            return;
        }

        Vector2 r = Random.insideUnitCircle * wanderRadius;
        target = home + new Vector3(r.x, 0f, r.y);
        state = State.Walking;
        Play(walkState, 0.25f);
    }

    private void WalkToTarget()
    {
        Vector3 to = target - transform.position;
        to.y = 0f;
        if (to.magnitude < 0.3f)
        {
            state = State.Idle;
            timer = Random.Range(idleTime.x, idleTime.y);
            Play(idleState, 0.3f);
            return;
        }

        Quaternion look = Quaternion.LookRotation(to.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * turnSpeed);
        // Avanza hacia donde mira (así las curvas se ven naturales).
        Vector3 step = transform.forward * walkSpeed * Time.deltaTime;
        Vector3 pos = transform.position + step;
        pos.y = GroundY(pos);
        transform.position = pos;
    }

    private float GroundY(Vector3 pos)
    {
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return pos.y;
    }

    private void FacePlayer()
    {
        if (player == null) return;
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), Time.deltaTime * turnSpeed * 1.5f);
    }

    // ---------------------------------------------------------------
    // Perro: jadea al pasar, ladra si te quedas encima
    // ---------------------------------------------------------------
    private void UpdateDogMood()
    {
        if (player == null) return;
        float d = Vector3.Distance(player.position, transform.position);
        bool near = d < nearRadius;
        pantCooldown -= Time.deltaTime;

        if (state == State.Angry)
        {
            barkTimer -= Time.deltaTime;
            if (barkTimer <= 0f)
            {
                PlayVoice();
                barkTimer = Random.Range(barkInterval.x, barkInterval.y);
            }
            if (d > calmRadius)
            {
                // Ya se fue: se calma y vuelve a lo suyo.
                state = State.Idle;
                nearTime = 0f;
                timer = Random.Range(idleTime.x, idleTime.y);
                Play(idleState, 0.4f);
            }
            return;
        }

        // Pasaste a su lado: jadeo (una vez por pasada, no en bucle).
        if (near && !wasNear && pantCooldown <= 0f && nearClip != null)
        {
            source.PlayOneShot(nearClip, volume * AudioVolumeSettings.Sfx);
            pantCooldown = 6f;
        }
        wasNear = near;

        nearTime = near ? nearTime + Time.deltaTime : Mathf.Max(0f, nearTime - Time.deltaTime * 2f);
        if (nearTime >= annoyAfter)
        {
            state = State.Angry;
            barkTimer = 0f;
            Play(extraState, 0.2f); // se planta y "juega" a ladrar
        }
    }

    // ---------------------------------------------------------------
    // Gato: maúlla solo de vez en cuando
    // ---------------------------------------------------------------
    private void UpdateCatVoice()
    {
        meowTimer -= Time.deltaTime;
        if (meowTimer > 0f) return;
        meowTimer = Random.Range(meowInterval.x, meowInterval.y);
        if (state == State.Walking) return; // camina callado; ya maullará después
        Meow(false);
    }

    private void Meow(bool towardsPlayer)
    {
        PlayVoice();
        if (!string.IsNullOrEmpty(voiceState))
        {
            state = State.Voice;
            timer = 1.6f;
            Play(voiceState, 0.15f, true);
        }
        if (!towardsPlayer) timer = 1.4f;
    }

    private void PlayVoice()
    {
        if (voiceClips == null || voiceClips.Length == 0) return;
        AudioClip clip = voiceClips[Random.Range(0, voiceClips.Length)];
        if (clip == null) return;
        source.pitch = Random.Range(0.94f, 1.06f);
        source.PlayOneShot(clip, volume * AudioVolumeSettings.Sfx);
    }

    private void Play(string stateName, float fade, bool restart = false)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        if (currentAnim == stateName && !restart) return;
        currentAnim = stateName;
        animator.CrossFadeInFixedTime(stateName, fade, 0);
    }

    // ---------------------------------------------------------------
    // Interacción (solo el gato): E para saludarlo
    // ---------------------------------------------------------------
    // v56c: el gatito ya no se lleva en brazos (así lo pidió Jr): con E se
    // le saluda y maúlla, como antes.
    public void Interact()
    {
        if (species != Species.Gato) return;
        Meow(true);
        meowTimer = Random.Range(meowInterval.x, meowInterval.y);
    }

    public string GetPrompt() => species == Species.Gato ? "Saludar al gatito" : "";

    // ---------------------------------------------------------------
    // v56: llevar al gatito en brazos
    // ---------------------------------------------------------------
    private bool carried;
    private int pickedFrame;
    private int[] savedLayers;
    private Collider[] colliders;
    private Vector3 baseScale;

    // Delante del pecho de Kuntur, mirando adelante como él.
    private static readonly Vector3 ArmsOffset = new Vector3(0f, 0.84f, 0.46f);

    private void PickUp()
    {
        if (carried || player == null) return;
        if (CarriedCat != null && CarriedCat != this) return;
        carried = true;
        CarriedCat = this;
        pickedFrame = Time.frameCount;
        baseScale = transform.localScale;

        // Mientras va en brazos no es "interactuable": si no, la E siempre
        // apuntaría al gatito y no se podría recoger basura ni hablar.
        colliders = GetComponentsInChildren<Collider>(true);
        savedLayers = new int[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            savedLayers[i] = colliders[i].gameObject.layer;
            colliders[i].gameObject.layer = 0;
            colliders[i].enabled = false;
        }

        BackToArms();
        state = State.Idle;
        Play(!string.IsNullOrEmpty(extraState) ? extraState : idleState, 0.2f, true); // sentadito
        PlayVoice();
    }

    // En brazos de Kuntur (también al bajarse de un vehículo).
    public void BackToArms()
    {
        if (!carried || player == null) return;
        transform.SetParent(player, false);
        transform.localPosition = ArmsOffset;
        transform.localRotation = Quaternion.identity;
        transform.localScale = baseScale;
    }

    // Viaja sentado en el asiento de atrás del vehículo.
    public void RideIn(Transform seat)
    {
        if (!carried || seat == null) return;
        transform.SetParent(seat, false);
        transform.localPosition = new Vector3(0f, 0.05f, 0f);
        transform.localRotation = Quaternion.identity;
        transform.localScale = baseScale;
    }

    public void PutDown()
    {
        if (!carried) return;
        carried = false;
        if (CarriedCat == this) CarriedCat = null;

        Vector3 spot = player != null ? player.position + player.forward * 0.9f : transform.position;
        transform.SetParent(null, true);
        spot.y = GroundY(spot + Vector3.up * 0.5f);
        transform.position = spot;
        transform.rotation = Quaternion.Euler(0f, player != null ? player.eulerAngles.y : transform.eulerAngles.y, 0f);
        transform.localScale = baseScale;

        if (colliders != null)
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                colliders[i].gameObject.layer = savedLayers[i];
                colliders[i].enabled = true;
            }

        // Ahora su casa es donde lo dejaron: pasea por ahí.
        home = spot;
        state = State.Idle;
        timer = Random.Range(idleTime.x, idleTime.y);
        Play(idleState, 0.25f);
        PlayVoice();
    }

    private void UpdateCarried()
    {
        if (transform.parent == null) BackToArms();

        // Maullidos de vez en cuando, también en brazos.
        meowTimer -= Time.deltaTime;
        if (meowTimer <= 0f)
        {
            meowTimer = Random.Range(meowInterval.x, meowInterval.y);
            PlayVoice();
        }
    }

    private void LateUpdate()
    {
        if (!carried || Time.frameCount == pickedFrame) return;
        if (VehicleSystem.Driving) return;
        if (PauseMenu.IsOpen || InventoryUI.IsOpen || TutorialUI.Showing) return;
        UnityEngine.InputSystem.Keyboard k = UnityEngine.InputSystem.Keyboard.current;
        if (k == null) return;

        // G lo suelta siempre. E también, si no hay otra cosa con qué
        // interactuar (si hay basura al lado, la E es para la basura).
        bool g = k.gKey.wasPressedThisFrame;
        bool e = k.eKey.wasPressedThisFrame && InteractionSystem.LastInteractFrame != Time.frameCount
                 && !InteractionSystem.AnyTargetInRange;
        if (g || e) PutDown();
    }
    public InteractKey GetInteractKey() => InteractKey.Primary;
}
