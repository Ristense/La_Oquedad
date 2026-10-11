using UnityEngine;

/// <summary>
/// Sistema de sonidos ambientales que maneja una pista base,
/// un viento dinámico que se activa cuando el jugador está expuesto al vacío,
/// ráfagas de viento aleatorias, y sonidos manuales con teclado.
/// </summary>
/// <summary>Situaciones que pueden pedir una capa de ambiente. NO son excluyentes entre sí.</summary>
public enum ContextoAmbiente
{
    /// <summary>Dentro de una sala tipo vivienda (macro chunk tipo 1).</summary>
    Vivienda,
    /// <summary>Bajo techo pero fuera de una vivienda: pasillos, cruces, pozos.</summary>
    Pasillo,
    /// <summary>Sin techo encima (misma detección que el crossfade base/exterior).</summary>
    Exterior,
    /// <summary>Llevás varios segundos sin moverte.</summary>
    Quieto,
    /// <summary>Colgado de una escalera, un rapel o la tirolina.</summary>
    Trepando
}

[System.Serializable]
public class CapaAmbiente
{
    [Tooltip("Solo para reconocerla en el inspector.")]
    public string nombre = "Capa";
    [Tooltip("Cuándo suena esta capa.")]
    public ContextoAmbiente contexto = ContextoAmbiente.Pasillo;
    [Tooltip("Loop que se mantiene mientras el contexto esté activo. Puede quedar vacío si solo querés el sonido puntual de entrada.")]
    public AudioClip loop;
    [Range(0f, 1f)] public float volumen = 0.35f;
    [Tooltip("Velocidad del fade al entrar y salir del contexto. Bajo = transición lenta y suave.")]
    public float velocidadFade = 1.5f;

    [Tooltip("Sonido puntual al ENTRAR en el contexto (ej. el golpe de aire al salir de la vivienda al pasillo). Opcional.")]
    public AudioClip clipEntrada;
    [Range(0f, 1f)] public float volumenEntrada = 0.6f;

    // Runtime
    [HideInInspector] public AudioSource fuente;
    [HideInInspector] public bool activaAntes;
}

public class AmbientSounds : MonoBehaviour
{
    [Header("Sonido Base (Loop Continuo - INTERIOR)")]
    [Tooltip("Ambiente que suena cuando el jugador está DENTRO de una estructura/pasillo.")]
    public AudioClip clipBase;
    [Range(0f, 1f)]
    public float volumenBase = 0.4f;
    public float duracionFadeIn = 3f;
    [Tooltip("Segundos de silencio DESPUÉS de que termina el recorrido de cámara del menú y la cámara se pega al personaje, antes de que arranque el fade in. El ambiente entrando justo en el frame en que agarrás el control se siente automático; un par de segundos de nada primero hace que se note cuando aparece.\n\nSe mide desde que ModoMenu.Activo pasa a false, así que funciona igual con el viaje de cámara activado o desactivado. En una escena sin menú arranca de una.")]
    public float retrasoDespuesDelMenu = 1.5f;

    [Header("Ambiente EXTERIOR (fuera de las estructuras)")]
    [Tooltip("Ambiente que suena cuando el jugador está FUERA de las estructuras (al aire libre / abismo). Hace crossfade con el interior.")]
    public AudioClip clipExterior;
    [Range(0f, 1f)]
    public float volumenExterior = 0.4f;
    [Tooltip("Velocidad del crossfade interior <-> exterior.")]
    public float velocidadCrossfade = 1.5f;
    [Tooltip("Distancia máxima del raycast hacia arriba para considerar que hay techo (jugador 'dentro'). Si no golpea nada en este rango, se considera 'fuera'.")]
    public float alturaDeteccionTecho = 25f;
    [Tooltip("Capas que cuentan como 'techo/estructura' para la detección. Déjalo en Everything si no usas capas especiales.")]
    public LayerMask capasEstructura = ~0;

    [Header("Viento del Vacío (Dinámico)")]
    [Tooltip("Sonido de viento constante que se oye cuando el jugador está expuesto al vacío (sin techo encima).")]
    public AudioClip clipVientoBase;
    [Range(0f, 1f)]
    public float volumenMaxViento = 0.6f;
    [Tooltip("Velocidad de transición del volumen del viento.")]
    public float velocidadSuavizadoViento = 2f;

    [Header("Ráfagas de Viento Aleatorias")]
    [Tooltip("Sonidos de ráfagas que se reproducen en intervalos aleatorios.")]
    public AudioClip[] clipsRafagas;
    public float tiempoMinRafaga = 15f;
    public float tiempoMaxRafaga = 35f;
    public float volumenMinRafaga = 0.2f;
    public float volumenMaxRafaga = 0.6f;

