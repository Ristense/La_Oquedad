using UnityEngine;

/// <summary>
/// Objeto que orbita alrededor del jugador a cierta distancia y, cada cierto tiempo
/// aleatorio, reproduce un sonido. Mientras suena, pausa su órbita y se queda quieto
/// en su posición. Al terminar el sonido, retoma la órbita. Ideal para crear una
/// "presencia" que el jugador puede intentar seguir por el sonido.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class OrbitadorSonoro : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("Jugador alrededor del cual orbita. Si está vacío, busca el tag 'Player'.")]
    public Transform jugador;

    [Header("Órbita")]
    [Tooltip("Distancia horizontal a la que orbita (debe ser grande para no alcanzarlo).")]
    public float radio = 20f;
    [Tooltip("Altura sobre el jugador.")]
    public float altura = 4f;
    [Tooltip("Velocidad angular de la órbita (grados por segundo). Negativo = sentido contrario.")]
    public float velocidadAngular = 25f;
    [Tooltip("Qué tan rápido alcanza su posición orbital objetivo (suaviza el seguimiento).")]
    public float velocidadSeguimiento = 1.5f;

    [Header("Sonido")]
    [Tooltip("Clips a reproducir; se elige uno al azar cada vez.")]
    public AudioClip[] sonidos;
    [Tooltip("Rango de tiempo (segundos) entre sonidos.")]
    public Vector2 intervaloSonido = new Vector2(12f, 30f);

    private AudioSource audioSource;
    private float angulo;
    private bool sonando;
    private float tiempoProximoSonido;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D, para que se pueda ubicar por el oído

        if (jugador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) jugador = p.transform;
        }

        // Ángulo inicial según la posición actual respecto al jugador
        if (jugador != null)
        {
            Vector3 d = transform.position - jugador.position;
            angulo = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
        }

        ProgramarProximoSonido();
    }

    private bool mundoEstabaListo;

    private void Update()
    {
        if (jugador == null) return;

        // ⚠️ Nada de sonar antes de que el jugador tome el control. Mismo criterio que
        // SistemaDesprendimientos y SustoVapor: el reloj no corre hasta que el mundo está listo, y
        // se REPROGRAMA en la transición — si no, el rato de menú/recorrido de cámara contaría como
        // espera y te comías el primer sonido en el segundo uno de partida.
        bool listo = WorldGenerator.Instance != null && WorldGenerator.Instance.MundoListo && !ModoMenu.Activo;
        if (listo != mundoEstabaListo)
        {
            mundoEstabaListo = listo;
            if (listo) ProgramarProximoSonido();
        }
        if (!listo) return;

        if (sonando)
        {
            // Mientras suena se queda quieto (mantiene su posición). Al terminar, retoma órbita.
            if (!audioSource.isPlaying)
            {
                sonando = false;
                ProgramarProximoSonido();
            }
            return;
        }

        // Avanzar la órbita
        angulo += velocidadAngular * Time.deltaTime;
        float rad = angulo * Mathf.Deg2Rad;
        Vector3 objetivo = jugador.position + new Vector3(Mathf.Cos(rad) * radio, altura, Mathf.Sin(rad) * radio);

        // Seguimiento suave (independiente del framerate): permite "alcanzar" al jugador si se movió
        float t = 1f - Mathf.Exp(-velocidadSeguimiento * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, objetivo, t);

        // ¿Toca sonar?
        if (Time.time >= tiempoProximoSonido)
            ReproducirSonido();
    }

    private void ReproducirSonido()
    {
        if (sonidos == null || sonidos.Length == 0) return;

        AudioClip clip = sonidos[Random.Range(0, sonidos.Length)];
        if (clip == null) return;

        audioSource.clip = clip;
        audioSource.Play();
        sonando = true; // pausa la órbita hasta que termine
    }

    private void ProgramarProximoSonido()
    {
        tiempoProximoSonido = Time.time + Random.Range(intervaloSonido.x, intervaloSonido.y);
    }
}
