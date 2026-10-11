using UnityEngine;

/// <summary>
/// Reproduce sonidos de pasos aleatorios cuando el jugador se desplaza sobre el suelo.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Rigidbody))]
public class PasosJugador : MonoBehaviour
{
    [Header("Sonidos de Pasos")]
    [Tooltip("Lista de clips de audio para los pasos. Se elegirá uno aleatorio en cada paso.")]
    public AudioClip[] clipsPasos;
    public AudioClip[] clipsCorrida; // Audio clips para corrida

    [Header("Ajustes")]
    [Tooltip("Tiempo en segundos entre cada paso.")]
    public float intervaloPasos = 0.5f; // paso normal
    public float intervaloCorrida = 0.35f; // paso más frecuente al sprint
    [Range(0f, 1f)]
    public float volumenPasos = 0.5f;

    private Rigidbody rb;
    private PlayerMovement playerMovement;
    private AudioSource audioSource;
    private float temporizadorPasos = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<PlayerMovement>();
        audioSource = GetComponent<AudioSource>();

        audioSource.loop = false;
        audioSource.playOnAwake = false;
    }

    void Update()
    {
        if (playerMovement == null || clipsPasos == null || clipsPasos.Length == 0) return;

        // Medir velocidad en el plano horizontal (ignorar caídas/saltos en Y)
        Vector3 velocidadHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        bool seEstaMoviendo = velocidadHorizontal.magnitude > 0.2f;

        // Solo dar pasos si está en el suelo y moviéndose
        if (playerMovement.EnSuelo && seEstaMoviendo)
        {
            temporizadorPasos += Time.deltaTime;

            float intervaloActual = playerMovement.IsSprinting ? intervaloCorrida : intervaloPasos;
            if (temporizadorPasos >= intervaloActual)
            {
                ReproducirPasoAleatorio();
                temporizadorPasos = 0f;
            }
        }
        else
        {
            // Resetear el temporizador para que el primer paso suene de inmediato al empezar a caminar
            temporizadorPasos = intervaloPasos;
        }
    }

    private void ReproducirPasoAleatorio()
    {
        AudioClip[] sourceArray = playerMovement.IsSprinting && clipsCorrida != null && clipsCorrida.Length > 0 ? clipsCorrida : clipsPasos;
        if (sourceArray == null || sourceArray.Length == 0) return;
        AudioClip paso = sourceArray[Random.Range(0, sourceArray.Length)];
        if (paso != null)
        {
            audioSource.PlayOneShot(paso, volumenPasos);
        }
    }
}
