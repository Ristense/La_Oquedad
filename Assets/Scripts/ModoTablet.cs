using UnityEngine;

/// <summary>
/// Estado de la tablet, para que los sistemas que también usan Click Derecho (apuntar la tirolina,
/// inspeccionar un objeto en mano) no se disparen mientras estás usando su interfaz.
///
/// ⚠️ `UsandoMouse` es una PROPIEDAD calculada, no un bool que alguien setea en su Update. Esa fue
/// la primera versión y estaba mal: el orden de Update entre scripts es arbitrario, así que en el
/// frame en que apretabas el botón, SistemaTirolina lo leía ANTES de que TabletaMouse lo escribiera
/// — veía `false`, entraba en modo apuntado, y recién se corregía al frame siguiente. Se veía como
/// un parpadeo del HUD de la tirolina y un zoom que arrancaba y se cortaba.
///
/// Calculándolo al vuelo, cualquiera que lo consulte obtiene la respuesta correcta sin importar
/// cuándo corra. `Abierta` sí es un campo, pero lo escriben OnEnable/OnDisable de la tablet, no un
/// Update — cambia cuando abrís o cerrás la tablet, no en el frame del click.
/// </summary>
public static class ModoTablet
{
    /// <summary>La tablet está abierta. Lo mantiene TabletaMouse en OnEnable/OnDisable.</summary>
    public static bool Abierta = false;

    /// <summary>Botón que libera el cursor sobre la tablet (1 = Click Derecho). Lo publica TabletaMouse.</summary>
    public static int BotonMouse = 1;

    /// <summary>El jugador está manejando la interfaz de la tablet con el mouse AHORA.</summary>
    public static bool UsandoMouse => Abierta && !ModoMenu.Activo && Input.GetMouseButton(BotonMouse);
}
