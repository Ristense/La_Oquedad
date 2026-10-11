using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance;

    [Header("Referencias de UI")]
    public GameObject canvasGroup; // El objeto principal del Canvas que se encenderá/apagará
    public TextMeshProUGUI textoTooltip; // Asigna aquí el texto de UI (opcional si usas Text clásico)
    public Text textoTooltipClasico; // Por si usas el Text de Unity normal

    [Header("Configuraciones")]
    public float distanciaAltura = 0.5f; // Altura extra si el objeto no tiene PuntoUI
    public float tiempoOcultar = 0.2f; // Retraso antes de ocultarse para evitar parpadeos

    private float timerOcultar;
    private bool estaMostrando = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        // Si no está asignado canvasGroup, intentamos auto-crear uno fallback
        if (canvasGroup == null)
        {
            CrearTooltipFallback();
        }

        OcultarInmediato();
    }

    private void CrearTooltipFallback()
    {
        Debug.LogWarning("[TooltipManager] La variable 'canvasGroup' no está asignada en el Inspector. Creando un tooltip fallback temporal en tiempo de ejecución.");

        // 1. Crear el objeto contenedor del Tooltip
        canvasGroup = new GameObject("TooltipFallback_AutoCreated", typeof(RectTransform));
        canvasGroup.transform.SetParent(this.transform, false);

        // Configurar RectTransform
        RectTransform rt = canvasGroup.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(250, 60);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // 2. Fondo simple oscuro
        Image bgImage = canvasGroup.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.75f);

        // 3. Objeto de texto
        GameObject textObj = new GameObject("Text_TMP", typeof(RectTransform));
        textObj.transform.SetParent(canvasGroup.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        textoTooltip = textObj.AddComponent<TextMeshProUGUI>();
        textoTooltip.alignment = TextAlignmentOptions.Center;
        textoTooltip.fontSize = 18;
        textoTooltip.color = Color.white;
        textoTooltip.text = "";
    }

    void Update()
    {
        // El timer siempre resta si está activo.
        // Si dejas de mirar el objeto, MostrarTooltip() dejará de llamarse,
        // el timer llegará a 0 y la UI se apagará.
        if (timerOcultar > 0)
        {
            timerOcultar -= Time.deltaTime;
            if (timerOcultar <= 0)
            {
                if (canvasGroup != null) canvasGroup.SetActive(false);
                estaMostrando = false;
            }
        }
    }

    // Se debe llamar constantemente (ej. en el Update desde el Raycast del jugador)
    public void MostrarTooltip(GameObject objetoObjetivo, string mensaje)
    {
        if (objetoObjetivo == null) return;

        // 1. Verificación Físico-Cinética (Realismo)
        Rigidbody rb = objetoObjetivo.GetComponentInParent<Rigidbody>();
        if (rb != null && rb.linearVelocity.magnitude > 0.1f && !rb.isKinematic)
        {
            // Si se está moviendo, no mostramos nada
            OcultarTooltip();
            return;
        }

        // 2. Determinar la posición
        Vector3 posicionFinal = objetoObjetivo.transform.position + (Vector3.up * distanciaAltura);

        // Buscar PuntoUI
        Transform puntoUI = objetoObjetivo.transform.Find("PuntoUI");
        if (puntoUI != null)
        {
            // Tomamos la distancia exacta del PuntoUI al centro, pero forzamos que sea hacia arriba en coordenadas globales
            float distancia = Vector3.Distance(objetoObjetivo.transform.position, puntoUI.position);
            posicionFinal = objetoObjetivo.transform.position + (Vector3.up * distancia);
        }

        // 3. Aplicar a la pantalla (Screen Space)
        Vector3 screenPos = Camera.main.WorldToScreenPoint(posicionFinal);
        
        // Solo mostrar si el objeto está delante de la cámara (z > 0)
        if (screenPos.z > 0)
        {
            // ¡Mover el Panel (canvasGroup), NO el Canvas global!
            if (canvasGroup != null)
            {
                canvasGroup.transform.position = screenPos;
                canvasGroup.SetActive(true);
            }
            
            if (textoTooltip != null) textoTooltip.text = mensaje;
            if (textoTooltipClasico != null) textoTooltipClasico.text = mensaje;

            estaMostrando = true;
            timerOcultar = tiempoOcultar; // Mantener vivo el timer mientras se mire
        }
    }

    public void OcultarTooltip()
    {
        timerOcultar = 0;
        if (canvasGroup != null) canvasGroup.SetActive(false);
        estaMostrando = false;
    }

    private void OcultarInmediato()
    {
        estaMostrando = false;
        timerOcultar = 0;
        if (canvasGroup != null) canvasGroup.SetActive(false);
    }
}
