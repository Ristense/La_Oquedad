using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lleva las reservas (agua / comida / energía, en SEGUNDOS) de cada vivienda del mundo.
///
/// ESTADO POR EXCEPCIÓN: el diccionario solo guarda las viviendas que el jugador TOCÓ. Una
/// vivienda nunca visitada no ocupa memoria — sus valores se calculan al vuelo por hash +
/// semilla (determinista: misma semilla = mismas reservas iniciales, igual que el resto de la
/// generación). Recién cuando se suma o resta algo se crea la entrada, y de ahí en más manda
/// el diccionario. Así se puede tener un mundo infinito de viviendas sin explotar la RAM.
///
/// DRENAJE SIN SIMULACIÓN: las reservas bajan con el tiempo de partida, pero NO se simula a nadie.
/// Cada vivienda guarda a qué momento corresponden sus valores (`tiempoUltimaActualizacion`) y el
/// descuento se hace de una sola resta cuando se la lee. Una vivienda que nunca visitaste tiene la
/// marca en 0, así que se le descuenta la partida entera de golpe la primera vez que preguntás. El
/// costo no depende de cuántas viviendas haya: solo de cuántas mires.
///
/// Poner este componente en un GameObject de la escena (uno solo, junto al WorldGenerator).
/// </summary>
public class RegistroViviendas : MonoBehaviour
{
    public static RegistroViviendas Instance { get; private set; }

    [Header("Reservas iniciales (segundos, aleatorias por semilla)")]
    [Tooltip("Rango (min, max) de segundos de AGUA con los que arranca una vivienda no visitada. Referencia: el jugador aguanta 600s con el tanque lleno.")]
    public Vector2 rangoAguaInicial = new Vector2(0f, 1800f);
    [Tooltip("Rango (min, max) de segundos de COMIDA con los que arranca una vivienda no visitada.")]
    public Vector2 rangoComidaInicial = new Vector2(0f, 1800f);
    [Tooltip("Rango (min, max) de segundos de ENERGÍA con los que arranca una vivienda no visitada.")]
    public Vector2 rangoEnergiaInicial = new Vector2(0f, 1800f);

    [Header("Drenaje (reloj global, sin simulación)")]
    [Tooltip("Segundos de reserva que pierde una vivienda por cada segundo REAL de partida.\n\n0 = no drena (comportamiento viejo: las reservas quedan congeladas).\n\nReferencia con 1800s de reserva inicial: 1 = se vacía en 30 min · 0.1 = en 5 horas · 0.05 = en 10 horas.\n\n⚠️ Afecta a TODAS las viviendas del mundo, las hayas visitado o no. No se simula nada: se descuenta al momento de leer.")]
    [Min(0f)] public float drenajeAgua = 0.1f;
    [Min(0f)] public float drenajeComida = 0.1f;
    [Min(0f)] public float drenajeEnergia = 0.1f;

    [Header("Debug")]
    [Tooltip("Loguea en consola cada vez que se suma o resta un recurso a una vivienda.")]
    public bool logDeMovimientos = false;

    /// <summary>
    /// Segundos de partida transcurridos. Avanza con Time.deltaTime, así que NO corre con el juego
    /// en pausa (timeScale 0) — pausar no te vacía las viviendas.
    ///
    /// ⚠️ Hoy arranca en 0 en cada Play y NO se guarda: al cargar una partida el mundo vuelve a
    /// estar "recién empezado". Ver GDD §22 — persistirlo es parte de lo que falta.
    /// </summary>
    public float TiempoMundo { get; private set; }

    void Update()
    {
        TiempoMundo += Time.deltaTime;
    }

    // Solo las viviendas que el jugador modificó. El resto se calcula por hash cuando se pregunta.
    private readonly Dictionary<Vector3Int, StatsVivienda> viviendasModificadas = new Dictionary<Vector3Int, StatsVivienda>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // ---------- Ubicación ----------

