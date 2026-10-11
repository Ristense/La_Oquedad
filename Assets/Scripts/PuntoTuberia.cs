using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marcador para poner en una tubería REAL del mundo (el mismo punto donde ya vive el vapor
/// ambiente, o el pivote de la tubería si no hay uno). Se auto-registra en una lista estática —
/// mismo patrón que PuntoInteresCentinela — así SustoVapor puede encontrar "la tubería real más
/// cercana al jugador" sin que el WorldGenerator tenga que saber nada de esto.
///
/// OnEnable/OnDisable (no Awake/OnDestroy): las tuberías se activan y desactivan todo el tiempo
/// con el streaming de la burbuja, así que la lista tiene que reflejar SOLO las que están
/// actualmente visibles/activas — una tubería apagada lejos del jugador no debería poder "ganar"
/// la búsqueda de cercanía.
/// </summary>
public class PuntoTuberia : MonoBehaviour
{
    public static readonly List<PuntoTuberia> Todos = new List<PuntoTuberia>();

    void OnEnable() => Todos.Add(this);
    void OnDisable() => Todos.Remove(this);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}
