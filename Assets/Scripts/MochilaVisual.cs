using UnityEngine;

/// <summary>
/// Dibuja la forma actual de la mochila (celdas existentes + anclajes libres)
/// usando Gizmos, en el plano local XY del objeto (paralelo a la cara de la mochila).
/// </summary>
[RequireComponent(typeof(MochilaGrid))]
public class MochilaVisual : MonoBehaviour
{
    [Tooltip("Tamaño de cada celda en unidades de mundo/local.")]
    public float tamanoCelda = 0.2f;

    [Tooltip("Offset local de la grilla en unidades de celda. Ajustalo si el prefab cambió y querés reubicar el snap sin tocar el pivote.")]
    public Vector2 offsetLocalCeldas = Vector2.zero;

    [Tooltip("Grosor visual de las celdas en el eje Z local.")]
    public float grosor = 0.02f;

    private MochilaGrid grid;

    private void Awake()
    {
        grid = GetComponent<MochilaGrid>();
    }

    public Vector3 CeldaACentroLocal(Vector2Int celda)
    {
        return new Vector3(
            (offsetLocalCeldas.x + celda.x + 0.5f) * tamanoCelda,
            (offsetLocalCeldas.y + celda.y + 0.5f) * tamanoCelda,
            0f);
    }

    public Vector2Int LocalACelda(Vector3 localPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(localPos.x / tamanoCelda - offsetLocalCeldas.x),
            Mathf.FloorToInt(localPos.y / tamanoCelda - offsetLocalCeldas.y));
    }

    public Vector3 CentroRectLocal(Vector2Int origen, int w, int h)
    {
        return new Vector3(
            (offsetLocalCeldas.x + origen.x + w * 0.5f) * tamanoCelda,
            (offsetLocalCeldas.y + origen.y + h * 0.5f) * tamanoCelda,
            0f);
    }

    public Vector3 CentroFormaLocal(System.Collections.Generic.IList<Vector2Int> forma, Vector2Int origen)
    {
        Vector2 suma = Vector2.zero;
        foreach (var rel in forma)
        {
            Vector2Int c = rel + origen;
            suma += new Vector2(c.x + 0.5f, c.y + 0.5f);
        }
        suma /= Mathf.Max(1, forma.Count);
        return new Vector3(
            (offsetLocalCeldas.x + suma.x) * tamanoCelda,
            (offsetLocalCeldas.y + suma.y) * tamanoCelda,
            0f);
    }

    private void OnDrawGizmos()
    {
        MochilaGrid g = grid != null ? grid : GetComponent<MochilaGrid>();
        if (g == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.55f);
        if (g.Celdas != null && g.Celdas.Count > 0)
        {
            foreach (var c in g.Celdas)
            {
                Vector3 centro = CeldaACentroLocal(c);
                Gizmos.DrawCube(centro, new Vector3(tamanoCelda * 0.92f, tamanoCelda * 0.92f, grosor));
            }
        }
        else
        {
            for (int x = 0; x < g.tamanoBase.x; x++)
            {
                for (int y = 0; y < g.tamanoBase.y; y++)
                {
                    Vector3 centro = CeldaACentroLocal(new Vector2Int(x, y));
                    Gizmos.DrawCube(centro, new Vector3(tamanoCelda * 0.92f, tamanoCelda * 0.92f, grosor));
                }
            }
            return;
        }

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        foreach (var a in g.AnclajesLibres())
        {
            Vector3 centro = CeldaACentroLocal(a);
            Gizmos.DrawWireCube(centro, new Vector3(tamanoCelda * 0.92f, tamanoCelda * 0.92f, grosor));
        }
    }
}
