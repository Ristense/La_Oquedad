using UnityEngine;

/// <summary>
/// Controla el gesto de "revisar la interfaz del brazo" (estilo Pip-Boy): mantener Q sube el brazo
/// y muestra el panel de stats (World Space); soltar Q lo baja y lo oculta.
///
/// Versión con Parameters + Transitions del Animator Controller (en vez de Animator.Play manual):
/// este script solo setea un Bool ("MirandoInterfaz") y deja que el Animator maneje las transiciones
/// entre el estado de reposo/Idle y el estado del brazo — configura eso en el Animator Controller
/// (transición Idle → SubirBrazo con condición MirandoInterfaz=true, y la inversa con false).
/// </summary>
public class InterfazBrazo : MonoBehaviour
{
    [Header("Animación")]
    [Tooltip("Animator del modelo del jugador.")]
    public Animator animator;
    [Tooltip("Nombre EXACTO del parámetro Bool creado en el Animator Controller.")]
    public string nombreParametro = "MirandoInterfaz";

    [Header("Input")]
    public KeyCode teclaInterfaz = KeyCode.Q;

    [Header("Panel de Stats (World Space)")]
    public GameObject panelInterfaz;

    void Awake()
    {
        if (panelInterfaz != null) panelInterfaz.SetActive(false);
    }

    void Update()
    {
        bool sostenido = Input.GetKey(teclaInterfaz);

        if (animator != null)
            animator.SetBool(nombreParametro, sostenido);

        if (panelInterfaz != null && panelInterfaz.activeSelf != sostenido)
            panelInterfaz.SetActive(sostenido);
    }
}
