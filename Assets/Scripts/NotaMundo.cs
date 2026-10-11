using UnityEngine;

/// <summary>
/// Componente del objeto físico de una nota en el mundo. Referencia un NotaData
/// (título / contenido / autor). Se recoge con Click Izq pero NO entra al inventario
/// ni ocupa celdas: se sostiene aparte y se "guarda" con E (entra a la BD del jugador).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class NotaMundo : MonoBehaviour
{
    [Tooltip("Datos de la nota: título, contenido, autor.")]
    public NotaData data;

    [Tooltip("Peso virtual en kg cuando está sostenida. Suma a PlayerData.pesoTotal pero no ocupa celdas.")]
    public float peso = 0.01f;

    void Start()
    {
        // Misión encadenada: la celda donde esta nota apareció es el PUNTO INICIAL.
        // Registrar la misión acá (al spawnear, antes de que la muevan) fija el destino
        // determinista por semilla. Si la misma NotaData ya se registró (otra copia
        // spawneó antes), WorldGenerator ignora el duplicado.
        if (data != null && data.esMisionEncadenada && WorldGenerator.Instance != null)
        {
            Vector3Int celdaInicial = WorldGenerator.Instance.MundoACelda(transform.position);
            WorldGenerator.Instance.RegistrarMisionEncadenada(data, celdaInicial);
        }
    }
}
