using UnityEngine;
using TMPro;

/// <summary>
/// Pinta los stats de la última run en la pantalla de Game Over del menú.
/// Si EstadisticasRun.huboMuerte == false (el jugador abrió el menú sin haber muerto),
/// se oculta el panel para no mostrar ceros.
/// </summary>
public class MenuGameOver : MonoBehaviour
{
    [Tooltip("Root del panel de Game Over que se muestra solo si hubo una muerte previa.")]
    public GameObject panelRoot;
    public TMP_Text txtCausa;
    public TMP_Text txtDias;
    public TMP_Text txtNotas;
    public TMP_Text txtPesoMaximo;

    void OnEnable()
    {
        Pintar();
    }

    private void Pintar()
    {
        if (panelRoot != null) panelRoot.SetActive(EstadisticasRun.huboMuerte);
        if (!EstadisticasRun.huboMuerte) return;

        if (txtCausa != null)       txtCausa.text       = $"Causa: {EstadisticasRun.causaMuerte}";
        if (txtDias != null)        txtDias.text        = $"Días sobrevividos: {EstadisticasRun.diasSobrevividos}";
        if (txtNotas != null)       txtNotas.text       = $"Notas leídas: {EstadisticasRun.notasLeidas}";
        if (txtPesoMaximo != null)  txtPesoMaximo.text  = $"Carga máxima: {EstadisticasRun.pesoMaximoCargado:F1} KG";
    }
}
