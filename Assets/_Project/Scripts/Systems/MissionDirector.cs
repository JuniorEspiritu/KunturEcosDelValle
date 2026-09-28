using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Las misiones del barrio, una detrás de otra, cada vez más difíciles:
//   1) Un vecino (elegido al azar) te está buscando: la ruta morada del mapa
//      te lleva hasta él.
//   2) Te cuenta, a su manera, dónde se juntó la basura y aceptas ayudar.
//   3) Tienes un TIEMPO LÍMITE para limpiar. Si lo logras, ganas de 1 a 3
//      estrellas según cuánto tiempo te sobró. Si se acaba el tiempo, pierdes
//      (y reintentas el día desde lo último guardado).
// Cada misión trae más residuos, más lugares y menos segundos por residuo.
// Los lugares se van turnando: nunca te mandan dos veces seguidas al mismo.
//
// Es el ODS 11 hecho mecánica: el barrio organizado arregla el problema, pero
// la gente vuelve a ensuciar y hay que actuar a tiempo, siempre.
public class MissionDirector : MonoBehaviour
{
    public static MissionDirector Instance { get; private set; }

    [System.Serializable]
    public class Zone
    {
        public string id;           // también es el id del objetivo que reportan sus residuos
        public string displayName;  // "afuera del Plaza Vea"
        public GameObject root;     // todos los residuos posibles de la zona
        public Transform center;
        public bool hasWaterSamples;
    }

    [Header("Zonas y vecinos")]
    [SerializeField] private Zone[] zones;
    [SerializeField] private DialogueNPC[] giverCandidates;
    [SerializeField] private string[] giverNames;
    [SerializeField] private bool[] giverFemale;
    [SerializeField] private string[] giverRoles;
    [SerializeField] private string[] giverZones;   // zona propia del vecino ("" = al azar)
    [SerializeField] private DialogueNPC rosa;

    [Header("Dificultad (cada misión un poco más difícil)")]
    // Ahora se recogen BOLSAS (una por agachada, ~2.7 s de animación), así
    // que son menos que las basuritas de antes pero cada una cuesta más.
    // Cada misión nueva: +1 bolsa, más lugares (1, luego 2, luego 3), menos
    // segundos por bolsa y un tiempo total cada vez más justo.
    [SerializeField] private int baseTrash = 4;                // bolsas de la primera misión
    [SerializeField] private int trashPerMission = 1;          // cada misión, una bolsa más
    [SerializeField] private float baseSecondsPerTrash = 11f;  // agacharse + ir de una bolsa a otra
    [SerializeField] private float minSecondsPerTrash = 6f;
    [SerializeField] private float secondsLessPerMission = 0.5f;
    [SerializeField] private float tightenPerMission = 0.05f;  // 5% menos de tiempo total por misión
    [SerializeField] private float minTighten = 0.6f;
    [SerializeField] private float travelSecondsPerZone = 60f;
    [SerializeField] private float samplesExtraSeconds = 70f;

    [Header("Interfaz")]
    [SerializeField] private Transform giverMarker;      // el "!" que flota sobre quien da la misión
    [SerializeField] private GameObject bannerPanel;
    [SerializeField] private TMP_Text bannerTitle;
    [SerializeField] private TMP_Text bannerBody;
    [SerializeField] private Image[] bannerStars;
    [SerializeField] private Sprite starFilled;
    [SerializeField] private Sprite starEmpty;
    [SerializeField] private TMP_Text missionsTitle;     // "MISIÓN 3 · CUIDADO AMBIENTAL"
    [SerializeField] private ClimaxDirector climax;
    [SerializeField] private CountdownManager countdown;
    [SerializeField] private VictoryDanceUI victoryDance;   // baile de misión cumplida

    public event System.Action<int> OnStarsChanged;
    public int TotalStars { get; private set; }
    public int MissionsDone { get; private set; }
    public Vector3? CurrentTarget { get; private set; }
    public bool IsCleaning => phase == Phase.Cleaning;

    private enum Phase { Waiting, TalkToGiver, Cleaning, Done }
    private Phase phase = Phase.Waiting;
    private DialogueNPC currentGiver;
    private int currentGiverIndex = -1;
    private int lastGiverIndex = -1;
    private readonly List<Zone> activeZones = new List<Zone>();
    private readonly Dictionary<string, int> zoneTrashCount = new Dictionary<string, int>();
    private readonly List<string> recentZones = new List<string>();
    private readonly List<int> recentGivers = new List<int>();
    private readonly Dictionary<string, CleanupSpot> chosenSpots = new Dictionary<string, CleanupSpot>();
    private readonly Dictionary<string, CleanupSpot> lastSpot = new Dictionary<string, CleanupSpot>();
    private GameObject giverMapIcon;
    private bool needSamples;
    private float timeLimit;
    private bool pendingStart;
    private float bannerTimer;
    private Camera mainCamera;