    /// <summary>
    /// Macro chunk de la vivienda que contiene esa posición de mundo.
    /// Devuelve false si ahí no hay una vivienda (sala tipo 1).
    /// </summary>
    public bool TryMacroDeVivienda(Vector3 worldPos, out Vector3Int macro)
    {
        macro = default;
        WorldGenerator wg = WorldGenerator.Instance;
        if (wg == null) return false;
        if (wg.TipoMacroChunkEnPosicion(worldPos) != 1) return false;

        Vector3Int celda = wg.MundoACelda(worldPos);
        macro = new Vector3Int(
            Mathf.FloorToInt((float)celda.x / wg.macroSize),
            Mathf.FloorToInt((float)celda.y / wg.macroSize),
            Mathf.FloorToInt((float)celda.z / wg.macroSize));
        return true;
    }

    // ---------- Lectura ----------

    /// <summary>
    /// Reservas de la vivienda en esa posición. Devuelve false si ahí no hay vivienda.
    /// OJO: el objeto devuelto es una COPIA — para modificar usá Agregar/Quitar.
    /// </summary>
    public bool TryObtenerStats(Vector3 worldPos, out StatsVivienda stats)
    {
        stats = null;
        if (!TryMacroDeVivienda(worldPos, out Vector3Int macro)) return false;
        stats = ObtenerSoloLectura(macro); // ya devuelve una copia, no hace falta clonar de nuevo
        return true;
    }

    /// <summary>Segundos que le quedan a esta vivienda de un recurso. -1 si ahí no hay vivienda.</summary>
    public float ObtenerRecurso(Vector3 worldPos, RecursoVivienda recurso)
    {
        if (!TryMacroDeVivienda(worldPos, out Vector3Int macro)) return -1f;
        return ObtenerSoloLectura(macro).Obtener(recurso);
    }

    /// <summary>
    /// Las stats de una vivienda SIN crear su entrada en el diccionario: si ya la tocaste devuelve
    /// la viva, si no la calcula al vuelo y la descarta.
    ///
    /// ⚠️ Los caminos de LECTURA tienen que pasar por acá, no por ObtenerOCrear. Antes leían por
    /// ObtenerOCrear y eso rompía el estado por excepción de la forma más silenciosa posible: una
    /// pantalla que muestra las stats refrescando cada segundo materializaba una entrada
    /// permanente por CADA vivienda que mirabas, sin que nadie hubiera modificado nada. El
    /// diccionario tiene que guardar solo lo que el jugador CAMBIÓ.
    /// </summary>
    private StatsVivienda ObtenerSoloLectura(Vector3Int macro)
    {
        StatsVivienda baseStats = viviendasModificadas.TryGetValue(macro, out StatsVivienda existente)
            ? existente
            : CalcularStatsIniciales(macro);

        // Copia con el drenaje ya aplicado. NO se toca la entrada del diccionario: leer no
        // modifica nada, y da igual cuántas veces preguntes — el resultado depende solo del
        // reloj, no de cuándo miraste.
        StatsVivienda copia = baseStats.Clonar();
        AplicarDrenaje(copia);
        return copia;
    }

    /// <summary>
    /// Descuenta lo que se consumió entre `tiempoUltimaActualizacion` y ahora, y adelanta la marca.
    ///
    /// Esto es el corazón del "drena sin simular": en vez de restarle a cada vivienda todos los
    /// frames (imposible con viviendas infinitas), cada una guarda CUÁNDO se calculó por última vez
    /// y el descuento se hace de una sola resta en el momento de leerla. Una vivienda que nunca
    /// tocaste tiene la marca en 0, así que se le descuenta la partida entera de una.
    /// </summary>
    private void AplicarDrenaje(StatsVivienda stats)
    {
        float transcurrido = TiempoMundo - stats.tiempoUltimaActualizacion;
        stats.tiempoUltimaActualizacion = TiempoMundo;
        if (transcurrido <= 0f) return;

        stats.agua = Mathf.Max(0f, stats.agua - drenajeAgua * transcurrido);
        stats.comida = Mathf.Max(0f, stats.comida - drenajeComida * transcurrido);
        stats.energia = Mathf.Max(0f, stats.energia - drenajeEnergia * transcurrido);
    }

