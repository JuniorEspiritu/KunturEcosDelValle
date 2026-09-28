using System;
using System.Collections.Generic;
using UnityEngine;

// Una misión de la checklist (ej. "Tomar 4 muestras de agua"). targetCount>1
// permite representar progreso tipo "(2/4)" como en el mockup, no solo
// completo/incompleto.
[Serializable]
public class Objective
{
    public string id;
    public string description;
    public int targetCount = 1;
    public bool isSecondary; // no bloquea OnAllMainObjectivesCompleted si sigue incompleta

    [HideInInspector] public int currentCount;
    public bool IsComplete => currentCount >= targetCount;
}

// Lleva el estado de todas las misiones de la fase actual (panel "MISIONES"
// del HUD). Las mecánicas (TrashPickup, WaterSampleKit, DialogueNPC) reportan
// progreso por id - este script no sabe qué acción concreta generó ese
// progreso, solo lo contabiliza.
public class ObjectiveSystem : MonoBehaviour
{
    public static ObjectiveSystem Instance { get; private set; }

    [SerializeField] private List<Objective> objectives = new();

    public event Action<Objective> OnObjectiveProgress;
    public event Action<Objective> OnObjectiveCompleted;
    public event Action OnAllMainObjectivesCompleted;
    public event Action OnObjectivesChanged; // la lista cambió entera (nuevo nivel)

    public IReadOnlyList<Objective> Objectives => objectives;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Permite cargar la lista de misiones por código (ej. al iniciar un nivel
    // distinto) además de configurarlas a mano en el Inspector.
    public void SetObjectives(List<Objective> newObjectives)
    {
        objectives = newObjectives;
        OnObjectivesChanged?.Invoke();
    }

    public Objective Find(string id) => objectives.Find(o => o.id == id);

    public void ReportProgress(string id, int amount = 1)
    {
        Objective objective = objectives.Find(o => o.id == id);
        if (objective == null || objective.IsComplete) return;

        objective.currentCount = Mathf.Min(objective.currentCount + amount, objective.targetCount);
        OnObjectiveProgress?.Invoke(objective);

        if (objective.IsComplete)
        {
            OnObjectiveCompleted?.Invoke(objective);

            bool allMainDone = objectives.TrueForAll(o => o.isSecondary || o.IsComplete);
            if (allMainDone) OnAllMainObjectivesCompleted?.Invoke();
        }
    }
}
