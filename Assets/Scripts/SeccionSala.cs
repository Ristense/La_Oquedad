using UnityEngine;

/// <summary>
/// Punto de reabastecimiento temático dentro de una vivienda (una celda del 3x3).
/// Va en el prefab PADRE del conjunto: el WorldGenerator lo instancia en el CENTRO de una de
/// las 4 esquinas de la sala, a la altura del piso y sin rotación — con el pivote del conjunto
/// en el origen, calza solo. Todo el contenido se arma a mano dentro del prefab.
///
/// Este script solo identifica de qué tipo es la sección. Para marcar el objeto interactuable
/// de adentro, agregale a mano el componente MarcaObjetoEspecial.
/// </summary>
public class SeccionSala : MonoBehaviour
{
    public enum Tipo { Agua, Comida, Bateria, Dormir, Almacenamiento }

    [Tooltip("Qué stat/función cubre esta sección. Por ahora es solo identificación (no hay lógica atada al tipo).")]
    public Tipo tipo = Tipo.Comida;
}
