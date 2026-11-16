using UnityEngine;

/// <summary>
/// Cofre que se abre y se cierra con [E] mirándolo. La animación la hace el Animator del cofre:
/// este script solo prende o apaga el Bool `parametroAbierto` (por defecto "Abierto").
///
/// Armado del Animator Controller (uno solo para el cofre):
///   · Parámetro Bool "Abierto".
///   · Estados "Cerrado" (default) y "Abierto", cada uno con su clip.
///   · Transición Cerrado → Abierto con condición Abierto = true, y Abierto → Cerrado con
///     Abierto = false. En las dos: Has Exit Time apagado.
///
/// Bloqueo: segunda etapa, todavía APAGADA (`usarBloqueo`). Cuando se prenda, un cofre
/// bloqueado pide "[E] Desbloquear" primero y recién después se puede abrir. Hoy no hay forma
/// de desbloquear (ni llave, ni minijuego): con el toggle encendido, [E] solo lo desbloquea.
/// </summary>
public class CofreInteractivo : MonoBehaviour
{
    [Header("Animación")]
    [Tooltip("Animator del cofre. Vacío = lo busca en este objeto o en sus hijos.")]
    public Animator animator;
    [Tooltip("Nombre del parámetro Bool del Animator: true = abierto, false = cerrado.")]
    public string parametroAbierto = "Abierto";

    [Header("Interacción")]
    [Tooltip("Distancia máxima (m) a la que se puede abrir mirándolo.")]
    public float alcanceInteraccion = 4f;
    public KeyCode tecla = KeyCode.E;
    [Tooltip("Si está tildado, con el cofre abierto [E] lo vuelve a cerrar. Apagado = una vez abierto, queda abierto.")]
    public bool permitirCerrar = true;

    [Header("Bloqueo (segunda etapa, APAGADO por ahora)")]
    [Tooltip("Todavía no se usa. Encendido = el cofre empieza bloqueado y [E] primero lo desbloquea.")]
    public bool usarBloqueo = false;
    [Tooltip("Solo con Usar Bloqueo: si arranca bloqueado.")]
    public bool empiezaBloqueado = true;

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip sonidoAbrir;
    public AudioClip sonidoCerrar;
    public AudioClip sonidoDesbloquear;

    private bool abierto;
    private bool bloqueado;
    private int hashParametro;
    private bool animatorTieneParametro;

    public bool EstaAbierto => abierto;
    public bool EstaBloqueado => usarBloqueo && bloqueado;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        hashParametro = Animator.StringToHash(parametroAbierto);
        bloqueado = empiezaBloqueado;

        if (animator != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.nameHash == hashParametro && p.type == AnimatorControllerParameterType.Bool) animatorTieneParametro = true;
            if (!animatorTieneParametro)
                Debug.LogWarning($"[Cofre] El Animator de '{name}' no tiene un parámetro Bool llamado '{parametroAbierto}'. Crealo en el Animator Controller.", this);
        }
        else
        {
            Debug.LogWarning($"[Cofre] '{name}' no tiene Animator: se puede apuntar pero no se va a mover.", this);
        }
    }

    void OnEnable()
    {
        // El generador apaga y prende las celdas: al volver a prenderse, el Animator arranca de cero,
        // así que se le repite el estado que tenía.
        if (animator != null && animatorTieneParametro) animator.SetBool(hashParametro, abierto);
    }

    void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (Vector3.Distance(transform.position, cam.transform.position) > alcanceInteraccion + 2f) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, alcanceInteraccion, ~0, QueryTriggerInteraction.Collide)) return;
        if (hit.transform != transform && !hit.transform.IsChildOf(transform)) return;

        // Apuntando a algo que está ADENTRO (un objeto agarrable): el cartel es de ese objeto, no
        // del cofre. Si no, los dos carteles se pelean y parpadean.
        InventoryItem item = hit.collider.GetComponentInParent<InventoryItem>();
        if (item != null && item.transform != transform && item.transform.IsChildOf(transform)) return;

        string prompt = TextoPrompt();
        if (prompt == null) return;

        if (TooltipManager.Instance != null)
            TooltipManager.Instance.MostrarTooltip(gameObject, prompt);

        if (Input.GetKeyDown(tecla)) Interactuar();
    }

    /// <summary>Lo que dice el cartel según el estado. Null = no hay nada que hacer.</summary>
    private string TextoPrompt()
    {
        if (EstaBloqueado) return $"[{tecla}] Desbloquear";
        if (!abierto) return $"[{tecla}] Abrir";
        return permitirCerrar ? $"[{tecla}] Cerrar" : null;
    }

    public void Interactuar()
    {
        if (EstaBloqueado)
        {
            Desbloquear();
            return;
        }
        if (abierto && !permitirCerrar) return;
        SetAbierto(!abierto);
    }

    public void Desbloquear()
    {
        bloqueado = false;
        Sonar(sonidoDesbloquear);
    }

    public void SetAbierto(bool valor)
    {
        abierto = valor;
        if (animator != null && animatorTieneParametro) animator.SetBool(hashParametro, abierto);
        Sonar(abierto ? sonidoAbrir : sonidoCerrar);
    }

    private void Sonar(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
