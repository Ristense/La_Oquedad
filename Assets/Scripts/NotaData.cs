using UnityEngine;

[CreateAssetMenu(fileName = "NuevaNota", menuName = "Lore/NotaData")]
public class NotaData : ScriptableObject
{
    [Header("Identificación")]
    public string titulo = "Sin Título";

    [Header("Contenido")]
    [TextArea(4, 30)]
    public string contenido = "Contenido vacío.";

    [Header("Autoría")]
    public string autor = "Anónimo";

    [Header("Misión / Tesoro Opcional")]
    [Tooltip("¿Esta nota tiene una misión asociada que genera un cofre con tesoro en la sala indicada?")]
    public bool tieneMision = false;
    [Tooltip("Prefab del contenedor/cofre que aparecerá en la sala de destino.")]
    public GameObject prefabCofre;

    [Header("Misión Encadenada (cadena de notas)")]
    [Tooltip("¿Esta nota inicia/continúa una misión encadenada? El punto donde SPAWNEA la nota es el inicio; el destino se genera por semilla a distanciaMisionCeldas de ahí, en cualquier dirección. La nota siempre muestra la celda destino y tu celda actual al leerla.")]
    public bool esMisionEncadenada = false;
    [Tooltip("Distancia aproximada (en celdas) entre el punto de spawn de la nota y el destino.")]
    public int distanciaMisionCeldas = 50;
    [Tooltip("Prefabs de materiales sueltos que aparecen tirados en el destino (además del cofre, si hay).")]
    public GameObject[] materialesRecompensa;
    [Tooltip("Cantidad de materiales sueltos a generar (min/max, aleatorio determinista).")]
    public Vector2Int cantidadMateriales = new Vector2Int(2, 4);
    [Tooltip("Siguiente nota de la cadena (null = esta es la última). Aparece en el destino junto a la recompensa, y al spawnear registra su propia misión → cadena de 3-5 notas.")]
    public NotaData notaSiguiente;
    [Tooltip("Prefab físico de nota (con NotaMundo) usado para instanciar la notaSiguiente en el destino. Se le asigna el data en runtime.")]
    public GameObject prefabNotaFisica;
}
