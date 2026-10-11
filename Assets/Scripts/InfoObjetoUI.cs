using UnityEngine;
using TMPro;

/// <summary>
/// Panel FIJO en pantalla que muestra la info del objeto apuntado (estilo survival:
/// Subnautica / The Long Dark). A diferencia de TooltipManager (que flotaba sobre el
/// objeto en world space y dependía del modo del Canvas), este solo enciende/apaga un
/// panel que vos posicionás donde quieras — funciona con cualquier Canvas (Overlay,
/// Screen Space Camera, etc.). Se apaga solo con un timer cuando dejás de mirar.
/// </summary>
public class InfoObjetoUI : MonoBehaviour
{
    public static InfoObjetoUI Instance;

    [Header("Referencias")]
    [Tooltip("El GameObject que se muestra u oculta (ej. el propio texto o un panel contenedor). Si se deja vacío, se usa el del campo Texto — NUNCA el Canvas entero.")]
    public GameObject panel;
    [Tooltip("Texto donde se pinta la info del objeto.")]
    public TMP_Text texto;

    [Header("Configuración")]
    [Tooltip("Segundos que sigue visible tras dejar de mirar (evita parpadeo al mover la mira).")]
    public float tiempoOcultar = 0.15f;

    [Header("Modo Flotante (opcional)")]
    [Tooltip("Si está activo, el panel se reposiciona sobre el objeto mirado (WorldToScreenPoint) en vez de quedar fijo en un lugar de la pantalla. Requiere que el Canvas esté en Screen Space - Overlay (no Camera) para calcular bien la posición.")]
    public bool flotante = false;
    [Tooltip("Cámara usada para proyectar la posición 3D a pantalla. Si se deja vacía, usa Camera.main.")]
    public Camera camara;
    [Tooltip("Altura extra (unidades) sobre el objeto si no tiene un hijo llamado 'PuntoUI'.")]
    public float alturaFlotante = 0.5f;

    private float timer;
    private RectTransform rectPanel;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        // Fallback: si no se asignó panel, usar el GameObject del texto (NUNCA el Canvas entero,
        // que apagaría toda la UI). Solo cae a este mismo objeto si tampoco hay texto asignado.
        if (panel == null) panel = texto != null ? texto.gameObject : gameObject;
        if (panel != null) panel.SetActive(false);
        if (panel != null) rectPanel = panel.GetComponent<RectTransform>();
    }

    void Update()
    {
        if (timer > 0f)
        {
            timer -= Time.unscaledDeltaTime; // unscaled: funciona aunque el juego esté pausado
            if (timer <= 0f && panel != null) panel.SetActive(false);
        }
    }

    /// <summary>
    /// Muestra el mensaje. Llamalo cada frame mientras se apunte al objeto.
    /// Si se pasa `objetivo` y `flotante` está activo, el panel se reposiciona sobre él.
    /// </summary>
    public void Mostrar(string mensaje, Transform objetivo = null)
    {
        if (panel == null || texto == null) return;
        texto.text = mensaje;
        if (!panel.activeSelf) panel.SetActive(true);
        timer = tiempoOcultar;

        if (flotante && objetivo != null) PosicionarSobre(objetivo);
    }

    private void PosicionarSobre(Transform objetivo)
    {
        if (rectPanel == null) return;
        Camera cam = camara != null ? camara : Camera.main;
        if (cam == null) return;

        // Si el objeto tiene un hijo "PuntoUI", usar esa altura (igual que TooltipManager);
        // si no, la altura fija de alturaFlotante.
        Vector3 posMundo = objetivo.position + Vector3.up * alturaFlotante;
        Transform puntoUI = objetivo.Find("PuntoUI");
        if (puntoUI != null)
        {
            float distancia = Vector3.Distance(objetivo.position, puntoUI.position);
            posMundo = objetivo.position + Vector3.up * distancia;
        }

        Vector3 screenPos = cam.WorldToScreenPoint(posMundo);
        if (screenPos.z > 0) rectPanel.position = screenPos; // solo si está delante de la cámara
    }

    /// <summary>Apaga el panel al instante (opcional).</summary>
    public void Ocultar()
    {
        timer = 0f;
        if (panel != null) panel.SetActive(false);
    }
}
