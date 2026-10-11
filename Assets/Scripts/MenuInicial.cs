using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Controlador unico del menu principal (2 pantallas):
///   - Menu1: "presiona cualquier tecla para continuar".
///   - Menu2: menu real con botones (Jugar, Opciones, etc.).
/// Mientras alguno de los dos este activo, el cursor queda visible y libre.
/// Cuando ambos se ocultan, se bloquea/oculta el cursor para jugar.
/// Ademas, al iniciar, ubica la camara del menu en una celda del mundo
/// a cierta distancia (por pasillos, no en linea recta) del jugador.
/// </summary>
public class MenuInicial : MonoBehaviour
{
    [Header("Pantallas")]
    [Tooltip("Menu1: pantalla de 'presiona cualquier tecla'. Se desactiva al continuar.")]
    public GameObject menu1;
    [Tooltip("Menu2: menu real con botones (Jugar, Opciones, etc.). Se activa al continuar.")]
    public GameObject menu2;

    [Header("Camara del menu")]
    [Tooltip("Camara que se posiciona lejos del jugador mientras se ve el menu (la Main Camera, ya no hija del Player).")]
    public Transform camaraMenu;
    [Tooltip("Distancia en celdas (por pasillos) a la que se ubica la camara respecto del jugador.")]
    public int distanciaCeldasCamara = 2;
    [Tooltip("Altura sobre el piso de la celda a la que se sube la camara (simula altura de ojos).")]
    public float alturaCamara = 1.6f;
    [Tooltip("Tope de celdas a explorar buscando un lugar valido (seguridad, no deberia hacer falta tocarlo).")]
    public int maxNodosBusquedaCamara = 300;

    [Header("Enganche con el jugador")]
    [Tooltip("Empty 'Cabeza' hijo del Player. Al presionar Jugar, la camara se pega ahi.")]
    public Transform cabezaJugador;
    [Tooltip("Velocidad (metros/segundo) del viaje de la camara por los pasillos hasta el jugador.")]
    public float velocidadViajeCamara = 6f;
    [Tooltip("Si esta destildado, 'Jugar' engancha directo a la cabeza sin el recorrido de camara (para probar rapido). Tildar para el flujo normal.")]
    public bool activarViajeCamara = true;

    [Header("Menu 1")]
    [Tooltip("Segundos que Menu1 ignora cualquier tecla/click al inicio, para tapar la carga del mundo.")]
    public float bloqueoInicialSegundos = 2f;
    [Tooltip("CanvasGroup de Menu1 para el fade out. Si se deja vacio, se busca/agrega uno automaticamente.")]
    public CanvasGroup menu1CanvasGroup;
    [Tooltip("Duracion del fade out de Menu1 al continuar.")]
    public float duracionFadeMenu1 = 0.6f;

    [Header("Menu 2")]
    [Tooltip("Boton 'Continuar'. Opcional: si no hay partida guardada, se desactiva solo.")]
    public UnityEngine.UI.Button botonContinuar;

    [Header("Ocultar mientras el menú está en pantalla")]
    [Tooltip("Objetos del HUD de juego (mira, sliders, etc.) que se apagan mientras algún menú " +
             "esté activo (Menu1 o Menu2) y se reactivan apenas ambos se cierran.")]
    public GameObject[] ocultarMientrasMenu;

    private float tiempoInicio;
    private bool yaContinuo = false;
    private bool cursorBloqueado = false;

