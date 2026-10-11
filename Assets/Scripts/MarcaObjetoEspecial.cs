using UnityEngine;

/// <summary>
/// Marcador que `SeccionSala` agrega a los objetos que tenía tildado `esObjetoEspecial`.
/// NO tiene lógica: existe para poder identificarlos después (buscarlos por componente,
/// filtrarlos, engancharles el sistema de reabastecimiento/interacción cuando se defina).
/// </summary>
public class MarcaObjetoEspecial : MonoBehaviour
{
    [Tooltip("Tipo de sección de la que salió este objeto (agua/comida/batería/dormir/almacenamiento).")]
    public SeccionSala.Tipo seccionDeOrigen;
}
