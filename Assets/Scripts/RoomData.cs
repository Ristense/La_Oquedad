using UnityEngine;

[CreateAssetMenu(fileName = "NuevaSala", menuName = "Mundo/RoomData")]
public class RoomData : ScriptableObject
{
    [Header("Identificación")]
    public string nombreSala = "Sala";
    [Tooltip("Si está activo, esta sala es un CUARTEL DE CENTINELAS: las rondas de vigías spawnean desde la sala de este tipo más cercana al jugador y se retiran a ella al terminar (RondaCentinelas/Centinela).")]
    public bool esCuartelCentinelas = false;
    [Tooltip("Qué tan seguido aparece esta sala respecto a las demás. Es un PESO relativo, no una probabilidad: con Vivienda=10 y Cuartel=1, 1 de cada 11 salas es cuartel. Con todas en 1, todas son igual de comunes (reparto uniforme, el comportamiento viejo).")]
    [Min(0f)]
    public float pesoAparicion = 1f;

    [System.Serializable]
    public class LootEntry
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float probabilidad = 0.5f;
    }

    [Header("Loot / Objetos que pueden aparecer en la sala")]
    public LootEntry[] lootPosible;

    [Header("Cantidad de objetos de loot a spawnear en la sala")]
    public Vector2Int cantidadLoot = new Vector2Int(1, 3);

    [Header("Mobiliario / Decoración fija (opcional)")]
    public GameObject[] mobiliario;

    [Header("Prefabs Centrales (Se elige uno al azar para el centro de la sala)")]
    public GameObject[] prefabsCentrales;

    [Header("Secciones de Vivienda (prefabs con el script SeccionSala)")]
    [Tooltip("Las 4 secciones se reparten entre las 4 ESQUINAS del 3x3 (nunca bloquean el paso a las puertas). Qué esquina toca cada una se baraja por semilla, así cada vivienda queda distinta. Dejar un slot vacío = esa esquina queda sin sección.")]
    public GameObject seccionAgua;
    public GameObject seccionComida;
    public GameObject seccionBateria;
    public GameObject seccionDormir;
    [Tooltip("Sección de la celda CENTRAL (almacenamiento). Se instancia además del prefab central elegido de prefabsCentrales — ojo de que no se superpongan visualmente. Vacío = la central queda solo con su prefab central de siempre.")]
    public GameObject seccionAlmacenamiento;

    [Header("Luces de Techo (8 celdas del 3x3, excluye la central)")]
    [Tooltip("Si está activo, esta sala spawnea luces colgando del techo de las 8 celdas no-centrales.")]
    public bool generarLuces = true;
    [Tooltip("Prefabs de luz a elegir aleatoriamente (puede haber distintos modelos / variaciones).")]
    public GameObject[] prefabsLuces;
    [Tooltip("Rango (min, max) inclusivo de cuántas luces colgar por celda de techo.")]
    public Vector2Int cantidadLuces = new Vector2Int(1, 2);
    [Tooltip("Pequeña distancia desde la malla del techo hacia abajo (m) para que el pivote no quede atrapado.")]
    public float luzOffsetTecho = 0.05f;

}
