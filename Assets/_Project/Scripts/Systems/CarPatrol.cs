using System.Collections.Generic;
using UnityEngine;

// Tránsito del pueblo. Cada vehículo recorre SU calle ida y vuelta, siempre
// por el carril de la DERECHA (como se maneja en el Perú): va por un carril,
// da la vuelta en U al final de la calle y regresa por el otro. Nunca más por
// el mismo carril en los dos sentidos, que era lo que hacía que se cruzaran.
//
// Tres reglas de la calle de verdad, que además enseñan sin decirlo:
//   1) Respeta el semáforo: en rojo (o en ámbar si todavía está lejos) se
//      detiene antes de la línea de pare.
//   2) Guarda distancia con el de adelante y con el que le cruza, así no se
//      chocan ni se atraviesan.
//   3) Si Kuntur se le para DELANTE, en su carril, frena y toca el claxon.
public class CarPatrol : MonoBehaviour
{
    // Todos los vehículos, para mirar a los demás sin buscar en la escena
    // cada frame.
    private static readonly List<CarPatrol> All = new List<CarPatrol>();

    [Header("Recorrido (eje de la calle)")]
    [SerializeField] private Vector3 pointA;
    [SerializeField] private Vector3 pointB;
    // Distancia del eje de la calle al centro del carril. En Perú se maneja
    // por la derecha: yendo de A a B, el carril queda a la derecha del eje.
    [SerializeField] private float laneOffset = 2f;
    [SerializeField, Range(0f, 1f)] private float startProgress;
    [SerializeField] private float speed = 9f;
    [SerializeField] private float turnSpeed = 6f;
    [SerializeField] private float uTurnSpeedFactor = 0.4f;

    [Header("Un solo sentido (v53)")]
    // En vez de dar la vuelta en U al final de la calle, el vehículo
    // desaparece y vuelve a salir desde su punto de partida con OTRO modelo
    // (auto, van, camión o bus), así la calle no se ve siempre con los mismos.
    [SerializeField] private bool oneWay;
    [SerializeField] private GameObject[] variants;
    [SerializeField] private float[] variantHalfLengths;
    [SerializeField] private Vector2 respawnDelay = new Vector2(1.5f, 6f);
    [SerializeField] private float spawnClearance = 12f;

    [Header("Vuelta a la manzana (v56)")]
    // Si trae puntos, el vehículo da vueltas a este circuito para siempre
    // (dobla en las esquinas y sigue por la otra calle) en vez de ir y volver
    // por una sola. Los puntos ya vienen en el carril derecho y con la altura
    // del suelo (la cuesta del mirador).
    [SerializeField] private Vector3[] circuit;

    [Header("Distancias")]
    [SerializeField] private float followGap = 3.5f;       // espacio libre con el de adelante

    [Header("Frenar por Kuntur")]
    // Se mide desde la TROMPA del vehículo, no desde su centro: con un bus de
    // 10 m, medir desde el centro lo haría frenar cuando Kuntur ya está
    // debajo del parachoques.
    [SerializeField] private float stopDistance = 7f;
    [SerializeField] private float resumeDistance = 9.5f;
    [SerializeField] private float laneHalfWidth = 1.6f;
    [SerializeField] private float vehicleHalfLength = 2.1f;
    [SerializeField] private float brakeSharpness = 5f;
    [SerializeField] private float honkDelay = 0.35f;

    [Header("Sonido")]
    [SerializeField] private AudioClip engineClip;
    [SerializeField] private AudioClip hornClip;
    [SerializeField] private float honkInterval = 1.25f;
    [SerializeField] private float engineVolume = 0.9f;
    [SerializeField] private float hornVolume = 0.85f;
    [SerializeField] private float pitchScale = 1f;

    private Vector3[] path;
    private bool[] isTurn;   // true en los puntos de la vuelta en U
    private int next;
    private float currentSpeed;

    private bool hidden;
    private float respawnTimer;
    private int currentVariant;

    private bool blockedByPlayer;
    private float blockedTime;
    private float honkTimer;
    private CarPatrol blockingCar; // el vehículo que me hace frenar (para desempatar)

