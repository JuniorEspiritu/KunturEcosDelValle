using UnityEngine;

// Pide a la cámara que genere la textura de profundidad. El agua del río
// (shader del asset Underwater Effect) la usa para oscurecerse donde es honda
// y dibujar la espuma de la orilla. En Built-in la cámara no la genera sola,
// y esta opción no se guarda en la escena: hay que activarla al arrancar.
[RequireComponent(typeof(Camera))]
public class CameraDepthTexture : MonoBehaviour
{
    private void OnEnable()
    {
        GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;
    }
}
