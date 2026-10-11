using UnityEngine;

public class InventoryItem : MonoBehaviour
{
    public ItemData data;
    [Tooltip("Pasos de rotación de 90° (0..3).")]
    [HideInInspector] public int rotacion = 0;
    [HideInInspector] public int gridX, gridY;

    // Escala mundial "natural" del objeto, capturada antes de cualquier reparent.
    [HideInInspector] public Vector3 escalaMundialOriginal = Vector3.one;

    [Header("Ajuste visual en mochila")]
    [Tooltip("Correccion local en celdas al pegar este prefab en la mochila. Usalo para compensar pivotes/bounds del modelo sin mover la grilla base.")]
    public Vector2 offsetVisualCeldas = Vector2.zero;

    [Tooltip("Porciones de comida/agua restantes en este objeto (si es fraccionable). -1 significa que no se ha inicializado.")]
    [HideInInspector] public int porcionesRestantes = -1;

    public float ObtenerPesoActual()
    {
        if (data != null)
        {
            if (data is ConsumibleData consumible && consumible.esFraccionable)
            {
                int maxPorciones = Mathf.Max(1, consumible.porcionesMaximas);
                if (porcionesRestantes == -1) porcionesRestantes = maxPorciones;
                return data.peso * ((float)porcionesRestantes / maxPorciones);
            }
            return data.peso;
        }
        return 0f;
    }

    public bool isRotated => (rotacion % 2) != 0;
    public int Width => isRotated ? data.height : data.width;
    public int Height => isRotated ? data.width : data.height;

    public void Rotate()
    {
        rotacion = (rotacion + 1) % 4;
    }

    void Awake()
    {
        escalaMundialOriginal = transform.lossyScale;
    }

    void Start()
    {
        // Hacer que este objeto no choque físicamente con el jugador
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Collider myCollider = GetComponent<Collider>();
            Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
            foreach (Collider pc in playerColliders)
            {
                Physics.IgnoreCollision(myCollider, pc);
            }
        }
    }

}