    private Transform player;
    private AudioSource engineSource;
    private AudioSource hornSource;

    public float HalfLength => vehicleHalfLength;

    // Número propio de cada vehículo, para desempatar en los cruces.
    private static int nextId;
    private int id;

    private void OnEnable()
    {
        if (id == 0) id = ++nextId;
        if (!All.Contains(this)) All.Add(this);
    }
    private void OnDisable()
    {
        All.Remove(this);
        // Si Kuntur se sube a este auto, el motor "automático" se calla.
        if (engineSource != null) engineSource.Stop();
        if (hornSource != null) hornSource.Stop();
    }

    private bool IsCircuit => circuit != null && circuit.Length >= 3;

    private void Start()
    {
        BuildPath();
        PlaceAtProgress(startProgress);
        currentSpeed = speed * 0.5f;
        SetupAudio();
    }

    // Ruta en circuito: carril derecho de A a B, vuelta en U, carril derecho
    // de B a A (que es el otro lado de la calle), vuelta en U.
    private void BuildPath()
    {
        if (IsCircuit) { BuildCircuitPath(); return; }
        if (oneWay) { BuildOneWayPath(); return; }
        Vector3 a = new Vector3(pointA.x, 0f, pointA.z);
        Vector3 b = new Vector3(pointB.x, 0f, pointB.z);
        Vector3 dir = (b - a).normalized;
        Vector3 right = new Vector3(dir.z, 0f, -dir.x);
        float r = Mathf.Max(0.5f, laneOffset);
        float y = pointA.y;

        var points = new List<Vector3>();
        var turns = new List<bool>();
        const int arcSteps = 6;

        points.Add(a + right * r); turns.Add(false);
        points.Add(b + right * r); turns.Add(false);
        // Vuelta en U alrededor de B: del carril de ida al de vuelta.
        for (int i = 1; i < arcSteps; i++)
        {
            float t = Mathf.PI * i / arcSteps;
            points.Add(b + right * (r * Mathf.Cos(t)) + dir * (r * Mathf.Sin(t)));
            turns.Add(true);
        }
        points.Add(b - right * r); turns.Add(false);
        points.Add(a - right * r); turns.Add(false);
        for (int i = 1; i < arcSteps; i++)
        {
            float t = Mathf.PI * i / arcSteps;
            points.Add(a - right * (r * Mathf.Cos(t)) - dir * (r * Mathf.Sin(t)));
            turns.Add(true);
        }

        path = points.ToArray();
        isTurn = turns.ToArray();
        for (int i = 0; i < path.Length; i++) path[i].y = y;
    }

    // Circuito cerrado: los puntos tal cual. Las curvas (donde cambia la
    // dirección) se toman más despacio.
    private void BuildCircuitPath()
    {
        oneWay = false;
        path = (Vector3[])circuit.Clone();
        isTurn = new bool[path.Length];
        for (int i = 0; i < path.Length; i++)
        {
            Vector3 a = path[i] - path[(i - 1 + path.Length) % path.Length];
            Vector3 b = path[(i + 1) % path.Length] - path[i];
            a.y = b.y = 0f;
            isTurn[i] = a.sqrMagnitude > 0.001f && b.sqrMagnitude > 0.001f && Vector3.Angle(a, b) > 12f;
        }
    }

    // Un solo tramo: carril derecho de A a B.
    private void BuildOneWayPath()
    {
        Vector3 a = new Vector3(pointA.x, 0f, pointA.z);
        Vector3 b = new Vector3(pointB.x, 0f, pointB.z);
        Vector3 dir = (b - a).normalized;
        Vector3 right = new Vector3(dir.z, 0f, -dir.x);
        float r = Mathf.Max(0.5f, laneOffset);
        path = new[] { a + right * r, b + right * r };
        isTurn = new[] { false, false };
        for (int i = 0; i < path.Length; i++) path[i].y = pointA.y;
    }

