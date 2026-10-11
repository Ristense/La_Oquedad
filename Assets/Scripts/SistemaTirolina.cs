using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pistola de tirolina del jugador. Va en el mismo GameObject del Player (junto a PlayerMovement).
///
/// FLUJO:
///   0) Mano vacía + mantener click derecho → modo apuntado (zoom de cámara).
///   1) DISPARO 1 (click izquierdo en modo apuntado): se instancia un proyectil FÍSICO real
///      (ProyectilTirolina, con Rigidbody y gravedad) que vuela en arco y se clava en lo primero
///      sólido que choque. Donde se clave, ahí queda el DESTINO. Si apuntas mal, se clava mal —
///      no hay raycast mágico. Queda 1 disparo en el cargador.
///   2) DISPARO 2 (click izquierdo de nuevo): mismo proceso, pero ancla TU extremo (el "lugar
///      seguro") y crea la tirolina tensa entre ambos puntos donde se clavaron los proyectiles.
///   3) Miras la cuerda + F para engancharte y deslizarte: gravedad si hay desnivel (acelera en bajada)
///      + una velocidad mínima para poder cruzar tramos horizontales. Space para soltarte antes.
///
/// Cargador de 2 disparos = exactamente una tirolina. Recargas con R.
/// El movimiento espeja al SistemaRapel: bloquea PlayerMovement y mueve el transform con el Rigidbody
/// en kinematic, para no pelear con las físicas normales.
/// </summary>
public class SistemaTirolina : MonoBehaviour
{
    [Header("Modo Apuntado (Click Derecho, mano vacía)")]
    [Tooltip("ManoJugador del player: mientras tenga algo en la mano, no se puede entrar en modo tirolina. " +
             "Si se deja vacío, se busca automáticamente en este mismo GameObject.")]
    public ManoJugador manoJugador;
    [Tooltip("LectorNotas del player: mientras sostenga una nota (Click Izq ocupado sosteniéndola), tampoco " +
             "se puede entrar en modo tirolina — ambos sistemas usan Click Izquierdo y se pisan. " +
             "Si se deja vacío, se busca automáticamente en este mismo GameObject.")]
    public LectorNotas lectorNotas;
    [Tooltip("ControladorMochila: mientras la mochila está ABIERTA (al frente de la cámara) no se puede " +
             "apuntar — ambos sistemas usan Click Derecho (la mochila para rotarse) y se pisan, además de " +
             "que la mochila tapa la vista. Si se deja vacío, se busca automáticamente en la escena.")]
    public ControladorMochila controladorMochila;
    [Tooltip("FOV de la cámara al apuntar (zoom). El FOV normal se guarda solo al entrar en juego.")]
    public float fovApuntado = 45f;
    [Tooltip("Velocidad con la que la cámara interpola hacia el FOV de apuntado y de vuelta al normal.")]
    public float velocidadZoom = 10f;
    public bool EnModoApuntado { get; private set; }

    [Header("UI: qué mostrar/ocultar al entrar en modo apuntado")]
    [Tooltip("El punto de mira normal (el de siempre). Se OCULTA mientras apuntas la tirolina.")]
    public GameObject miraNormal;
    [Tooltip("El slider de intensidad de la linterna. Se OCULTA mientras apuntas la tirolina y se " +
             "REACTIVA al salir del modo apuntado.")]
    public GameObject sliderLinterna;
    [Tooltip("El punto de mira específico de la tirolina (el que hiciste para el zoom). Se MUESTRA solo " +
             "mientras apuntas; el resto del tiempo queda oculto.")]
    public GameObject miraTirolina;

