using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

/// <summary>
/// Bitácora / codex: ventana aparte que lista los objetos descubiertos (tomados al menos una vez),
/// separados por PESTAÑAS según su CategoriaItem (asignada a mano en cada ItemData). Se abre/cierra
/// desde la TABLET (tecla 1) — no tiene atajo propio; `Alternar()` es público para que la tablet o un botón la abran.
///
/// Setup: poné este script en un GameObject SIEMPRE ACTIVO (ej. el Canvas), y arrastrá al campo
/// `ventana` el panel que se muestra/oculta. Armá un botón por pestaña (una por CategoriaItem que
/// quieras separar, más opcionalmente una "Todos") y completá el array `pestanas` en el inspector.
/// El script no puede ir en el panel que apaga, porque un objeto inactivo no corre Update y no
/// podría volver a abrirse.
/// </summary>
public class BitacoraUI : MonoBehaviour
{
    [System.Serializable]
    public class Pestana
    {
        [Tooltip("Solo para identificarla en el inspector.")]
        public string nombre = "Pestaña";
        [Tooltip("Botón que selecciona esta pestaña.")]
        public Button boton;
        [Tooltip("Si está activo, esta pestaña muestra TODAS las categorías juntas (ignora 'Categoria').")]
        public bool esTodos = false;
        [Tooltip("Categoría que filtra esta pestaña (ignorado si 'Es Todos' o 'Es Lugares' están activos).")]
        public CategoriaItem categoria = CategoriaItem.Chatarra;
        [Tooltip("Si está activo, esta pestaña muestra las VIVIENDAS visitadas en vez de items (ignora 'Categoria' y 'Es Todos').")]
        public bool esLugares = false;
        [Tooltip("Si está activo, esta pestaña muestra el estado de las MISIONES de conexión (el mismo texto del HUD: [SIN ESCUCHAR] / [EN CURSO] / [COMPLETADA] con el nombre del habitante y a dónde hay que ir). Manda sobre 'Es Lugares', 'Es Todos' y 'Categoria'.")]
        public bool esMisiones = false;
    }

    [Header("Ventana")]
    [Tooltip("Panel que se muestra/oculta desde la tablet. Debe ser un objeto distinto a este (este queda siempre activo).")]
    public GameObject ventana;
    [Tooltip("Liberar el cursor mientras la bitácora está abierta.")]
    public bool liberarCursor = true;
    [Tooltip("MouseLook a bloquear mientras está abierta (opcional).")]
    public MouseLook mouseLook;

