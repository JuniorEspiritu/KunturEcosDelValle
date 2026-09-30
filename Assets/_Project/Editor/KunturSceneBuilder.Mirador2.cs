using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

// v58: el MIRADOR DEL CERRO, atrás de la casa de Kuntur (suroeste del
// pueblo), 50 m de alto para ver la ciudad entera. Desde el final del
// Jr. Mantaro (circunvalación sur, junto a la casa) sale un camino de
// tierra, como los de chacra, que sube un cerro dando vueltas en curvas hasta
// una explanada arriba. Desde ahí se ve toda la ciudad: el pueblo, el río y
// la cordillera detrás. Se puede subir a pie o con el tuk tuk.
//
// Es a propósito simple: un cerro (un cono con la punta plana), el camino en
// espiral cortado en la ladera, barandas, dos bancas y los letreros.
public static partial class KunturSceneBuilder
{
    private static readonly Vector3 NorthHillCenter = new Vector3(-160f, 0f, -138f);
    private const float NorthHillRadius = 55f;     // la falda
    private const float NorthHillTop = 11f;        // radio de la explanada de arriba
    private const float NorthHillHeight = 50f;
    private const float NorthHillTurns = 2f;    // vueltas del camino
    private const float NorthRoadHalf = 3f;        // camino de 6 m (entra el tuk tuk)

    private static float NorthHillCone(float r)
    {
        if (r <= NorthHillTop) return NorthHillHeight;
        if (r >= NorthHillRadius) return 0f;
        float t = (NorthHillRadius - r) / (NorthHillRadius - NorthHillTop);
        // Pie suave: sale del pasto sin un quiebre marcado.
        if (t < 0.12f) t = t * t / 0.24f;
        else t = t - 0.06f;
        return NorthHillHeight * t / 0.94f;
    }

    // Centro del camino: s = 0 abajo (en el pasto), s = 1 arriba.
    private static Vector3 NorthRoadPoint(float s, float startAngle)
    {
        float r = Mathf.Lerp(NorthHillRadius, NorthHillTop + 1.5f, s);
        float a = startAngle - s * NorthHillTurns * Mathf.PI * 2f;
        Vector3 c = NorthHillCenter;
        return new Vector3(c.x + Mathf.Cos(a) * r, NorthHillCone(r), c.z + Mathf.Sin(a) * r);
    }

    private static void BuildNorthMirador(Transform parent)
    {
        GameObject root = new GameObject("Mirador_Cerro");
        root.transform.SetParent(parent);
        Vector3 c = NorthHillCenter;

        // El camino arranca del lado que mira al final de la circunvalación.
        Vector3 ringEnd = new Vector3(CrossStreetFromX, 0f, SouthRingZ);
        Vector3 toRing = ringEnd - c; toRing.y = 0f;
        float startAngle = Mathf.Atan2(toRing.z, toRing.x);

        // Muestras del camino (para el cerro y para la malla del camino).
        const int samples = 420;
        var road = new List<Vector3>(samples + 1);
        for (int i = 0; i <= samples; i++) road.Add(NorthRoadPoint(i / (float)samples, startAngle));

        ClearAroundNorthHill(parent, root);
        BuildNorthHillMesh(root.transform, road);
        BuildNorthRoadMesh(root.transform, road);

        // Tramo recto de tierra: del final de la circunvalación al pie del cerro.
        Vector3 foot = road[0];
        BuildFlatDirtStrip(root.transform, "Camino_Mirador_Entrada", ringEnd + (foot - ringEnd).normalized * 1.5f, foot, NorthRoadHalf);

        BuildNorthMiradorTop(root.transform, road[road.Count - 1]);

        // Letrero de abajo, para saber a dónde lleva el camino.
        Vector3 dir = (foot - ringEnd); dir.y = 0f; dir.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        BuildWoodSign(root.transform, "Letrero_Mirador_Abajo", ringEnd + dir * 7f - side * (NorthRoadHalf + 1.4f), dir, "MIRADOR  ↑", 1.2f);

        AddMapIcon(root.transform, c, HexColor("#8e44ad"), 6f);
        Debug.Log("[Kuntur] Mirador del cerro listo (atrás de la casa de Kuntur).");
    }

