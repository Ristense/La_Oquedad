// PlayerMovement con sprint y aceleración
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    // --- Configuración de movimiento ---
    [Header("Velocidades")]
    public float velocidadCaminar = 5f;          // velocidad base
    public float velocidadSprintObjetivo = 9f;    // velocidad máxima al sprint
    public float aceleracionSprint = 6f;          // aceleración cuando sprintas
    public float desaceleracion = 6f;            // desaceleración al dejar de sprintar

    // --- Salto ---
    [Header("Salto")]
    public float fuerzaSaltoBase = 6f;
    [Tooltip("Cuánto disminuye la fuerza del salto por cada kg de carga.")]
    public float factorPesoSalto = 0.1f;
    public float fuerzaSaltoMinima = 1.5f;
    public float distanciaDeteccionSuelo = 1.1f;
    public LayerMask capasSuelo = ~0;

    // --- Vuelo (Space para volar) ---
    [Header("Vuelo")]
    [Tooltip("ON = Space te hace volar (debug). OFF = Space salta normal (fuerzaSaltoBase, frenado por el peso). Se alterna en juego con teclaModoVuelo.")]
    public bool modoVuelo = true;
    public KeyCode teclaModoVuelo = KeyCode.F8;
    public float velocidadVueloAscenso = 8f;    // velocidad de ascenso mientras mantienes Space
    public float velocidadVueloCaida = -15f;   // velocidad de caída cuando sueltas Space (gravedad)
    [Tooltip("Multiplicador de velocidad (tanto horizontal como de ascenso) cuando presionas Shift en el aire/volando.")]
    public float multiplicadorVelocidadSprintVuelo = 2.5f;

    // --- Agacharse ---
    [Header("Agacharse")]
    public KeyCode teclaAgacharse = KeyCode.LeftControl;
    [Tooltip("Empty 'Cabeza' (donde se engancha la cámara). Si se deja vacío, se busca un hijo llamado 'Cabeza'.")]
    public Transform cabeza;
    [Tooltip("Cuánto BAJA la cabeza al agacharse (metros).")]
    public float descensoCabeza = 0.8f;
    [Tooltip("Velocidad al moverse agachado.")]
    public float velocidadAgachado = 2.5f;
    [Tooltip("Qué tan rápido baja/sube la cabeza. Más alto = más seco.")]
    public float velocidadTransicionAgachado = 10f;
    [Tooltip("Proporción de la altura original del collider al estar agachado (0.55 = poco más de la mitad).")]
    [Range(0.2f, 1f)]
    public float factorAlturaColliderAgachado = 0.55f;

    // --- Daño por caída ---
    [Header("Daño por Caída")]
    [Tooltip("Si está activo, el jugador muere al caer más de distanciaCaidaLetal metros.")]
    public bool danoCaidaActivo = true;
    [Tooltip("Distancia (metros) que el jugador puede caer antes de morir al aterrizar. Se mide desde el punto más alto alcanzado mientras estaba en el aire.")]
    public float distanciaCaidaLetal = 25f;
    [Tooltip("Si está activo, log de diagnóstico de cada aterrizaje con la distancia de caída.")]
    public bool logCaidas = false;

    // --- Referencias internas ---
    private Rigidbody rb;
    private PlayerData playerData;
    private bool enSuelo = false;
    private bool intentarSaltar = false;
    private float velocidadActual = 0f;
    private bool isSprinting = false;
    private bool bloquearSpaceHastaSoltar = false;
    // Cuando true, bloquea la lógica de movimiento (usado por escalada/rapel)
    public bool movementLocked = false;

    // Track de la altura máxima alcanzada en aire (para calcular distancia de caída al aterrizar)
    private float alturaMaximaEnAire;
    private bool enSueloFrameAnterior = true;

    // --- Estado de agacharse ---
    private bool agachado = false;
    private CapsuleCollider capsula;
    private float alturaCapsulaOriginal;
    private Vector3 centroCapsulaOriginal;
    private float cabezaYOriginal;

