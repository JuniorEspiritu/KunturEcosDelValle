using System;
using System.Collections.Generic;
using UnityEngine;

// Tipos de residuo que Kuntur puede recoger. Separarlos no es un detalle
// decorativo: el ODS 12/11 se trata justamente de CLASIFICAR, y el inventario
// le muestra al jugador qué está sacando del valle, no solo un número.
public enum TrashType
{
    Botella,
    Lata,
    Papel,
}

// Mochila de Kuntur: cuenta cuántos residuos de cada tipo lleva recogidos y
// avisa cuando ya no queda basura en el valle (condición de victoria).
public class TrashInventory : MonoBehaviour
{
    public static TrashInventory Instance { get; private set; }

    private readonly Dictionary<TrashType, int> counts = new Dictionary<TrashType, int>();

    public event Action OnInventoryChanged;
    public event Action OnAllTrashCollected;

    public int TotalInWorld { get; private set; }
    public int TotalCollected { get; private set; }
    public int Remaining => Mathf.Max(0, TotalInWorld - TotalCollected);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        foreach (TrashType type in Enum.GetValues(typeof(TrashType)))
            counts[type] = 0;
    }

    // Cada residuo del mundo se registra solo al arrancar la escena, así el
    // total sale del nivel real y no de un número escrito a mano.
    public void RegisterTrash() => TotalInWorld++;

    public int GetCount(TrashType type) => counts.TryGetValue(type, out int value) ? value : 0;

    public void Collect(TrashType type)
    {
        counts[type] = GetCount(type) + 1;
        TotalCollected++;

        OnInventoryChanged?.Invoke();

        if (TotalCollected >= TotalInWorld && TotalInWorld > 0)
            OnAllTrashCollected?.Invoke();
    }
}
