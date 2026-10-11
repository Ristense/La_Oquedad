using UnityEngine;

/// <summary>
/// Dibuja una esfera de color en la vista Scene, SIEMPRE (sin necesidad de estar seleccionado).
/// Solo para debug/edicion — un GameObject vacio (Target/Hint de IK) no dibuja nada por si solo,
/// esto le da algo visible para poder encontrarlo y arrastrarlo.
/// </summary>
public class MarcadorGizmo : MonoBehaviour
{
    public Color color = Color.yellow;
    public float radio = 0.08f;

    private void OnDrawGizmos()
    {
        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, radio);
    }
}
