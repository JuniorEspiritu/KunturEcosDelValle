using System.Collections.Generic;
using UnityEngine;

// Cualquier vehículo al que Kuntur se puede subir: su tuk tuk, los autos del
// tránsito, las combis, el bus y los autos estacionados.
//
// Se maneja sin física de ruedas (WheelCollider): para un juego de curso eso
// es mucho ajuste fino y se vuelca con nada. Acá el vehículo avanza por
// código, un rayo por eje lo pega al suelo (sube la cuesta del mirador y el
// puente) y una "caja" lanzada hacia adelante lo frena contra paredes, postes,
// gente y otros autos. Es predecible y no se traba.
public class DrivableVehicle : MonoBehaviour
{
    public static readonly List<DrivableVehicle> All = new List<DrivableVehicle>();

    [Header("Identidad")]
    [SerializeField] private string displayName = "auto";
    [SerializeField] private bool isTukTuk;

    [Header("Asientos")]
    [SerializeField] private Transform seat;       // donde va la cadera del conductor
    [SerializeField] private Transform backSeat;   // el gatito, si viaja con Kuntur

    [Header("Ruedas (opcional: solo giran)")]
    [SerializeField] private Transform[] wheels;
    [SerializeField] private float wheelRadius = 0.29f;

    [Header("Manejo")]
    [SerializeField] private float maxSpeed = 15f;
    [SerializeField] private float reverseSpeed = 5f;
    [SerializeField] private float acceleration = 6.5f;
    [SerializeField] private float brakeForce = 16f;
    [SerializeField] private float coastDrag = 2.5f;
    [SerializeField] private float turnRate = 75f;       // grados por segundo a velocidad normal
    [SerializeField] private float groundClearance = 0.35f;

    [Header("Sonido")]
    [SerializeField] private AudioClip engineClip;
    [SerializeField] private AudioClip startClip;
    [SerializeField] private AudioClip hornClip;
    [SerializeField] private AudioClip crashClip;       // v56c: al chocar contra algo
    [SerializeField] private float crashVolume = 1f;
    [SerializeField] private float engineVolume = 0.75f;
    [SerializeField] private float startVolume = 0.95f;
    [SerializeField] private float hornVolume = 0.95f;

    public string DisplayName => displayName;
    public bool IsTukTuk => isTukTuk;
    public Transform Seat => seat;
    public Transform BackSeat => backSeat;
    public bool Occupied { get; private set; }
    public bool Autopilot { get; private set; }
    public float Speed { get; private set; }
    public float HalfLength => halfSize.z;
    public float HalfWidth => halfSize.x;
    public float Height => size.y;

    // Un vehículo quieto que no es del tránsito automático (el tuk tuk
    // estacionado, un auto que Kuntur dejó parado): los autos del tránsito lo
    // tienen que esquivar/esperar como a cualquier otro.
    public bool IsStandingObstacle => isActiveAndEnabled && (patrol == null || !patrol.enabled);

    private Vector3 center;       // centro de la caja, en local
    private Vector3 size;
    private Vector3 halfSize;
    private float wheelBase;
    private float yaw;
    private float pitch;
    private float verticalSpeed;
    private Collider[] ownColliders;
    private CarPatrol patrol;
    private AudioSource engineSource;
    private AudioSource fxSource;
    private float engineStartAt = -1f;
    private float engineFade;
    private Vector3 lastSafePosition;
    private float lastSafeYaw;
    private int[] wheelAxis;
    private float nextCrashSound;

    private static readonly RaycastHit[] Hits = new RaycastHit[24];

    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    private void Awake()
    {
        patrol = GetComponent<CarPatrol>();
        RefreshShape();
        yaw = transform.eulerAngles.y;
        lastSafePosition = transform.position;
        lastSafeYaw = yaw;
        SetupWheels();
    }

