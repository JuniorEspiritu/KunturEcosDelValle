using UnityEngine;

// El cielo del valle a lo largo del día, armado con las fotos panorámicas de
// AllSky Free (día, tarde y noche) más un velo de estrellas finitas.
//
// Cuatro piezas, y cada una está por una razón:
//
// 1) Tres fotos de cielo (día nublado, atardecer, noche) que se funden entre
//    sí con el shader Kuntur/SkyPanoramicBlend. Unity solo admite UN skybox a
//    la vez, así que cambiar de material al llegar la tarde se vería como un
//    corte; fundiéndolos, la tarde llega de a poco.
//
// 2) El azimut del sol se le pasa al shader. Estas fotos traen el sol pintado
//    en un punto fijo; sin corregir el giro, ese sol pintado apuntaría a
//    cualquier lado y no coincidiría con las sombras del pueblo.
//
// 3) Un domo de estrellas encima de la noche: la foto nocturna de AllSky
//    ("Cold Night") trae la luna pintada pero no estrellas.
public class SkyController : MonoBehaviour
{
    [Header("Fotos de cielo")]
    [SerializeField] private Texture dayTexture;
    [SerializeField] private Texture sunsetTexture;
    [SerializeField] private Texture nightTexture;

    [Header("Estrellas")]
    [SerializeField] private Renderer starDome;
    [SerializeField] private Transform starDomeTransform;
    // Brillo máximo de las estrellas: un velo suave, que no compita con la
    // luna y las nubes de la foto nocturna.
    [SerializeField] private float starBrightness = 0.8f;

    [Header("Brillo de cada momento")]
    [SerializeField] private float dayExposure = 1f;
    [SerializeField] private float sunsetExposure = 2.1f;
    [SerializeField] private float nightExposure = 1.15f;

    private Material skyMaterial;
    private Material starMaterial;
    private Light sun;
    private Transform viewer;

    private Texture currentA;
    private Texture currentB;

    private static readonly int TexAId = Shader.PropertyToID("_TexA");
    private static readonly int TexBId = Shader.PropertyToID("_TexB");
    private static readonly int BlendId = Shader.PropertyToID("_Blend");
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly int SunAzimuthId = Shader.PropertyToID("_SunAzimuth");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        // Copia propia del material del cielo. RenderSettings.skybox apunta a
        // un asset del proyecto: si se le cambiaran las propiedades
        // directamente, quedarían GUARDADAS en el archivo y el cielo
        // amanecería de noche la próxima vez que se abra la escena.
        if (RenderSettings.skybox != null)
        {
            skyMaterial = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyMaterial;
        }

        if (starDome != null) starMaterial = starDome.material;
    }

    private void Start()
    {
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            sun = light;
            break;
        }

        if (sun != null && RenderSettings.sun == null) RenderSettings.sun = sun;
        ApplySunAzimuth();
    }

    // El sol recorre siempre el mismo plano del cielo (sale por un lado y se
    // pone por el opuesto), así que su azimut solo puede tomar dos valores
    // separados 180°. Si se leyera directo de la dirección de la luz, al
    // mediodía saltaría de uno al otro y el cielo entero giraría de golpe. Por
    // eso se saca del GIRO de la luz, que no cambia en todo el día, y se usa
    // el lado por donde el sol se PONE, que es cuando el cielo naranja importa.
    private void ApplySunAzimuth()
    {
        if (skyMaterial == null) return;

        float lightYaw = sun != null ? sun.transform.eulerAngles.y : 0f;
        skyMaterial.SetFloat(SunAzimuthId, 90f - lightYaw);
    }

    private void LateUpdate()
    {
        if (DayNightCycle.Instance == null) return;

        float hour = DayNightCycle.Instance.CurrentHour;
        UpdateSky(hour);
        UpdateNightPieces(hour);
    }

    private void UpdateSky(float hour)
    {
        if (skyMaterial == null) return;

        Texture a, b;
        float blend, exposure;

        if (hour < 5f)                              // noche cerrada
        {
            a = b = nightTexture;
            blend = 0f;
            exposure = nightExposure;
        }
        else if (hour < 7f)                         // la noche se abre al amanecer
        {
            float t = Mathf.InverseLerp(5f, 7f, hour);
            a = nightTexture; b = sunsetTexture; blend = t;
            exposure = Mathf.Lerp(nightExposure, sunsetExposure, t);
        }
        else if (hour < 9f)                         // amanece
        {
            float t = Mathf.InverseLerp(7f, 9f, hour);
            a = sunsetTexture; b = dayTexture; blend = t;
            exposure = Mathf.Lerp(sunsetExposure, dayExposure, t);
        }
        else if (hour < 16.5f)                      // pleno día
        {
            a = b = dayTexture;
            blend = 0f;
            exposure = dayExposure;
        }
        else if (hour < 19.3f)                      // atardecer
        {
            float t = Mathf.InverseLerp(16.5f, 19.3f, hour);
            a = dayTexture; b = sunsetTexture; blend = t;
            exposure = Mathf.Lerp(dayExposure, sunsetExposure, t);
        }
        else if (hour < 21f)                        // cae la noche
        {
            float t = Mathf.InverseLerp(19.3f, 21f, hour);
            a = sunsetTexture; b = nightTexture; blend = t;
            exposure = Mathf.Lerp(sunsetExposure, nightExposure, t);
        }
        else                                        // noche
        {
            a = b = nightTexture;
            blend = 0f;
            exposure = nightExposure;
        }

        // Las texturas solo se asignan cuando CAMBIAN. Asignar una textura a
        // un material en cada frame obliga a rehacer su estado aunque sea la
        // misma.
        if (a != currentA) { skyMaterial.SetTexture(TexAId, a); currentA = a; }
        if (b != currentB) { skyMaterial.SetTexture(TexBId, b); currentB = b; }

        skyMaterial.SetFloat(BlendId, blend);
        skyMaterial.SetFloat(ExposureId, exposure);
    }

    private void UpdateNightPieces(float hour)
    {
        if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
        if (viewer == null) return;

        float night = NightFactor(hour);

        // El domo viaja con la cámara: si se quedara quieto en el origen, al
        // cruzar el valle se le vería el borde.
        if (starDomeTransform != null) starDomeTransform.position = viewer.position;
        if (starMaterial != null) starMaterial.SetColor(ColorId, new Color(1f, 1f, 1f, night * starBrightness));
    }

    // 0 de día, 1 de noche, con subida y bajada suaves.
    private float NightFactor(float hour)
    {
        if (hour >= 18.6f) return Mathf.InverseLerp(18.6f, 20.4f, hour);
        if (hour <= 4.6f) return 1f;
        if (hour <= 6.2f) return 1f - Mathf.InverseLerp(4.6f, 6.2f, hour);
        return 0f;
    }
}
