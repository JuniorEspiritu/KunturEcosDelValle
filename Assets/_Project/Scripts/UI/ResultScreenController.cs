using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Pantalla de resultado: se abre cuando se pierde una misión (se acabó el
// tiempo). Muestra cómo quedó el valle, cuántas misiones se cumplieron y las
// estrellas ganadas, y deja reintentar el día o volver al menú.
public class ResultScreenController : MonoBehaviour
{
    [SerializeField] private Image[] stars = new Image[3];
    [SerializeField] private Sprite starFilled;
    [SerializeField] private Sprite starEmpty;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text healthStatText;
    [SerializeField] private TMP_Text neighborsStatText; // misiones cumplidas
    [SerializeField] private TMP_Text pointsStatText;    // estrellas

    [SerializeField] private string mainMenuScene = "MenuPrincipal";

    private bool victory;
    private string title = "¡SE ACABÓ EL TIEMPO!";
    private string subtitle = "La basura se fue al río...";
    private int ratingStars;
    private int missionsDone;
    private int totalStars;

    public void Configure(bool isVictory, string newTitle, string newSubtitle, int rating, int missions, int starsTotal)
    {
        victory = isVictory;
        title = newTitle;
        subtitle = newSubtitle;
        ratingStars = rating;
        missionsDone = missions;
        totalStars = starsTotal;
    }

    private void OnEnable()
    {
        float health = ValleyHealthManager.Instance != null ? ValleyHealthManager.Instance.CurrentHealth : 0f;
        if (healthStatText != null) healthStatText.text = $"{Mathf.RoundToInt(health)}%";
        if (neighborsStatText != null) neighborsStatText.text = missionsDone.ToString();
        if (pointsStatText != null) pointsStatText.text = totalStars.ToString();

        for (int i = 0; i < stars.Length; i++)
            if (stars[i] != null) stars[i].sprite = i < ratingStars ? starFilled : starEmpty;

        if (titleText != null)
        {
            titleText.text = title;
            titleText.color = victory ? UIPalette.Green : new Color(1f, 0.45f, 0.35f);
        }
        if (subtitleText != null) subtitleText.text = subtitle;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.DuckMusic();
            if (victory) AudioManager.Instance.PlayVictory();
            else AudioManager.Instance.PlayDefeat();
        }
    }

    // Vuelve a cargar la escena: arranca el día desde lo último guardado.
    public void Retry()
    {
        Time.timeScale = 1f;
        string scene = SceneManager.GetActiveScene().name;
        if (ScreenFader.Instance != null) ScreenFader.Instance.FadeAndLoadScene(scene);
        else SceneManager.LoadScene(scene);
    }

    public void GoToMenu()
    {
        Time.timeScale = 1f;
        if (ScreenFader.Instance != null) ScreenFader.Instance.FadeAndLoadScene(mainMenuScene);
        else SceneManager.LoadScene(mainMenuScene);
    }

    // Se mantienen por compatibilidad con código viejo.
    public void SetVictory(bool value) => victory = value;
    public void SetNeighborsConvinced(int value) { }
    public void SetMissionStars(int starsEarned, int max) { totalStars = starsEarned; }
    public int CalculateStars(float health, int neighbors) => Mathf.Clamp(1 + (health >= 60f ? 1 : 0) + (neighbors >= 1 ? 1 : 0), 1, 3);
}
