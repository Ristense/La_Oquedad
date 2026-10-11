using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// HUD siempre visible con el estado de las misiones de conexion entre viviendas: quien te la
/// dio, en que estado esta y cuantas conexiones llevas. Es la misma info que muestra la pestaña
/// "Lugares" de la Bitacora, pero permanente en pantalla.
///
/// El destino solo se revela si ESCUCHASTE la mision (misionAceptada): antes de eso no tenes
/// por que saber hacia donde ir. Se puede forzar con mostrarDestinoSiempre para debug.
/// </summary>
public class HUDMisionViviendas : MonoBehaviour
{
    [Tooltip("Texto donde se pinta la celda actual del jugador.")]
    public TMP_Text txtPosicionJugador;
    [Tooltip("Texto donde se pinta el estado de las misiones de vivienda.")]
    public TMP_Text txtRedViviendas;

    [Tooltip("Cada cuantos segundos se refresca el texto (no hace falta cada frame).")]
    public float intervaloRefresco = 0.5f;

    [Header("Opciones")]
    [Tooltip("Debug: muestra la celda destino aunque todavia no hayas escuchado al NPC.")]
    public bool mostrarDestinoSiempre = false;

    [Header("Colores de estado (hex)")]
    public string colorCompletada = "#7CFF7C";
    public string colorEnCurso = "#FFD86B";
    public string colorSinAceptar = "#9A9A9A";

    private float proximoRefresco = 0f;

    void Update()
    {
        if (Time.time < proximoRefresco) return;
        proximoRefresco = Time.time + intervaloRefresco;

        WorldGenerator wg = WorldGenerator.Instance;
        if (wg == null) return;

        if (txtPosicionJugador != null && wg.jugador != null)
        {
            Vector3Int celda = wg.MundoACelda(wg.jugador.position);
            txtPosicionJugador.text = $"Jugador: celda {celda}";
        }

        if (txtRedViviendas != null)
        {
            txtRedViviendas.text = ConstruirTexto(wg);
        }
    }

    private string ConstruirTexto(WorldGenerator wg)
    {
        StringBuilder sb = new StringBuilder();

        int total = 0;
        int conectadas = 0;
        foreach (DatosVivienda d in wg.ViviendasRegistradas)
        {
            total++;
            if (d.conectada) conectadas++;
        }

        if (total == 0) return "Sin viviendas encontradas todavía.";

        sb.AppendLine($"CONEXIONES: {conectadas} / {total}");

        foreach (DatosVivienda d in wg.ViviendasRegistradas)
        {
            string nombre = (d.habitante != null && !string.IsNullOrWhiteSpace(d.habitante.nombre))
                ? d.habitante.nombre
                : "Habitante";

            Vector3Int celdaEsta = CeldaCentralDeMacro(d.macroChunk, wg.macroSize);

            string estado;
            string color;
            if (d.conectada)      { estado = "COMPLETADA";  color = colorCompletada; }
            else if (d.misionAceptada) { estado = "EN CURSO"; color = colorEnCurso; }
            else                  { estado = "SIN ESCUCHAR"; color = colorSinAceptar; }

            sb.AppendLine($"<color={color}>[{estado}]</color> {nombre} · celda {celdaEsta}");

            // El destino solo se revela una vez que escuchaste la mision.
            if (d.tieneObjetivo && !d.conectada && (d.misionAceptada || mostrarDestinoSiempre))
            {
                Vector3Int celdaDestino = CeldaCentralDeMacro(d.macroChunkBuscado, wg.macroSize);
                string dir = wg.DireccionCardinal(d.macroChunk, d.macroChunkBuscado);
                sb.AppendLine($"    → buscar al {dir} · celda {celdaDestino}");
            }
            else if (d.conectada)
            {
                Vector3Int celdaCon = CeldaCentralDeMacro(d.conectadaCon, wg.macroSize);
                // Se usa "→" y no "✓": el check no existe en la fuente TMP del HUD y se
                // dibuja como cuadrito. La flecha ya está probada en la línea de arriba.
                sb.AppendLine($"    → conectada con celda {celdaCon}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    // Centro habitable (lx=2, ly=1, lz=2) del macro chunk — mismo criterio que usa
    // WorldGenerator en todos sus otros sistemas (ObtenerCeldaDestinoMision, etc).
    private static Vector3Int CeldaCentralDeMacro(Vector3Int macro, int macroSize)
    {
        return new Vector3Int(macro.x * macroSize + 2, macro.y * macroSize + 1, macro.z * macroSize + 2);
    }
}