    // ---------- Escritura ----------

    /// <summary>
    /// Suma segundos de un recurso a la vivienda. Devuelve false si ahí no hay vivienda.
    /// </summary>
    public bool Agregar(Vector3 worldPos, RecursoVivienda recurso, float segundos)
    {
        if (segundos <= 0f) return false;
        if (!TryMacroDeVivienda(worldPos, out Vector3Int macro)) return false;

        StatsVivienda stats = ObtenerOCrear(macro);
        stats.Asignar(recurso, stats.Obtener(recurso) + segundos);

        if (logDeMovimientos)
            Debug.Log($"[RegistroViviendas] +{segundos:0}s de {recurso} en vivienda {macro} → {stats.Obtener(recurso):0}s");
        return true;
    }

    /// <summary>
    /// Saca segundos de un recurso. Devuelve CUÁNTO se pudo sacar realmente (0 si no había nada
    /// o si ahí no hay vivienda) — nunca deja el recurso en negativo.
    /// </summary>
    public float Quitar(Vector3 worldPos, RecursoVivienda recurso, float segundos)
    {
        if (segundos <= 0f) return 0f;
        if (!TryMacroDeVivienda(worldPos, out Vector3Int macro)) return 0f;

        StatsVivienda stats = ObtenerOCrear(macro);
        float disponible = stats.Obtener(recurso);
        float entregado = Mathf.Min(disponible, segundos);
        stats.Asignar(recurso, disponible - entregado);

        if (logDeMovimientos)
            Debug.Log($"[RegistroViviendas] -{entregado:0}s de {recurso} en vivienda {macro} (pedido {segundos:0}s) → {stats.Obtener(recurso):0}s");
        return entregado;
    }

    // ---------- Interno ----------

    /// <summary>
    /// Devuelve la entrada viva de esa vivienda, creándola con sus valores iniciales
    /// deterministas la primera vez que se la toca.
    /// </summary>
    private StatsVivienda ObtenerOCrear(Vector3Int macro)
    {
        if (!viviendasModificadas.TryGetValue(macro, out StatsVivienda entrada))
        {
            entrada = CalcularStatsIniciales(macro);
            viviendasModificadas[macro] = entrada;
        }

        // ⚠️ Se LIQUIDA el drenaje pendiente ANTES de devolverla. Si no, sumarle 60s de agua a una
        // vivienda que lleva media hora sin tocarse se los sumaría al valor viejo, y el drenaje de
        // esa media hora se descontaría después — regalando (o comiéndose) reservas según el orden.
        AplicarDrenaje(entrada);
        return entrada;
    }

    /// <summary>
    /// Reservas iniciales por hash + semilla del mundo. Mismo patrón determinista que
    /// ObtenerTipoMacroChunk / ObtenerRoomDataParaMacro: se siembra Random, se tira, y se
    /// restaura el estado para no ensuciar la generación del mundo.
    /// </summary>
    private StatsVivienda CalcularStatsIniciales(Vector3Int macro)
    {
        int semilla = WorldGenerator.Instance != null ? WorldGenerator.Instance.semillaDelMundo : 0;
        int hash = semilla + macro.x * 48611 + macro.y * 27689 + macro.z * 63149;

        Random.State prev = Random.state;
        Random.InitState(hash);

        StatsVivienda stats = new StatsVivienda
        {
            agua = Random.Range(rangoAguaInicial.x, rangoAguaInicial.y),
            comida = Random.Range(rangoComidaInicial.x, rangoComidaInicial.y),
            energia = Random.Range(rangoEnergiaInicial.x, rangoEnergiaInicial.y),
        };

        Random.state = prev;
        return stats;
    }
}
