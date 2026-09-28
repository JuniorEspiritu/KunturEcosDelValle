using UnityEditor;
using UnityEngine;

// v55: heladerías. Una frente a la casa de Kuntur (en el Jr. Arequipa) y otra
// arriba, en el mirador de la subida. Con E, Kuntur se come un helado y se le
// llena la energía (ver IceCreamShop).
public static partial class KunturSceneBuilder
{
    // Cruzando el Jr. Arequipa desde la casa de Kuntur, mirando a la calle (oeste).
    private static readonly Vector3 TownIceCreamSpot = new Vector3(ArequipaX + CrossStreetHalfRoad + SidewalkWidth + 0.4f + 2.3f, 0f, -111f);

    private static void BuildTownIceCreamShop(Transform parent)
    {
        BuildIceCreamShop(parent, "Heladeria_Arequipa", TownIceCreamSpot, -1f, "HELADERÍA", "Don Pepe");
    }

    // center = centro del local a nivel del suelo; facing = +1 si la fachada
    // mira al +X, -1 si mira al -X (siempre hacia su calle).
    private static GameObject BuildIceCreamShop(Transform parent, string name, Vector3 center, float facing, string title, string owner)
    {
        const float depth = 4.6f;   // en X
        const float width = 6f;     // en Z
        const float h = 3.2f;

        Occupy(center + new Vector3(facing * 0.8f, 0f, 0f), depth + 1.8f, width + 1f);

        GameObject shop = new GameObject(name);
        shop.transform.SetParent(parent);
        shop.transform.position = center;
        Transform t = shop.transform;

        Color wall = HexColor("#f7c9da");
        Color trim = HexColor("#c2567f");
        Color cream = HexColor("#fff6ec");
        float front = center.x + facing * depth / 2f;
        float y = center.y;

        PrimitiveObject(t, "Local", PrimitiveType.Cube, new Vector3(center.x, y + h / 2f, center.z), new Vector3(depth, h, width), wall);
        PrimitiveObject(t, "Zocalo", PrimitiveType.Cube, new Vector3(center.x, y + 0.22f, center.z), new Vector3(depth + 0.1f, 0.45f, width + 0.1f), trim);
        PrimitiveObject(t, "Techo", PrimitiveType.Cube, new Vector3(center.x, y + h + 0.12f, center.z), new Vector3(depth + 0.4f, 0.25f, width + 0.4f), cream);
        // Franja de galleta en el borde del techo.
        PrimitiveObject(t, "Borde_Galleta", PrimitiveType.Cube, new Vector3(center.x, y + h - 0.1f, center.z), new Vector3(depth + 0.2f, 0.22f, width + 0.2f), HexColor("#d9a05b"));

        // Vitrina, puerta y mostrador con los baldes de helado.
        PrimitiveObject(t, "Vitrina", PrimitiveType.Cube, new Vector3(front + facing * 0.03f, y + 1.8f, center.z - 0.7f), new Vector3(0.06f, 1.3f, 3.4f), HexColor("#bfe6f2"));
        PrimitiveObject(t, "Puerta", PrimitiveType.Cube, new Vector3(front + facing * 0.04f, y + 1.1f, center.z + 2.1f), new Vector3(0.08f, 2.2f, 1.1f), HexColor("#8a5a44"));
        PrimitiveObject(t, "Perilla", PrimitiveType.Sphere, new Vector3(front + facing * 0.1f, y + 1.1f, center.z + 1.75f), Vector3.one * 0.09f, HexColor("#f2c230"));

        float counterX = front + facing * 0.5f;
        PrimitiveObject(t, "Mostrador", PrimitiveType.Cube, new Vector3(counterX, y + 0.5f, center.z - 0.7f), new Vector3(0.9f, 1f, 3.2f), cream);
        PrimitiveObject(t, "Mostrador_Franja", PrimitiveType.Cube, new Vector3(counterX + facing * 0.46f, y + 0.55f, center.z - 0.7f), new Vector3(0.03f, 0.3f, 3.2f), trim);
        PrimitiveObject(t, "Vidrio", PrimitiveType.Cube, new Vector3(counterX, y + 1.12f, center.z - 0.7f), new Vector3(0.8f, 0.24f, 3.0f), HexColor("#d9f4ff"));
        Color[] flavors = { HexColor("#ff8fb1"), HexColor("#9be3c2"), HexColor("#6b3e26"), HexColor("#ffe27a"), HexColor("#c9a0ff"), HexColor("#ff6b5a") };
        for (int i = 0; i < flavors.Length; i++)
        {
            float z = center.z - 2.0f + i * 0.52f;
            PrimitiveObject(t, "Balde_Helado", PrimitiveType.Sphere, new Vector3(counterX, y + 1.02f, z), new Vector3(0.36f, 0.2f, 0.36f), flavors[i]);
        }

        // Toldo a rayas rosadas y blancas sobre la vitrina.
        for (int i = 0; i < 6; i++)
        {
            float z = center.z - width / 2f + 0.5f + i * 1f;
            PrimitiveObject(t, "Toldo", PrimitiveType.Cube, new Vector3(front + facing * 0.62f, y + 2.72f, z),
                new Vector3(1.35f, 0.06f, 1.0f), i % 2 == 0 ? HexColor("#ff5c93") : Color.white,
                Quaternion.Euler(0f, 0f, -facing * 20f));
        }

        // Letrero con el nombre.
        Vector3 board = new Vector3(front + facing * 0.02f, y + h + 0.75f, center.z);
        PrimitiveObject(t, "Letrero", PrimitiveType.Cube, board, new Vector3(0.14f, 0.95f, 4.4f), HexColor("#6b2a4a"));
        TextMeshProLabel(t, "Letrero_Texto", title, 4.6f, cream, board + new Vector3(facing * 0.09f, 0.1f, 0f), facing, new Vector2(4.2f, 0.7f));
        TextMeshProLabel(t, "Letrero_Sub", "helados artesanales · " + owner, 2.1f, HexColor("#ffc2da"), board + new Vector3(facing * 0.09f, -0.3f, 0f), facing, new Vector2(4.2f, 0.35f));

        // Barquillo gigante en el techo (el cono con la punta para abajo).
        Vector3 cone = new Vector3(center.x - facing * 0.6f, y + h + 1.75f, center.z - 1.6f);
        MeshObject(t, "Barquillo", LowPolyMeshFactory.Cone(10), cone, new Vector3(0.5f, 1.45f, 0.5f), HexColor("#d9a05b"), Quaternion.Euler(180f, 0f, 0f));
        PrimitiveObject(t, "Bola_Fresa", PrimitiveType.Sphere, cone + Vector3.up * 0.3f, Vector3.one * 1.1f, HexColor("#ff8fb1"));
        PrimitiveObject(t, "Bola_Menta", PrimitiveType.Sphere, cone + Vector3.up * 0.95f, Vector3.one * 0.92f, HexColor("#9be3c2"));
        PrimitiveObject(t, "Bola_Chocolate", PrimitiveType.Sphere, cone + Vector3.up * 1.48f, Vector3.one * 0.74f, HexColor("#6b3e26"));
        PrimitiveObject(t, "Cereza", PrimitiveType.Sphere, cone + Vector3.up * 1.92f, Vector3.one * 0.24f, HexColor("#e0262f"));

        // Mesita con sombrilla al costado.
        Vector3 table = new Vector3(front + facing * 1.6f, y, center.z + width / 2f + 1.3f);
        PrimitiveObject(t, "Mesa_Pata", PrimitiveType.Cylinder, table + Vector3.up * 0.37f, new Vector3(0.1f, 0.37f, 0.1f), HexColor("#7a7a7a"));
        PrimitiveObject(t, "Mesa", PrimitiveType.Cylinder, table + Vector3.up * 0.76f, new Vector3(0.9f, 0.03f, 0.9f), Color.white);
        PrimitiveObject(t, "Sombrilla_Palo", PrimitiveType.Cylinder, table + Vector3.up * 1.3f, new Vector3(0.05f, 1.3f, 0.05f), HexColor("#d0d0d0"));
        MeshObject(t, "Sombrilla", LowPolyMeshFactory.Cone(8), table + Vector3.up * 2.2f, new Vector3(1.3f, 0.5f, 1.3f), HexColor("#ff5c93"));
        for (int i = 0; i < 2; i++)
        {
            float dz = i == 0 ? -0.8f : 0.8f;
            PrimitiveObject(t, "Silla", PrimitiveType.Cube, table + new Vector3(0f, 0.45f, dz), new Vector3(0.45f, 0.08f, 0.45f), HexColor("#9be3c2"));
            PrimitiveObject(t, "Silla_Respaldo", PrimitiveType.Cube, table + new Vector3(0f, 0.72f, dz + (dz > 0 ? 0.2f : -0.2f)), new Vector3(0.45f, 0.5f, 0.06f), HexColor("#9be3c2"));
        }

        // El local choca (paredes y mostrador).
        BoxCollider body = shop.AddComponent<BoxCollider>();
        body.center = new Vector3(0f, h / 2f, 0f);
        body.size = new Vector3(depth, h, width);
        BoxCollider counter = shop.AddComponent<BoxCollider>();
        counter.center = new Vector3(counterX - center.x, 0.55f, -0.7f);
        counter.size = new Vector3(0.9f, 1.1f, 3.2f);

        // Donde se pide el helado: "E  Comer un helado".
        GameObject sale = new GameObject("Venta_Helados");
        sale.transform.SetParent(t);
        sale.transform.position = new Vector3(counterX + facing * 1.2f, y + 1f, center.z - 0.7f);
        sale.layer = interactableLayerForGivers;
        BoxCollider trigger = sale.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1.8f, 2f, 3.4f);
        sale.AddComponent<IceCreamShop>();

        AddMapIcon(shop.transform, center, HexColor("#ff5c93"), 4.5f);
        SetStaticRecursive(shop, true);
        sale.isStatic = false;
        return shop;
    }

    private static void TextMeshProLabel(Transform parent, string name, string text, float size, Color color, Vector3 position, float facing, Vector2 box)
    {
        TMPro.TextMeshPro label = MakeLogoText(parent, name, text, size, color, position,
            Quaternion.Euler(0f, facing > 0f ? -90f : 90f, 0f), new Vector2(0.5f, 0.5f), TMPro.TextAlignmentOptions.Center, 0.5f);
        label.rectTransform.sizeDelta = box;
        label.enableAutoSizing = true;
        label.fontSizeMin = 0.5f;
        label.fontSizeMax = size;
    }
}
