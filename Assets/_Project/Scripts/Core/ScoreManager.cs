using UnityEngine;
using System;

// Puntaje total de la partida. Cada mecánica (basura, agua, diálogo) llama a
// AddScore - este script no sabe nada de ellas, solo acumula y avisa a la UI.
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [SerializeField] private int currentScore = 0;

    // (puntos sumados, puntaje total, color sugerido para el popup flotante).
    // El color es opcional: cada mecánica pasa el color exacto de su categoría
    // (ver UIPalette) para que el popup "+10 pts" / "+25 pts" salga con el
    // mismo color que en el mockup en vez de adivinarlo por el monto.
    public event Action<int, int, Color> OnScoreAdded;

    public int CurrentScore => currentScore;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void AddScore(int amount, Color? popupColor = null)
    {
        if (amount == 0) return;
        currentScore += amount;
        OnScoreAdded?.Invoke(amount, currentScore, popupColor ?? UIPalette.Gold);
    }

    public void ResetScore() => currentScore = 0;
}