    [Header("Capas Contextuales (ambiente dinámico)")]
    [Tooltip("Capas que se suman ENCIMA del ambiente base según lo que estés haciendo. No lo reemplazan: el base sigue siendo la cama, esto le pone color.\n\nUna capa entra y sale con fade cuando su contexto se cumple, y puede tirar un sonido puntual al entrar (el 'clip de entrada'). Varios contextos pueden estar activos a la vez: podés estar en un pasillo Y quieto.")]
    public CapaAmbiente[] capas;
    [Tooltip("Cada cuánto se re-evalúa el contexto. No hace falta cada frame: preguntar en qué tipo de chunk estás cuesta más que un bool.")]
    public float intervaloEvaluacionContexto = 0.25f;

    [Header("Contexto: Quieto")]
    [Tooltip("Segundos sin moverte para que se considere que estás QUIETO. Es el gancho para el ambiente de 'te frenaste a escuchar'.")]
    public float segundosParaQuieto = 6f;
    [Tooltip("Cuánto te podés mover (metros) sin que cuente como movimiento. Evita que mirar alrededor o un temblor del CharacterController rompa el estado.")]
    public float umbralMovimientoQuieto = 0.35f;

    [Header("Ducking (el ambiente se agacha ante lo importante)")]
    [Tooltip("ON = el ambiente de fondo baja de volumen mientras suena algo importante (el loop de patrulla de un centinela, un susto, un dron cerca). Lo piden ellos a través de DuckingAmbiente; acá solo se aplica.")]
    public bool usarDucking = true;
    [Tooltip("Qué tan rápido baja y vuelve a subir. Bajo = transición lenta, no se nota el corte. 2-4 va bien.")]
    public float velocidadDucking = 3f;
    [Tooltip("Solo lectura, para mirar en Play: cuánto está agachado ahora mismo (1 = normal, 0 = mudo).")]
    [Range(0f, 1f)] public float factorDuckingActual = 1f;

    [Header("Contexto: Trepando")]
    [Tooltip("Opcionales: si los dejás vacíos se buscan solos en la escena al arrancar. Sirven para la capa 'Trepando' (escalera, rapel o tirolina).")]
    public SistemaEscalera sistemaEscalera;
    public SistemaRapel sistemaRapel;
    public SistemaTirolina sistemaTirolina;

    [Header("Sonidos Secundarios (Teclas 1, 2, 3...)")]
    public AudioClip[] sonidosSecundarios;
    [Range(0f, 1f)]
    public float volumenSecundarios = 0.7f;

    // --- Componentes de Audio Internos ---
    private AudioSource fuenteBase;
    private AudioSource fuenteExterior;
    private AudioSource fuenteViento;
    private AudioSource fuenteRafagas;
    private AudioSource fuenteSecundaria;

    // --- Variables de Control ---
    private float tiempoFade = 0f;
    private bool fadeTerminado = false;
    private float retrasoRestante;   // cuenta atrás desde que termina el menú (ver Update)
    // El gate de "ya tomaste el personaje". Es campo y no local porque lo miran TODOS los
    // subsistemas de audio, no solo el fade in del ambiente base.
    private bool puedeSonar;
    private bool jugadorDentro = true;   // Estado actual: dentro/fuera de la estructura
    private float temporizadorRafaga = 0f;
    private float siguienteTiempoRafaga = 10f;

    private WorldGenerator worldGen;
    private Transform jugadorTransform;

