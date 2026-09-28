using UnityEngine;

// Agua del río de verdad: una malla de cuadrícula cuyos vértices se mueven con
// dos olas que viajan corriente abajo, así el río se ve fluyendo en vez de ser
// una plancha azul quieta. La malla se genera en Awake (no es un asset), y las
// normales se recalculan cada frame para que la luz del sol y del atardecer
// rebote en las olas.
// ExecuteAlways: así el agua también se ve (y se mueve) en la ventana Scene
// del Editor, no solo al darle Play. Si no, el río se vería vacío mientras se
// arma la escena y parecería que algo falló.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RiverWater : MonoBehaviour
{
    [Header("Tamaño del cauce")]
    [SerializeField] private float width = 13f;
    [SerializeField] private float length = 190f;
    // Malla deliberadamente ligera: en una laptop con gráficos integrados,
    // recalcular normales de miles de vértices cada frame es lo que termina
    // colgando el driver.
    [SerializeField] private int columns = 6;
    [SerializeField] private int rows = 40;

    [Header("Corriente")]
    [SerializeField] private float waveHeight = 0.11f;
    [SerializeField] private float waveLength = 6f;
    [SerializeField] private float flowSpeed = 4.5f;
    [SerializeField] private float crossWaveHeight = 0.05f;

    [Header("Recorrido con curvas (opcional)")]
    // Puntos del eje del río, en coordenadas locales. Si hay al menos dos, la
    // malla sigue ese recorrido (el Mantaro serpentea) en vez de ser una
    // franja recta; cada fila de la malla va perpendicular al cauce.
    [SerializeField] private Vector3[] path;

    private Mesh mesh;
    private Vector3[] baseVertices;
    private Vector3[] vertices;
    private float[] along;   // metros recorridos a lo largo del río (para las olas)
    private float[] across;  // metros desde el eje hacia el costado

    private void Awake()
    {
        BuildGrid();
    }

    private void BuildGrid()
    {
        // Si ya hay una malla propia (por ejemplo tras recompilar en el
        // Editor) se reutiliza en vez de crear otra y dejar la anterior
        // colgada en memoria.
        if (mesh != null) return;

        mesh = new Mesh { name = "Agua_Rio" };
        mesh.MarkDynamic(); // se actualiza cada frame

        bool curved = path != null && path.Length >= 2;
        int cols = Mathf.Max(2, columns);
        int rws = curved ? path.Length : Mathf.Max(2, rows);
        along = new float[cols * rws];
        across = new float[cols * rws];
        float distance = 0f;

        baseVertices = new Vector3[cols * rws];
        Vector2[] uvs = new Vector2[baseVertices.Length];
        int[] triangles = new int[(cols - 1) * (rws - 1) * 6];

        for (int z = 0; z < rws; z++)
        {
            Vector3 center = Vector3.zero;
            Vector3 side = Vector3.right;
            if (curved)
            {
                center = path[z];
                Vector3 tangent = path[Mathf.Min(z + 1, rws - 1)] - path[Mathf.Max(z - 1, 0)];
                tangent.y = 0f;
                tangent.Normalize();
                side = new Vector3(tangent.z, 0f, -tangent.x);
                if (z > 0) distance += Vector3.Distance(path[z], path[z - 1]);
            }
            else
            {
                center = new Vector3(0f, 0f, (z / (float)(rws - 1) - 0.5f) * length);
                distance = center.z;
            }

            for (int x = 0; x < cols; x++)
            {
                float offset = (x / (float)(cols - 1) - 0.5f) * width;
                int index = z * cols + x;
                baseVertices[index] = new Vector3(center.x + side.x * offset, center.y, center.z + side.z * offset);
                along[index] = distance;
                across[index] = offset;
                // V en "anchos de río": así la textura se repite igual en
                // tramos rectos y en curvas, sin estirarse.
                uvs[index] = new Vector2(x / (float)(cols - 1), distance / Mathf.Max(0.01f, width));
            }
        }

        int t = 0;
        for (int z = 0; z < rws - 1; z++)
        {
            for (int x = 0; x < cols - 1; x++)
            {
                int bottomLeft = z * cols + x;
                int topLeft = (z + 1) * cols + x;

                triangles[t++] = bottomLeft;
                triangles[t++] = topLeft;
                triangles[t++] = bottomLeft + 1;

                triangles[t++] = bottomLeft + 1;
                triangles[t++] = topLeft;
                triangles[t++] = topLeft + 1;
            }
        }

        vertices = (Vector3[])baseVertices.Clone();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    // ---------------------------------------------------------------
    // ¿Está este punto dentro del agua? Lo usa el jugador para nadar.
    // ---------------------------------------------------------------
    // No hacen falta colliders: el eje del río ya está en "path" (ordenado de
    // sur a norte), así que basta con ver a qué distancia del eje queda el
    // punto a esa altura del río.
    public static RiverWater Active { get; private set; }
    public float SurfaceY => transform.position.y;
    public float HalfWidth => width * 0.5f;

    private void OnEnable()
    {
        if (Application.isPlaying) Active = this;
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    public bool Contains(Vector3 world, float margin = 0f)
    {
        Vector3 local = world - transform.position;
        float half = width * 0.5f + margin;

        if (path == null || path.Length < 2)
            return Mathf.Abs(local.x) < half && Mathf.Abs(local.z) < length * 0.5f;

        if (local.z < path[0].z || local.z > path[path.Length - 1].z) return false;

        // Búsqueda binaria del tramo del eje que corresponde a esta Z.
        int lo = 0, hi = path.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (path[mid].z <= local.z) lo = mid; else hi = mid;
        }
        float span = path[hi].z - path[lo].z;
        float t = span > 0.0001f ? (local.z - path[lo].z) / span : 0f;
        float centerX = Mathf.Lerp(path[lo].x, path[hi].x, t);

        // En las curvas el ancho medido en X es un poco mayor que el real.
        float slope = span > 0.0001f ? (path[hi].x - path[lo].x) / span : 0f;
        return Mathf.Abs(local.x - centerX) < half * Mathf.Sqrt(1f + slope * slope);
    }

    private int frameSkip;

    private void Update()
    {
        if (mesh == null) return;

        // El agua se actualiza 1 de cada 2 frames: a simple vista se mueve
        // igual y le quita la mitad del trabajo a la GPU.
        frameSkip++;
        if (frameSkip % 2 != 0) return;

        float time = Time.time;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 basePoint = baseVertices[i];

            // Ola principal viajando a lo largo del cauce (eje Z) y una
            // segunda ola cruzada, para que no se vea un patrón repetido.
            float main = Mathf.Sin((along[i] / waveLength) + time * flowSpeed) * waveHeight;
            float cross = Mathf.Sin((across[i] / (waveLength * 0.4f)) + time * flowSpeed * 0.6f) * crossWaveHeight;

            vertices[i] = new Vector3(basePoint.x, basePoint.y + main + cross, basePoint.z);
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
    }
}
