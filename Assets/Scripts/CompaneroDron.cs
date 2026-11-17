using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dron compañero: te sigue flotando con su linterna, lo podés MANDAR a un punto (apuntás y
/// apretás la tecla) o PILOTEAR a mano, y su cámara se ve en una ventanita en pantalla (o en
/// pantalla completa).
///
/// Armado del prefab (el modelo lo ponés vos):
///   CompaneroDron (este script, en la raíz)
///     ├─ Modelo        → tu malla (sin collider, o con collider marcado Is Trigger)
///     ├─ Luz           → un Spot Light mirando hacia adelante (+Z)       → campo `luz`
///     └─ CamaraDron    → una Camera mirando hacia adelante (+Z)           → campo `camara`
/// Si dejás `luz` o `camara` vacíos, se crean solos para poder probar con un cubo.
///
/// Teclas (por defecto, se cambian en el Inspector):
///   Z = mandar al punto que apuntás / si ya está afuera, volver
///   X = pilotear a mano (WASD, Space sube, Ctrl baja, mouse gira). X de nuevo = soltar
///   T = prender/apagar la luz del dron
///   V = cámara del dron en pantalla completa / ventanita
///
/// Límites: distancia máxima al jugador (si la pasa pierde la señal y vuelve solo) y los
/// centinelas lo ven con su misma vista: si lo detectan, dan la alerta en la posición del dron.
/// </summary>
public class CompaneroDron : MonoBehaviour
{
    public enum Estado { Siguiendo, YendoAPunto, Quieto, Piloteado, Volviendo }

    [Header("Referencias")]
    [Tooltip("El jugador. Vacío = lo toma del WorldGenerator o de la cámara principal.")]
    public Transform jugador;
    public Light luz;
    public Camera camara;

    [Header("Teclas")]
    public KeyCode teclaMandar = KeyCode.Z;
    public KeyCode teclaPilotear = KeyCode.X;
    public KeyCode teclaLuz = KeyCode.T;
    public KeyCode teclaPantallaCompleta = KeyCode.V;

    [Header("Seguir")]
    [Tooltip("Dónde flota respecto a la cabeza del jugador: X = derecha, Y = arriba, Z = adelante (negativo = atrás).")]
    public Vector3 offsetSeguir = new Vector3(0.45f, 0.1f, -0.15f);
    [Tooltip("Qué tan rápido se acomoda siguiéndote (más chico = más pegado).")]
    public float suavidadSeguir = 0.08f;
    [Tooltip("Qué tan rápido gira para apuntar adonde mirás (más alto = más pegado a la mira).")]
    public float velocidadGiroSeguir = 14f;

    [Header("Mandar a un punto")]
    [Tooltip("Hasta qué distancia (m) se puede apuntar para mandarlo.")]
    public float alcanceMandar = 60f;
    [Tooltip("Velocidad (m/s) cuando va solo a un punto o vuelve.")]
    public float velocidadVuelo = 8f;
    [Tooltip("Se queda a esta distancia (m) de la superficie que apuntaste, para no meterse en la pared.")]
    public float separacionSuperficie = 1.5f;

    [Header("Pilotear")]
    public float velocidadPiloteo = 6f;
    public float velocidadPiloteoRapido = 14f;
    public float sensibilidadMouse = 2f;

    [Header("Choques")]
    [Tooltip("Radio (m) del dron para no atravesar paredes.")]
    public float radio = 0.35f;
    [Tooltip("Capas contra las que choca. Por defecto todas.")]
    public LayerMask capasChoque = ~0;

    [Header("Límites")]
    [Tooltip("Distancia máxima (m) al jugador. Si la pasa, pierde la señal y vuelve solo.")]
    public float distanciaMaxima = 50f;
    [Tooltip("Los centinelas lo ven con su misma vista. Si lo detectan, dan la alerta donde está el dron.")]
    public bool centinelasLoDetectan = true;
    [Tooltip("Si un centinela lo ve, el dron vuelve solo con vos.")]
    public bool volverSiLoVen = true;
    [Tooltip("Segundos entre alertas, para que no dispare una por frame.")]
    public float esperaEntreAlertas = 4f;

    [Header("Cámara en pantalla")]
    [Tooltip("Tamaño de la ventanita (px de referencia, sobre 1920x1080).")]
    public Vector2 tamanoVentanita = new Vector2(400f, 225f);
    [Tooltip("Margen de la ventanita a la esquina de abajo a la derecha.")]
    public Vector2 margenVentanita = new Vector2(24f, 24f);
    [Tooltip("Mostrar la ventanita también cuando el dron te sigue. Apagado = solo cuando está afuera.")]
    public bool ventanitaSiempre = false;
    [Tooltip("Al pilotear, la cámara del dron pasa sola a pantalla completa.")]
    public bool pantallaCompletaAlPilotear = true;

    public Estado EstadoActual { get; private set; } = Estado.Siguiendo;

    private Vector3 destino;
    private Vector3 velocidadSuave;
    private float yawPiloto, pitchPiloto;
    private float proximaRevision, proximaAlerta;
    private bool pantallaCompleta;

    private PlayerMovement movimientoJugador;
    private MouseLook mouseLook;
    private RenderTexture rt;
    private RawImage imagen;
    private RectTransform rectImagen;

    void Start()
    {
        if (jugador == null && WorldGenerator.Instance != null) jugador = WorldGenerator.Instance.jugador;
        if (jugador == null && Camera.main != null) jugador = Camera.main.transform.root;
        if (jugador != null)
        {
            movimientoJugador = jugador.GetComponentInChildren<PlayerMovement>();
            mouseLook = jugador.GetComponentInChildren<MouseLook>();
        }

        if (luz == null)
        {
            var go = new GameObject("Luz (auto)");
            go.transform.SetParent(transform, false);
            luz = go.AddComponent<Light>();
            luz.type = LightType.Spot;
            luz.spotAngle = 60f;
            luz.range = 25f;
            Debug.Log("[Dron] No tenía Luz asignada: creé un Spot de prueba. Poné el tuyo en el prefab.", this);
        }

        if (camara == null)
        {
            var go = new GameObject("CamaraDron (auto)");
            go.transform.SetParent(transform, false);
            camara = go.AddComponent<Camera>();
            camara.nearClipPlane = 0.1f;
            Debug.Log("[Dron] No tenía Cámara asignada: creé una de prueba. Poné la tuya en el prefab.", this);
        }

        rt = new RenderTexture(640, 360, 24) { name = "RT_CamaraDron" };
        camara.targetTexture = rt;
        CrearVentanita();

        if (jugador != null) transform.position = PosicionSeguir();
    }

    void OnDestroy()
    {
        SoltarControl();
        if (camara != null) camara.targetTexture = null;
        if (rt != null) rt.Release();
        if (imagen != null) Destroy(imagen.canvas.gameObject);
    }

    void Update()
    {
        if (jugador == null) return;
        if (ModoMenu.Activo) { ActualizarVentanita(); return; }

        LeerTeclas();

        switch (EstadoActual)
        {
            case Estado.Siguiendo:
                transform.position = Vector3.SmoothDamp(transform.position, PosicionSeguir(), ref velocidadSuave, suavidadSeguir);
                transform.rotation = Quaternion.Slerp(transform.rotation, RotacionHaciaMira(), velocidadGiroSeguir * Time.deltaTime);
                break;

            case Estado.YendoAPunto:
                if (VolarHacia(destino)) EstadoActual = Estado.Quieto;
                break;

            case Estado.Quieto:
                break;

            case Estado.Piloteado:
                Pilotear();
                break;

            case Estado.Volviendo:
                if (VolarHacia(PosicionSeguir())) EstadoActual = Estado.Siguiendo;
                break;
        }

        if (EstadoActual != Estado.Siguiendo && EstadoActual != Estado.Volviendo
            && Vector3.Distance(transform.position, jugador.position) > distanciaMaxima)
        {
            Debug.Log("[Dron] Señal perdida: demasiado lejos. Vuelve solo.", this);
            Volver();
        }

        if (centinelasLoDetectan && Time.time >= proximaRevision)
        {
            proximaRevision = Time.time + 0.25f;
            RevisarCentinelas();
        }

        ActualizarVentanita();
    }

    // ---------- Teclas ----------

    private void LeerTeclas()
    {
        if (Input.GetKeyDown(teclaLuz) && luz != null) luz.enabled = !luz.enabled;
        if (Input.GetKeyDown(teclaPantallaCompleta)) pantallaCompleta = !pantallaCompleta;

        if (Input.GetKeyDown(teclaPilotear))
        {
            if (EstadoActual == Estado.Piloteado) { SoltarControl(); EstadoActual = Estado.Quieto; }
            else TomarControl();
            return;
        }

        if (Input.GetKeyDown(teclaMandar) && EstadoActual != Estado.Piloteado)
        {
            if (EstadoActual == Estado.Siguiendo) Mandar();
            else Volver();
        }
    }

    /// <summary>Lo manda volando al punto que estás apuntando (o al alcance máximo si apuntás al aire).</summary>
    public void Mandar()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        destino = Physics.Raycast(ray, out RaycastHit hit, alcanceMandar, capasChoque, QueryTriggerInteraction.Ignore)
            ? hit.point + hit.normal * separacionSuperficie
            : ray.GetPoint(alcanceMandar);
        EstadoActual = Estado.YendoAPunto;
    }

    public void Volver()
    {
        if (EstadoActual == Estado.Piloteado) SoltarControl();
        EstadoActual = Estado.Volviendo;
    }

    // ---------- Movimiento ----------

    /// <summary>
    /// Apunta al MISMO punto que la mira, no en paralelo a la cámara: como el dron va corrido al
    /// hombro, mirando en paralelo la luz caería al costado de donde apuntás.
    /// </summary>
    private Quaternion RotacionHaciaMira()
    {
        Camera cam = Camera.main;
        if (cam == null) return jugador.rotation;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 punto = Physics.Raycast(ray, out RaycastHit hit, alcanceMandar, capasChoque, QueryTriggerInteraction.Ignore) && !EsDelJugadorODron(hit.transform)
            ? hit.point
            : ray.GetPoint(alcanceMandar);
        Vector3 dir = punto - transform.position;
        return dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir, Vector3.up) : cam.transform.rotation;
    }

    private Vector3 PosicionSeguir()
    {
        Transform cabeza = Camera.main != null ? Camera.main.transform : jugador;
        Vector3 adelantePlano = Vector3.ProjectOnPlane(cabeza.forward, Vector3.up).normalized;
        if (adelantePlano.sqrMagnitude < 0.01f) adelantePlano = jugador.forward;
        Vector3 derecha = Vector3.Cross(Vector3.up, adelantePlano);
        Vector3 objetivo = cabeza.position + derecha * offsetSeguir.x + Vector3.up * offsetSeguir.y + adelantePlano * offsetSeguir.z;

        // Que no quede metido en una pared: si entre la cabeza y el punto hay algo, se queda antes.
        Vector3 d = objetivo - cabeza.position;
        if (Physics.SphereCast(cabeza.position, radio, d.normalized, out RaycastHit hit, d.magnitude, capasChoque, QueryTriggerInteraction.Ignore)
            && !EsDelJugadorODron(hit.transform))
            objetivo = cabeza.position + d.normalized * Mathf.Max(0f, hit.distance - 0.05f);
        return objetivo;
    }

    /// <summary>Vuela hacia `objetivo` sin atravesar paredes. True cuando llegó.</summary>
    private bool VolarHacia(Vector3 objetivo)
    {
        Vector3 d = objetivo - transform.position;
        float dist = d.magnitude;
        if (dist < 0.15f) return true;

        Vector3 dir = d / dist;
        float paso = Mathf.Min(dist, velocidadVuelo * Time.deltaTime);
        if (!MoverConChoque(dir * paso)) return true; // trabado contra algo: se queda ahí

        Quaternion rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(dir, Vector3.up).sqrMagnitude > 0.01f ? Vector3.ProjectOnPlane(dir, Vector3.up) : transform.forward, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, 6f * Time.deltaTime);
        return false;
    }

    /// <summary>Mueve el dron sin meterse en las paredes. False si no se pudo mover nada.</summary>
    private bool MoverConChoque(Vector3 delta)
    {
        float largo = delta.magnitude;
        if (largo < 0.0001f) return false;
        Vector3 dir = delta / largo;
        if (Physics.SphereCast(transform.position, radio, dir, out RaycastHit hit, largo, capasChoque, QueryTriggerInteraction.Ignore)
            && !EsDelJugadorODron(hit.transform))
        {
            float libre = Mathf.Max(0f, hit.distance - 0.02f);
            transform.position += dir * libre;
            // Deslizar por la pared con lo que sobró, así no se pega al rozarla.
            Vector3 resto = Vector3.ProjectOnPlane(dir * (largo - libre), hit.normal);
            if (resto.sqrMagnitude > 0.0001f
                && !Physics.SphereCast(transform.position, radio, resto.normalized, out _, resto.magnitude, capasChoque, QueryTriggerInteraction.Ignore))
                transform.position += resto;
            return libre > 0.001f || resto.sqrMagnitude > 0.0001f;
        }
        transform.position += delta;
        return true;
    }

    private bool EsDelJugadorODron(Transform t) =>
        t == transform || t.IsChildOf(transform) || (jugador != null && (t == jugador || t.IsChildOf(jugador)));

    // ---------- Piloteo ----------

    private void TomarControl()
    {
        EstadoActual = Estado.Piloteado;
        Vector3 e = transform.eulerAngles;
        yawPiloto = e.y;
        pitchPiloto = 0f;
        if (movimientoJugador != null) movimientoJugador.movementLocked = true;
        if (mouseLook != null) mouseLook.canLook = false;
        if (pantallaCompletaAlPilotear) pantallaCompleta = true;
    }

    private void SoltarControl()
    {
        if (movimientoJugador != null) movimientoJugador.movementLocked = false;
        if (mouseLook != null) mouseLook.canLook = true;
        if (pantallaCompletaAlPilotear) pantallaCompleta = false;
    }

    private void Pilotear()
    {
        yawPiloto += Input.GetAxis("Mouse X") * sensibilidadMouse;
        pitchPiloto = Mathf.Clamp(pitchPiloto - Input.GetAxis("Mouse Y") * sensibilidadMouse, -80f, 80f);
        transform.rotation = Quaternion.Euler(0f, yawPiloto, 0f);
        if (camara != null) camara.transform.localRotation = Quaternion.Euler(pitchPiloto, 0f, 0f);

        Vector3 entrada = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (Input.GetKey(KeyCode.Space)) entrada.y += 1f;
        if (Input.GetKey(KeyCode.LeftControl)) entrada.y -= 1f;
        float vel = Input.GetKey(KeyCode.LeftShift) ? velocidadPiloteoRapido : velocidadPiloteo;

        Vector3 mundo = transform.right * entrada.x + Vector3.up * entrada.y + transform.forward * entrada.z;
        if (mundo.sqrMagnitude > 1f) mundo.Normalize();
        MoverConChoque(mundo * vel * Time.deltaTime);
    }

    // ---------- Centinelas ----------

    private void RevisarCentinelas()
    {
        if (EstadoActual == Estado.Siguiendo) return; // pegado a vos: si lo ven, te ven a vos
        foreach (Centinela c in Centinela.Activos)
        {
            if (c == null || !c.VePunto(transform.position)) continue;
            if (Time.time >= proximaAlerta)
            {
                proximaAlerta = Time.time + esperaEntreAlertas;
                Debug.Log($"[Dron] El centinela '{c.name}' vio al dron: alerta en {transform.position}.", this);
                AlertaCentinelas.Disparar(transform.position);
            }
            if (volverSiLoVen) Volver();
            return;
        }
    }

    // ---------- Ventanita ----------

    private void CrearVentanita()
    {
        var goCanvas = new GameObject("Canvas_CamaraDron");
        var canvas = goCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = goCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var goImg = new GameObject("VistaDron");
        goImg.transform.SetParent(goCanvas.transform, false);
        imagen = goImg.AddComponent<RawImage>();
        imagen.texture = rt;
        imagen.raycastTarget = false;
        rectImagen = imagen.rectTransform;

        var borde = goImg.AddComponent<Outline>();
        borde.effectColor = new Color(0.3f, 0.9f, 1f, 0.8f);
        borde.effectDistance = new Vector2(2f, -2f);
    }

    private void ActualizarVentanita()
    {
        if (imagen == null) return;
        bool afuera = EstadoActual != Estado.Siguiendo;
        bool visible = (afuera || ventanitaSiempre || pantallaCompleta) && !ModoMenu.Activo;

        imagen.enabled = visible;
        if (camara != null) camara.enabled = visible; // sin ventanita no se renderiza: ahorra rendimiento

        if (pantallaCompleta)
        {
            rectImagen.anchorMin = Vector2.zero;
            rectImagen.anchorMax = Vector2.one;
            rectImagen.pivot = new Vector2(0.5f, 0.5f);
            rectImagen.offsetMin = Vector2.zero;
            rectImagen.offsetMax = Vector2.zero;
        }
        else
        {
            rectImagen.anchorMin = rectImagen.anchorMax = new Vector2(1f, 0f);
            rectImagen.pivot = new Vector2(1f, 0f);
            rectImagen.sizeDelta = tamanoVentanita;
            rectImagen.anchoredPosition = new Vector2(-margenVentanita.x, margenVentanita.y);
        }
    }
}
