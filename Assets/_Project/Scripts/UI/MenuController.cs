using UnityEngine;
using UnityEngine.SceneManagement;

// Navegación entre escenas para Menú principal (01), Elegir ciudad (02) y Fin
// de partida (07) - son pantallas de navegación pura, sin mecánica de juego,
// así que no pasan por GameManager.SetState. Cada botón del mockup llama a
// uno de estos métodos desde su OnClick() en el Inspector.
//
// Ya no carga la escena de golpe: si hay un ScreenFader en la escena, primero
// funde a negro y recién ahí carga. Si no lo hay (una escena vieja, por
// ejemplo), carga directo como antes - así nunca se queda trabado.
public class MenuController : MonoBehaviour
{
    [SerializeField] private string citySelectSceneName = "SeleccionCiudad";
    [SerializeField] private string explorationSceneName = "Huancayo_Exploracion";
    [SerializeField] private string mainMenuSceneName = "MenuPrincipal";
    [SerializeField] private string introSceneName = "Intro";

    public void PlayGame() => Go(citySelectSceneName);
    public void StartHuancayo() => Go(explorationSceneName);
    public void GoToMainMenu() => Go(mainMenuSceneName);

    // "CONTINUAR": retoma el día guardado (si no hay nada guardado, empieza).
    public void ContinueGame() => Go(explorationSceneName);

    // "NUEVA PARTIDA": borra lo guardado y arranca en el día 1.
    public void NewGame()
    {
        SaveSystem.Clear();
        // v55b: primero la intro cinemática (y al entrar, el tutorial).
        Go(Application.CanStreamedLevelBeLoaded(introSceneName) ? introSceneName : explorationSceneName);
    }

    public void QuitGame()
    {
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.FadeAndQuit();
            return;
        }

        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void Go(string sceneName)
    {
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.FadeAndLoadScene(sceneName);
            return;
        }
        SceneManager.LoadScene(sceneName);
    }
}
