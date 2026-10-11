using UnityEngine;

/// <summary>Prende/apaga un GameObject con una tecla. Mismo patrón que BitacoraUI (tecla B).</summary>
public class TogglePantalla : MonoBehaviour
{
    public GameObject pantalla;
    public KeyCode tecla = KeyCode.Tab;

    [Header("Cursor (opcional)")]
    [Tooltip("ON = libera el cursor mientras la pantalla está abierta. Necesario si la pantalla tiene botones para clickear (ej. las pestañas de la bitácora dentro de la tablet). Dejalo OFF en pantallas que son solo lectura.")]
    public bool liberarCursor = false;
    [Tooltip("MouseLook a bloquear mientras la pantalla está abierta (opcional). Sin esto, mover el mouse para clickear un botón también gira la cámara.")]
    public MouseLook mouseLook;

    void Start()
    {
        if (pantalla != null) pantalla.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(tecla) && pantalla != null)
        {
            bool abrir = !pantalla.activeSelf;
            pantalla.SetActive(abrir);

            // El cursor lo maneja QUIEN prende la pantalla. Antes esto vivía en BitacoraUI.Alternar,
            // pero la bitácora ya no se abre sola: es contenido siempre activo adentro de la tablet.
            if (liberarCursor)
            {
                Cursor.lockState = abrir ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = abrir;
            }
            if (mouseLook != null) mouseLook.canLook = !abrir;
        }
    }
}
