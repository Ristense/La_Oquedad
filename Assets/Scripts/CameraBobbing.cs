using UnityEngine;

/// <summary>
/// Añade un efecto de movimiento/balanceo de cabeza (Head Bobbing) y micro-vibraciones (Camera Shake)
/// a la cámara en primera persona, adaptándose si caminas, corres o escalas.
/// </summary>
public class CameraBobbing : MonoBehaviour
{
    [Header("Configuración del Bobbing (Caminar)")]
    public float frecuenciaCaminar = 14f;
    public float amplitudCaminarY = 0.05f;
    public float amplitudCaminarX = 0.03f;
    [Tooltip("Intensidad de la micro-vibración aleatoria al caminar.")]
    public float vibracionCaminar = 0.005f;

    [Header("Configuración del Bobbing (Correr)")]
    public float frecuenciaCorrer = 18f;
    public float amplitudCorrerY = 0.1f;
    public float amplitudCorrerX = 0.06f;
    [Tooltip("Intensidad de la micro-vibración aleatoria al correr.")]
    public float vibracionCorrer = 0.015f;

    [Header("Configuración del Bobbing (Escalar)")]
    public float frecuenciaEscalar = 10f;
    public float amplitudEscalarY = 0.04f;
    public float amplitudEscalarX = 0.02f;
    [Tooltip("Intensidad de la micro-vibración aleatoria al escalar.")]
    public float vibracionEscalar = 0.008f;

    [Header("Suavizado y Retorno")]
    [Tooltip("Velocidad con la que la cámara regresa al centro al detenerse.")]
    public float velocidadRetorno = 8f;

    private Vector3 posicionLocalInicial;
    private float temporizador = 0f;

    // Referencias al jugador
    private Rigidbody playerRb;
    private PlayerMovement playerMovement;
    private SistemaEscalera playerEscalera;
    private SistemaRapel playerRapel;

    void Start()
    {
        // IMPORTANTE: Guardamos la posición LOCAL. Si usamos la posición global (transform.position),
        // la cámara se quedaría congelada en el punto de spawn del mapa al movernos.
        posicionLocalInicial = transform.localPosition;

        // Buscamos referencias en el objeto padre (Cuerpo del jugador)
        Transform parent = transform.parent;
        if (parent != null)
        {
            playerRb = parent.GetComponent<Rigidbody>();
            playerMovement = parent.GetComponent<PlayerMovement>();
            playerEscalera = parent.GetComponent<SistemaEscalera>();
            playerRapel = parent.GetComponent<SistemaRapel>();
        }
    }

    void Update()
    {
        if (playerRb == null || playerMovement == null) return;

        float freq = 0f;
        float ampX = 0f;
        float ampY = 0f;
        float vibracionActual = 0f;
        bool seEstaMoviendo = false;

        // 1. Determinar el estado del jugador (Escalando vs Moviéndose en el suelo)
        bool enEscalera = playerEscalera != null && playerEscalera.estaEnEscalera;
        bool enRapel = playerRapel != null && playerRapel.estaEnRapel;

        if (enEscalera || enRapel)
        {
            // Si está subiendo o bajando por escalera o cuerda (W o S presionado)
            float inputVertical = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(inputVertical) > 0.1f)
            {
                seEstaMoviendo = true;
                freq = frecuenciaEscalar;
                ampX = amplitudEscalarX;
                ampY = amplitudEscalarY;
                vibracionActual = vibracionEscalar;
            }
        }
        else
        {
            // Movimiento normal en el suelo (obtenemos velocidad horizontal del Rigidbody)
            Vector3 velHorizontal = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
            float velocidadHorizontal = velHorizontal.magnitude;

            if (velocidadHorizontal > 0.1f && playerMovement.EnSuelo)
            {
                seEstaMoviendo = true;
                if (playerMovement.IsSprinting)
                {
                    freq = frecuenciaCorrer;
                    ampX = amplitudCorrerX;
                    ampY = amplitudCorrerY;
                    vibracionActual = vibracionCorrer;
                }
                else
                {
                    freq = frecuenciaCaminar;
                    ampX = amplitudCaminarX;
                    ampY = amplitudCaminarY;
                    vibracionActual = vibracionCaminar;
                }
            }
        }

        // 2. Calcular los offsets de posición del balanceo (Bobbing) y vibración (Shake)
        float offsetX = 0f;
        float offsetY = 0f;
        Vector3 ruidoAleatorio = Vector3.zero;

        if (seEstaMoviendo)
        {
            // Avanzamos el temporizador del balanceo
            temporizador += Time.deltaTime * freq;
            
            offsetY = Mathf.Sin(temporizador) * ampY;
            offsetX = Mathf.Cos(temporizador / 2f) * ampX;

            // Micro-vibraciones aleatorias en 3D multiplicadas por la intensidad del estado actual
            ruidoAleatorio = Random.insideUnitSphere * vibracionActual;
        }
        else
        {
            // Si se detiene, el temporizador vuelve a 0 suavemente
            temporizador = Mathf.MoveTowards(temporizador, 0f, Time.deltaTime * velocidadRetorno);
        }

        // 3. Aplicar de forma suave los offsets a la posición local de la cámara (Lerp)
        Vector3 posicionDestino = posicionLocalInicial + new Vector3(offsetX, offsetY, 0f) + ruidoAleatorio;
        transform.localPosition = Vector3.Lerp(transform.localPosition, posicionDestino, Time.deltaTime * velocidadRetorno);
    }
}
