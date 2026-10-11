using UnityEngine;

/// <summary>
/// Cada cierto tiempo random, toma una pieza exterior (pared, piso, techo, arista, pilar)
/// cerca del jugador y la "desprende": le agrega Rigidbody, le aplica una fuerza en una
/// dirección random horizontal (nunca hacia arriba) + un torque para que rote, y la
/// destruye después de unos segundos. Da la sensación de mega-estructura abandonada
/// que se está desarmando.
/// </summary>
public class SistemaDesprendimientos : MonoBehaviour
{
    [Header("Referencias")]
    public Transform jugador;
    public WorldGenerator worldGen;

    [Header("Frecuencia")]
    [Tooltip("Segundos mínimos entre desprendimientos.")]
    public float intervaloMin = 20f;
    public float intervaloMax = 60f;
    [Tooltip("Gracia (s) desde que el MUNDO queda listo hasta que puede caer la primera pieza. Existe para que el arranque de partida no venga siempre con un derrumbe de bienvenida: dejá que el jugador se ubique antes de que la estructura empiece a desarmarse.")]
    public float esperaInicialMin = 45f;
    public float esperaInicialMax = 150f;
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que, al vencer el temporizador, REALMENTE se desprenda algo. Abajo de 1 los desprendimientos dejan de caer en un ritmo reconocible: a veces pasan dos seguidos, a veces se hace un silencio largo. En 1 vuelve al comportamiento de reloj.")]
    public float probabilidadDesprendimiento = 0.6f;

    [Header("Búsqueda")]
    [Tooltip("Radio (m) alrededor del jugador donde se busca una pieza exterior.")]
    public float radioBusqueda = 100f;
    [Tooltip("Palabras clave en el NOMBRE del GameObject que identifican una pieza exterior desprendible. Si el nombre contiene cualquiera, es candidato.")]
    public string[] palabrasClaveExterior = new string[] {
        "_externo",          // piso/techo exterior (ej: 'Piso.Completo_piso_externo')
        "ParedExterior",      // paredes exteriores
        "Pilar",              // pilares de paredes exteriores
        "AristaExterior"      // aristas
    };

    [Header("Física")]
    [Tooltip("Masa que se le asigna al Rigidbody al desprenderse.")]
    public float masa = 50f;
    [Tooltip("Velocidad inicial mínima del empujón (m/s).")]
    public float velocidadMin = 2f;
    [Tooltip("Velocidad inicial máxima del empujón (m/s).")]
    public float velocidadMax = 6f;
    [Tooltip("Torque inicial random para que la pieza rote mientras cae (rad/s aprox).")]
    public float torqueMax = 5f;

    [Header("Limpieza")]
    [Tooltip("Segundos después de los cuales la pieza se destruye.")]
    public float tiempoVida = 5f;

    [Header("Sonido")]
    [Tooltip("Clips de sonido para reproducir cuando se desprende la pieza.")]
    public AudioClip[] sonidosDesprendimiento;
    [Range(0f, 1f)] public float volumenSonido = 0.8f;
    public float sonidoMinDistance = 5f;
    public float sonidoMaxDistance = 100f;

    [Header("Debug")]
    public bool logDebug = false;

    private float temporizador;
    private float proximoEvento;
    private bool mundoEstabaListo;

    void Start()
    {
        BuscarReferencias();
        ProgramarSiguiente();
    }

    private void BuscarReferencias()
    {
        if (jugador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) jugador = p.transform;
        }
        // Reintentable: si el WorldGenerator todavía no corrió su Awake cuando este Start se
        // ejecutó, Instance era null y el sistema quedaba muerto para siempre.
        if (worldGen == null) worldGen = WorldGenerator.Instance;
    }

    void Update()
    {
        if (jugador == null || worldGen == null)
        {
            BuscarReferencias();
            return;
        }

        // El reloj NO corre hasta que el mundo está listo. Antes arrancaba apenas existían las
        // referencias, así que el rato en el menú y la intro contaba como espera: para cuando
        // empezabas a jugar el temporizador ya había vencido y la primera pieza caía en el primer
        // frame, siempre. Además se reinicia en cada transición a listo (partida nueva incluida).
        bool listo = worldGen.MundoListo;
        if (listo != mundoEstabaListo)
        {
            mundoEstabaListo = listo;
            if (listo)
            {
                temporizador = 0f;
                proximoEvento = Random.Range(esperaInicialMin, esperaInicialMax);
                if (logDebug) Debug.Log($"[Desprendimientos] Mundo listo — primera pieza en {proximoEvento:F0}s.");
            }
        }
        if (!listo) return;

        temporizador += Time.deltaTime;
        if (temporizador < proximoEvento) return;

        // Reprogramar SIEMPRE, se desprenda algo o no: el tiro fallido consume su turno y el
        // siguiente vuelve a sortearse, que es lo que rompe el ritmo de metrónomo.
        temporizador = 0f;
        ProgramarSiguiente();

        if (Random.value > probabilidadDesprendimiento)
        {
            if (logDebug) Debug.Log("[Desprendimientos] Turno salteado por probabilidad.");
            return;
        }

        Desprender();
    }

    private void ProgramarSiguiente()
    {
        proximoEvento = Random.Range(intervaloMin, intervaloMax);
    }

    private void Desprender()
    {
        GameObject pieza = worldGen.TomarPiezaExteriorAleatoria(jugador.position, radioBusqueda, palabrasClaveExterior);
        if (pieza == null)
        {
            if (logDebug) Debug.Log("[Desprendimientos] No se encontró pieza exterior cercana.");
            return;
        }

        // Asegurar collider físico (si no tiene MeshCollider, agregar BoxCollider)
        if (pieza.GetComponent<Collider>() == null)
        {
            pieza.AddComponent<BoxCollider>();
        }

        // Rigidbody
        Rigidbody rb = pieza.GetComponent<Rigidbody>();
        if (rb == null) rb = pieza.AddComponent<Rigidbody>();
        rb.mass = masa;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Empujón: dirección random horizontal con componente Y entre 0 y -0.3 (nunca hacia arriba)
        Vector2 horiz = Random.insideUnitCircle.normalized;
        float yComp = Random.Range(-0.3f, 0f);
        Vector3 dir = new Vector3(horiz.x, yComp, horiz.y).normalized;
        float vel = Random.Range(velocidadMin, velocidadMax);
        rb.linearVelocity = dir * vel;

        // Torque random para que rote mientras cae
        rb.angularVelocity = Random.insideUnitSphere * torqueMax;

        // Reproducir sonido que siga al objeto al caer
        if (sonidosDesprendimiento != null && sonidosDesprendimiento.Length > 0)
        {
            AudioClip clip = sonidosDesprendimiento[Random.Range(0, sonidosDesprendimiento.Length)];
            if (clip != null)
            {
                AudioSource src = pieza.AddComponent<AudioSource>();
                src.clip = clip;
                src.spatialBlend = 1f; // 3D
                src.volume = volumenSonido;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = sonidoMinDistance;
                src.maxDistance = sonidoMaxDistance;
                src.playOnAwake = false;
                src.loop = false;
                src.Play();
            }
        }

        // Destrucción programada
        Destroy(pieza, tiempoVida);

        if (logDebug) Debug.Log($"[Desprendimientos] '{pieza.name}' desprendido. vel={vel:F1}, dir={dir}");
    }
}
