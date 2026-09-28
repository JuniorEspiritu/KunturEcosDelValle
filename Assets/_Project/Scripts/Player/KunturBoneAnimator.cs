using UnityEngine;

// Anima a Kuntur moviéndole los HUESOS, sin necesitar ningún clip.
//
// El modelo viene rigueado (28 huesos) pero sin animación, así que el ciclo de
// caminar, correr, saltar y agacharse se calcula acá con senos y cosenos.
//
// El detalle que hace que esto funcione: cada hueso NO se gira sobre su propio
// eje local. Blender orienta cada hueso como quiere, así que "girar en X" da
// un resultado distinto en cada uno y las patas salen torcidas. En vez de eso
// se gira alrededor del eje DERECHA (o ADELANTE) del personaje, convertido al
// espacio del padre de cada hueso: así una pata siempre se columpia hacia
// adelante y atrás, sin importar cómo esté orientado su hueso.
public class KunturBoneAnimator : MonoBehaviour
{
    [Header("Referencias")]
    // Apágalo (desde el menú Kuntur, o desmarcándolo en el Inspector) si la
    // animación de patas deforma el modelo: el cóndor se sigue moviendo por el
    // pueblo igual, solo que sin mover las patas. Vale más un cóndor quieto
    // bien hecho que uno articulado que se descuadra.
    // Encendido. Si en algún momento el cóndor se vuelve a descuadrar, se
    // apaga al instante desde el menú Kuntur > Animación de patas, sin tocar
    // nada más: el personaje sigue moviéndose por el pueblo con el rebote y la
    // inclinación del cuerpo, que no pueden deformarlo.
    [SerializeField] private bool animateBones = true;
    [SerializeField] private CharacterController controller;
    [SerializeField] private SimpleThirdPersonController playerController;
    // El transform del modelo entero: se usa para el rebote y la inclinación.
    [SerializeField] private Transform modelRoot;

    [Header("Huesos")]
    // Cada pata, de la cadera hacia abajo: cadera, rodilla, pie.
    [SerializeField] private Transform[] leftLeg;
    [SerializeField] private Transform[] rightLeg;
    // Opcionales: si el rig no los tiene identificados, se quedan vacíos y
    // esas partes simplemente no se animan.
    [SerializeField] private Transform[] leftWing;
    [SerializeField] private Transform[] rightWing;
    [SerializeField] private Transform neck;

    [Header("Caminar")]
    // ====== SI QUIERES LOS PASOS MÁS LENTOS O MÁS RÁPIDOS, ES ESTE NÚMERO ======
    // Zancadas por metro recorrido. Más bajo = pasos más largos y pausados;
    // más alto = pasos más cortos y rápidos. Empezó en 1.6, que a la velocidad
    // de entonces daban SEIS ciclos por segundo: de ahí el bamboleo de
    // pingüino apurado.
    [SerializeField] private float stridePerMeter = 0.34f;
    // Tope de pasos por segundo pase lo que pase. Aunque corra o lo empuje
    // algo, el paso nunca se acelera más allá de esto, que es el límite a
    // partir del cual deja de leerse como caminar y parece vibrar.
    [SerializeField] private float maxStepsPerSecond = 1.5f;
    [SerializeField] private float hipSwing = 19f;          // grados que se columpia la cadera
    // Más rodilla y menos cadera: el paso se lee como un paso en vez de como
    // un péndulo, y el cuerpo deja de irse de lado en cada zancada.
    [SerializeField] private float kneeBend = 27f;
    [SerializeField] private float bodyBob = 0.038f;        // rebote vertical del cuerpo
    // El balanceo lateral es JUSTO lo que hace que camine como pingüino.
    // Queda apenas insinuado.
    [SerializeField] private float bodySway = 0.9f;

    [Header("Correr")]
    // Justo por encima de la velocidad de caminar (2.8), para que al apretar
    // Shift se note el cambio de zancada.
    [SerializeField] private float runSpeedThreshold = 3.4f;
    [SerializeField] private float runLean = 12f;           // inclinación adelante al correr

    [Header("Salto")]
    [SerializeField] private float jumpTuck = 18f;          // las patas se recogen en el aire
    [SerializeField] private float wingSpread = 45f;        // las alas se abren al saltar
    // isGrounded del CharacterController parpadea solo: en un desnivel o un
    // borde de vereda devuelve false un frame suelto aunque se esté caminando
    // tranquilo. Sin este margen, cada parpadeo recogía las patas de golpe y
    // el cóndor se veía descuadrarse al andar.
    [SerializeField] private float airborneGrace = 0.18f;

