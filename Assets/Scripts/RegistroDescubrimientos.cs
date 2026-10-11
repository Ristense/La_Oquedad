using System.Collections.Generic;

/// <summary>
/// Codex de objetos: registra qué TIPOS de item (por su ItemData) el jugador ya "conoce".
/// Un item se descubre al TOMARLO. Hasta entonces la UI lo muestra como "Desconocido".
/// Persiste entre escenas por ser estático (igual que EstadisticasRun); NO entre cierres del
/// juego — para eso habría que serializar a disco/PlayerPrefs (mejora futura).
/// </summary>
public static class RegistroDescubrimientos
{
    // Lookup O(1) de "¿ya lo conozco?"
    private static readonly HashSet<ItemData> descubiertos = new HashSet<ItemData>();
    // Orden de descubrimiento, para la bitácora.
    private static readonly List<ItemData> bitacora = new List<ItemData>();

    /// <summary>Se dispara SOLO cuando se descubre un item nuevo (para toasts / repintar la bitácora).</summary>
    public static event System.Action<ItemData> OnNuevoDescubrimiento;

    public static bool EstaDescubierto(ItemData data) => data != null && descubiertos.Contains(data);

    /// <summary>Marca el item como descubierto. Devuelve true solo si era nuevo (no estaba ya).</summary>
    public static bool Descubrir(ItemData data)
    {
        if (data == null) return false;
        if (!descubiertos.Add(data)) return false; // ya lo conocía
        bitacora.Add(data);
        OnNuevoDescubrimiento?.Invoke(data);
        return true;
    }

    /// <summary>Lista en orden de descubrimiento (para pintar la bitácora).</summary>
    public static IReadOnlyList<ItemData> Bitacora => bitacora;
    public static int Total => bitacora.Count;

    /// <summary>Reinicia el codex (ej. para empezar una partida "desde cero" de verdad).</summary>
    public static void Reset()
    {
        descubiertos.Clear();
        bitacora.Clear();
    }
}
