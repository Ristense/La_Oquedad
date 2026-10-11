using System.Collections.Generic;

/// <summary>
/// Estado persistente entre escenas: stats de la última run + colección global de notas leídas.
/// Sobrevive el cambio de escena porque es estático. NO persiste entre cierres del juego
/// (para eso habría que serializar a PlayerPrefs o disco — se deja como mejora futura).
/// </summary>
public static class EstadisticasRun
{
    // --- Stats de la última run (Game Over screen los lee) ---
    public static int diasSobrevividos = 0;
    public static int notasLeidas = 0;
    public static float pesoMaximoCargado = 0f;
    public static string causaMuerte = "—";
    public static bool huboMuerte = false; // false = aún no se jugó / se reseteó

    // --- Lore log: notas leídas a lo largo de TODAS las runs ---
    public static HashSet<NotaData> bibliotecaNotas = new HashSet<NotaData>();

    public static void RegistrarMuerte(int diasSobrevividosRun, int notasRun, float pesoMaxRun, string causa, IEnumerable<NotaData> notasDeRun)
    {
        diasSobrevividos = diasSobrevividosRun;
        notasLeidas = notasRun;
        pesoMaximoCargado = pesoMaxRun;
        causaMuerte = causa;
        huboMuerte = true;
        if (notasDeRun != null)
            foreach (var n in notasDeRun) if (n != null) bibliotecaNotas.Add(n);
    }
}