    private KeyCode[] teclasNumericas = new KeyCode[]
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
        KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
        KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
    };

    void Awake()
    {
        // Crear las distintas fuentes de audio
        fuenteBase       = gameObject.AddComponent<AudioSource>();
        fuenteExterior   = gameObject.AddComponent<AudioSource>();
        fuenteViento     = gameObject.AddComponent<AudioSource>();
        fuenteRafagas    = gameObject.AddComponent<AudioSource>();
        fuenteSecundaria = gameObject.AddComponent<AudioSource>();

        fuenteBase.loop   = true;
        fuenteBase.volume = 0f;
        fuenteBase.clip   = clipBase;

        fuenteExterior.loop   = true;
        fuenteExterior.volume = 0f;
        fuenteExterior.clip   = clipExterior;

        fuenteViento.loop   = true;
        fuenteViento.volume = 0f;
        fuenteViento.clip   = clipVientoBase;

        fuenteRafagas.loop = false;
        fuenteSecundaria.loop = false;

        // Una AudioSource por capa: cada una necesita su propio volumen para poder cruzarse con
        // las demás. Se crean acá y no en el inspector para que agregar una capa sea solo llenar
        // un elemento del array.
        if (capas != null)
        {
            foreach (CapaAmbiente capa in capas)
            {
                if (capa == null) continue;
                capa.fuente = gameObject.AddComponent<AudioSource>();
                capa.fuente.loop = true;
                capa.fuente.playOnAwake = false;
                capa.fuente.volume = 0f;
                capa.fuente.clip = capa.loop;
            }
        }
    }

    void Start()
    {
        retrasoRestante = retrasoDespuesDelMenu;

        // Las fuentes arrancan sonando pero en volumen 0 (ver Awake) — el silencio del menú lo da
        // el volumen, no el Play. Así el loop ya está en marcha y el fade in no tiene que esperar
        // a que el clip cargue.
        if (fuenteBase.clip != null) fuenteBase.Play();
        if (fuenteExterior.clip != null) fuenteExterior.Play();
        if (fuenteViento.clip != null) fuenteViento.Play();

        // Buscar referencias
        worldGen = FindAnyObjectByType<WorldGenerator>();
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            jugadorTransform = playerObj.transform;
        }

        if (sistemaEscalera == null) sistemaEscalera = FindAnyObjectByType<SistemaEscalera>();
        if (sistemaRapel == null) sistemaRapel = FindAnyObjectByType<SistemaRapel>();
        if (sistemaTirolina == null) sistemaTirolina = FindAnyObjectByType<SistemaTirolina>();

        if (capas != null)
            foreach (CapaAmbiente capa in capas)
                if (capa != null && capa.fuente != null && capa.fuente.clip != null) capa.fuente.Play();

        if (jugadorTransform != null) ultimaPosQuieto = jugadorTransform.position;

        // Definir primer tiempo para la ráfaga aleatoria
        CalcularSiguienteRafaga();
    }

    /// <summary>
    /// Consume los pedidos de ducking del frame. ⚠️ Va en LateUpdate y no en Update: el orden de
    /// Update entre scripts es arbitrario, así que leerlo ahí se perdería los pedidos de todos los
    /// emisores que corrieran después. LateUpdate corre tras TODOS los Update.
    /// </summary>
    void LateUpdate()
    {
        float objetivo = usarDucking ? DuckingAmbiente.ConsumirFactor() : 1f;
        factorDuckingActual = Mathf.Lerp(factorDuckingActual, objetivo,
                                         Time.deltaTime * Mathf.Max(0.01f, velocidadDucking));
    }

    // ---------- Capas contextuales ----------

    private float temporizadorContexto;
    private Vector3 ultimaPosQuieto;
    private float tiempoSinMoverse;
    private readonly System.Collections.Generic.HashSet<ContextoAmbiente> contextosActivos =
        new System.Collections.Generic.HashSet<ContextoAmbiente>();

    /// <summary>
    /// Recalcula qué contextos están activos. Throttleado por intervaloEvaluacionContexto: saber
    /// en qué tipo de macro chunk estás cuesta bastante más que leer un bool, y el ambiente no
    /// necesita reaccionar en el mismo frame.
    /// </summary>
    private void ActualizarContextos()
    {
        contextosActivos.Clear();
        if (jugadorTransform == null) return;

        if (!jugadorDentro)
        {
            contextosActivos.Add(ContextoAmbiente.Exterior);
        }
        else
        {
            // Vivienda = macro chunk tipo 1 (misma consulta que usa RegistroViviendas). Todo lo
            // demás bajo techo cuenta como pasillo: cruces, pozos, salas grandes.
            bool enVivienda = worldGen != null && worldGen.TipoMacroChunkEnPosicion(jugadorTransform.position) == 1;
            contextosActivos.Add(enVivienda ? ContextoAmbiente.Vivienda : ContextoAmbiente.Pasillo);
        }

        bool trepando = (sistemaEscalera != null && sistemaEscalera.estaEnEscalera)
                     || (sistemaRapel != null && sistemaRapel.estaEnRapel)
                     || (sistemaTirolina != null && sistemaTirolina.EnTirolina);
        if (trepando) contextosActivos.Add(ContextoAmbiente.Trepando);

        if (tiempoSinMoverse >= segundosParaQuieto) contextosActivos.Add(ContextoAmbiente.Quieto);
    }

    /// <summary>
    /// El contador de "quieto" corre en Update, no en la evaluación throttleada: si se midiera
    /// cada 0.25s se perdería movimiento entre muestras y podrías caminar despacio sin que se
    /// entere. El umbral en METROS (no en velocidad) evita que mirar alrededor o el jitter del
    /// CharacterController rompan el estado.
    /// </summary>
    private void ActualizarQuieto()
    {
        if (jugadorTransform == null) return;

        if ((jugadorTransform.position - ultimaPosQuieto).sqrMagnitude > umbralMovimientoQuieto * umbralMovimientoQuieto)
        {
            ultimaPosQuieto = jugadorTransform.position;
            tiempoSinMoverse = 0f;
        }
        else
        {
            tiempoSinMoverse += Time.deltaTime;
        }
    }

    private void ActualizarCapas(float factorFadeIn)
    {
        if (capas == null) return;

        foreach (CapaAmbiente capa in capas)
        {
            if (capa == null || capa.fuente == null) continue;

            bool activa = contextosActivos.Contains(capa.contexto);

            // Sonido puntual solo en el FLANCO de entrada, nunca mientras el contexto se mantiene
            // (si no, cada evaluación dispararía uno). factorFadeIn > 0 lo silencia durante el menú.
            if (activa && !capa.activaAntes && capa.clipEntrada != null && factorFadeIn > 0.01f)
                fuenteSecundaria.PlayOneShot(capa.clipEntrada, capa.volumenEntrada * factorFadeIn);
            capa.activaAntes = activa;

            float objetivo = (activa ? capa.volumen : 0f) * factorFadeIn;
            capa.fuente.volume = Mathf.Lerp(capa.fuente.volume, objetivo, Time.deltaTime * Mathf.Max(0.01f, capa.velocidadFade));
        }
    }

    void Update()
    {
        // 1. Detectar si el jugador está dentro o fuera de las estructuras (raycast hacia arriba)
        ActualizarEstadoInteriorExterior();

        // 2. Crossfade interior/exterior + fade in inicial combinados.
        //
        // El fade in NO arranca hasta que termina el menú (la cámara ya viajó y se pegó al
        // personaje) más `retrasoDespuesDelMenu`. Se usa ModoMenu.Activo en vez de un evento de
        // MenuInicial porque las TRES salidas del menú lo apagan (con viaje de cámara, sin viaje,
        // y el atajo de enganche directo), así que no hay forma de que quede colgado.
        //
        // El contador se REINICIA mientras el menú esté activo: si MenuInicial todavía no corrió
        // su Start en el primer frame, el flag arranca en false y esto empezaría a contar de más;
        // al prenderse el menú un frame después, la cuenta vuelve a cero sola.
        if (ModoMenu.Activo)
        {
            retrasoRestante = retrasoDespuesDelMenu;
        }
        else if (retrasoRestante > 0f)
        {
            retrasoRestante -= Time.deltaTime;
        }
        puedeSonar = retrasoRestante <= 0f;

        float factorFadeIn = fadeTerminado ? 1f : Mathf.Clamp01(tiempoFade / duracionFadeIn);
        if (!fadeTerminado && puedeSonar)
        {
            tiempoFade += Time.deltaTime;
            if (tiempoFade >= duracionFadeIn) fadeTerminado = true;
        }
        if (!puedeSonar) factorFadeIn = 0f;

        // El ducking multiplica al fade in: son dos cosas distintas (uno es "todavía no arrancó",
        // el otro "callate un rato") y se combinan sin pisarse.
        factorFadeIn *= factorDuckingActual;

        float volumenObjetivoBase     = (jugadorDentro ? volumenBase     : 0f) * factorFadeIn;
        float volumenObjetivoExterior = (jugadorDentro ? 0f              : volumenExterior) * factorFadeIn;

        fuenteBase.volume     = Mathf.Lerp(fuenteBase.volume,     volumenObjetivoBase,     Time.deltaTime * velocidadCrossfade);
        fuenteExterior.volume = Mathf.Lerp(fuenteExterior.volume, volumenObjetivoExterior, Time.deltaTime * velocidadCrossfade);

        // 2.5. Capas contextuales. El contador de quieto corre todos los frames; la evaluación
        // de contexto va throttleada porque consulta el tipo de macro chunk.
        ActualizarQuieto();
        temporizadorContexto -= Time.deltaTime;
        if (temporizadorContexto <= 0f)
        {
            temporizadorContexto = Mathf.Max(0.05f, intervaloEvaluacionContexto);
            ActualizarContextos();
        }
        ActualizarCapas(factorFadeIn);

        // 3. Control dinámico del viento del vacío (expuesto = suena)
        ControlarVientoDelVacio();

        // 4. Temporizador de ráfagas aleatorias
        ControlarRafagasAleatorias();

        // 5. Sonidos manuales por teclado
        for (int i = 0; i < teclasNumericas.Length; i++)
        {
            if (Input.GetKeyDown(teclasNumericas[i]))
            {
                ReproducirSecundario(i);
                break;
            }
        }
    }

    /// <summary>
    /// Determina si el jugador está dentro de una estructura/pasillo o al exterior.
    /// Usa un raycast hacia arriba: si golpea algo dentro de `alturaDeteccionTecho`,
    /// se considera que hay techo encima y por lo tanto el jugador está "dentro".
    /// Funciona con balcones, abismos, vuelos por encima de la mega-estructura, etc.
    /// </summary>
    private void ActualizarEstadoInteriorExterior()
    {
        if (jugadorTransform == null) return;

        Vector3 origen = jugadorTransform.position + Vector3.up * 0.5f; // pequeño offset para evitar el propio collider
        bool hayTecho = Physics.Raycast(origen, Vector3.up, alturaDeteccionTecho, capasEstructura, QueryTriggerInteraction.Ignore);
        jugadorDentro = hayTecho;
    }

    private void ControlarVientoDelVacio()
    {
        if (jugadorTransform == null || clipVientoBase == null) return;

        // Viento cuando el jugador está expuesto al vacío (sin techo encima, ya detectado
        // por ActualizarEstadoInteriorExterior). Dentro de las estructuras: silencio.
        //
        // ⚠️ El gate de puedeSonar hace falta acá también: si el spawn cae expuesto al vacío, el
        // viento entraba a volumen completo en el frame 0, sonando por encima del menú.
        float volumenObjetivoViento = (jugadorDentro || !puedeSonar) ? 0f : volumenMaxViento;

        // Transición suave de volumen
        fuenteViento.volume = Mathf.Lerp(fuenteViento.volume, volumenObjetivoViento, Time.deltaTime * velocidadSuavizadoViento);
    }

    private void ControlarRafagasAleatorias()
    {
        if (clipsRafagas == null || clipsRafagas.Length == 0) return;

        // ⚠️ El reloj NO avanza durante el menú. Antes corría desde Start, así que un recorrido de
        // cámara largo podía consumir los 15-35s de espera y meterte una ráfaga sobre el menú —
        // o peor, dispararla en el primer segundo de partida.
        if (!puedeSonar) return;

        temporizadorRafaga += Time.deltaTime;
        if (temporizadorRafaga >= siguienteTiempoRafaga)
        {
            ReproducirRafagaAleatoria();
            CalcularSiguienteRafaga();
        }
    }

    private void ReproducirRafagaAleatoria()
    {
        AudioClip clip = clipsRafagas[Random.Range(0, clipsRafagas.Length)];
        if (clip != null)
        {
            float vol = Random.Range(volumenMinRafaga, volumenMaxRafaga);
            // Si el jugador está cerca de la muralla, aumentamos el volumen de las ráfagas
            if (fuenteViento.volume > 0.1f)
            {
                vol = Mathf.Clamp01(vol * 1.5f);
            }

            fuenteRafagas.PlayOneShot(clip, vol);
            // Debug.Log($"AmbientSounds: Ráfaga de viento reproducida: '{clip.name}' con volumen {vol:F2}");
        }
    }

    private void CalcularSiguienteRafaga()
    {
        temporizadorRafaga = 0f;
        siguienteTiempoRafaga = Random.Range(tiempoMinRafaga, tiempoMaxRafaga);
    }

    private void ReproducirSecundario(int indice)
    {
        if (sonidosSecundarios == null || indice >= sonidosSecundarios.Length) return;
        AudioClip clip = sonidosSecundarios[indice];
        if (clip == null) return;

        fuenteSecundaria.Stop();
        fuenteSecundaria.clip = clip;
        fuenteSecundaria.volume = volumenSecundarios;
        fuenteSecundaria.Play();
        // Debug.Log($"AmbientSounds: Sonido secundario [{indice + 1}] -> '{clip.name}'");
    }

    public void CambiarBase(AudioClip nuevoClip)
    {
        fuenteBase.Stop();
        fuenteBase.clip   = nuevoClip;
        fuenteBase.volume = 0f;
        tiempoFade        = 0f;
        fadeTerminado     = false;
        fuenteBase.Play();
    }
}
