using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Un bloque de la intro: el texto que se lee sobre negro y la ilustración
// que aparece DESPUÉS de que el texto se va.
[System.Serializable]
public class IntroBloque
{
    public string texto;
    public Sprite imagen;
}

// v55b: intro cinemática de "Kuntur: Ecos del Valle". Se ve al elegir NUEVA
// PARTIDA, antes de cargar el juego. Secuencia exacta:
//   negro → Texto 1 → Imagen 1 → Texto 2 → Imagen 2 → ... → Texto 6 → Imagen 6 → juego
// El texto SIEMPRE va solo sobre negro puro; nunca texto e imagen a la vez.
//
// Funciona solo con corrutinas (no necesita DOTween). Todos los tiempos se
// ajustan desde el Inspector.
public class IntroCinematica : MonoBehaviour
{
    [Header("Bloques (texto + imagen), en orden")]
    [SerializeField] private IntroBloque[] bloques =
    {
        new IntroBloque { texto = "Huancayo fue una ciudad limpia." },
        new IntroBloque { texto = "Poco a poco, la basura llegó." },
        new IntroBloque { texto = "El río se llenó de desechos." },
        new IntroBloque { texto = "Hasta que Kuntur despertó." },
        new IntroBloque { texto = "Kuntur ayudará a limpiar." },
        new IntroBloque { texto = "Es hora de comenzar la aventura." },
    };

    [Header("Referencias de la interfaz")]
    [SerializeField] private CanvasGroup grupoTexto;
    [SerializeField] private TMP_Text texto;
    [SerializeField] private CanvasGroup grupoImagen;
    [SerializeField] private Image imagen;
    [SerializeField] private Button botonSaltar;

    [Header("Texto")]
    [SerializeField] private float tamanoTexto = 64f;
    [SerializeField] private float textoAparece = 0.8f;
    [SerializeField] private float textoSube = 26f;          // px que sube al aparecer
    [SerializeField] private float textoVisible = 2.5f;
    [SerializeField] private float textoSeVa = 0.5f;
    [SerializeField] private float textoEscalaFinal = 1.1f;
    [SerializeField] private float pausaEntre = 0.25f;       // negro puro entre pasos

    [Header("Imagen")]
    [SerializeField] private float imagenAparece = 0.6f;
    [SerializeField] private float imagenVisible = 3.5f;
    [SerializeField] private float imagenSeVa = 0.5f;
    [SerializeField] private float zoomInicial = 1.0f;
    [SerializeField] private float zoomFinal = 1.12f;

    [Header("Audio")]
    [SerializeField] private AudioSource fuenteMusica;       // historia (en bucle)
    [SerializeField] private AudioSource fuenteTransicion;   // transicion (PlayOneShot)
    [SerializeField] private AudioClip sonidoTransicion;
    [SerializeField] private float volumenMusica = 0.8f;
    [SerializeField] private float musicaAparece = 1.5f;
    [SerializeField] private float musicaSeVa = 1f;

    [Header("Escena del juego")]
    [SerializeField] private string escenaJuego = "Exploracion";

    private Vector2 posTexto;
    private bool saliendo;

    private void Start()
    {
        if (texto != null)
        {
            texto.fontSize = tamanoTexto;
            posTexto = texto.rectTransform.anchoredPosition;
        }
        if (grupoTexto != null) grupoTexto.alpha = 0f;
        if (grupoImagen != null) grupoImagen.alpha = 0f;
        if (botonSaltar != null) botonSaltar.onClick.AddListener(Saltar);

        // Al entrar al juego después de la intro, sale el tutorial con Kuntur saludando.
        PlayerPrefs.SetInt(TutorialUI.PendingKey, 1);
        PlayerPrefs.Save();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        StartCoroutine(Secuencia());
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Saltar();
    }

