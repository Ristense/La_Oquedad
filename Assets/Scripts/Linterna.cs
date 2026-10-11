using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla la linterna del jugador con UNA sola tecla (Q por defecto), distinguiendo dos gestos:
///   - TAP (apretar y soltar rápido, sin scrollear) → prende/apaga.
///   - MANTENER + rueda del mouse → ajusta la intensidad de la luz (no togglea).
/// El toggle se decide recién al SOLTAR la tecla, y se cancela si hubo scroll durante el hold —
/// así mantener Q para ajustar intensidad no prende/apaga la linterna de paso.
/// </summary>
public class Linterna : MonoBehaviour
{
    [Tooltip("GameObject de la linterna (el que tiene el componente Light). Si se deja vacío, usa este mismo GameObject.")]
    public GameObject linternaObjeto;
    [Tooltip("Tecla para prender/apagar (tap) y ajustar intensidad (mantener + rueda).")]
    public KeyCode tecla = KeyCode.F;
    [Tooltip("¿Arranca encendida al empezar la partida?")]
    public bool encendidaAlInicio = false;

    [Header("Ajuste de Intensidad (mantener tecla + rueda del mouse)")]
    [Tooltip("Intensidad mínima de la luz (en las unidades de tu Light: Lux/Lumen en HDRP, o el valor plano en built-in).")]
    public float intensidadMin = 100f;
    [Tooltip("Intensidad máxima de la luz.")]
    public float intensidadMax = 2000f;
    [Tooltip("Paso de ajuste por cada click de scroll (ej: 0.02 = 2% del rango total por click, toma 50 clicks ir de 0% a 100%).")]
    public float pasoAjusteScroll = 0.02f;
    [Tooltip("Slider opcional de UI para visualizar el nivel de intensidad regulado de la linterna.")]
    public Slider sliderIntensidad;

    [System.Serializable]
    public class EnfoqueLinterna
    {
        [Tooltip("Solo para identificarlo en el inspector.")]
        public string nombre = "Enfoque";
        [Tooltip("Apertura del cono en grados. Más chico = haz angosto que llega más lejos.")]
        [Range(1f, 179f)] public float anguloCono = 45f;
        [Tooltip("Alcance de la luz en metros.")]
        public float alcance = 25f;
    }

    [Header("Enfoque (click de la rueda del mouse)")]
    [Tooltip("Pasos de enfoque que se van ciclando con cada click de rueda. Al llegar al último vuelve al primero.")]
    public EnfoqueLinterna[] enfoques =
    {
        new EnfoqueLinterna { nombre = "Cerca", anguloCono = 80f, alcance = 12f },
        new EnfoqueLinterna { nombre = "Medio", anguloCono = 45f, alcance = 30f },
        new EnfoqueLinterna { nombre = "Lejos", anguloCono = 20f, alcance = 70f },
    };
    [Tooltip("OBSOLETO mientras 'Enfoque Fijo' esté en ON. Con qué enfoque arrancaba la partida cuando el ciclado estaba activo.")]
    public int enfoqueInicial = 0;
    [Tooltip("ON = la linterna se queda SIEMPRE en un solo enfoque y el click de rueda no hace nada. Es como quedó decidido: el paso 'Medio' es el que se ve bien, los otros dos no aportaban.\n\nOFF = vuelve al ciclado de los 3 pasos con el click de rueda (hay que descomentar el bloque de Update también, ver ahí).")]
    public bool enfoqueFijo = true;
    [Tooltip("Índice del enfoque único cuando 'Enfoque Fijo' está en ON. 1 = el segundo del array ('Medio').\n\n⚠️ Es un campo NUEVO a propósito: reusar 'Enfoque Inicial' no habría servido porque la escena ya lo tiene serializado en 0, y Unity ignora el default del script cuando el campo ya existe guardado.")]
    public int indiceEnfoqueFijo = 1;

    [Header("Sonido (opcional)")]
    public AudioSource fuenteSonido;
    public AudioClip sonidoEncender;
    public AudioClip sonidoApagar;
    [Tooltip("Click al cambiar de enfoque (como el aro de una linterna real).")]
    public AudioClip sonidoEnfoque;

    public bool Encendida { get; private set; }
    /// <summary>True mientras se mantiene la tecla Y se está ajustando la intensidad (para que la UI del slider se muestre).</summary>
    public bool AjustandoIntensidad { get; private set; }
    public float IntensidadActual => GetIntensity();
    /// <summary>Índice del enfoque activo (0 = el primero del array).</summary>
    public int EnfoqueActual { get; private set; }
    /// <summary>Nombre del enfoque activo, para mostrarlo en el HUD si hace falta.</summary>
    public string NombreEnfoqueActual =>
        (enfoques != null && EnfoqueActual >= 0 && EnfoqueActual < enfoques.Length)
            ? enfoques[EnfoqueActual].nombre
            : "";

    private Light luz;
    private bool huboScrollEnEsteHold;
    private MonoBehaviour hdLightData;
    private System.Reflection.PropertyInfo hdIntensityProp;

    void Awake()
    {
        if (linternaObjeto == null) linternaObjeto = gameObject;
        luz = linternaObjeto.GetComponent<Light>();
        
        // Detectar de forma segura si usamos HDRP para mapear los lúmenes reales
        var hdLightType = System.Type.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalLightData, Unity.RenderPipelines.HighDefinition.Runtime");
        if (hdLightType != null)
        {
            hdLightData = linternaObjeto.GetComponent(hdLightType) as MonoBehaviour;
            if (hdLightData != null)
            {
                hdIntensityProp = hdLightType.GetProperty("intensity");
            }
        }

        Encendida = encendidaAlInicio;
        AplicarEstado();
        ActualizarSlider();

        if (enfoques != null && enfoques.Length > 0)
        {
            EnfoqueActual = Mathf.Clamp(enfoqueFijo ? indiceEnfoqueFijo : enfoqueInicial, 0, enfoques.Length - 1);
            AplicarEnfoque();
        }
    }

