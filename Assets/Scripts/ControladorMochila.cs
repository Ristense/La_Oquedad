using UnityEngine;

/// <summary>
/// Mueve la mochila entre dos posiciones: "Guardada" (cerrada, en la espalda) y
/// "Abierta" (al frente de la cámara). Por defecto sigue la posición Guardada.
/// Tecla Tab alterna abrir/cerrar. Al abrir libera el cursor y bloquea la cámara.
/// Click derecho mantenido (mochila abierta, sin item en mano) rota la mochila sobre su eje.
/// </summary>
public class ControladorMochila : MonoBehaviour
{
    [Header("Transforms")]
    [Tooltip("El objeto mochila que se mueve (el que tiene MochilaGrid).")]
    public Transform mochila;
    [Tooltip("Posición/rotación cuando está cerrada (ej. MochilaPos.Guardada).")]
    public Transform posGuardada;
    [Tooltip("Posición/rotación cuando está abierta (ej. MochilaPos.Abierta).")]
    public Transform posAbierta;

    [Header("Ajustes")]
    public KeyCode teclaToggle = KeyCode.Tab;
    [Tooltip("Velocidad de interpolación del movimiento de la mochila.")]
    public float velocidad = 10f;

    [Header("Rotación Manual (Click Derecho)")]
    [Tooltip("Sensibilidad de rotación al arrastrar con click derecho.")]
    public float sensibilidadRotacion = 0.5f;
    [Tooltip("Velocidad con la que la mochila vuelve a su rotación original al cerrar.")]
    public float velocidadRetorno = 8f;

    [Header("Cámara / Cursor (opcional)")]
    [Tooltip("Script MouseLook para bloquear la cámara mientras el inventario está abierto.")]
    public MouseLook mouseLook;

    [Header("Referencias")]
    [Tooltip("Referencia a ManoJugador para saber si tiene item en mano.")]
    public ManoJugador manoJugador;

    public bool Abierta { get; private set; } = false;

    private bool rotandoManual = false;
    private Quaternion rotacionExtra = Quaternion.identity;

    private void Update()
    {
        if (Input.GetKeyDown(teclaToggle))
        {
            Abierta = !Abierta;
            if (!Abierta) rotacionExtra = Quaternion.identity;
            AplicarEstadoCursor();
        }

        Transform objetivo = Abierta ? posAbierta : posGuardada;
        if (mochila == null || objetivo == null) return;

        bool tieneItemEnMano = manoJugador != null && manoJugador.ItemEnMano != null;

        if (Abierta && !tieneItemEnMano)
        {
            if (Input.GetMouseButtonDown(1))
                rotandoManual = true;

            if (Input.GetMouseButtonUp(1))
                rotandoManual = false;

            if (rotandoManual)
            {
                float deltaX = Input.GetAxis("Mouse X") * sensibilidadRotacion;
                rotacionExtra = Quaternion.Euler(0f, -deltaX, 0f) * rotacionExtra;
            }
        }
        else
        {
            rotandoManual = false;
            float tRetorno = 1f - Mathf.Exp(-velocidadRetorno * Time.deltaTime);
            rotacionExtra = Quaternion.Slerp(rotacionExtra, Quaternion.identity, tRetorno);
        }

        float t = 1f - Mathf.Exp(-velocidad * Time.deltaTime);
        mochila.position = Vector3.Lerp(mochila.position, objetivo.position, t);

        Quaternion rotObjetivo = objetivo.rotation * rotacionExtra;
        mochila.rotation = Quaternion.Slerp(mochila.rotation, rotObjetivo, t);
    }

    private void AplicarEstadoCursor()
    {
        if (Abierta)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (mouseLook != null) mouseLook.canLook = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (mouseLook != null) mouseLook.canLook = true;
        }
    }
}