    [Header("Disparo / Anclaje (proyectil físico)")]
    [Tooltip("Prefab del pincho: necesita Rigidbody + Collider + el script ProyectilTirolina. Obligatorio.")]
    public GameObject prefabProyectil;
    [Tooltip("Velocidad de salida del proyectil (m/s). La gravedad real lo va curvando en el aire, así que " +
             "hay que aprender a apuntar un poco más arriba para tiros largos.")]
    public float velocidadProyectil = 45f;
    [Tooltip("Distancia máxima que se dibuja/considera para el arco de previsualización (metros).")]
    public float alcanceMaximo = 40f;
    [Tooltip("Qué capas puede considerar el arco de previsualización como 'sólido' para dibujar dónde pegaría. " +
             "El proyectil real se clava en CUALQUIER collider sólido que choque, esto es solo para el preview.")]
    public LayerMask capasAnclaje = ~0;
    [Tooltip("Opcional: punto por donde 'sale' físicamente el proyectil y la cuerda (la boca del arma). Si se " +
             "deja vacío, sale del centro de la cámara.")]
    public Transform boca;

    [Header("Sonido")]
    [Tooltip("AudioSource del arma (va en el Player o en la boca del arma). Si se deja vacío se busca uno en este GameObject; si tampoco hay, se agrega uno solo. Todos los clips son OPCIONALES: el que dejes vacío simplemente no suena.")]
    public AudioSource fuenteSonido;
    [Tooltip("Disparo. Si ponés varios, se elige uno al azar en cada tiro para que no se sienta repetitivo (mismo criterio que los pasos del jugador).")]
    public AudioClip[] sonidosDisparo;
    [Tooltip("El dardo se CLAVA en algo. Suena en el punto del impacto, no en el jugador, así que se escucha lejos y con caída por distancia.")]
    public AudioClip sonidoImpacto;
    [Tooltip("El dardo se pierde en el vacío sin clavarse en nada.")]
    public AudioClip sonidoFallo;
    [Tooltip("La cuerda se TENSA: los dos anclajes quedaron puestos y la tirolina quedó armada. Es el momento de 'listo, ya podés cruzar'.")]
    public AudioClip sonidoTirolinaLista;
    [Tooltip("Te enganchás a la cuerda.")]
    public AudioClip sonidoEnganche;
    [Tooltip("Bucle mientras te deslizás. Se prende al engancharte y se corta al soltarte — no hace falta que el clip esté marcado como Loop, lo fuerza el código.")]
    public AudioClip sonidoDeslizando;
    [Tooltip("Recarga (tecla R).")]
    public AudioClip sonidoRecarga;
    [Range(0f, 1f)]
    [Tooltip("Volumen para todos los sonidos de la tirolina.")]
    public float volumenSonidos = 0.8f;

    [Header("Consumo de Batería")]
    [Tooltip("Segundos de batería que gasta CADA disparo (acierte o falle). Una tirolina completa sin fallar " +
             "cuesta 2× esto. Usa el mismo recurso 'energySeconds' de PlayerData que la linterna.")]
    public float costoEnergiaPorDisparo = 20f;
    [Tooltip("PlayerData del jugador. Si se deja vacío, se busca automáticamente.")]
    public PlayerData playerData;

    [Header("Munición")]
    public int disparosPorCargador = 2;
    public KeyCode teclaRecarga = KeyCode.R;
    [Tooltip("Slider opcional para mostrar la munición (0..disparosPorCargador).")]
    public Slider sliderMunicion;
    [Tooltip("Objetos que representan cada dardo en el arma (los PNG Municion_1, Municion_2...). Se apagan en el ORDEN DEL ARRAY a medida que se gasta el cargador: el del slot 0 es el primero en desaparecer. Poné tantos como disparosPorCargador.")]
    public GameObject[] iconosMunicion;

