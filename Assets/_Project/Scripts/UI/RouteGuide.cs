using System.Collections.Generic;
using UnityEngine;

// "Cómo llego": calcula el camino por las CALLES (no en línea recta a través
// de las casas) desde Kuntur hasta un destino, y lo dibuja como una línea
// morada que se ve en el mapa grande y en el minimapa, igual que un GPS.
//
// El destino puede ser:
//   - uno marcado a mano con clic derecho en el mapa (en cualquier momento), o
//   - el de la misión activa (a quién hablarle, qué basura recoger).
// El marcado a mano manda; al llegar se borra solo.
//
// La línea vive en la capa "SoloMapa": la cámara del mapa la ve, la cámara
// del juego no. Así no hay una raya morada flotando en el cielo del pueblo.
public class RouteGuide : MonoBehaviour
{
    public static RouteGuide Instance { get; private set; }

    [System.Serializable]
    public struct Road
    {
        public Vector2 a; // (x, z) del mundo
        public Vector2 b;
    }

    [SerializeField] private Road[] roads;
    [SerializeField] private Transform player;
    [SerializeField] private LineRenderer line;
    [SerializeField] private Camera mapCamera;
    [SerializeField] private float lineHeight = 25f;
    [SerializeField] private float widthPerZoom = 0.035f;  // grosor según el zoom del mapa
    [SerializeField] private float arriveDistance = 6f;
    [SerializeField] private float recalcInterval = 0.4f;

    private Vector3? customTarget;
    private Vector3? lastTarget;
    private Vector3 lastStart;
    private float recalcTimer;

    // ---- Grafo de calles ----
    private readonly List<Vector2> nodes = new List<Vector2>();
    private readonly List<List<(int to, float cost)>> edges = new List<List<(int, float)>>();
    // Por cada calle, sus puntos de corte ordenados (esquinas y extremos).
    private readonly List<List<(float t, int node)>> roadCuts = new List<List<(float, int)>>();

    // De noche (después de las 9) la ruta siempre lleva a la casa de Kuntur.
    public Vector3? ActiveTarget =>
        customTarget ??
        (DayManager.Instance != null && DayManager.Instance.DayOver && !DayManager.Instance.Sleeping
            ? DayManager.Instance.HomePosition
            : (!missionRouteHidden && MissionDirector.Instance != null ? MissionDirector.Instance.CurrentTarget : null));

    // Doble clic en el mapa sin punto marcado: se esconde también la ruta de
    // la misión (hasta que marques otro destino o empiece otro encargo).
    private bool missionRouteHidden;

    public void HideRoute()
    {
        if (customTarget.HasValue) customTarget = null;
        else missionRouteHidden = true;
        recalcTimer = 0f;
    }

    public void ShowMissionRoute()
    {
        missionRouteHidden = false;
        recalcTimer = 0f;
    }

    private void Awake()
    {
        Instance = this;
        BuildGraph();
        if (line != null) line.enabled = false;
    }

    public void SetCustomTarget(Vector3 target)
    {
        customTarget = target;
        missionRouteHidden = false;
        recalcTimer = 0f;
    }

    public void ClearCustomTarget()
    {
        customTarget = null;
        recalcTimer = 0f;
    }

    private void Update()
    {
        if (player == null || line == null) return;

        // Llegó al destino marcado a mano: se borra solo.
        if (customTarget.HasValue && FlatDistance(player.position, customTarget.Value) < arriveDistance)
            customTarget = null;

        Vector3? target = ActiveTarget;
        if (!target.HasValue)
        {
            if (line.enabled) line.enabled = false;
            lastTarget = null;
            return;
        }

        if (mapCamera != null) line.widthMultiplier = Mathf.Max(0.8f, mapCamera.orthographicSize * widthPerZoom);

        recalcTimer -= Time.deltaTime;
        bool targetMoved = !lastTarget.HasValue || FlatDistance(lastTarget.Value, target.Value) > 0.5f;
        bool playerMoved = FlatDistance(lastStart, player.position) > 2f;
        if (recalcTimer > 0f && !targetMoved && !playerMoved) return;

        recalcTimer = recalcInterval;
        lastTarget = target;
        lastStart = player.position;

        List<Vector2> path = FindPath(new Vector2(player.position.x, player.position.z), new Vector2(target.Value.x, target.Value.z));
        line.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++) line.SetPosition(i, new Vector3(path[i].x, lineHeight, path[i].y));
        line.enabled = path.Count >= 2;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------------------------------------------------------------
    // Grafo: cada esquina (donde se cruzan dos calles) es un nodo, y cada
    // tramo de calle entre dos esquinas es una arista.
    // ---------------------------------------------------------------
    private void BuildGraph()
    {
        nodes.Clear(); edges.Clear(); roadCuts.Clear();
        if (roads == null) return;

        var index = new Dictionary<Vector2Int, int>();
        int NodeAt(Vector2 p)
        {
            Vector2Int key = new Vector2Int(Mathf.RoundToInt(p.x * 4f), Mathf.RoundToInt(p.y * 4f));
            if (index.TryGetValue(key, out int i)) return i;
            nodes.Add(p);
            edges.Add(new List<(int, float)>());
            index[key] = nodes.Count - 1;
            return nodes.Count - 1;
        }

        for (int i = 0; i < roads.Length; i++)
        {
            var cuts = new List<(float t, int node)> { (0f, NodeAt(roads[i].a)), (1f, NodeAt(roads[i].b)) };
            for (int j = 0; j < roads.Length; j++)
            {
                if (i == j) continue;
                if (Intersect(roads[i], roads[j], out Vector2 p, out float t)) cuts.Add((t, NodeAt(p)));
            }
            cuts.Sort((x, y) => x.t.CompareTo(y.t));
            roadCuts.Add(cuts);

            for (int k = 0; k + 1 < cuts.Count; k++)
            {
                int n0 = cuts[k].node, n1 = cuts[k + 1].node;
                if (n0 == n1) continue;
                float cost = Vector2.Distance(nodes[n0], nodes[n1]);
                edges[n0].Add((n1, cost));
                edges[n1].Add((n0, cost));
            }
        }
    }

