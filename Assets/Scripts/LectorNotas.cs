using UnityEngine;

/// <summary>
/// Maneja la sostención y lectura de notas (NotaMundo) por fuera del sistema de inventario.
/// - Click Izq: agarra (raycast desde el centro de la cámara).
/// - Click Izq UP: suelta (cae con físicas).
/// - Click Der HOLD: muestra el panel de lectura (mientras la sostiene).
/// - E: guarda la nota en PlayerData.notasGuardadas y desaparece del mundo.
///
/// Pensado para coexistir con ManoJugador: si el raycast no encuentra una nota,
/// no hace nada y ManoJugador maneja la interacción normalmente.
/// </summary>
public class LectorNotas : MonoBehaviour
{
    [Header("Referencias")]
    public Camera camara;
    public PlayerData playerData;
    public NotaPanelUI panelLectura;
    [Tooltip("Para chequear si la mochila está abierta y bloquear el agarre desde el cursor.")]
    public ControladorMochila mochila;

    [Header("Configuración")]
    public float rangoRaycast = 4f;
    public LayerMask capasRaycast = ~0;
    [Tooltip("Distancia a la que la nota flota frente a la cámara mientras se sostiene.")]
    public float distanciaSostener = 0.6f;
    [Tooltip("Offset vertical (en metros) respecto al centro de la cámara.")]
    public float offsetVertical = -0.15f;

    public bool SostieneNota => enMano != null;
    public NotaMundo NotaSostenida => enMano;

    private NotaMundo enMano;
    private Rigidbody rbEnMano;
    private ManoJugador manoJugador;

    void Start()
    {
        manoJugador = GetComponent<ManoJugador>();
        if (manoJugador == null) manoJugador = FindAnyObjectByType<ManoJugador>();
    }

    void Update()
    {
        // Si tenemos nota en mano y se suelta Click Izq → soltar
        if (enMano != null && Input.GetMouseButtonUp(0))
        {
            SoltarNota();
            return;
        }

        // Agarrar si no tenemos nada (ni nota ni item en ManoJugador), la mochila está cerrada y se presiona Click Izq
        bool manoLibre = (manoJugador == null || manoJugador.ItemEnMano == null);
        bool mochilaCerrada = (mochila == null || !mochila.Abierta);
        if (enMano == null && manoLibre && mochilaCerrada && Input.GetMouseButtonDown(0) && camara != null)
        {
            Ray ray = camara.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
            if (Physics.Raycast(ray, out RaycastHit hit, rangoRaycast, capasRaycast, QueryTriggerInteraction.Ignore))
            {
                NotaMundo nota = hit.collider.GetComponentInParent<NotaMundo>();
                if (nota != null) AgarrarNota(nota);
            }
        }

        // Mientras sostiene
        if (enMano != null)
        {
            // Flotar frente a la cámara (sin punto Transform, similar al anclaje en modo preview)
            if (camara != null)
            {
                enMano.transform.position = camara.transform.position
                                          + camara.transform.forward * distanciaSostener
                                          + camara.transform.up * offsetVertical;
                enMano.transform.rotation = camara.transform.rotation;
            }

            // Click Der HOLD: panel de lectura
            if (panelLectura != null)
            {
                if (Input.GetMouseButton(1)) panelLectura.Mostrar(enMano.data);
                else                          panelLectura.Ocultar();
            }

            // E: guardar y consumir
            if (Input.GetKeyDown(KeyCode.E))
            {
                // Guardar la nota en la BD del jugador. NO tocamos pesoTotal acá:
                // lo recalcula PlayerData cada frame desde la mochila + item en mano + nota sostenida.
                if (playerData != null && enMano.data != null)
                    playerData.GuardarNota(enMano.data);
                if (panelLectura != null) panelLectura.Ocultar();
                Destroy(enMano.gameObject);
                enMano = null;
                rbEnMano = null;
            }
        }
    }

    private void AgarrarNota(NotaMundo nota)
    {
        enMano = nota;
        rbEnMano = nota.GetComponent<Rigidbody>();
        if (rbEnMano != null)
        {
            rbEnMano.linearVelocity = Vector3.zero;
            rbEnMano.angularVelocity = Vector3.zero;
            rbEnMano.isKinematic = true;
        }
        // pesoTotal se recalcula en PlayerData.Update — no hace falta sumar acá.
    }

    private void SoltarNota()
    {
        if (enMano == null) return;
        if (rbEnMano != null) rbEnMano.isKinematic = false;
        if (panelLectura != null) panelLectura.Ocultar();
        enMano = null;
        rbEnMano = null;
    }
}
