using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Kuntur sube y baja de los vehículos (tecla F), maneja (W A S D), toca el
// claxon (E) y puede pedir que su tuk tuk venga solo hasta donde está (desde
// la pausa, "PEDIR MI TUK TUK").
//
// Va en el objeto del jugador. Corre DESPUÉS de las animaciones
// (DefaultExecutionOrder): así, sentado, la cadera de Kuntur se acomoda justo
// en el asiento aunque la animación de manejar la mueva.
[DefaultExecutionOrder(200)]
public class VehicleSystem : MonoBehaviour
{
    public static VehicleSystem Instance { get; private set; }

    // Textos de ayuda que el HUD muestra en los carteles de E y F.
    public static string PrimaryHint { get; private set; }
    public static string SecondaryHint { get; private set; }
    public static bool Driving => Instance != null && Instance.Current != null;

    [SerializeField] private float reach = 1.9f;          // qué tan cerca hay que estar para subirse
    [SerializeField] private float hipsAboveSeat = 0.1f;
    [SerializeField] private float requestTeleportAbove = 150f; // si está más lejos, aparece más cerca y llega manejando
    [SerializeField] private float requestDriveDistance = 85f;

    public DrivableVehicle Current { get; private set; }

    // Para la prueba automática del editor (KunturPlayTest): acelerar y
    // doblar sin teclado.
    public static float DebugThrottle;
    public static float DebugSteer;

    public void EnterVehicle(DrivableVehicle v)
    {
        if (v == null || Current != null || entering) return;
        StartCoroutine(Enter(v));
    }

    private SimpleThirdPersonController player;
    private InteractionSystem interaction;
    private Transform hips;
    private bool entering;
    private DrivableVehicle seatedIn;   // desde que se sienta (también durante la subida)

    private void Awake()
    {
        Instance = this;
        player = GetComponent<SimpleThirdPersonController>();
        PrimaryHint = SecondaryHint = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        PrimaryHint = SecondaryHint = null;
    }

    private void Start()
    {
        interaction = FindAnyObjectByType<InteractionSystem>();
        hips = FindDeep(transform, "mixamorig:Hips");
    }

