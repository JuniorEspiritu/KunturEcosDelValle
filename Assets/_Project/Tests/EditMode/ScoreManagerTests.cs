using NUnit.Framework;
using UnityEngine;

// Edit Mode tests: corren sin cargar escena ni Play Mode, ideales para lógica
// pura como la acumulación de puntaje.
public class ScoreManagerTests
{
    private ScoreManager CreateManager()
    {
        var go = new GameObject("TestScoreManager");
        return go.AddComponent<ScoreManager>();
    }

    [Test]
    public void AddScore_AccumulatesTotalCorrectly()
    {
        ScoreManager manager = CreateManager();

        manager.AddScore(10);
        manager.AddScore(25);

        Assert.AreEqual(35, manager.CurrentScore);
        Object.DestroyImmediate(manager.gameObject);
    }

    [Test]
    public void AddScore_ZeroAmount_DoesNotInvokeEvent()
    {
        ScoreManager manager = CreateManager();
        bool invoked = false;
        manager.OnScoreAdded += (amount, total, color) => invoked = true;

        manager.AddScore(0);

        Assert.IsFalse(invoked, "Sumar 0 puntos no debería disparar el popup de puntaje.");
        Object.DestroyImmediate(manager.gameObject);
    }

    [Test]
    public void ResetScore_SetsScoreBackToZero()
    {
        ScoreManager manager = CreateManager();
        manager.AddScore(50);

        manager.ResetScore();

        Assert.AreEqual(0, manager.CurrentScore);
        Object.DestroyImmediate(manager.gameObject);
    }
}