// Exponer al exterior (para audio de sprint y detección de suelo)
    public bool IsSprinting => isSprinting;
    public bool EnSuelo => enSuelo;
    public bool Agachado => agachado;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        playerData = GetComponent<PlayerData>();
        // Inicializar la altura máxima a la altura actual para que la primera "caída" hasta tocar piso
        // en el spawn (mientras la burbuja carga el suelo) no se cuente como caída letal.
        alturaMaximaEnAire = transform.position.y;

        capsula = GetComponent<CapsuleCollider>();
        if (capsula != null)
        {
            alturaCapsulaOriginal = capsula.height;
            centroCapsulaOriginal = capsula.center;
        }

        if (cabeza == null) cabeza = transform.Find("Cabeza");
        if (cabeza != null) cabezaYOriginal = cabeza.localPosition.y;
    }

    /// <summary>
    /// Lee la tecla de agacharse y mueve la cabeza / achica la cápsula. Va en Update (no en
    /// FixedUpdate) para que el descenso de la cámara se vea suave a los FPS reales.
    /// No deja PARARSE si hay techo justo encima: si no, te incrustarías en la geometría.
    /// </summary>
    private void ActualizarAgacharse()
    {
        bool bloqueado = movementLocked || ModoMenu.Activo;
        bool quiere = !bloqueado && Input.GetKey(teclaAgacharse);

        if (!quiere && agachado && HayTechoParaPararse())
        {
            quiere = true; // hay algo encima: seguimos agachados aunque sueltes la tecla
        }

        agachado = quiere;

        if (capsula != null)
        {
            float alturaDestino = agachado ? alturaCapsulaOriginal * factorAlturaColliderAgachado : alturaCapsulaOriginal;
            float nuevaAltura = Mathf.MoveTowards(capsula.height, alturaDestino, velocidadTransicionAgachado * Time.deltaTime);

            // El pie de la cápsula queda clavado donde estaba: si solo cambiáramos la altura,
            // el collider se encogería hacia el centro y el jugador "flotaría" o se hundiría.
            float pie = centroCapsulaOriginal.y - alturaCapsulaOriginal * 0.5f;
            capsula.height = nuevaAltura;
            capsula.center = new Vector3(centroCapsulaOriginal.x, pie + nuevaAltura * 0.5f, centroCapsulaOriginal.z);
        }

        if (cabeza != null)
        {
            float yDestino = agachado ? cabezaYOriginal - descensoCabeza : cabezaYOriginal;
            Vector3 lp = cabeza.localPosition;
            lp.y = Mathf.MoveTowards(lp.y, yDestino, velocidadTransicionAgachado * Time.deltaTime);
            cabeza.localPosition = lp;
        }
    }

    /// <summary>True si hay geometría encima que impide volver a pararse.</summary>
    private bool HayTechoParaPararse()
    {
        if (capsula == null) return false;

        float faltante = alturaCapsulaOriginal - capsula.height;
        if (faltante <= 0.01f) return false;

        float radio = Mathf.Max(0.05f, capsula.radius * 0.9f);
        Vector3 origen = transform.position + Vector3.up * (capsula.center.y + capsula.height * 0.5f - radio);
        return Physics.SphereCast(origen, radio, Vector3.up, out _, faltante + 0.05f, capasSuelo, QueryTriggerInteraction.Ignore);
    }

    void Update()
    {
        ActualizarAgacharse();

        // El vuelo se maneja en FixedUpdate con Input.GetKey(Space)
        // Solo aquí detectamos si intentamos saltar (si Space está presionado pero NO en vuelo)

        // Tecla rápida para desarrollador: presionar F10 para alternar el daño por caída (muerte)
        if (Input.GetKeyDown(KeyCode.F10))
        {
            danoCaidaActivo = !danoCaidaActivo;
            Debug.Log($"[DEBUG - CHEAT] Daño por caída (Muerte) {(danoCaidaActivo ? "ACTIVADO" : "DESACTIVADO")}");
        }

        if (Input.GetKeyDown(teclaModoVuelo))
        {
            modoVuelo = !modoVuelo;
            Debug.Log($"[DEBUG - CHEAT] Modo vuelo {(modoVuelo ? "ACTIVADO" : "DESACTIVADO (Space = salto)")}");
        }

        // GetKeyDown se lee acá y no en FixedUpdate: a FPS altos FixedUpdate puede no correr en
        // el frame exacto del KeyDown y el salto se perdería.
        if (!modoVuelo && !bloquearSpaceHastaSoltar && !movementLocked && !ModoMenu.Activo && Input.GetKeyDown(KeyCode.Space))
        {
            intentarSaltar = true;
        }
    }

    /// <summary>Velocidad vertical del salto: la base menos lo que resta la carga, con un piso.</summary>
    private float VelocidadSalto()
    {
        float peso = playerData != null ? playerData.pesoTotal : 0f;
        return Mathf.Max(fuerzaSaltoBase - peso * factorPesoSalto, fuerzaSaltoMinima);
    }

    void FixedUpdate()
    {
        if (movementLocked || ModoMenu.Activo) return;
        // 1. Detección del suelo (raycast hacia abajo)
        enSuelo = Physics.Raycast(transform.position, Vector3.down, distanciaDeteccionSuelo, capasSuelo);

        // 1.5. Daño por caída: trackea la altura máxima alcanzada en el aire y, al aterrizar,
        // mide la distancia caída. Si supera el umbral letal, mata al jugador.
        float yActual = transform.position.y;
        if (!enSuelo)
        {
            if (yActual > alturaMaximaEnAire) alturaMaximaEnAire = yActual;
        }
        else
        {
            // Aterrizaje: el frame en que pasamos de aire a suelo
            if (!enSueloFrameAnterior)
            {
                float distanciaCaida = alturaMaximaEnAire - yActual;
                // if (logCaidas) Debug.Log($"[PlayerMovement] Aterrizaje. Caída: {distanciaCaida:F1} m (letal a partir de {distanciaCaidaLetal} m).");
                if (danoCaidaActivo && distanciaCaida >= distanciaCaidaLetal && playerData != null && !playerData.Muerto)
                {
                    playerData.Morir($"Caída de {distanciaCaida:F0} m");
                    return; // no procesar más este frame
                }
            }
            // Reset: estando en el suelo, la altura máxima se sigue al jugador
            alturaMaximaEnAire = yActual;
        }
        enSueloFrameAnterior = enSuelo;

        // 2. Lectura del input de movimiento
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 direccion = transform.right * x + transform.forward * z;
        if (direccion.magnitude > 1f) direccion.Normalize();

        // 3. Determinar si se está sprintando (agachado no se puede: prima el sigilo)
        isSprinting = !agachado && Input.GetKey(KeyCode.LeftShift) && direccion.magnitude > 0f;
        float objetivoVel = agachado ? velocidadAgachado
                          : (isSprinting ? velocidadSprintObjetivo : velocidadCaminar);
        
        // Si está volando y sprintando, aplicar el multiplicador de velocidad. Solo en modo vuelo:
        // en modo salto, Shift + saltar te lanzaría 2.5x más lejos.
        if (modoVuelo && !enSuelo && Input.GetKey(KeyCode.LeftShift))
        {
            objetivoVel *= multiplicadorVelocidadSprintVuelo;
        }

        float accel = isSprinting ? aceleracionSprint : desaceleracion;
        velocidadActual = Mathf.MoveTowards(velocidadActual, objetivoVel, accel * Time.fixedDeltaTime);

        // 4. VUELO: Space para ascender, soltar Space para caer
        float yVel = rb.linearVelocity.y;

        if (!modoVuelo)
        {
            // 4b. SALTO: la gravedad del Rigidbody hace el resto.
            if (bloquearSpaceHastaSoltar && !Input.GetKey(KeyCode.Space))
                bloquearSpaceHastaSoltar = false;

            if (intentarSaltar && enSuelo)
                yVel = VelocidadSalto();
            intentarSaltar = false;
        }
        else if (bloquearSpaceHastaSoltar)
        {
            if (Input.GetKeyUp(KeyCode.Space))
            {
                bloquearSpaceHastaSoltar = false;
            }

            yVel = Mathf.Max(yVel, velocidadVueloCaida);
        }
        else if (Input.GetKey(KeyCode.Space))
        {
            // Mientras mantienes Space, asciendes a velocidad constante (o más rápido si mantienes Shift)
            float velAscenso = velocidadVueloAscenso;
            if (Input.GetKey(KeyCode.LeftShift))
            {
                velAscenso *= multiplicadorVelocidadSprintVuelo;
            }
            yVel = velAscenso;
        }
        else
        {
            // Cuando sueltas Space, caes con gravedad
            yVel = Mathf.Max(yVel, velocidadVueloCaida);
        }

        // 5. Aplicar movimiento
        rb.linearVelocity = new Vector3(direccion.x * velocidadActual, yVel, direccion.z * velocidadActual);
    }

    public void BloquearSpaceHastaSoltar()
    {
        bloquearSpaceHastaSoltar = true;
    }
}
