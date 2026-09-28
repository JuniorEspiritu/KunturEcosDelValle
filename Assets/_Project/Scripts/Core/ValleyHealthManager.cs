using UnityEngine;
using System;

// Refleja en un solo número (0-100) el estado del valle: sube cuando el
// jugador recoge basura, toma muestras de agua o convence a un vecino; baja
// si elige una respuesta de diálogo equivocada. Es la mecánica que conecta
// cada acción concreta del jugador con el mensaje del ODS 6 + 11 - por eso
// vive separada de ScoreManager, aunque ambas suban juntas casi siempre.
public class ValleyHealthManager : MonoBehaviour
{
    public static ValleyHealthManager Instance { get; private set; }

    [SerializeField] private float startingHealth = 50f; // arranca en un punto medio, no en 0 ni 100
    [SerializeField] private float currentHealth;

    public event Action<float> OnHealthChanged;

    public float CurrentHealth => currentHealth;
    public float HealthPercent01 => currentHealth / 100f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentHealth = startingHealth;
    }

    private void Start()
    {
        // Dispara el evento una vez al iniciar para que la barra de UI se
        // pinte con el valor inicial sin esperar el primer cambio.
        OnHealthChanged?.Invoke(currentHealth);
    }

    // Al cargar una partida guardada: el valle queda como se dejó ayer.
    public void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, 100f);
        OnHealthChanged?.Invoke(currentHealth);
    }

    public void ChangeHealth(float delta)
    {
        currentHealth = Mathf.Clamp(currentHealth + delta, 0f, 100f);
        OnHealthChanged?.Invoke(currentHealth);
    }
}
