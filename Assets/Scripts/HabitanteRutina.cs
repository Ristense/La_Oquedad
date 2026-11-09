using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persona que vive dentro de una vivienda, tiene un TRABAJO asignado (Centro, Agua, Comida, Energía, General)
/// y hace su rutina acorde a su rol: camina entre su zona de trabajo, los bordes del edificio central
/// y algún desvío aleatorio, quedándose un rato en cada parada.
///
/// También permite hablarle con [E] para escuchar diálogos sobre su trabajo y las necesidades de su área
/// según las reservas actuales de RegistroViviendas (e incluye lógica desactivable para entregarle recursos).
/// </summary>
public class HabitanteRutina : MonoBehaviour
{
    // ⚠️ El orden de los valores no se toca: Unity guarda el enum como número, y reordenarlo le
    // cambiaría la profesión a los prefabs que ya la tienen puesta.
    public enum Profesion
    {
        Centro,
        Agua,
        Comida,
        Energia,
        General
    }

    [Header("Profesión")]
    [Tooltip("Profesión de esta persona (se sortea por semilla al inicializar, salvo que apagues Asignar Trabajo Al Azar).")]
    public Profesion trabajo = Profesion.General;
    [Tooltip("ON = la profesión se sortea por semilla al spawnear (dos personas de la misma casa PUEDEN tocar la misma). OFF = respeta la puesta en el Inspector.")]
    public bool asignarTrabajoAlAzar = true;

    [Header("Interacción y Necesidades ([E])")]
    [Tooltip("Permite hablarle con [E] para que cuente sobre su trabajo y si falta su recurso en la vivienda.")]
    public bool permitirDialogo = true;
    [Tooltip("Distancia máxima para interactuar con [E].")]
    public float alcanceInteraccion = 3.5f;
    [Tooltip("Al hablarle se queda quieto mirándote. Cuando dejás de mirarlo, espera estos segundos y vuelve a su rutina.")]
    [Min(0f)]
    public float esperaAlDejarDeMirar = 1f;
    [Tooltip("Si te alejás más que esto (en múltiplos del alcance de interacción) mientras te atiende, vuelve a su rutina al toque.")]
    [Min(1f)]
    public float multiplicadorDistanciaAtencion = 2f;
    [Tooltip("Segundos de reserva bajo los cuales este trabajador considera que su recurso está en estado CRÍTICO y pide ayuda.")]
    public float umbralNecesidadRecurso = 600f;
    [Tooltip("DESACTIVADO POR AHORA: Si lo activás, al hablarle con [E] sosteniendo un consumible en la mano que aporte su recurso, se lo entregás directo a la vivienda.")]
    public bool permitirEntregarRecursoDirecto = false;

    [Header("Misión")]
    [Tooltip("Probabilidad (por semilla) de que esta persona tenga una misión para dar. Se le marca con un \"!\" sobre la cabeza (IndicadorMision).")]
    [Range(0f, 1f)]
    public float probabilidadMision = 0.3f;

    /// <summary>¿Esta persona tiene una misión? Se decide en Inicializar, por semilla.</summary>
    public bool TieneMision { get; private set; }
    public IndicadorMision Indicador { get; private set; }
    /// <summary>La misión que da (de Resources/Misiones), elegida por semilla. Null si no tiene.</summary>
    public MisionData Mision { get; private set; }

    private WorldGenerator wgMision;
    private int indiceHabitante;

    // Todas las misiones del proyecto, cargadas una sola vez para todos los habitantes.
    private static MisionData[] misionesDisponibles;

    // ---- Frases ----
    // Las frases de verdad viven en assets FrasesProfesion (Resources/Profesiones/), uno por
    // profesión. Estas son el respaldo para cuando una profesión no tiene asset o lo tiene vacío.
    private static readonly string[][] FrasesNormalPorDefecto =
    {
        /* Centro  */ new[] { "Reviso el núcleo central. Todo el sistema de la vivienda responde bien.",
                              "Llevo el control en el edificio central. Las terminales están estables." },
        /* Agua    */ new[] { "Los filtros de la zona de agua funcionan limpios hoy.",
                              "Revisé los tanques y el caudal hacia el centro está estable." },
        /* Comida  */ new[] { "Las raciones de la zona de comida están bien conservadas.",
                              "Acabo de llevar inventario de comida hacia el edificio central." },
        /* Energía */ new[] { "Las celdas de la zona de energía mantienen buena carga.",
                              "Chequeando los cables entre las baterías y el edificio central. Todo en orden." },
        /* General */ new[] { "Hago rondas ayudando entre las zonas y el centro.",
                              "Todo tranquilo por acá, sigo llevando cosas de un sector a otro." },
    };
    private static readonly string[][] FrasesNecesidadPorDefecto =
    {
        /* Centro  */ new[] { "El panel central marca alerta en los suministros. Necesitamos reabastecer la vivienda.",
                              "Desde el edificio central veo caer las reservas... no vamos a durar mucho así." },
        /* Agua    */ new[] { "¡Los tanques están casi secos! Necesito agua para mantener la purificadora andando.",
                              "Sin agua no puedo seguir operando los filtros de esta zona. ¿Trajiste algo?" },
        /* Comida  */ new[] { "Nos quedamos sin reservas en el sector de comida... no tengo qué repartir.",
                              "Hace falta comida urgente para mantener a la gente trabajando." },
        /* Energía */ new[] { "¡Las baterías están cayendo a cero! Necesito carga energética para que no se apague todo.",
                              "El sector de energía está crítico, apenas logro mantener encendido el centro." },
        /* General */ new[] { "La cosa está difícil, escasean recursos en las zonas.",
                              "Ando dando vueltas viendo cómo estirar lo poco que nos queda." },
    };