    private const string TalkObjective = "hablar_mision";
    private const string SamplesObjective = "muestras_agua";
    private const string RosaObjective = "hablar_con_rosa";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        // Toda la basura empieza escondida: aparece la de la zona que te
        // encargan, así "lo acordado" es claro y no se limpia al azar.
        foreach (Zone zone in zones)
            if (zone.root != null) zone.root.SetActive(false);

        foreach (DialogueNPC giver in giverCandidates)
        {
            if (giver == null) continue;
            SetGiverActive(giver, false);
            giver.OnConvinced += HandleGiverConvinced;
        }

        if (bannerPanel != null) bannerPanel.SetActive(false);
        if (countdown == null) countdown = FindAnyObjectByType<CountdownManager>();
    }

    private void Start()
    {
        if (ObjectiveSystem.Instance != null)
        {
            ObjectiveSystem.Instance.OnObjectiveCompleted += HandleObjectiveCompleted;
            ObjectiveSystem.Instance.OnAllMainObjectivesCompleted += HandleAllMainDone;
        }
        if (countdown != null) countdown.OnCountdownFinished += HandleTimeUp;
        if (rosa != null) rosa.OnConvinced += _ => { SaveSystem.Current.rosaConvinced = true; SaveSystem.SaveCurrent(); };

        // Lo que ya se había logrado en días anteriores.
        SaveData save = SaveSystem.Current;
        TotalStars = save.stars;
        MissionsDone = save.missionsDone;
        if (!string.IsNullOrEmpty(save.lastZones)) recentZones.AddRange(save.lastZones.Split(','));
        if (save.rosaConvinced && rosa != null) rosa.MarkConvinced();

        OnStarsChanged?.Invoke(TotalStars);
        UpdateMissionsTitle();
        // Primero se lee el saludo del día (DayManager); después, la misión.
        StartCoroutine(StartLevelAfter(6.5f));
    }

    private void OnDestroy()
    {
        if (ObjectiveSystem.Instance != null)
        {
            ObjectiveSystem.Instance.OnObjectiveCompleted -= HandleObjectiveCompleted;
            ObjectiveSystem.Instance.OnAllMainObjectivesCompleted -= HandleAllMainDone;
        }
        if (countdown != null) countdown.OnCountdownFinished -= HandleTimeUp;
    }

    private IEnumerator StartLevelAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        // Mientras se lee el tutorial no aparece ningún encargo. Y al cerrarlo
        // va primero el saludo del día y DESPUÉS la misión, uno a la vez.
        bool hadTutorial = false;
        while (TutorialUI.Pending || TutorialUI.Showing) { hadTutorial = true; yield return null; }
        if (hadTutorial)
        {
            float wait = 7.5f - (Time.time - TutorialUI.ClosedAt);
            if (wait > 0f) yield return new WaitForSeconds(wait);
            while (TutorialUI.Showing) yield return null;
        }
        StartLevel();
    }

    // ---------------------------------------------------------------
    // Paso 1: ir a hablar con alguien
    // ---------------------------------------------------------------
    private void StartLevel()
    {
        // De noche no se reparten encargos: se retoma al día siguiente.
        if (DayManager.Instance != null && DayManager.Instance.DayOver) { pendingStart = true; return; }
        if (phase == Phase.TalkToGiver || phase == Phase.Cleaning || phase == Phase.Done) return;
        pendingStart = false;

        int level = MissionsDone;
        currentGiverIndex = PickGiver();
        if (currentGiverIndex < 0) return;
        currentGiver = giverCandidates[currentGiverIndex];

        // Más lugares a medida que avanza: 1, luego 2, luego 3.
        int zoneCount = level < 2 ? 1 : level < 5 ? 2 : 3;
        activeZones.Clear();
        // El vecino con zona propia (el chacarero, la heladera del mirador)
        // siempre te manda primero a SU lugar.
        string own = GiverZone(currentGiverIndex);
        Zone ownZone = string.IsNullOrEmpty(own) ? null : System.Array.Find(zones, z => z.id == own);
        if (ownZone != null) activeZones.Add(ownZone);
        var free = new List<Zone>();
        foreach (Zone z in zones) if (!recentZones.Contains(z.id) && z != ownZone) free.Add(z);
        if (free.Count < zoneCount - activeZones.Count) { free.Clear(); foreach (Zone z in zones) if (z != ownZone) free.Add(z); }
        zoneCount -= activeZones.Count;
        for (int i = 0; i < zoneCount && free.Count > 0; i++)
        {
            int pick = Random.Range(0, free.Count);
            activeZones.Add(free[pick]);
            free.RemoveAt(pick);
        }

        // En cada zona, UN punto exacto (el montón de basura): una esquina,
        // el estacionamiento, la orilla... El vecino te dice cuál.
        chosenSpots.Clear();
        foreach (Zone z in activeZones) chosenSpots[z.id] = PickSpot(z);

        // Cuánta basura (cada vez más) y cuánto tiempo (según lo lejos que
        // queda y cada vez más justo).
        int totalTrash = baseTrash + trashPerMission * level;
        int perZone = Mathf.CeilToInt(totalTrash / (float)activeZones.Count);
        zoneTrashCount.Clear();
        int realTotal = 0;
        foreach (Zone z in activeZones)
        {
            Transform poolRoot = chosenSpots[z.id] != null ? chosenSpots[z.id].transform : (z.root != null ? z.root.transform : null);
            int pool = poolRoot != null ? poolRoot.GetComponentsInChildren<TrashPickup>(true).Length : 0;
            int n = Mathf.Clamp(perZone, 1, Mathf.Max(1, pool));
            zoneTrashCount[z.id] = n;
            realTotal += n;
        }
        needSamples = !SaveSystem.Current.samplesDone && activeZones.Exists(z => z.hasWaterSamples);

        // Recorrido: del vecino al primer montón, y de ahí al siguiente.
        float travel = 0f;
        Vector3 from = currentGiver.transform.position;
        foreach (Zone z in activeZones)
        {
            Vector3 to = SpotCenter(z);
            travel += Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(to.x, 0f, to.z));
            from = to;
        }
        // Calles en vez de línea recta (x1.35), a paso de trote (~5 m/s).
        float travelSeconds = travel * 1.35f / 5f;
        float perTrash = Mathf.Max(minSecondsPerTrash, baseSecondsPerTrash - secondsLessPerMission * level);
        float tighten = Mathf.Max(minTighten, 1f - tightenPerMission * level);
        timeLimit = (travelSeconds + realTotal * perTrash + 25f + (needSamples ? samplesExtraSeconds : 0f)) * tighten;

        ConfigureGiverDialogue(level);
        SetGiverActive(currentGiver, true);

        var objectives = new List<Objective>
        {
            new Objective { id = TalkObjective, description = $"Habla con {GiverName()}", targetCount = 1 },
        };
        AddRosaObjective(objectives);
        ObjectiveSystem.Instance.SetObjectives(objectives);

        phase = Phase.TalkToGiver;
        if (RouteGuide.Instance != null) RouteGuide.Instance.ShowMissionRoute();
        UpdateMissionsTitle();
        string load = activeZones.Count > 1 ? $"{realTotal} bolsas en {activeZones.Count} lugares" : $"{realTotal} bolsas";
        ShowBanner($"MISIÓN {level + 1}  ·  {load}", $"{GiverName()} te está buscando. Sigue la ruta morada del mapa.");
    }

    // Un punto de limpieza al azar dentro de la zona (distinto del último).
    private CleanupSpot PickSpot(Zone z)
    {
        if (z.root == null) return null;
        CleanupSpot[] spots = z.root.GetComponentsInChildren<CleanupSpot>(true);
        if (spots.Length == 0) return null;
        lastSpot.TryGetValue(z.id, out CleanupSpot last);
        var options = new List<CleanupSpot>();
        foreach (CleanupSpot sp in spots) if (sp != last) options.Add(sp);
        if (options.Count == 0) options.AddRange(spots);
        CleanupSpot pick = options[Random.Range(0, options.Count)];
        lastSpot[z.id] = pick;
        return pick;
    }

    private Vector3 SpotCenter(Zone z)
    {
        if (chosenSpots.TryGetValue(z.id, out CleanupSpot sp) && sp != null) return sp.Center;
        return z.center != null ? z.center.position : (z.root != null ? z.root.transform.position : Vector3.zero);
    }

    private string SpotName(Zone z) =>
        chosenSpots.TryGetValue(z.id, out CleanupSpot sp) && sp != null ? sp.PlaceName : z.displayName;

    private string GiverZone(int index) =>
        giverZones != null && index >= 0 && index < giverZones.Length ? giverZones[index] : "";

    private int PickGiver()
    {
        // Primero, los vecinos con zona propia que todavía no te la encargaron
        // (así la chacra de los perros y el mirador no se quedan sin misión).
        string done = SaveSystem.Current.specialZonesDone ?? "";
        for (int i = 0; i < giverCandidates.Length; i++)
        {
            string zone = GiverZone(i);
            if (giverCandidates[i] == null || string.IsNullOrEmpty(zone)) continue;
            if (("," + done + ",").Contains("," + zone + ",")) continue;
            return i;
        }

        // Que no repitan los últimos que ya te pidieron algo: así te habla
        // gente distinta del pueblo cada vez.
        var options = new List<int>();
        for (int i = 0; i < giverCandidates.Length; i++)
            if (giverCandidates[i] != null && !recentGivers.Contains(i)) options.Add(i);
        if (options.Count == 0)
            for (int i = 0; i < giverCandidates.Length; i++) if (giverCandidates[i] != null) options.Add(i);
        return options.Count == 0 ? -1 : options[Random.Range(0, options.Count)];
    }

    private string GiverName() =>
        giverNames != null && currentGiverIndex >= 0 && currentGiverIndex < giverNames.Length
            ? giverNames[currentGiverIndex] : currentGiver.NpcName;

    private bool GiverIsFemale() =>
        giverFemale != null && currentGiverIndex >= 0 && currentGiverIndex < giverFemale.Length && giverFemale[currentGiverIndex];

    private string GiverRole() =>
        giverRoles != null && currentGiverIndex >= 0 && currentGiverIndex < giverRoles.Length && !string.IsNullOrEmpty(giverRoles[currentGiverIndex])
            ? giverRoles[currentGiverIndex] : (GiverIsFemale() ? "VECINA DEL BARRIO" : "VECINO DEL BARRIO");

    // ---------------------------------------------------------------
    // Cómo habla la gente del valle
    // ---------------------------------------------------------------
    private static string ZoneComplaint(string id)
    {
        switch (id)
        {
            case "plaza_vea": return "Afuera del Plaza Vea la gente sale con sus bolsas y las deja tiradas nomás, ya parece basural";
            case "parque": return "En la Plaza Constitución los chiquitos ya no pueden ni jugar, está lleno de botellas y envolturas";
            case "camino_chacra": return "Por el caminito a la chacra han botado un montón de basura, los perros la desparraman toda y cuando llueve se va a la acequia";
            case "rio": return "En la orilla del Mantaro hay botellas hasta flotando, ¡atatay! Con esa agua regamos las papas";
            case "calle_real": return "La Calle Real está cochinita, después de la feria del domingo nadie recogió nada";
            case "arequipa": return "En el Jr. Arequipa sacan su basura cuando el camión ya pasó, y los perritos la desparraman toda";
            case "puente": return "En el puente la gente tira sus cosas de frente al río, qué pena da, oye";
            case "amazonas": return "Por el Jr. Amazonas han dejado bolsas en las esquinas y ya huele feo, feo";
            case "bodega": return "Al ladito de la bodega de Doña Rosa se junta la basura de toda la cuadra";
            case "mirador": return "Arriba en el mirador la gente sube a ver el valle, se come su helado y deja los vasitos y las bolsas tiradas en la bajada";
            default: return "Hay mucha basura tirada por el barrio";
        }
    }

    // Cada vecino habla a su manera: la señora de mercado no habla igual que
    // el chibolo del barrio ni que la profesora.
    private enum Voice { Mother, Father, Young, Teacher, Leader }

    private Voice GiverVoice()
    {
        string n = GiverName();
        if (n.StartsWith("Joven") || n.StartsWith("Señorita")) return Voice.Young;
        if (n.StartsWith("Profesor")) return Voice.Teacher;
        if (n == "Yamile") return Voice.Leader;
        return GiverIsFemale() ? Voice.Mother : Voice.Father;
    }

    private static string Pick(params string[] options) => options[Random.Range(0, options.Length)];

    private void ConfigureGiverDialogue(int level)
    {
        Voice voice = GiverVoice();
        // Qué pasa y DÓNDE exactamente: el montón queda marcado en el mapa.
        string complaint = $"{ZoneComplaint(activeZones[0].id)}. Justo {SpotName(activeZones[0])} se ha hecho un montón de basura";
        string extra = "";
        if (activeZones.Count > 1)
        {
            var more = new List<string>();
            for (int i = 1; i < activeZones.Count; i++) more.Add(SpotName(activeZones[i]));
            extra = $" Y no es lo único, ah: también hay otro montón {string.Join(" y otro ", more)}.";
        }
        string samples = needSamples ? " Ah, y de paso llévate unos frasquitos y saca muestras del agua del río, para saber qué tan sucia está." : "";
        string time = FormatTime(timeLimit);

        string greeting, ask, yes, yesReply, no1, no1Reply, no2, no2Reply;
        switch (voice)
        {
            case Voice.Mother:
                greeting = Pick("¡Ay, Kuntur, hijito! Qué bueno que te encuentro.", "¡Kuntur, ven, ven, hijito! Toda la mañana te he estado buscando.", "¡Ay, mi Kuntur! Justo a ti te quería ver.");
                ask = "¿Me ayudas, pues? Yo ya no tengo fuerzas para agacharme tanto.";
                yes = Pick("¡Ya, señito! Ahorita mismo voy.", "Claro, mamita, déjemelo a mí.");
                yesReply = $"¡Ay, gracias, hijito! Que Diosito te lo pague. Te marqué el camino en tu mapa, sigue la línea morada. Tienes {time} nomás, ¿ya? Antes que llueva.";
                no1 = "Ay no, señito, que lo recoja la municipalidad, pe.";
                no1Reply = "¿La muni? Ay, hijito, esos pasan cuando se acuerdan nomás. Si no lo hacemos nosotros, ¿quién pues?";
                no2 = "Un poquito de basura no le hace nada a nadie.";
                no2Reply = "¿Poquito? Todo ese poquito se va derechito al Mantaro, y de ahí tomamos todos. Piénsalo, pues.";
                break;
            case Voice.Young:
                greeting = Pick("¡Causa, Kuntur! Justo te estaba buscando, oe.", "¡Kuntur, pata! Ven un toque, que está feo esto.", "¡Oe, Kuntur! ¿Tienes un ratito?");
                ask = "¿Me das una mano? Entre los dos lo dejamos limpiecito al toque.";
                yes = Pick("¡Normal, causa! Vamos de una.", "¡Ya, bro! Yo me encargo.");
                yesReply = $"¡Esa, bro! Sabía que contaba contigo. Te pasé la ruta a tu mapa, la línea morada. Tienes {time}, así que corre nomás.";
                no1 = "Uy, no, que lo limpie otro, yo estoy ocupado.";
                no1Reply = "Si todos decimos lo mismo, nadie lo limpia nunca, causa. El valle es de todos, pe.";
                no2 = "¿Y a mí qué? Yo no la boté.";
                no2Reply = "Yo tampoco, bro, pero el río no sabe quién la botó. Igual se ensucia.";
                break;
            case Voice.Teacher:
                greeting = Pick("¡Kuntur, qué gusto verte! Justo les hablaba de ti a mis alumnos.", "¡Buenos días, Kuntur! Te necesito para algo importante.");
                ask = "¿Nos ayudas a limpiar? Los chicos van a aprender mucho de tu ejemplo.";
                yes = Pick("Con gusto, profesora. Voy ahora mismo.", "¡Claro, profe! Cuente conmigo.");
                yesReply = $"¡Excelente, Kuntur! Así se da el ejemplo. Te dejé el camino marcado en el mapa, la línea morada. Tienes {time}, no te distraigas, ¿ya?";
                no1 = "Ahorita no, profe, tengo otras cosas que hacer.";
                no1Reply = "Te entiendo, pero la basura no espera: con la primera lluvia se va al río. Anímate, pues.";
                no2 = "Siempre ha sido así, no va a cambiar.";
                no2Reply = "Si pensamos así, nunca va a cambiar. Todo empieza con alguien que se anima. ¿Por qué no tú?";
                break;
            case Voice.Leader:
                greeting = Pick("¡Kuntur, compañero! Qué bueno que llegas.", "¡Kuntur! Te estaba esperando, tenemos trabajo en el barrio.");
                ask = "¿Te animas? Si el barrio se organiza, el valle se salva.";
                yes = Pick("¡Vamos, Yamile! Yo me encargo.", "Claro que sí, cuenta conmigo.");
                yesReply = $"¡Eso, compañero! Te marqué la ruta en el mapa, la línea morada. Tienes {time}. ¡Vamos que se puede!";
                no1 = "Que lo haga la municipalidad, para eso pagamos.";
                no1Reply = "La muni también tiene que cumplir, pero mientras tanto el río se sigue ensuciando. No podemos esperar.";
                no2 = "Eso no es mi problema.";
                no2Reply = "El agua que tomamos todos viene de ahí, Kuntur. Sí es nuestro problema.";
                break;
            default: // Father
                greeting = Pick("¡Oye, Kuntur! Ven un ratito, pues, que te quiero contar algo.", "¡Kuntur, muchacho! Qué bueno que pasas por aquí.", "¡Buenas, Kuntur! Justo te andaba buscando.");
                ask = "¿Me echas una mano? Yo con esta rodilla ya no puedo tanto.";
                yes = Pick("¡Claro, don! Déjemelo a mí.", "Ya, señor, ahorita voy y lo dejo limpiecito.");
                yesReply = $"¡Así me gusta, muchacho! Eso es ser del valle. Te marqué la ruta en tu mapa, sigue la línea morada. Tienes {time} nomás, ¿ya?";
                no1 = "Uy, don, que lo recoja la municipalidad, pe.";
                no1Reply = "¡Qué va a venir la muni! Si esperamos, la lluvia se lo lleva todo al río, muchacho.";
                no2 = "Bah, siempre ha habido basura ahí.";
                no2Reply = "Por eso mismo, pues. Si nadie hace nada, el Mantaro se nos muere. ¿Vas a dejar que pase eso?";
                break;
        }

        string opening = $"{greeting} {complaint}.{extra}{samples} {ask}";
        var options = new[]
        {
            new DialogueOption { text = yes, isCorrect = true, npcResponse = yesReply },
            new DialogueOption { text = no1, isCorrect = false, npcResponse = no1Reply },
            new DialogueOption { text = no2, isCorrect = false, npcResponse = no2Reply },
        };
        // Se mezclan: que la buena respuesta no sea siempre la número 1.
        for (int i = options.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (options[i], options[j]) = (options[j], options[i]);
        }

        currentGiver.Configure(GiverName(), GiverRole(), opening, options, TalkObjective);
    }

    // ---------------------------------------------------------------
    // Paso 2: aceptó -> aparece la basura y corre el tiempo
    // ---------------------------------------------------------------
    private void HandleGiverConvinced(DialogueNPC npc)
    {
        if (npc != currentGiver || phase != Phase.TalkToGiver) return;
        StartCoroutine(BeginCleaningAfter(3.2f)); // que se alcance a leer la respuesta
    }

    private IEnumerator BeginCleaningAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetGiverActive(currentGiver, false);
        lastGiverIndex = currentGiverIndex;
        recentGivers.Add(currentGiverIndex);
        while (recentGivers.Count > Mathf.Min(4, giverCandidates.Length - 1)) recentGivers.RemoveAt(0);

        var objectives = new List<Objective>();
        foreach (Zone zone in activeZones)
        {
            int count = zoneTrashCount[zone.id];
            PrepareZone(zone, count);
            objectives.Add(new Objective { id = zone.id, description = $"Recoge las bolsas de basura {SpotName(zone)}", targetCount = count });
        }
        if (needSamples)
            objectives.Add(new Objective { id = SamplesObjective, description = "Toma 4 muestras de agua del río", targetCount = 4 });
        AddRosaObjective(objectives);
        ObjectiveSystem.Instance.SetObjectives(objectives);

        phase = Phase.Cleaning;
        if (RouteGuide.Instance != null) RouteGuide.Instance.ShowMissionRoute();
        if (countdown != null) countdown.StartCountdown(timeLimit);
        if (DayManager.Instance != null && DayManager.Instance.DayOver && countdown != null) countdown.PauseCountdown();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMissionMusic();
        ShowBanner("¡MISIÓN ACEPTADA!", $"Ve {SpotName(activeZones[0])} (línea morada) y recoge todo en {FormatTime(timeLimit)}.");
    }

    // Prende solo la cantidad de residuos que pide esta misión (al azar entre
    // todos los puntos posibles de la zona) y los deja como nuevos.
    private void PrepareZone(Zone zone, int count)
    {
        if (zone.root == null) return;
        zone.root.SetActive(true);

        // Solo el montón elegido queda prendido; los otros puntos de la zona, no.
        CleanupSpot chosen = chosenSpots.TryGetValue(zone.id, out CleanupSpot sp) ? sp : null;
        foreach (CleanupSpot other in zone.root.GetComponentsInChildren<CleanupSpot>(true))
            other.gameObject.SetActive(other == chosen);

        Transform poolRoot = chosen != null ? chosen.transform : zone.root.transform;
        TrashPickup[] pool = poolRoot.GetComponentsInChildren<TrashPickup>(true);
        for (int i = pool.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        for (int i = 0; i < pool.Length; i++)
        {
            if (i < count) pool[i].ResetPickup(zone.id);
            else pool[i].gameObject.SetActive(false);
        }

        foreach (WaterSampleKit kit in zone.root.GetComponentsInChildren<WaterSampleKit>(true))
            kit.gameObject.SetActive(needSamples && zone.hasWaterSamples && !kit.Sampled);
    }

    // ---------------------------------------------------------------
    // Paso 3: terminar a tiempo -> estrellas; no llegar -> derrota
    // ---------------------------------------------------------------
    private void HandleObjectiveCompleted(Objective objective)
    {
        if (phase != Phase.Cleaning) return;

        if (objective.id == SamplesObjective)
        {
            SaveSystem.Current.samplesDone = true;
            AddStars(1);
            ShowBanner("¡MUESTRAS LISTAS!", "+1 estrella. Ya sabemos qué tan contaminada está el agua.", 1);
            return;
        }

        Zone zone = activeZones.Find(z => z.id == objective.id);
        if (zone == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayTrashPickup();
        if (activeZones.Count > 1) ShowBanner($"¡{zone.displayName.ToUpper()}, LIMPIO!", "Bien hecho. Sigue, que falta poco.");
    }

    private void HandleAllMainDone()
    {
        if (phase != Phase.Cleaning) return;
        phase = Phase.Waiting;

        float left = countdown != null ? countdown.Fraction01 : 1f;
        float secondsLeft = countdown != null ? countdown.Remaining : 0f;
        if (countdown != null) countdown.StopCountdown();
        int stars = left >= 0.5f ? 3 : left >= 0.25f ? 2 : 1;
        AddStars(stars);
        MissionsDone++;

        recentZones.Clear();
        foreach (Zone z in activeZones) recentZones.Add(z.id);
        foreach (Zone z in activeZones) if (z.root != null) z.root.SetActive(false);

        // Guardado: lo logrado hoy no se pierde aunque se cierre el juego.
        SaveData save = SaveSystem.Current;
        save.stars = TotalStars;
        save.missionsDone = MissionsDone;
        save.lastZones = string.Join(",", recentZones);
        foreach (Zone z in activeZones)
        {
            string list = save.specialZonesDone ?? "";
            if (!("," + list + ",").Contains("," + z.id + ","))
                save.specialZonesDone = string.IsNullOrEmpty(list) ? z.id : list + "," + z.id;
        }
        if (ValleyHealthManager.Instance != null) save.health = ValleyHealthManager.Instance.CurrentHealth;
        SaveSystem.SaveCurrent();

        if (AudioManager.Instance != null) { AudioManager.Instance.PlayVictory(); AudioManager.Instance.PlayGameMusic(); }
        string verdict = stars == 3 ? "¡Impecable, cumpliste con lo acordado!" : stars == 2 ? "¡Bien! Un poquito más rápido y son 3." : "Justo a tiempo, ¡uf!";
        string title = $"¡MISIÓN {MissionsDone} CUMPLIDA!";
        string body = $"{verdict} Te sobraron {FormatTime(secondsLeft)}.";

        ObjectiveSystem.Instance.SetObjectives(new List<Objective>());
        UpdateMissionsTitle();

        // Kuntur celebra bailando (cámara delante) hasta que el jugador
        // aprieta CONTINUAR; recién ahí aparece el siguiente vecino.
        if (victoryDance != null && KunturMixamoAnimator.Instance != null)
        {
            victoryDance.Show(title, body, stars, () => StartCoroutine(StartLevelAfter(2.5f)));
            return;
        }

        ShowBanner(title, body, stars);
        // Aventura sin fin: al toque aparece alguien más que necesita ayuda.
        StartCoroutine(StartLevelAfter(4f));
    }

    private void HandleTimeUp()
    {
        if (phase != Phase.Cleaning) return;
        phase = Phase.Done;
        CurrentTarget = null;

        string places = DescribeZones(activeZones);
        if (climax != null)
            climax.FailMission("¡SE ACABÓ EL TIEMPO!",
                $"No alcanzaste a limpiar {places} y la lluvia se llevó la basura al Mantaro. Descansa y vuelve a intentarlo.",
                MissionsDone, TotalStars);
    }

    // ---------------------------------------------------------------
    // Noche: el tiempo de la misión se congela hasta mañana
    // ---------------------------------------------------------------
    public void PauseForNight()
    {
        if (countdown != null) countdown.PauseCountdown();
    }

    public void OnNewDay()
    {
        if (phase == Phase.Cleaning && countdown != null) countdown.ResumeCountdown();
        if (pendingStart || phase == Phase.Waiting) StartCoroutine(StartLevelAfter(6f));
    }

    private void AddStars(int amount)
    {
        TotalStars += amount;
        SimpleThirdPersonController.AddEnergy(1f); // cumplir te recarga la energía
        SaveSystem.Current.stars = TotalStars;
        OnStarsChanged?.Invoke(TotalStars);
    }

    private void AddRosaObjective(List<Objective> objectives)
    {
        if (rosa != null && rosa.Convinced) return;
        Objective old = ObjectiveSystem.Instance != null ? ObjectiveSystem.Instance.Find(RosaObjective) : null;
        if (old != null && old.IsComplete) return;
        objectives.Add(new Objective
        {
            id = RosaObjective, description = "Convence a Doña Rosa de no botar basura al río",
            targetCount = 1, isSecondary = true,
        });
    }

    private void UpdateMissionsTitle()
    {
        if (missionsTitle != null) missionsTitle.text = $"MISIÓN {MissionsDone + 1}  ·  CUIDADO AMBIENTAL";
    }

    // ---------------------------------------------------------------
    // Cada frame: a dónde apunta la ruta y el "!" sobre el vecino
    // ---------------------------------------------------------------
    private void Update()
    {
        UpdateTarget();
        UpdateMarker();
#if UNITY_EDITOR
        // Atajo de prueba (solo en el editor): F9 lleva a Kuntur junto a su
        // objetivo actual, para probar diálogos y misiones sin cruzar el pueblo.
        Vector3? debugTarget = RouteGuide.Instance != null ? RouteGuide.Instance.ActiveTarget : CurrentTarget;
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame && debugTarget.HasValue)
        {
            var p = FindAnyObjectByType<SimpleThirdPersonController>();
            if (p != null) p.Teleport(debugTarget.Value + new Vector3(1.6f, 0.2f, -1.6f), -45f);
        }
#endif

        if (bannerTimer > 0f)
        {
            bannerTimer -= Time.deltaTime;
            if (bannerTimer <= 0f && bannerPanel != null)
            {
                // Se va con su animación (sube y se esfuma), no de golpe.
                BannerFx fx = bannerPanel.GetComponent<BannerFx>();
                if (fx != null) fx.Hide();
                else bannerPanel.SetActive(false);
            }
        }
    }

    private void UpdateTarget()
    {
        if (phase == Phase.TalkToGiver && currentGiver != null)
        {
            CurrentTarget = currentGiver.transform.position;
            return;
        }

        if (phase != Phase.Cleaning) { CurrentTarget = null; return; }

        // La ruta lleva al residuo más cercano que falte: así se va de uno en
        // uno sin buscar a ciegas.
        if (mainCamera == null) mainCamera = Camera.main;
        Vector3 from = mainCamera != null ? mainCamera.transform.position : Vector3.zero;

        float best = float.MaxValue;
        Vector3? target = null;
        foreach (Zone zone in activeZones)
        {
            if (zone.root == null || !zone.root.activeInHierarchy) continue;
            foreach (TrashPickup trash in zone.root.GetComponentsInChildren<TrashPickup>())
            {
                if (trash.Collected) continue;
                float d = (trash.transform.position - from).sqrMagnitude;
                if (d < best) { best = d; target = trash.transform.position; }
            }
            if (needSamples && zone.hasWaterSamples)
            {
                foreach (WaterSampleKit kit in zone.root.GetComponentsInChildren<WaterSampleKit>())
                {
                    if (kit.Sampled) continue;
                    float d = (kit.transform.position - from).sqrMagnitude;
                    if (d < best) { best = d; target = kit.transform.position; }
                }
            }
        }
        CurrentTarget = target;
    }

    private void UpdateMarker()
    {
        if (giverMarker == null) return;
        bool show = phase == Phase.TalkToGiver && currentGiver != null;
        if (giverMarker.gameObject.activeSelf != show) giverMarker.gameObject.SetActive(show);
        UpdateGiverMapIcon(show);
        if (!show) return;

        giverMarker.position = currentGiver.transform.position + Vector3.up * (3.3f + Mathf.Sin(Time.time * 3f) * 0.12f);
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 look = giverMarker.position - mainCamera.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) giverMarker.rotation = Quaternion.LookRotation(look);
        }
    }

    // Punto amarillo en el minimapa sobre quien te busca (la capa "SoloMapa"
    // solo la ve la cámara del mapa).
    private void UpdateGiverMapIcon(bool show)
    {
        if (giverMapIcon == null)
        {
            if (!show) return;
            int layer = LayerMask.NameToLayer("SoloMapa");
            Shader shader = Shader.Find("Sprites/Default");
            if (layer < 0 || shader == null) return;
            giverMapIcon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            giverMapIcon.name = "Icono_Mapa_Vecino";
            Destroy(giverMapIcon.GetComponent<Collider>());
            giverMapIcon.layer = layer;
            giverMapIcon.transform.localScale = new Vector3(6f, 0.02f, 6f);
            var mat = new Material(shader) { color = new Color(1f, 0.82f, 0.1f) };
            giverMapIcon.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        if (giverMapIcon.activeSelf != show) giverMapIcon.SetActive(show);
        if (show && currentGiver != null)
        {
            Vector3 p = currentGiver.transform.position;
            giverMapIcon.transform.position = new Vector3(p.x, 47f, p.z);
        }
    }

    private static void SetGiverActive(DialogueNPC giver, bool active)
    {
        // Solo el vecino del nivel se puede hablar: los demás siguen
        // conversando entre ellos, sin prompt ni marcador.
        foreach (Collider col in giver.GetComponents<Collider>()) col.enabled = active;
        // Si es alguien que iba caminando, se queda parado esperándote.
        PedestrianWalker walker = giver.GetComponent<PedestrianWalker>();
        if (walker != null) walker.enabled = !active;
    }

    public void HideBanner()
    {
        if (bannerPanel != null) bannerPanel.SetActive(false);
        bannerTimer = 0f;
    }

    // Cartel de arriba al centro. stars >= 0 muestra las 3 estrellitas.
    public void ShowBanner(string title, string body, int stars = -1, float seconds = 6f)
    {
        if (bannerPanel == null) return;
        bannerPanel.SetActive(true);
        BannerFx bannerFx = bannerPanel.GetComponent<BannerFx>();
        if (bannerFx != null) bannerFx.Play();
        if (bannerTitle != null) bannerTitle.text = title;
        if (bannerBody != null) bannerBody.text = body;
        if (bannerStars != null)
        {
            for (int i = 0; i < bannerStars.Length; i++)
            {
                if (bannerStars[i] == null) continue;
                bannerStars[i].gameObject.SetActive(stars >= 0);
                bannerStars[i].sprite = i < stars ? starFilled : starEmpty;
            }
        }
        bannerTimer = seconds;
    }

    private static string DescribeZones(List<Zone> list)
    {
        if (list.Count == 0) return "";
        if (list.Count == 1) return list[0].displayName;
        var names = new List<string>();
        foreach (Zone z in list) names.Add(z.displayName);
        return string.Join(", ", names.GetRange(0, names.Count - 1)) + " y " + names[names.Count - 1];
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int m = total / 60, s = total % 60;
        string min = m == 1 ? "1 minuto" : $"{m} minutos";
        if (m == 0) return $"{s} segundos";
        return s == 0 ? min : $"{min} y {s} segundos";
    }
}
