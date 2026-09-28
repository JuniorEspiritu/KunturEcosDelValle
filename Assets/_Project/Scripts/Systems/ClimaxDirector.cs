using UnityEngine;

// Cierra la partida cuando algo sale mal: si el tiempo de una misión se acaba
// antes de limpiar lo acordado, se muestra la pantalla de resultado como
// derrota (con la opción de reintentar el día desde lo último guardado).
//
// Antes también disparaba un "clímax" a las 5 de la tarde; con los días y las
// misiones por nivel eso ya no aplica: el juego sigue día tras día mientras
// Kuntur cumpla a tiempo.
public class ClimaxDirector : MonoBehaviour
{
    [SerializeField] private CountdownManager countdown;
    [SerializeField] private GameObject resultPanel;

    private bool finished;

    private void Awake()
    {
        if (countdown == null) countdown = FindAnyObjectByType<CountdownManager>();
    }

    private void Start()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
        // El GameManager sobrevive entre escenas: al reintentar, podía quedar
        // en "Resultado" y Kuntur no se movería.
        if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Exploracion);
        Time.timeScale = 1f;
    }

    // Lo llama MissionDirector cuando el tiempo de la misión llega a cero.
    public void FailMission(string title, string subtitle, int missionsDone, int totalStars)
    {
        if (finished) return;
        finished = true;

        if (countdown != null) countdown.StopCountdown();
        if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Resultado);

        // Antes del cartel de derrota, Kuntur se pone triste (animación
        // "triste") y la cámara le da la vuelta para que se le vea la cara.
        float delay = 0f;
        if (KunturMixamoAnimator.Instance != null)
        {
            KunturMixamoAnimator.Instance.PlaySad();
            if (KunturCinematicCamera.Instance != null) KunturCinematicCamera.Instance.StartPortrait();
            delay = KunturMixamoAnimator.SadDuration + 0.4f;
        }
        StartCoroutine(ShowResultAfter(delay, title, subtitle, missionsDone, totalStars));
    }

    private System.Collections.IEnumerator ShowResultAfter(float delay, string title, string subtitle, int missionsDone, int totalStars)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (resultPanel != null)
        {
            ResultScreenController result = resultPanel.GetComponent<ResultScreenController>();
            if (result != null) result.Configure(false, title, subtitle, 0, missionsDone, totalStars);
            resultPanel.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Compatibilidad con llamadas viejas.
    public void FinishWithStars(int stars, int maxStars) { }
    public void FinishDay() { }
}