    [Header("Enganche / Recorrido")]
    public KeyCode teclaEnganche = KeyCode.E;
    [Tooltip("Distancia a la que puedes mirar la cuerda para engancharte.")]
    public float distanciaEnganche = 5f;
    [Tooltip("Velocidad mínima (m/s) EN EL SENTIDO QUE ESTÁS EMPUJANDO con W/S: permite cruzar tramos horizontales o levemente cuesta arriba. Sin tocar nada no se aplica — ahí mandan la gravedad y la inercia.")]
    public float velocidadMinima = 6f;
    [Tooltip("Con cuánta fuerza W (adelante) y S (atrás) te impulsan a lo largo de la cuerda (m/s²). Subilo para un control más inmediato, bajalo para que se sienta más pesado.")]
    public float aceleracionEntrada = 25f;
    [Tooltip("Cuánto te vas frenando por segundo (m/s²) cuando NO tocás W ni S. En 0 quedás deslizándote por inercia para siempre en tramos planos.")]
    public float rozamientoSinEntrada = 8f;
    [Tooltip("Velocidad máxima (m/s) a la que puedes acelerar por gravedad en bajada.")]
    public float velocidadMaxima = 30f;
    [Tooltip("Cuánta 'gravedad' aporta la pendiente (más alto = acelera más fuerte en las bajadas).")]
    public float factorGravedad = 12f;
    [Tooltip("Cuánto cuelga el jugador por debajo de la cuerda mientras se desliza (metros).")]
    public float colgadoDebajo = 1.2f;

    [Header("Cuerda (visual)")]
    [Tooltip("Material de la cuerda. En HDRP debe ser HDRP/Unlit o similar. Si se deja vacío se crea uno automáticamente.")]
    public Material materialCuerda;
    public float anchoCuerda = 0.06f;
    [ColorUsage(true, true)] public Color colorCuerda = Color.white;

    [Header("Previsualización del disparo")]
    [Tooltip("LineRenderer opcional para ver hacia dónde dispararás (verde = pega algo, rojo = fuera de rango).")]
    public LineRenderer lineaPreview;
    [ColorUsage(true, true)] public Color colorValido = Color.green;
    [ColorUsage(true, true)] public Color colorInvalido = Color.red;
    [Tooltip("Texto (TMP) que muestra la distancia en metros entre vos y hacia dónde apuntas, mientras estás " +
             "en modo tirolina. Mismo color verde/rojo que la línea de preview, para feedback de si estás en rango.")]
    public TMP_Text textoDistancia;
    [Tooltip("Cuántos segmentos tiene el arco de preview dibujado (más = más suave, más caro).")]
    public int segmentosArco = 24;
    [Tooltip("Paso de tiempo (segundos) de cada segmento simulado del arco de preview.")]
    public float pasoTiempoArco = 0.05f;

    // --- Estado interno ---
    // Fallar (el proyectil no se clava en nada) NO gasta el cargador — el dardo va atado a la
    // misma cuerda, así que un tiro perdido se "recupera" y puedes reintentar. Solo si fallas DOS
    // veces seguidas sin haber clavado nada en el medio, el cargador queda vacío y hay que recargar.
    // Cualquier anclaje exitoso resetea la racha de fallos a cero.
    private int fallosConsecutivos = 0;
    private bool destinoAnclado = false;
    private Vector3 anclaDestino;
    private bool proyectilEnVuelo = false;
    private Collider miCollider;

    private bool enTirolina = false;
    /// <summary>Viajando colgado de la cuerda. Lo lee AmbientSounds para su capa de "trepando".</summary>
    public bool EnTirolina => enTirolina;
    private Vector3 rideOrigen, rideDestino;
    private float rideT;    // 0..1 a lo largo de la cuerda
    private float rideVel;  // velocidad actual (m/s)

    private Camera cam;
    private Rigidbody rb;
    private PlayerMovement movimiento;
    private float fovNormal;

