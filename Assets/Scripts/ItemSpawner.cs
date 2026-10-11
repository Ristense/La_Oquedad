using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [Header("Configuración de Spawn")]
    [Tooltip("Probabilidad de que aparezca un objeto (0 a 1). Ejemplo: 0.2 es 20%.")]
    [Range(0f, 1f)]
    public float probabilidadSpawn = 0.2f;

    [Tooltip("Lista de prefabs que pueden aparecer aquí (tuberías, chatarra, etc).")]
    public GameObject[] itemsPosibles;

    void Start()
    {
        // Generar una semilla única basada en la posición física en el mundo para evitar que todos los spawners
        // elijan el mismo item y para que el loot sea persistente si el chunk se recarga.
        int seed = Mathf.RoundToInt(transform.position.x * 73856093 + transform.position.y * 19349663 + transform.position.z * 83492791);
        Random.State estadoViejo = Random.state;
        Random.InitState(seed);

        // 1. Tirar los dados para ver si tenemos suerte
        if (Random.value <= probabilidadSpawn && itemsPosibles.Length > 0)
        {
            // 2. Elegir un prefab aleatorio de la lista
            GameObject prefabElegido = itemsPosibles[Random.Range(0, itemsPosibles.Length)];
            
            // 3. Instanciar el item exactamente en la posición y rotación de este punto vacío
            // Lo hacemos hijo del mismo padre (la pared) para mantener todo ordenado
            Instantiate(prefabElegido, transform.position, transform.rotation, transform.parent);
        }

        // Restaurar el estado aleatorio global para no interferir con otros sistemas
        Random.state = estadoViejo;

        // 4. Autodestruir este script/GameObject vacío. 
        // Ya hizo su trabajo, así que lo borramos para que no consuma memoria.
        Destroy(gameObject);
    }
}
