using UnityEngine;

// Conecta una fuente de ambiente fija (el río) con el control de "Ambiente"
// de la pausa.
//
// Guarda su propio volumen de diseño y siempre lo multiplica por el ajuste del
// jugador. Importa guardarlo aparte: si cada vez se leyera el volumen actual
// de la fuente como referencia, bajar el control a 0 y volver a subirlo
// dejaría la fuente muda para siempre, porque su "volumen de diseño" habría
// pasado a ser 0.
[RequireComponent(typeof(AudioSource))]
public class AmbienceSourceVolume : MonoBehaviour
{
    [SerializeField] private float baseVolume = 0.8f;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        Apply();
    }

    private void OnEnable()
    {
        AudioVolumeSettings.OnChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        AudioVolumeSettings.OnChanged -= Apply;
    }

    private void Apply()
    {
        if (source == null) return;
        source.volume = baseVolume * AudioVolumeSettings.Ambience;
    }

    public void SetBaseVolume(float value)
    {
        baseVolume = value;
        Apply();
    }
}