    void Start()
    {
        cam = Camera.main;
        rb = GetComponent<Rigidbody>();
        miCollider = GetComponent<Collider>();
        movimiento = GetComponent<PlayerMovement>();
        if (manoJugador == null) manoJugador = GetComponent<ManoJugador>();
        if (lectorNotas == null) lectorNotas = GetComponent<LectorNotas>();
        if (controladorMochila == null) controladorMochila = FindAnyObjectByType<ControladorMochila>();
        if (playerData == null) playerData = GetComponent<PlayerData>();
        if (playerData == null) playerData = FindAnyObjectByType<PlayerData>();
        // El AudioSource se resuelve solo: primero el asignado, después uno en este GameObject, y
        // si no hay ninguno se crea. Así el sistema nunca queda mudo por un slot sin llenar.
        if (fuenteSonido == null) fuenteSonido = GetComponent<AudioSource>();
        if (fuenteSonido == null)
        {
            fuenteSonido = gameObject.AddComponent<AudioSource>();
            fuenteSonido.playOnAwake = false;
        }

        fallosConsecutivos = 0;
        SetActivoUiMunicion(false);
        if (lineaPreview != null) lineaPreview.enabled = false;
        if (textoDistancia != null) textoDistancia.gameObject.SetActive(false);
        if (miraTirolina != null) miraTirolina.SetActive(false);
        if (cam != null) fovNormal = cam.fieldOfView;
    }

    void Update()
    {
        // En los menús y durante el VIAJE DE CÁMARA del menú no se apunta, no se dispara y no se
        // hace zoom: eso es solo de juego. ModoMenu.Activo lo prende MenuInicial al arrancar y
        // recién lo apaga cuando la cámara termina de llegar al jugador, así que cubre las dos
        // cosas de una. Mismo guard que ya tenían PlayerMovement, MouseLook y ManoJugador —
        // SistemaTirolina era el único sistema del jugador al que le faltaba.
        if (ModoMenu.Activo)
        {
            // Si por lo que sea quedó apuntando, se sale ORDENADO (devuelve la mira normal y
            // apaga la munición) en vez de dejar el HUD a medio camino.
            if (EnModoApuntado) SalirModoApuntado();

            // El FOV solo se toca si de verdad quedó zoomeado. Durante el menú la cámara la
            // maneja MenuInicial (DOPath), y no queremos pelearle por el FOV sin necesidad.
            if (cam != null && !Mathf.Approximately(cam.fieldOfView, fovNormal)) ActualizarZoom();
            return;
        }

        // Mientras te deslizas, solo corre la lógica del recorrido.
        if (enTirolina)
        {
            ActualizarRecorrido();
            return;
        }

        // Recarga: limpia la racha de fallos y cancela cualquier anclaje a medio hacer.
        if (Input.GetKeyDown(teclaRecarga) && (fallosConsecutivos > 0 || destinoAnclado))
        {
            fallosConsecutivos = 0;
            destinoAnclado = false;
            ActualizarMunicionUI();
            Sonar(sonidoRecarga);
        }

        // Solo se puede entrar en modo tirolina con la mano vacía: sin item de ManoJugador
        // Y sin nota sostenida de LectorNotas (que ocupa Click Izquierdo para mantenerse en mano).
        bool manoVacia = (manoJugador == null || manoJugador.ItemEnMano == null)
                       && (lectorNotas == null || !lectorNotas.SostieneNota);
        // La mochila abierta también bloquea: usa Click Derecho para rotarse (se pisaban) y
        // además está literalmente delante de la cámara.
        bool mochilaAbierta = controladorMochila != null && controladorMochila.Abierta;
        // La tablet abierta con Click Derecho mantenido usa ese mismo botón para liberar el mouse
        // sobre su interfaz (ver TabletaMouse): sin este guard, abrir la tablet y clickear un botón
        // metía zoom y te cambiaba la mira al mismo tiempo.
        bool quiereApuntar = manoVacia && !mochilaAbierta && !ModoTablet.UsandoMouse && Input.GetMouseButton(1);

        if (quiereApuntar && !EnModoApuntado) EntrarModoApuntado();
        else if (!quiereApuntar && EnModoApuntado) SalirModoApuntado();

        ActualizarZoom();

        if (EnModoApuntado)
        {
            ActualizarPreview();

            if (Input.GetMouseButtonDown(0) && fallosConsecutivos < disparosPorCargador && !proyectilEnVuelo)
                Disparar();
        }
        else
        {
            if (lineaPreview != null) lineaPreview.enabled = false;
            if (textoDistancia != null) textoDistancia.gameObject.SetActive(false);
        }

        DetectarEnganche();
    }

