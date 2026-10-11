using UnityEngine;

/// <summary>
/// Permite recoger, sostener y soltar objetos (InventoryItem) del mundo.
/// Click izquierdo (mira al centro de la pantalla): agarra el objeto apuntado;
/// si ya tienes uno en la mano, lo suelta conservando algo de inercia.
/// </summary>
public class ManoJugador : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Punto donde se sostiene el objeto (hijo de la cÃ¡mara).")]
    public Transform manoDerecha;
    [Tooltip("CÃ¡mara usada para el raycast. Si estÃ¡ vacÃ­o, usa Camera.main.")]
    public Camera camara;
    [Tooltip("Controlador de la mochila, para saber si estÃ¡ abierta (modo inspecciÃ³n 2D).")]
    public ControladorMochila mochila;
    [Tooltip("Sistema de tirolina: mientras esté en modo apuntado (click derecho, mano vacía), no se puede " +
             "agarrar objetos con click izquierdo (ambos reaccionan al mismo gesto). Si se deja vacío, se busca automáticamente.")]
    public SistemaTirolina tirolina;

    [Header("Ajustes")]
    [Tooltip("Alcance del raycast para agarrar objetos (metros).")]
    public float alcance = 4f;
    [Tooltip("Capas que se pueden agarrar. Configura el layer 'ObjetoInventario'.")]
    public LayerMask capaAgarrable = ~0;
    [Tooltip("Multiplicador de inercia heredada al soltar.")]
    public float multiplicadorInercia = 1.2f;

    [Header("Modo InspecciÃ³n (mochila abierta)")]
    [Tooltip("Distancia inicial del objeto frente a la cÃ¡mara.")]
    public float distanciaInspeccion = 1.5f;
    public float distanciaMin = 0.6f;
    public float distanciaMax = 4f;
    [Tooltip("Velocidad de zoom con la rueda del ratÃ³n.")]
    public float velocidadRueda = 2f;

    private InventoryItem enMano;
    private Vector3 ultimaPos;
    private Vector3 velocidad;
    private bool enModo2D = false;

    // Estado de snap de mÃ³dulo
    private MochilaGrid gridMochila;
    private MochilaVisual visualMochila;
    private bool snapValido = false;
    private Vector2Int snapOrigen;

    // Estado de vista previa de colocaciÃ³n de anclaje
    private bool estaApuntandoAnclaje = false;
    private bool previewValido = false;
    private Vector3 posicionPreviewValida;

    [Header("Inspección / Análisis (Click Der con objeto en mano)")]
    [Tooltip("Distancia frente a la cámara a la que flota el objeto al inspeccionarlo.")]
    public float distanciaInspeccionObjeto = 0.7f;
    [Tooltip("Sensibilidad de rotación del objeto con el mouse al inspeccionar.")]
    public float velocidadRotacionInspeccion = 4f;
    [Tooltip("Velocidad de zoom con la rueda al inspeccionar.")]
    public float velocidadZoomInspeccion = 0.5f;
    [Tooltip("Distancia mínima (más cerca) al acercar con la rueda.")]
    public float distanciaInspeccionMin = 0.35f;
    [Tooltip("Distancia máxima (más lejos) al alejar con la rueda.")]
    public float distanciaInspeccionMax = 1.5f;
    [Tooltip("Si acercás el objeto MÁS que esta distancia, se oculta el InfoObjetoUI para verlo sin el panel encima.")]
    public float distanciaOcultarInfo = 0.5f;

    private float distanciaInspeccionActual;
    [Tooltip("MouseLook a bloquear mientras se inspecciona (para que el mouse rote el objeto, no la cámara). Si se deja vacío se toma del ControladorMochila.")]
    public MouseLook mouseLook;

    private bool enModoInspeccion = false;
    private Quaternion rotacionInspeccion = Quaternion.identity;

    public InventoryItem ItemEnMano => enMano;
    private LectorNotas lectorNotas;

    [Header("Consumo de Objetos")]
    [Tooltip("Slider UI para mostrar el progreso de consumo.")]
    public UnityEngine.UI.Slider sliderConsumo;
    [Tooltip("Tiempo necesario para consumir un objeto (en segundos).")]
    public float tiempoParaConsumir = 1.5f;

    private float tiempoConsumo = 0f;
    private PlayerData player;

    private void Awake()
    {
        if (camara == null) camara = Camera.main;
        if (tirolina == null) tirolina = GetComponent<SistemaTirolina>();
        player = GetComponent<PlayerData>();
        if (player == null) player = FindAnyObjectByType<PlayerData>();

        if (sliderConsumo != null)
        {
            sliderConsumo.gameObject.SetActive(false);
            sliderConsumo.value = 0f;
        }
    }

    private void Start()
    {
        lectorNotas = GetComponent<LectorNotas>();
        if (lectorNotas == null) lectorNotas = FindAnyObjectByType<LectorNotas>();
    }

    private void Update()
    {
        if (ModoMenu.Activo) return;

        // Mantener el click = sostener (como apretar la mano). Soltar el click = soltar.
        bool apuntandoTirolina = tirolina != null && tirolina.EnModoApuntado;
        if (Input.GetMouseButtonDown(0) && enMano == null && !apuntandoTirolina && (lectorNotas == null || !lectorNotas.SostieneNota))
            IntentarAgarrar();

        if (Input.GetMouseButtonUp(0) && enMano != null)
        {
            if (estaApuntandoAnclaje)
            {
                CancelarPreview();
            }

            if (enModo2D && snapValido)
            {
                if (enMano.data is ModuloData) PegarModulo();
                else PegarItem();
            }
            else Soltar();
        }

        // Click derecho con la mochila abierta y un objeto sostenido: rotar 90Â°
        if (Input.GetMouseButtonDown(1) && enMano != null && mochila != null && mochila.Abierta)
            enMano.Rotate();

        // LÃ³gica de vista previa de colocaciÃ³n de anclaje (Click izquierdo sostenido + Click derecho sostenido)
        PilarRapel pilar = enMano != null ? enMano.GetComponent<PilarRapel>() : null;
        if (pilar != null && !enModo2D)
        {
            if (Input.GetMouseButton(1))
            {
                if (!estaApuntandoAnclaje)
                {
                    estaApuntandoAnclaje = true;
                    enMano.transform.SetParent(null);
                    enMano.transform.localScale = enMano.escalaMundialOriginal;
                    SetCollidersTrigger(enMano.gameObject, true);
                    pilar.SetParticulasActivas(true);
                }
                ActualizarPreviewAnclaje();
            }
            else if (estaApuntandoAnclaje && Input.GetMouseButtonUp(1))
            {
                if (previewValido)
                {
                    ColocarAnclaje(pilar);
                }
                else
                {
                    CancelarPreview();
                }
            }
            else if (estaApuntandoAnclaje && !Input.GetMouseButton(1))
            {
                CancelarPreview();
            }
        }

        // --- MODO INSPECCIÓN / ANÁLISIS ---
        // Click Der mantenido con un objeto NORMAL en mano (mochila cerrada, no anclaje, no modo 2D):
        // el objeto flota en el centro de la mira y el mouse lo rota. Inspeccionarlo lo DESCUBRE.
        // Soltar Click Der → vuelve a la mano. Soltar Click Izq → se cae (lo maneja el bloque de arriba vía Soltar).
        bool esAnclajeMano = pilar != null;
        bool mochilaCerradaInsp = mochila == null || !mochila.Abierta;
        // La tablet abierta se queda con el Click Derecho para su propio cursor (ver TabletaMouse).
        if (enMano != null && !esAnclajeMano && !enModo2D && mochilaCerradaInsp && !ModoTablet.UsandoMouse)
        {
            if (!enModoInspeccion && Input.GetMouseButtonDown(1))
                EntrarInspeccion();
            else if (enModoInspeccion && !Input.GetMouseButton(1))
                SalirInspeccion();

            if (enModoInspeccion)
                ActualizarInspeccion();
        }
        else if (enModoInspeccion)
        {
            // Cambió el contexto (mochila abierta, se soltó el objeto, etc.) → cerrar prolijo
            SalirInspeccion();
        }

        // Mostrar informaciÃ³n del objeto en la mira cuando no tenemos nada en la mano, no sostenemos nota y la mochila estÃ¡ cerrada
        bool mochilaCerrada = mochila == null || !mochila.Abierta;
        if (enMano == null && (lectorNotas == null || !lectorNotas.SostieneNota) && mochilaCerrada && camara != null)
        {
            Ray ray = camara.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, alcance, capaAgarrable))
            {
                InventoryItem item = hit.collider.GetComponentInParent<InventoryItem>();
                if (item != null && item.data != null)
                {
                    // No mostrar info si es un pilar de rÃ¡pel con la cuerda ya desplegada (lo maneja SistemaRapel)
                    PilarRapel pilarComponent = item.GetComponent<PilarRapel>();
                    bool esAnclajeColocado = pilarComponent != null && pilarComponent.GetComponent<LineRenderer>() != null && pilarComponent.GetComponent<LineRenderer>().enabled;

                    if (!esAnclajeColocado)
                    {
                        // Codex: si nunca tomaste este tipo de objeto, aparece como "Desconocido".
                        if (RegistroDescubrimientos.EstaDescubierto(item.data))
                            InfoObjetoUI.Instance?.Mostrar(item.data.DescripcionTooltip(), item.transform);
                        else
                            InfoObjetoUI.Instance?.Mostrar("Desconocido\n???", item.transform);
                    }
                }
                else
                {
                    // Ver si es una nota en el mundo
                    NotaMundo nota = hit.collider.GetComponentInParent<NotaMundo>();
                    if (nota != null && nota.data != null)
                    {
                        InfoObjetoUI.Instance?.Mostrar($"{nota.data.titulo}\nAutor: {nota.data.autor}", nota.transform);
                    }
                }
            }
        }

        if (enMano != null)
        {
            bool mochilaAbierta = mochila != null && mochila.Abierta;

            // Cambios de modo entre "en mano" (cerrada) e "inspecciÃ³n 2D" (abierta)
            if (mochilaAbierta && !enModo2D)
            {
                if (estaApuntandoAnclaje) CancelarPreview();
                EntrarModo2D();
            }
            else if (!mochilaAbierta && enModo2D) SalirModo2D();

            if (enModo2D) ActualizarModo2D();

            // Trackear velocidad del objeto sostenido (para inercia al soltar)
            if (!estaApuntandoAnclaje)
            {
                velocidad = (enMano.transform.position - ultimaPos) / Mathf.Max(Time.deltaTime, 0.0001f);
                ultimaPos = enMano.transform.position;
            }
        }

        if (enMano != null && !enModo2D && mochilaCerrada && enMano.data is ConsumibleData consumible)
        {
            if (Input.GetKey(KeyCode.E))
            {
                if (sliderConsumo != null && !sliderConsumo.gameObject.activeSelf)
                {
                    sliderConsumo.gameObject.SetActive(true);
                }

                tiempoConsumo += Time.deltaTime;
                if (sliderConsumo != null)
                {
                    sliderConsumo.value = tiempoConsumo / tiempoParaConsumir;
                }

                if (tiempoConsumo >= tiempoParaConsumir)
                {
                    // Consumir el objeto
                    if (player != null)
                    {
                        consumible.Usar(player);
                    }
                    else
                    {
                        player = FindAnyObjectByType<PlayerData>();
                        if (player != null) consumible.Usar(player);
                    }

                    // Destruir el objeto
                    Destroy(enMano.gameObject);
                    enMano = null;

                    // Resetear el slider
                    tiempoConsumo = 0f;
                    if (sliderConsumo != null)
                    {
                        sliderConsumo.value = 0f;
                        sliderConsumo.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                // Si suelta la tecla E, se resetea la barra
                tiempoConsumo = 0f;
                if (sliderConsumo != null && sliderConsumo.gameObject.activeSelf)
                {
                    sliderConsumo.value = 0f;
                    sliderConsumo.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // Si no tiene un consumible en la mano o abre la mochila, se resetea la barra y se oculta
            tiempoConsumo = 0f;
            if (sliderConsumo != null && sliderConsumo.gameObject.activeSelf)
            {
                sliderConsumo.value = 0f;
                sliderConsumo.gameObject.SetActive(false);
            }
        }
    }

    private void EntrarModo2D()
    {
        enModo2D = true;
        enMano.transform.SetParent(null); // se despega de la mano para flotar frente a la cÃ¡mara
        enMano.transform.localScale = enMano.escalaMundialOriginal;
    }

    private void SalirModo2D()
    {
        enModo2D = false;
        if (manoDerecha != null)
        {
            enMano.transform.SetParent(manoDerecha, worldPositionStays: false);
            enMano.transform.localPosition = Vector3.zero;
            enMano.transform.localRotation = Quaternion.identity;
            FijarEscalaMundial(enMano.transform, enMano.escalaMundialOriginal);
        }
    }

    private void ActualizarModo2D()
    {
        if (camara == null) return;

        // Rueda del ratÃ³n: acercar/alejar el objeto
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
            distanciaInspeccion = Mathf.Clamp(distanciaInspeccion + scroll * velocidadRueda, distanciaMin, distanciaMax);

        // PosiciÃ³n libre en plano 2D siguiendo el cursor
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = distanciaInspeccion;
        Vector3 destino = camara.ScreenToWorldPoint(mousePos);

        // Si lo que sostengo es un MÃ“DULO, intento hacer snap a la grilla de la mochila
        snapValido = false;
        if (enMano.data is ModuloData modulo && CachearMochila())
        {
            Transform tGrid = gridMochila.transform; // espacio donde se dibujan las celdas
            float tam = visualMochila.tamanoCelda;

            // Forma rotada segÃºn la rotaciÃ³n actual del mÃ³dulo
            var formaRot = RotarForma(modulo.forma, enMano.rotacion);

            // Convertir la posiciÃ³n del cursor a celda local del Grid
            Vector3 local = tGrid.InverseTransformPoint(destino);
            Vector2Int origen = visualMochila.LocalACelda(local);

            if (gridMochila.PuedeColocarModulo(formaRot, origen))
            {
                snapValido = true;
                snapOrigen = origen;

                // Snap visual: centrar el mesh sobre el conjunto de celdas que ocupa (compensando el offset del pivote)
                enMano.transform.rotation = tGrid.rotation * Quaternion.Euler(0f, 0f, 90f * enMano.rotacion);
                Vector3 offsetLocal = ObtenerOffsetCentroMesh(enMano);
                Vector3 centroLocal = visualMochila.CentroFormaLocal(formaRot, origen)
                                    + OffsetVisualLocal(enMano, tam)
                                    - enMano.transform.localRotation * offsetLocal;
                enMano.transform.position = tGrid.TransformPoint(centroLocal);
                return;
            }
        }
        // Si es un ITEM normal (no mÃ³dulo), intento guardarlo ocupando celdas existentes
        else if (CachearMochila())
        {
            Transform tGrid = gridMochila.transform;
            float tam = visualMochila.tamanoCelda;
            int w = enMano.Width, h = enMano.Height;

            Vector3 local = tGrid.InverseTransformPoint(destino);
            Vector2Int origen = visualMochila.LocalACelda(local);

            if (gridMochila.PuedeColocarItem(origen, w, h, enMano))
            {
                snapValido = true;
                snapOrigen = origen;

                enMano.transform.rotation = tGrid.rotation * Quaternion.Euler(0f, 0f, 90f * enMano.rotacion);
                Vector3 offsetLocal = ObtenerOffsetCentroMesh(enMano);
                Vector3 centroLocal = visualMochila.CentroRectLocal(origen, w, h)
                                    + OffsetVisualLocal(enMano, tam)
                                    - enMano.transform.localRotation * offsetLocal;
                enMano.transform.position = tGrid.TransformPoint(centroLocal);
                return;
            }
        }

        // Sin snap: flota libre frente a la cÃ¡mara (rota segÃºn rotacion)
        enMano.transform.position = destino;
        enMano.transform.rotation = camara.transform.rotation * Quaternion.Euler(0f, 0f, 90f * enMano.rotacion);
    }

    /// <summary>Rota una forma 90Â° en sentido antihorario, `pasos` veces.</summary>
    private System.Collections.Generic.List<Vector2Int> RotarForma(
        System.Collections.Generic.IList<Vector2Int> forma, int pasos)
    {
        var resultado = new System.Collections.Generic.List<Vector2Int>(forma.Count);
        foreach (var c in forma)
        {
            Vector2Int r = c;
            for (int i = 0; i < (pasos % 4 + 4) % 4; i++)
                r = new Vector2Int(-r.y, r.x); // 90Â° CCW
            resultado.Add(r);
        }
        return resultado;
    }

    private Vector3 ObtenerOffsetCentroMesh(InventoryItem item)
    {
        if (item == null) return Vector3.zero;
        Renderer r = item.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            Vector3 worldCenter = r.bounds.center;
            return item.transform.InverseTransformPoint(worldCenter);
        }
        return Vector3.zero;
    }

    private Vector3 OffsetVisualLocal(InventoryItem item, float tamanoCelda)
    {
        if (item == null) return Vector3.zero;
        return new Vector3(item.offsetVisualCeldas.x * tamanoCelda, item.offsetVisualCeldas.y * tamanoCelda, 0f);
    }

    /// <summary>
    /// Fija una escala UNIFORME en el mundo, contrarrestando la escala del padre con un solo factor.
    /// Necesario al parentar a un objeto con escala no uniforme (la mochila aplastada): una escala
    /// uniforme + rotaciÃ³n no produce deformaciÃ³n (shear), a diferencia de la compensaciÃ³n por ejes.
    /// </summary>
    private void FijarEscalaUniformeEnPadre(Transform t, float mundialUniforme)
    {
        // Leemos la escala del PADRE directamente. No usar t.lossyScale: cuando el objeto
        // estÃ¡ rotado respecto a un padre de escala no uniforme, Unity lo aproxima mal
        // y el resultado varÃ­a segÃºn la rotaciÃ³n (mÃ³dulo bien en vertical, agrandado en horizontal).
        float padre = t.parent != null ? t.parent.lossyScale.x : 1f;
        float f = Mathf.Approximately(padre, 0f) ? mundialUniforme : mundialUniforme / padre;
        t.localScale = new Vector3(f, f, f);
    }

    /// <summary>
    /// Compensa por eje la escala del PADRE para conservar la proporciÃ³n real del objeto
    /// (sin achatarlo en Z como la escala uniforme). Como el grid es cuadrado en X/Y,
    /// rotar alrededor de Z no produce deformaciÃ³n.
    /// </summary>
    private void FijarEscalaMundialEnPadre(Transform t, Vector3 mundialDeseada)
    {
        Vector3 padre = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(
            Mathf.Approximately(padre.x, 0f) ? mundialDeseada.x : mundialDeseada.x / padre.x,
            Mathf.Approximately(padre.y, 0f) ? mundialDeseada.y : mundialDeseada.y / padre.y,
            Mathf.Approximately(padre.z, 0f) ? mundialDeseada.z : mundialDeseada.z / padre.z);
    }

    /// <summary>Ajusta el localScale para que el lossyScale (tamaÃ±o visual en mundo) sea el deseado.</summary>
    private void FijarEscalaMundial(Transform t, Vector3 mundialDeseada)
    {
        t.localScale = Vector3.one;
        Vector3 padre = t.lossyScale; // con localScale=1, esto es la escala del padre
        t.localScale = new Vector3(
            Mathf.Approximately(padre.x, 0f) ? mundialDeseada.x : mundialDeseada.x / padre.x,
            Mathf.Approximately(padre.y, 0f) ? mundialDeseada.y : mundialDeseada.y / padre.y,
            Mathf.Approximately(padre.z, 0f) ? mundialDeseada.z : mundialDeseada.z / padre.z);
    }

    private bool CachearMochila()
    {
        if (mochila == null || mochila.mochila == null) return false;
        // Los componentes pueden estar en un hijo (ej. objeto "Grid"), asÃ­ que buscamos en hijos.
        if (gridMochila == null) gridMochila = mochila.mochila.GetComponentInChildren<MochilaGrid>();
        if (visualMochila == null) visualMochila = mochila.mochila.GetComponentInChildren<MochilaVisual>();
        return gridMochila != null && visualMochila != null;
    }

    private void PegarModulo()
    {
        ModuloData modulo = enMano.data as ModuloData;
        if (modulo == null || !CachearMochila()) return;

        var formaRot = RotarForma(modulo.forma, enMano.rotacion);

        if (gridMochila.ColocarModulo(formaRot, snapOrigen))
        {
            // Recordar dÃ³nde y cÃ³mo se pegÃ³ (para poder despegarlo luego con la misma forma rotada)
            enMano.gridX = snapOrigen.x;
            enMano.gridY = snapOrigen.y;

            // El mÃ³dulo pasa a ser parte de la mochila: se pega alineado y sin fÃ­sica.
            Rigidbody rb = enMano.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Transform tGrid = gridMochila.transform;
            float tam = visualMochila.tamanoCelda;
            enMano.transform.SetParent(tGrid, worldPositionStays: false);
            FijarEscalaUniformeEnPadre(enMano.transform, enMano.escalaMundialOriginal.x);
            enMano.transform.rotation = tGrid.rotation * Quaternion.Euler(0f, 0f, 90f * enMano.rotacion);
            Vector3 offsetLocal = ObtenerOffsetCentroMesh(enMano);
            Vector3 centroLocal = visualMochila.CentroFormaLocal(formaRot, snapOrigen)
                                + OffsetVisualLocal(enMano, tam)
                                - enMano.transform.localRotation * offsetLocal;
            enMano.transform.position = tGrid.TransformPoint(centroLocal);

        }

        enMano = null;
        enModo2D = false;
        snapValido = false;
    }

    private void PegarItem()
    {
        if (!CachearMochila()) return;

        int w = enMano.Width, h = enMano.Height;
        if (gridMochila.ColocarItem(enMano, snapOrigen, w, h))
        {
            enMano.gridX = snapOrigen.x;
            enMano.gridY = snapOrigen.y;

            Rigidbody rb = enMano.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Transform tGrid = gridMochila.transform;
            float tam = visualMochila.tamanoCelda;
            enMano.transform.SetParent(tGrid, worldPositionStays: false);
            FijarEscalaMundialEnPadre(enMano.transform, enMano.escalaMundialOriginal); // proporciÃ³n real, sin achatar Z
            enMano.transform.rotation = tGrid.rotation * Quaternion.Euler(0f, 0f, 90f * enMano.rotacion);
            Vector3 offsetLocal = ObtenerOffsetCentroMesh(enMano);
            Vector3 centroLocal = visualMochila.CentroRectLocal(snapOrigen, w, h)
                                + OffsetVisualLocal(enMano, tam)
                                - enMano.transform.localRotation * offsetLocal;
            enMano.transform.position = tGrid.TransformPoint(centroLocal);

        }

        enMano = null;
        enModo2D = false;
        snapValido = false;
    }

    private void IntentarAgarrar()
    {
        if (camara == null) return;

        // Con la mochila abierta apuntamos con el cursor; cerrada, con el centro de la pantalla.
        bool abierta = mochila != null && mochila.Abierta;
        Vector3 screenPoint = abierta
            ? Input.mousePosition
            : new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

        Ray ray = camara.ScreenPointToRay(screenPoint);
        if (Physics.Raycast(ray, out RaycastHit hit, alcance, capaAgarrable))
        {
            InventoryItem item = hit.collider.GetComponentInParent<InventoryItem>();
            if (item == null) return;

            // Si estaba guardado en la grilla, liberar lo que ocupaba.
            if (CachearMochila() && item.transform.IsChildOf(gridMochila.transform))
            {
                if (item.data is ModuloData mod)
                {
                    var formaRot = RotarForma(mod.forma, item.rotacion);
                    gridMochila.QuitarModulo(formaRot, new Vector2Int(item.gridX, item.gridY));
                }
                else
                {
                    gridMochila.QuitarItem(item); // libera las celdas que ocupaba
                }
            }

            enMano = item;

            // Si es un pilar de rÃ¡pel, ocultamos la cuerda al agarrarlo
            PilarRapel pilarItem = item.GetComponent<PilarRapel>();
            if (pilarItem != null)
            {
                pilarItem.MostrarCuerda(false);
                pilarItem.SetParticulasActivas(false);
            }

            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            if (manoDerecha != null)
            {
                item.transform.SetParent(manoDerecha, worldPositionStays: false);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
                FijarEscalaMundial(item.transform, item.escalaMundialOriginal);
            }

            ultimaPos = item.transform.position;
            velocidad = Vector3.zero;
        }
    }

    private void Soltar()
    {
        if (enMano == null) return;

        if (enModoInspeccion) LimpiarInspeccion(); // restaura colliders/cámara antes de tirarlo

        enMano.transform.SetParent(null);
        enMano.transform.localScale = enMano.escalaMundialOriginal; // sin padre, local == mundial

        Rigidbody rb = enMano.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = velocidad * multiplicadorInercia;
        }

        enMano = null;
        enModo2D = false;
    }

    // --- INSPECCIÓN / ANÁLISIS ---

    private void EntrarInspeccion()
    {
        if (enMano == null || camara == null) return;
        enModoInspeccion = true;

        enMano.transform.SetParent(null);
        enMano.transform.localScale = enMano.escalaMundialOriginal;
        SetCollidersTrigger(enMano.gameObject, true); // no colisiona mientras flota

        rotacionInspeccion = Quaternion.LookRotation(camara.transform.forward, Vector3.up);
        distanciaInspeccionActual = Mathf.Clamp(distanciaInspeccionObjeto, distanciaInspeccionMin, distanciaInspeccionMax);

        // Bloquear la cámara: el mouse debe rotar el objeto, no la vista
        MouseLook ml = ObtenerMouseLook();
        if (ml != null) ml.canLook = false;

        // Inspeccionar = analizar = DESCUBRIR (registra en el codex; si es nuevo dispara el evento)
        if (enMano.data != null) RegistroDescubrimientos.Descubrir(enMano.data);
    }

    private void ActualizarInspeccion()
    {
        if (enMano == null || camara == null) return;

        // Rueda: acercar/alejar el objeto (con clamp)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
            distanciaInspeccionActual = Mathf.Clamp(distanciaInspeccionActual + scroll * velocidadZoomInspeccion,
                                                    distanciaInspeccionMin, distanciaInspeccionMax);

        // Flotar en el centro de la mira
        enMano.transform.position = camara.transform.position + camara.transform.forward * distanciaInspeccionActual;

        // Rotar con el mouse alrededor de los ejes de la cámara (intuitivo). Mouse X/Y ya son delta por frame.
        float mx = Input.GetAxis("Mouse X") * velocidadRotacionInspeccion;
        float my = Input.GetAxis("Mouse Y") * velocidadRotacionInspeccion;
        rotacionInspeccion = Quaternion.AngleAxis(-mx, camara.transform.up)
                           * Quaternion.AngleAxis(my, camara.transform.right)
                           * rotacionInspeccion;
        enMano.transform.rotation = rotacionInspeccion;

        // Mostrar los datos (ya descubierto) mientras se analiza — salvo que lo tengas MUY cerca
        // (distancia < distanciaOcultarInfo): ahí se oculta para ver el objeto sin el panel encima.
        if (enMano.data != null && distanciaInspeccionActual >= distanciaOcultarInfo)
            InfoObjetoUI.Instance?.Mostrar(enMano.data.DescripcionTooltip(), enMano.transform);
        else
            InfoObjetoUI.Instance?.Ocultar();
    }

    private void SalirInspeccion()
    {
        if (!enModoInspeccion) return;
        LimpiarInspeccion();

        // Volver a la mano
        if (enMano != null && manoDerecha != null)
        {
            enMano.transform.SetParent(manoDerecha, worldPositionStays: false);
            enMano.transform.localPosition = Vector3.zero;
            enMano.transform.localRotation = Quaternion.identity;
            FijarEscalaMundial(enMano.transform, enMano.escalaMundialOriginal);
        }
    }

    /// <summary>Restaura colliders y cámara del estado de inspección (sin reparentar).</summary>
    private void LimpiarInspeccion()
    {
        enModoInspeccion = false;
        if (enMano != null) SetCollidersTrigger(enMano.gameObject, false);
        MouseLook ml = ObtenerMouseLook();
        if (ml != null) ml.canLook = true;
    }

    private MouseLook ObtenerMouseLook()
    {
        if (mouseLook != null) return mouseLook;
        if (mochila != null && mochila.mouseLook != null) return mochila.mouseLook;
        return null;
    }

    private void SetCollidersTrigger(GameObject obj, bool isTrigger)
    {
        if (obj == null) return;
        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (col.gameObject.name == "ColliderCuerdaFisica") continue;
            col.isTrigger = isTrigger;
        }
    }

    private void ActualizarPreviewAnclaje()
    {
        if (enMano == null || camara == null) return;

        // Mantener el objeto flotando enfrente del usuario, a una distancia fija de la cÃ¡mara (sin snapping)
        Vector3 posicionPreview = camara.transform.position + camara.transform.forward * 1.5f;
        enMano.transform.position = posicionPreview;
        enMano.transform.rotation = Quaternion.Euler(0f, camara.transform.eulerAngles.y + 180f, 0f);

        // El preview en el aire siempre es vÃ¡lido para visualizar
        previewValido = true;
    }

    private void ColocarAnclaje(PilarRapel pilar)
    {
        if (enMano == null || camara == null) return;

        Vector3 posicionPreview = enMano.transform.position;

        // Lanzar un raycast hacia abajo desde la posiciÃ³n del preview flotante
        // Se aÃ±ade un offset de 1 metro hacia arriba para asegurar que empiece arriba del suelo en caso de clipping
        Vector3 origenRaycast = posicionPreview + Vector3.up * 1.0f;
        Ray rayAbajo = new Ray(origenRaycast, Vector3.down);

        // Raycast de 11 metros (10 metros hacia abajo + 1 metro de offset hacia arriba)
        if (Physics.Raycast(rayAbajo, out RaycastHit hitAbajo, 11f, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 puntoColocacion = hitAbajo.point;

            // Colocar fÃ­sicamente en el punto de impacto (suelo justo debajo del anclaje)
            enMano.transform.SetParent(null);
            enMano.transform.position = puntoColocacion;
            enMano.transform.rotation = Quaternion.Euler(0f, camara.transform.eulerAngles.y + 180f, 0f);
            enMano.transform.localScale = enMano.escalaMundialOriginal;

            // Restaurar colisionadores a sÃ³lidos
            SetCollidersTrigger(enMano.gameObject, false);

            // Ajustar fÃ­sicas para que quede clavado (kinematic)
            Rigidbody rb = enMano.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Desplegar la cuerda fÃ­sicamente y visualmente
            pilar.DesplegarCuerda();
            pilar.SetParticulasActivas(false);


            // Liberar de la mano
            enMano = null;
            estaApuntandoAnclaje = false;
            previewValido = false;
        }
        else
        {
            // Si no hay suelo debajo, cancelamos la colocaciÃ³n y vuelve a la mano
            Debug.LogWarning("[ManoJugador] No se detectÃ³ suelo debajo del preview del anclaje. Cancelando colocaciÃ³n.");
            CancelarPreview();
        }
    }

    private void CancelarPreview()
    {
        if (enMano == null || !estaApuntandoAnclaje) return;

        // Desactivar partÃ­culas
        PilarRapel pilarItem = enMano.GetComponent<PilarRapel>();
        if (pilarItem != null) pilarItem.SetParticulasActivas(false);

        // Restaurar colisionadores a sÃ³lidos
        SetCollidersTrigger(enMano.gameObject, false);

        // Reubicar de regreso en manoDerecha
        if (manoDerecha != null)
        {
            enMano.transform.SetParent(manoDerecha, worldPositionStays: false);
            enMano.transform.localPosition = Vector3.zero;
            enMano.transform.localRotation = Quaternion.identity;
            FijarEscalaMundial(enMano.transform, enMano.escalaMundialOriginal);
        }

        estaApuntandoAnclaje = false;
        previewValido = false;
    }
}


