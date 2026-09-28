// Tecla asignada a cada tipo de interacción, tal como se ve en el HUD de
// exploración del mockup: "E" para la acción principal (basura, hablar con
// NPCs) y "F" para la acción secundaria (muestras de agua). Con esto un mismo
// InteractionSystem puede mostrar el prompt correcto sin que cada objeto sepa
// nada del sistema de input.
public enum InteractKey
{
    Primary,   // tecla E
    Secondary  // tecla F
}

// Contrato para cualquier objeto del mundo con el que Kuntur pueda interactuar
// (basura, puntos de muestreo de agua, NPCs de diálogo). Mantiene
// InteractionSystem desacoplado de la lógica específica de cada objeto.
public interface IInteractable
{
    void Interact();
    string GetPrompt();
    InteractKey GetInteractKey();
}
