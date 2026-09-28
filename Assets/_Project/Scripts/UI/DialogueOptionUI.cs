using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// Una de las 3 filas de opción en el panel de diálogo. El color/resaltado
// (opción correcta con glow verde vs. opciones "apagadas") lo decide
// DialogueUI cuando llega la respuesta del NPC - este script solo conecta el
// botón con el callback que le pasen.
public class DialogueOptionUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private TMP_Text optionText;

    public void Setup(int number, string text, Action onChosen)
    {
        numberText.text = number.ToString();
        optionText.text = text;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onChosen());
        SetInteractable(true);
    }

    public void SetInteractable(bool value) => button.interactable = value;
}
