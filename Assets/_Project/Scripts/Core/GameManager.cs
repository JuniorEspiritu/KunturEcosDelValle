using UnityEngine;
using System;

// Fases del juego, una por cada pantalla del mockup que representa un estado
// distinto (no cuentes Menú/Elegir ciudad como "fases de juego": esas son
// simple navegación de escena, ver MenuController).
public enum GameState
{
    Exploracion,
    Dialogo,
    Climax,
    Resultado
}

// Único punto para el estado global de la partida en curso. No maneja
// puntaje ni salud del valle directamente (eso vive en ScoreManager y
// ValleyHealthManager) - solo coordina en qué fase está el juego, para que
// UI y mecánicas puedan reaccionar sin acoplarse entre sí.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameState currentState = GameState.Exploracion;

    public GameState CurrentState => currentState;
    public event Action<GameState> OnStateChanged;

    private void Awake()
    {
        // Uno por escena, y el de la escena nueva manda.
        //
        // Antes era DontDestroyOnLoad, y eso rompía "Reintentar" (y volver al
        // menú y luego a Continuar): el GameManager comparte objeto con TODOS
        // los managers (misiones, días, puntaje...), así que el objeto viejo
        // sobrevivía a la recarga y el nuevo se autodestruía. El juego seguía
        // con los managers de la partida perdida: salía "DÍA 1", la misión
        // vieja a medias y no arrancaba ninguna nueva. Nada necesita que este
        // objeto pase de una escena a otra (el progreso vive en SaveSystem).
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetState(GameState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        OnStateChanged?.Invoke(newState);
    }
}
