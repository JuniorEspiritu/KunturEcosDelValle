using TMPro;
using UnityEngine;

// Anima el título LETRA POR LETRA:
//   1) entrada: cada letra sube desde abajo y aparece, una detrás de otra
//   2) reposo: una onda muy suave recorre las letras, como si respirara
//
// Se hace moviendo los vértices de la malla del texto (TextMeshPro genera 4
// vértices por carácter), que es la única forma de animar letras sueltas sin
// crear un objeto de texto por cada letra.
[RequireComponent(typeof(TMP_Text))]
public class TitleTextAnimator : MonoBehaviour
{
    [Header("Entrada letra por letra")]
    [SerializeField] private float delayBetweenLetters = 0.045f;
    [SerializeField] private float letterDuration = 0.35f;
    [SerializeField] private float riseDistance = 42f;
    [SerializeField] private float startDelay = 0.15f;

    [Header("Onda en reposo")]
    [SerializeField] private bool idleWave = true;
    [SerializeField] private float waveAmplitude = 3.2f;
    [SerializeField] private float waveSpeed = 2.1f;
    [SerializeField] private float waveSpacing = 0.22f;

    private TMP_Text text;
    private TMP_MeshInfo[] originalMesh;
    private float timer;
    private int characterCount;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        timer = 0f;
        // El texto se remaqueta cada vez que el panel se vuelve a abrir, así
        // que hay que volver a copiar la malla original.
        text.ForceMeshUpdate();
        CacheMesh();
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }

    // OJO: acá NO se llama a ForceMeshUpdate. ForceMeshUpdate dispara este
    // mismo evento, así que llamarlo desde su propio handler entra en
    // recursión infinita y congela el editor. Solo se vuelve a copiar la
    // malla que TMP acaba de generar.
    private void OnTextChanged(Object changed)
    {
        if (changed == text) CacheMesh();
    }

    private void CacheMesh()
    {
        originalMesh = text.textInfo.CopyMeshInfoVertexData();
        characterCount = text.textInfo.characterCount;
    }

    private void Update()
    {
        if (originalMesh == null || characterCount == 0) return;

        timer += Time.unscaledDeltaTime;
        TMP_TextInfo info = text.textInfo;

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible) continue; // los espacios no tienen vértices

            int materialIndex = character.materialReferenceIndex;
            int vertexIndex = character.vertexIndex;

            if (materialIndex >= originalMesh.Length) continue;
            Vector3[] source = originalMesh[materialIndex].vertices;
            Vector3[] destination = info.meshInfo[materialIndex].vertices;
            if (vertexIndex + 3 >= source.Length) continue;

            // --- progreso de la entrada de ESTA letra ---
            float letterStart = startDelay + i * delayBetweenLetters;
            float progress = Mathf.Clamp01((timer - letterStart) / letterDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f); // desacelera al final

            float offsetY = Mathf.Lerp(-riseDistance, 0f, eased);

            // --- onda de reposo, solo cuando la letra ya entró ---
            if (idleWave && progress >= 1f)
            {
                offsetY += Mathf.Sin(timer * waveSpeed + i * waveSpacing) * waveAmplitude;
            }

            Vector3 offset = new Vector3(0f, offsetY, 0f);
            for (int v = 0; v < 4; v++) destination[vertexIndex + v] = source[vertexIndex + v] + offset;

            // Alpha por letra: aparece junto con la subida.
            Color32[] colors = info.meshInfo[materialIndex].colors32;
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(eased) * 255f);
            for (int v = 0; v < 4; v++)
            {
                Color32 c = colors[vertexIndex + v];
                c.a = alpha;
                colors[vertexIndex + v] = c;
            }
        }

        for (int m = 0; m < info.meshInfo.Length; m++)
        {
            info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
            info.meshInfo[m].mesh.colors32 = info.meshInfo[m].colors32;
            text.UpdateGeometry(info.meshInfo[m].mesh, m);
        }
    }
}
