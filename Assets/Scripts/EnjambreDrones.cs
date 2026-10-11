using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manager de drones del mundo (NO atados al jugador). Mantiene hasta `cantidadObjetivo`
/// drones simultáneos en el exterior (Abismo) dentro de una burbuja alrededor del jugador.
/// Los drones se auto-mueven con `DronVagabundo`; el manager solo spawnea/despawnea.
/// </summary>
public class EnjambreDrones : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject prefabDron;
    [Tooltip("Jugador a usar como centro de la burbuja de spawn/despawn. Si está vacío, se busca por tag 'Player'.")]
    public Transform jugador;

    [Header("Cantidad")]
    [Tooltip("Cantidad máxima de drones simultáneos dentro de la burbuja.")]
    public int cantidadObjetivo = 15;

    [Header("Spawn (exterior alrededor del jugador)")]
    [Tooltip("Distancia mínima horizontal al jugador para spawnear.")]
    public float radioSpawnMin = 30f;
    [Tooltip("Distancia máxima horizontal al jugador para spawnear.")]
    public float radioSpawnMax = 120f;
    [Tooltip("Altura mínima sobre el jugador donde se busca spawn (positivo = arriba).")]
    public float alturaSpawnMin = 5f;
    [Tooltip("Altura máxima sobre el jugador.")]
    public float alturaSpawnMax = 60f;
    [Tooltip("Cuántos intentos por spawn antes de descartar en este tick.")]
    public int intentosSpawn = 20;
    [Tooltip("Radio del OverlapSphere para detectar que no hay paredes en el punto de spawn.")]
    public float radioCheckSphere = 3f;
    [Tooltip("Si no hay techo a esta distancia sobre el spawn, se considera exterior.")]
    public float alturaCheckTecho = 15f;
    [Tooltip("Capas que cuentan como obstáculo.")]
    public LayerMask capasObstaculo = ~0;

    [Header("Despawn")]
    [Tooltip("Si un dron está más lejos que esto del jugador, se destruye.")]
    public float distanciaDespawn = 250f;

    [Header("Tick")]
    [Tooltip("Cada cuántos segundos chequea cantidad y despawn.")]
    public float intervaloTick = 1.5f;


    private float temporizador;
    private List<GameObject> dronesVivos = new List<GameObject>();

    void Start()
    {
        if (jugador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) jugador = p.transform;
        }
    }

    void Update()
    {
        if (jugador == null || prefabDron == null) return;
        temporizador += Time.deltaTime;
        if (temporizador < intervaloTick) return;
        temporizador = 0f;

        // Limpiar nulos y despawnear lejanos
        for (int i = dronesVivos.Count - 1; i >= 0; i--)
        {
            GameObject d = dronesVivos[i];
            if (d == null) { dronesVivos.RemoveAt(i); continue; }
            float dist = Vector3.Distance(d.transform.position, jugador.position);
            if (dist > distanciaDespawn)
            {
                Destroy(d);
                dronesVivos.RemoveAt(i);
            }
        }

        // Spawnear hasta llegar al objetivo
        int faltan = cantidadObjetivo - dronesVivos.Count;
        for (int i = 0; i < faltan; i++)
        {
            if (TryEncontrarPosicionExterior(out Vector3 pos))
            {
                GameObject d = Instantiate(prefabDron, pos, Quaternion.identity);
                dronesVivos.Add(d);
            }
        }
    }

    private bool TryEncontrarPosicionExterior(out Vector3 pos)
    {
        for (int i = 0; i < intentosSpawn; i++)
        {
            // Posición horizontal aleatoria alrededor del jugador + siempre ARRIBA de él
            Vector2 h = Random.insideUnitCircle.normalized;
            float r = Random.Range(radioSpawnMin, radioSpawnMax);
            float y = Random.Range(alturaSpawnMin, alturaSpawnMax);
            Vector3 candidato = jugador.position + new Vector3(h.x * r, y, h.y * r);

            // 1) Sin paredes/suelo inmediato (el punto debe estar en espacio libre)
            if (Physics.CheckSphere(candidato, radioCheckSphere, capasObstaculo, QueryTriggerInteraction.Ignore))
                continue;

            // 2) Sin techo encima (si hay techo cerca, está dentro de la estructura)
            if (Physics.Raycast(candidato, Vector3.up, alturaCheckTecho, capasObstaculo, QueryTriggerInteraction.Ignore))
                continue;

            // 3) Sin suelo justo abajo en los primeros 2m (no sobre un pasillo cubierto)
            if (Physics.Raycast(candidato, Vector3.down, 2f, capasObstaculo, QueryTriggerInteraction.Ignore))
                continue;

            pos = candidato;
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (jugador == null) return;
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.15f);
        Gizmos.DrawWireSphere(jugador.position, radioSpawnMin);
        Gizmos.DrawWireSphere(jugador.position, radioSpawnMax);
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.15f);
        Gizmos.DrawWireSphere(jugador.position, distanciaDespawn);
    }
}
