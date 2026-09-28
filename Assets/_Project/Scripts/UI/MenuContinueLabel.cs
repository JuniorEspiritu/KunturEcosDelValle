using TMPro;
using UnityEngine;

// El primer botón del menú dice "CONTINUAR · DÍA 3" si hay una partida
// guardada, o "JUGAR" si es la primera vez.
public class MenuContinueLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private void OnEnable()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        label.text = SaveSystem.HasSave ? $"CONTINUAR  ·  DÍA {SaveSystem.Load().day}" : "JUGAR";
    }
}