    // Compendio cargado de Resources/Profesiones/ una sola vez para todos los habitantes.
    private static Dictionary<Profesion, FrasesProfesion> compendio;

    private static FrasesProfesion CompendioDe(Profesion p)
    {
        if (compendio == null)
        {
            compendio = new Dictionary<Profesion, FrasesProfesion>();
            foreach (FrasesProfesion f in Resources.LoadAll<FrasesProfesion>("Profesiones"))
                if (f != null && !compendio.ContainsKey(f.profesion)) compendio[f.profesion] = f;
        }
        return compendio.TryGetValue(p, out FrasesProfesion r) ? r : null;
    }

    [Header("Caminata")]
    [Tooltip("Velocidad al caminar (m/s). 1.2–1.5 es paso tranquilo de persona.")]
    public float velocidad = 1.3f;
    [Tooltip("Qué tan rápido gira hacia donde camina o hacia lo que mira. Bajo = giros más pesados.")]
    public float velocidadGiro = 4f;
    [Tooltip("Corrimiento lateral máximo de su carril (m). Cada persona usa un desfase distinto para no encimarse.")]
    public float anchoCarril = 1.2f;
    [Tooltip("Altura extra sobre el piso (m).")]
    public float alturaSobrePiso = 0f;

    [Header("Esquivar al Jugador")]
    [Tooltip("Distancia (m) a la que te nota. Si estás adelante en su camino, o encima de donde está parado, se corre para un lado.")]
    public float radioEsquive = 1.6f;
    [Tooltip("Cuánto se puede correr como máximo (m). No subirlo mucho: el esquive no mira paredes y podría meterse en una.")]
    public float esquiveMaximo = 0.9f;
    [Tooltip("Qué tan suave se corre y vuelve a su carril (s). Bajo = reacción brusca.")]
    public float suavidadEsquive = 0.35f;

    [Header("Paradas")]
    [Tooltip("Cuántas paradas tiene su rutina (mín/máx).")]
    public Vector2Int cantidadParadas = new Vector2Int(4, 6);
    [Tooltip("Cuánto se queda en cada parada (segundos, mín/máx).")]
    public Vector2 tiempoParada = new Vector2(8f, 25f);
    [Tooltip("Qué tan adentro del cuarto se para frente a la sección de su zona (m desde el centro de esa celda hacia el centro de la sala).")]
    public float distanciaFrenteSeccion = 5f;
    [Tooltip("Probabilidad (0 a 1) de meter un desvío aleatorio entre su zona de trabajo y el edificio central.")]
    [Range(0f, 1f)]
    public float probabilidadDesvio = 0.45f;

    // ---- Rutina precalculada: tramos que se recorren en orden y se repiten ----
    private struct Tramo
    {
        public bool caminando;
        public List<Vector3> puntos;   // caminando: polilínea · quieto: un solo punto
        public float duracion;
        public Vector3 mirarHacia;     // quieto: hacia dónde mira mientras está parado
        public float largo;            // caminando: largo total de la polilínea
    }

    /// <summary>True mientras camina, false parado. Para conectar un Animator (walk/idle).</summary>
    public bool Caminando { get; private set; }

    private readonly List<Tramo> tramos = new List<Tramo>();
    private float duracionCiclo;
    private float desfase;
    private bool inicializado;
    private bool primerFrame = true;
    private float alturaBase;
    private Vector3Int macroVivienda;
    private int semillaPersona;
    private Vector3 esquive, velocidadEsquive;

    /// <summary>
    /// Arma la rutina sobre la planta baja de la vivienda del macro chunk dado. Lo llama el
    /// WorldGenerator al instanciar a la persona; `indice` distingue a los habitantes de la misma casa.
    /// </summary>
    public void Inicializar(WorldGenerator wg, Vector3Int macro, int indice)
    {
        tramos.Clear();
        macroVivienda = macro;

        alturaBase = 0f;
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            alturaBase = transform.position.y - b.min.y;
        }

        // Asegurar collider para poder apuntarle con [E] si no trae uno en el prefab
        if (GetComponentInChildren<Collider>() == null)
        {
            CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.35f;
            col.center = new Vector3(0f, 0.9f - alturaBase, 0f);
            // Trigger: el raycast del [E] lo sigue viendo, pero no empuja al jugador ni se traba con él.
            col.isTrigger = true;
        }

        int s = wg.macroSize;
        int centro = s / 2;
        Vector3Int baseCelda = new Vector3Int(macro.x * s, macro.y * s + 1, macro.z * s);
        Vector3 centroSala = wg.CeldaACentroMundoPiso(baseCelda + new Vector3Int(centro, 0, centro));

