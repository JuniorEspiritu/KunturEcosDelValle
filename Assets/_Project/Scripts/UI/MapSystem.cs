using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Minimapa en la esquina + mapa completo con la tecla M. Los dos usan la MISMA
// cámara cenital y la misma RenderTexture: al abrir el mapa grande la cámara
// se aleja, al cerrarlo vuelve a seguir a Kuntur de cerca. Así no hay dos
// cámaras renderizando a la vez (costaría el doble por nada).
public class MapSystem : MonoBehaviour
{
    // Lo lee el controlador del jugador: con el mapa abierto no se camina ni
    // se gira la cámara, porque el mouse está ocupado arrastrando el mapa.
    public static bool FullMapOpen { get; private set; }

    [Header("Cámara cenital")]
    [SerializeField] private Camera mapCamera;
    [SerializeField] private Transform player;
    // La flechita apunta hacia donde MIRA el jugador (la cámara), no hacia
    // donde está girado el cuerpo del cóndor: al mover el mouse para ver una
    // calle, la flecha ya señala esa calle aunque todavía no se camine.
    [SerializeField] private Transform viewDirection;
    [SerializeField] private float cameraHeight = 60f;

    [Header("Minimapa (esquina)")]
    [SerializeField] private GameObject minimapPanel;
    [SerializeField] private RectTransform minimapArrow;
    [SerializeField] private float minimapZoom = 28f;
    // Apagado (por defecto): el norte queda fijo arriba, el mapa NO gira y lo
    // que gira es la flecha. Encendido sería un minimapa tipo GPS que gira.
    [SerializeField] private bool minimapFollowsHeading = false;

    [Header("Mapa completo (tecla M)")]
    [SerializeField] private GameObject fullMapPanel;
    [SerializeField] private RectTransform fullMapImage;
    [SerializeField] private RectTransform fullMapArrow;
    [SerializeField] private float fullMapZoom = 140f;
    [SerializeField] private Vector2 worldCenter = new Vector2(10f, 0f);
    [SerializeField] private Vector2 worldLimit = new Vector2(150f, 130f);

    [Header("Zoom del mapa")]
    [SerializeField] private float minZoom = 25f;
    [SerializeField] private float maxZoom = 170f;
    [SerializeField] private float zoomStep = 12f;

    [Header("Marcadores")]
    [SerializeField] private Image markerTemplate;        // plantilla desactivada, se clona por residuo
    [SerializeField] private Color markerColor = new Color(0.48f, 0.89f, 0.63f);
    [SerializeField] private TextMeshProUGUI streetLabelTemplate;
    [SerializeField] private StreetLabel[] streets;
    [SerializeField] private Color sampleColor = new Color(0.35f, 0.72f, 1f);   // muestras de agua
    [SerializeField] private Color missionColor = new Color(1f, 0.62f, 0.15f);  // personas con misión
    [SerializeField] private Color waypointColor = new Color(0.67f, 0.36f, 1f); // destino marcado (morado)
    private Image waypointMarker;

    [System.Serializable]
    public struct StreetLabel
    {
        public string name;
        public Vector2 position; // x, z del mundo
        public float angle;      // 0 = calle que corre este-oeste
        // Lugares (Plaza Vea, el parque, el río...) van en otro color y más
        // grandes que las calles. Color con alfa 0 = el de la plantilla.
        public Color color;
        public float fontSize;   // 0 = el de la plantilla
    }

    private bool fullMapOpen;
    private Vector2 mapFocus;    // qué punto del mundo está en el centro del mapa
    private bool dragging;
    private Vector2 dragOrigin;
    private Vector2 focusAtDragStart;
    private CursorLockMode cursorBeforeMap;

    private TrashPickup[] trashPoints;
    private SimpleThirdPersonController walker;
    private float lastClickTime = -10f;
    private Vector2 lastClickPos;
    private Image[] markers;
    private TextMeshProUGUI[] streetLabels;

    // Marcadores que no son basura: muestras de agua (se apagan al tomarlas)
    // y las personas que dan misión (siempre visibles).
    private WaterSampleKit[] samplePoints;
    private Image[] sampleMarkers;
    private Transform[] missionPoints;
    private Image[] missionMarkers;

    private void Start()
    {
        if (fullMapPanel != null) fullMapPanel.SetActive(false);
        FullMapOpen = false;
        BuildTrashMarkers();
        BuildOtherMarkers();
        BuildStreetLabels();
        ApplyMinimapView();
    }

