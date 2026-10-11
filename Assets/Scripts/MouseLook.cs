using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 200f;
    public Transform playerBody;

    // Variable para saber si podemos girar la cabeza o no
    public bool canLook = true;

    [Header("Suavizado de Mira (peso del casco)")]
    [Tooltip("Tiempo de suavizado (s). Más alto = mira más pesada. 0 = instantáneo. Sugerido 0.04 - 0.08.")]
    [Range(0f, 0.25f)]
    public float suavizadoMira = 0.05f;

    [Header("Inclinación al girar (roll / peso de la cabeza)")]
    [Tooltip("Grados máximos que la cámara se inclina sobre su propio eje al girar de lado. 0 = apagado. Sugerido 2-6 (más que eso marea).")]
    [Range(0f, 15f)]
    public float rollMaximo = 3f;
    [Tooltip("Cuánto giro de mouse hace falta para llegar a la inclinación máxima. Más bajo = se inclina con menos movimiento.")]
    public float rollSensibilidad = 2.5f;
    [Tooltip("Tiempo (s) que tarda en llegar/volver de la inclinación. Más alto = más perezoso.")]
    [Range(0.01f, 0.5f)]
    public float rollSuavizado = 0.12f;
    [Tooltip("Invertir el lado hacia el que se inclina.")]
    public bool rollInvertido = false;

    private float xRotation = 0f;
    private float mouseXSuave = 0f;
    private float mouseYSuave = 0f;
    private float rollActual = 0f;

    [Header("Escáner del Casco (HUD)")]
    public float distanciaRayo = 1.5f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Corrección defensiva: evitar que el playerBody apunte a la cámara misma,
        // lo cual causaría que la rotación Y se resetee constantemente en el Update.
        if (playerBody == null)
        {
            playerBody = transform.parent;
        }
        else if (playerBody == transform)
        {
            Debug.LogWarning("[MouseLook] playerBody está asignado a la propia Cámara. Corrigiendo automáticamente al Transform padre.");
            playerBody = transform.parent;
        }
    }

    void Update()
    {
        // ⚠️ Con el juego en PAUSA (timeScale 0) Time.deltaTime vale 0, y eso deja el factor del
        // filtro de abajo en k = 1 - exp(0) = 0. Con k = 0 el Lerp devuelve el valor ANTERIOR, así
        // que mouseXSuave/mouseYSuave se quedan congelados con la última velocidad — y como
        // xRotation se sigue aplicando cada frame, la cámara giraba sola para siempre mientras el
        // menú de pausa estaba abierto. Cortar acá arregla el caso para cualquiera que pause, no
        // solo para MenuPausa.
        if (Time.timeScale <= 0f || Time.deltaTime <= 0f)
        {
            FrenarSuavizado();
            return;
        }

        // Si no podemos mirar (ej. estamos en el inventario), salimos de la función.
        // Se frena el suavizado igual: si no, al cerrar el inventario la mira arrancaba con el
        // impulso viejo y pegaba un tirón.
        if (!canLook || ModoMenu.Activo)
        {
            FrenarSuavizado();
            return;
        }

        float mouseXRaw = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseYRaw = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Filtro pasa-bajos: suaviza el input para que la mira se sienta levemente pesada,
        // sin lag perceptible. Si suavizadoMira es 0, va directo (sin filtro).
        if (suavizadoMira > 0.001f)
        {
            float k = 1f - Mathf.Exp(-Time.deltaTime / suavizadoMira);
            mouseXSuave = Mathf.Lerp(mouseXSuave, mouseXRaw, k);
            mouseYSuave = Mathf.Lerp(mouseYSuave, mouseYRaw, k);
        }
        else
        {
            mouseXSuave = mouseXRaw;
            mouseYSuave = mouseYRaw;
        }

        xRotation -= mouseYSuave;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        // (ver FrenarSuavizado más abajo: estos dos valores nunca deben sobrevivir a una pausa)

        // Roll: al girar de lado la cabeza se inclina un poco hacia ese lado y vuelve sola al
        // soltar. Se aplica en el eje Z LOCAL de la cámara, así acompaña la mira sin importar
        // hacia dónde esté mirando el cuerpo (el "Z" del gizmo depende de a dónde apuntás).
        float rollObjetivo = 0f;
        if (rollMaximo > 0.001f && rollSensibilidad > 0.001f)
        {
            float t = Mathf.Clamp(mouseXSuave / rollSensibilidad, -1f, 1f);
            rollObjetivo = -t * rollMaximo;
            if (rollInvertido) rollObjetivo = -rollObjetivo;
        }
        float kRoll = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, rollSuavizado));
        rollActual = Mathf.Lerp(rollActual, rollObjetivo, kRoll);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, rollActual);
        playerBody.Rotate(Vector3.up * mouseXSuave);

        // Si el cuerpo tiene Rigidbody (con interpolación activa), sincronizamos
        // su rotación física para que la interpolación no fuerce la mira de vuelta al centro.
        Rigidbody rb = playerBody.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.rotation = playerBody.rotation;
        }

        // Ejecutar el escaneo del casco hacia adelante
        RealizarRaycastCasco();
    }

    private void RealizarRaycastCasco()
    {
        Ray rayoCasco = new Ray(transform.position, transform.forward);
        
        // Dibujamos el rayo de escaneo en azul cian en la pestaña Scene para debuguear
        Debug.DrawRay(transform.position, transform.forward * distanciaRayo, Color.cyan);

        if (Physics.Raycast(rayoCasco, out RaycastHit hit, distanciaRayo))
        {
            // Si impacta con algo, pintamos la línea en verde hasta el punto de impacto
            Debug.DrawLine(transform.position, hit.point, Color.green);
            
            // NOTA: Aquí es donde más adelante buscaremos si el objeto apuntado
            // es un 'InventoryItem' consumible o de escalada (tag 'Escalada') para avisar a la UI del casco.
        }
    }

    /// <summary>
    /// Deja el filtro de la mira en cero. Se llama cada vez que la mirada se corta (pausa,
    /// inventario, menú): el suavizado guarda VELOCIDAD, y esa velocidad no puede sobrevivir al
    /// corte — si sobrevive, la cámara sigue girando sola, o pega un tirón al volver.
    /// </summary>
    private void FrenarSuavizado()
    {
        mouseXSuave = 0f;
        mouseYSuave = 0f;
    }
}