    [Header("Agacharse")]
    [SerializeField] private float crouchDrop = 0.32f;      // cuánto baja el cuerpo
    [SerializeField] private float crouchFold = 38f;        // cuánto se doblan las patas

    private Quaternion[] leftLegRest;
    private Quaternion[] rightLegRest;
    private Quaternion[] leftWingRest;
    private Quaternion[] rightWingRest;
    private Quaternion neckRest;

    private Vector3 modelRestPosition;
    private Quaternion modelRestRotation;

    private float stridePhase;
    private float airBlend;     // 0 en el suelo, 1 en el aire
    private float crouchBlend;  // 0 de pie, 1 agachado
    private float moveBlend;    // 0 quieto, 1 caminando a tope
    private float airborneTime; // cuánto lleva sin pisar suelo, de verdad

    private void Start()
    {
        if (controller == null) controller = GetComponentInParent<CharacterController>();
        if (playerController == null) playerController = GetComponentInParent<SimpleThirdPersonController>();
        if (modelRoot == null) modelRoot = transform;

        modelRestPosition = modelRoot.localPosition;
        modelRestRotation = modelRoot.localRotation;

        leftLegRest = CaptureRest(leftLeg);
        rightLegRest = CaptureRest(rightLeg);
        leftWingRest = CaptureRest(leftWing);
        rightWingRest = CaptureRest(rightWing);
        if (neck != null) neckRest = neck.localRotation;
    }

