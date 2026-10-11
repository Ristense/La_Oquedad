using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un módulo de mochila ES un item (hereda de ItemData), por lo que se puede
/// recoger del mundo, pesar, etc. Además define su FORMA como lista de celdas
/// relativas a su origen (0,0). 1 celda = solo (0,0). Una L = (0,0),(0,1),(1,1)...
/// La forma se edita visualmente desde el inspector (ver ModuloDataEditor).
/// </summary>
[CreateAssetMenu(menuName = "Inventario/Modulo")]
public class ModuloData : ItemData
{
    [Header("Forma del Módulo (celdas relativas a su origen 0,0)")]
    [Tooltip("Lista de celdas que ocupa el módulo. Editable con la grilla visual del inspector.")]
    public List<Vector2Int> forma = new List<Vector2Int> { Vector2Int.zero };

    [Header("Visual del Módulo")]
    [Tooltip("Prefab 3D opcional que representa este módulo enganchado a la mochila.")]
    public GameObject prefabVisual;

    /// <summary>Cantidad de celdas que aporta el módulo.</summary>
    public int CantidadCeldas => forma != null ? forma.Count : 0;
}
