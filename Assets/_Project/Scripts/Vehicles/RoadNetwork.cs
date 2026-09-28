using System.Collections.Generic;
using UnityEngine;

// Red de calles para que el tuk tuk llegue SOLO hasta donde está Kuntur
// (opción "Pedir mi tuk tuk" de la pausa).
//
// El generador de escena guarda cada calle como un tramo recto (todas corren
// de norte a sur o de este a oeste). Al arrancar, los tramos se cortan en
// cada cruce y queda un grafo: nodos en las esquinas y aristas por las
// cuadras. Con eso se busca el camino más corto (Dijkstra) por las calles de
// verdad: pasa por el puente, dobla en las esquinas y sube al mirador.
public class RoadNetwork : MonoBehaviour
{
    public static RoadNetwork Instance { get; private set; }

    [System.Serializable]
    public struct Segment
    {
        public Vector2 a;   // (x, z)
        public Vector2 b;
    }

    [SerializeField] private Segment[] segments;

    private readonly List<Vector2> nodes = new List<Vector2>();
    private readonly List<List<int>> links = new List<List<int>>();

    public int NodeCount => nodes.Count;

    private void Awake()
    {
        Instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetSegments(Segment[] value)
    {
        segments = value;
        Build();
    }

    private void Build()
    {
        nodes.Clear();
        links.Clear();
        if (segments == null) return;

        // Puntos de corte de cada tramo: sus extremos y cada cruce con otro.
        var cuts = new List<float>[segments.Length];
        for (int i = 0; i < segments.Length; i++)
        {
            cuts[i] = new List<float> { 0f, 1f };
            for (int j = 0; j < segments.Length; j++)
            {
                if (i == j) continue;
                if (Intersect(segments[i], segments[j], out float t)) cuts[i].Add(t);
            }
            cuts[i].Sort();
        }

        for (int i = 0; i < segments.Length; i++)
        {
            int previous = -1;
            float lastT = -1f;
            foreach (float t in cuts[i])
            {
                if (lastT >= 0f && Mathf.Abs(t - lastT) < 0.0001f) continue;
                lastT = t;
                Vector2 p = Vector2.Lerp(segments[i].a, segments[i].b, t);
                int node = NodeAt(p);
                if (previous >= 0 && previous != node) Link(previous, node);
                previous = node;
            }
        }
    }

    // Cruce entre dos tramos (o un extremo que toca al otro: las calles en T).
    private static bool Intersect(Segment s, Segment o, out float t)
    {
        t = 0f;
        Vector2 d = s.b - s.a;
        Vector2 e = o.b - o.a;
        float den = d.x * e.y - d.y * e.x;
        if (Mathf.Abs(den) < 0.0001f) return false; // paralelos
        Vector2 w = o.a - s.a;
        float ts = (w.x * e.y - w.y * e.x) / den;
        float to = (w.x * d.y - w.y * d.x) / den;
        const float tol = 0.02f;
        float tolS = tol * 10f / Mathf.Max(d.magnitude, 1f);
        float tolO = tol * 10f / Mathf.Max(e.magnitude, 1f);
        if (ts < -tolS || ts > 1f + tolS || to < -tolO || to > 1f + tolO) return false;
        t = Mathf.Clamp01(ts);
        return true;
    }

    private int NodeAt(Vector2 p)
    {
        for (int i = 0; i < nodes.Count; i++)
            if ((nodes[i] - p).sqrMagnitude < 1f) return i;
        nodes.Add(p);
        links.Add(new List<int>());
        return nodes.Count - 1;
    }

    private void Link(int a, int b)
    {
        if (!links[a].Contains(b)) links[a].Add(b);
        if (!links[b].Contains(a)) links[b].Add(a);
    }

    public Vector2 Node(int index) => nodes[index];

    public int NearestNode(Vector3 position)
    {
        Vector2 p = new Vector2(position.x, position.z);
        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            float d = (nodes[i] - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    // Punto de la calle más cercano a una posición (sobre cualquier cuadra),
    // y los dos nodos de esa cuadra.
    public bool ClosestPointOnRoad(Vector3 position, out Vector2 point, out int nodeA, out int nodeB)
    {
        Vector2 p = new Vector2(position.x, position.z);
        point = p;
        nodeA = nodeB = -1;
        float best = float.MaxValue;
        for (int a = 0; a < nodes.Count; a++)
        {
            foreach (int b in links[a])
            {
                if (b < a) continue;
                Vector2 q = ClosestOnSegment(nodes[a], nodes[b], p);
                float d = (q - p).sqrMagnitude;
                if (d < best) { best = d; point = q; nodeA = a; nodeB = b; }
            }
        }
        return nodeA >= 0;
    }

    private static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        float len = ab.sqrMagnitude;
        if (len < 0.0001f) return a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len);
        return a + ab * t;
    }

    // Camino más corto de un nodo a otro (lista de nodos, incluidos los dos).
    public List<int> FindPath(int from, int to)
    {
        var result = new List<int>();
        if (from < 0 || to < 0 || from >= nodes.Count || to >= nodes.Count) return result;

        int n = nodes.Count;
        var dist = new float[n];
        var prev = new int[n];
        var done = new bool[n];
        for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
        dist[from] = 0f;

        for (int step = 0; step < n; step++)
        {
            int u = -1;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
            if (u < 0 || u == to) break;
            done[u] = true;
            foreach (int v in links[u])
            {
                float alt = dist[u] + Vector2.Distance(nodes[u], nodes[v]);
                if (alt < dist[v]) { dist[v] = alt; prev[v] = u; }
            }
        }

        if (from != to && prev[to] < 0) return result;
        for (int at = to; at >= 0; at = prev[at])
        {
            result.Add(at);
            if (at == from) break;
        }
        result.Reverse();
        return result;
    }
}