    private void EntrarModoApuntado()
    {
        EnModoApuntado = true;
        if (miraNormal != null) miraNormal.SetActive(false);
        if (sliderLinterna != null) sliderLinterna.SetActive(false);
        if (miraTirolina != null) miraTirolina.SetActive(true);
        SetActivoUiMunicion(true);
        ActualizarMunicionUI(); // repone los iconos según el cargador actual, no todos prendidos
    }

    private void SalirModoApuntado()
    {
        EnModoApuntado = false;
        if (miraNormal != null) miraNormal.SetActive(true);
        if (sliderLinterna != null) sliderLinterna.SetActive(true);
        if (miraTirolina != null) miraTirolina.SetActive(false);
        SetActivoUiMunicion(false);
        // Ojo: NO reseteamos destinoAnclado acá — si sueltas el click derecho a medio camino
        // (por ejemplo para reposicionarte) conservas el primer anclaje ya hecho. Para cancelarlo
        // del todo, usa la tecla de recarga.
    }

    /// <summary>Prende/apaga el slider y los iconos de munición de un saque (fuera de modo apuntado no
    /// tiene sentido mostrarlos: el único HUD visible en reposo es la mira normal).</summary>
    private void SetActivoUiMunicion(bool activo)
    {
        if (sliderMunicion != null) sliderMunicion.gameObject.SetActive(activo);
        if (iconosMunicion == null) return;
        foreach (GameObject icono in iconosMunicion)
        {
            if (icono != null) icono.SetActive(activo);
        }
    }