    // Tamaño real del vehículo (con el modelo que esté visible ahora: los del
    // tránsito cambian de modelo cada vez que reaparecen).
    public void RefreshShape()
    {
        var own = new List<Collider>();
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
            if (c.GetComponentInParent<SimpleThirdPersonController>() == null && c.GetComponentInParent<AnimalWander>() == null) own.Add(c);
        ownColliders = own.ToArray();

        bool any = false;
        Vector3 min = Vector3.zero, max = Vector3.zero;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            // Lo que va sentado adentro (Kuntur, el gatito) no es parte del
            // vehículo: si se contara, la "caja" del tuk tuk crecía hasta
            // abarcar la cámara y los íconos del mapa de Kuntur.
            if (r.GetComponentInParent<SimpleThirdPersonController>() != null) continue;
            if (r.GetComponentInParent<AnimalWander>() != null) continue;
            Bounds lb = r.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!any) { min = max = local; any = true; }
                else { min = Vector3.Min(min, local); max = Vector3.Max(max, local); }
            }
        }
        if (!any) { min = new Vector3(-0.8f, 0f, -1.4f); max = new Vector3(0.8f, 1.6f, 1.4f); }

        size = max - min;
        center = (min + max) * 0.5f;
        halfSize = size * 0.5f;
        wheelBase = Mathf.Max(1.2f, size.z * 0.62f);

        if (seat == null)
        {
            // Asiento del conductor calculado por el tamaño: en el Perú se
            // maneja por la derecha, así que el timón va a la IZQUIERDA.
            bool tall = size.y > 2.4f;
            GameObject s = new GameObject("Asiento_Conductor");
            s.transform.SetParent(transform, false);
            s.transform.localPosition = tall
                ? new Vector3(center.x - halfSize.x * 0.45f, min.y + 1.15f, max.z - 1.5f)
                : new Vector3(center.x - halfSize.x * 0.38f, min.y + 0.52f, center.z + size.z * 0.02f);
            seat = s.transform;
        }
        if (backSeat == null)
        {
            GameObject s = new GameObject("Asiento_Atras");
            s.transform.SetParent(transform, false);
            s.transform.localPosition = seat.localPosition + new Vector3(halfSize.x * 0.7f, 0.05f, -0.3f);
            backSeat = s.transform;
        }
    }

    private void SetupWheels()
    {
        if (wheels == null) return;
        wheelAxis = new int[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            wheelAxis[i] = -1;
            if (wheels[i] == null) continue;
            MeshFilter mf = wheels[i].GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            // Si el pivote no está en el centro del eje, girarla la haría
            // columpiarse alrededor de otro punto: esa rueda se queda quieta.
            float radius = Mathf.Max(b.extents.y, b.extents.z);
            if (Mathf.Abs(b.center.y) > radius * 0.2f || Mathf.Abs(b.center.z) > radius * 0.2f) continue;
            wheelAxis[i] = 0;
        }
    }

    // ---------------------------------------------------------------
    // Subir / bajar
    // ---------------------------------------------------------------
    public void BeginDriving()
    {
        CancelAutopilot();
        if (patrol != null && patrol.enabled) patrol.enabled = false;
        // El motor de "tránsito automático" que pone CarPatrol se calla: desde
        // ahora suena el motor de este script, que sigue al acelerador.
        foreach (AudioSource s in GetComponents<AudioSource>())
            if (s != engineSource && s != fxSource) s.Stop();
        EnsureAudio();

        Occupied = true;
        yaw = transform.eulerAngles.y;
        Speed = 0f;
        verticalSpeed = 0f;

        float delay = 0.2f;
        if (startClip != null)
        {
            fxSource.PlayOneShot(startClip, startVolume * AudioVolumeSettings.Sfx);
            delay = Mathf.Clamp(startClip.length * 0.8f, 0.4f, 2.2f);
        }
        engineStartAt = Time.time + delay;
    }

    public void EndDriving()
    {
        Occupied = false;
        Speed = 0f;
        engineStartAt = -1f;
        // El motor se apaga al bajarse.
        if (engineSource != null) engineSource.Stop();
    }

    public void Horn()
    {
        EnsureAudio();
        if (hornClip != null) fxSource.PlayOneShot(hornClip, hornVolume * AudioVolumeSettings.Sfx);
    }

    private void EnsureAudio()
    {
        if (engineSource == null)
        {
            engineSource = gameObject.AddComponent<AudioSource>();
            engineSource.loop = true;
            engineSource.playOnAwake = false;
            engineSource.spatialBlend = 0.75f;
            engineSource.rolloffMode = AudioRolloffMode.Linear;
            engineSource.minDistance = 4f;
            engineSource.maxDistance = 45f;
            engineSource.dopplerLevel = 0f;
            engineSource.clip = engineClip;
        }
        if (fxSource == null)
        {
            fxSource = gameObject.AddComponent<AudioSource>();
            fxSource.playOnAwake = false;
            fxSource.spatialBlend = 0.6f;
            fxSource.rolloffMode = AudioRolloffMode.Linear;
            fxSource.minDistance = 5f;
            fxSource.maxDistance = 60f;
            fxSource.dopplerLevel = 0f;
        }
    }

    // Punto libre al lado del vehículo para bajarse (primero la puerta del
    // conductor; si hay una pared, el otro lado; si no, atrás o adelante).
    public Vector3 ExitPoint()
    {
        Vector3[] options =
        {
            new Vector3(-(halfSize.x + 0.75f), 0f, seat != null ? seat.localPosition.z : 0f),
            new Vector3(halfSize.x + 0.75f, 0f, seat != null ? seat.localPosition.z : 0f),
            new Vector3(0f, 0f, -(halfSize.z + 0.9f)),
            new Vector3(0f, 0f, halfSize.z + 0.9f),
        };
        foreach (Vector3 o in options)
        {
            Vector3 p = transform.position + Quaternion.Euler(0f, yaw, 0f) * (o + new Vector3(center.x, 0f, center.z));
            p.y = GroundHeight(p, transform.position.y + 2f, out bool found);
            if (!found) continue;
            if (IsSpotFree(p)) return p;
        }
        Vector3 up = transform.position + Vector3.up * (size.y + 0.3f);
        return up;
    }

    // La puerta del conductor (del lado donde está Kuntur), para la animación de subir.
    public Vector3 DoorPoint(Vector3 near)
    {
        float side = (Quaternion.Inverse(FlatRotation) * (near - transform.position)).x >= center.x ? 1f : -1f;
        Vector3 local = new Vector3(side * (halfSize.x + 0.45f) + center.x, 0f, seat != null ? seat.localPosition.z : center.z);
        Vector3 p = transform.position + Quaternion.Euler(0f, yaw, 0f) * local;
        p.y = GroundHeight(p, transform.position.y + 2f, out _);
        return p;
    }

    public Quaternion FlatRotation => Quaternion.Euler(0f, yaw, 0f);

    // Distancia (en planta) de un punto al contorno del vehículo.
    public float DistanceTo(Vector3 p)
    {
        Vector3 l = Quaternion.Inverse(FlatRotation) * (p - transform.position) - new Vector3(center.x, 0f, center.z);
        float dx = Mathf.Max(0f, Mathf.Abs(l.x) - halfSize.x);
        float dz = Mathf.Max(0f, Mathf.Abs(l.z) - halfSize.z);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private bool IsSpotFree(Vector3 feet)
    {
        Vector3 a = feet + Vector3.up * 0.45f;
        Vector3 b = feet + Vector3.up * 1.4f;
        Collider[] overlaps = Physics.OverlapCapsule(a, b, 0.33f, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider c in overlaps)
        {
            if (IsOwn(c)) continue;
            if (c.GetComponentInParent<SimpleThirdPersonController>() != null) continue;
            return false;
        }
        return true;
    }

    // ---------------------------------------------------------------
    // Manejo (lo llama VehicleSystem con el teclado, o el piloto automático)
    // ---------------------------------------------------------------
    public void Drive(float throttle, float steer, bool handbrake, float dt, bool ignoreObstacles = false)
    {
        if (dt <= 0f) return;

        if (throttle > 0.05f)
        {
            Speed = Speed < -0.3f
                ? Mathf.MoveTowards(Speed, 0f, brakeForce * dt)
                : Mathf.MoveTowards(Speed, maxSpeed * throttle, acceleration * dt);
        }
        else if (throttle < -0.05f)
        {
            Speed = Speed > 0.3f
                ? Mathf.MoveTowards(Speed, 0f, brakeForce * dt)
                : Mathf.MoveTowards(Speed, -reverseSpeed * -throttle, acceleration * 0.7f * dt);
        }
        else
        {
            Speed = Mathf.MoveTowards(Speed, 0f, coastDrag * dt);
        }
        if (handbrake) Speed = Mathf.MoveTowards(Speed, 0f, brakeForce * 1.3f * dt);

        // Girar: solo si se está moviendo (y al revés en retroceso), y un poco
        // menos a toda velocidad para que no dé vueltas como trompo.
        float move = Mathf.Abs(Speed);
        float steerFactor = Mathf.Clamp01(move / 2f) * (1f - 0.2f * Mathf.Clamp01(move / Mathf.Max(maxSpeed, 0.1f)));
        yaw += steer * turnRate * steerFactor * Mathf.Sign(Speed) * dt;

        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 delta = forward * Speed * dt;

        if (delta.sqrMagnitude > 0.000001f && !ignoreObstacles && Blocked(delta, out float allowed))
        {
            delta = delta.normalized * Mathf.Max(0f, allowed - 0.05f);
            // Choque: suena (según qué tan rápido iba) y rebota un poquito
            // en vez de atravesar la pared.
            if (Mathf.Abs(Speed) > 2f && Time.time >= nextCrashSound && crashClip != null)
            {
                EnsureAudio();
                float v = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(Mathf.Abs(Speed) / 12f));
                fxSource.PlayOneShot(crashClip, crashVolume * v * AudioVolumeSettings.Sfx);
                nextCrashSound = Time.time + 0.8f;
            }
            Speed = -Speed * 0.12f;
        }

        Vector3 next = transform.position + delta;

        // Al río no se entra manejando (el tuk tuk no flota).
        if (IntoDeepWater(next)) { next = transform.position; Speed = 0f; }

        PlaceOnGround(next, dt);
        SpinWheels(dt);
    }

    private bool IntoDeepWater(Vector3 p)
    {
        RiverWater water = RiverWater.Active;
        if (water == null || !water.Contains(p, 0f)) return false;
        float ground = GroundHeight(p, transform.position.y + 2.5f, out bool found);
        return found && ground < water.SurfaceY - 0.25f;
    }

    private bool Blocked(Vector3 delta, out float allowed)
    {
        allowed = delta.magnitude;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        float boxHeight = Mathf.Max(0.3f, size.y - groundClearance);
        Vector3 origin = transform.position + rot * new Vector3(center.x, groundClearance + boxHeight * 0.5f, center.z);
        Vector3 half = new Vector3(halfSize.x * 0.92f, boxHeight * 0.45f, halfSize.z * 0.97f);
        Vector3 dir = delta.normalized;

        int count = Physics.BoxCastNonAlloc(origin, half, dir, Hits, rot, allowed + 0.1f, ~0, QueryTriggerInteraction.Ignore);
        bool blocked = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = Hits[i];
            if (h.distance <= 0f) continue;              // ya estaba tocándolo: que pueda salir
            if (IsOwn(h.collider)) continue;
            if (h.normal.y > 0.55f) continue;            // es suelo o rampa, no pared
            if (Occupied && h.collider.GetComponentInParent<SimpleThirdPersonController>() != null) continue;
            if (h.distance < allowed + 0.1f)
            {
                allowed = Mathf.Min(allowed, h.distance);
                blocked = true;
            }
        }
        return blocked;
    }

    private bool IsOwn(Collider c)
    {
        if (c == null) return true;
        if (c.transform.IsChildOf(transform) && c.GetComponentInParent<AnimalWander>() == null) return true;
        if (ownColliders != null) foreach (Collider o in ownColliders) if (o == c) return true;
        return false;
    }

    // Pegado al suelo: un rayo en el eje de adelante y otro en el de atrás.
    // Con los dos sale la inclinación (sube la cuesta mirando hacia arriba).
    private void PlaceOnGround(Vector3 next, float dt)
    {
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 front = next + forward * (wheelBase * 0.5f + center.z);
        Vector3 back = next - forward * (wheelBase * 0.5f - center.z);
        float from = transform.position.y + 2.2f;
        float fy = GroundHeight(front, from, out bool fFound);
        float by = GroundHeight(back, from, out bool bFound);

        float y = next.y;
        if (fFound || bFound)
        {
            if (!fFound) fy = by;
            if (!bFound) by = fy;
            float ground = (fy + by) * 0.5f;
            if (ground >= transform.position.y - 0.05f)
            {
                // Subir (vereda, rampa): rápido pero sin salto.
                y = Mathf.Lerp(transform.position.y, ground, 1f - Mathf.Exp(-22f * dt));
                verticalSpeed = 0f;
            }
            else
            {
                // Bajar: con gravedad, así un desnivel no lo "teletransporta".
                verticalSpeed -= 20f * dt;
                y = Mathf.Max(ground, transform.position.y + verticalSpeed * dt);
                if (y <= ground + 0.001f) verticalSpeed = 0f;
            }
            float targetPitch = -Mathf.Atan2(fy - by, wheelBase) * Mathf.Rad2Deg;
            pitch = Mathf.LerpAngle(pitch, Mathf.Clamp(targetPitch, -30f, 30f), 1f - Mathf.Exp(-10f * dt));
        }
        else
        {
            verticalSpeed -= 20f * dt;
            y = transform.position.y + verticalSpeed * dt;
        }

        next.y = y;
        if (y < -12f)
        {
            // Se cayó del mundo: vuelve al último lugar seguro.
            next = lastSafePosition;
            yaw = lastSafeYaw;
            Speed = 0f;
            verticalSpeed = 0f;
        }
        else if (fFound && bFound && Mathf.Abs(Speed) < 20f)
        {
            lastSafePosition = next;
            lastSafeYaw = yaw;
        }

        transform.SetPositionAndRotation(next, Quaternion.Euler(pitch, yaw, 0f));
    }

    private float GroundHeight(Vector3 p, float fromY, out bool found, float maxDistance = 8f)
    {
        found = false;
        float best = float.MinValue;
        int count = Physics.RaycastNonAlloc(new Vector3(p.x, fromY, p.z), Vector3.down, Hits, maxDistance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = Hits[i];
            if (IsOwn(h.collider)) continue;
            if (h.collider.GetComponentInParent<SimpleThirdPersonController>() != null) continue;
            if (h.collider.GetComponentInParent<DrivableVehicle>() != null) continue;
            if (h.normal.y < 0.5f) continue;   // un costado, no un piso
            if (h.point.y > best) { best = h.point.y; found = true; }
        }
        return found ? best : p.y;
    }

    private void SpinWheels(float dt)
    {
        if (wheels == null || wheelAxis == null) return;
        float degrees = Speed / Mathf.Max(0.05f, wheelRadius) * Mathf.Rad2Deg * dt;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null || wheelAxis[i] < 0) continue;
            // Siempre alrededor del eje del vehículo (de costado a costado),
            // pasado al espacio de la rueda: así rueda y no se "columpia".
            Vector3 axis = wheels[i].InverseTransformDirection(transform.right).normalized;
            wheels[i].Rotate(axis, degrees, Space.Self);
        }
    }

    // Ponerlo en un punto (lo usa "pedir mi tuk tuk" para acercarlo).
    public void PlaceAt(Vector3 position, float newYaw)
    {
        yaw = newYaw;
        pitch = 0f;
        Speed = 0f;
        verticalSpeed = 0f;
        position.y = GroundHeight(position, 60f, out bool found, 90f);
        if (!found) position.y = transform.position.y;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        lastSafePosition = position;
        lastSafeYaw = yaw;
    }

    // ---------------------------------------------------------------
    // Piloto automático: el tuk tuk viene solo por las calles
    // ---------------------------------------------------------------
    private List<Vector3> route;
    private int routeIndex;
    private System.Action onArrived;
    private float stuckTimer;
    private float ghostUntil;
    private int ghosts;
    private float autopilotStarted;
    private const float LaneOffset = 1.5f;

    private Vector3? pickupPoint;

    public void StartAutopilot(List<Vector3> waypoints, System.Action arrived, Vector3? waitNear = null)
    {
        pickupPoint = waitNear;
        if (waypoints == null || waypoints.Count == 0) return;
        if (patrol != null && patrol.enabled) patrol.enabled = false;
        EnsureAudio();
        route = waypoints;
        routeIndex = 0;
        onArrived = arrived;
        stuckTimer = 0f;
        ghosts = 0;
        ghostUntil = 0f;
        autopilotStarted = Time.time;
        Autopilot = true;
        yaw = transform.eulerAngles.y;
        if (engineClip != null && engineSource != null && !engineSource.isPlaying) engineSource.Play();
    }

    public void CancelAutopilot()
    {
        if (!Autopilot) return;
        Autopilot = false;
        route = null;
        Speed = 0f;
        if (!Occupied && engineSource != null) engineSource.Stop();
    }

    // Punto al que apunta: el waypoint corrido al carril derecho de la cuadra.
    private Vector3 LaneTarget(int i)
    {
        Vector3 p = route[i];
        Vector3 prev = i > 0 ? route[i - 1] : transform.position;
        Vector3 d = p - prev;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return p;
        d.Normalize();
        Vector3 right = new Vector3(d.z, 0f, -d.x);
        // El último punto (donde espera a Kuntur) va pegado a la vereda del
        // lado donde está Kuntur, como un mototaxi que se orilla a recogerte.
        if (i == route.Count - 1)
        {
            float side = pickupPoint.HasValue && Vector3.Dot(pickupPoint.Value - p, right) < 0f ? -1f : 1f;
            return p + right * (side * (LaneOffset + 0.7f));
        }
        return p + right * LaneOffset;
    }

    private void UpdateAutopilot(float dt)
    {
        if (route == null || routeIndex >= route.Count) { FinishAutopilot(); return; }

        Vector3 target = LaneTarget(routeIndex);
        Vector3 to = target - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        bool last = routeIndex == route.Count - 1;

        if (!last && dist < 3.8f) { routeIndex++; return; }
        if (last && dist < 2.2f) { FinishAutopilot(); return; }

        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        float angle = Vector3.SignedAngle(forward, to, Vector3.up);
        float steer = Mathf.Clamp(angle / 32f, -1f, 1f);

        float throttle = 1f;
        float limit = 11f;
        if (Mathf.Abs(angle) > 25f) limit = 4.5f;               // doblando la esquina
        if (last) limit = Mathf.Min(limit, Mathf.Max(2.2f, dist * 0.7f));
        else if (routeIndex + 1 < route.Count)
        {
            // Frena antes de la esquina.
            Vector3 a = route[routeIndex] - (routeIndex > 0 ? route[routeIndex - 1] : transform.position);
            Vector3 b = route[routeIndex + 1] - route[routeIndex];
            a.y = b.y = 0f;
            if (Vector3.Angle(a, b) > 30f && dist < 14f) limit = Mathf.Min(limit, 4.5f + dist * 0.4f);
        }
        if (Mathf.Abs(angle) > 100f && dist < 8f) throttle = -0.6f; // se pasó: retrocede un poco
        else if (Speed > limit) throttle = 0f;

        bool ghost = Time.time < ghostUntil;
        Drive(throttle, steer, Speed > limit + 2f, dt, ghost);

        // ¿Trabado (un auto parado, una pared)? Primero espera, después pasa
        // "de fantasma" un momento, y si igual no avanza, salta al siguiente
        // punto de la ruta. Nunca se queda atascado para siempre.
        stuckTimer = Mathf.Abs(Speed) < 0.6f && throttle > 0f ? stuckTimer + dt : 0f;
        if (stuckTimer > 2.5f)
        {
            stuckTimer = 0f;
            ghosts++;
            if (ghosts > 3)
            {
                Vector3 jump = LaneTarget(Mathf.Min(routeIndex, route.Count - 1));
                PlaceAt(jump, yaw);
                routeIndex = Mathf.Min(routeIndex + 1, route.Count - 1);
                ghosts = 0;
            }
            else ghostUntil = Time.time + 2f;
        }

        // Tope de seguridad: si tarda demasiado, aparece cerca.
        if (Time.time - autopilotStarted > 70f)
        {
            Vector3 end = LaneTarget(route.Count - 1);
            PlaceAt(end, yaw);
            FinishAutopilot();
        }
    }

    private void FinishAutopilot()
    {
        Autopilot = false;
        Speed = 0f;
        route = null;
        if (!Occupied && engineSource != null) engineSource.Stop();
        System.Action cb = onArrived;
        onArrived = null;
        cb?.Invoke();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (Autopilot && !Occupied) UpdateAutopilot(dt);
        UpdateEngine(dt);
    }

    private void UpdateEngine(float dt)
    {
        if (engineSource == null || engineClip == null) return;
        bool running = Occupied || Autopilot;
        if (Occupied && engineStartAt > 0f && Time.time >= engineStartAt)
        {
            engineStartAt = -1f;
            engineSource.volume = 0f;
            engineFade = 0f;
            engineSource.Play();
        }
        if (!running || !engineSource.isPlaying) return;

        engineFade = Mathf.MoveTowards(engineFade, 1f, dt * 2.5f);
        float throttle = Mathf.Clamp01(Mathf.Abs(Speed) / Mathf.Max(maxSpeed, 0.1f));
        engineSource.pitch = Mathf.Lerp(0.85f, 1.45f, throttle);
        engineSource.volume = engineVolume * Mathf.Lerp(0.55f, 1f, throttle) * engineFade * AudioVolumeSettings.Sfx;
    }
}