    private IEnumerator Secuencia()
    {
        if (fuenteMusica != null)
        {
            fuenteMusica.loop = true;
            fuenteMusica.volume = 0f;
            fuenteMusica.Play();
            StartCoroutine(FundirVolumen(fuenteMusica, volumenMusica * AudioVolumeSettings.Music, musicaAparece));
        }

        yield return new WaitForSeconds(0.8f); // un respiro de negro al empezar

        foreach (IntroBloque b in bloques)
        {
            // 1) El texto, solo, sobre negro.
            yield return MostrarTexto(b.texto);
            yield return new WaitForSeconds(pausaEntre);

            // 2) Sonido de transición y la imagen con Ken Burns.
            if (b.imagen != null)
            {
                if (fuenteTransicion != null && sonidoTransicion != null)
                    fuenteTransicion.PlayOneShot(sonidoTransicion, AudioVolumeSettings.Sfx);
                yield return MostrarImagen(b.imagen);
                yield return new WaitForSeconds(pausaEntre);
            }
        }

        Saltar();
    }

    private IEnumerator MostrarTexto(string contenido)
    {
        texto.text = contenido;
        RectTransform rt = texto.rectTransform;

        // Aparece subiendo.
        for (float t = 0f; t < textoAparece; t += Time.deltaTime)
        {
            float k = Suave(t / textoAparece);
            grupoTexto.alpha = k;
            rt.anchoredPosition = posTexto + new Vector2(0f, -textoSube * (1f - k));
            rt.localScale = Vector3.one;
            yield return null;
        }
        grupoTexto.alpha = 1f;
        rt.anchoredPosition = posTexto;

        yield return new WaitForSeconds(textoVisible);

        // Se disuelve creciendo un poquito.
        for (float t = 0f; t < textoSeVa; t += Time.deltaTime)
        {
            float k = Suave(t / textoSeVa);
            grupoTexto.alpha = 1f - k;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, textoEscalaFinal, k);
            yield return null;
        }
        grupoTexto.alpha = 0f;
        rt.localScale = Vector3.one;
    }

    private IEnumerator MostrarImagen(Sprite sprite)
    {
        imagen.sprite = sprite;
        RectTransform rt = imagen.rectTransform;
        float total = imagenAparece + imagenVisible + imagenSeVa;

        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            // Ken Burns: zoom lento y continuo durante toda la imagen.
            rt.localScale = Vector3.one * Mathf.Lerp(zoomInicial, zoomFinal, t / total);

            if (t < imagenAparece) grupoImagen.alpha = Suave(t / imagenAparece);
            else if (t > imagenAparece + imagenVisible) grupoImagen.alpha = 1f - Suave((t - imagenAparece - imagenVisible) / imagenSeVa);
            else grupoImagen.alpha = 1f;
            yield return null;
        }
        grupoImagen.alpha = 0f;
    }

    // Botón "Saltar", tecla Escape o fin de la última imagen.
    public void Saltar()
    {
        if (saliendo) return;
        saliendo = true;
        StopAllCoroutines();
        StartCoroutine(Salir());
    }

    private IEnumerator Salir()
    {
        if (botonSaltar != null) botonSaltar.interactable = false;
        float startText = grupoTexto != null ? grupoTexto.alpha : 0f;
        float startImage = grupoImagen != null ? grupoImagen.alpha : 0f;
        float startVolume = fuenteMusica != null ? fuenteMusica.volume : 0f;

        for (float t = 0f; t < musicaSeVa; t += Time.deltaTime)
        {
            float k = t / musicaSeVa;
            if (grupoTexto != null) grupoTexto.alpha = Mathf.Lerp(startText, 0f, k);
            if (grupoImagen != null) grupoImagen.alpha = Mathf.Lerp(startImage, 0f, k);
            if (fuenteMusica != null) fuenteMusica.volume = Mathf.Lerp(startVolume, 0f, k);
            yield return null;
        }
        if (fuenteMusica != null) fuenteMusica.Stop();
        SceneManager.LoadScene(escenaJuego);
    }

    private IEnumerator FundirVolumen(AudioSource source, float target, float duration)
    {
        float start = source.volume;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (saliendo) yield break;
            source.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        source.volume = target;
    }

    private static float Suave(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
