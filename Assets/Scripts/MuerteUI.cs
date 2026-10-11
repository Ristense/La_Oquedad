using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Canvas de muerte que sobrevive el cambio de escena (DontDestroyOnLoad) y muestra los stats
/// de la run finalizada. Se cierra con Escape (y se autodestruye).
/// </summary>
public class MuerteUI : MonoBehaviour
{
    [Header("Textos de Stats")]
    public TMP_Text txtCausa;
    public TMP_Text txtDias;
    public TMP_Text txtNotas;
    public TMP_Text txtPesoMaximo;

    [Header("Fade")]
    [Tooltip("CanvasGroup para fade-in. Si está vacío, no se hace fade.")]
    public CanvasGroup canvasGroup;
    public float duracionFadeIn = 1.5f;

    private bool activo = false;

    void Awake()
    {
        // Empezar oculto si hay CanvasGroup
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Llamar al morir: persiste el canvas entre escenas, lee los stats de EstadisticasRun
    /// y hace fade-in.
    /// </summary>
    public void Activar()
    {
        if (activo) return;
        activo = true;

        // Pintar stats
        if (txtCausa != null)       txtCausa.text       = $"Causa: {EstadisticasRun.causaMuerte}";
        if (txtDias != null)        txtDias.text        = $"Días sobrevividos: {EstadisticasRun.diasSobrevividos}";
        if (txtNotas != null)       txtNotas.text       = $"Notas leídas: {EstadisticasRun.notasLeidas}";
        if (txtPesoMaximo != null)  txtPesoMaximo.text  = $"Carga máxima: {EstadisticasRun.pesoMaximoCargado:F1} KG";

        // Sobrevivir el cambio de escena al menú
        transform.SetParent(null); // si era hijo de Player.UI, despegarlo
        DontDestroyOnLoad(gameObject);

        StartCoroutine(FadeIn());
    }

    void Update()
    {
        if (!activo) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;
        float t = 0f;
        float dur = Mathf.Max(0.01f, duracionFadeIn);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / dur);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }
}
