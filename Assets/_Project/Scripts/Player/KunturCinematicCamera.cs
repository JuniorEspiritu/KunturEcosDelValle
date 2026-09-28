using UnityEngine;

// Tomas de "película" con la cámara del jugador, sin cámaras extra:
//
//  - Conversación (tecla E con un vecino): la cámara se va a un costado y un
//    poco arriba, y encuadra a los DOS de perfil, como en una escena de
//    diálogo. Al terminar vuelve suave a su lugar detrás de Kuntur.
//  - Retrato (misión fallida): se pone delante de Kuntur para que se le vea
//    la cara triste.
//
// Va en la Main Camera, que es hija del CameraPivot del jugador. Mientras
// dura la toma se mueve en espacio de mundo; al soltarla regresa a su
// posición local de siempre (la que arma SimpleThirdPersonController).
public class KunturCinematicCamera : MonoBehaviour
{
    public static KunturCinematicCamera Instance { get; private set; }

    [SerializeField] private float blendSpeed = 3.2f;
    [SerializeField] private float returnTime = 0.7f;

    [Header("Conversación")]
    [SerializeField] private float heightAboveGround = 2.3f;  // "un poquito arribita"
    [SerializeField] private float lookHeight = 1.05f;
    [SerializeField] private float minDistance = 3.4f;

    [Header("Retrato")]
    [SerializeField] private float portraitDistance = 3.1f;
    [SerializeField] private float portraitHeight = 1.9f;

    private enum Shot { None, Dialogue, Portrait }

    private Shot shot = Shot.None;
    private Transform player;
    private Transform other;
    private float sideSign = 1f;
    private Vector3 restLocalPosition;
    private Quaternion restLocalRotation;
    private bool hasRest;
    private float returning;
    private Vector3 returnFromPos;
    private Quaternion returnFromRot;
    private float shotDistance;
    private bool shotSway;
    private float shotTime;

    public bool Active => shot != Shot.None;
    // Mientras dura una toma o la vuelta a su lugar, nadie más mueve la cámara
    // (SimpleThirdPersonController lo respeta al cambiar de modo con Z).
    public bool Busy => shot != Shot.None || returning > 0f;

    private void Awake()
    {
        Instance = this;
    }

    private Transform FindPlayer()
    {
        if (player != null) return player;
        SimpleThirdPersonController p = SimpleThirdPersonController.Instance;
        if (p == null) p = FindAnyObjectByType<SimpleThirdPersonController>();
        if (p != null) player = p.transform;
        return player;
    }

    public void StartDialogue(Transform npc)
    {
        if (npc == null || FindPlayer() == null) return;
        other = npc;
        RememberRest();

        // Se va al costado que ya queda más cerca de la cámara: así el giro
        // es corto y no pasa por dentro de nadie.
        Vector3 dir = Flat(other.position - player.position);
        if (dir.sqrMagnitude < 0.001f) dir = player.forward;
        Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
        sideSign = Vector3.Dot(transform.position - player.position, side) >= 0f ? 1f : -1f;

        shot = Shot.Dialogue;
        returning = 0f;
    }

    // distance < 0: la de siempre. sway: la cámara se mece de un lado a
    // otro despacito (para el baile de victoria, que no se vea como foto).
    public void StartPortrait(float distance = -1f, bool sway = false)
    {
        if (FindPlayer() == null) return;
        RememberRest();
        shot = Shot.Portrait;
        shotDistance = distance > 0f ? distance : portraitDistance;
        shotSway = sway;
        shotTime = 0f;
        returning = 0f;
    }

    public void Stop()
    {
        if (shot == Shot.None) return;
        shot = Shot.None;
        returning = returnTime;
        returnFromPos = transform.position;
        returnFromRot = transform.rotation;
    }

    private void RememberRest()
    {
        if (shot != Shot.None || returning > 0f) return;
        restLocalPosition = transform.localPosition;
        restLocalRotation = transform.localRotation;
        hasRest = true;
    }

    private void LateUpdate()
    {
        if (shot != Shot.None)
        {
            if (!TryGetShot(out Vector3 pos, out Quaternion rot)) { Stop(); return; }
            float k = 1f - Mathf.Exp(-blendSpeed * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, k);
            return;
        }

        if (returning > 0f && hasRest)
        {
            returning -= Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(returning / returnTime));
            Transform parent = transform.parent;
            Vector3 restPos = parent != null ? parent.TransformPoint(restLocalPosition) : restLocalPosition;
            Quaternion restRot = parent != null ? parent.rotation * restLocalRotation : restLocalRotation;
            transform.position = Vector3.Lerp(returnFromPos, restPos, t);
            transform.rotation = Quaternion.Slerp(returnFromRot, restRot, t);

            if (returning <= 0f)
            {
                transform.localPosition = restLocalPosition;
                transform.localRotation = restLocalRotation;
            }
        }
    }

    private bool TryGetShot(out Vector3 pos, out Quaternion rot)
    {
        pos = transform.position;
        rot = transform.rotation;
        if (player == null) return false;

        if (shot == Shot.Portrait)
        {
            shotTime += Time.unscaledDeltaTime;
            Vector3 fwd = Flat(player.forward).normalized;
            if (shotSway) fwd = Quaternion.Euler(0f, Mathf.Sin(shotTime * 0.35f) * 30f, 0f) * fwd;
            pos = player.position + fwd * shotDistance + Vector3.up * portraitHeight;
            Vector3 look = player.position + Vector3.up * lookHeight;
            rot = Quaternion.LookRotation(look - pos);
            return true;
        }

        if (other == null) return false;

        // Los dos de perfil: la cámara mira perpendicular a la línea que los
        // une, desde un poco más arriba que sus cabezas.
        Vector3 a = player.position, b = other.position;
        Vector3 mid = (a + b) * 0.5f;
        Vector3 dir = Flat(b - a);
        float gap = dir.magnitude;
        if (gap < 0.001f) dir = player.forward; else dir /= gap;
        Vector3 side = Vector3.Cross(Vector3.up, dir) * sideSign;

        float distance = Mathf.Max(minDistance, gap * 1.35f + 2.2f);
        float ground = Mathf.Min(a.y, b.y);
        pos = new Vector3(mid.x, ground, mid.z) + side * distance + Vector3.up * heightAboveGround;
        // Se mira un poco por debajo de las caras: el panel del diálogo tapa
        // la parte de abajo de la pantalla, así los dos quedan arriba.
        Vector3 target = new Vector3(mid.x, ground + lookHeight * 0.8f, mid.z);
        rot = Quaternion.LookRotation(target - pos);
        return true;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
