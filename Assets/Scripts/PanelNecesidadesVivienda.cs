using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Los widgets de UN recurso de la vivienda: icono + tiempo. Los dos opcionales.</summary>
[System.Serializable]
public class InfoStatVivienda
{
    [Tooltip("Solo para reconocerlo en el inspector.")]
    public string nombre = "Agua";
    [Tooltip("Icono del recurso. Se tiñe de rojo cuando está bajo el umbral crítico.")]
    public Image icono;
    [Tooltip("Tiempo restante (12:30). También se tiñe cuando está crítico.")]
    public TMP_Text texto;
}

/// <summary>
/// Panel de la vivienda (World Canvas): título + reservas de agua / comida / energía.
/// Solo LEE — no consume ni modifica nada.
///
/// Mismo patrón que PantallaInventarioVivienda: deduce SU PROPIA vivienda por posición, así que
/// funciona embebido en el prefab de la sala sin que nadie le inyecte nada al instanciarlo. Los
/// dos componentes conviven en el mismo panel sin pisarse: este muestra los números, el otro
/// lista los objetos que hay parados en la celda.
///
/// TODOS los campos de UI son opcionales. La maquetación la hacés vos; esto solo llena valores.
/// </summary>
public class PanelNecesidadesVivienda : MonoBehaviour
{
    [Header("Título")]
    [Tooltip("Título del panel. Opcional.")]
    public TMP_Text textoTitulo;
    [Tooltip("Texto del título. Acepta dos comodines:\n\n{NOMBRE} → nombre del habitante de esta vivienda\n{COORD} → coordenada del macro chunk (ej. 3,1,-2)\n\nEj: 'VIVIENDA {COORD} — {NOMBRE}'")]
    public string formatoTitulo = "VIVIENDA {COORD}";

    [Header("Stats por recurso")]
    public InfoStatVivienda infoAgua = new InfoStatVivienda { nombre = "Agua" };
    public InfoStatVivienda infoComida = new InfoStatVivienda { nombre = "Comida" };
    public InfoStatVivienda infoEnergia = new InfoStatVivienda { nombre = "Energía" };

    [Header("Textos sueltos (opcional)")]
    [Tooltip("Un solo texto con las tres reservas, una por línea. Alternativa rápida a los widgets de arriba.")]
    public TMP_Text textoResumen;
    [Tooltip("Nombra el recurso MÁS BAJO (ej. 'FALTA: AGUA'). Es lo que el NPC va a pedir cuando exista esa mecánica.")]
    public TMP_Text textoCarencia;

    [Header("Formato")]
    [Tooltip("ON = muestra 12:30 (min:seg). OFF = segundos pelados (1750s), útil para debug.")]
    public bool mostrarComoTiempo = true;
    [Tooltip("Debajo de estos segundos el recurso se pinta con Color Critico y cuenta como carencia grave.")]
    public float umbralCritico = 300f;
    public Color colorNormal = Color.white;
    public Color colorCritico = new Color(1f, 0.3f, 0.25f);
    [Tooltip("Título cuando el panel NO está dentro de una vivienda (mal colocado, o la sala no es tipo 1).")]
    public string mensajeSinVivienda = "SIN SEÑAL";

    [Header("Refresco")]
    [Tooltip("Cada cuánto se relee (segundos). No hace falta que sea cada frame: las reservas no drenan solas.")]
    public float intervaloRefresco = 1f;

    private float proximoRefresco;

    void OnEnable()
    {
        // Refresco inmediato al aparecer: la vivienda entra en la burbuja y el panel tiene que
        // mostrar algo YA, no esperar hasta un segundo.
        proximoRefresco = 0f;
    }

    void Update()
    {
        if (Time.time < proximoRefresco) return;
        proximoRefresco = Time.time + Mathf.Max(0.05f, intervaloRefresco);
        Refrescar();
    }

