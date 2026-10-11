using UnityEngine;

/// <summary>
/// Vigía no persistente: patrulla/inspecciona puntos de interés sin priorizar al
/// jugador. Si el jugador entra en su cono de visión escala a Persiguiendo (va directo y
/// con memoria visual, no te suelta por cruzar una columna); al perderlo degrada a Alerta
/// (corre por ruta caminable a tu última posición) y de ahí a Buscando (investiga pistas:
/// dónde te vio, hacia dónde ibas, y puntos de interés en esa dirección). Cada avistamiento
/// nuevo lo hace más tenaz. Todas las transiciones pasan por CambiarEstado, que loguea una
/// sola vez por cambio (logEstados) — nunca por frame. No sigue afuera si "decidió"
/// no hacerlo (sigueAlExterior), dándole al jugador una salida segura garantizada.
/// Al terminar la ronda, RondaCentinelas llama IniciarRetiro() y el centinela camina
/// (por los centros de celda, ver WorldGenerator.TryCalcularRutaCeldas) hasta el cuartel
/// de centinelas más cercano (RoomData.esCuartelCentinelas), descansa un rato ahí y
/// recién entonces se autodestruye. Sigue percibiendo mientras se retira y descansa.
/// </summary>
public class Centinela : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidadInspeccion = 3f;
    public float velocidadAlerta = 6f;
    public float aceleracion = 4f;
    public float distanciaLlegada = 1.5f;
    [Tooltip("Capas que cuentan como obstáculo (paredes, pisos, techos).")]
    public LayerMask capasObstaculo = ~0;

    [Header("Patrulla por la isla")]
    [Tooltip("El centinela elige destinos al azar por TODA la isla en la que está y camina hasta ellos por los centros de celda, en vez de dar vueltas siempre alrededor de donde spawneó. Nunca sale de su isla: los destinos se sortean dentro del volumen de esa isla. Apagalo para volver al vagabundeo local de antes.")]
    public bool patrullarIslaCompleta = true;
    [Tooltip("Presupuesto de celdas del BFS al calcular la ruta de patrulla. Una isla de radio 2 son ~15.000 celdas: si lo dejás corto, los destinos lejanos se descartan y patrulla más cerca.")]
    public int maxNodosRutaPatrulla = 6000;
    [Tooltip("Cuántos destinos de patrulla prueba antes de rendirse y caer al vagabundeo local (un destino se descarta si no hay ruta caminable hasta él dentro del presupuesto).")]
    public int intentosDestinoPatrulla = 3;

    [Header("Inspección (sin prioridad sobre el jugador)")]
    [Tooltip("Radio en el que busca PuntoInteresCentinela cercanos como destino.")]
    public float radioBusquedaPuntos = 30f;
    [Tooltip("Si no hay puntos de interés cerca (y la patrulla por isla está apagada o no encontró ruta), vaga en una esfera de este radio alrededor de sí mismo.")]
    public float radioVagar = 15f;
    public float rangoY = 4f;
    public int intentosWaypoint = 12;
    public float tiempoInspeccionMin = 2f;
    public float tiempoInspeccionMax = 6f;

    [Header("Percepción")]
    [Tooltip("Cada cuántos segundos chequea si ve al jugador (no cada frame).")]
    public float intervaloPercepcion = 0.2f;
    public float distanciaVision = 18f;
    [Range(0f, 360f)] public float anguloVision = 100f;
    [Tooltip("Distancia a la que escala a Alerta sin importar el cono de visión.")]
    public float distanciaMuyCerca = 3f;
    [Tooltip("OPCIONAL pero recomendado en bichos grandes: un Empty hijo del prefab puesto donde están los ojos. De ahí sale el raycast de visión Y desde ahí se mide el cono. Si lo dejás vacío se usa 'Altura Ojos' sobre el pivote, que en un centinela alto queda a la altura de las rodillas y hace que el rayo choque contra el piso o contra su propio cuerpo.")]
    public Transform ojos;
    [Tooltip("Solo si NO asignaste 'Ojos': altura sobre el pivote de la que sale el raycast.")]
    public float alturaOjos = 1.6f;
    [Tooltip("ON = el cono de visión apunta hacia donde mira el Empty 'Ojos' (su eje Z azul). OFF = usa el forward del centinela entero. Dejalo OFF salvo que el Empty esté bien orientado — si el Empty está rotado, con esto ON el bicho mira para cualquier lado.")]
    public bool usarForwardDeOjos = false;
    [Tooltip("Altura del PUNTO DEL JUGADOR al que apunta el rayo. El transform del jugador está en los pies: mirando desde arriba, un rayo a los pies roza el piso y da 'no lo veo' aunque lo tenga enfrente. 1 = al pecho.")]
    public float alturaObjetivoJugador = 1f;
    [Tooltip("El rayo de visión no es UNO solo: se prueba a varias alturas del jugador (pies, pecho, cabeza) y con que UNA pase alcanza. Un solo rayo fino fallaba contra barandas, cajas y el borde de las pasarelas.")]
    public float alturaCabezaJugador = 1.8f;

    [Header("Volumen de detección (collider)")]
    [Tooltip("OPCIONAL: arrastrá acá un Collider (un Empty hijo con un Box/Sphere Collider grande, marcado Is Trigger). Si el jugador está DENTRO de ese volumen, cuenta como detectado sin importar el cono de visión — es el 'me tenés encima, obvio que te veo'. Reemplaza/complementa a 'Distancia Muy Cerca', que es una esfera y no se puede estirar hacia adelante.")]
    public Collider volumenDeteccion;
    [Tooltip("ON = estar dentro del volumen alcanza, aunque haya una pared en el medio. OFF = además tiene que tener línea de vista.")]
    public bool volumenIgnoraParedes = true;

    [Header("Alerta / Búsqueda")]
    public float tiempoBusquedaMax = 8f;
    [Tooltip("Probabilidad (0-1) de que este centinela siga al jugador si escapa al exterior.")]
    [Range(0f, 1f)] public float chanceSeguirExterior = 0.15f;
    [Tooltip("En Alerta, cada cuánto recalcula la ruta caminable hasta la última posición vista (no cada frame: recalcular es una pasada de BFS).")]
    public float intervaloRecalculoRuta = 1f;
    [Tooltip("Presupuesto del BFS al perseguir/buscar. Una isla completa son miles de celdas.")]
    public int maxNodosRutaAlerta = 6000;
    [Tooltip("Cuántos puntos investiga en Buscando antes de rendirse (si el tiempo no se acaba antes). Estilo Alien Isolation: no sabe dónde estás, arma hipótesis con pistas y las revisa una por una en vez de plantarse mirando el aire.")]
    public int cantidadPuntosBusqueda = 3;
    [Tooltip("Segundos hacia adelante que extrapola con la velocidad estimada del jugador al perderlo de vista — la 2da pista es \"para dónde iba\", no solo \"dónde lo vi\".")]
    public float extrapolacionBusqueda = 2.5f;
    [Tooltip("Radio para buscar PuntoInteresCentinela cerca de la posición extrapolada — se investigan como pistas adicionales (puertas, pasillos) antes que puntos al azar.")]
    public float radioPistaCercaExtrapolado = 12f;
    [Tooltip("Cuánto se queda \"mirando\" cada punto de búsqueda antes de pasar al siguiente.")]
    public float tiempoMirarPunto = 2.5f;
    [Tooltip("Cuando agota las pistas y todavía le queda tiempo, barre un anillo de puntos a esta distancia de donde te vio (x2 en la 2da ronda, x3 en la 3ra...). Antes se plantaba quieto esperando el reloj — por eso parecía que se rendía enseguida.")]
    public float radioAmpliacionBusqueda = 20f;
    [Tooltip("Cuántos puntos suma en cada anillo de ampliación (repartidos en 360°).")]
    public int puntosExploratoriosPorRonda = 4;

    [Header("Persecución (te veo, no te suelto)")]
    [Tooltip("Segundos que sigue persiguiendo en modo Persiguiendo DESPUÉS de perderte de vista, antes de degradar a Alerta (ir a tu última posición conocida). Es la \"memoria visual\": sin esto, te escondés medio segundo detrás de una columna y se olvida de vos.")]
    public float memoriaVisualSegundos = 3f;
    [Tooltip("Mientras te está viendo, el cono de visión se multiplica por esto — ya te tiene fichado, cuesta más salirse de su vista que entrar. 1 = sin bonus.")]
    [Range(1f, 3.6f)] public float bonusConoPersiguiendo = 1.8f;
    [Tooltip("Igual pero para la distancia: mientras te persigue ve más lejos que cuando patrulla tranquilo.")]
    [Range(1f, 3f)] public float bonusDistanciaPersiguiendo = 1.4f;
    [Tooltip("Cada vez que te vuelve a ver se pone más insistente: el tiempo de búsqueda se multiplica por (1 + veces*este valor). En 0 se desactiva la tenacidad.")]
    public float tenacidadPorAvistamiento = 0.5f;
    [Tooltip("Techo del multiplicador de tenacidad, para que no termine buscándote durante minutos.")]
    public float tenacidadMaxima = 3f;

    [Header("Sonido")]
    [Tooltip("AudioSource del centinela. Si lo dejás vacío se crea uno solo, ya configurado en 3D.")]
    public AudioSource fuenteSonido;
    [Tooltip("Loop que suena SIEMPRE mientras el centinela está vivo — motor, servos, pasos metálicos. Es lo que te deja ubicarlo de oído antes de verlo, así que no se corta durante la persecución.")]
    public AudioClip sonidoPatrulla;
    [Range(0f, 1f)] public float volumenPatrulla = 0.5f;
    [Tooltip("Se elige uno al azar cuando TE ENCUENTRA (pasa a Persiguiendo). Mismo criterio que el resto del proyecto: array y sorteo, para que no sea siempre el mismo.")]
    public AudioClip[] sonidosAlerta;
    [Range(0f, 1f)] public float volumenAlerta = 1f;
    [Tooltip("Segundos mínimos entre dos alertas del MISMO centinela.\n\n0 = suena cada vez que te vuelve a ver, aunque sea tres veces en cinco segundos.\nAlto (ej. 30) = prácticamente una sola vez por episodio de persecución.\n\nSin esto, perder y recuperar el contacto detrás de una columna dispara el sonido en ráfaga.")]
    public float cooldownAlerta = 8f;
    [Tooltip("Distancia (m) a la que deja de oírse. Rolloff LINEAL a propósito: en sigilo conviene un radio de audición predecible, no una curva logarítmica que se arrastra hasta el infinito.")]
    public float distanciaMaximaSonido = 40f;
    [Tooltip("A cuánto baja el ambiente de fondo cuando lo tenés ENCIMA (0 = ambiente mudo, 1 = sin ducking). El efecto se desvanece con la distancia: a Distancia Maxima Sonido no baja nada.\n\nEs lo que hace que escuchar al centinela no sea una pelea contra el ambiente — cuando aparece, el mundo se calla y queda él.")]
    [Range(0f, 1f)] public float duckingAmbiente = 0.3f;

    [Header("Debug")]
    [Tooltip("Un log por CAMBIO de estado (nunca por frame), con el motivo. Para ver si el bicho realmente está persiguiendo o se quedó pensando.")]
    public bool logEstados = true;

    [Header("Retiro / Descanso (cuartel)")]
    [Tooltip("Radio (en macro chunks) en el que busca una sala cuartel (RoomData.esCuartelCentinelas) al retirarse.")]
    public int radioBusquedaCuartelMacros = 3;
    [Tooltip("Presupuesto de celdas exploradas por el BFS de ruta al cuartel antes de rendirse y caer al punto de spawn.")]
    public int maxNodosRuta = 4000;
    [Tooltip("Cuánto descansa en el cuartel al llegar, antes de despawnear en silencio.")]
    public float tiempoDescansoMin = 15f;
    public float tiempoDescansoMax = 40f;

    /// <summary>
    /// Tranquilo    → patrulla, no sospecha nada.
    /// Persiguiendo → TE ESTÁ VIENDO (o te vio hace menos de memoriaVisualSegundos). Va directo.
    /// Alerta       → no te ve, pero sabe dónde estabas: corre hasta ahí por ruta caminable.
    /// Buscando     → te perdió: investiga las pistas (última pos, hacia dónde ibas, POIs cerca).
    /// </summary>
    private enum Estado { Tranquilo, Alerta, Persiguiendo, Buscando, RetirandoseABase, Descansando }
    private Estado estado = Estado.Tranquilo;

    private Transform jugador;
    private Vector3 waypoint;
    private Vector3 velocidad;
    private bool pausado;
    private float tiempoPausaRestante;
    private float tiempoBusquedaRestante;
    private float temporizadorPercepcion;
    private Vector3 ultimaPosJugador;
    private Vector3 velocidadJugadorEstimada;
    private bool sigueAlExterior;
    private bool rondaTerminando;
    private Vector3 posBase;
    private float temporizadorRecalculoRuta;

    // "Pistas" de Buscando: dónde lo vio, hacia dónde iba, y puntos de interés cerca de ahí.
    // Se investigan una por una en vez de plantarse mirando el aire 8 segundos.
    private readonly System.Collections.Generic.List<Vector3> puntosBusqueda = new System.Collections.Generic.List<Vector3>();
    private int indicePuntoBusqueda;
    private bool mirandoPuntoBusqueda;
    private float tiempoMirarRestante;
    private bool intentoRutaBusquedaHecho; // 1 intento de BFS por punto, no uno por frame si falla
    private float memoriaVisualRestante;   // Persiguiendo: cuánto le queda de "todavía te tengo"
    private bool viendoAhora;              // persiste entre frames de percepción (no es por-frame)
    private int rondasDeBusqueda;          // cada vez que agota las pistas, amplía el radio
    private int vecesQueTeVio;             // tenacidad: cada avistamiento nuevo lo pone más insistente

    // Ruta por centros de celda (WorldGenerator.TryCalcularRutaCeldas). La comparten la PATRULLA
    // y el RETIRO: las dos caminan igual, lo único que cambia es qué pasa al llegar al final.
    private readonly System.Collections.Generic.List<Vector3> ruta = new System.Collections.Generic.List<Vector3>();
    private int indiceRuta;
    private float tiempoDescansoRestante;

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) jugador = p.transform;

        posBase = transform.position;
        sigueAlExterior = Random.value <= chanceSeguirExterior;
        PrepararSonido();
        ElegirNuevoDestino();
    }

    /// <summary>
    /// Deja el AudioSource listo en 3D y arranca el loop de patrulla. Se configura por código y no
    /// en el prefab para que un centinela nuevo suene bien sin tener que acordarse de tocar seis
    /// campos del AudioSource — lo único que hay que asignar es el clip.
    /// </summary>
    private void PrepararSonido()
    {
        if (fuenteSonido == null) fuenteSonido = GetComponent<AudioSource>();
        if (fuenteSonido == null) fuenteSonido = gameObject.AddComponent<AudioSource>();

        fuenteSonido.playOnAwake = false;
        fuenteSonido.spatialBlend = 1f;                        // 3D puro: tiene que venir de ÉL
        fuenteSonido.rolloffMode = AudioRolloffMode.Linear;    // radio de audición predecible
        fuenteSonido.minDistance = 1f;
        fuenteSonido.maxDistance = Mathf.Max(1f, distanciaMaximaSonido);

        if (sonidoPatrulla != null)
        {
            fuenteSonido.clip = sonidoPatrulla;
            fuenteSonido.loop = true;
            fuenteSonido.volume = volumenPatrulla;
            fuenteSonido.Play();
        }
    }

    /// <summary>
    /// Grito de alerta al encontrarte. Va por PlayOneShot para que NO pise el loop de patrulla:
    /// los dos suenan a la vez desde la misma fuente, que es lo que querés — el bicho grita sin
    /// dejar de hacer su ruido de máquina.
    /// </summary>
    private void SonarAlerta()
    {
        if (fuenteSonido == null || sonidosAlerta == null || sonidosAlerta.Length == 0) return;
        if (Time.time < proximaAlertaPermitida) return;

        AudioClip clip = sonidosAlerta[Random.Range(0, sonidosAlerta.Length)];
        if (clip == null) return;

        fuenteSonido.PlayOneShot(clip, volumenAlerta);
        proximaAlertaPermitida = Time.time + cooldownAlerta;
    }

    private float proximaAlertaPermitida;

    /// <summary>
    /// Le pide al ambiente que se agache, proporcional a lo cerca que está. Se llama TODOS los
    /// frames a propósito: el bus se resetea solo, así que no hay que avisar cuándo soltar el
    /// ducking — al morir o alejarse, simplemente deja de pedir y el ambiente vuelve solo.
    /// </summary>
    private void PedirDucking()
    {
        // Cinturón y tirantes: aunque la ronda ya espera a que cierre el menú, si un centinela
        // llega a existir durante el menú por cualquier vía, se calla igual.
        if (fuenteSonido != null) fuenteSonido.mute = ModoMenu.Activo;
        if (ModoMenu.Activo) return;

        if (jugador == null || sonidoPatrulla == null) return;
        if (duckingAmbiente >= 0.999f) return;

        float dist = Vector3.Distance(transform.position, jugador.position);
        if (dist >= distanciaMaximaSonido) return;

        // Encima tuyo → duckingAmbiente. En el borde del radio → 1 (sin efecto).
        float t = Mathf.Clamp01(dist / Mathf.Max(0.01f, distanciaMaximaSonido));
        DuckingAmbiente.Pedir(Mathf.Lerp(duckingAmbiente, 1f, t));
    }

    void Update()
    {
        PedirDucking();

        temporizadorPercepcion += Time.deltaTime;
        bool tocaPercibir = temporizadorPercepcion >= intervaloPercepcion;
        if (tocaPercibir) temporizadorPercepcion = 0f;

        switch (estado)
        {
            case Estado.Tranquilo: ActualizarInspeccionando(tocaPercibir); break;
            case Estado.Persiguiendo: ActualizarPersiguiendo(tocaPercibir); break;
            case Estado.Alerta: ActualizarAlerta(tocaPercibir); break;
            case Estado.Buscando: ActualizarBuscando(tocaPercibir); break;
            case Estado.RetirandoseABase: ActualizarRetirada(tocaPercibir); break;
            case Estado.Descansando: ActualizarDescanso(tocaPercibir); break;
        }
    }

    /// <summary>
    /// ÚNICO punto por el que pasa cualquier transición — así el log sale una vez por cambio y
    /// nunca por frame. Si el estado nuevo es el mismo que el actual no hace nada (ni loguea).
    /// </summary>
    private void CambiarEstado(Estado nuevo, string motivo)
    {
        if (estado == nuevo) return;
        Estado anterior = estado;
        estado = nuevo;

        // Te encontró. Va acá y no en EntrarPersiguiendo porque este es el único punto por el que
        // pasan TODAS las transiciones — si mañana aparece otra forma de escalar a Persiguiendo,
        // el sonido la cubre sola.
        if (nuevo == Estado.Persiguiendo) SonarAlerta();
        if (logEstados)
            Debug.Log($"[Centinela {name}] {anterior} → {nuevo} · {motivo} · dist jugador: " +
                      (jugador != null ? $"{Vector3.Distance(transform.position, jugador.position):F1}m" : "—"));
    }

    // ---------- API pública ----------

    /// <summary>Cualquier arma/trampa del jugador llama esto al golpear al centinela.</summary>
    public void RecibirDano(float dano, Vector3 origenAtaque)
    {
        RegistrarAvistamiento(jugador != null ? jugador.position : origenAtaque);
        EntrarAlerta("recibió daño");
    }

    /// <summary>RondaCentinelas llama esto cuando la ronda terminó.</summary>
    public void IniciarRetiro()
    {
        rondaTerminando = true;
        if (estado == Estado.Tranquilo) EntrarRetirada();
    }

    // ---------- Inspeccionando ----------

    private void ActualizarInspeccionando(bool tocaPercibir)
    {
        if (tocaPercibir && DetectaJugador()) { EntrarPersiguiendo("te vio mientras patrullaba"); return; }

        if (pausado)
        {
            tiempoPausaRestante -= Time.deltaTime;
            if (tiempoPausaRestante <= 0f) ElegirNuevoDestino();
            return;
        }

        // Con ruta de patrulla: se camina celda a celda y la "llegada" es el final de la ruta.
        bool llego = ruta.Count > 0 ? AvanzarPorRuta(velocidadInspeccion)
                                    : MoverHacia(waypoint, velocidadInspeccion);
        if (llego)
        {
            pausado = true;
            tiempoPausaRestante = Random.Range(tiempoInspeccionMin, tiempoInspeccionMax);
        }
    }

    private void ElegirNuevoDestino()
    {
        pausado = false;
        ruta.Clear();
        indiceRuta = 0;

        // 1. Punto de interés cercano: sigue mandando, es la señal más fuerte de "andá a mirar acá".
        PuntoInteresCentinela punto = BuscarPuntoInteresCercano();
        if (punto != null) { waypoint = punto.transform.position; return; }

        // 2. Patrulla por la isla entera: destino al azar en SU isla, caminado por celdas.
        if (patrullarIslaCompleta && TryRutaDePatrulla()) return;

        // 3. Vagabundeo local (sin isla debajo, o sin ruta caminable hasta ningún destino).
        for (int i = 0; i < intentosWaypoint; i++)
        {
            Vector2 h = Random.insideUnitCircle.normalized;
            float r = Random.Range(radioVagar * 0.3f, radioVagar);
            float y = Random.Range(-rangoY, rangoY);
            Vector3 candidato = transform.position + new Vector3(h.x * r, y, h.y * r);
            if (Physics.Linecast(transform.position, candidato, capasObstaculo, QueryTriggerInteraction.Ignore)) continue;
            // El vagabundeo tampoco puede derivar hacia adentro de una casa.
            if (WorldGenerator.Instance != null && WorldGenerator.Instance.EsPosicionDeViviendaVedada(candidato)) continue;
            waypoint = candidato;
            return;
        }
        waypoint = transform.position + transform.forward * 1f;
    }

    /// <summary>
    /// Sortea un destino de patrulla dentro de la isla actual y arma la ruta caminable hasta él.
    /// Reintenta con otros destinos porque uno puede caer en un bolsillo sin conexión por celdas
    /// vacías (o simplemente demasiado lejos para el presupuesto del BFS).
    /// </summary>
    private bool TryRutaDePatrulla()
    {
        WorldGenerator wg = WorldGenerator.Instance;
        if (wg == null) return false;

        for (int i = 0; i < Mathf.Max(1, intentosDestinoPatrulla); i++)
        {
            if (!wg.TryElegirCeldaPatrullaEnIsla(transform.position, out Vector3Int destino)) return false;
            if (ConstruirRutaHasta(wg, destino, maxNodosRutaPatrulla)) return true;
        }
        return false;
    }

    /// <summary>
    /// Llena `ruta` con los centros de celda desde la posición actual hasta `celdaDestino`,
    /// conservando la altura a la que va el centinela dentro de la celda (si no, al entrar en la
    /// ruta pegaría un salto vertical hasta el piso exacto).
    /// </summary>
    private bool ConstruirRutaHasta(WorldGenerator wg, Vector3Int celdaDestino, int maxNodos)
    {
        Vector3Int celdaActual = wg.MundoACelda(transform.position);
        var rutaCeldas = new System.Collections.Generic.List<Vector3Int>();

        // La ruta esquiva las viviendas: filtrar solo el destino no alcanzaba, porque un destino
        // legítimo del otro lado de una casa hacía que la cruzaran por adentro.
        //
        // EXCEPCIÓN: si el centinela YA está dentro de una vivienda (te persiguió hasta adentro),
        // el veto se levanta para esta ruta. Si no, sus 6 vecinos estarían vetados también y se
        // quedaría sin forma de salir caminando.
        System.Func<Vector3Int, bool> veto = wg.EsCeldaDeViviendaVedada(celdaActual)
            ? null
            : (System.Func<Vector3Int, bool>)wg.EsCeldaDeViviendaVedada;

        if (!wg.TryCalcularRutaCeldas(celdaActual, celdaDestino, maxNodos, rutaCeldas, veto)) return false;

        float alturaActual = transform.position.y - wg.CeldaACentroMundoPiso(celdaActual).y;
        ruta.Clear();
        foreach (Vector3Int c in rutaCeldas)
            ruta.Add(wg.CeldaACentroMundoPiso(c) + Vector3.up * Mathf.Max(alturaActual, 0.1f));

        indiceRuta = 0;
        waypoint = ruta[0];
        return true;
    }

    /// <summary>Avanza un paso por `ruta`. Devuelve true al consumir el último tramo.</summary>
    private bool AvanzarPorRuta(float velocidad)
    {
        if (!MoverHacia(ruta[indiceRuta], velocidad)) return false;

        indiceRuta++;
        if (indiceRuta >= ruta.Count) return true;
        waypoint = ruta[indiceRuta];
        return false;
    }

    private PuntoInteresCentinela BuscarPuntoInteresCercano()
    {
        PuntoInteresCentinela mejor = null;
        float mejorDist = radioBusquedaPuntos;
        WorldGenerator wg = WorldGenerator.Instance;
        foreach (var punto in PuntoInteresCentinela.Todos)
        {
            if (punto == null || punto.tipo != PuntoInteresCentinela.Tipo.Inspeccion) continue;
            // Un punto de interés dentro de una vivienda los metía derecho a la casa: esta rama
            // manda sobre la patrulla Y va en línea recta, así que se saltaba todos los filtros.
            if (wg != null && wg.EsPosicionDeViviendaVedada(punto.transform.position)) continue;
            float dist = Vector3.Distance(transform.position, punto.transform.position);
            if (dist < mejorDist)
            {
                mejorDist = dist;
                mejor = punto;
            }
        }
        return mejor;
    }

    // ---------- Alerta ----------

    /// <summary>Te vio: entra en persecución activa. Es el estado más agresivo.</summary>
    private void EntrarPersiguiendo(string motivo)
    {
        pausado = false;
        ruta.Clear();
        indiceRuta = 0;
        temporizadorRecalculoRuta = 0f;
        memoriaVisualRestante = memoriaVisualSegundos;
        viendoAhora = true;
        if (estado != Estado.Persiguiendo) vecesQueTeVio++;
        CambiarEstado(Estado.Persiguiendo, motivo);
    }

    private void ActualizarPersiguiendo(bool tocaPercibir)
    {
        if (tocaPercibir)
        {
            // Cono y distancia AMPLIADOS: ya te tiene fichado, cuesta más salir de su vista que
            // entrar. Sin esto, cruzar una esquina un frame lo hacía perderte de una.
            viendoAhora = DetectaJugador(bonusConoPersiguiendo, bonusDistanciaPersiguiendo);
            if (viendoAhora) memoriaVisualRestante = memoriaVisualSegundos;
        }

        if (!sigueAlExterior && EsExterior(transform.position))
        {
            EntrarBuscando(reducido: true, motivo: "te fuiste al exterior y este no sigue afuera");
            return;
        }

        // ⚠️ La ÚNICA salida de Persiguiendo es que se agote la memoria visual. Antes también
        // salía por "llegué a la última posición conocida", y eso lo rompía: esa comparación
        // usaba un `teVe` local que solo era true en los frames de percepción (1 de cada ~12),
        // así que llegar al punto viejo en cualquier otro frame lo degradaba a Alerta con el
        // jugador a 4 metros y a la vista. Se veía exactamente como "me persigue 1 segundo y
        // se aburre".
        memoriaVisualRestante -= Time.deltaTime;
        if (memoriaVisualRestante <= 0f)
        {
            viendoAhora = false;
            EntrarAlerta("te perdió de vista, va a tu última posición conocida");
            return;
        }

        // Con línea de vista no hace falta BFS: va derecho, que además es más reactivo (la ruta
        // por celdas redondea a centros de celda y se ve robótica cuando te tiene enfrente).
        MoverHacia(ultimaPosJugador, velocidadAlerta);
    }

    private void EntrarAlerta(string motivo)
    {
        pausado = false;
        ruta.Clear();
        indiceRuta = 0;
        temporizadorRecalculoRuta = 0f; // fuerza a calcular ruta YA en el próximo update
        CambiarEstado(Estado.Alerta, motivo);
    }

    private void ActualizarAlerta(bool tocaPercibir)
    {
        if (tocaPercibir && DetectaJugador())
        {
            EntrarPersiguiendo("te volvió a ver");
            return;
        }

        if (!sigueAlExterior && EsExterior(transform.position))
        {
            EntrarBuscando(reducido: true, motivo: "salió al exterior y este no sigue afuera");
            return;
        }

        // Persigue por los CENTROS de celda (como la patrulla), no en línea recta — si no, contra
        // cualquier pared entre medio se queda empujando sin poder rodearla. Se recalcula cada
        // intervaloRecalculoRuta (no cada frame) porque cada recálculo es una pasada de BFS.
        temporizadorRecalculoRuta -= Time.deltaTime;
        if (temporizadorRecalculoRuta <= 0f)
        {
            temporizadorRecalculoRuta = intervaloRecalculoRuta;
            WorldGenerator wg = WorldGenerator.Instance;
            if (wg != null) ConstruirRutaHasta(wg, wg.MundoACelda(ultimaPosJugador), maxNodosRutaAlerta);
        }

        bool llego = ruta.Count > 0 ? AvanzarPorRuta(velocidadAlerta) : MoverHacia(ultimaPosJugador, velocidadAlerta);
        if (llego) EntrarBuscando(reducido: false, motivo: "llegó a tu última posición y no estabas");
    }

    // ---------- Buscando ----------

    private void EntrarBuscando(bool reducido, string motivo)
    {
        // Tenacidad: cuantas más veces te vio en este episodio, más tiempo te busca. La 3ra vez
        // que te asomás ya no se rinde a los 8 segundos.
        float tenacidad = Mathf.Min(1f + vecesQueTeVio * tenacidadPorAvistamiento, Mathf.Max(1f, tenacidadMaxima));
        tiempoBusquedaRestante = tiempoBusquedaMax * tenacidad * (reducido ? 0.5f : 1f);
        ArmarPuntosBusqueda();
        ruta.Clear();
        indiceRuta = 0;
        indicePuntoBusqueda = 0;
        mirandoPuntoBusqueda = false;
        intentoRutaBusquedaHecho = false;
        rondasDeBusqueda = 0;
        CambiarEstado(Estado.Buscando, $"{motivo} · {puntosBusqueda.Count} pista(s) a revisar, {tiempoBusquedaRestante:F0}s (te vio {vecesQueTeVio}x)");
    }

    /// <summary>
    /// Arma la lista de hipótesis a investigar, de más a menos confiable: 1) donde lo vio,
    /// 2) hacia dónde iba (extrapolando la velocidad estimada), 3) PuntoInteresCentinela cerca de
    /// esa extrapolación (puertas/pasillos = "por ahí se puede haber ido"). No es omnisciente:
    /// son hipótesis, así que puede investigar mal y perderlo igual.
    /// </summary>
    private void ArmarPuntosBusqueda()
    {
        puntosBusqueda.Clear();
        puntosBusqueda.Add(ultimaPosJugador);

        Vector3 extrapolado = ultimaPosJugador + velocidadJugadorEstimada * extrapolacionBusqueda;
        if ((extrapolado - ultimaPosJugador).sqrMagnitude > 0.25f) puntosBusqueda.Add(extrapolado);

        WorldGenerator wg = WorldGenerator.Instance;
        var candidatos = new System.Collections.Generic.List<(float d, Vector3 pos)>();
        foreach (var punto in PuntoInteresCentinela.Todos)
        {
            if (punto == null) continue;
            if (wg != null && wg.EsPosicionDeViviendaVedada(punto.transform.position)) continue;
            float d = Vector3.Distance(extrapolado, punto.transform.position);
            if (d <= radioPistaCercaExtrapolado) candidatos.Add((d, punto.transform.position));
        }
        candidatos.Sort((a, b) => a.d.CompareTo(b.d));
        foreach (var c in candidatos)
        {
            if (puntosBusqueda.Count >= cantidadPuntosBusqueda) break;
            puntosBusqueda.Add(c.pos);
        }
    }

    /// <summary>
    /// Suma puntos exploratorios en un anillo de `radio` alrededor de donde te vio por última vez.
    /// No verifica que sean caminables acá: si no hay ruta, ConstruirRutaHasta falla y el punto se
    /// consume rápido (un intento de BFS, no uno por frame) — sale más barato que validar los 4.
    /// </summary>
    private void AgregarPuntosExploratorios(float radio)
    {
        float anguloBase = Random.Range(0f, 360f);
        for (int i = 0; i < puntosExploratoriosPorRonda; i++)
        {
            float ang = (anguloBase + i * (360f / Mathf.Max(1, puntosExploratoriosPorRonda))) * Mathf.Deg2Rad;
            Vector3 p = ultimaPosJugador + new Vector3(Mathf.Cos(ang) * radio, 0f, Mathf.Sin(ang) * radio);
            if (WorldGenerator.Instance != null && WorldGenerator.Instance.EsPosicionDeViviendaVedada(p)) continue;
            puntosBusqueda.Add(p);
        }
    }

    private void ActualizarBuscando(bool tocaPercibir)
    {
        if (tocaPercibir && DetectaJugador()) { EntrarPersiguiendo("te encontró mientras buscaba"); return; }

        tiempoBusquedaRestante -= Time.deltaTime;
        if (tiempoBusquedaRestante <= 0f)
        {
            // Se rinde: el episodio terminó, se olvida de cuántas veces te vio.
            vecesQueTeVio = 0;
            if (rondaTerminando) EntrarRetirada();
            else
            {
                ElegirNuevoDestino();
                CambiarEstado(Estado.Tranquilo, "se rindió, vuelve a patrullar");
            }
            return;
        }

        // "Mirando" el punto actual antes de pasar al siguiente — no es instantáneo, un cazador
        // real se detiene a revisar, no pasa de largo.
        if (mirandoPuntoBusqueda)
        {
            tiempoMirarRestante -= Time.deltaTime;
            if (tiempoMirarRestante <= 0f)
            {
                mirandoPuntoBusqueda = false;
                indicePuntoBusqueda++;
                ruta.Clear();
                indiceRuta = 0;
                intentoRutaBusquedaHecho = false;
            }
            return;
        }

        // Se acabaron las pistas y todavía le queda tiempo: en vez de plantarse esperando el
        // reloj (que es exactamente como se veía "se rinde sin más"), AMPLÍA el área — barre
        // puntos exploratorios cada vez más lejos de donde te vio. Se aleja del punto original
        // en círculos, que es lo que haría alguien que sabe que te fuiste para algún lado.
        if (indicePuntoBusqueda >= puntosBusqueda.Count)
        {
            rondasDeBusqueda++;
            AgregarPuntosExploratorios(radioAmpliacionBusqueda * rondasDeBusqueda);
            if (logEstados)
                Debug.Log($"[Centinela {name}] Buscando: amplía el área (ronda {rondasDeBusqueda}, radio {radioAmpliacionBusqueda * rondasDeBusqueda:F0}m) · quedan {tiempoBusquedaRestante:F0}s");
            if (indicePuntoBusqueda >= puntosBusqueda.Count) return; // no encontró ni uno válido
        }

        Vector3 destino = puntosBusqueda[indicePuntoBusqueda];
        WorldGenerator wg = WorldGenerator.Instance;
        if (ruta.Count == 0 && !intentoRutaBusquedaHecho && wg != null)
        {
            intentoRutaBusquedaHecho = true;
            ConstruirRutaHasta(wg, wg.MundoACelda(destino), maxNodosRutaAlerta);
        }

        bool llego = ruta.Count > 0 ? AvanzarPorRuta(velocidadAlerta) : MoverHacia(destino, velocidadAlerta);
        if (llego)
        {
            mirandoPuntoBusqueda = true;
            tiempoMirarRestante = tiempoMirarPunto;
        }
    }

    // ---------- RetirandoseABase ----------

    private void EntrarRetirada()
    {
        CambiarEstado(Estado.RetirandoseABase, "la ronda terminó, vuelve al cuartel");
        ruta.Clear();
        indiceRuta = 0;

        // 1. Cuartel de centinelas (RoomData.esCuartelCentinelas) más cercano, con ruta
        //    por los CENTROS de las celdas (nada de cortar camino a través de paredes).
        WorldGenerator wg = WorldGenerator.Instance;
        if (wg != null && wg.TryEncontrarSalaCentinela(transform.position, radioBusquedaCuartelMacros, out Vector3Int celdaCuartel))
        {
            if (ConstruirRutaHasta(wg, celdaCuartel, maxNodosRuta)) return;
        }

        // 2. Fallback: PuntoInteresCentinela de salida, o el punto de spawn, en línea recta.
        PuntoInteresCentinela salida = BuscarBaseSalida();
        waypoint = salida != null ? salida.transform.position : posBase;
    }

    private PuntoInteresCentinela BuscarBaseSalida()
    {
        PuntoInteresCentinela mejor = null;
        float mejorDist = float.MaxValue;
        foreach (var punto in PuntoInteresCentinela.Todos)
        {
            if (punto == null || punto.tipo != PuntoInteresCentinela.Tipo.BaseSalida) continue;
            float dist = Vector3.Distance(transform.position, punto.transform.position);
            if (dist < mejorDist)
            {
                mejorDist = dist;
                mejor = punto;
            }
        }
        return mejor;
    }

    private void ActualizarRetirada(bool tocaPercibir)
    {
        // Amenaza pasiva hasta el final: retirándose SIGUE percibiendo. Si te ve o lo
        // atacás mientras se va, vuelve a Alerta (RecibirDano ya lo cubre por su lado).
        if (tocaPercibir && DetectaJugador()) { EntrarPersiguiendo("te vio mientras se retiraba"); return; }

        // Sin ruta de celdas: caminata directa al punto de fallback y despawn al llegar
        // (comportamiento anterior, para cuando no hay cuartel ni ruta).
        if (ruta.Count == 0)
        {
            if (MoverHacia(waypoint, velocidadInspeccion)) Destroy(gameObject);
            return;
        }

        if (AvanzarPorRuta(velocidadInspeccion))
        {
            CambiarEstado(Estado.Descansando, "llegó al cuartel");
            tiempoDescansoRestante = Random.Range(tiempoDescansoMin, tiempoDescansoMax);
        }
    }

    private void ActualizarDescanso(bool tocaPercibir)
    {
        // Descansa en el cuartel pero NO baja la guardia: acercate demasiado y escala igual.
        if (tocaPercibir && DetectaJugador()) { EntrarPersiguiendo("te vio mientras descansaba"); return; }

        velocidad = Vector3.MoveTowards(velocidad, Vector3.zero, aceleracion * Time.deltaTime);
        tiempoDescansoRestante -= Time.deltaTime;
        if (tiempoDescansoRestante <= 0f) Destroy(gameObject);
    }

    // ---------- Percepción ----------

    /// <param name="bonusCono">Multiplicador del ángulo de visión (>1 solo en persecución).</param>
    /// <param name="bonusDistancia">Multiplicador de la distancia de visión (>1 solo en persecución).</param>
    private bool DetectaJugador(float bonusCono = 1f, float bonusDistancia = 1f)
    {
        if (jugador == null) return false;
        float dist = Vector3.Distance(transform.position, jugador.position);
        if (dist <= distanciaMuyCerca)
        {
            RegistrarAvistamiento(jugador.position);
            return true;
        }

        // Volumen de detección: si estás adentro del collider, te vio. Sin cono ni distancia.
        if (JugadorEnVolumenDeteccion() && (volumenIgnoraParedes || PuedeVerJugador()))
        {
            RegistrarAvistamiento(jugador.position);
            return true;
        }

        if (dist > distanciaVision * bonusDistancia) return false;

        // El cono se mide DESDE LOS OJOS hacia el pecho del jugador, no de pivote a pivote: en un
        // centinela alto contra un jugador bajo esos dos vectores no son el mismo, y la diferencia
        // de altura se comía parte del cono.
        Vector3 dirHaciaJugador = (PuntoObjetivoJugador - PosOjos).normalized;
        float angulo = Vector3.Angle(MiradaForward, dirHaciaJugador);
        if (angulo > Mathf.Min(anguloVision * bonusCono, 360f) * 0.5f) return false;

        if (!PuedeVerJugador()) return false;

        RegistrarAvistamiento(jugador.position);
        return true;
    }

    /// <summary>
    /// Línea de vista al jugador probando VARIAS alturas (pecho, cabeza, pies). Con que una pase
    /// alcanza: un solo rayo fino fallaba contra barandas, cajas y el borde de las pasarelas, y
    /// eso se veía como "me está mirando de frente y no me detecta".
    /// </summary>
    private bool PuedeVerJugador()
    {
        if (jugador == null) return false;
        Vector3 pies = jugador.position;
        return PuedeVer(pies + Vector3.up * alturaObjetivoJugador)
            || PuedeVer(pies + Vector3.up * alturaCabezaJugador)
            || PuedeVer(pies + Vector3.up * 0.2f);
    }

    /// <summary>
    /// ¿El jugador está dentro del collider de detección? ClosestPoint devuelve el mismo punto
    /// cuando está adentro, así que no hace falta ni Rigidbody ni eventos de trigger (que además
    /// no dispararían sin Rigidbody en alguno de los dos).
    /// </summary>
    private bool JugadorEnVolumenDeteccion()
    {
        if (volumenDeteccion == null || jugador == null) return false;
        Vector3 p = PuntoObjetivoJugador;
        return (volumenDeteccion.ClosestPoint(p) - p).sqrMagnitude < 0.0001f;
    }

    /// <summary>De dónde sale la vista: el Empty 'ojos' si está asignado, si no alturaOjos sobre el pivote.</summary>
    private Vector3 PosOjos => ojos != null ? ojos.position : transform.position + Vector3.up * alturaOjos;

    /// <summary>Hacia dónde mira. Solo usa el forward del Empty si lo pediste explícitamente.</summary>
    private Vector3 MiradaForward => (ojos != null && usarForwardDeOjos) ? ojos.forward : transform.forward;

    /// <summary>A qué punto del jugador apunta el rayo (el pecho, no los pies).</summary>
    private Vector3 PuntoObjetivoJugador => jugador.position + Vector3.up * alturaObjetivoJugador;

    /// <summary>
    /// Actualiza última posición vista Y estima la velocidad del jugador (posición nueva menos
    /// la anterior, sobre el intervalo de percepción) — esa velocidad es la "pista" que usa
    /// Buscando para adivinar hacia dónde seguir, no solo dónde estuvo.
    /// </summary>
    private void RegistrarAvistamiento(Vector3 pos)
    {
        if (intervaloPercepcion > 0.01f)
            velocidadJugadorEstimada = (pos - ultimaPosJugador) / intervaloPercepcion;
        ultimaPosJugador = pos;
    }

    private bool PuedeVer(Vector3 posObjetivo)
    {
        return !Physics.Linecast(PosOjos, posObjetivo, capasObstaculo, QueryTriggerInteraction.Ignore);
    }

    private bool EsExterior(Vector3 pos)
    {
        if (Physics.CheckSphere(pos, 2f, capasObstaculo, QueryTriggerInteraction.Ignore)) return false;
        if (Physics.Raycast(pos, Vector3.up, 10f, capasObstaculo, QueryTriggerInteraction.Ignore)) return false;
        return true;
    }

    // ---------- Movimiento compartido ----------

    private bool MoverHacia(Vector3 destino, float velocidadObjetivoMax)
    {
        Vector3 deseado = destino - transform.position;
        float dist = deseado.magnitude;
        Vector3 dirDeseada = (dist > 0.01f) ? deseado / dist : Vector3.zero;
        Vector3 velocidadObjetivo = dirDeseada * velocidadObjetivoMax;
        velocidad = Vector3.MoveTowards(velocidad, velocidadObjetivo, aceleracion * Time.deltaTime);
        transform.position += velocidad * Time.deltaTime;

        if (velocidad.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(velocidad.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 4f * Time.deltaTime);
        }

        return dist < distanciaLlegada;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = estado switch
        {
            Estado.Persiguiendo => Color.red,
            Estado.Alerta => new Color(1f, 0.5f, 0f),
            Estado.Buscando => Color.yellow,
            Estado.RetirandoseABase => Color.green,
            _ => Color.cyan
        };
        Gizmos.DrawSphere(waypoint, 0.5f);
        Gizmos.DrawLine(transform.position, waypoint);
        Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, distanciaVision);

        // Ojos: bolita magenta + hacia dónde mira. Sirve para ubicar el Empty 'Ojos' sin entrar
        // a Play — si la bolita queda a la altura de las rodillas, el rayo choca contra el piso.
        Vector3 posOjos = PosOjos;
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(posOjos, 0.15f);
        Gizmos.DrawLine(posOjos, posOjos + MiradaForward * 3f);
        if (jugador != null)
        {
            Gizmos.color = PuedeVerJugador() ? Color.green : Color.red;
            Gizmos.DrawLine(posOjos, PuntoObjetivoJugador);
        }
    }
}
