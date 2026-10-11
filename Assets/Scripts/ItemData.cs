using UnityEngine;

/// <summary>Categoría manual para agrupar items en la Bitácora (pestañas). Asignala a mano por ItemData.</summary>
public enum CategoriaItem
{
    Chatarra,
    Consumible,
    Modulo,
    Herramienta,
    Documento,
    Otro
}

[CreateAssetMenu(menuName = "Inventario/Item")]
public class ItemData : ScriptableObject
{
    [Header("Identificación")]
    public string nombreArticulo = "Objeto Desconocido";
    public string tipoMaterial = "Chatarra";
    [Tooltip("Categoría para agrupar en las pestañas de la Bitácora. Asignala a mano según qué es el objeto.")]
    public CategoriaItem categoria = CategoriaItem.Chatarra;

    [Header("Propiedades Físicas")]
    public float peso = 1.5f;

    [Header("Inventario (UI)")]
    public int width = 1;
    public int height = 1;
    public Sprite icon;

    [Header("Módulo de Mochila")]
    public bool esModulo = false;
    public int moduloAnchoExtra = 0;
    public int moduloAltoExtra = 0;

    // Método virtual: significa que es un molde vacío. Las clases "hijas" pueden sobreescribirlo.
    // Por defecto, un objeto normal como chatarra no hace nada si intentas "usarlo".
    public virtual void Usar(PlayerData player)
    {
        // Debug.Log($"El objeto {nombreArticulo} no se puede consumir ni usar de esta forma.");
    }

    /// <summary>
    /// Texto que muestra la UI (InfoObjetoUI) al apuntar/inspeccionar el objeto ya descubierto.
    /// La base da nombre + peso; las clases hijas (ej. ConsumibleData) agregan sus estadísticas.
    /// </summary>
    public virtual string DescripcionTooltip()
    {
        return $"{nombreArticulo}\nPeso: {peso:F1} KG";
    }

    /// <summary>Formatea segundos a m:ss (mismo estilo que el HUD).</summary>
    protected static string FormatoSegundos(float seg)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seg));
        return $"{total / 60}:{(total % 60):D2}";
    }
}
