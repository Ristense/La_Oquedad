using UnityEngine;

/// <summary>
/// Marcador para poner DENTRO de un prefab (mueble, estante, locker, etc.): define qué items
/// pueden aparecer en ese punto exacto. El WorldGenerator lo encuentra al instanciar el prefab
/// y hace el roll usando el Random.State YA SEMBRADO de la celda — así el resultado es
/// determinista por semilla, igual que el resto de la generación procedural.
///
/// Uso: agregá este componente a un GameObject hijo del prefab (donde querés que "aparezca" el
/// item), completá `itemsPosibles` con los prefabs candidatos y su probabilidad, y listo — no
/// hace falta tocar WorldGenerator.cs para cada mueble nuevo.
/// </summary>
public class PuntoSpawnItem : MonoBehaviour
{
    [System.Serializable]
    public class ItemPosible
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float probabilidad = 0.5f;
    }

    [Tooltip("Items candidatos para este punto. Se evalúan en orden y el PRIMERO que pase su probabilidad es el elegido (no se acumulan varios).")]
    public ItemPosible[] itemsPosibles;

    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que este punto genere ALGO. Si falla, el punto queda vacío aunque itemsPosibles tenga candidatos.")]
    public float probabilidadSpawn = 1f;

    [Header("Grupo Excluyente (opcional)")]
    [Tooltip("Vacío (default) = este punto es TOTALMENTE independiente de los demás.\n\nPodés poner un nombre de grupo (ej: Palet_Izq) o varios separados por comas (ej: Palet_Izq, Palet_Der). En un grupo EXCLUYENTE se evalúan en orden de Prioridad, y en cuanto UNO logra spawnear algo, los demás de ese grupo se apagan directamente sin tirar dados.")]
    public string grupoExclusivo = "";

    [Tooltip("Solo importa si tiene Grupo Excluyente. Mayor número = se evalúa ANTES. El objeto grande que quiere prioridad va con un número alto; los que ceden el lugar se quedan en 0.")]
    public int prioridad = 0;
}