        // NODOS CAMINABLES de la planta baja (excluyendo el centro exacto para no atravesar el edificio central)
        var celdas = new List<Vector2Int>();
        var indiceDe = new Dictionary<Vector2Int, int>();
        for (int lx = 0; lx < s; lx++)
            for (int lz = 0; lz < s; lz++)
            {
                if (lx == centro && lz == centro) continue;
                Vector3Int c = baseCelda + new Vector3Int(lx, 0, lz);
                if (!wg.IsCellEmpty(c.x, c.y, c.z) || wg.IsCellEmpty(c.x, c.y - 1, c.z)) continue;
                indiceDe[new Vector2Int(lx, lz)] = celdas.Count;
                celdas.Add(new Vector2Int(lx, lz));
            }
        if (celdas.Count < 2) { inicializado = false; return; }

        semillaPersona = wg.semillaDelMundo ^ (macro.x * 73856) ^ (macro.y * 19349) ^ (macro.z * 83492) ^ (indice * 2654435);
        var rng = new System.Random(semillaPersona);

        // Misión: con su propio Random, así no le cambia la profesión ni la rutina a nadie.
        wgMision = wg;
        indiceHabitante = indice;
        var rngMision = new System.Random(semillaPersona ^ 0x4D15);
        TieneMision = rngMision.NextDouble() < probabilidadMision;
        Mision = TieneMision ? ElegirMision(rngMision) : null;
        if (Mision == null) TieneMision = false; // sin assets de misión no hay nada que dar
        Indicador = GetComponent<IndicadorMision>();
        if (TieneMision && Indicador == null) Indicador = gameObject.AddComponent<IndicadorMision>();
        ActualizarIndicadorMision();

        // 1. Sortear profesión por semilla (es azar: dos personas de la misma casa pueden repetir)
        if (asignarTrabajoAlAzar)
        {
            int totalTrabajos = System.Enum.GetValues(typeof(Profesion)).Length;
            trabajo = (Profesion)((indice + rng.Next(totalTrabajos)) % totalTrabajos);
        }

        // 2. Identificar nodos del edificio central (las 4 celdas ortogonales pegadas al centro) y nodo de su zona
        var nodosCentro = new List<int>();
        Vector2Int[] dirsCentro = {
            new Vector2Int(centro - 1, centro),
            new Vector2Int(centro + 1, centro),
            new Vector2Int(centro, centro - 1),
            new Vector2Int(centro, centro + 1)
        };
        foreach (Vector2Int dc in dirsCentro)
            if (indiceDe.TryGetValue(dc, out int idxC))
                nodosCentro.Add(idxC);

        int nodoZona = -1;
        if (trabajo == Profesion.Agua || trabajo == Profesion.Comida || trabajo == Profesion.Energia)
        {
            int idxSeccion = trabajo == Profesion.Agua ? 0 : (trabajo == Profesion.Comida ? 1 : 2);
            Vector2Int celdaZona = wg.ObtenerZonaDeSeccion(macro, idxSeccion);
            if (indiceDe.TryGetValue(celdaZona, out int idxZ))
                nodoZona = idxZ;
        }

        // 3. Punto y dirección de mirada de cada nodo
        int n = celdas.Count;
        Vector3[] punto = new Vector3[n];
        Vector3[] mirar = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector2Int l = celdas[i];
            Vector3 c = wg.CeldaACentroMundoPiso(baseCelda + new Vector3Int(l.x, 0, l.y));
            Vector3 haciaCentro = centroSala - c; haciaCentro.y = 0f;
            bool esSeccion = (l.x == centro - 1 || l.x == centro + 1) && (l.y == centro - 1 || l.y == centro + 1);
            bool esBordeCentro = Mathf.Abs(l.x - centro) + Mathf.Abs(l.y - centro) == 1;

