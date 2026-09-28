using UnityEngine;
using System;

// Tiempo límite de la misión en curso. Lo arranca MissionDirector al aceptar
// un encargo (cada nivel con menos tiempo por residuo) y, si llega a cero
// antes de terminar de limpiar, la misión se pierde. Esa presión es la que
// hace tangible la urgencia del ODS 11: la basura no espera a nadie.
public class CountdownManager : MonoBehaviour
{
    [SerializeField] private float durationSeconds = 300f;
    [SerializeField] private bool startAutomatically = false;

    private float remaining;
    private float duration;
    private bool running;

    public event Action<float> OnTick; // segundos restantes
    public event Action OnCountdownFinished;

    public bool IsRunning => running;
    public bool IsActive { get; private set; } // arrancó y no terminó (aunque esté en pausa)
    public float Remaining => remaining;
    public float Duration => duration;
    public float Fraction01 => duration > 0.01f ? Mathf.Clamp01(remaining / duration) : 0f;

    public string FormattedTime
    {
        get
        {
            int total = Mathf.CeilToInt(remaining);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }

    private void Start()
    {
        remaining = durationSeconds;
        duration = durationSeconds;
        if (startAutomatically) StartCountdown();
    }

    private void Update()
    {
        if (!running) return;

        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        OnTick?.Invoke(remaining);

        if (remaining <= 0f)
        {
            running = false;
            IsActive = false;
            OnCountdownFinished?.Invoke();
        }
    }

    public void StartCountdown() { running = true; IsActive = true; }

    public void StartCountdown(float seconds)
    {
        duration = Mathf.Max(1f, seconds);
        remaining = duration;
        running = true;
        IsActive = true;
    }

    public void PauseCountdown() => running = false;

    public void ResumeCountdown()
    {
        if (IsActive && remaining > 0f) running = true;
    }

    public void StopCountdown()
    {
        running = false;
        IsActive = false;
    }
}