    private static Quaternion[] CaptureRest(Transform[] bones)
    {
        if (bones == null) return new Quaternion[0];

        Quaternion[] rest = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            rest[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
        return rest;
    }

    private void LateUpdate()
    {
        // LateUpdate a propósito: así se corre DESPUÉS de que el
        // CharacterController ya movió al personaje este frame.
        float speed = 0f;
        bool grounded = true;

        if (controller != null)
        {
            Vector3 horizontal = controller.velocity;
            horizontal.y = 0f;
            speed = horizontal.magnitude;
            grounded = controller.isGrounded;
        }

        bool crouching = playerController != null && playerController.IsCrouching;

        // Solo se considera "en el aire" tras un rato sin pisar, no al primer
        // frame: así un bordillo o una rampa no le recogen las patas.
        airborneTime = grounded ? 0f : airborneTime + Time.deltaTime;
        bool reallyAirborne = airborneTime > airborneGrace;

        // Mezclas suaves: sin esto los cambios se ven como saltos bruscos.
        airBlend = Mathf.MoveTowards(airBlend, reallyAirborne ? 1f : 0f, Time.deltaTime * 6f);
        crouchBlend = Mathf.MoveTowards(crouchBlend, crouching ? 1f : 0f, Time.deltaTime * 7f);
        moveBlend = Mathf.MoveTowards(moveBlend, speed > 0.2f ? 1f : 0f, Time.deltaTime * 8f);

        // La zancada avanza con los METROS recorridos, no con el tiempo: así
        // los pasos siempre pegan con el avance y nunca patina. El tope evita
        // que a alta velocidad el paso se vuelva un temblor.
        float stepsPerSecond = Mathf.Min(speed * stridePerMeter, maxStepsPerSecond);
        stridePhase += stepsPerSecond * Time.deltaTime * Mathf.PI * 2f;
        if (stridePhase > Mathf.PI * 4f) stridePhase -= Mathf.PI * 4f;

        float running = Mathf.Clamp01((speed - runSpeedThreshold) / 3f);
        float amplitude = moveBlend * (1f + running * 0.5f) * (1f - airBlend) * (1f - crouchBlend * 0.6f);

        if (animateBones)
        {
            AnimateLegs(amplitude, running);
            AnimateWings();
        }

        // El rebote y la inclinación del cuerpo entero SÍ se mantienen aunque
        // se apaguen los huesos: eso mueve el modelo completo, no puede
        // deformarlo, y ya hace que no se vea como una estatua deslizándose.
        AnimateBody(speed, running);
    }

    private void AnimateLegs(float amplitude, float running)
    {
        float swing = Mathf.Sin(stridePhase) * hipSwing * amplitude;

        // En el aire las dos patas se recogen; agachado, las dos se doblan.
        float tuck = jumpTuck * airBlend;
        float fold = crouchFold * crouchBlend;

        ApplyLeg(leftLeg, leftLegRest, swing, tuck, fold, running, amplitude);
        ApplyLeg(rightLeg, rightLegRest, -swing, tuck, fold, running, amplitude);
    }

    private void ApplyLeg(Transform[] leg, Quaternion[] rest, float swing, float tuck, float fold,
        float running, float amplitude)
    {
        if (leg == null || leg.Length == 0) return;

        // La rodilla se dobla cuando la pata va hacia atrás (valor negativo del
        // seno): es lo que hace que el paso se lea como un paso y no como un
        // péndulo rígido.
        float bend = Mathf.Max(0f, -Mathf.Sin(stridePhase)) * kneeBend * amplitude * (1f + running * 0.6f);

        for (int i = 0; i < leg.Length; i++)
        {
            if (leg[i] == null || i >= rest.Length) continue;

            float angle;
            if (i == 0) angle = swing + tuck + fold;            // cadera
            else if (i == 1) angle = -bend - tuck * 1.4f - fold * 1.6f; // rodilla
            else angle = bend * 0.4f + fold * 0.5f;             // pie

            leg[i].localRotation = RotateAroundCharacterAxis(leg[i], rest[i], angle, true);
        }
    }

    private void AnimateWings()
    {
        // Las alas solo se abren al saltar: un cóndor caminando las lleva
        // plegadas, y hacerlas aletear todo el rato se ve ridículo.
        float spread = wingSpread * airBlend;
        if (spread < 0.01f && airBlend <= 0f) spread = 0f;

        ApplyWing(leftWing, leftWingRest, spread);
        ApplyWing(rightWing, rightWingRest, -spread);
    }

    private void ApplyWing(Transform[] wing, Quaternion[] rest, float spread)
    {
        if (wing == null || wing.Length == 0) return;

        // Un aleteo lento mientras está en el aire.
        float flap = Mathf.Sin(Time.time * 9f) * 12f * airBlend;

        for (int i = 0; i < wing.Length; i++)
        {
            if (wing[i] == null || i >= rest.Length) continue;
            float angle = (spread + flap) * (1f - i * 0.25f);
            wing[i].localRotation = RotateAroundCharacterAxis(wing[i], rest[i], angle, false);
        }
    }

    private void AnimateBody(float speed, float running)
    {
        if (modelRoot == null) return;

        // Rebote: dos por zancada (sube en cada paso, no en cada ciclo).
        float bob = Mathf.Abs(Mathf.Sin(stridePhase)) * bodyBob * moveBlend * (1f - airBlend);
        float drop = crouchDrop * crouchBlend;

        modelRoot.localPosition = modelRestPosition + new Vector3(0f, bob - drop, 0f);

        // Se inclina adelante al correr y se encoge al agacharse.
        float lean = runLean * running * moveBlend * (1f - airBlend) + 18f * crouchBlend;
        float sway = Mathf.Sin(stridePhase) * bodySway * moveBlend * (1f - airBlend);

        modelRoot.localRotation = modelRestRotation * Quaternion.Euler(lean, 0f, sway);

        if (neck != null)
        {
            // La cabeza compensa un poco la inclinación, para que el cóndor no
            // corra mirando al suelo.
            neck.localRotation = RotateAroundCharacterAxis(neck, neckRest, -lean * 0.6f, true);
        }
    }

    // Gira el hueso "angle" grados alrededor del eje derecha (o adelante) del
    // personaje, respetando la pose de reposo. Se convierte el eje al espacio
    // del PADRE del hueso porque localRotation se mide respecto de él.
    private Quaternion RotateAroundCharacterAxis(Transform bone, Quaternion rest, float angle, bool useRightAxis)
    {
        if (Mathf.Abs(angle) < 0.01f) return rest;

        Transform reference = playerController != null ? playerController.transform : transform;
        Vector3 worldAxis = useRightAxis ? reference.right : reference.forward;

        Vector3 localAxis = bone.parent != null
            ? bone.parent.InverseTransformDirection(worldAxis)
            : worldAxis;

        return Quaternion.AngleAxis(angle, localAxis.normalized) * rest;
    }
}