    // Un punto en el mapa por cada residuo: es la guía para saber a dónde ir a
    // limpiar, en vez de dar vueltas buscando a ciegas.
    private void BuildTrashMarkers()
    {
        if (markerTemplate == null || fullMapImage == null) return;

        // Incluye los apagados: la basura de cada misión aparece recién cuando
        // aceptas el encargo, y su punto en el mapa tiene que aparecer con ella.
        trashPoints = FindObjectsByType<TrashPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        markers = new Image[trashPoints.Length];

        for (int i = 0; i < trashPoints.Length; i++)
        {
            Image marker = Instantiate(markerTemplate, fullMapImage);
            marker.gameObject.SetActive(true);
            marker.color = markerColor;
            marker.rectTransform.sizeDelta = new Vector2(12f, 12f);
            markers[i] = marker;
        }
    }

    private void BuildOtherMarkers()
    {
        if (markerTemplate == null || fullMapImage == null) return;

        samplePoints = FindObjectsByType<WaterSampleKit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sampleMarkers = new Image[samplePoints.Length];
        for (int i = 0; i < samplePoints.Length; i++)
            sampleMarkers[i] = MakeMarker(sampleColor, 15f);

        waypointMarker = MakeMarker(waypointColor, 22f);
        waypointMarker.gameObject.SetActive(false);

        DialogueNPC[] npcs = FindObjectsByType<DialogueNPC>(FindObjectsSortMode.None);
        missionPoints = new Transform[npcs.Length];
        missionMarkers = new Image[npcs.Length];
        for (int i = 0; i < npcs.Length; i++)
        {
            missionPoints[i] = npcs[i].transform;
            missionMarkers[i] = MakeMarker(missionColor, 20f);
        }
    }

    private Image MakeMarker(Color color, float size)
    {
        Image marker = Instantiate(markerTemplate, fullMapImage);
        marker.gameObject.SetActive(true);
        marker.color = color;
        marker.rectTransform.sizeDelta = new Vector2(size, size);
        return marker;
    }

