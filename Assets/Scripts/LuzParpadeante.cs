using UnityEngine;

/// <summary>
/// Hace parpadear una luz en Unity (compatible con HDRP/URP).
/// Permite sincronizar el parpadeo de la luz física con el brillo (emisión) del material
/// de la bombilla de forma eficiente usando MaterialPropertyBlocks.
/// </summary>
[RequireComponent(typeof(Light))]
public class LuzParpadeante : MonoBehaviour
{
    private Light miLuz;
    private MaterialPropertyBlock propertyBlock;
    private int emissionColorId;

    [Header("Configuración de Intensidad")]
    [Tooltip("Intensidad máxima de la luz (se autodetecta al iniciar si se deja en 1).")]
    public float intensidadMax = 1f;
    [Tooltip("Intensidad mínima al parpadear (típicamente 0).")]
    public float intensidadMin = 0f;

    [Header("Estilo de Parpadeo")]
    [Tooltip("JITTER: Inestabilidad constante. BURSTS: Encendida con ráfagas rápidas de apagado (Terror). BROKEN: Apagada con destellos cortos.")]
    public TipoParpadeo tipo = TipoParpadeo.Bursts;

    [Tooltip("Duración mínima del estado actual (en segundos).")]
    [Range(0.01f, 1f)]
    public float velocidadMin = 0.05f;

    [Tooltip("Duración máxima del estado actual (en segundos).")]
    [Range(0.01f, 2f)]
    public float velocidadMax = 0.2f;

    [Header("Sincronización de Emisión (Opcional)")]
    [Tooltip("El MeshRenderer de la bombilla física para sincronizar su material de emisión con la luz.")]
    public Renderer bombillaRenderer;
    [Tooltip("Nombre de la propiedad de emisión del shader. HDRP usa '_EmissiveColor'. URP/Standard usa '_EmissionColor'.")]
    public string propiedadEmision = "_EmissiveColor";
    
    private Color colorEmisionBase;
    private float proximaActualizacion = 0f;
    private bool estaEncendida = true;

    public enum TipoParpadeo
    {
        Jitter,      // Fluctuación rápida y ruidosa constante
        Bursts,      // Mayormente encendida, con apagones rápidos de terror
        Broken       // Mayormente apagada, con fogonazos cortos de luz
    }

    void Awake()
    {
        miLuz = GetComponent<Light>();
        
        // Si no se configuró una intensidad máxima manual, tomar la del componente Light
        if (miLuz != null && intensidadMax == 1f)
        {
            intensidadMax = miLuz.intensity;
        }

        // Configurar sincronización del material emissive
        if (bombillaRenderer != null)
        {
            propertyBlock = new MaterialPropertyBlock();
            emissionColorId = Shader.PropertyToID(propiedadEmision);
            
            if (bombillaRenderer.sharedMaterial != null)
            {
                // Intentar leer el color de emisión base
                if (bombillaRenderer.sharedMaterial.HasProperty(emissionColorId))
                {
                    colorEmisionBase = bombillaRenderer.sharedMaterial.GetColor(emissionColorId);
                }
                else
                {
                    // Fallback a blanco si el material no tiene esa propiedad
                    colorEmisionBase = Color.white;
                }
            }
        }
    }

    void Update()
    {
        if (Time.time < proximaActualizacion) return;

        float delay = Random.Range(velocidadMin, velocidadMax);
        float nuevaIntensidad = intensidadMax;

        switch (tipo)
        {
            case TipoParpadeo.Jitter:
                nuevaIntensidad = Random.Range(intensidadMin, intensidadMax);
                break;

            case TipoParpadeo.Bursts:
                if (estaEncendida)
                {
                    // 10% de probabilidad de iniciar una ráfaga de parpadeo (apagones rápidos)
                    if (Random.value > 0.90f)
                    {
                        estaEncendida = false;
                        nuevaIntensidad = intensidadMin;
                        delay = Random.Range(0.02f, 0.1f); // parpadeos muy rápidos
                    }
                    else
                    {
                        nuevaIntensidad = intensidadMax;
                        delay = Random.Range(0.2f, 2.0f); // tiempo largo encendida
                    }
                }
                else
                {
                    // Durante la ráfaga, decidir si vuelve a encenderse o sigue fallando
                    estaEncendida = Random.value > 0.4f;
                    nuevaIntensidad = estaEncendida ? intensidadMax : intensidadMin;
                    delay = Random.Range(velocidadMin, velocidadMax);
                }
                break;

            case TipoParpadeo.Broken:
                if (!estaEncendida)
                {
                    // 5% de probabilidad de dar un chispazo de luz
                    if (Random.value > 0.95f)
                    {
                        estaEncendida = true;
                        nuevaIntensidad = intensidadMax;
                        delay = Random.Range(0.05f, 0.15f); // chispazo rápido
                    }
                    else
                    {
                        nuevaIntensidad = intensidadMin;
                        delay = Random.Range(0.3f, 3.0f); // apagada por largo rato
                    }
                }
                else
                {
                    estaEncendida = false;
                    nuevaIntensidad = intensidadMin;
                    delay = Random.Range(velocidadMin, velocidadMax);
                }
                break;
        }

        // Aplicar intensidad a la luz
        miLuz.intensity = nuevaIntensidad;

        // Sincronizar el brillo del material de la bombilla
        ActualizarEmisionMaterial(nuevaIntensidad);

        proximaActualizacion = Time.time + delay;
    }

    private void ActualizarEmisionMaterial(float intensidad)
    {
        if (bombillaRenderer == null || propertyBlock == null) return;

        // Calcular el factor de atenuación según la intensidad actual
        float factor = (intensidadMax > 0f) ? (intensidad / intensidadMax) : 0f;
        Color colorActual = colorEmisionBase * factor;

        // Aplicar el color de emisión atenuado al Renderer de forma eficiente
        bombillaRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(emissionColorId, colorActual);
        bombillaRenderer.SetPropertyBlock(propertyBlock);
    }
}