    private bool CanUseVehicles()
    {
        if (player == null) return false;
        if (PauseMenu.IsOpen || InventoryUI.IsOpen || MapSystem.FullMapOpen || TutorialUI.Showing) return false;
        if (SimpleThirdPersonController.Frozen) return false;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Exploracion
            && GameManager.Instance.CurrentState != GameState.Climax) return false;
        return true;
    }

    private void Update()
    {
        PrimaryHint = SecondaryHint = null;
        if (player == null) return;

        if (Current != null) { DrivingUpdate(); return; }
        if (entering) return;
        if (!CanUseVehicles() || player.IsSwimming || player.IsActionLocked) return;

        // La F también es la de las muestras de agua: si hay un frasco al
        // lado, la F es para el frasco.
        if (interaction != null && interaction.HasTarget && interaction.CurrentTargetKey == InteractKey.Secondary) return;

        DrivableVehicle near = FindNearest();
        if (near == null) return;

        SecondaryHint = near.IsTukTuk ? "Subir a tu tuk tuk" : $"Subir al {near.DisplayName}";
        Keyboard k = Keyboard.current;
        if (k != null && k.fKey.wasPressedThisFrame) StartCoroutine(Enter(near));
    }

    private DrivableVehicle FindNearest()
    {
        Vector3 p = player.transform.position;
        DrivableVehicle best = null;
        float bestD = reach;
        foreach (DrivableVehicle v in DrivableVehicle.All)
        {
            // v56c: Kuntur solo maneja SU tuk tuk.
            if (v == null || v.Occupied || !v.IsTukTuk) continue;
            if (Mathf.Abs(v.transform.position.y - p.y) > 2.5f) continue;
            float d = v.DistanceTo(p);
            // Los autos que van andando solo si Kuntur está pegadito a la
            // puerta: si no, el cartel aparecería cada vez que pasa uno.
            if (!v.IsStandingObstacle && d > 1.1f) continue;
            if (d < bestD) { bestD = d; best = v; }
        }
        return best;
    }

    // ---------------------------------------------------------------
    // Subir
    // ---------------------------------------------------------------
    private IEnumerator Enter(DrivableVehicle v)
    {
        entering = true;
        v.CancelAutopilot();
        v.RefreshShape();   // con el modelo visible ahora, y antes de que Kuntur se siente adentro
        player.SetVehicleMode(true, CameraDistanceFor(v));
        if (interaction != null) interaction.enabled = false;

        KunturMixamoAnimator anim = KunturMixamoAnimator.Instance;
        Vector3 door = v.DoorPoint(player.transform.position);
        player.transform.SetPositionAndRotation(door, v.FlatRotation);

        float wait = 0.1f;
        if (anim != null) wait = anim.PlayEnterCar() * 0.85f;

        // v56c: mientras hace la animación de subir, el cuerpo va desde la
        // puerta (al costado) hasta el asiento. Antes la animación lo movía a
        // su manera y Kuntur terminaba subiéndose por DELANTE del tuk tuk.
        player.transform.SetParent(v.Seat, true);
        player.transform.localRotation = Quaternion.identity;
        enterFrom = v.Seat.InverseTransformPoint(door + Vector3.up * 0.88f);
        enterDuration = Mathf.Max(0.1f, wait);
        enterTime = 0f;
        seatedIn = v;

        while (enterTime < enterDuration)
        {
            // Si en medio de la subida algo cambia (una pantalla, la noche), se cancela.
            if (!StillFine()) { AbortEnter(v); yield break; }
            enterTime += Time.deltaTime;
            yield return null;
        }
        enterTime = enterDuration;

        // Ya sentado: el motor arranca con su sonido.
        if (hips == null) player.transform.localPosition = new Vector3(0f, -0.55f, 0f);
        if (anim != null) anim.PlayDrive();
        v.BeginDriving();
        if (AnimalWander.CarriedCat != null) AnimalWander.CarriedCat.RideIn(v.BackSeat);

        Current = v;
        drivingSince = Time.time;
        entering = false;
    }

    private float drivingSince;

    private bool StillFine()
    {
        if (SimpleThirdPersonController.Frozen) return false;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Exploracion
            && GameManager.Instance.CurrentState != GameState.Climax) return false;
        return true;
    }

    private Vector3 enterFrom;
    private float enterTime = 1f, enterDuration = 1f;

    private void AbortEnter(DrivableVehicle v)
    {
        entering = false;
        seatedIn = null;
        player.transform.SetParent(null, true);
        player.SetVehicleMode(false, 0f);
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.StopVehicle();
        if (interaction != null) interaction.enabled = true;
    }

    private static float CameraDistanceFor(DrivableVehicle v)
    {
        return Mathf.Clamp(v.HalfLength * 2f + 3.2f, 5.8f, 14f);
    }

    // ---------------------------------------------------------------
    // Manejar
    // ---------------------------------------------------------------
    private void DrivingUpdate()
    {
        if (!StillFine()) { Exit(); return; }

        bool menus = PauseMenu.IsOpen || InventoryUI.IsOpen || MapSystem.FullMapOpen;
        Keyboard k = Keyboard.current;
        float throttle = 0f, steer = 0f;
        bool handbrake = false;
        if (k != null && !menus)
        {
            if (k.wKey.isPressed || k.upArrowKey.isPressed) throttle += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) throttle -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) steer += 1f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) steer -= 1f;
            handbrake = k.spaceKey.isPressed;
        }

        throttle = Mathf.Clamp(throttle + DebugThrottle, -1f, 1f);
        steer = Mathf.Clamp(steer + DebugSteer, -1f, 1f);
        Current.Drive(throttle, steer, handbrake, Time.deltaTime);

        // Los carteles de E y F tapan la calle: se ven al subir y cuando el
        // vehículo está parado, no mientras se maneja.
        if (Time.time - drivingSince < 6f || Mathf.Abs(Current.Speed) < 0.6f)
        {
            string name = Current.IsTukTuk ? "tuk tuk" : Current.DisplayName;
            PrimaryHint = "Tocar claxon";
            SecondaryHint = $"Bajar del {name}";
        }

        if (k == null || menus) return;
        if (k.eKey.wasPressedThisFrame) Current.Horn();
        if (k.fKey.wasPressedThisFrame && Mathf.Abs(Current.Speed) < 6f) Exit();
    }

    // ---------------------------------------------------------------
    // Bajar
    // ---------------------------------------------------------------
    public void ForceExit()
    {
        if (entering && seatedIn == null)
        {
            StopAllCoroutines();
            AbortEnter(null);
            return;
        }
        if (Current != null) Exit();
    }

    private void Exit()
    {
        DrivableVehicle v = Current;
        Current = null;
        seatedIn = null;
        if (v == null) return;

        v.EndDriving();
        Vector3 spot = v.ExitPoint();
        player.transform.SetParent(null, true);
        player.transform.SetPositionAndRotation(spot, v.FlatRotation);
        player.SetVehicleMode(false, 0f);
        if (KunturMixamoAnimator.Instance != null) KunturMixamoAnimator.Instance.StopVehicle();
        if (interaction != null) interaction.enabled = true;
        if (AnimalWander.CarriedCat != null) AnimalWander.CarriedCat.BackToArms();
    }

    // Sentado: la cadera va exactamente sobre el asiento. Se corrige el
    // cuerpo entero (no la cadera), así las piernas y el torso siguen la
    // animación de manejar sin despegarse del vehículo.
    private void LateUpdate()
    {
        if (seatedIn == null || hips == null) return;
        Transform seat = seatedIn.Seat;
        Vector3 seatPoint = new Vector3(0f, hipsAboveSeat, 0f);
        if (enterTime < enterDuration)
        {
            // Subiendo: de la puerta al asiento, con una curva suave.
            float x = Mathf.Clamp01(enterTime / enterDuration);
            float e = x * x * (3f - 2f * x);
            Vector3 local = Vector3.Lerp(enterFrom, seatPoint, e);
            local.y += Mathf.Sin(e * Mathf.PI) * 0.25f;   // un saltito al pasar el estribo
            player.transform.position += seat.TransformPoint(local) - hips.position;
            return;
        }
        Vector3 target = seat.TransformPoint(seatPoint);
        Vector3 delta = target - hips.position;
        float k = 1f - Mathf.Exp(-14f * Mathf.Max(Time.deltaTime, 0.0001f));
        player.transform.position += delta * k;
    }

    // ---------------------------------------------------------------
    // "Pedir mi tuk tuk" (menú de pausa)
    // ---------------------------------------------------------------
    public void RequestTukTuk()
    {
        DrivableVehicle tuk = null;
        foreach (DrivableVehicle v in DrivableVehicle.All) if (v != null && v.IsTukTuk) { tuk = v; break; }
        if (tuk == null) { Banner("NO ENCUENTRO TU TUK TUK", "Parece que se perdió por el valle."); return; }
        if (Current == tuk) { Banner("¡YA ESTÁS EN TU TUK TUK!", "W A S D para manejar, E para el claxon."); return; }
        if (Current != null) Exit();

        Vector3 me = player.transform.position;
        if (tuk.DistanceTo(me) < 10f) { Banner("TU TUK TUK ESTÁ AQUÍ", "Acércate y presiona F para subir."); tuk.Horn(); return; }

        List<Vector3> route = BuildRoute(tuk.transform.position, me, out float length);
        if (route == null || route.Count == 0)
        {
            // Sin red de calles: aparece al lado, en la calle más cercana.
            Vector3 spot = me + player.transform.right * 3.5f;
            tuk.PlaceAt(spot, player.transform.eulerAngles.y);
            Banner("¡TU TUK TUK LLEGÓ!", "Presiona F para subir.");
            tuk.Horn();
            return;
        }

        // Muy lejos: se adelanta por el camino y recorre manejando solo el
        // último tramo, para que no haya que esperar un minuto entero.
        if (length > requestTeleportAbove) route = SkipAhead(tuk, route, length - requestDriveDistance);

        tuk.StartAutopilot(route, () =>
        {
            Banner("¡TU TUK TUK LLEGÓ!", "Presiona F para subir.");
            tuk.Horn();
        }, me);
        Banner("¡TU TUK TUK YA VIENE!", "Espéralo en la vereda: llega por la calle en unos segundos.");
    }

    private List<Vector3> BuildRoute(Vector3 from, Vector3 to, out float length)
    {
        length = 0f;
        RoadNetwork net = RoadNetwork.Instance;
        if (net == null || net.NodeCount < 2) return null;
        if (!net.ClosestPointOnRoad(from, out Vector2 start, out int sa, out int sb)) return null;
        if (!net.ClosestPointOnRoad(to, out Vector2 goal, out int ga, out int gb)) return null;

        var result = new List<Vector2>();
        bool sameBlock = (sa == ga && sb == gb) || (sa == gb && sb == ga);
        if (sameBlock)
        {
            result.Add(goal);
        }
        else
        {
            List<int> bestPath = null;
            float bestLen = float.MaxValue;
            foreach (int s in new[] { sa, sb })
            {
                foreach (int g in new[] { ga, gb })
                {
                    List<int> path = net.FindPath(s, g);
                    if (path.Count == 0) continue;
                    float len = Vector2.Distance(start, net.Node(s)) + Vector2.Distance(net.Node(g), goal);
                    for (int i = 1; i < path.Count; i++) len += Vector2.Distance(net.Node(path[i - 1]), net.Node(path[i]));
                    if (len < bestLen) { bestLen = len; bestPath = path; }
                }
            }
            if (bestPath == null) return null;
            foreach (int n in bestPath) result.Add(net.Node(n));
            if ((result[result.Count - 1] - goal).sqrMagnitude > 0.25f) result.Add(goal);
        }

        var route = new List<Vector3>();
        Vector2 prev = new Vector2(from.x, from.z);
        foreach (Vector2 p in result)
        {
            length += Vector2.Distance(prev, p);
            prev = p;
            route.Add(new Vector3(p.x, 0f, p.y));
        }
        return route;
    }

    // Deja el tuk tuk a "distance" metros de su posición, siguiendo el camino,
    // y devuelve lo que falta recorrer.
    private List<Vector3> SkipAhead(DrivableVehicle tuk, List<Vector3> route, float distance)
    {
        Vector3 prev = tuk.transform.position;
        prev.y = 0f;
        float walked = 0f;
        for (int i = 0; i < route.Count; i++)
        {
            Vector3 p = route[i];
            float seg = Vector3.Distance(prev, p);
            if (walked + seg >= distance && seg > 0.01f)
            {
                float t = (distance - walked) / seg;
                Vector3 at = Vector3.Lerp(prev, p, t);
                Vector3 dir = (p - prev).normalized;
                Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                tuk.PlaceAt(at + right * 1.5f, Quaternion.LookRotation(dir).eulerAngles.y);
                return route.GetRange(i, route.Count - i);
            }
            walked += seg;
            prev = p;
        }
        return route;
    }

    private static void Banner(string title, string body)
    {
        if (MissionDirector.Instance != null) MissionDirector.Instance.ShowBanner(title, body, -1, 4.5f);
        else Debug.Log($"[Kuntur] {title} - {body}");
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