    private float GetIntensity()
    {
        if (hdLightData != null && hdIntensityProp != null)
        {
            return (float)hdIntensityProp.GetValue(hdLightData);
        }
        return luz != null ? luz.intensity : 0f;
    }

    private void SetIntensity(float val)
    {
        if (hdLightData != null && hdIntensityProp != null)
        {
            hdIntensityProp.SetValue(hdLightData, val);
        }
        else if (luz != null)
        {
            luz.intensity = val;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(tecla))
            huboScrollEnEsteHold = false; // reinicia el gesto al empezar a apretar

        if (Input.GetKey(tecla))
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                huboScrollEnEsteHold = true;
                AjustandoIntensidad = true;
                AjustarIntensidad(scroll);
            }
        }

        if (Input.GetKeyUp(tecla))
        {
            AjustandoIntensidad = false;
            if (!huboScrollEnEsteHold) Alternar(); // tap limpio (sin scroll) = toggle
        }

        // Click de la rueda = siguiente enfoque (cerca → medio → lejos → cerca...).
        // No choca con el ajuste de intensidad: eso es GIRAR la rueda, esto es APRETARLA.
        //
        // ⚠️ DESACTIVADO (2026-09-09): de los 3 pasos, solo 'Medio' se veía bien — 'Cerca' era un
        // charco de luz sin alcance y 'Lejos' un puntero que no iluminaba el entorno. Ciclar entre
        // ellos era una decisión sin buena respuesta, así que la linterna se queda fija en Medio.
        // El array de enfoques y SiguienteEnfoque() quedan por si se retoma.
        //
        // Para volver al ciclado: descomentar esto Y destildar 'Enfoque Fijo' en el inspector.
        //
        // if (Input.GetMouseButtonDown(2))
        // {
        //     SiguienteEnfoque();
        // }
    }

    /// <summary>
    /// Cicla al siguiente paso de enfoque; después del último vuelve al primero.
    /// Hoy NADIE la llama desde Update (ver el bloque comentado ahí), pero sigue siendo pública
    /// por si se quiere disparar desde un botón o al retomar el ciclado. Respeta 'Enfoque Fijo'.
    /// </summary>
    public void SiguienteEnfoque()
    {
        if (enfoqueFijo) return;
        if (enfoques == null || enfoques.Length == 0) return;

        EnfoqueActual = (EnfoqueActual + 1) % enfoques.Length;
        AplicarEnfoque();

        if (fuenteSonido != null && sonidoEnfoque != null)
            fuenteSonido.PlayOneShot(sonidoEnfoque);
    }

    /// <summary>
    /// Aplica ángulo y alcance del enfoque activo. spotAngle/range viven en el componente Light
    /// base, así que esto funciona igual en HDRP que en built-in (a diferencia de la intensidad,
    /// que en HDRP hay que tocarla por HDAdditionalLightData para respetar los lúmenes).
    /// </summary>
    private void AplicarEnfoque()
    {
        if (luz == null || enfoques == null || enfoques.Length == 0) return;

        EnfoqueLinterna e = enfoques[Mathf.Clamp(EnfoqueActual, 0, enfoques.Length - 1)];
        if (e == null) return;

        luz.spotAngle = e.anguloCono;
        luz.range = e.alcance;
    }

    private void AjustarIntensidad(float scroll)
    {
        if (luz == null) return;

        int pasos = Mathf.RoundToInt(scroll / 0.1f);
        if (pasos == 0) pasos = scroll > 0 ? 1 : -1;

        float rango = intensidadMax - intensidadMin;
        if (rango <= 0f) return;

        // Calcular el porcentaje lineal actual de la intensidad
        float pct = Mathf.Clamp01((GetIntensity() - intensidadMin) / rango);

        // Sumar o restar el paso fijo por cada "paso" del scroll
        pct = Mathf.Clamp01(pct + (pasos * pasoAjusteScroll));

        // Aplicar la nueva intensidad de forma lineal
        float nueva = intensidadMin + (pct * rango);
        SetIntensity(nueva);
        ActualizarSlider();
    }

    private void ActualizarSlider()
    {
        if (sliderIntensidad != null)
        {
            float rango = intensidadMax - intensidadMin;
            sliderIntensidad.value = rango > 0f ? Mathf.Clamp01((GetIntensity() - intensidadMin) / rango) : 0f;
        }
    }

    public void Alternar()
    {
        Encendida = !Encendida;
        AplicarEstado();

        if (fuenteSonido != null)
        {
            AudioClip clip = Encendida ? sonidoEncender : sonidoApagar;
            if (clip != null) fuenteSonido.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// Apaga/prende SOLO la luz (Light.enabled), nunca el GameObject completo — si linternaObjeto
    /// fuera el mismo objeto que tiene este script, un SetActive(false) se apagaría a sí mismo y
    /// Q dejaría de responder para siempre. Fallback a SetActive solo si no hay componente Light
    /// (ej. el objeto es un prefab con mesh/partículas propias en vez de una luz).
    /// </summary>
    private void AplicarEstado()
    {
        if (luz != null) luz.enabled = Encendida;
        else linternaObjeto.SetActive(Encendida);

        if (sliderIntensidad != null)
        {
            sliderIntensidad.gameObject.SetActive(Encendida);
        }
    }
}
