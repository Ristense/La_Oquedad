using UnityEngine;
using TMPro;
using System.Text;

/// <summary>
/// Lore log: muestra la lista de notas leídas (acumulada entre runs) en un TMP_Text único.
/// Una imagen de fondo cubre el panel. Cada renglón es el título de una nota.
///
/// Setup en la escena:
///   - Un GameObject con una Image (bg) + un TMP_Text hijo (para los títulos).
///   - Este script en el GameObject raíz del panel; arrastrá el TMP_Text al campo.
/// </summary>
public class MenuLoreLog : MonoBehaviour
{
    [Tooltip("TMP_Text donde se listan los títulos de las notas leídas, una por línea.")]
    public TMP_Text txtListado;
    [Tooltip("Mensaje cuando aún no se leyó ninguna nota.")]
    public string mensajeVacio = "Sin notas registradas aún.";

    void OnEnable()
    {
        Pintar();
    }

    private void Pintar()
    {
        if (txtListado == null) return;

        if (EstadisticasRun.bibliotecaNotas.Count == 0)
        {
            txtListado.text = mensajeVacio;
            return;
        }

        StringBuilder sb = new StringBuilder();
        foreach (var nota in EstadisticasRun.bibliotecaNotas)
        {
            if (nota == null) continue;
            sb.Append("• ").Append(nota.titulo);
            if (!string.IsNullOrEmpty(nota.autor)) sb.Append("  — ").Append(nota.autor);
            sb.Append('\n');
        }
        txtListado.text = sb.ToString().TrimEnd();
    }
}