    // Los nombres de las calles sobre el mapa. Sin esto, el mapa es un dibujo
    // bonito pero no dice dónde estás parado: los letreros de las esquinas se
    // leen en la calle, no desde arriba.
    private void BuildStreetLabels()
    {
        if (streetLabelTemplate == null || streets == null || fullMapImage == null) return;

        streetLabels = new TextMeshProUGUI[streets.Length];
        for (int i = 0; i < streets.Length; i++)
        {
            TextMeshProUGUI label = Instantiate(streetLabelTemplate, fullMapImage);
            label.gameObject.SetActive(true);
            label.text = streets[i].name;
            label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, streets[i].angle);
            if (streets[i].color.a > 0.01f) label.color = streets[i].color;
            if (streets[i].fontSize > 0.1f) label.fontSize = streets[i].fontSize;
            streetLabels[i] = label;
        }
    }

    private void Update()
    {
        // Solo M abre y cierra: Escape ya lo usa el jugador para soltar el
        // mouse, y si también cerrara el mapa harían las dos cosas a la vez.
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            ToggleFullMap();

        if (fullMapOpen)
        {
            HandleZoom();
            HandleDrag();
            HandleRecenter();
            HandleWaypoint();
            ApplyMapCamera();
            UpdateFullMap();
        }
        else
        {
            ApplyMinimapView();
        }
    }

    // Zoom con la rueda: acercarse sirve para ver EXACTAMENTE en qué esquina
    // quedó cada residuo, en vez de adivinar por un puntito.
    private void HandleZoom()
    {
        float step = 0f;

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) step -= Mathf.Sign(scroll) * zoomStep;
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.numpadPlusKey.isPressed || Keyboard.current.equalsKey.isPressed)
                step -= zoomStep * 20f * Time.unscaledDeltaTime;
            if (Keyboard.current.numpadMinusKey.isPressed || Keyboard.current.minusKey.isPressed)
                step += zoomStep * 20f * Time.unscaledDeltaTime;
        }

        if (Mathf.Abs(step) < 0.001f) return;
        fullMapZoom = Mathf.Clamp(fullMapZoom + step, minZoom, maxZoom);
    }

    // Arrastrar con el mouse para pasear por el mapa.
    //
    // La cuenta clave: la cámara del mapa cubre 2*zoom metros de alto y el
    // recuadro mide fullMapImage.rect.height píxeles. Dividiendo una cosa por
    // la otra sale cuántos METROS vale cada píxel arrastrado, y por eso el
    // mapa sigue al puntero exactamente, esté como esté el zoom. Sin esa
    // conversión, acercado se movería lentísimo y alejado se dispararía.
    private void HandleDrag()
    {
        if (Mouse.current == null || fullMapImage == null) return;

        Vector2 pointer = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            dragOrigin = pointer;
            focusAtDragStart = mapFocus;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame) dragging = false;
        if (!dragging) return;

        Vector2 size = fullMapImage.rect.size;
        if (size.x < 1f || size.y < 1f) return;

        // El recuadro está escalado por el Canvas (referencia 1280x720), así
        // que los píxeles de pantalla hay que pasarlos a píxeles del recuadro.
        float canvasScale = fullMapImage.lossyScale.y;
        if (canvasScale < 0.0001f) canvasScale = 1f;

        // Cada eje por separado: el recuadro del mapa no es cuadrado (640x570),
        // así que un píxel horizontal y uno vertical no valen los mismos
        // metros. Con un solo factor, arrastrar en diagonal se iba de lado.
        Vector2 metersPerPixel = new Vector2(
            (fullMapZoom * 2f) / size.x / canvasScale,
            (fullMapZoom * 2f) / size.y / canvasScale);

        Vector2 delta = pointer - dragOrigin;
        Vector2 moved = new Vector2(delta.x * metersPerPixel.x, delta.y * metersPerPixel.y);

        // Se resta: al arrastrar hacia la derecha, el mapa va a la derecha, o
        // sea que el centro se corre hacia la izquierda.
        mapFocus = focusAtDragStart - moved;
        mapFocus.x = Mathf.Clamp(mapFocus.x, worldCenter.x - worldLimit.x, worldCenter.x + worldLimit.x);
        mapFocus.y = Mathf.Clamp(mapFocus.y, worldCenter.y - worldLimit.y, worldCenter.y + worldLimit.y);
    }

    // Tecla C: volver a centrar el mapa en Kuntur. Después de pasear por el
    // valle hay que poder volver sin buscar a ojo.
    private void HandleRecenter()
    {
        bool cKey = Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
        if (!cKey) return;

        if (player != null) mapFocus = new Vector2(player.position.x, player.position.z);
    }

    // Clic derecho en el mapa: "quiero ir aquí". RouteGuide calcula el camino
    // por las calles y lo pinta de morado en el mapa y en el minimapa. Sirve
    // en cualquier momento, con misión o sin ella. X lo borra.
    private void HandleWaypoint()
    {
        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame && RouteGuide.Instance != null)
            RouteGuide.Instance.ClearCustomTarget();

        // Doble clic (izquierdo o derecho) sobre el mapa: se borra el punto
        // morado y su ruta.
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
        {
            Vector2 click = Mouse.current.position.ReadValue();
            bool isDouble = Time.unscaledTime - lastClickTime < 0.35f && Vector2.Distance(click, lastClickPos) < 30f;
            lastClickTime = isDouble ? -10f : Time.unscaledTime;
            lastClickPos = click;
            if (isDouble)
            {
                if (RouteGuide.Instance != null) RouteGuide.Instance.HideRoute();
                return;
            }
        }

        if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame || fullMapImage == null) return;

        Vector2 pointer = Mouse.current.position.ReadValue();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(fullMapImage, pointer, null, out Vector2 local)) return;

        Vector2 half = fullMapImage.rect.size * 0.5f;
        if (half.x < 1f || half.y < 1f) return;
        Vector2 normalized = new Vector2(local.x / half.x, local.y / half.y);
        if (Mathf.Abs(normalized.x) > 1f || Mathf.Abs(normalized.y) > 1f) return; // clic fuera del mapa

        Vector2 world = mapFocus + normalized * fullMapZoom;
        if (RouteGuide.Instance != null) RouteGuide.Instance.SetCustomTarget(new Vector3(world.x, 0f, world.y));
    }

    private void ApplyMapCamera()
    {
        if (mapCamera == null) return;
        mapCamera.orthographicSize = fullMapZoom;
        mapCamera.transform.position = new Vector3(mapFocus.x, cameraHeight * 2f, mapFocus.y);
        mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    public void ToggleFullMap()
    {
        fullMapOpen = !fullMapOpen;
        FullMapOpen = fullMapOpen;
        dragging = false;

        if (fullMapPanel != null) fullMapPanel.SetActive(fullMapOpen);
        if (minimapPanel != null) minimapPanel.SetActive(!fullMapOpen);

        if (fullMapOpen)
        {
            // Se abre centrado en Kuntur: lo primero que uno quiere saber al
            // abrir un mapa es dónde está.
            mapFocus = player != null
                ? new Vector2(player.position.x, player.position.z)
                : worldCenter;

            ApplyMapCamera();
            UpdateFullMap();

            // El puntero se libera solo: el mapa se maneja con el mouse.
            cursorBeforeMap = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = cursorBeforeMap;
            Cursor.visible = cursorBeforeMap != CursorLockMode.Locked;
        }
    }

    private void OnDisable()
    {
        FullMapOpen = false;
    }

    private void ApplyMinimapView()
    {
        if (mapCamera == null || player == null) return;

        mapCamera.orthographicSize = minimapZoom;
        mapCamera.transform.position = new Vector3(player.position.x, player.position.y + cameraHeight, player.position.z);

        // Kuntur siempre va al centro del minimapa; lo que cambia es si gira
        // el mapa o la flecha.
        float heading = LookHeading();

        if (minimapFollowsHeading)
        {
            // Minimapa giratorio: "arriba" es siempre hacia donde mira la
            // cámara (lo que tienes delante), y la flechita marca hacia dónde
            // caminas respecto de eso.
            float view = ViewHeading();
            minimapYaw = Mathf.LerpAngle(minimapYaw, view, Time.deltaTime * 8f);
            mapCamera.transform.rotation = Quaternion.Euler(90f, minimapYaw, 0f);
            if (minimapArrow != null)
            {
                minimapArrow.anchoredPosition = Vector2.zero;
                minimapArrow.localRotation = Quaternion.Euler(0f, 0f, -(heading - minimapYaw));
            }
        }
        else
        {
            mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            if (minimapArrow != null)
            {
                minimapArrow.anchoredPosition = Vector2.zero;
                minimapArrow.localRotation = Quaternion.Euler(0f, 0f, -heading);
            }
        }
    }

    private void UpdateFullMap()
    {
        if (fullMapImage == null || player == null) return;

        Vector2 halfSize = fullMapImage.rect.size * 0.5f;

        if (fullMapArrow != null)
        {
            // La cámara ortográfica cubre ±fullMapZoom en alto, y la
            // RenderTexture es cuadrada, así que en ancho cubre lo mismo: el
            // mapeo mundo -> recuadro es una regla de tres directa.
            Vector2 normalized = WorldToMap(new Vector2(player.position.x, player.position.z));
            fullMapArrow.anchoredPosition = new Vector2(normalized.x * halfSize.x, normalized.y * halfSize.y);

            float heading = LookHeading();
            fullMapArrow.localRotation = Quaternion.Euler(0f, 0f, -heading);

            // En el mapa completo el norte queda fijo arriba, así que la
            // flecha se esconde si Kuntur quedó fuera del trozo que se está
            // mirando (se puede arrastrar lejos de él).
            bool inside = Mathf.Abs(normalized.x) <= 1.02f && Mathf.Abs(normalized.y) <= 1.02f;
            if (fullMapArrow.gameObject.activeSelf != inside) fullMapArrow.gameObject.SetActive(inside);
        }

        UpdateTrashMarkers(halfSize);
        UpdateOtherMarkers(halfSize);
        UpdateStreetLabels(halfSize);
    }

    // Hacia dónde mira el jugador, en grados (0 = norte / +Z). Se usa solo el
    // giro horizontal de la cámara: si mira hacia abajo o arriba, la flecha
    // no debe cambiar.
    private float minimapYaw;

    private float ViewHeading()
    {
        Transform source = viewDirection;
        if (source == null && Camera.main != null) source = Camera.main.transform;
        if (source == null) return 0f;
        Vector3 forward = source.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = source.up;
        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    private float LookHeading()
    {
        // La flechita señala hacia DONDE CAMINA Kuntur (su último movimiento),
        // no hacia donde apunta la cámara: así el mapa se lee como un GPS.
        if (walker == null && player != null) walker = player.GetComponent<SimpleThirdPersonController>();
        if (walker != null)
        {
            Vector3 move = walker.LastMoveDirection;
            move.y = 0f;
            if (move.sqrMagnitude > 0.0001f) return Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg;
        }

        Transform source = viewDirection;
        if (source == null && Camera.main != null) source = Camera.main.transform;
        if (source == null) source = player;

        Vector3 forward = source.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = source.up; // mirando justo abajo
        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    private Vector2 WorldToMap(Vector2 world)
    {
        return new Vector2((world.x - mapFocus.x) / fullMapZoom, (world.y - mapFocus.y) / fullMapZoom);
    }

    private void UpdateTrashMarkers(Vector2 halfSize)
    {
        if (markers == null || trashPoints == null) return;

        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] == null) continue;

            TrashPickup trash = trashPoints[i];
            bool visible = trash != null && trash.gameObject.activeInHierarchy && !trash.Collected;

            if (!visible)
            {
                if (markers[i].gameObject.activeSelf) markers[i].gameObject.SetActive(false);
                continue;
            }

            Vector3 position = trash.transform.position;
            Vector2 normalized = WorldToMap(new Vector2(position.x, position.z));

            // Fuera del recorte: se esconde en vez de quedar pegado al borde
            // engañando sobre dónde está.
            bool inside = Mathf.Abs(normalized.x) <= 1.02f && Mathf.Abs(normalized.y) <= 1.02f;
            if (markers[i].gameObject.activeSelf != inside) markers[i].gameObject.SetActive(inside);
            if (!inside) continue;

            markers[i].rectTransform.anchoredPosition = new Vector2(normalized.x * halfSize.x, normalized.y * halfSize.y);
        }
    }

    private void UpdateOtherMarkers(Vector2 halfSize)
    {
        if (sampleMarkers != null)
        {
            for (int i = 0; i < sampleMarkers.Length; i++)
            {
                WaterSampleKit kit = samplePoints[i];
                PlaceMarker(sampleMarkers[i], kit != null && kit.gameObject.activeInHierarchy && !kit.Sampled ? kit.transform : null, halfSize);
            }
        }

        if (missionMarkers != null)
        {
            for (int i = 0; i < missionMarkers.Length; i++)
            {
                // Solo se marca a quien HOY tiene algo que decir: el vecino que
                // da la misión del nivel (su collider está prendido) o alguien a
                // quien todavía falta convencer.
                Transform point = missionPoints[i];
                bool relevant = false;
                if (point != null)
                {
                    DialogueNPC npc = point.GetComponent<DialogueNPC>();
                    Collider col = point.GetComponent<Collider>();
                    relevant = npc != null && !npc.Convinced && (col == null || col.enabled);
                }
                PlaceMarker(missionMarkers[i], relevant ? point : null, halfSize);
            }
        }

        if (waypointMarker != null)
        {
            Vector3? target = RouteGuide.Instance != null ? RouteGuide.Instance.ActiveTarget : null;
            if (target.HasValue)
            {
                Vector2 n = WorldToMap(new Vector2(target.Value.x, target.Value.z));
                bool show = Mathf.Abs(n.x) <= 1.02f && Mathf.Abs(n.y) <= 1.02f;
                if (waypointMarker.gameObject.activeSelf != show) waypointMarker.gameObject.SetActive(show);
                if (show) waypointMarker.rectTransform.anchoredPosition = new Vector2(n.x * halfSize.x, n.y * halfSize.y);
            }
            else if (waypointMarker.gameObject.activeSelf) waypointMarker.gameObject.SetActive(false);
        }
    }

    private void PlaceMarker(Image marker, Transform target, Vector2 halfSize)
    {
        if (marker == null) return;
        bool show = target != null;
        Vector2 normalized = Vector2.zero;
        if (show)
        {
            normalized = WorldToMap(new Vector2(target.position.x, target.position.z));
            show = Mathf.Abs(normalized.x) <= 1.02f && Mathf.Abs(normalized.y) <= 1.02f;
        }

        if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
        if (show) marker.rectTransform.anchoredPosition = new Vector2(normalized.x * halfSize.x, normalized.y * halfSize.y);
    }

    private void UpdateStreetLabels(Vector2 halfSize)
    {
        if (streetLabels == null || streets == null) return;

        // Muy alejado, los nombres se amontonan y tapan el mapa; se muestran
        // recién cuando el zoom es suficiente para leerlos sin estorbar.
        bool showNames = fullMapZoom < maxZoom * 0.92f;

        for (int i = 0; i < streetLabels.Length; i++)
        {
            if (streetLabels[i] == null) continue;

            Vector2 normalized = WorldToMap(streets[i].position);
            // Los lugares (con color propio) se ven siempre; las calles, solo
            // con suficiente zoom.
            bool place = streets[i].color.a > 0.01f;
            bool inside = (showNames || place) && Mathf.Abs(normalized.x) <= 1f && Mathf.Abs(normalized.y) <= 1f;

            if (streetLabels[i].gameObject.activeSelf != inside) streetLabels[i].gameObject.SetActive(inside);
            if (!inside) continue;

            streetLabels[i].rectTransform.anchoredPosition = new Vector2(normalized.x * halfSize.x, normalized.y * halfSize.y);
        }
    }
}
