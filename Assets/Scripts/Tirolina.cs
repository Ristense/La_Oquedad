using UnityEngine;

/// <summary>
/// Representa una tirolina YA armada en el mundo: dos puntos de anclaje (A y B),
/// su cuerda visual (LineRenderer) y un collider a lo largo para poder mirarla y engancharse.
/// La crea en runtime <see cref="SistemaTirolina"/> mediante <see cref="Crear"/>. Se queda en el
/// mundo (igual que las cuerdas de rapel) hasta que la destruyas.
///
/// A = primer disparo (el DESTINO al que apuntaste).
/// B = segundo disparo (TU extremo, el "lugar seguro").
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class Tirolina : MonoBehaviour
{
    [HideInInspector] public Vector3 anclaA;
    [HideInInspector] public Vector3 anclaB;

    private LineRenderer lr;

    public float Longitud => Vector3.Distance(anclaA, anclaB);

    /// <summary>Fábrica: crea el GameObject de la tirolina con su cuerda y collider ya listos.</summary>
    public static Tirolina Crear(Vector3 a, Vector3 b, Material material, float ancho, Color color)
    {
        GameObject go = new GameObject("Tirolina");
        Tirolina t = go.AddComponent<Tirolina>();
        t.anclaA = a;
        t.anclaB = b;
        t.ConfigurarVisual(material, ancho, color);
        t.ConstruirCollider();
        return t;
    }

    void Awake()
    {
        if (lr == null) lr = GetComponent<LineRenderer>();
    }

    private void ConfigurarVisual(Material material, float ancho, Color color)
    {
        if (lr == null) lr = GetComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;

        // En HDRP el LineRenderer necesita un material HDRP-compatible o sale invisible/magenta.
        if (material != null)
        {
            lr.material = material;
        }
        else if (lr.sharedMaterial == null)
        {
            Shader s = Shader.Find("HDRP/Unlit");
            if (s != null) lr.material = new Material(s);
        }

        lr.startWidth = ancho;
        lr.endWidth = ancho;
        lr.startColor = color;
        lr.endColor = color;
        lr.numCapVertices = 2; // puntas redondeadas

        lr.SetPosition(0, anclaA);
        lr.SetPosition(1, anclaB);
    }

    private void ConstruirCollider()
    {
        // Hijo con BoxCollider orientado a lo largo de la cuerda, para poder mirarla y engancharse.
        GameObject colGo = new GameObject("ColliderTirolina");
        colGo.transform.SetParent(transform);

        Vector3 centro = (anclaA + anclaB) * 0.5f;
        Vector3 dir = anclaB - anclaA;
        float largo = dir.magnitude;

        colGo.transform.position = centro;
        if (largo > 0.001f)
            colGo.transform.rotation = Quaternion.LookRotation(dir.normalized);

        BoxCollider box = colGo.AddComponent<BoxCollider>();
        box.center = Vector3.zero;
        // Grosor de 0.6 m para que sea fácil apuntarle; el "largo" (eje Z local) = distancia entre anclas.
        box.size = new Vector3(0.6f, 0.6f, largo);
    }

    /// <summary>
    /// Punto de la cuerda (segmento A–B) más cercano a p. El raycast pega en la cara del collider,
    /// que está 0.3 m afuera de la cuerda; esto lo devuelve a la cuerda misma.
    /// </summary>
    public Vector3 PuntoMasCercano(Vector3 p)
    {
        Vector3 ab = anclaB - anclaA;
        float largoCuadrado = ab.sqrMagnitude;
        if (largoCuadrado < 0.0001f) return anclaA;
        float t = Mathf.Clamp01(Vector3.Dot(p - anclaA, ab) / largoCuadrado);
        return anclaA + ab * t;
    }

    /// <summary>Extremo más cercano a una posición (desde ahí arrancas a deslizarte).</summary>
    public Vector3 ExtremoCercano(Vector3 desde)
    {
        return Vector3.Distance(desde, anclaA) <= Vector3.Distance(desde, anclaB) ? anclaA : anclaB;
    }

    /// <summary>Extremo opuesto al más cercano (hacia donde te deslizas).</summary>
    public Vector3 ExtremoLejano(Vector3 desde)
    {
        return Vector3.Distance(desde, anclaA) <= Vector3.Distance(desde, anclaB) ? anclaB : anclaA;
    }

    // Dibuja los anclajes en la vista de Escena para debug.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(anclaA, 0.4f);
        Gizmos.DrawWireSphere(anclaB, 0.4f);
    }
}
