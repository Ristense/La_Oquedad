using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zona de uso general de la comunidad (las celdas de relleno): la gente la usa para lo que haga
/// falta. Se pone en el ROOT del prefab de relleno (puede ser un GameObject vacío) y el
/// WorldGenerator la llena al generar la celda, ordenado y por semilla:
///   · Faroles: en 1 a 4 esquinas, o uno solo en el centro.
///   · Mesas: en una grilla pareja según el tamaño de la celda, todas con el mismo giro.
/// Las sillas (o lo que sea) van como PuntoSpawnItem dentro del prefab de la mesa: se resuelven
/// después de poner las mesas.
/// Todo queda en el espacio local del prefab, así que gira junto con él.
/// </summary>
public class ZonaGeneral : MonoBehaviour
{
    [Header("Área")]
    [Tooltip("Distancia (m) desde el borde de la celda que no se usa: el espacio para caminar junto a las paredes.")]
    [Min(0f)] public float margen = 2f;

    [Tooltip("Cuánto (m) se sube TODA la zona al generarse: lo que pusiste a mano adentro, las mesas y faroles, y las sillas de los PuntoSpawnItem. Para compensar el grosor del piso. Ajustalo en Play hasta que apoyen bien.")]
    public float alturaExtra = 0f;
    [Tooltip("Log en consola de cada cosa que pone: dónde quedó y el tamaño de su malla.")]
    public bool log = true;

    [Header("Faroles")]
    public GameObject[] prefabsFarol;
    [Tooltip("Probabilidad de poner UN farol en el centro en vez de faroles en las esquinas.")]
    [Range(0f, 1f)] public float probabilidadFarolCentro = 0.5f;
    [Tooltip("Cuántas esquinas llevan farol (mín, máx) cuando no va al centro.")]
    public Vector2Int farolesEsquina = new Vector2Int(1, 4);
    [Tooltip("Distancia (m) de los faroles de esquina al borde de la celda.")]
    [Min(0f)] public float margenFarol = 1.5f;

    [Header("Mesas")]
    public GameObject[] prefabsMesa;
    [Tooltip("Cuántas mesas (mín, máx). Si no caben en la grilla, se ponen las que quepan.")]
    public Vector2Int cantidadMesas = new Vector2Int(2, 4);
    [Tooltip("Espacio (m) que ocupa cada mesa con sus sillas. Con esto se arma la grilla: más chico = más lugares.")]
    [Min(1f)] public float espacioMesa = 5f;
    [Tooltip("Corrimiento al azar (m) de cada mesa dentro de su lugar. 0 = grilla perfecta.")]
    [Min(0f)] public float desorden = 0.3f;
    [Tooltip("Si está activo, cada mesa puede quedar girada 90° respecto a las otras. Apagado = todas iguales.")]
    public bool girosMezclados = false;
    [Tooltip("Giro extra al azar de cada mesa, hasta este ángulo (grados) para cada lado. 0 = todas derechas.")]
    [Range(0f, 45f)] public float giroAlAzar = 20f;

    /// <summary>Llena la zona. Lo llama el WorldGenerator con el tamaño de la celda y un hash estable.</summary>
    public void Poblar(float tamanoCelda, int hash)
    {
        var rng = new System.Random(hash);
        if (log) Debug.Log($"[ZonaGeneral] {name}: poblando · faroles {(prefabsFarol == null ? 0 : prefabsFarol.Length)} · mesas {(prefabsMesa == null ? 0 : prefabsMesa.Length)}", this);
        float mitad = tamanoCelda * 0.5f;
        bool farolEnCentro = false;

        // --- Faroles ---
        if (prefabsFarol != null && prefabsFarol.Length > 0)
        {
            if (rng.NextDouble() < probabilidadFarolCentro)
            {
                farolEnCentro = true;
                Poner(Elegir(prefabsFarol, rng), Vector3.zero, 0f);
            }
            else
            {
                float e = mitad - margenFarol;
                var esquinas = new List<Vector3> { new Vector3(e, 0, e), new Vector3(-e, 0, e), new Vector3(e, 0, -e), new Vector3(-e, 0, -e) };
                Barajar(esquinas, rng);
                int min = Mathf.Clamp(Mathf.Min(farolesEsquina.x, farolesEsquina.y), 0, 4);
                int max = Mathf.Clamp(Mathf.Max(farolesEsquina.x, farolesEsquina.y), min, 4);
                int n = rng.Next(min, max + 1);
                for (int i = 0; i < n; i++)
                {
                    // Que mire hacia el centro de la celda.
                    float yaw = Mathf.Atan2(-esquinas[i].x, -esquinas[i].z) * Mathf.Rad2Deg;
                    Poner(Elegir(prefabsFarol, rng), esquinas[i], yaw);
                }
            }
        }

        // --- Mesas en grilla ---
        if (prefabsMesa != null && prefabsMesa.Length > 0)
        {
            float util = tamanoCelda - 2f * margen;
            int lado = Mathf.Max(1, Mathf.FloorToInt(util / espacioMesa));
            float paso = util / lado;
            int centro = lado / 2;

            var lugares = new List<Vector3>();
            for (int i = 0; i < lado; i++)
                for (int j = 0; j < lado; j++)
                {
                    if (farolEnCentro && lado % 2 == 1 && i == centro && j == centro) continue;
                    lugares.Add(new Vector3(-util * 0.5f + paso * (i + 0.5f), 0f, -util * 0.5f + paso * (j + 0.5f)));
                }
            Barajar(lugares, rng);

            int min = Mathf.Max(0, Mathf.Min(cantidadMesas.x, cantidadMesas.y));
            int max = Mathf.Max(min, Mathf.Max(cantidadMesas.x, cantidadMesas.y));
            int cuantas = Mathf.Min(rng.Next(min, max + 1), lugares.Count);
            float yawBase = rng.Next(2) * 90f;

            for (int i = 0; i < cuantas; i++)
            {
                Vector3 p = lugares[i] + new Vector3(
                    (float)(rng.NextDouble() * 2 - 1) * desorden, 0f,
                    (float)(rng.NextDouble() * 2 - 1) * desorden);
                float yaw = yawBase + (girosMezclados ? rng.Next(2) * 90f : 0f)
                    + (float)(rng.NextDouble() * 2 - 1) * giroAlAzar;
                Poner(Elegir(prefabsMesa, rng), p, yaw);
            }
        }
    }

    private void Poner(GameObject prefab, Vector3 local, float yaw)
    {
        if (prefab == null) return;
        GameObject go = Instantiate(prefab, transform);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        if (log)
        {
            string malla = "sin renderers";
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                malla = $"malla centro {b.center} tamaño {b.size}";
            }
            Debug.Log($"[ZonaGeneral] {name}: puse '{prefab.name}' en local {local} (mundo {go.transform.position}) · {malla}", go);
        }
    }

    private static GameObject Elegir(GameObject[] lista, System.Random rng) => lista[rng.Next(lista.Length)];

    private static void Barajar<T>(List<T> l, System.Random rng)
    {
        for (int i = l.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (l[i], l[j]) = (l[j], l[i]);
        }
    }
}