    private void ActualizarZoom()
    {
        if (cam == null) return;
        float objetivo = EnModoApuntado ? fovApuntado : fovNormal;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, objetivo, velocidadZoom * Time.deltaTime);
    }

    private Ray RayoMira()
    {
        return cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
    }

    /// <summary>Reproduce un clip en el jugador. Null = no suena, sin quejarse.</summary>
    private void Sonar(AudioClip clip)
    {
        if (clip == null || fuenteSonido == null) return;
        fuenteSonido.PlayOneShot(clip, volumenSonidos);
    }

    /// <summary>Uno al azar del array, para que repetir la acción no suene siempre igual.</summary>
    private void Sonar(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        Sonar(clips[Random.Range(0, clips.Length)]);
    }

    /// <summary>
    /// Suena en un punto del MUNDO, no en el jugador: el impacto del dardo puede caer a 40m y tiene
    /// que escucharse desde allá, con su caída por distancia.
    /// </summary>
    private void SonarEnPunto(AudioClip clip, Vector3 punto)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, punto, volumenSonidos);
    }

    /// <summary>Prende o corta el bucle del deslizamiento.</summary>
    private void SonidoDeslizamiento(bool encendido)
    {
        if (fuenteSonido == null) return;

        if (encendido)
        {
            if (sonidoDeslizando == null) return;
            fuenteSonido.clip = sonidoDeslizando;
            fuenteSonido.loop = true;
            fuenteSonido.volume = volumenSonidos;
            fuenteSonido.Play();
        }
        else if (fuenteSonido.clip == sonidoDeslizando && sonidoDeslizando != null)
        {
            // Solo se corta si lo que suena es NUESTRO bucle: si no, un Stop() acá mataría
            // cualquier otro sonido que estuviera usando el mismo AudioSource.
            fuenteSonido.Stop();
            fuenteSonido.loop = false;
            fuenteSonido.clip = null;
        }
    }

    private void Disparar()
    {
        if (prefabProyectil == null)
        {
            Debug.LogWarning("[SistemaTirolina] Falta asignar 'Prefab Proyectil' en el Inspector.");
            return;
        }

        if (playerData != null && playerData.energySeconds < costoEnergiaPorDisparo)
        {
            // Sin batería suficiente para este disparo — no sale el proyectil ni se gasta nada.
            return;
        }

        Ray ray = RayoMira();
        Vector3 origen = boca != null ? boca.position : ray.origin;

        GameObject go = Instantiate(prefabProyectil, origen, Quaternion.LookRotation(ray.direction));
        ProyectilTirolina proyectil = go.GetComponent<ProyectilTirolina>();
        if (proyectil == null)
        {
            Debug.LogWarning("[SistemaTirolina] El prefab del proyectil no tiene el script ProyectilTirolina.");
            Destroy(go);
            return;
        }

        proyectilEnVuelo = true;

        // Va acá y no antes: si faltaba el prefab o la batería, la función ya salió por un return
        // y no tiene que sonar un disparo que nunca ocurrió.
        Sonar(sonidosDisparo);

        if (playerData != null)
        {
            playerData.energySeconds = Mathf.Max(0f, playerData.energySeconds - costoEnergiaPorDisparo);
            playerData.MostrarPopupEnergia(-costoEnergiaPorDisparo);
        }

        proyectil.Lanzar(ray.direction * velocidadProyectil, miCollider, OnProyectilImpacto, OnProyectilFallo);
    }

    private void OnProyectilImpacto(Vector3 punto)
    {
        proyectilEnVuelo = false;
        fallosConsecutivos = 0; // anclaje exitoso: se recupera toda la racha de fallos
        ActualizarMunicionUI();

        SonarEnPunto(sonidoImpacto, punto);

        if (!destinoAnclado)
        {
            // 1er disparo → ancla el destino.
            anclaDestino = punto;
            destinoAnclado = true;
        }
        else
        {
            // 2do disparo → clavó, pero la cuerda solo se arma si la distancia REAL entre ambos
            // anclajes entra dentro de alcanceMaximo. El proyectil ya voló con física libre, así que
            // puede haber llegado más lejos de lo que la cuerda te alcanza a cubrir.
            float distanciaEntrePuntos = Vector3.Distance(anclaDestino, punto);
            if (distanciaEntrePuntos <= alcanceMaximo)
            {
                Tirolina.Crear(anclaDestino, punto, materialCuerda, anchoCuerda, colorCuerda);
                destinoAnclado = false;
                Sonar(sonidoTirolinaLista); // la cuerda quedó tensa: "ya podés cruzar"
            }
            else
            {
                // No alcanza: el 2do anclaje se queda clavado en el mundo (inofensivo, decorativo),
                // pero mantenemos el 1er anclaje activo para que reintentes el 2do disparo más cerca.
                Debug.Log($"[SistemaTirolina] Los anclajes quedaron a {distanciaEntrePuntos:F1}m " +
                          $"(máximo {alcanceMaximo}m) — la cuerda no alcanza, no se armó.");
            }
        }
    }

    private void OnProyectilFallo()
    {
        // El proyectil se autodestruyó sin pegarle a nada (voló al vacío). Como va atado a la misma
        // cuerda, un tiro perdido se "recupera" — no cuenta como gastado salvo que sea el 2do fallo
        // seguido, ahí sí se vacía el cargador y hay que recargar con R.
        proyectilEnVuelo = false;
        fallosConsecutivos++;
        ActualizarMunicionUI();

        Sonar(sonidoFallo);
    }

    private readonly List<Vector3> puntosArco = new List<Vector3>();

    /// <summary>
    /// Simula la misma trayectoria balística que tendría el proyectil real (misma velocidad inicial +
    /// gravedad de Physics.gravity), parando en el primer collider sólido que "tocaría" o al llegar a
    /// alcanceMaximo. Así el jugador ve una curva real, no una línea recta que miente sobre dónde cae.
    /// </summary>
    private void ActualizarPreview()
    {
        Ray ray = RayoMira();
        Vector3 origen = boca != null ? boca.position : ray.origin;
        Vector3 pos = origen;
        Vector3 vel = ray.direction * velocidadProyectil;
        Vector3 gravedad = Physics.gravity;

        puntosArco.Clear();
        puntosArco.Add(pos);

        bool pega = false;
        Vector3 puntoFinal = pos;
        float distanciaAcumulada = 0f;

        for (int i = 0; i < segmentosArco; i++)
        {
            Vector3 siguientePos = pos + vel * pasoTiempoArco + 0.5f * gravedad * pasoTiempoArco * pasoTiempoArco;
            Vector3 siguienteVel = vel + gravedad * pasoTiempoArco;

            if (Physics.Linecast(pos, siguientePos, out RaycastHit hit, capasAnclaje))
            {
                puntosArco.Add(hit.point);
                puntoFinal = hit.point;
                pega = true;
                break;
            }

            distanciaAcumulada += Vector3.Distance(pos, siguientePos);
            puntosArco.Add(siguientePos);
            puntoFinal = siguientePos;
            pos = siguientePos;
            vel = siguienteVel;

            if (distanciaAcumulada >= alcanceMaximo) break;
        }

        // "Válido" = pegó en algo sólido Y dentro del alcance considerado (más allá, muy poco control real).
        bool valido = pega && distanciaAcumulada <= alcanceMaximo;
        Color c = valido ? colorValido : colorInvalido;

        if (lineaPreview != null)
        {
            lineaPreview.enabled = true;
            lineaPreview.positionCount = puntosArco.Count;
            lineaPreview.SetPositions(puntosArco.ToArray());
            lineaPreview.startColor = c;
            lineaPreview.endColor = c;
        }

        if (textoDistancia != null)
        {
            // Distancia real entre VOS (el jugador) y el punto donde caería, no la cámara.
            float distancia = Vector3.Distance(transform.position, puntoFinal);
            textoDistancia.gameObject.SetActive(true);
            textoDistancia.text = $"{distancia:F1} m";
            textoDistancia.color = c;
        }
    }

    private void DetectarEnganche()
    {
        Ray ray = RayoMira();
        if (Physics.Raycast(ray, out RaycastHit hit, distanciaEnganche))
        {
            Tirolina t = hit.collider.GetComponentInParent<Tirolina>();
            if (t != null)
            {
                if (TooltipManager.Instance != null)
                    TooltipManager.Instance.MostrarTooltip(t.gameObject, "[E] Tirolina");

                if (Input.GetKeyDown(teclaEnganche))
                {
                    if (TooltipManager.Instance != null) TooltipManager.Instance.OcultarTooltip();
                    // hit.point es el punto EXACTO de la cuerda que estabas mirando: ahí te colgás.
                    Engancharse(t, hit.point);
                }
            }
        }
    }

    private void Engancharse(Tirolina t, Vector3 puntoAgarre)
    {
        enTirolina = true;

        Sonar(sonidoEnganche);
        SonidoDeslizamiento(true);

        rideOrigen = t.ExtremoCercano(transform.position);
        rideDestino = t.ExtremoLejano(transform.position);

        // Te colgás DONDE agarraste, no en la punta. Antes esto era rideT = 0, que te teletransportaba
        // al extremo más cercano por más que hubieras enganchado en el medio del tramo.
        // Proyección del punto de agarre sobre el segmento origen→destino, normalizada a 0..1.
        Vector3 delta = rideDestino - rideOrigen;
        float largoCuadrado = delta.sqrMagnitude;
        rideT = largoCuadrado > 0.0001f
            ? Mathf.Clamp01(Vector3.Dot(puntoAgarre - rideOrigen, delta) / largoCuadrado)
            : 0f;

        // Arrancás quieto: el movimiento sale de la gravedad según la pendiente (y de W/S), no de un
        // empujón de salida. Colgarte en un tramo plano te deja ahí hasta que empujes.
        rideVel = 0f;

        if (movimiento != null) movimiento.movementLocked = true;
        if (rb != null) rb.isKinematic = true;

        ColocarEnCuerda();
    }

    private void ActualizarRecorrido()
    {
        // Soltarte antes de tiempo.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Desengancharse();
            return;
        }

        float longitud = Vector3.Distance(rideOrigen, rideDestino);
        if (longitud < 0.01f) { Desengancharse(); return; }

        Vector3 dir = (rideDestino - rideOrigen) / longitud;

        // rideVel es una velocidad CON SIGNO a lo largo de la cuerda: + va hacia el extremo lejano,
        // − vuelve hacia el extremo por el que te enganchaste.
        float entrada = Input.GetAxisRaw("Vertical"); // W = +1, S = −1

        // Gravedad según pendiente: dir.y < 0 (bajando) → empuja hacia el destino. Se aplica siempre,
        // vayas para donde vayas, así retroceder cuesta arriba se siente pesado de verdad.
        rideVel += (-dir.y) * factorGravedad * Time.deltaTime;

        // Impulso del jugador.
        rideVel += entrada * aceleracionEntrada * Time.deltaTime;

        // Sin manos: te vas frenando. Es lo que hace que soltar W se note.
        if (Mathf.Approximately(entrada, 0f))
            rideVel = Mathf.MoveTowards(rideVel, 0f, rozamientoSinEntrada * Time.deltaTime);

        rideVel = Mathf.Clamp(rideVel, -velocidadMaxima, velocidadMaxima);

        // El piso de velocidad solo vale EN EL SENTIDO QUE PEDÍS. Antes era un mínimo absoluto hacia
        // adelante, que con control manual te haría imposible frenar o volver.
        if (entrada > 0f) rideVel = Mathf.Max(rideVel, velocidadMinima);
        else if (entrada < 0f) rideVel = Mathf.Min(rideVel, -velocidadMinima);

        rideT += (rideVel / longitud) * Time.deltaTime;

        if (rideT >= 1f)
        {
            rideT = 1f;
            ColocarEnCuerda();     // te deja colgando en el extremo final...
            Desengancharse();      // ...y reactiva la gravedad para que aterrices.
            return;
        }

        if (rideT <= 0f)
        {
            // Volviste al extremo de partida: te quedás colgando ahí en vez de soltarte solo.
            // Si te soltara, mantener S apretado te tiraría al vacío sin querer.
            rideT = 0f;
            rideVel = 0f;
        }

        ColocarEnCuerda();
    }

    private void ColocarEnCuerda()
    {
        Vector3 puntoCuerda = Vector3.Lerp(rideOrigen, rideDestino, rideT);
        transform.position = puntoCuerda + Vector3.down * colgadoDebajo;
    }

    private void Desengancharse()
    {
        enTirolina = false;

        SonidoDeslizamiento(false);

        if (rb != null) rb.isKinematic = false;
        if (movimiento != null)
        {
            movimiento.movementLocked = false;
            movimiento.BloquearSpaceHastaSoltar(); // evita el fly-up instantáneo al soltar (igual que el rapel)
        }
    }

    private void ActualizarMunicionUI()
    {
        // Lo que de verdad se gasta es el MARGEN DE FALLO: clavar el dardo recupera la racha entera
        // (ver OnProyectilImpacto), así que este número sube solo cuando el tiro se pierde.
        int gastados = Mathf.Clamp(fallosConsecutivos, 0, disparosPorCargador);

        if (sliderMunicion != null)
        {
            sliderMunicion.maxValue = disparosPorCargador;
            // Muestra cuánto margen de fallo te queda (lleno = sin fallos acumulados, vacío = toca recargar).
            sliderMunicion.value = disparosPorCargador - gastados;
        }

        // Iconos: se apagan en el ORDEN DEL ARRAY. El de índice 0 es el primero en irse, así que
        // arrastrá Municion_1 al slot 0 y Municion_2 al 1. Se vuelven a prender solos al recargar
        // o al clavar un anclaje, porque ahí el cargador se recupera de verdad.
        //
        // Solo se tocan si estamos apuntando: fuera de modo apuntado están todos apagados por
        // SetActivoUiMunicion (lo único visible en reposo es la mira normal), y forzar el SetActive
        // acá los reencendería sin querer (ej. al recargar con R sin estar apuntando).
        if (iconosMunicion == null || !EnModoApuntado) return;
        for (int i = 0; i < iconosMunicion.Length; i++)
        {
            if (iconosMunicion[i] == null) continue;
            iconosMunicion[i].SetActive(i >= gastados);
        }
    }
}
