using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Permite abrir y cerrar una pared/puerta interactiva mediante un Canvas en World Space
/// o directamente mirando al panel y presionando la tecla [E].
/// </summary>
public class PuertaInteractiva : MonoBehaviour
{
    [Header("Referencias de Movimiento Procedimental (Fallback)")]
    [Tooltip("El objeto de la pared que se moverá físicamente (ej. la plancha de metal).")]
    public Transform parteMovil;
    [Tooltip("Hacia dónde se desplaza la parte móvil cuando se abre (relativo a su posición inicial).")]
    public Vector3 offsetAbierto = new Vector3(0f, 6f, 0f);
    [Tooltip("Velocidad de la animación de apertura/cierre.")]
    public float velocidadMovimiento = 2f;

    [Header("Referencias de Animación Exportada (Animator)")]
    [Tooltip("Componente Animator opcional. Si está asignado, se usará en lugar del movimiento procedimental por script.")]
    public Animator animator;
    [Tooltip("Nombre del parámetro Bool en el Animator para controlar la apertura (true = abierto, false = cerrado).")]
    public string parametroBool = "IsOpen";

    [Header("Ajustes de Interacción")]
    [Tooltip("Distancia máxima a la que el jugador puede interactuar con el panel.")]
    public float alcanceInteraccion = 4f;

    [Header("Feedback de Sonido (Opcional)")]
    public AudioSource audioSource;
    public AudioClip sonidoAbrir;
    public AudioClip sonidoCerrar;

    [Header("Carteles de Peligro (Hologramas)")]
    [Tooltip("GameObjects de los carteles holográficos a cada lado de la puerta. Inician desactivados, se encienden al abrir y se apagan al cerrar.")]
    public GameObject[] cartelesPeligro;

    [Header("Referencias de Interfaz (Canvas World Space)")]
    [Tooltip("Texto en la pantalla que indica el estado (ej. ABIERTO / CERRADO).")]
    public TMP_Text textoEstado;
    [Tooltip("Imagen de fondo del panel (ej. luz indicadora roja/verde).")]
    public Image imagenIndicadora;
    public Color colorAbierto = Color.green;
    public Color colorCerrado = Color.red;

    private Vector3 posicionCerrado;
    private Vector3 posicionAbierto;
    private bool isOpen = false;
    private Coroutine coroutineMovimiento;

    private void Start()
    {
        if (parteMovil == null) parteMovil = transform;

        // Registrar las posiciones cerrado/abierto relativas al inicio
        posicionCerrado = parteMovil.localPosition;
        posicionAbierto = posicionCerrado + offsetAbierto;

        if (animator != null)
        {
            animator.SetBool(parametroBool, isOpen);
        }

        ActualizarInterfazVisual();
        SetCartelesPeligro(false);
    }

    private void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float dist = Vector3.Distance(transform.position, cam.transform.position);
        if (dist > alcanceInteraccion) return;

        // Hacer Raycast desde la cámara del jugador para ver si está apuntando al panel
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, alcanceInteraccion))
        {
            // El jugador está mirando a esta puerta o a uno de sus hijos
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                if (TooltipManager.Instance != null)
                {
                    string msg = isOpen ? "[Click] Cerrar Compuerta" : "[Click] Abrir Compuerta";
                    TooltipManager.Instance.MostrarTooltip(gameObject, msg);
                }

                if (Input.GetMouseButtonDown(0))
                {
                    AlternarPuerta();
                }
            }
        }
    }

    /// <summary>
    /// Método público para alternar el estado. Puede llamarse desde el evento OnClick()
    /// de un botón del Canvas en World Space de Unity.
    /// </summary>
    public void AlternarPuerta()
    {
        isOpen = !isOpen;

        // Detener animación previa si estaba en curso
        if (coroutineMovimiento != null)
        {
            StopCoroutine(coroutineMovimiento);
        }

        SetCartelesPeligro(true);

        if (animator != null)
        {
            animator.SetBool(parametroBool, isOpen);
            StartCoroutine(EsperarAnimator());
        }
        else
        {
            Vector3 targetPos = isOpen ? posicionAbierto : posicionCerrado;
            coroutineMovimiento = StartCoroutine(AnimarMovimiento(targetPos));
        }

        // Reproducir sonido
        if (audioSource != null)
        {
            AudioClip clip = isOpen ? sonidoAbrir : sonidoCerrar;
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        ActualizarInterfazVisual();
    }

    private void SetCartelesPeligro(bool activo)
    {
        if (cartelesPeligro == null) return;
        foreach (var cartel in cartelesPeligro)
            if (cartel != null) cartel.SetActive(activo);
    }

    private IEnumerator AnimarMovimiento(Vector3 destino)
    {
        while (Vector3.Distance(parteMovil.localPosition, destino) > 0.01f)
        {
            parteMovil.localPosition = Vector3.MoveTowards(
                parteMovil.localPosition,
                destino,
                velocidadMovimiento * Time.deltaTime
            );
            yield return null;
        }
        parteMovil.localPosition = destino;
        SetCartelesPeligro(false);
    }

    private IEnumerator EsperarAnimator()
    {
        yield return null; // esperar un frame para que el Animator cambie de estado
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        yield return new WaitForSeconds(info.length);
        SetCartelesPeligro(false);
    }

    private void ActualizarInterfazVisual()
    {
        if (textoEstado != null)
        {
            textoEstado.text = isOpen ? "ACCESO ABIERTO" : "SISTEMA CERRADO";
        }

        if (imagenIndicadora != null)
        {
            imagenIndicadora.color = isOpen ? colorAbierto : colorCerrado;
        }
    }

    // Dibujar el gizmo del movimiento de apertura en el editor para guiar al desarrollador
    private void OnDrawGizmosSelected()
    {
        if (parteMovil == null) return;

        Vector3 posStart = parteMovil.position;
        Vector3 posEnd = parteMovil.parent != null 
            ? parteMovil.parent.TransformPoint(parteMovil.localPosition + offsetAbierto)
            : posStart + offsetAbierto;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(posStart, posEnd);
        Gizmos.DrawWireCube(posEnd, parteMovil.localScale);
    }
}