    [Header("Pestañas")]
    [Tooltip("Una entrada por pestaña. El orden acá define el orden visual (vos lo ordenás en la Hierarchy).")]
    public Pestana[] pestanas;
    [Tooltip("Color del botón de la pestaña ACTIVA.")]
    public Color colorPestanaActiva = Color.white;
    [Tooltip("Color de los botones de pestañas inactivas.")]
    public Color colorPestanaInactiva = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Header("Contenido")]
    [Tooltip("TMP_Text donde se listan los objetos descubiertos de la pestaña actual, uno por línea.")]
    public TMP_Text txtListado;
    [Tooltip("Mensaje cuando la pestaña actual no tiene descubrimientos.")]
    public string mensajeVacio = "Sin descubrimientos aún.";
    [Tooltip("Mostrar el tipo de material de cada objeto.")]
    public bool mostrarMaterial = true;
    [Tooltip("Mostrar el peso de cada objeto.")]
    public bool mostrarPeso = true;
    [Tooltip("TMP opcional para el contador global (ej. 'Descubiertos: 7'). Puede quedar vacío.")]
    public TMP_Text txtContador;

    public bool Abierta => ventana != null && ventana.activeSelf;

    private int pestanaActual = 0;

    void Awake()
    {
        // ⚠️ NO se apaga la ventana acá. La bitácora vive dentro de la tablet: lo que se prende y
        // se apaga es el canvas de la tablet (TogglePantalla, tecla 1), no esta ventana. Si la
        // apagáramos en Awake se quedaría apagada para siempre, porque nadie llama a Alternar().
        if (pestanas != null)
        {
            for (int i = 0; i < pestanas.Length; i++)
            {
                int idx = i; // captura por valor para el listener
                if (pestanas[i].boton != null)
                    pestanas[i].boton.onClick.AddListener(() => SeleccionarPestana(idx));
            }
        }
    }

    void OnEnable()
    {
        RegistroDescubrimientos.OnNuevoDescubrimiento += AlDescubrir;
        RegistroViviendasVisitadas.OnNuevaVisita += AlVisitarVivienda;
    }

    void OnDisable()
    {
        RegistroDescubrimientos.OnNuevoDescubrimiento -= AlDescubrir;
        RegistroViviendasVisitadas.OnNuevaVisita -= AlVisitarVivienda;
    }

    // ⚠️ SIN Update ni tecla propia. La bitácora vive DENTRO de la tablet, así que se abre y se
    // cierra con la tablet (tecla 1, ver TogglePantalla). El atajo B abría la ventana suelta,
    // fuera de la tablet. `Alternar()` queda público para que la tablet o un botón la llamen.
    //
    // Nota: NO alcanzaba con poner `tecla` en None en el código — la escena ya tenía KeyCode.B
    // serializado, y Unity serializa por valor guardado, no por el default del script.

    /// <summary>
    /// LEGACY / opcional: prende y apaga la ventana por su cuenta. Ya no la llama nadie — la
    /// bitácora está siempre activa dentro de la tablet. Queda pública por si querés un botón
    /// que la colapse dentro de la tablet.
    /// </summary>
    public void Alternar()
    {
        if (ventana == null) return;
        bool abrir = !ventana.activeSelf;
        ventana.SetActive(abrir);

        if (abrir) { ActualizarColoresPestanas(); Pintar(); }

        if (liberarCursor)
        {
            Cursor.lockState = abrir ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = abrir;
        }
        if (mouseLook != null) mouseLook.canLook = !abrir;
    }

    public void SeleccionarPestana(int indice)
    {
        if (pestanas == null || indice < 0 || indice >= pestanas.Length) return;
        pestanaActual = indice;
        ActualizarColoresPestanas();
        Pintar();
    }

    private void ActualizarColoresPestanas()
    {
        if (pestanas == null) return;
        for (int i = 0; i < pestanas.Length; i++)
        {
            if (pestanas[i].boton == null) continue;
            Image img = pestanas[i].boton.image;
            if (img != null) img.color = (i == pestanaActual) ? colorPestanaActiva : colorPestanaInactiva;
        }
    }

    void Start()
    {
        // Siempre visible ⇒ tiene que arrancar con contenido, no en blanco hasta el primer
        // descubrimiento. Antes lo pintaba Alternar() al abrirse.
        ActualizarColoresPestanas();
        Pintar();
    }

    [Header("Refresco")]
    [Tooltip("Cada cuántos segundos se repinta mientras la ventana está a la vista.\n\nLos descubrimientos de items y lugares avisan por evento, pero el ESTADO DE LAS MISIONES no: aceptar una misión o conectar dos viviendas cambia DatosVivienda sin disparar nada, así que la pestaña Misiones necesita este refresco para no quedar vieja.")]
    public float intervaloRefresco = 1f;

    private float proximoRefresco;

    void Update()
    {
        // Solo mientras se está viendo de verdad: dentro de la tablet, con la tablet cerrada el
        // objeto está inactivo en jerarquía y no tiene sentido armar strings.
        if (ventana == null || !ventana.activeInHierarchy) return;

        if (Time.time < proximoRefresco) return;
        proximoRefresco = Time.time + Mathf.Max(0.1f, intervaloRefresco);
        Pintar();
    }

    // Repintan SIEMPRE, ya no solo "si está abierta": la ventana está siempre activa y lo que se
    // esconde es la tablet entera, así que el guard de antes hacía que llegaras a la tablet con el
    // listado viejo. Solo corre cuando hay un descubrimiento nuevo, no por frame.
    private void AlDescubrir(ItemData _) => Pintar();

    private void AlVisitarVivienda(Vector3Int _) => Pintar();

    private void Pintar()
    {
        Pestana pestana = (pestanas != null && pestanaActual >= 0 && pestanaActual < pestanas.Length)
            ? pestanas[pestanaActual] : null;

        if (pestana != null && pestana.esMisiones)
        {
            PintarMisiones();
            return;
        }

        if (pestana != null && pestana.esLugares)
        {
            PintarLugares();
            return;
        }

        if (txtContador != null)
            txtContador.text = $"Descubiertos: {RegistroDescubrimientos.Total}";

        if (txtListado == null) return;

        var lista = RegistroDescubrimientos.Bitacora;

        StringBuilder sb = new StringBuilder();
        int mostrados = 0;
        foreach (var data in lista)
        {
            if (data == null) continue;
            if (pestana != null && !pestana.esTodos && data.categoria != pestana.categoria) continue;

            sb.Append("• ").Append(data.nombreArticulo);
            if (mostrarMaterial && !string.IsNullOrEmpty(data.tipoMaterial))
                sb.Append("  [").Append(data.tipoMaterial).Append(']');
            if (mostrarPeso)
                sb.Append("  ").Append(data.peso.ToString("F1")).Append(" kg");
            sb.Append('\n');
            mostrados++;
        }

        txtListado.text = mostrados == 0 ? mensajeVacio : sb.ToString().TrimEnd();
    }

    [Header("Pestaña Misiones")]
    [Tooltip("Colores de estado (hex), igual que en HUDMisionViviendas.")]
    public string colorCompletada = "#7CFF7C";
    public string colorEnCurso = "#FFD86B";
    public string colorSinAceptar = "#9A9A9A";
    [Tooltip("Debug: muestra la celda destino aunque todavía no hayas escuchado al NPC.")]
    public bool mostrarDestinoSiempre = false;

    /// <summary>
    /// Estado de las misiones de conexión. Mismo formato que HUDMisionViviendas, con UNA diferencia
    /// importante: acá se listan SOLO las viviendas que ya visitaste. La bitácora es el registro de
    /// lo que descubriste — si mostrara todas las registradas te estaría chivando la existencia de
    /// viviendas donde nunca estuviste, que el generador registra apenas las genera la burbuja.
    /// </summary>
    private void PintarMisiones()
    {
        WorldGenerator wg = WorldGenerator.Instance;
        if (wg == null)
        {
            if (txtListado != null) txtListado.text = mensajeVacio;
            return;
        }

        StringBuilder sb = new StringBuilder();
        int total = 0;
        int conectadas = 0;

        foreach (DatosVivienda d in wg.ViviendasRegistradas)
        {
            if (!RegistroViviendasVisitadas.FueVisitada(d.macroChunk)) continue;

            total++;
            if (d.conectada) conectadas++;

            string nombre = (d.habitante != null && !string.IsNullOrWhiteSpace(d.habitante.nombre))
                ? d.habitante.nombre
                : "Habitante";

            Vector3Int celdaEsta = CeldaCentralDeMacro(d.macroChunk, wg.macroSize);

            string estado;
            string color;
            if (d.conectada)           { estado = "COMPLETADA";   color = colorCompletada; }
            else if (d.misionAceptada) { estado = "EN CURSO";      color = colorEnCurso; }
            else                       { estado = "SIN ESCUCHAR";  color = colorSinAceptar; }

            sb.AppendLine($"<color={color}>[{estado}]</color> {nombre} · celda {celdaEsta}");

            // El destino solo se revela una vez que escuchaste la misión.
            if (d.tieneObjetivo && !d.conectada && (d.misionAceptada || mostrarDestinoSiempre))
            {
                Vector3Int celdaDestino = CeldaCentralDeMacro(d.macroChunkBuscado, wg.macroSize);
                string dir = wg.DireccionCardinal(d.macroChunk, d.macroChunkBuscado);
                sb.AppendLine($"    → buscar al {dir} · celda {celdaDestino}");
            }
            else if (d.conectada)
            {
                Vector3Int celdaCon = CeldaCentralDeMacro(d.conectadaCon, wg.macroSize);
                sb.AppendLine($"    → conectada con celda {celdaCon}");
            }
        }

        if (txtContador != null)
            txtContador.text = $"CONEXIONES: {conectadas} / {total}";

        if (txtListado != null)
            txtListado.text = total == 0 ? mensajeVacio : sb.ToString().TrimEnd();
    }

    // Centro habitable (lx=2, ly=1, lz=2) del macro chunk — mismo criterio que usa
    // WorldGenerator en todos sus otros sistemas.
    private static Vector3Int CeldaCentralDeMacro(Vector3Int macro, int macroSize)
    {
        return new Vector3Int(macro.x * macroSize + 2, macro.y * macroSize + 1, macro.z * macroSize + 2);
    }

    private void PintarLugares()
    {
        if (txtContador != null)
            txtContador.text = $"Viviendas visitadas: {RegistroViviendasVisitadas.Total}";

        if (txtListado == null) return;

        WorldGenerator wg = WorldGenerator.Instance;
        int macroSize = wg != null ? wg.macroSize : 5;

        StringBuilder sb = new StringBuilder();
        int mostrados = 0;
        foreach (Vector3Int macro in RegistroViviendasVisitadas.Bitacora)
        {
            Vector3Int celdaCentral = CeldaCentralDeMacro(macro, macroSize);

            string estado = "";
            if (wg != null && wg.TryObtenerDatosVivienda(macro, out DatosVivienda datos))
            {
                estado = datos.conectada ? "  [CONECTADA]" : (datos.misionAceptada ? "  [misión aceptada]" : "");
            }

            sb.Append("• Vivienda en celda ").Append(celdaCentral).Append(estado).Append('\n');
            mostrados++;
        }

        txtListado.text = mostrados == 0 ? mensajeVacio : sb.ToString().TrimEnd();
    }
}
