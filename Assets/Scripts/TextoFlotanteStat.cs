using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Popup flotante chico que va al lado de un stat del HUD (Agua, Comida, Sueño, Batería). Se muestra
/// SOLO cuando algún script lo llama explícitamente con <see cref="Mostrar"/> — no reacciona al
/// drenaje pasivo normal de cada frame (Time.deltaTime), solo a cambios puntuales (consumir un item,
/// gastar batería en la tirolina, etc.).
///
/// "+20s" en verde para cambios positivos, "-20s" en rojo para negativos. Sube un poco y se desvanece solo.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TextoFlotanteStat : MonoBehaviour
{
    [Tooltip("Cuánto dura visible (a tamaño completo) antes de empezar a desvanecerse, en segundos.")]
    public float duracionVisible = 0.6f;
    [Tooltip("Cuánto tarda en desvanecerse una vez que empieza, en segundos.")]
    public float duracionFade = 0.5f;
    [Tooltip("Cuánto sube (en unidades locales del RectTransform) mientras se desvanece.")]
    public float desplazamientoY = 20f;
    public Color colorPositivo = Color.green;
    public Color colorNegativo = Color.red;

    private TMP_Text texto;
    private RectTransform rect;
    private Vector2 posInicial;
    private Coroutine rutinaActual;

    void Awake()
    {
        texto = GetComponent<TMP_Text>();
        rect = GetComponent<RectTransform>();
        posInicial = rect.anchoredPosition;
        texto.alpha = 0f;
    }

    /// <summary>Muestra el popup con el signo correspondiente. Ignora deltas de 0 (no hay nada que mostrar).</summary>
    public void Mostrar(float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return;

        if (rutinaActual != null) StopCoroutine(rutinaActual);
        rutinaActual = StartCoroutine(RutinaMostrar(delta));
    }

    private IEnumerator RutinaMostrar(float delta)
    {
        bool positivo = delta > 0f;
        texto.text = (positivo ? "+" : "-") + Mathf.Abs(delta).ToString("0") + "s";
        texto.color = positivo ? colorPositivo : colorNegativo;
        texto.alpha = 1f;
        rect.anchoredPosition = posInicial;

        float t = 0f;
        while (t < duracionVisible)
        {
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < duracionFade)
        {
            t += Time.deltaTime;
            float pct = Mathf.Clamp01(t / duracionFade);
            texto.alpha = 1f - pct;
            rect.anchoredPosition = posInicial + Vector2.up * (desplazamientoY * pct);
            yield return null;
        }

        texto.alpha = 0f;
        rutinaActual = null;
    }
}