    // Lo que ya estaba construido donde ahora va el cerro (árboles, postes,
    // alguna casa suelta) se saca: si no, quedaría enterrado o flotando.
    private static void ClearAroundNorthHill(Transform world, GameObject keep)
    {
        var doomed = new List<GameObject>();
        foreach (Renderer r in world.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r.transform.IsChildOf(keep.transform)) continue;
            Bounds b = r.bounds;
            if (b.size.x > 30f || b.size.z > 30f) continue;   // suelo, montañas, calles largas
            Vector3 p = b.center; p.y = 0f;
            Vector3 cc = NorthHillCenter; cc.y = 0f;
            if (Vector3.Distance(p, cc) > NorthHillRadius + 1f) continue;
            // Se borra el objeto "entero" (el hijo directo de su grupo).
            Transform t = r.transform;
            while (t.parent != null && t.parent != world && t.parent.parent != world) t = t.parent;
            if (!doomed.Contains(t.gameObject)) doomed.Add(t.gameObject);
        }
        foreach (GameObject g in doomed) if (g != null) Object.DestroyImmediate(g);
        if (doomed.Count > 0) Debug.Log($"[Kuntur] Mirador: saqué {doomed.Count} cosas que quedaban dentro del cerro.");
    }

    private static void BuildNorthHillMesh(Transform parent, List<Vector3> road)
    {
        const float step = 1.5f;
        float half = NorthHillRadius + 4f;
        int n = Mathf.CeilToInt(2f * half / step) + 1;
        var verts = new Vector3[n * n];
        var colors = new Color[n * n];
        var uvs = new Vector2[n * n];
        Color grass = HexColor("#6f9e4c"), dry = HexColor("#a8a061"), rock = HexColor("#8b7d6b");
        Vector3 c = NorthHillCenter;

        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                float x = c.x - half + ix * step, z = c.z - half + iz * step;
                float r = new Vector2(x - c.x, z - c.z).magnitude;
                float y = NorthHillCone(r);

                // El camino va CORTADO en la ladera: plano de lado a lado, con
                // un talud hacia arriba y un relleno hacia abajo.
                float best = float.MaxValue; float roadY = 0f;
                foreach (Vector3 p in road)
                {
                    float d = (x - p.x) * (x - p.x) + (z - p.z) * (z - p.z);
                    if (d < best) { best = d; roadY = p.y; }
                }
                float dist = Mathf.Sqrt(best);
                if (dist < NorthRoadHalf + 3.5f && r < NorthHillRadius + 1f)
                {
                    float k = Mathf.SmoothStep(0f, 1f, (dist - NorthRoadHalf - 0.3f) / 3.2f);
                    y = Mathf.Lerp(roadY - 0.04f, y, k);
                }
                if (r > NorthHillRadius + 1f) y = 0f;

                verts[iz * n + ix] = new Vector3(x, y - 0.02f, z);
                float h01 = y / NorthHillHeight;
                colors[iz * n + ix] = Color.Lerp(Color.Lerp(grass, dry, h01), rock, Mathf.Clamp01((h01 - 0.55f) * 0.6f));
                uvs[iz * n + ix] = new Vector2(x, z) / 4f;
            }

        var tris = new List<int>();
        for (int iz = 0; iz < n - 1; iz++)
            for (int ix = 0; ix < n - 1; ix++)
            {
                int a = iz * n + ix, b = a + 1, d = a + n, e = d + 1;
                tris.Add(a); tris.Add(d); tris.Add(b);
                tris.Add(b); tris.Add(d); tris.Add(e);
            }

        Mesh mesh = new Mesh { name = "Cerro_Mirador", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.uv = uvs;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        SaveGeneratedMesh(mesh);

        GameObject go = new GameObject("Cerro_Mirador");
        go.transform.SetParent(parent);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        Material mat = GetWorldTiledMaterial("Sup_Suelo_Valle", null, Color.white, 4f, 0.28f, true);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat != null ? mat : GetMaterial(grass);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.isStatic = true;
    }

    private static void BuildNorthRoadMesh(Transform parent, List<Vector3> road)
    {
        var verts = new List<Vector3>();
        var colors = new List<Color>();
        var tris = new List<int>();
        Color dirt = HexColor("#b09166"), track = HexColor("#9b7d55");
        for (int i = 0; i < road.Count; i++)
        {
            Vector3 p = road[i];
            Vector3 fwd = road[Mathf.Min(i + 1, road.Count - 1)] - road[Mathf.Max(i - 1, 0)];
            fwd.y = 0f; fwd.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            // Cinco columnas: borde, rodada, centro, rodada, borde.
            float[] offs = { -NorthRoadHalf, -1.5f, 0f, 1.5f, NorthRoadHalf };
            foreach (float o in offs)
            {
                verts.Add(p + side * o + Vector3.up * 0.05f);
                colors.Add(Mathf.Abs(Mathf.Abs(o) - 1.5f) < 0.01f ? track : dirt);
            }
        }
        for (int i = 0; i < road.Count - 1; i++)
            for (int j = 0; j < 4; j++)
            {
                int a = i * 5 + j, b = a + 1, d = a + 5, e = d + 1;
                tris.Add(a); tris.Add(d); tris.Add(b);
                tris.Add(b); tris.Add(d); tris.Add(e);
            }
        Mesh mesh = new Mesh { name = "Camino_Mirador_Curvas", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        SaveGeneratedMesh(mesh);

        GameObject go = new GameObject("Camino_Mirador_Curvas");
        go.transform.SetParent(parent);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        Material roadMat = GetWorldTiledMaterial("Sup_Camino_Mirador", null, Color.white, 4f, 0.12f, true);
        go.AddComponent<MeshRenderer>().sharedMaterial = roadMat != null ? roadMat : GetMaterial(dirt);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.isStatic = true;

        // Postes con cuerda por el lado del barranco, cada tanto.
        for (int i = 6; i < road.Count - 4; i += 6)
        {
            Vector3 p = road[i];
            Vector3 outward = p - NorthHillCenter; outward.y = 0f; outward.Normalize();
            Vector3 post = p + outward * (NorthRoadHalf + 0.5f);
            PrimitiveObject(go.transform, "Poste_Camino", PrimitiveType.Cube,
                post + Vector3.up * 0.45f, new Vector3(0.14f, 0.9f, 0.14f), HexColor("#6a4a2e"));
        }
    }

    private static void BuildFlatDirtStrip(Transform parent, string name, Vector3 a, Vector3 b, float halfWidth)
    {
        Vector3 d = b - a; d.y = 0f;
        float len = d.magnitude;
        Vector3 mid = (a + b) * 0.5f;
        GameObject strip = PrimitiveObject(parent, name, PrimitiveType.Cube,
            new Vector3(mid.x, 0.03f, mid.z), new Vector3(halfWidth * 2f, 0.04f, len + 1f), HexColor("#b09166"),
            Quaternion.LookRotation(d.normalized));
        strip.isStatic = true;
    }

    // Arriba: baranda de madera del lado de la ciudad, dos bancas mirando al
    // pueblo y el letrero grande "MIRADOR".
    private static void BuildNorthMiradorTop(Transform parent, Vector3 roadEnd)
    {
        Vector3 c = NorthHillCenter;
        float y = NorthHillHeight;
        Vector3 town = new Vector3(0f, 0f, 0f);
        Vector3 view = town - c; view.y = 0f; view.Normalize();
        Vector3 across = Vector3.Cross(Vector3.up, view);

        // Baranda en arco, del lado que mira a la ciudad.
        float railR = NorthHillTop - 0.6f;
        float baseAngle = Mathf.Atan2(view.z, view.x);
        for (int i = -5; i <= 5; i++)
        {
            float a0 = baseAngle + i * 0.16f, a1 = baseAngle + (i + 1) * 0.16f;
            Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * railR;
            Vector3 p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * railR;
            PrimitiveObject(parent, "Baranda_Poste", PrimitiveType.Cube,
                new Vector3(p0.x, y + 0.5f, p0.z), new Vector3(0.14f, 1f, 0.14f), HexColor("#6a4a2e"));
            if (i < 5)
            {
                Vector3 m = (p0 + p1) * 0.5f;
                Vector3 seg = p1 - p0;
                PrimitiveObject(parent, "Baranda_Tabla", PrimitiveType.Cube,
                    new Vector3(m.x, y + 0.95f, m.z), new Vector3(0.1f, 0.1f, seg.magnitude + 0.1f), HexColor("#7a5a3a"),
                    Quaternion.LookRotation(seg.normalized));
            }
        }
        // Que no se caiga nadie por la baranda.
        GameObject wall = new GameObject("Baranda_Collider");
        wall.transform.SetParent(parent);
        wall.transform.SetPositionAndRotation(c + view * railR + Vector3.up * (y + 0.6f), Quaternion.LookRotation(view));
        wall.AddComponent<BoxCollider>().size = new Vector3(railR * 1.6f, 1.2f, 0.3f);

        // Dos bancas mirando al pueblo.
        foreach (float s in new[] { -2.2f, 2.2f })
        {
            Vector3 p = c + view * (railR - 2.2f) + across * s;
            PlaceProp(parent, "Assets/ModularLowpolyStreetsFree/Prefabs/Other/Bench_1.prefab", "Banca_Mirador_Cerro",
                new Vector3(p.x, y + 0.05f, p.z), Mathf.Atan2(view.x, view.z) * Mathf.Rad2Deg, 0.9f);
        }

        // Letrero grande, al costado de donde llega el camino.
        Vector3 inward = c - roadEnd; inward.y = 0f; inward.Normalize();
        Vector3 signSide = Vector3.Cross(Vector3.up, inward);
        Vector3 signPos = roadEnd + inward * 2.5f + signSide * 4.2f;
        signPos.y = y;
        BuildWoodSign(parent, "Letrero_Mirador_Arriba", signPos, -inward, "MIRADOR", 1.8f);
    }

    // Letrero de madera: dos postes y una tabla con el texto. "facing" es
    // hacia dónde mira la cara escrita.
    private static void BuildWoodSign(Transform parent, string name, Vector3 basePos, Vector3 facing, string text, float scale)
    {
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
        facing.Normalize();
        Vector3 across = Vector3.Cross(Vector3.up, facing);
        GameObject sign = new GameObject(name);
        sign.transform.SetParent(parent);
        sign.transform.position = basePos;

        float w = 2.6f * scale, h = 0.8f * scale, top = 1.4f * scale + h;
        foreach (float s in new[] { -1f, 1f })
            PrimitiveObject(sign.transform, "Poste", PrimitiveType.Cube,
                basePos + across * (s * w * 0.42f) + Vector3.up * (top * 0.5f), new Vector3(0.16f, top, 0.16f), HexColor("#5c3d22"));
        PrimitiveObject(sign.transform, "Tabla", PrimitiveType.Cube,
            basePos + Vector3.up * (top - h * 0.5f), new Vector3(w, h, 0.12f), HexColor("#8a5a2b"),
            Quaternion.LookRotation(facing));

        GameObject label = new GameObject("Texto");
        label.transform.SetParent(sign.transform);
        label.transform.SetPositionAndRotation(basePos + Vector3.up * (top - h * 0.5f) + facing * 0.07f,
            Quaternion.LookRotation(-facing));
        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        if (defaultFont != null) tmp.font = defaultFont;
        tmp.text = text;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = HexColor("#fff3d6");
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1f;
        tmp.fontSizeMax = 12f;
        tmp.rectTransform.sizeDelta = new Vector2(w * 0.9f, h * 0.8f);
        SetStaticRecursive(sign, true);
        label.isStatic = false;
    }
}
