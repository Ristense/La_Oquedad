using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Núcleo lógico de la mochila modular (estilo Lego/Trailmakers, plano 2D).
/// Mantiene el conjunto de celdas que EXISTEN (base 2x2 + módulos pegados).
/// No dibuja nada: solo la lógica. El visualizador y el drag&drop se montan encima.
/// </summary>
public class MochilaGrid : MonoBehaviour
{
    [Header("Mochila Base")]
    [Tooltip("Tamaño de la mochila base fija (por defecto 2x2).")]
    public Vector2Int tamanoBase = new Vector2Int(2, 2);

    // Celdas que existen actualmente (base + módulos pegados)
    private readonly HashSet<Vector2Int> celdas = new HashSet<Vector2Int>();

    // Celdas ocupadas por items guardados (celda -> item que la ocupa)
    private readonly Dictionary<Vector2Int, InventoryItem> ocupadas = new Dictionary<Vector2Int, InventoryItem>();

    private static readonly Vector2Int[] dirs =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    public IReadOnlyCollection<Vector2Int> Celdas => celdas;

    /// <summary>
    /// Suma el peso (kg) de todos los items GUARDADOS en la mochila. Cada item ocupa varias
    /// celdas pero solo cuenta una vez (se deduplica por instancia de InventoryItem).
    /// </summary>
    public float PesoTotalItemsGuardados()
    {
        if (ocupadas.Count == 0) return 0f;
        var vistos = new HashSet<InventoryItem>();
        float total = 0f;
        foreach (var kv in ocupadas)
        {
            InventoryItem it = kv.Value;
            if (it == null || it.data == null) continue;
            if (vistos.Add(it)) total += it.data.peso;
        }
        return total;
    }

    private void Awake()
    {
        InicializarBase();
    }

    public void InicializarBase()
    {
        celdas.Clear();
        for (int x = 0; x < tamanoBase.x; x++)
            for (int y = 0; y < tamanoBase.y; y++)
                celdas.Add(new Vector2Int(x, y));

        // Debug.Log($"[MochilaGrid] Base {tamanoBase.x}x{tamanoBase.y} inicializada. Celdas: {celdas.Count}, anclajes libres: {AnclajesLibres().Count}");
    }

    public bool ExisteCelda(Vector2Int c) => celdas.Contains(c);

    // ---------- MÓDULOS ----------

    /// <summary>¿Se puede pegar este módulo con su origen en `origen`?</summary>
    public bool PuedeColocarModulo(IList<Vector2Int> forma, Vector2Int origen)
    {
        if (forma == null || forma.Count == 0) return false;

        bool tocaExistente = false;
        foreach (var rel in forma)
        {
            Vector2Int c = rel + origen;
            if (celdas.Contains(c)) return false; // pisaría una celda ya existente

            // ¿alguna celda del módulo es ortogonalmente adyacente a una celda existente?
            foreach (var d in dirs)
                if (celdas.Contains(c + d)) { tocaExistente = true; break; }
        }
        return tocaExistente; // debe conectarse al menos por un lado (sin diagonales)
    }

    /// <summary>Pega el módulo si es válido. Devuelve true si lo agregó.</summary>
    public bool ColocarModulo(IList<Vector2Int> forma, Vector2Int origen)
    {
        if (!PuedeColocarModulo(forma, origen)) return false;

        foreach (var rel in forma)
            celdas.Add(rel + origen);

        // Debug.Log($"[MochilaGrid] Módulo de {forma.Count} celdas pegado en {origen}. Total celdas: {celdas.Count}");
        return true;
    }

    /// <summary>¿Esta celda pertenece a la mochila base (no se puede quitar)?</summary>
    public bool EsCeldaBase(Vector2Int c)
        => c.x >= 0 && c.x < tamanoBase.x && c.y >= 0 && c.y < tamanoBase.y;

    /// <summary>Quita las celdas de un módulo (las de la base no se tocan).</summary>
    public void QuitarModulo(IList<Vector2Int> forma, Vector2Int origen)
    {
        if (forma == null) return;
        foreach (var rel in forma)
        {
            Vector2Int c = rel + origen;
            if (EsCeldaBase(c)) continue; // nunca borrar la base
            celdas.Remove(c);
        }
        // Debug.Log($"[MochilaGrid] Módulo quitado de {origen}. Total celdas: {celdas.Count}");
    }

    // ---------- ITEMS GUARDADOS (ocupan celdas existentes) ----------

    /// <summary>¿Cabe un item de wxh con su esquina en `origen`? Todas sus celdas deben existir y estar libres.</summary>
    public bool PuedeColocarItem(Vector2Int origen, int w, int h, InventoryItem ignorar = null)
    {
        for (int i = 0; i < w; i++)
            for (int j = 0; j < h; j++)
            {
                Vector2Int c = origen + new Vector2Int(i, j);
                if (!celdas.Contains(c)) return false; // no hay celda ahí
                if (ocupadas.TryGetValue(c, out var it) && it != null && it != ignorar) return false; // ocupada
            }
        return true;
    }

    /// <summary>Marca las celdas como ocupadas por el item. Limpia su posición anterior primero.</summary>
    public bool ColocarItem(InventoryItem item, Vector2Int origen, int w, int h)
    {
        if (!PuedeColocarItem(origen, w, h, item)) return false;
        QuitarItem(item);
        for (int i = 0; i < w; i++)
            for (int j = 0; j < h; j++)
                ocupadas[origen + new Vector2Int(i, j)] = item;
        return true;
    }

    /// <summary>Libera todas las celdas ocupadas por este item.</summary>
    public void QuitarItem(InventoryItem item)
    {
        var claves = new List<Vector2Int>();
        foreach (var kvp in ocupadas)
            if (kvp.Value == item) claves.Add(kvp.Key);
        foreach (var k in claves) ocupadas.Remove(k);
    }

    public bool CeldaOcupada(Vector2Int c) => ocupadas.ContainsKey(c);

    // ---------- ANCLAJES ----------

    /// <summary>
    /// Celdas vacías ortogonalmente adyacentes a una celda existente.
    /// Son los puntos donde un módulo de 1 celda podría engancharse.
    /// </summary>
    public List<Vector2Int> AnclajesLibres()
    {
        HashSet<Vector2Int> set = new HashSet<Vector2Int>();
        foreach (var c in celdas)
            foreach (var d in dirs)
            {
                Vector2Int n = c + d;
                if (!celdas.Contains(n)) set.Add(n);
            }
        return new List<Vector2Int>(set);
    }
}
