using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ObjectiveSystemTests
{
    private ObjectiveSystem CreateSystem(List<Objective> objectives)
    {
        var go = new GameObject("TestObjectiveSystem");
        var system = go.AddComponent<ObjectiveSystem>();
        system.SetObjectives(objectives);
        return system;
    }

    [Test]
    public void ReportProgress_CompletesObjectiveOnlyAtTargetCount()
    {
        var objective = new Objective { id = "muestras_agua", targetCount = 4 };
        ObjectiveSystem system = CreateSystem(new List<Objective> { objective });

        system.ReportProgress("muestras_agua", 1);
        system.ReportProgress("muestras_agua", 1);
        Assert.IsFalse(objective.IsComplete, "Con 2/4 muestras la misión todavía no debería estar completa.");

        system.ReportProgress("muestras_agua", 2);
        Assert.IsTrue(objective.IsComplete);

        Object.DestroyImmediate(system.gameObject);
    }

    [Test]
    public void ReportProgress_UnknownId_DoesNothing()
    {
        var objective = new Objective { id = "muestras_agua", targetCount = 4 };
        ObjectiveSystem system = CreateSystem(new List<Objective> { objective });

        system.ReportProgress("id_que_no_existe", 1);

        Assert.AreEqual(0, objective.currentCount);
        Object.DestroyImmediate(system.gameObject);
    }

    [Test]
    public void OnAllMainObjectivesCompleted_IgnoresIncompleteSecondaryObjectives()
    {
        var main = new Objective { id = "principal", targetCount = 1 };
        var secondary = new Objective { id = "secundaria", targetCount = 1, isSecondary = true };
        ObjectiveSystem system = CreateSystem(new List<Objective> { main, secondary });

        bool allMainCompleted = false;
        system.OnAllMainObjectivesCompleted += () => allMainCompleted = true;

        system.ReportProgress("principal", 1);

        Assert.IsTrue(allMainCompleted, "La misión secundaria incompleta no debería bloquear el evento de misiones principales.");
        Object.DestroyImmediate(system.gameObject);
    }
}
