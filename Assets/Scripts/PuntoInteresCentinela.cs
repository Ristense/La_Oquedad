using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto colocado a mano en pasillos/cuartos para que los Centinela lo usen como
/// destino de patrulla/inspección, o como base de retiro al terminar una ronda.
/// Se auto-registra en una lista estática (no hace falta que el manager los busque).
/// </summary>
public class PuntoInteresCentinela : MonoBehaviour
{
    public enum Tipo { Inspeccion, BaseSalida }

    [Tooltip("Inspeccion = destino de patrulla normal. BaseSalida = punto donde los centinelas se retiran y despawnean al terminar la ronda.")]
    public Tipo tipo = Tipo.Inspeccion;

    public static readonly List<PuntoInteresCentinela> Todos = new List<PuntoInteresCentinela>();

    void OnEnable() => Todos.Add(this);
    void OnDisable() => Todos.Remove(this);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = (tipo == Tipo.BaseSalida) ? Color.green : new Color(1f, 0.6f, 0f);
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}
