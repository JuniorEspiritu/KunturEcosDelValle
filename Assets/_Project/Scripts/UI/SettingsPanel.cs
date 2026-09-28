using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ajustes que de verdad hacen algo y se guardan entre partidas (PlayerPrefs).
// Son los tres que un jugador espera encontrar y que no requieren tocar el
// resto del juego: volumen, calidad gráfica y pantalla completa.
public class SettingsPanel : MonoBehaviour
{
    private const string VolumeKey = "Kuntur_Volumen";
    private const string QualityKey = "Kuntur_Calidad";
    private const string FullscreenKey = "Kuntur_PantallaCompleta";

    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TextMeshProUGUI volumeValue;

    [SerializeField] private Slider qualitySlider;
    [SerializeField] private TextMeshProUGUI qualityValue;

    [SerializeField] private Toggle fullscreenToggle;

    private static readonly string[] QualityNames = { "Baja", "Media", "Alta", "Muy alta" };

    private void Start()
    {
        // --- volumen ---
        float volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        AudioListener.volume = volume;
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.SetValueWithoutNotify(volume);
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        UpdateVolumeLabel(volume);

        // --- calidad ---
        int quality = PlayerPrefs.GetInt(QualityKey, Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, QualityNames.Length - 1));
        QualitySettings.SetQualityLevel(quality, true);
        if (qualitySlider != null)
        {
            qualitySlider.minValue = 0f;
            qualitySlider.maxValue = QualityNames.Length - 1;
            qualitySlider.wholeNumbers = true;
            qualitySlider.SetValueWithoutNotify(quality);
            qualitySlider.onValueChanged.AddListener(SetQuality);
        }
        UpdateQualityLabel(quality);

        // --- pantalla completa ---
        bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        Screen.fullScreen = fullscreen;
        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
    }

    public void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
        PlayerPrefs.Save();
        UpdateVolumeLabel(value);
    }

    public void SetQuality(float value)
    {
        int level = Mathf.Clamp(Mathf.RoundToInt(value), 0, QualityNames.Length - 1);
        QualitySettings.SetQualityLevel(level, true);
        PlayerPrefs.SetInt(QualityKey, level);
        PlayerPrefs.Save();
        UpdateQualityLabel(level);
    }

    public void SetFullscreen(bool value)
    {
        Screen.fullScreen = value;
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void UpdateVolumeLabel(float value)
    {
        if (volumeValue != null) volumeValue.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private void UpdateQualityLabel(int level)
    {
        if (qualityValue != null) qualityValue.text = QualityNames[Mathf.Clamp(level, 0, QualityNames.Length - 1)];
    }
}
