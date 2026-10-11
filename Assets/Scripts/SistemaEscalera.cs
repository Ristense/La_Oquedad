using UnityEngine;
using DG.Tweening;

// Sistema simple de escalera en el jugador.
public class SistemaEscalera : MonoBehaviour
{
    [Header("Configuracion Escalada")]
    public float velocidadEscalada = 3f;
    public float distanciaParaSubirATope = 1.2f;
    [Tooltip("Sonido tipo CLANK que suena al engancharse a la escalera.")]
    public AudioClip sonidoEnganche;

    [Header("Estado")]
    public bool estaEnEscalera = false;
    public Escalera escaleraActual;

    private Rigidbody rb;
    private PlayerMovement playerMovement;
    private CapsuleCollider capsuleCollider;
    private RigidbodyInterpolation interpolationOriginal;
    private bool anclandoEntrada = false;
    private float inputVertical;
    private bool pedirSalidaArriba = false;
    private float trackX;
    private float trackZ;
    private AudioSource audioSource;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<PlayerMovement>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        if (rb != null)
        {
            interpolationOriginal = rb.interpolation;
        }
    }

    void Update()
    {
        if (!estaEnEscalera || escaleraActual == null)
        {
            return;
        }

        inputVertical = Input.GetAxisRaw("Vertical");

        if (Input.GetMouseButtonDown(1))
        {
            pedirSalidaArriba = true;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            SalirDeEscalera();

            if (playerMovement != null)
            {
                playerMovement.BloquearSpaceHastaSoltar();
            }

            if (rb != null)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, 0f), rb.linearVelocity.z);
            }

            return;
        }
    }

    void FixedUpdate()
    {
        if (!estaEnEscalera || escaleraActual == null || anclandoEntrada) return;

        Collider col = escaleraActual.GetComponent<Collider>();
        Vector3 referencia = escaleraActual.transform.position;

        // Calculamos los límites basados en el Collider de la escalera
        float minY = col != null ? col.bounds.min.y : referencia.y;
        float maxY = col != null ? col.bounds.max.y : referencia.y + 3f;

        float offsetAltura = ObtenerOffsetAltura();
        float minRootY = minY + offsetAltura;
        float maxRootY = maxY + offsetAltura;

        Vector3 pos = transform.position;

        // Mantiene al jugador pegado a la escalera en el track X/Z calculado al entrar
        pos.x = trackX;
        pos.z = trackZ;

        // Subir y bajar con W/S.
        pos.y += inputVertical * velocidadEscalada * Time.fixedDeltaTime;
        pos.y = Mathf.Clamp(pos.y, minRootY, maxRootY);

        if (playerMovement != null)
            playerMovement.movementLocked = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.MovePosition(pos);
        }

        // Subir al tope con clic derecho si ya está cerca.
        if (pedirSalidaArriba && (maxRootY - transform.position.y) <= distanciaParaSubirATope)
        {
            pedirSalidaArriba = false;
            
            // Dirección hacia el centro de la escalera para dar el paso final
            Vector3 dirHaciaEscalera = new Vector3(referencia.x - trackX, 0f, referencia.z - trackZ).normalized;

            // Pisar 0.8m hacia la escalera y subir 0.5m extra
            Vector3 destino = new Vector3(trackX, maxRootY + 0.5f, trackZ) + dirHaciaEscalera * 0.8f;

            transform.position = destino;
            SalirDeEscalera();
            return;
        }
    }

    public void EntrarEnEscalera(Escalera escalera, Vector3 puntoContacto)
    {
        if (escalera == null) return;
        if (estaEnEscalera && escaleraActual == escalera) return;

        // Si estabas en rapel, se suelta antes de engancharte.
        SistemaRapel sr = GetComponent<SistemaRapel>();
        if (sr != null && sr.estaEnRapel) sr.Desengancharse();

        escaleraActual = escalera;
        estaEnEscalera = true;
        anclandoEntrada = true;
        inputVertical = 0f;
        pedirSalidaArriba = false;

        // Sonido de enganche
        if (sonidoEnganche != null && audioSource != null)
        {
            audioSource.PlayOneShot(sonidoEnganche);
        }

        // Calcular la dirección horizontal desde el punto de contacto hacia el jugador
        Vector3 playerCenter = transform.position;
        if (capsuleCollider != null)
        {
            playerCenter = transform.TransformPoint(capsuleCollider.center);
        }
        
        Vector3 dirHorizontal = playerCenter - puntoContacto;
        dirHorizontal.y = 0f;
        
        if (dirHorizontal.sqrMagnitude < 0.0001f)
        {
            dirHorizontal = -transform.forward;
            dirHorizontal.y = 0f;
        }
        dirHorizontal.Normalize();

        float radioJugador = capsuleCollider != null ? capsuleCollider.radius : 0.4f;
        float offsetFuera = 0.05f; // Margen de 5cm para evitar clipping
        
        trackX = puntoContacto.x + dirHorizontal.x * (radioJugador + offsetFuera);
        trackZ = puntoContacto.z + dirHorizontal.z * (radioJugador + offsetFuera);

        Collider col = escalera.GetComponent<Collider>();
        Vector3 referenciaBase = escalera.transform.position;
        float yContacto = puntoContacto.y;
        float yBase = col != null ? col.bounds.min.y : referenciaBase.y;
        float yEntrada = Mathf.Max(yContacto, yBase) + ObtenerOffsetAltura(escalera);
        
        Vector3 destinoBase = new Vector3(trackX, yEntrada, trackZ);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        if (playerMovement != null)
            playerMovement.movementLocked = true;

        transform.DOKill();
        transform.DOMove(destinoBase, 0.12f)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                anclandoEntrada = false;
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }
            });
    }

    public void SalirDeEscalera()
    {
        estaEnEscalera = false;
        escaleraActual = null;
        anclandoEntrada = false;
        pedirSalidaArriba = false;

        transform.DOKill();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.interpolation = interpolationOriginal;
        }

        if (playerMovement != null)
            playerMovement.movementLocked = false;
    }

    private float ObtenerOffsetAltura()
    {
        return ObtenerOffsetAltura(escaleraActual);
    }

    private float ObtenerOffsetAltura(Escalera escalera)
    {
        if (capsuleCollider == null || escalera == null)
            return 0f;

        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float halfHeight = capsuleCollider.height * scaleY * 0.5f;
        float centerY = capsuleCollider.center.y * scaleY;
        return halfHeight - centerY;
    }
}
