using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Pantalla LED dentro de una vivienda: lista los items que quedan físicamente parados ahí.
/// No mantiene ningún inventario propio — lee directo del WorldGenerator qué GameObjects siguen
/// registrados en la celda central de esa vivienda. Si el jugador roba un item, ManoJugador lo
/// reparenta a la mano/mochila (deja de ser hijo directo del WorldGenerator) y la pantalla lo
/// nota solo en el próximo refresco, sin que nadie le avise nada.
///
/// Mismo patrón que NPCHabitante: deduce su propia vivienda por posición, no depende de que
/// nadie le inyecte nada al instanciarla — funciona igual embebida en el prefab de la sala.
/// </summary>
public class PantallaInventarioVivienda : MonoBehaviour
{
    [Tooltip("Texto donde se lista el inventario. Requerido.")]
    public TMP_Text texto;

    [Tooltip("OPCIONAL: texto aparte para la cabecera ('INVENTARIO (7 · 3,5kg)'). Si lo asignás, la cabecera deja de ir pegada arriba del listado y podés maquetarla por tu cuenta. Vacío = como antes, la cabecera va dentro del mismo texto.")]
    public TMP_Text textoCabecera;
    [Tooltip("Formato de la cabecera. Comodines: {N} = cantidad de items, {KG} = peso total.")]
    public string formatoCabecera = "INVENTARIO ({N} · {KG}kg)";

    [Tooltip("Cada cuánto se refresca (segundos). No hace falta que sea cada frame.")]
    public float intervaloRefresco = 1f;

    [Tooltip("Mostrar el peso de cada item.")]
    public bool mostrarPeso = true;

    [Tooltip("Texto cuando no queda nada.")]
    public string mensajeVacio = "INVENTARIO VACÍO";

    [Tooltip("Parpadeo sutil tipo pantalla vieja (0 = apagado).")]
    [Range(0f, 1f)]
    public float intensidadParpadeo = 0.05f;

    private Vector3Int macroChunkVivienda;
    private bool macroResuelto = false;
    private float proximoRefresco = 0f;

    void Update()
    {
        ResolverMacroChunk();

        if (Time.time < proximoRefresco) return;
        proximoRefresco = Time.time + intervaloRefresco;

        Refrescar();
    }

    private void ResolverMacroChunk()
    {
        if (macroResuelto) return;
        if (WorldGenerator.Instance == null) return;

        macroChunkVivienda = WorldGenerator.Instance.MundoAMacroChunk(transform.position);
        macroResuelto = true;
    }

    private void Refrescar()
    {
        if (texto == null || WorldGenerator.Instance == null) return;

        WorldGenerator wg = WorldGenerator.Instance;
        Vector3Int celdaCentral = new Vector3Int(
            macroChunkVivienda.x * wg.macroSize + 2,
            macroChunkVivienda.y * wg.macroSize + 1,
            macroChunkVivienda.z * wg.macroSize + 2);

        IReadOnlyList<GameObject> objetos = wg.ObtenerObjetosDeCelda(celdaCentral);

        StringBuilder sb = new StringBuilder();
        int cantidad = 0;
        float pesoTotal = 0f;

        if (objetos != null)
        {
            foreach (GameObject go in objetos)
            {
                if (go == null || !go.activeInHierarchy) continue;
                // Sigue siendo "de la vivienda" solo si nadie lo movió de su lugar original
                // (ManoJugador reparenta al agarrar: mano, mochila, inspección).
                if (go.transform.parent != wg.transform) continue;

                InventoryItem item = go.GetComponent<InventoryItem>();
                if (item == null || item.data == null) continue;

                cantidad++;
                pesoTotal += item.data.peso;
                sb.Append("• ").Append(item.data.nombreArticulo);
                if (mostrarPeso) sb.Append("  ").Append(item.data.peso.ToString("F1")).Append("kg");
                sb.Append('\n');
            }
        }

        string cabecera = formatoCabecera
            .Replace("{N}", cantidad.ToString())
            .Replace("{KG}", pesoTotal.ToString("F1"));

        if (textoCabecera != null)
        {
            // Cabecera maquetada aparte: el listado queda limpio, solo los items.
            textoCabecera.text = cabecera;
            texto.text = cantidad == 0 ? mensajeVacio : sb.ToString().TrimEnd();
        }
        else
        {
            texto.text = cantidad == 0 ? mensajeVacio : $"{cabecera}\n{sb.ToString().TrimEnd()}";
        }

        if (intensidadParpadeo > 0.001f)
        {
            texto.alpha = 1f - Random.Range(0f, intensidadParpadeo);
        }
    }
}
