using UnityEngine;

/// <summary>
/// Compendio de frases de UNA profesión de los habitantes con rutina (agua, comida, energía, centro,
/// general). Se crean en Resources/Profesiones/ con Create → Mundo → FrasesProfesion, uno por
/// profesión, y HabitanteRutina los carga solo: no hace falta asignarlos en ningún prefab.
///
/// Si para alguna profesión no hay asset (o sus listas están vacías), el habitante usa las frases por
/// defecto que trae el código, así nunca se queda mudo.
/// </summary>
[CreateAssetMenu(fileName = "FrasesProfesion", menuName = "Mundo/FrasesProfesion")]
public class FrasesProfesion : ScriptableObject
{
    [Tooltip("A qué profesión pertenecen estas frases.")]
    public HabitanteRutina.Profesion profesion = HabitanteRutina.Profesion.General;

    [Tooltip("Lo que dice cuando su zona está bien abastecida.")]
    [TextArea(2, 4)]
    public string[] frasesNormal;

    [Tooltip("Lo que dice cuando el recurso de su zona está bajo el umbral (pide ayuda). Al final se le agrega cuántos minutos quedan.")]
    [TextArea(2, 4)]
    public string[] frasesNecesidad;
}