    // Cruce de dos tramos rectos (t = dónde cae sobre el primero, de 0 a 1).
    private static bool Intersect(Road r1, Road r2, out Vector2 point, out float t)
    {
        point = Vector2.zero; t = 0f;
        Vector2 p = r1.a, r = r1.b - r1.a, q = r2.a, s = r2.b - r2.a;
        float denom = r.x * s.y - r.y * s.x;
        if (Mathf.Abs(denom) < 1e-5f) return false; // paralelas
        Vector2 qp = q - p;
        t = (qp.x * s.y - qp.y * s.x) / denom;
        float u = (qp.x * r.y - qp.y * r.x) / denom;
        const float eps = 0.02f;
        if (t < -eps || t > 1f + eps || u < -eps || u > 1f + eps) return false;
        t = Mathf.Clamp01(t);
        point = p + r * t;
        return true;
    }

    // Punto de las calles más cercano a una posición: en qué calle, en qué
    // parte de ella (t) y dónde queda.
    private bool NearestOnRoads(Vector2 pos, out int roadIndex, out float t, out Vector2 point)
    {
        roadIndex = -1; t = 0f; point = pos;
        float best = float.MaxValue;
        for (int i = 0; i < roads.Length; i++)
        {
            Vector2 ab = roads[i].b - roads[i].a;
            float len2 = ab.sqrMagnitude;
            float tt = len2 < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(pos - roads[i].a, ab) / len2);
            Vector2 p = roads[i].a + ab * tt;
            float d = (p - pos).sqrMagnitude;
            if (d < best) { best = d; roadIndex = i; t = tt; point = p; }
        }
        return roadIndex >= 0;
    }

    // Camino más corto por las calles (Dijkstra; el pueblo tiene pocas
    // esquinas, así que alcanza y sobra).
    private List<Vector2> FindPath(Vector2 from, Vector2 to)
    {
        var result = new List<Vector2>();
        if (nodes.Count == 0 || !NearestOnRoads(from, out int ra, out float ta, out Vector2 pa)
                             || !NearestOnRoads(to, out int rb, out float tb, out Vector2 pb))
        {
            result.Add(from); result.Add(to);
            return result;
        }

        // Nodos temporales: donde Kuntur entra a la calle y donde sale.
        int start = nodes.Count, end = nodes.Count + 1;
        int total = nodes.Count + 2;
        var dist = new float[total];
        var prev = new int[total];
        var done = new bool[total];
        for (int i = 0; i < total; i++) { dist[i] = float.MaxValue; prev[i] = -1; }

        List<(int to, float cost)> Neighbors(int n)
        {
            var list = new List<(int, float)>();
            if (n < nodes.Count) list.AddRange(edges[n]);

            // Enlaces de los nodos temporales con las esquinas vecinas de su calle.
            void Link(int temp, int road, float t, Vector2 p)
            {
                var cuts = roadCuts[road];
                for (int k = 0; k + 1 < cuts.Count; k++)
                {
                    if (t < cuts[k].t - 1e-4f || t > cuts[k + 1].t + 1e-4f) continue;
                    int left = cuts[k].node, right = cuts[k + 1].node;
                    if (n == temp)
                    {
                        list.Add((left, Vector2.Distance(p, nodes[left])));
                        list.Add((right, Vector2.Distance(p, nodes[right])));
                    }
                    else if (n == left) list.Add((temp, Vector2.Distance(p, nodes[left])));
                    else if (n == right) list.Add((temp, Vector2.Distance(p, nodes[right])));
                    break;
                }
            }
            Link(start, ra, ta, pa);
            Link(end, rb, tb, pb);

            // Los dos en la misma cuadra de la misma calle: directo.
            if (ra == rb && (n == start || n == end))
                list.Add((n == start ? end : start, Vector2.Distance(pa, pb)));
            return list;
        }

        Vector2 PosOf(int n) => n == start ? pa : n == end ? pb : nodes[n];

        dist[start] = 0f;
        for (int iter = 0; iter < total; iter++)
        {
            int u = -1;
            float best = float.MaxValue;
            for (int i = 0; i < total; i++)
                if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
            if (u < 0 || u == end) break;
            done[u] = true;

            foreach (var (v, cost) in Neighbors(u))
            {
                if (done[v]) continue;
                float nd = dist[u] + cost;
                if (nd < dist[v]) { dist[v] = nd; prev[v] = u; }
            }
        }

        result.Add(from);
        if (prev[end] >= 0 || start == end)
        {
            var chain = new List<Vector2>();
            for (int n = end; n >= 0; n = prev[n]) chain.Add(PosOf(n));
            chain.Reverse();
            result.AddRange(chain);
        }
        result.Add(to);
        return result;
    }
}
