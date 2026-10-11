using UnityEngine;

/// <summary>
/// Va en la TABLET (el objeto que prende/apaga TogglePantalla). Mientras la tablet está abierta:
///
///   • Sin Click Derecho → la tablet sigue tu vista normalmente (no hacemos nada, la sigue por
///     jerarquía: es hija de la cámara/brazo).
///   • Manteniendo Click Derecho → la tablet queda CLAVADA donde estaba, se libera el mouse para
///     que puedas clickear la interfaz, y se bloquean la mirada y los otros usos del Click Derecho
///     (zoom de la tirolina, cambio de mira, inspección de objetos).
///
/// ⚠️ La tablet se congela reponiendo su pose de mundo en LateUpdate, NO desemparentándola. Sacarla
/// de su padre rompería el render si cuelga de un Canvas, y LateUpdate corre después de que la
/// cámara ya se movió — que es justo lo que hay que deshacer.
///
/// ⚠️ El estado que consultan los otros sistemas vive en <see cref="ModoTablet"/> y se calcula al
/// vuelo, no se escribe acá — ver el comentario de esa clase: escribirlo desde este Update hacía
/// que la tirolina lo leyera un frame tarde y parpadeara su HUD.
/// </summary>
public class TabletaMouse : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("MouseLook del jugador, para frenar la cámara mientras usás la interfaz. Si lo dejás vacío se busca solo por la escena al arrancar.")]
    public MouseLook mouseLook;

    [Header("Control")]
    [Tooltip("Botón del mouse que libera el cursor. 1 = Click Derecho.")]
    public int botonMouse = 1;
    [Tooltip("ON = mientras mantenés el botón, la tablet queda clavada en el mundo en vez de seguir tu vista. Apagalo si preferís que te siga siempre y solo querés el cursor libre.")]
    public bool congelarEnElMundo = true;
    [Tooltip("ON = el cursor del sistema queda invisible y el punto de mira hace de cursor (ver MiraSigueMouse). El mouse se mueve igual, solo no se dibuja. Apagalo para ver la flechita de siempre.")]
    public bool ocultarCursorDelSistema = true;

    [Header("HUD")]
    [Tooltip("Objetos del HUD que se apagan mientras manejás la tablet con el mouse (ej. Slider_Linterna). Van acá los que estorban visualmente o se comen los clicks por estar en el canvas Overlay, que se dibuja por encima del canvas World Space de la tablet.\n\nSe restaura el estado que tenía cada uno, no se prenden todos al soltar.")]
    public GameObject[] ocultarMientrasUsoMouse;

    private bool[] estadoOriginalHud;

    private bool usando;
    // Dónde queda clavada en el MUNDO mientras la mantenés.
    private Vector3 posCongelada;
    private Quaternion rotCongelada;
    // Su pose LOCAL de siempre (relativa a la cámara/brazo), para devolvérsela al soltar.
    private Vector3 posLocalOriginal;
    private Quaternion rotLocalOriginal;

    void Awake()
    {
        if (mouseLook == null) mouseLook = FindAnyObjectByType<MouseLook>();
    }

    void OnEnable()
    {
        ModoTablet.BotonMouse = botonMouse;
        ModoTablet.Abierta = true;

        // ⚠️ La pose LOCAL de la tablet en el brazo, capturada al ABRIRLA (no al apretar el botón),
        // que es el único momento garantizado en que está en su lugar.
        //
        // Hace falta porque congelarla en el mundo con SetPositionAndRotation le reescribe el
        // localPosition contra su padre (la cámara): mientras caminás y girás con el botón
        // apretado, ese offset local se va a cualquier lado. Sin devolvérselo al soltar, la tablet
        // quedaba clavada donde dejó la última cuenta y desaparecía de la vista, con la mecánica
        // funcionando igual porque el objeto seguía activo.
        posLocalOriginal = transform.localPosition;
        rotLocalOriginal = transform.localRotation;
    }

    /// <summary>
    /// Cerrar la tablet con el botón apretado dejaría el cursor suelto y la cámara trabada para
    /// siempre — este componente deja de correr Update apenas su objeto se apaga.
    /// </summary>
    void OnDisable()
    {
        ModoTablet.Abierta = false;
        if (usando) Salir();
    }

    void Update()
    {
        bool quiereUsar = ModoTablet.UsandoMouse;

        if (quiereUsar && !usando) Entrar();
        else if (!quiereUsar && usando) Salir();
    }

    void LateUpdate()
    {
        // Después de que la cámara (y por lo tanto el padre de la tablet) ya se movió este frame.
        if (usando && congelarEnElMundo)
            transform.SetPositionAndRotation(posCongelada, rotCongelada);
    }

    private void Entrar()
    {
        usando = true;

        posCongelada = transform.position;
        rotCongelada = transform.rotation;

        // Confined en vez de None cuando el cursor es invisible: si no, un mouse que no ves se te
        // escapa de la ventana y clickeás cualquier cosa del escritorio sin darte cuenta.
        Cursor.lockState = ocultarCursorDelSistema ? CursorLockMode.Confined : CursorLockMode.None;
        Cursor.visible = !ocultarCursorDelSistema;
        if (mouseLook != null) mouseLook.canLook = false;

        OcultarHud();
    }

    private void Salir()
    {
        usando = false;

        // Vuelve a su lugar en el brazo. Sin esto se queda donde la dejó el congelado en mundo.
        if (congelarEnElMundo)
        {
            transform.localPosition = posLocalOriginal;
            transform.localRotation = rotLocalOriginal;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (mouseLook != null) mouseLook.canLook = true;

        RestaurarHud();
    }

    private void OcultarHud()
    {
        if (ocultarMientrasUsoMouse == null) return;
        estadoOriginalHud = new bool[ocultarMientrasUsoMouse.Length];
        for (int i = 0; i < ocultarMientrasUsoMouse.Length; i++)
        {
            GameObject go = ocultarMientrasUsoMouse[i];
            if (go == null) continue;
            estadoOriginalHud[i] = go.activeSelf;
            go.SetActive(false);
        }
    }

    /// <summary>
    /// Repone lo que cada objeto tenía, no los prende a todos: si algo ya estaba apagado por otro
    /// sistema (ej. la tirolina esconde el slider de linterna al apuntar), soltar la tablet no
    /// tiene por qué encenderlo.
    /// </summary>
    private void RestaurarHud()
    {
        if (ocultarMientrasUsoMouse == null || estadoOriginalHud == null) return;
        for (int i = 0; i < ocultarMientrasUsoMouse.Length && i < estadoOriginalHud.Length; i++)
        {
            GameObject go = ocultarMientrasUsoMouse[i];
            if (go != null) go.SetActive(estadoOriginalHud[i]);
        }
        estadoOriginalHud = null;
    }
}
