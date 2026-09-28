using UnityEngine;

// Paleta EXACTA extraída del mockup HTML/Figma de "Kuntur: Ecos del Valle"
// (build_valle.py). Centraliza los colores aquí en vez de repetir hexadecimales
// sueltos en cada script - si el diseño cambia, se actualiza en un solo lugar.
public static class UIPalette
{
    public static readonly Color Amber     = HexToColor("#f6b93b"); // botones principales, salud media
    public static readonly Color AmberEdge = HexToColor("#c97f16"); // borde de botones ámbar
    public static readonly Color AmberDrop = HexToColor("#a8621a"); // "sombra 3D" bajo botones ámbar
    public static readonly Color Gold      = HexToColor("#f5c400"); // título KUNTUR, puntaje, estrellas
    public static readonly Color Green     = HexToColor("#7be3a0"); // salud alta, opción correcta, check
    public static readonly Color Blue      = HexToColor("#7dd3fc"); // agua / ODS 6, tecla F, pines de mapa
    public static readonly Color Cream     = HexToColor("#fff2df"); // texto principal claro (títulos)
    public static readonly Color CreamSoft = HexToColor("#fdf3e2"); // fondo de globo de diálogo
    public static readonly Color TextMuted = HexToColor("#cbd5df"); // texto secundario sobre fondo oscuro
    public static readonly Color OrangeHot = HexToColor("#ffd7a3"); // countdown / urgencia del clímax

    public static readonly Color PanelDark  = HexToColor("#0E1610", 0.78f); // fondo de paneles HUD (rgba(14,22,16,.78))
    public static readonly Color BorderSoft = HexToColor("#FFFFFF", 0.20f); // borde sutil de paneles

    public static Color HexToColor(string hex, float alpha = 1f)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        color.a = alpha;
        return color;
    }
}
