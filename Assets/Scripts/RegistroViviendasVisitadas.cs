using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Codex de LUGARES: viviendas que el jugador ya pisó al menos una vez (identificadas por su
/// macro chunk). Mismo patrón que RegistroDescubrimientos, pero separado — BitacoraUI ya
/// depende de ItemData/CategoriaItem para los items, mezclar los dos tipos ahí rompería ese
/// contrato. Persiste entre escenas por ser estático (no entre cierres del juego).
/// </summary>
public static class RegistroViviendasVisitadas
{
    private static readonly HashSet<Vector3Int> visitadas = new HashSet<Vector3Int>();
    private static readonly List<Vector3Int> bitacora = new List<Vector3Int>();

    /// <summary>Se dispara SOLO cuando se visita una vivienda nueva (para repintar la bitácora).</summary>
    public static event System.Action<Vector3Int> OnNuevaVisita;

    public static bool FueVisitada(Vector3Int macroChunk) => visitadas.Contains(macroChunk);

    /// <summary>Marca la vivienda como visitada. Devuelve true solo si era nueva.</summary>
    public static bool Visitar(Vector3Int macroChunk)
    {
        if (!visitadas.Add(macroChunk)) return false;
        bitacora.Add(macroChunk);
        OnNuevaVisita?.Invoke(macroChunk);
        return true;
    }

    public static IReadOnlyList<Vector3Int> Bitacora => bitacora;
    public static int Total => bitacora.Count;

    public static void Reset()
    {
        visitadas.Clear();
        bitacora.Clear();
    }
}
