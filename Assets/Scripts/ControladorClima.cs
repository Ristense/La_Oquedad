using UnityEngine;

/// <summary>
/// Controla la niebla nativa de Unity de forma dinámica según el clima de PlayerData.
/// </summary>
public class ControladorClima : MonoBehaviour
{
    [Header("Referencias")]
    public PlayerData playerData;

    [Header("Configuración de Niebla por Clima")]
    [Header("1. Despejado")]
    public Color colorDespejado = new Color(0.1f, 0.1f, 0.15f);
    public float densidadDespejado = 0.01f;

    [Header("2. Neblina")]
    public Color colorNeblina = new Color(0.4f, 0.4f, 0.45f);
    public float densidadNeblina = 0.06f;

    [Header("3. Tormenta de Aceite")]
    public Color colorTormenta = new Color(0.08f, 0.07f, 0.05f);
    public float densidadTormenta = 0.12f;

    [Header("Transición")]
    [Tooltip("Velocidad de cambio entre climas")]
    public float velocidadTransicion = 0.5f;

    private Color colorObjetivo;
    private float densidadObjetiva;

    void Start()
    {
        // Activar la niebla nativa de Unity
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        if (playerData == null)
        {
            playerData = FindAnyObjectByType<PlayerData>();
        }

        // Inicializar con valores actuales
        ActualizarValoresObjetivo(true);
    }

    void Update()
    {
        if (playerData == null) return;

        // Actualizar objetivos según el clima de PlayerData
        ActualizarValoresObjetivo(false);

        // Transición suave de color y densidad de niebla nativa
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, colorObjetivo, Time.deltaTime * velocidadTransicion);
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, densidadObjetiva, Time.deltaTime * velocidadTransicion);

        // Si la cámara principal usa Skybox, la niebla tapará el Skybox si la densidad es suficiente.
        // Opcional: Si la niebla es muy densa, es buena práctica que la cámara limpie a Color Sólido (coincidiendo con la niebla) para ahorrar rendimiento.
    }

    private void ActualizarValoresObjetivo(bool instantaneo)
    {
        switch (playerData.climaActual)
        {
            case "Neblina":
                colorObjetivo = colorNeblina;
                densidadObjetiva = densidadNeblina;
                break;

            case "Tormenta de Aceite":
                colorObjetivo = colorTormenta;
                densidadObjetiva = densidadTormenta;
                break;

            case "Despejado":
            default:
                colorObjetivo = colorDespejado;
                densidadObjetiva = densidadDespejado;
                break;
        }

        if (instantaneo)
        {
            RenderSettings.fogColor = colorObjetivo;
            RenderSettings.fogDensity = densidadObjetiva;
        }
    }
}