    // Llegó al final: desaparece un rato y sale otro desde el inicio.
    private void Vanish()
    {
        hidden = true;
        respawnTimer = Random.Range(respawnDelay.x, respawnDelay.y);
        SetVariantActive(-1);
        if (engineSource != null) engineSource.Stop();
        if (hornSource != null) hornSource.Stop();
        blockedByPlayer = false;
    }

    private void TryRespawn()
    {
        respawnTimer -= Time.deltaTime;
        if (respawnTimer > 0f) return;

        // Que no aparezca encima de otro que acaba de salir, ni encima de Kuntur.
        Vector3 start = path[0];
        foreach (CarPatrol other in All)
        {
            if (other == null || other == this || other.hidden) continue;
            if ((other.transform.position - start).sqrMagnitude < spawnClearance * spawnClearance) { respawnTimer = 0.5f; return; }
        }
        if (player != null && (player.position - start).sqrMagnitude < 6f * 6f) { respawnTimer = 0.5f; return; }

        int count = variants != null ? variants.Length : 0;
        int pick = count > 1 ? Random.Range(0, count - 1) : 0;
        if (count > 1 && pick >= currentVariant) pick++; // siempre uno distinto al anterior
        SetVariantActive(pick);

        transform.position = start;
        Vector3 look = path[1] - path[0];
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look);
        next = 1;
        currentSpeed = speed * 0.6f;
        hidden = false;
        if (engineSource != null)
        {
            engineSource.time = Random.Range(0f, engineSource.clip != null ? engineSource.clip.length : 0f);
            engineSource.Play();
        }
    }

    private void SetVariantActive(int index)
    {
        if (variants == null) return;
        for (int i = 0; i < variants.Length; i++)
            if (variants[i] != null) variants[i].SetActive(i == index);
        if (index >= 0)
        {
            currentVariant = index;
            if (variantHalfLengths != null && index < variantHalfLengths.Length)
                vehicleHalfLength = variantHalfLengths[index];
            // Los buses y camiones van un poco más lento y suenan más grave.
            bool big = vehicleHalfLength > 3.2f;
            pitchScale = big ? 0.72f : 1f;
        }
    }

    // Arranca en otro punto del circuito: si todos salieran de la esquina,
    // aparecerían en fila india.
    private void PlaceAtProgress(float progress)
    {
        if (oneWay)
        {
            // Repartidos a lo largo de su tramo (nunca justo en el final).
            float t = Mathf.Repeat(progress, 1f) * 0.9f;
            transform.position = Vector3.Lerp(path[0], path[1], t);
            next = 1;
            Vector3 dir = path[1] - path[0];
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
            SetVariantActive(Mathf.Clamp(currentVariant, 0, variants != null && variants.Length > 0 ? variants.Length - 1 : 0));
            return;
        }
        float total = 0f;
        for (int i = 0; i < path.Length; i++) total += Vector3.Distance(path[i], path[(i + 1) % path.Length]);

        float wanted = Mathf.Repeat(progress, 1f) * total;
        for (int i = 0; i < path.Length; i++)
        {
            Vector3 from = path[i];
            Vector3 to = path[(i + 1) % path.Length];
            float seg = Vector3.Distance(from, to);
            if (wanted <= seg || i == path.Length - 1)
            {
                transform.position = Vector3.Lerp(from, to, seg > 0.001f ? wanted / seg : 0f);
                next = (i + 1) % path.Length;
                Vector3 look = to - from;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look);
                return;
            }
            wanted -= seg;
        }
    }

    private void SetupAudio()
    {
        if (engineClip != null)
        {
            engineSource = gameObject.AddComponent<AudioSource>();
            engineSource.clip = engineClip;
            engineSource.loop = true;
            engineSource.volume = engineVolume;
            engineSource.spatialBlend = 1f;
            engineSource.rolloffMode = AudioRolloffMode.Linear;
            engineSource.minDistance = 4f;
            engineSource.maxDistance = 38f;
            engineSource.dopplerLevel = 0.35f;
            // Cada motor arranca en otra parte del loop: veinte motores
            // idénticos sonando en fase se oyen como uno solo, raro.
            engineSource.time = Random.Range(0f, engineClip.length);
            engineSource.Play();
        }

        if (hornClip != null)
        {
            hornSource = gameObject.AddComponent<AudioSource>();
            hornSource.clip = hornClip;
            hornSource.loop = false;
            hornSource.playOnAwake = false;
            hornSource.volume = hornVolume;
            hornSource.spatialBlend = 1f;
            hornSource.rolloffMode = AudioRolloffMode.Linear;
            hornSource.minDistance = 6f;
            hornSource.maxDistance = 55f;
        }
    }

    private void Update()
    {
        if (path == null || path.Length < 2) return;
        if (hidden) { TryRespawn(); return; }

        UpdateBlockedByPlayer();

        float cruise = vehicleHalfLength > 3.2f ? speed * 0.8f : speed; // buses y camiones, más calmados
        float limit = isTurn[next] ? cruise * uTurnSpeedFactor : cruise;
        if (blockedByPlayer) limit = 0f;
        limit = Mathf.Min(limit, TrafficLightLimit());
        limit = Mathf.Min(limit, VehicleAheadLimit());

        // Frenar y acelerar con transición, no de golpe.
        float sharp = limit < currentSpeed ? brakeSharpness * 1.4f : brakeSharpness * 0.6f;
        currentSpeed = Mathf.Lerp(currentSpeed, limit, Time.deltaTime * sharp);
        if (limit <= 0.01f && currentSpeed < 0.25f) currentSpeed = 0f;

        Vector3 target = path[next];
        transform.position = Vector3.MoveTowards(transform.position, target, currentSpeed * Time.deltaTime);
        if (currentSpeed > 0.1f) FaceTowards(target);

        if ((transform.position - target).sqrMagnitude < 0.36f)
        {
            if (oneWay && next == path.Length - 1) { Vanish(); return; }
            next = (next + 1) % path.Length;
        }

        UpdateEngine();
        UpdateHorn();
    }

    // ---- Semáforos ----
    private float TrafficLightLimit()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);
        bool northSouth = Mathf.Abs(forward.z) >= Mathf.Abs(forward.x);

        float limit = float.MaxValue;
        foreach (TrafficSignal signal in TrafficSignal.All)
        {
            if (signal == null) continue;

            Vector3 toCenter = signal.transform.position - transform.position;
            toCenter.y = 0f;
            float ahead = Vector3.Dot(toCenter, forward);
            float lateral = Mathf.Abs(Vector3.Dot(toCenter, right));

            float halfAlong = northSouth ? signal.HalfSizeZ : signal.HalfSizeX;
            float halfAcross = northSouth ? signal.HalfSizeX : signal.HalfSizeZ;
            if (lateral > halfAcross + 1f) continue;              // no va por esa calle

            // Distancia de la TROMPA a la línea de pare (antes de la cebra).
            float toLine = ahead - halfAlong - 2.6f - vehicleHalfLength;
            if (toLine < -1.2f || toLine > 22f) continue;         // ya entró al cruce, o está lejos

            TrafficSignal.Phase phase = signal.PhaseFor(northSouth);
            bool mustStop = phase == TrafficSignal.Phase.Red
                            || (phase == TrafficSignal.Phase.Yellow && toLine > 3f);
            if (!mustStop) continue;

            // Frenada progresiva hasta quedar justo en la línea.
            limit = Mathf.Min(limit, Mathf.Max(0f, toLine) * 0.9f);
        }
        return limit;
    }

    // ---- Distancia con los demás vehículos ----
    private float VehicleAheadLimit()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);

        float limit = float.MaxValue;
        CarPatrol blocker = null;

        foreach (CarPatrol other in All)
        {
            if (other == null || other == this || other.hidden) continue;

            Vector3 rel = other.transform.position - transform.position;
            rel.y = 0f;
            float ahead = Vector3.Dot(rel, forward);
            if (ahead <= 0f) continue;

            float lateral = Mathf.Abs(Vector3.Dot(rel, right));
            // Ancho de mi carril más medio ancho del otro: si lo voy a pisar,
            // lo tengo que esperar (sea el de adelante o uno que me cruza).
            if (lateral > 2.3f) continue;

            float gap = ahead - vehicleHalfLength - other.HalfLength;
            float lookAhead = followGap + currentSpeed * 0.9f + 2f;
            if (gap > lookAhead) continue;

            // Si los dos nos estamos esperando (dos que llegan a la vez a un
            // cruce sin semáforo), pasa uno: el de número menor. Si no, se
            // quedarían mirándose para siempre.
            if (other.blockingCar == this && id < other.id) continue;

            float allowed = gap <= followGap ? 0f : (gap - followGap) * 1.2f;
            if (allowed < limit) { limit = allowed; blocker = other; }
        }

        // v56: el tuk tuk estacionado (o un auto que Kuntur dejó parado)
        // también cuenta: se le espera detrás en vez de atravesarlo.
        foreach (DrivableVehicle parked in DrivableVehicle.All)
        {
            if (parked == null || !parked.IsStandingObstacle || parked.gameObject == gameObject) continue;
            Vector3 rel = parked.transform.position - transform.position;
            rel.y = 0f;
            float ahead = Vector3.Dot(rel, forward);
            if (ahead <= 0f) continue;
            if (Mathf.Abs(Vector3.Dot(rel, right)) > parked.HalfWidth + 1.3f) continue;
            float gap = ahead - vehicleHalfLength - parked.HalfLength;
            if (gap > followGap + currentSpeed * 0.9f + 2f) continue;
            float allowed = gap <= followGap ? 0f : (gap - followGap) * 1.2f;
            if (allowed < limit) limit = allowed;
        }

        blockingCar = blocker;
        return limit;
    }

    // ---- Kuntur delante ----
    // Solo frena si Kuntur está DELANTE y DENTRO del carril que el vehículo
    // va a pisar. Con histéresis (frena a 7 m, arranca a 9.5 m) para que no
    // tartamudee arrancando y frenando en el borde.
    private void UpdateBlockedByPlayer()
    {
        if (player == null)
        {
            SimpleThirdPersonController controller = FindAnyObjectByType<SimpleThirdPersonController>();
            if (controller == null) { blockedByPlayer = false; return; }
            player = controller.transform;
        }

        Vector3 local = transform.InverseTransformPoint(player.position);
        float ahead = local.z - vehicleHalfLength;
        float side = Mathf.Abs(local.x);

        if (!blockedByPlayer)
            blockedByPlayer = ahead > -0.3f && ahead < stopDistance && side < laneHalfWidth;
        else
            blockedByPlayer = ahead > -0.8f && ahead < resumeDistance && side < laneHalfWidth + 0.6f;

        blockedTime = blockedByPlayer ? blockedTime + Time.deltaTime : 0f;
    }

    private void UpdateEngine()
    {
        if (engineSource == null) return;

        float throttle = Mathf.Clamp01(currentSpeed / Mathf.Max(speed, 0.01f));
        engineSource.volume = engineVolume * Mathf.Lerp(0.45f, 1f, throttle) * AudioVolumeSettings.Ambience;
        engineSource.pitch = Mathf.Lerp(0.78f, 1.08f, throttle) * pitchScale;
    }

    // El claxon es SOLO para Kuntur: esperar un semáforo o al carro de
    // adelante no se pita.
    private void UpdateHorn()
    {
        if (hornSource == null) return;

        if (!blockedByPlayer)
        {
            honkTimer = 0f;
            if (hornSource.isPlaying) hornSource.Stop();
            return;
        }

        if (blockedTime < honkDelay) return;

        honkTimer -= Time.deltaTime;
        if (honkTimer > 0f) return;

        hornSource.volume = hornVolume * AudioVolumeSettings.Ambience;
        hornSource.Play();
        honkTimer = honkInterval;
    }

    private void FaceTowards(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        // En el circuito del mirador el auto sube y baja la cuesta: se
        // inclina con la calle.
        if (!IsCircuit) direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * turnSpeed);
    }

    public void SetRoute(Vector3 a, Vector3 b)
    {
        pointA = a;
        pointB = b;
        BuildPath();
    }
}
