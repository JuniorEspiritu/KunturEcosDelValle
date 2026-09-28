using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// v56c: sonidos del menú principal. Un sonido al pasar de una opción a otra
// (JUGAR, NUEVA PARTIDA, AJUSTES, CRÉDITOS...) y otro al hacer clic.
//
// Suenan desde un objeto que no se destruye al cambiar de escena: el clic de
// "NUEVA PARTIDA" arranca el fundido a la historia, y si el sonido viviera en
// el menú se cortaría a la mitad.
public class MenuSounds : MonoBehaviour
{
    [SerializeField] private AudioClip changeClip;   // cambiar de opción
    [SerializeField] private AudioClip clickClip;    // hacer clic
    [SerializeField] private float changeVolume = 0.8f;
    [SerializeField] private float clickVolume = 1f;

    private static AudioSource source;
    private GameObject lastSelected;
    private float readyAt;
    private float lastChange;

    private void Start()
    {
        EnsureSource();
        readyAt = Time.unscaledTime + 0.4f; // la primera opción que se marca sola al abrir, callada
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            b.onClick.AddListener(PlayClick);
            if (b.GetComponent<MenuHoverSound>() == null) b.gameObject.AddComponent<MenuHoverSound>().owner = this;
        }
    }

    private void Update()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return;
        GameObject sel = es.currentSelectedGameObject;
        if (sel == lastSelected) return;
        lastSelected = sel;
        if (sel != null) PlayChange();
    }

    public void PlayChange()
    {
        if (Time.unscaledTime < readyAt || Time.unscaledTime - lastChange < 0.06f) return;
        lastChange = Time.unscaledTime;
        Play(changeClip, changeVolume);
    }

    public void PlayClick() => Play(clickClip, clickVolume);

    private static void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        EnsureSource();
        source.PlayOneShot(clip, volume * AudioVolumeSettings.Sfx);
    }

    private static void EnsureSource()
    {
        if (source != null) return;
        GameObject go = new GameObject("Sonidos_Menu");
        DontDestroyOnLoad(go);
        source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
    }
}

// Pasar el mouse por encima de un botón que no cambia la selección (VOLVER,
// los de ajustes) también suena.
public class MenuHoverSound : MonoBehaviour, IPointerEnterHandler
{
    public MenuSounds owner;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null) owner.PlayChange();
    }
}
