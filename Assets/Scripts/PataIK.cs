using UnityEngine;
using System.Collections;

/// <summary>
/// Logica de "pisada" de UNA pata: mide la distancia entre donde esta parado el pie (target)
/// y donde DEBERIA estar idealmente (un rayo hacia el piso desde puntoReposo, que sigue al
/// cuerpo). Si se estiro de mas, mueve el target al nuevo punto con un pasito animado.
///
/// No mueve huesos ni hace IK — eso ya lo resuelve el Two Bone IK Constraint solo, mirando
/// este mismo target. Esto SOLO decide cuando y donde debe pisar el pie.
/// </summary>
public class PataIK : MonoBehaviour
{
    [Tooltip("El Target que sigue el pie (el mismo que ya tenias, el que mueve el IK Constraint).")]
    public Transform target;
    [Tooltip("Punto fijo al cuerpo desde donde se busca el piso hacia abajo — normalmente el hombro/cadera de la pata (el '_Raiz').")]
    public Transform puntoReposo;

    [Header("Cuando pisar")]
    [Tooltip("Que tan lejos del punto ideal puede estirarse el pie antes de moverse a buscar uno nuevo.")]
    public float distanciaMaximaEstiramiento = 0.6f;

    [Header("Donde buscar el piso")]
    [Tooltip("Cuanto mas arriba de puntoReposo arranca el rayo (para no empezar adentro del piso si el cuerpo esta bajo).")]
    public float alturaInicioRayo = 1.5f;
    [Tooltip("Cuanto puede bajar el rayo buscando el piso.")]
    public float alcanceRayo = 3f;
    [Tooltip("Capas que cuentan como piso/pared donde puede pisar.")]
    public LayerMask capasPiso = ~0;

    [Header("Animacion del paso")]
    [Tooltip("Cuanto tarda en moverse de un punto al otro (segundos).")]
    public float duracionPaso = 0.15f;
    [Tooltip("Cuanto se levanta el pie a mitad de camino (efecto de paso, no arrastrado).")]
    public float alturaPaso = 0.3f;

    public bool EnPaso { get; private set; }

    void Update()
    {
        if (EnPaso || target == null || puntoReposo == null) return;

        if (TryBuscarPuntoIdeal(out Vector3 puntoIdeal))
        {
            float dist = Vector3.Distance(target.position, puntoIdeal);
            if (dist > distanciaMaximaEstiramiento)
            {
                StartCoroutine(DarPaso(puntoIdeal));
            }
        }
    }

    private bool TryBuscarPuntoIdeal(out Vector3 punto)
    {
        Vector3 origen = puntoReposo.position + Vector3.up * alturaInicioRayo;
        if (Physics.Raycast(origen, Vector3.down, out RaycastHit hit, alturaInicioRayo + alcanceRayo, capasPiso, QueryTriggerInteraction.Ignore))
        {
            punto = hit.point;
            return true;
        }
        punto = Vector3.zero;
        return false;
    }

    private IEnumerator DarPaso(Vector3 destino)
    {
        EnPaso = true;
        Vector3 origen = target.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duracionPaso);
            float tc = Mathf.Clamp01(t);

            Vector3 pos = Vector3.Lerp(origen, destino, tc);
            pos.y += Mathf.Sin(tc * Mathf.PI) * alturaPaso; // arco: sube a mitad de camino, no arrastra
            target.position = pos;

            yield return null;
        }

        target.position = destino;
        EnPaso = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (puntoReposo == null) return;
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
        Vector3 origen = puntoReposo.position + Vector3.up * alturaInicioRayo;
        Gizmos.DrawLine(origen, origen + Vector3.down * (alturaInicioRayo + alcanceRayo));

        if (target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position, distanciaMaximaEstiramiento);
        }
    }
}
