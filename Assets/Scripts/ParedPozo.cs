using UnityEngine;

/// <summary>
/// Marcador para los prefabs de POZO: declara "de este lado hay pared". Se pone en un Empty hijo
/// del prefab, corrido hacia el lado de la pared (no en el centro). El WorldGenerator lee la
/// posición de todos los marcadores para saber qué lados del prefab necesitan muro, y solo lo usa
/// en celdas donde esas paredes EXISTEN, girado para que calcen.
///
/// El script no supone cómo armaste el prefab: lo lee. Un prefab SIN marcadores es genérico
/// (va en cualquier pozo con giro al azar). Todos van en el mismo array, Prefabs Centro Pozo.
///
///
/// El lado se deduce del eje dominante de la posición local respecto a la raíz del prefab: un
/// marcador en (0, 1, 4) es la pared +Z; uno en (-4, 1, 0.5) es la pared -X.
/// </summary>
public class ParedPozo : MonoBehaviour
{
    void OnDrawGizmos()
    {
        // Una placa roja semitransparente, para ver en el editor de prefab qué lados declaraste.
        Transform raiz = transform.root;
        Vector3 local = raiz.InverseTransformPoint(transform.position);
        bool ejeX = Mathf.Abs(local.x) > Mathf.Abs(local.z);

        Gizmos.matrix = Matrix4x4.TRS(transform.position, raiz.rotation, Vector3.one);
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.35f);
        Vector3 tam = ejeX ? new Vector3(0.2f, 6f, 6f) : new Vector3(6f, 6f, 0.2f);
        Gizmos.DrawCube(Vector3.zero, tam);
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, tam);
    }
}
