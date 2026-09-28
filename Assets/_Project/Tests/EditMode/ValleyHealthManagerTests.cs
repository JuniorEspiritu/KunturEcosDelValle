using NUnit.Framework;
using UnityEngine;

// Prioriza testear los límites de la Salud del Valle: si el clamp 0-100 se
// rompe, el mensaje del ODS se pierde sin que se note jugando una sola vez
// (una barra que se pasa de 100% o baja de 0% deja de comunicar nada).
public class ValleyHealthManagerTests
{
    private ValleyHealthManager CreateManager()
    {
        var go = new GameObject("TestValleyHealthManager");
        return go.AddComponent<ValleyHealthManager>();
    }

    [Test]
    public void ChangeHealth_NeverExceeds100()
    {
        ValleyHealthManager manager = CreateManager();

        manager.ChangeHealth(999f);

        Assert.AreEqual(100f, manager.CurrentHealth);
        Object.DestroyImmediate(manager.gameObject);
    }

    [Test]
    public void ChangeHealth_NeverGoesBelowZero()
    {
        ValleyHealthManager manager = CreateManager();

        manager.ChangeHealth(-999f);

        Assert.AreEqual(0f, manager.CurrentHealth);
        Object.DestroyImmediate(manager.gameObject);
    }

    [Test]
    public void HealthPercent01_MatchesCurrentHealthDividedBy100()
    {
        ValleyHealthManager manager = CreateManager();

        manager.ChangeHealth(-10f);

        Assert.AreEqual(manager.CurrentHealth / 100f, manager.HealthPercent01, 0.001f);
        Object.DestroyImmediate(manager.gameObject);
    }
}
