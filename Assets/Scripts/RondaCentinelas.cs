using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manager de rondas de Centinela. No mantiene población constante (a diferencia de
/// EnjambreDrones): dispara rondas temporales por temporizador aleatorio y/o por
/// AlertaCentinelas.Disparar(). La ronda SALE de la sala cuartel más cercana
/// (RoomData.esCuartelCentinelas, ver WorldGenerator.TryEncontrarSalaCentinela) — si no
/// hay ninguna cerca, NO spawnea nada (reintenta más tarde). Al terminar la ronda, los
/// centinelas se retiran caminando de vuelta al cuartel en vez de despawnear de golpe.
/// </summary>
public class RondaCentinelas : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject prefabCentinela;
    [Tooltip("Jugador a usar como centro de spawn. Si está vacío, se busca por tag 'Player'.")]
    public Transform jugador;

    [Header("Disparo de ronda")]
    [Tooltip("Próxima ronda automática entre este mínimo y máximo de segundos.")]
    public float intervaloRondaMin = 90f;
    public float intervaloRondaMax = 240f;
    [Tooltip("Si una AlertaCentinelas.Disparar() ocurre más lejos que esto del jugador, se ignora.")]
    public float radioAlertaValida = 60f;

    [Header("Ronda")]
    [Tooltip("Máximo de centinelas simultáneos (poco espacio disponible: 2 por defecto).")]
    public int cantidadPorRonda = 2;
    public float duracionRondaMin = 30f;
    public float duracionRondaMax = 60f;

    [Header("Cuartel (RoomData.esCuartelCentinelas)")]
    [Tooltip("TECHO (en macro chunks) de la búsqueda del cuartel más cercano al jugador. La búsqueda va por cáscaras hacia afuera y corta en la primera que tenga un cuartel, así que subirlo NO cuesta nada cuando hay uno cerca: solo amplía hasta dónde se estira cuando no lo hay. Con 3 hay muchas rondas que no spawnean (1 macro chunk = 100m); 10-15 hace que prácticamente siempre encuentre. Sin cuartel dentro del techo, la ronda NO spawnea y reintenta más tarde.")]
    public int radioBusquedaCuartelMacros = 3;
    [Tooltip("Altura sobre el piso a la que se instancia el centinela dentro del cuartel.")]
    public float alturaSpawnCuartel = 0.1f;
    [Tooltip("Si no se encontró ningún cuartel (el mundo recién está cargando, o no hay ninguna sala esCuartelCentinelas cerca), cada cuántos segundos reintenta.")]
    public float reintentoSinCuartelSegundos = 5f;

    private float temporizadorProximaRonda;
    private float temporizadorRondaActual;
    private bool rondaActiva;
    private readonly List<GameObject> centinelasVivos = new List<GameObject>();

    void OnEnable() => AlertaCentinelas.OnAlerta += OnAlertaRecibida;
    void OnDisable() => AlertaCentinelas.OnAlerta -= OnAlertaRecibida;

    void Start()
    {
        if (jugador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) jugador = p.transform;
        }

        // Primera ronda inmediata en vez de esperar el temporizador (90-240s de arranque
        // en vacío se sentía como que el sistema no hacía nada). ⚠️ NO se dispara acá: el orden
        // de Start entre scripts es arbitrario, así que el jugador puede no estar todavía en su
        // posición de spawn y la ronda saldría del cuartel equivocado (se notaba sobre todo con
        // 'Spawn En Cuartel Debug'). Se espera a que el mundo esté listo, en Update.
        ReprogramarProximaRonda();
    }

    private bool primeraRondaHecha;

    void Update()
    {
        if (jugador == null || prefabCentinela == null) return;

        // Primera ronda apenas el mundo queda listo (el jugador ya está posicionado).
        if (!primeraRondaHecha)
        {
            // ⚠️ También hay que esperar a que se cierre el menú. MundoListo se pone en true ANTES
            // de que termine el recorrido de cámara del menú, así que solo con eso la primera ronda
            // salía mientras seguías mirando la intro — y se escuchaba el loop de patrulla.
            if (WorldGenerator.Instance == null || !WorldGenerator.Instance.MundoListo) return;
            if (ModoMenu.Activo) return;
            primeraRondaHecha = true;
            IniciarRonda();
            return;
        }

        if (!rondaActiva)
        {
            temporizadorProximaRonda -= Time.deltaTime;
            if (temporizadorProximaRonda <= 0f) IniciarRonda();
            return;
        }

        LimpiarNulos();
        temporizadorRondaActual -= Time.deltaTime;
        if (temporizadorRondaActual <= 0f) TerminarRonda();
    }

    private void OnAlertaRecibida(Vector3 posicion)
    {
        if (jugador == null) return;
        if (Vector3.Distance(posicion, jugador.position) > radioAlertaValida) return;

        if (!rondaActiva) IniciarRonda();
        else temporizadorRondaActual = Mathf.Max(temporizadorRondaActual, duracionRondaMin);
    }

    private void IniciarRonda()
    {
        List<GameObject> nuevos = SpawnearDesdeCuartel();

        if (nuevos.Count == 0)
        {
            // Sin cuartel cerca (o el mundo recién está cargando) — NO spawnear en cualquier
            // lado (ej. la sala donde esté parado el jugador). Reintentar más tarde nomás.
            rondaActiva = false;
            temporizadorProximaRonda = reintentoSinCuartelSegundos;
            return;
        }

        rondaActiva = true;
        temporizadorRondaActual = Random.Range(duracionRondaMin, duracionRondaMax);
        centinelasVivos.AddRange(nuevos);
    }

    private List<GameObject> SpawnearDesdeCuartel()
    {
        var nuevos = new List<GameObject>();

        WorldGenerator wg = WorldGenerator.Instance;
        if (wg == null || jugador == null) return nuevos;
        if (!wg.TryEncontrarSalaCentinela(jugador.position, radioBusquedaCuartelMacros, out Vector3Int celdaCuartel))
            return nuevos;

        Vector3 centro = wg.CeldaACentroMundoPiso(celdaCuartel) + Vector3.up * alturaSpawnCuartel;
        for (int i = 0; i < cantidadPorRonda; i++)
        {
            // Separados entre sí para que no spawneen apilados en el mismo punto
            Vector3 offset = new Vector3((i % 2 == 0 ? -1 : 1) * 2f, 0f, (i / 2) * 2f);
            nuevos.Add(Instantiate(prefabCentinela, centro + offset, Quaternion.identity));
        }
        // La distancia importa para tunear radioBusquedaCuartelMacros: si el cuartel queda muy
        // lejos, los centinelas salen de un lugar al que el jugador no va a llegar nunca.
        Debug.Log($"[RondaCentinelas] Ronda de {cantidadPorRonda} desde cuartel en celda {celdaCuartel} · a {Vector3.Distance(centro, jugador.position):F0}m del jugador.");
        return nuevos;
    }

    private void TerminarRonda()
    {
        rondaActiva = false;
        ReprogramarProximaRonda();

        foreach (GameObject c in centinelasVivos)
        {
            if (c == null) continue;
            Centinela comp = c.GetComponent<Centinela>();
            if (comp != null) comp.IniciarRetiro();
        }
        centinelasVivos.Clear();
    }

    private void ReprogramarProximaRonda()
    {
        temporizadorProximaRonda = Random.Range(intervaloRondaMin, intervaloRondaMax);
    }

    private void LimpiarNulos()
    {
        for (int i = centinelasVivos.Count - 1; i >= 0; i--)
        {
            if (centinelasVivos[i] == null) centinelasVivos.RemoveAt(i);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (jugador == null || WorldGenerator.Instance == null) return;
        WorldGenerator wg = WorldGenerator.Instance;
        if (wg.TryEncontrarSalaCentinela(jugador.position, radioBusquedaCuartelMacros, out Vector3Int celdaCuartel))
        {
            Gizmos.color = new Color(1f, 0.2f, 0.6f, 0.6f);
            Gizmos.DrawWireSphere(wg.CeldaACentroMundoPiso(celdaCuartel), 2f);
        }
    }
}
