using UnityEngine;
using TMPro; // Requerido para TextMeshPro 3D

/// <summary>
/// Calcula automáticamente las coordenadas de la grilla según su posición física
/// y las muestra en un TextMeshPro 3D con el formato: "X / PISO Y / Z".
/// </summary>
public class CartelCoordenadas : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Componente TextMeshPro (3D) que mostrará el texto en la pared. Si está vacío, buscará en el mismo objeto.")]
    public TMP_Text textoDisplay;

    [Header("Ajustes de Escala")]
    [Tooltip("Tamaño de cada chunk o celda en metros (In Side Us usa 20m).")]
    public float tamañoCelda = 20f;

    [Header("Formato Visual")]
    [Tooltip("Si está activo, convierte la X a una letra del abecedario (A-Z) para un estilo más militar/de sector.")]
    public bool usarLetraParaX = false;

    // --- Celda forzada por el WorldGenerator ---
    // El cartel vive clavado EN el plano de la cara = el límite exacto entre dos celdas, así que
    // derivar la celda desde transform.position es ambiguo (FloorToInt cae en la celda vecina en
    // las caras X+/Z+ → dos carteles del mismo pasillo mostraban celdas distintas). El generador
    // conoce la celda transitable real (emptyCell) y la inyecta acá antes de la activación.
    [HideInInspector] public bool usarCeldaForzada = false;
    [HideInInspector] public Vector3Int celdaForzada;

    void Start()
    {
        // 1. Obtener la referencia de texto si no se asignó en el inspector
        if (textoDisplay == null)
        {
            textoDisplay = GetComponent<TMP_Text>();
            if (textoDisplay == null)
            {
                textoDisplay = GetComponentInChildren<TMP_Text>();
            }
        }

        if (textoDisplay == null)
        {
            Debug.LogError($"[CartelCoordenadas] No se encontró ningún componente TextMeshPro en '{name}'.");
            return;
        }

        // 2. Determinar la celda a mostrar. Prioridad: la celda FORZADA por el WorldGenerator
        // (la celda transitable real hacia la que mira el cartel). El cálculo por posición queda
        // como fallback para carteles colocados a mano en la escena.
        int celdaX, celdaY, celdaZ;
        if (usarCeldaForzada)
        {
            celdaX = celdaForzada.x;
            celdaY = celdaForzada.y;
            celdaZ = celdaForzada.z;
        }
        else
        {
            // FloorToInt porque las celdas están indexadas por su esquina inferior. OJO: en
            // carteles clavados en el plano de una cara X+/Z+ esto cae en la celda vecina —
            // por eso los procedurales usan la celda forzada.
            // Se delega en MundoACelda cuando hay WorldGenerator para usar SU grilla (local a su
            // transform); si no, se cae al cálculo suelto con el tamañoCelda del propio cartel.
            if (WorldGenerator.Instance != null)
            {
                Vector3Int celda = WorldGenerator.Instance.MundoACelda(transform.position);
                celdaX = celda.x;
                celdaY = celda.y;
                celdaZ = celda.z;
            }
            else
            {
                celdaX = Mathf.FloorToInt(transform.position.x / tamañoCelda);
                celdaY = Mathf.FloorToInt(transform.position.y / tamañoCelda);
                celdaZ = Mathf.FloorToInt(transform.position.z / tamañoCelda);
            }
        }

        // 3. Dar formato a los valores
        string parteX = celdaX.ToString();
        if (usarLetraParaX)
        {
            // Convierte coordenadas a letras A, B, C... Z (y repite con módulo si supera 26)
            char letra = (char)('A' + Mathf.Abs(celdaX) % 26);
            parteX = letra.ToString();
        }

        string parteY = $"PISO {celdaY}";

        string parteZ = celdaZ.ToString("D2"); // Formato de 2 dígitos (ej: 05, 12)

        // 4. Actualizar el texto (Ejemplo: "4 / PISO -2 / 12")
        textoDisplay.text = $"{parteX} / {parteY} / {parteZ}";
    }
}