    private static readonly Vector3Int[] Direcciones =
    {
        Vector3Int.right, Vector3Int.left,
        Vector3Int.up, Vector3Int.down,
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    void Start()
    {
        if (menu1 != null) menu1.SetActive(true);
        if (menu2 != null) menu2.SetActive(false);
        ModoMenu.Activo = true;
        tiempoInicio = Time.time;

        SetActivoHudOculto(false);

        if (botonContinuar != null) botonContinuar.interactable = SistemaGuardado.ExisteGuardado();

        StartCoroutine(PosicionarCamaraMenu());
    }

    // Enganchar en el boton "Jugar" (OnClick) de Menu2: partida nueva.
    // Prioridad: 1) usarSemillaAleatoria -> mundo nuevo de verdad (semilla distinta).
    //            2) spawnEnVivienda actua siempre por separado, adentro de DefinirPuntoDeSpawn,
    //               sea cual sea el camino de arriba.
    public void Jugar()
    {
        // Si la camara quedo pegada a "Cabeza" de una partida anterior, la soltamos ANTES de
        // reposicionar al jugador: si no, al ser hija se moveria junto con el y la ruta le
        // quedaria en largo cero (celda camara == celda jugador), pareciendo un teletransporte.
        if (camaraMenu != null && camaraMenu.parent == cabezaJugador)
        {
            camaraMenu.SetParent(null, worldPositionStays: true);
        }

        WorldGenerator wg = WorldGenerator.Instance;
        if (wg != null)
        {
            if (wg.usarSemillaAleatoria)
            {
                wg.ReiniciarMundoConSemillaAleatoria();
            }
            else
            {
                wg.IniciarNuevaPartidaAleatoria();
            }
        }

        if (menu2 != null) menu2.SetActive(false);

        if (activarViajeCamara)
        {
            StartCoroutine(ViajarCamaraHaciaJugador());
        }
        else
        {
            PegarCamaraAlJugador();
            ModoMenu.Activo = false;
            SetActivoHudOculto(true);
        }
    }

    // Enganchar en el boton "Continuar" (OnClick) de Menu2: usa el punto guardado
    // (ya quedo cargado ahi por WorldGenerator al iniciar la escena, no hace falta reposicionar).
    // Sin viaje de camara por ahora, enganche directo.
    public void Continuar()
    {
        if (menu2 != null) menu2.SetActive(false);
        PegarCamaraAlJugador();
        ModoMenu.Activo = false;
        SetActivoHudOculto(true);
    }

    /// <summary>SetActive(activo) sobre todo lo listado en ocultarMientrasMenu. activo=true reactiva el HUD.</summary>
    private void SetActivoHudOculto(bool activo)
    {
        if (ocultarMientrasMenu == null) return;
        foreach (GameObject go in ocultarMientrasMenu)
        {
            if (go != null) go.SetActive(activo);
        }
    }

    /// <summary>
    /// Calcula la ruta de celdas (BFS, mismo criterio que usan los centinelas) desde la celda
    /// actual de la camara hasta la celda del jugador, y la hace viajar por el centro de cada
    /// celda con DOTween hasta llegar. Al terminar, se pega a "Cabeza" y descongela al jugador.
    /// Si no hay camino (ej. la camara quedo en una zona desconectada tras un reinicio de
    /// semilla), se salta el viaje y engancha directo.
    /// </summary>
    private IEnumerator ViajarCamaraHaciaJugador()
    {
        if (camaraMenu == null)
        {
            PegarCamaraAlJugador();
            ModoMenu.Activo = false;
            SetActivoHudOculto(true);
            yield break;
        }

        WorldGenerator wg = WorldGenerator.Instance;
        while (wg == null || wg.jugador == null || !wg.MundoListo)
        {
            wg = WorldGenerator.Instance;
            yield return null;
        }

        Vector3Int celdaJugador = wg.MundoACelda(wg.jugador.position);

        // El jugador puede haber terminado en cualquier parte del mapa (spawn random),
        // lejos de donde la camara estaba parada al principio del menu. Antes de viajar,
        // la re-ubicamos cerca del jugador NUEVO (misma logica que al inicio), para que el
        // recorrido sea siempre corto y no se pase del presupuesto de busqueda de la ruta.
        Vector3Int celdaCamara;
        bool cercaEncontrada = false;
        int intentos = 0;
        while (!cercaEncontrada && intentos < 60)
        {
            cercaEncontrada = BuscarCeldaADistancia(wg, celdaJugador, distanciaCeldasCamara, maxNodosBusquedaCamara, out celdaCamara);
            if (!cercaEncontrada)
            {
                intentos++;
                yield return null;
            }
            else
            {
                Vector3 posBase = wg.CeldaACentroMundoPiso(celdaCamara);
                camaraMenu.position = posBase + Vector3.up * alturaCamara;
            }
        }

        if (!cercaEncontrada)
        {
            PegarCamaraAlJugador();
            ModoMenu.Activo = false;
            SetActivoHudOculto(true);
            yield break;
        }

        celdaCamara = wg.MundoACelda(camaraMenu.position);

        List<Vector3Int> ruta = new List<Vector3Int>();
        bool encontrada = wg.TryCalcularRutaCeldas(celdaCamara, celdaJugador, maxNodosBusquedaCamara, ruta);

        if (!encontrada || ruta.Count == 0)
        {
            PegarCamaraAlJugador();
            ModoMenu.Activo = false;
            SetActivoHudOculto(true);
            yield break;
        }

        Vector3[] puntos = new Vector3[ruta.Count];
        for (int i = 0; i < ruta.Count; i++)
        {
            puntos[i] = wg.CeldaACentroMundoPiso(ruta[i]) + Vector3.up * alturaCamara;
        }

        float distanciaTotal = Vector3.Distance(camaraMenu.position, puntos[0]);
        for (int i = 1; i < puntos.Length; i++)
        {
            distanciaTotal += Vector3.Distance(puntos[i - 1], puntos[i]);
        }

        float duracion = Mathf.Max(0.3f, distanciaTotal / velocidadViajeCamara);

        bool terminado = false;
        camaraMenu.DOPath(puntos, duracion, PathType.Linear)
            .SetEase(Ease.Linear)
            .SetLookAt(0.02f)
            .OnComplete(() => terminado = true);

        while (!terminado) yield return null;

        PegarCamaraAlJugador();
        ModoMenu.Activo = false;
        SetActivoHudOculto(true);
    }

    /// <summary>
    /// Reparenta la camara del menu a "Cabeza" y la deja en (0,0,0) local,
    /// para que a partir de ahi se mueva y gire junto con el jugador.
    /// (Por ahora es un enganche directo; despues esto pasa a ocurrir
    /// recien cuando termine el viaje de la camara por los pasillos.)
    /// </summary>
    private void PegarCamaraAlJugador()
    {
        if (camaraMenu == null || cabezaJugador == null) return;

        camaraMenu.SetParent(cabezaJugador, worldPositionStays: false);
        camaraMenu.localPosition = Vector3.zero;
        camaraMenu.localRotation = Quaternion.identity;

        MouseLook mouseLook = camaraMenu.GetComponent<MouseLook>();
        if (mouseLook != null)
        {
            mouseLook.playerBody = cabezaJugador.parent;
        }
    }

    void Update()
    {
        bool algunMenuActivo = (menu1 != null && menu1.activeInHierarchy) ||
                               (menu2 != null && menu2.activeInHierarchy);

        if (algunMenuActivo)
        {
            // Fuerza cursor visible sin importar donde se haga click.
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
            cursorBloqueado = false;
        }
        else if (!cursorBloqueado)
        {
            // Ambos menus desaparecieron: bloqueamos el cursor para jugar.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            cursorBloqueado = true;
        }

        if (yaContinuo) return;

        if (Time.time - tiempoInicio < bloqueoInicialSegundos) return;

        if (Input.anyKeyDown)
        {
            yaContinuo = true;
            if (menu2 != null) menu2.SetActive(true);
            OcultarMenu1ConFade();
        }
    }

    private void OcultarMenu1ConFade()
    {
        if (menu1 == null) return;

        CanvasGroup cg = menu1CanvasGroup != null ? menu1CanvasGroup : menu1.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            Debug.LogWarning("[MenuInicial] Menu1 no tiene CanvasGroup, agregale uno manualmente para el fade. Desactivando sin fade.");
            menu1.SetActive(false);
            return;
        }

        cg.DOFade(0f, duracionFadeMenu1).OnComplete(() => menu1.SetActive(false));
    }