            if (esSeccion)
            {
                punto[i] = c + haciaCentro.normalized * distanciaFrenteSeccion;
                mirar[i] = -haciaCentro.normalized; // mira hacia su estación/sección
            }
            else if (esBordeCentro)
            {
                // En los bordes del edificio central se acerca un poco al centro y mira hacia el edificio central
                punto[i] = c + haciaCentro.normalized * 2.5f;
                mirar[i] = haciaCentro.normalized;
            }
            else
            {
                punto[i] = c;
                mirar[i] = haciaCentro.normalized;
            }
        }

        // 4. Armar secuencia de paradas según el trabajo (Zona <-> Centro + desvíos aleatorios)
        int minP = Mathf.Clamp(Mathf.Min(cantidadParadas.x, cantidadParadas.y), 2, 12);
        int maxP = Mathf.Clamp(Mathf.Max(cantidadParadas.x, cantidadParadas.y), minP, 12);
        int cantidad = rng.Next(minP, maxP + 1);

        var paradas = ConstruirParadasPorTrabajo(cantidad, n, nodoZona, nodosCentro, rng);

        // 5. Desfase lateral acotado (para que con 20 habitantes no se salgan de los pasillos)
        int slotCarril = indice % 5; // 5 carriles repartidos entre -anchoCarril y +anchoCarril
        float factorCarril = (slotCarril - 2) * 0.5f; // -1, -0.5, 0, 0.5, 1
        float lado = factorCarril * anchoCarril;

        Vector3[] parada = new Vector3[n];
        for (int i = 0; i < n; i++)
            parada[i] = punto[i] + Vector3.Cross(Vector3.up, mirar[i]).normalized * lado;

        for (int k = 0; k < paradas.Count; k++)
        {
            int a = paradas[k];
            int b = paradas[(k + 1) % paradas.Count];

            // Si está en su estación de trabajo o en el centro, se queda un poco más trabajando
            float multTrabajo = (a == nodoZona || (trabajo == Profesion.Centro && nodosCentro.Contains(a))) ? 1.35f : 0.85f;
            float espera = Mathf.Lerp(tiempoParada.x, tiempoParada.y, (float)rng.NextDouble()) * multTrabajo;

            tramos.Add(new Tramo
            {
                caminando = false,
                puntos = new List<Vector3> { parada[a] },
                duracion = espera,
                mirarHacia = mirar[a],
            });

            List<int> ruta = RutaEntreNodos(a, b, celdas, indiceDe);
            if (ruta == null) continue;

            var camino = new List<Vector3>(ruta.Count);
            foreach (int r in ruta) camino.Add(punto[r]);
            var conCarril = AplicarCarril(camino, lado);
            conCarril[0] = parada[a];
            conCarril[conCarril.Count - 1] = parada[b];

            float largo = LargoPolilinea(conCarril);
            tramos.Add(new Tramo
            {
                caminando = true,
                puntos = conCarril,
                duracion = Mathf.Max(0.5f, largo / Mathf.Max(0.1f, velocidad)),
                largo = largo,
            });
        }

        duracionCiclo = 0f;
        foreach (Tramo t in tramos) duracionCiclo += t.duracion;
        desfase = (float)rng.NextDouble() * duracionCiclo;
        inicializado = duracionCiclo > 0f;
        primerFrame = true;
    }

    private List<int> ConstruirParadasPorTrabajo(int cantidad, int totalNodos, int nodoZona, List<int> nodosCentro, System.Random rng)
    {
        var paradas = new List<int>(cantidad);

        // Si trabaja en Agua / Comida / Energía: alterna entre SU ZONA, el EDIFICIO CENTRAL y algún DESVÍO aleatorio
        if (nodoZona >= 0 && nodosCentro.Count > 0)
        {
            for (int k = 0; k < cantidad; k++)
            {
                int elegido;
                if (k % 2 == 0)
                {
                    elegido = nodoZona; // Va a su estación
                }
                else if (rng.NextDouble() < probabilidadDesvio)
                {
                    elegido = rng.Next(totalNodos); // Desvío aleatorio en el camino
                }
                else
                {
                    elegido = nodosCentro[rng.Next(nodosCentro.Count)]; // Va al edificio central
                }

                // Evitar dos paradas iguales seguidas
                if (paradas.Count > 0 && elegido == paradas[paradas.Count - 1])
                    elegido = nodosCentro[rng.Next(nodosCentro.Count)];

                paradas.Add(elegido);
            }
            if (paradas.Count > 1 && paradas[paradas.Count - 1] == paradas[0])
                paradas[paradas.Count - 1] = nodosCentro[rng.Next(nodosCentro.Count)];
            return paradas;
        }

        // Si trabaja en el EDIFICIO CENTRAL: orbita los 4 lados del edificio central con algún desvío ocasional
        if (trabajo == Profesion.Centro && nodosCentro.Count >= 2)
        {
            for (int k = 0; k < cantidad; k++)
            {
                bool desvio = rng.NextDouble() < (probabilidadDesvio * 0.6f);
                int p = desvio ? rng.Next(totalNodos) : nodosCentro[rng.Next(nodosCentro.Count)];
                int intentos = 0;
                while (intentos < 20 && ((paradas.Count > 0 && p == paradas[paradas.Count - 1]) ||
                                         (k == cantidad - 1 && paradas.Count > 0 && p == paradas[0])))
                {
                    p = nodosCentro[rng.Next(nodosCentro.Count)];
                    intentos++;
                }
                paradas.Add(p);
            }
            return paradas;
        }

        // General / Ronda: recorre toda la vivienda libremente
        for (int k = 0; k < cantidad; k++)
        {
            int p;
            int intentos = 0;
            do { p = rng.Next(totalNodos); intentos++; }
            while (intentos < 30 && ((paradas.Count > 0 && p == paradas[paradas.Count - 1]) ||
                                     (k == cantidad - 1 && paradas.Count > 0 && p == paradas[0])));
            paradas.Add(p);
        }
        return paradas;
    }

    // ---------- Interacción, Diálogo y Necesidades ----------

    private void ActualizarInteraccion()
    {
        if (!permitirDialogo) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        float dist = Vector3.Distance(transform.position, cam.transform.position);
        if (dist > alcanceInteraccion) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, alcanceInteraccion, ~0, QueryTriggerInteraction.Collide))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                string etiquetaRol = NombreRol();
                string prompt = $"[E] Hablar ({etiquetaRol})";

                if (permitirEntregarRecursoDirecto && JugadorSostieneRecursoDeEsteTrabajo(out _))
                    prompt = $"[E] Entregar suministro ({etiquetaRol})";

                InventoryItem pedido = ObjetoDeMisionEnMano(out _);
                if (pedido != null)
                    prompt = $"[E] Entregar {pedido.data.nombreArticulo} ({etiquetaRol})";

                if (TooltipManager.Instance != null)
                    TooltipManager.Instance.MostrarTooltip(gameObject, prompt);

                if (Input.GetKeyDown(KeyCode.E))
                {
                    Interactuar();
                }
            }
        }
    }

    public void Interactuar()
    {
        if (TooltipManager.Instance != null)
            TooltipManager.Instance.OcultarTooltip();

        EmpezarAtencion();
        if (IntentarEntregarDeMano()) return;
        if (AtenderMision()) return;

        // Mecánica de entrega directa (desactivada por defecto con permitirEntregarRecursoDirecto = false)
        if (permitirEntregarRecursoDirecto && IntentarRecibirRecursoDeMano(out string mensajeEntrega))
        {
            MostrarDialogo(mensajeEntrega);
            return;
        }

        bool tieneNecesidad = TieneNecesidadActiva(out RecursoVivienda recursoFaltante, out float segundosRestantes);
        string frase = ElegirFrase(tieneNecesidad);

        if (tieneNecesidad && segundosRestantes >= 0f)
        {
            int mins = Mathf.Max(0, Mathf.FloorToInt(segundosRestantes / 60f));
            frase += $" (Reserva de {recursoFaltante}: ~{mins} min)";
        }

        MostrarDialogo($"[{NombreRol()}] {frase}");
    }

    // ---------- Misión de cofre ----------

    private static MisionData ElegirMision(System.Random rng)
    {
        if (misionesDisponibles == null) misionesDisponibles = Resources.LoadAll<MisionData>("Misiones");

        float total = 0f;
        foreach (MisionData m in misionesDisponibles)
            if (m != null && m.prefabCofre != null && m.objetoPedido != null) total += m.pesoAparicion;
        if (total <= 0f) return null;

        double tirada = rng.NextDouble() * total;
        MisionData ultima = null;
        foreach (MisionData m in misionesDisponibles)
        {
            if (m == null || m.prefabCofre == null || m.objetoPedido == null) continue;
            ultima = m;
            tirada -= m.pesoAparicion;
            if (tirada <= 0) return m;
        }
        return ultima;
    }

    /// <summary>"!" si la tiene para dar, "?" si está en curso, nada si no tiene o ya se cerró.</summary>
    private void ActualizarIndicadorMision()
    {
        if (Indicador == null) return;
        if (!TieneMision) { Indicador.SetEstado(IndicadorMision.Estado.Ninguna); return; }

        RegistroMisiones.Mision m = RegistroMisiones.Buscar(macroVivienda, indiceHabitante);
        if (m == null) Indicador.SetEstado(IndicadorMision.Estado.Disponible);
        else if (m.estado == RegistroMisiones.Estado.Aceptada) Indicador.SetEstado(IndicadorMision.Estado.EnCurso);
        else Indicador.SetEstado(IndicadorMision.Estado.Ninguna);
    }

    /// <summary>
    /// Hablarle a alguien con misión: la primera vez te la da (y aparece el cofre), después te
    /// recuerda que está en curso. Devuelve false si no hay nada de misión que decir, para que
    /// siga con su charla normal. (Entregar o quedarse el objeto viene en la etapa 2.)
    /// </summary>
    private bool AtenderMision()
    {
        if (!TieneMision || Mision == null || wgMision == null) return false;

        int hash = semillaPersona ^ 0x3F1;
        RegistroMisiones.Mision m = RegistroMisiones.Buscar(macroVivienda, indiceHabitante);
        if (m != null)
        {
            if (m.estado != RegistroMisiones.Estado.Aceptada) return false;
            MostrarDialogo($"[{NombreRol()}] {HabitanteData.ElegirFrase(Mision.frasesEnCurso, hash)}");
            return true;
        }

        GameObject cofre = wgMision.SpawnCofreMision(macroVivienda, Mision, semillaPersona, out Vector3Int celdaCofre);
        if (cofre == null) return false; // no hubo lugar: queda con su "!" para otra vez

        RegistroMisiones.Aceptar(Mision, macroVivienda, indiceHabitante, celdaCofre, cofre);
        ActualizarIndicadorMision();
        MostrarDialogo($"[{NombreRol()}] {HabitanteData.ElegirFrase(Mision.frasesPedido, hash)}");
        return true;
    }

    /// <summary>ItemData del objeto que pide la misión (sale del InventoryItem de su prefab).</summary>
    private ItemData DataPedida =>
        Mision != null && Mision.objetoPedido != null && Mision.objetoPedido.TryGetComponent(out InventoryItem inv) ? inv.data : null;

    /// <summary>
    /// Si tenés en la mano el objeto que pide su misión en curso, devuelve el item (para el prompt
    /// "[E] Entregar" y para entregarlo). Null si no.
    /// </summary>
    private InventoryItem ObjetoDeMisionEnMano(out RegistroMisiones.Mision mision)
    {
        mision = null;
        if (!TieneMision) return null;

        RegistroMisiones.Mision m = RegistroMisiones.Buscar(macroVivienda, indiceHabitante);
        if (m == null || m.estado != RegistroMisiones.Estado.Aceptada) return null;

        ManoJugador mano = FindAnyObjectByType<ManoJugador>();
        InventoryItem item = mano != null ? mano.ItemEnMano : null;
        if (item == null || item.data == null || item.data != DataPedida) return null;

        mision = m;
        return item;
    }

    /// <summary>
    /// Entregar = mirarlo con el objeto pedido en la mano y apretar [E]: se lo queda, misión
    /// completada como entregada (+karma).
    /// </summary>
    private bool IntentarEntregarDeMano()
    {
        InventoryItem item = ObjetoDeMisionEnMano(out RegistroMisiones.Mision m);
        if (item == null) return false;

        Destroy(item.gameObject);
        RegistroMisiones.Completar(m, true);
        ActualizarIndicadorMision();
        MostrarDialogo($"[{NombreRol()}] {HabitanteData.ElegirFrase(Mision.frasesAgradecimiento, semillaPersona ^ 0x7A1)}");
        return true;
    }

    private void MostrarDialogo(string texto)
    {
        if (DialogoUI.Instance != null)
            DialogoUI.Instance.Mostrar(texto, transform);
    }

    /// <summary>
    /// Revisa en RegistroViviendas si el área de este trabajador tiene carencia de su recurso.
    /// </summary>
    public bool TieneNecesidadActiva(out RecursoVivienda recursoRevisado, out float segundosRestantes)
    {
        recursoRevisado = RecursoVivienda.Agua;
        segundosRestantes = -1f;

        RegistroViviendas reg = RegistroViviendas.Instance;
        if (reg == null || !reg.TryObtenerStats(transform.position, out StatsVivienda stats))
            return false;

        switch (trabajo)
        {
            case Profesion.Agua:
                recursoRevisado = RecursoVivienda.Agua;
                segundosRestantes = stats.agua;
                return stats.agua <= umbralNecesidadRecurso;

            case Profesion.Comida:
                recursoRevisado = RecursoVivienda.Comida;
                segundosRestantes = stats.comida;
                return stats.comida <= umbralNecesidadRecurso;

            case Profesion.Energia:
                recursoRevisado = RecursoVivienda.Energia;
                segundosRestantes = stats.energia;
                return stats.energia <= umbralNecesidadRecurso;

            default:
                // Centro y General miran el recurso más bajo de toda la vivienda
                recursoRevisado = RecursoVivienda.Agua;
                segundosRestantes = stats.agua;
                if (stats.comida < segundosRestantes) { recursoRevisado = RecursoVivienda.Comida; segundosRestantes = stats.comida; }
                if (stats.energia < segundosRestantes) { recursoRevisado = RecursoVivienda.Energia; segundosRestantes = stats.energia; }
                return segundosRestantes <= umbralNecesidadRecurso;
        }
    }

    private string ElegirFrase(bool enNecesidad)
    {
        FrasesProfesion datos = CompendioDe(trabajo);
        string[] pool = datos != null ? (enNecesidad ? datos.frasesNecesidad : datos.frasesNormal) : null;
        if (pool == null || pool.Length == 0)
            pool = (enNecesidad ? FrasesNecesidadPorDefecto : FrasesNormalPorDefecto)[(int)trabajo];

        if (pool == null || pool.Length == 0) return "...";
        // Cambia de frase cada 15 s. Índice con (h % n + n) % n y no con Mathf.Abs: Abs de un entero
        // muy negativo (int.MinValue) sigue siendo negativo y tiraba un error de índice.
        int h = semillaPersona + Mathf.FloorToInt(Time.time / 15f);
        return pool[(h % pool.Length + pool.Length) % pool.Length];
    }

    private string NombreRol()
    {
        switch (trabajo)
        {
            case Profesion.Centro: return "Edificio Central";
            case Profesion.Agua: return "Encargado de Agua";
            case Profesion.Comida: return "Encargado de Comida";
            case Profesion.Energia: return "Encargado de Energía";
            default: return "Apoyo General";
        }
    }

    // ---------- Entrega directa de recursos (preparada y desactivada por defecto) ----------

    private bool JugadorSostieneRecursoDeEsteTrabajo(out ConsumibleData consumible)
    {
        consumible = null;
        ManoJugador mano = FindAnyObjectByType<ManoJugador>();
        if (mano == null || mano.ItemEnMano == null) return false;
        consumible = mano.ItemEnMano.data as ConsumibleData;
        if (consumible == null) return false;

        switch (trabajo)
        {
            case Profesion.Agua: return consumible.restaurarAgua > 0f;
            case Profesion.Comida: return consumible.restaurarComida > 0f;
            case Profesion.Energia: return consumible.restaurarBateria > 0f;
            default:
                return consumible.restaurarAgua > 0f || consumible.restaurarComida > 0f || consumible.restaurarBateria > 0f;
        }
    }

    private bool IntentarRecibirRecursoDeMano(out string mensaje)
    {
        mensaje = null;
        if (RegistroViviendas.Instance == null) return false;

        ManoJugador mano = FindAnyObjectByType<ManoJugador>();
        if (mano == null || mano.ItemEnMano == null) return false;
        ConsumibleData cons = mano.ItemEnMano.data as ConsumibleData;
        if (cons == null) return false;

        bool entregoAlgo = false;
        if ((trabajo == Profesion.Agua || trabajo == Profesion.Centro || trabajo == Profesion.General) && cons.restaurarAgua > 0f)
            entregoAlgo |= RegistroViviendas.Instance.Agregar(transform.position, RecursoVivienda.Agua, cons.restaurarAgua);

        if ((trabajo == Profesion.Comida || trabajo == Profesion.Centro || trabajo == Profesion.General) && cons.restaurarComida > 0f)
            entregoAlgo |= RegistroViviendas.Instance.Agregar(transform.position, RecursoVivienda.Comida, cons.restaurarComida);

        if ((trabajo == Profesion.Energia || trabajo == Profesion.Centro || trabajo == Profesion.General) && cons.restaurarBateria > 0f)
            entregoAlgo |= RegistroViviendas.Instance.Agregar(transform.position, RecursoVivienda.Energia, cons.restaurarBateria);

        if (!entregoAlgo) return false;

        Destroy(mano.ItemEnMano.gameObject);
        mensaje = $"[{NombreRol()}] ¡Gracias! Sumé {cons.nombreArticulo} a las reservas de la vivienda.";
        return true;
    }

    // ---------- Navegación y Movimiento ----------

    /// <summary>BFS entre dos nodos de la planta, con vecinos en orden fijo (determinista).</summary>
    private static List<int> RutaEntreNodos(int desde, int hasta, List<Vector2Int> celdas, Dictionary<Vector2Int, int> indiceDe)
    {
        if (desde == hasta) return new List<int> { desde };
        Vector2Int[] dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        var previo = new Dictionary<int, int> { [desde] = -1 };
        var cola = new Queue<int>();
        cola.Enqueue(desde);
        while (cola.Count > 0)
        {
            int actual = cola.Dequeue();
            if (actual == hasta) break;
            foreach (Vector2Int d in dirs)
            {
                if (!indiceDe.TryGetValue(celdas[actual] + d, out int vecino) || previo.ContainsKey(vecino)) continue;
                previo[vecino] = actual;
                cola.Enqueue(vecino);
            }
        }
        if (!previo.ContainsKey(hasta)) return null;

        var ruta = new List<int>();
        for (int v = hasta; v != -1; v = previo[v]) ruta.Add(v);
        ruta.Reverse();
        return ruta;
    }

    /// <summary>Corre cada punto de la polilínea hacia un costado, según la dirección del tramo.</summary>
    private static List<Vector3> AplicarCarril(List<Vector3> puntos, float lado)
    {
        var res = new List<Vector3>(puntos.Count);
        for (int i = 0; i < puntos.Count; i++)
        {
            Vector3 antes = i > 0 ? puntos[i] - puntos[i - 1] : Vector3.zero;
            Vector3 despues = i < puntos.Count - 1 ? puntos[i + 1] - puntos[i] : Vector3.zero;
            Vector3 dir = antes + despues; dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) { res.Add(puntos[i]); continue; }
            res.Add(puntos[i] + Vector3.Cross(Vector3.up, dir.normalized) * lado);
        }
        return res;
    }

    private static float LargoPolilinea(List<Vector3> p)
    {
        float l = 0f;
        for (int i = 1; i < p.Count; i++) l += Vector3.Distance(p[i - 1], p[i]);
        return l;
    }

    private static Vector3 PuntoEnPolilinea(List<Vector3> p, float distancia)
    {
        for (int i = 1; i < p.Count; i++)
        {
            float seg = Vector3.Distance(p[i - 1], p[i]);
            if (distancia <= seg || i == p.Count - 1)
                return Vector3.Lerp(p[i - 1], p[i], seg > 1e-4f ? Mathf.Clamp01(distancia / seg) : 1f);
            distancia -= seg;
        }
        return p[p.Count - 1];
    }

    private static float Reloj()
    {
        return RegistroViviendas.Instance != null ? RegistroViviendas.Instance.TiempoMundo : Time.time;
    }

    void Update()
    {
        if (!inicializado) return;

        ActualizarInteraccion();

        // La rutina es función del reloj: mientras te atiende, lo que avanza el reloj se le descuenta
        // al desfase, así al soltarte sigue exactamente donde quedó en vez de "saltar" adelante.
        float ahora = Reloj();
        if (atendiendo)
        {
            desfase -= ahora - relojAnterior;
            relojAnterior = ahora;
            ActualizarAtencion();
            return;
        }
        relojAnterior = ahora;

        float t = Mathf.Repeat(ahora + desfase, duracionCiclo);
        Vector3 pos = transform.position;
        Vector3 mirar = transform.forward;

        foreach (Tramo tramo in tramos)
        {
            if (t > tramo.duracion) { t -= tramo.duracion; continue; }
            Caminando = tramo.caminando;

            if (!tramo.caminando)
            {
                pos = tramo.puntos[0];
                mirar = tramo.mirarHacia;
            }
            else
            {
                float u = Mathf.SmoothStep(0f, 1f, tramo.duracion > 0f ? t / tramo.duracion : 1f);
                float d = u * tramo.largo;
                pos = PuntoEnPolilinea(tramo.puntos, d);
                Vector3 adelante = PuntoEnPolilinea(tramo.puntos, Mathf.Min(tramo.largo, d + 0.5f)) - pos;
                if (adelante.sqrMagnitude > 1e-4f) mirar = adelante;
            }
            break;
        }

        esquive = Vector3.SmoothDamp(esquive, CalcularEsquive(pos, mirar), ref velocidadEsquive, suavidadEsquive);
        transform.position = pos + esquive + Vector3.up * (alturaBase + alturaSobrePiso);
        mirar.y = 0f;
        if (mirar.sqrMagnitude > 1e-4f)
        {
            Quaternion objetivo = Quaternion.LookRotation(mirar.normalized, Vector3.up);
            transform.rotation = primerFrame
                ? objetivo
                : Quaternion.Slerp(transform.rotation, objetivo, Time.deltaTime * velocidadGiro);
        }
        primerFrame = false;
    }

    /// <summary>
    /// Corrimiento lateral para no chocarte. Es solo un desplazamiento sobre la ruta (la rutina
    /// sigue siendo función del tiempo): cuando te alejas vuelve solo a su carril. Caminando se
    /// corre al lado contrario al tuyo si estás adelante; parado, se aparta si te le metes encima.
    /// </summary>
    private Vector3 CalcularEsquive(Vector3 posRuta, Vector3 adelante)
    {
        Camera cam = Camera.main;
        if (cam == null || radioEsquive <= 0f) return Vector3.zero;

        Vector3 aJugador = cam.transform.position - posRuta;
        aJugador.y = 0f;
        if (aJugador.sqrMagnitude > radioEsquive * radioEsquive) return Vector3.zero;

        adelante.y = 0f;
        if (adelante.sqrMagnitude < 1e-4f) adelante = transform.forward;
        adelante.Normalize();
        Vector3 derecha = Vector3.Cross(Vector3.up, adelante);

        float frente = Vector3.Dot(aJugador, adelante);
        float lateral = Vector3.Dot(aJugador, derecha);

        if (Caminando)
        {
            if (frente < -0.3f) return Vector3.zero;              // ya te pasó: no se corre
            float lado = lateral >= 0f ? -1f : 1f;                // al lado contrario al tuyo
            float cuanto = Mathf.Clamp(radioEsquive - Mathf.Abs(lateral), 0f, esquiveMaximo);
            return derecha * (lado * cuanto);
        }

        // Parado: se aparta en línea recta de ti, lo justo para no quedar encimados.
        float dist = aJugador.magnitude;
        Vector3 lejos = dist > 1e-3f ? -aJugador / dist : -derecha;
        return lejos * Mathf.Min(radioEsquive - dist, esquiveMaximo);
    }

    // ---------- Atender al jugador mientras le habla ----------

    private bool atendiendo;
    private float relojAnterior;
    private float tiempoSinMirada;

    private void EmpezarAtencion()
    {
        if (!atendiendo) relojAnterior = Reloj();
        atendiendo = true;
        tiempoSinMirada = 0f;
        Caminando = false;
    }

    /// <summary>
    /// Quieto, girado hacia vos. Si dejás de mirarlo cuenta esperaAlDejarDeMirar segundos (y vuelve
    /// a contar de cero si lo volvés a mirar); si te alejás demasiado, se suelta al toque.
    /// </summary>
    private void ActualizarAtencion()
    {
        Camera cam = Camera.main;
        if (cam == null) { atendiendo = false; return; }

        Vector3 aJugador = cam.transform.position - transform.position;
        aJugador.y = 0f;
        if (aJugador.magnitude > alcanceInteraccion * multiplicadorDistanciaAtencion) { atendiendo = false; return; }

        if (JugadorLoMira(cam, alcanceInteraccion * multiplicadorDistanciaAtencion)) tiempoSinMirada = 0f;
        else
        {
            tiempoSinMirada += Time.deltaTime;
            if (tiempoSinMirada >= esperaAlDejarDeMirar) { atendiendo = false; return; }
        }

        if (aJugador.sqrMagnitude > 1e-4f)
        {
            Quaternion objetivo = Quaternion.LookRotation(aJugador.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, objetivo, Time.deltaTime * velocidadGiro);
        }
    }

    private bool JugadorLoMira(Camera cam, float alcance)
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        return Physics.Raycast(ray, out RaycastHit hit, alcance, ~0, QueryTriggerInteraction.Collide)
            && (hit.transform == transform || hit.transform.IsChildOf(transform));
    }

    void OnEnable()
    {
        primerFrame = true;
        atendiendo = false; // si lo apagó la burbuja mientras te hablaba, vuelve a su rutina
    }
}