    private void Refrescar()
    {
        RegistroViviendas registro = RegistroViviendas.Instance;
        if (registro == null || !registro.TryObtenerStats(transform.position, out StatsVivienda stats))
        {
            MostrarSinVivienda();
            return;
        }

        PintarTitulo();

        PintarStat(infoAgua, stats.agua);
        PintarStat(infoComida, stats.comida);
        PintarStat(infoEnergia, stats.energia);

        if (textoResumen != null)
        {
            textoResumen.text =
                $"AGUA    {Formatear(stats.agua)}\n" +
                $"COMIDA  {Formatear(stats.comida)}\n" +
                $"ENERGÍA {Formatear(stats.energia)}";
        }

        if (textoCarencia != null)
        {
            RecursoVivienda peor = RecursoMasBajo(stats, out float valorPeor);
            bool grave = valorPeor <= umbralCritico;
            textoCarencia.text = grave ? $"FALTA: {Nombre(peor)}" : "SIN URGENCIAS";
            textoCarencia.color = grave ? colorCritico : colorNormal;
        }
    }

    private void PintarTitulo()
    {
        if (textoTitulo == null) return;

        WorldGenerator wg = WorldGenerator.Instance;
        if (wg == null) { textoTitulo.text = formatoTitulo; return; }

        Vector3Int macro = wg.MundoAMacroChunk(transform.position);

        // El nombre sale de DatosVivienda, que solo existe DESPUÉS de que la vivienda se registra.
        // Antes de eso se muestra vacío en vez de un placeholder feo: el título se completa solo
        // en el próximo refresco.
        string nombre = "";
        if (wg.TryObtenerDatosVivienda(macro, out DatosVivienda datos) && datos.habitante != null)
            nombre = datos.habitante.nombre;

        textoTitulo.text = formatoTitulo
            .Replace("{COORD}", $"{macro.x},{macro.y},{macro.z}")
            .Replace("{NOMBRE}", nombre);
        textoTitulo.color = colorNormal;
    }

    private void PintarStat(InfoStatVivienda info, float segundos)
    {
        if (info == null) return;

        Color color = segundos <= umbralCritico ? colorCritico : colorNormal;

        if (info.icono != null) info.icono.color = color;

        if (info.texto != null)
        {
            info.texto.text = Formatear(segundos);
            info.texto.color = color;
        }
    }

    /// <summary>
    /// El recurso con menos segundos. Es lo que el NPC va a pedir cuando exista esa mecánica —
    /// se calcula al vuelo a propósito: las tres reservas son independientes y no hay campo
    /// "carencia" guardado (ver GDD §22.2).
    /// </summary>
    private static RecursoVivienda RecursoMasBajo(StatsVivienda stats, out float valor)
    {
        RecursoVivienda peor = RecursoVivienda.Agua;
        valor = stats.agua;
        if (stats.comida < valor) { peor = RecursoVivienda.Comida; valor = stats.comida; }
        if (stats.energia < valor) { peor = RecursoVivienda.Energia; valor = stats.energia; }
        return peor;
    }

    private static string Nombre(RecursoVivienda r)
    {
        switch (r)
        {
            case RecursoVivienda.Agua: return "AGUA";
            case RecursoVivienda.Comida: return "COMIDA";
            default: return "ENERGÍA";
        }
    }

    private string Formatear(float segundos)
    {
        if (!mostrarComoTiempo) return $"{segundos:0}s";
        int total = Mathf.Max(0, Mathf.RoundToInt(segundos));
        return $"{total / 60:00}:{total % 60:00}";
    }

    private void MostrarSinVivienda()
    {
        if (textoTitulo != null) { textoTitulo.text = mensajeSinVivienda; textoTitulo.color = colorNormal; }
        if (textoResumen != null) { textoResumen.text = mensajeSinVivienda; textoResumen.color = colorNormal; }
        if (textoCarencia != null) textoCarencia.text = "";
        PintarStat(infoAgua, 0f);
        PintarStat(infoComida, 0f);
        PintarStat(infoEnergia, 0f);
    }
}