    /// <summary>
    /// Espera a que el mundo tenga celdas generadas alrededor del jugador y
    /// ubica la camara del menu en una celda transitable a distanciaCeldasCamara
    /// pasillos de distancia (BFS de 6 vecinos, igual criterio que usa el WorldGenerator
    /// para las rutas de los centinelas).
    /// </summary>
    private IEnumerator PosicionarCamaraMenu()
    {
        if (camaraMenu == null) yield break;

        // Esperamos a que el jugador ya este posicionado (guardado o spawn aleatorio)
        // y el mundo inicial generado, si no la camara podria leer la posicion "cruda"
        // del jugador en la escena (ej. una zona de pruebas) antes de que lo reubiquen.
        while (WorldGenerator.Instance == null || WorldGenerator.Instance.jugador == null || !WorldGenerator.Instance.MundoListo)
        {
            yield return null;
        }

        WorldGenerator wg = WorldGenerator.Instance;
        Vector3Int celdaJugador = default;
        Vector3Int celdaElegida = default;
        bool encontrada = false;

        while (!encontrada)
        {
            celdaJugador = wg.MundoACelda(wg.jugador.position);
            encontrada = BuscarCeldaADistancia(wg, celdaJugador, distanciaCeldasCamara, maxNodosBusquedaCamara, out celdaElegida);
            if (!encontrada) yield return null;
        }

        Vector3 posBase = wg.CeldaACentroMundoPiso(celdaElegida);
        camaraMenu.position = posBase + Vector3.up * alturaCamara;

        // Apunta al centro de la celda del jugador (grilla, fijo), no a su transform.position
        // en vivo: asi siempre mira hacia el pasillo/sala real y no a un punto raro a mitad
        // de un teletransporte o de una fisica en pleno ajuste.
        Vector3 puntoMira = wg.CeldaACentroMundoPiso(celdaJugador) + Vector3.up * alturaCamara;
        camaraMenu.LookAt(puntoMira);
    }

    /// <summary>
    /// BFS de 6 vecinos sobre celdas vacias (mismo criterio que TryCalcularRutaCeldas)
    /// buscando alguna celda a exactamente "distancia" pasos del origen.
    /// </summary>
    private bool BuscarCeldaADistancia(WorldGenerator wg, Vector3Int origen, int distancia, int maxNodos, out Vector3Int resultado)
    {
        var visitadas = new HashSet<Vector3Int> { origen };
        var frontera = new Queue<(Vector3Int celda, int dist)>();
        frontera.Enqueue((origen, 0));

        var candidatas = new List<Vector3Int>();
        int nodosVisitados = 0;

        while (frontera.Count > 0 && nodosVisitados < maxNodos)
        {
            var (celda, dist) = frontera.Dequeue();
            nodosVisitados++;

            if (dist == distancia)
            {
                candidatas.Add(celda);
                continue;
            }

            foreach (var dir in Direcciones)
            {
                Vector3Int vecino = celda + dir;
                if (visitadas.Contains(vecino)) continue;
                if (!wg.IsCellEmpty(vecino.x, vecino.y, vecino.z)) continue;

                visitadas.Add(vecino);
                frontera.Enqueue((vecino, dist + 1));
            }
        }

        if (candidatas.Count > 0)
        {
            resultado = candidatas[Random.Range(0, candidatas.Count)];
            return true;
        }

        resultado = default;
        return false;
    }
}
